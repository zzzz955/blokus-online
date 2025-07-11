#include "core/Session.h"
#include "handler/MessageHandler.h"
#include <spdlog/spdlog.h>
#include <openssl/rand.h>
#include <openssl/sha.h>
#include <chrono>
#include <iomanip>
#include <sstream>

namespace Blokus::Server {

    // ========================================
    // 생성자 및 소멸자
    // ========================================

    Session::Session(boost::asio::ip::tcp::socket socket)
        : socket_(std::move(socket))
        , sessionId_(generateSessionId())
        , userId_("")
        , username_("")
        , state_(ConnectionState::Connected)
        , active_(true)
        , lastActivity_(std::chrono::steady_clock::now())
        , messageHandler_(nullptr)
        , writing_(false)
    {
        spdlog::debug("Session 생성: {}, Lobby 상태로 설정", sessionId_);
    }

    Session::~Session() {
        spdlog::debug("Session 소멸: {}", sessionId_);
        if (active_.load()) {
            stop();
        }
    }

    // ========================================
    // 세션 제어
    // ========================================

    void Session::setMessageHandler(std::unique_ptr<MessageHandler> handler) {
        messageHandler_ = std::move(handler);
        spdlog::info("MessageHandler 설정 완료 - SessionId: {}", sessionId_);
    }

    void Session::start() {
        if (!active_.load()) {
            spdlog::warn("이미 비활성화된 세션 시작 시도: {}", sessionId_);
            return;
        }

        try {
            // 원격 주소 로깅
            std::string remoteAddr = getRemoteAddress();
            spdlog::info("세션 시작: {} (클라이언트: {})", sessionId_, remoteAddr);

            // 상태 업데이트
            state_ = ConnectionState::Connected;
            updateLastActivity();

            // 비동기 읽기 시작
            startRead();

        }
        catch (const std::exception& e) {
            spdlog::error("세션 시작 중 오류 ({}): {}", sessionId_, e.what());
            handleError(boost::system::error_code());
        }
    }

    void Session::stop() {
        bool expected = true;
        if (!active_.compare_exchange_strong(expected, false)) {
            return; // 이미 중지됨
        }

        spdlog::info("세션 중지: {}", sessionId_);

        try {
            // 소켓 종료
            if (socket_.is_open()) {
                boost::system::error_code ec;
                socket_.shutdown(boost::asio::ip::tcp::socket::shutdown_both, ec);
                socket_.close(ec);
                // 에러는 무시 (이미 닫혀있을 수 있음)
            }

            // 연결 해제 콜백 호출
            notifyDisconnect();

        }
        catch (const std::exception& e) {
            spdlog::error("세션 중지 중 오류 ({}): {}", sessionId_, e.what());
        }
    }

    // ========================================
    // 메시지 송수신
    // ========================================

    void Session::sendMessage(const std::string& message) {
        if (!active_.load() || !socket_.is_open()) {
            spdlog::debug("비활성 세션에 메시지 전송 시도: {}", sessionId_);
            return;
        }

        try {
            std::lock_guard<std::mutex> lock(sendMutex_);

            // 메시지를 큐에 추가
            outgoingMessages_.push(message + "\n"); // 줄바꿈으로 구분

            // 현재 쓰기 중이 아니면 쓰기 시작
            if (!writing_) {
                writing_ = true;
                doWrite();
            }

        }
        catch (const std::exception& e) {
            spdlog::error("메시지 전송 준비 중 오류 ({}): {}", sessionId_, e.what());
            handleError(boost::system::error_code());
        }
    }

    void Session::sendBinary(const std::vector<uint8_t>& data) {
        // 바이너리 데이터를 Base64나 hex로 인코딩하여 텍스트로 전송
        // 또는 별도의 바이너리 프로토콜 구현
        // 현재는 간단히 크기 정보만 전송
        std::string message = "BINARY_DATA:" + std::to_string(data.size());
        sendMessage(message);
    }

    // ========================================
    // 세션 정보 관리
    // ========================================

    void Session::setAuthenticated(const std::string& userId, const std::string& username) {
        userId_ = userId;
        username_ = username;
        state_ = ConnectionState::InLobby;
        updateLastActivity();

        spdlog::info("세션 인증 완료: {} (사용자: {})", sessionId_, username);
    }

    bool Session::isTimedOut(std::chrono::seconds timeout) const {
        auto now = std::chrono::steady_clock::now();
        auto elapsed = std::chrono::duration_cast<std::chrono::seconds>(now - lastActivity_);
        return elapsed > timeout;
    }

    std::string Session::getRemoteAddress() const {
        try {
            if (socket_.is_open()) {
                auto endpoint = socket_.remote_endpoint();
                return endpoint.address().to_string() + ":" + std::to_string(endpoint.port());
            }
        }
        catch (const std::exception&) {
            // 소켓이 이미 닫혔거나 오류 발생
        }
        return "unknown";
    }

    // ========================================
    // 비동기 읽기/쓰기
    // ========================================

    void Session::startRead() {
        if (!active_.load()) {
            return;
        }

        auto self = shared_from_this();
        socket_.async_read_some(
            boost::asio::buffer(readBuffer_),
            [this, self](const boost::system::error_code& error, size_t bytesTransferred) {
                handleRead(error, bytesTransferred);
            });
    }

    void Session::handleRead(const boost::system::error_code& error, size_t bytesTransferred) {
        if (!active_.load()) {
            return;
        }

        if (error) {
            if (error != boost::asio::error::eof &&
                error != boost::asio::error::connection_reset) {
                spdlog::error("읽기 오류 ({}): {}", sessionId_, error.message());
            }
            handleError(error);
            return;
        }

        try {
            // 수신된 데이터를 버퍼에 추가
            messageBuffer_.append(readBuffer_.data(), bytesTransferred);
            updateLastActivity();

            // 완전한 메시지들 처리
            processReceivedData();

            // 다음 읽기 시작
            startRead();

        }
        catch (const std::exception& e) {
            spdlog::error("메시지 처리 중 오류 ({}): {}", sessionId_, e.what());
            handleError(boost::system::error_code());
        }
    }

    void Session::doWrite() {
        // 뮤텍스는 이미 sendMessage()에서 잡혀있음
        if (!active_.load() || outgoingMessages_.empty()) {
            writing_ = false;
            return;
        }

        auto self = shared_from_this();
        boost::asio::async_write(
            socket_,
            boost::asio::buffer(outgoingMessages_.front()),
            [this, self](const boost::system::error_code& error, size_t bytesTransferred) {
                handleWrite(error, bytesTransferred);
            });
    }

    void Session::handleWrite(const boost::system::error_code& error, size_t bytesTransferred) {
        if (!active_.load()) {
            return;
        }

        if (error) {
            spdlog::error("쓰기 오류 ({}): {}", sessionId_, error.message());
            handleError(error);
            return;
        }

        try {
            std::lock_guard<std::mutex> lock(sendMutex_);

            // 전송 완료된 메시지 제거 - O(1) 성능!
            if (!outgoingMessages_.empty()) {
                outgoingMessages_.pop();
            }

            // 대기 중인 메시지가 있으면 계속 전송
            if (!outgoingMessages_.empty()) {
                doWrite();
            }
            else {
                writing_ = false;  // 더 이상 쓸 메시지 없음
            }

        }
        catch (const std::exception& e) {
            spdlog::error("쓰기 완료 처리 중 오류 ({}): {}", sessionId_, e.what());
            handleError(boost::system::error_code());
        }
    }

    // ========================================
    // 메시지 처리
    // ========================================

    void Session::processReceivedData() {
        // 줄바꿈으로 구분된 메시지들 처리
        size_t pos = 0;
        while ((pos = messageBuffer_.find('\n')) != std::string::npos) {
            std::string message = messageBuffer_.substr(0, pos);
            messageBuffer_.erase(0, pos + 1);

            // 빈 메시지 무시
            if (!message.empty()) {
                // \r 제거 (Windows 클라이언트 대응)
                if (!message.empty() && message.back() == '\r') {
                    message.pop_back();
                }

                processMessage(message);
            }
        }

        // 버퍼 크기 제한 (DoS 공격 방지)
        const size_t MAX_BUFFER_SIZE = 64 * 1024; // 64KB
        if (messageBuffer_.size() > MAX_BUFFER_SIZE) {
            spdlog::warn("메시지 버퍼 크기 초과 ({}): {} bytes", sessionId_, messageBuffer_.size());
            handleError(boost::system::error_code());
        }
    }

    void Session::processMessage(const std::string& message) {
        updateLastActivity();

        spdlog::debug("메시지 수신 ({}): {}", sessionId_,
            message.length() > 100 ? message.substr(0, 100) + "..." : message);

        // 메시지 핸들러를 통해 처리
        if (messageHandler_) {
            try {
                messageHandler_->handleMessage(message);
            }
            catch (const std::exception& e) {
                spdlog::error("메시지 핸들러 오류 ({}): {}", sessionId_, e.what());
                // 에러 응답 전송
                sendMessage("ERROR:Message processing failed");
            }
        }
        else {
            // 메시지 콜백 호출
            notifyMessage(message);
        }
    }

    // ========================================
    // 에러 처리 및 정리
    // ========================================

    void Session::handleError(const boost::system::error_code& error) {
        if (error && error != boost::asio::error::eof &&
            error != boost::asio::error::connection_reset) {
            spdlog::error("세션 오류 ({}): {}", sessionId_, error.message());
        }

        stop();
    }

    void Session::cleanup() {
        // 메시지 핸들러 정리
        messageHandler_.reset();

        // 버퍼 정리
        messageBuffer_.clear();

        // 큐 정리 - while 루프로 모든 요소 제거
        std::lock_guard<std::mutex> lock(sendMutex_);
        while (!outgoingMessages_.empty()) {
            outgoingMessages_.pop();
        }
        writing_ = false;
    }

    // ========================================
    // 콜백 호출
    // ========================================

    void Session::notifyDisconnect() {
        if (disconnectCallback_) {
            try {
                disconnectCallback_(sessionId_);
            }
            catch (const std::exception& e) {
                spdlog::error("연결 해제 콜백 오류 ({}): {}", sessionId_, e.what());
            }
        }
    }

    void Session::notifyMessage(const std::string& message) {
        if (messageCallback_) {
            try {
                messageCallback_(sessionId_, message);
            }
            catch (const std::exception& e) {
                spdlog::error("메시지 콜백 오류 ({}): {}", sessionId_, e.what());
            }
        }
    }

    // ========================================
    // 유틸리티 함수
    // ========================================

    std::string Session::generateSessionId() {
        // OpenSSL을 사용한 안전한 랜덤 세션 ID 생성
        unsigned char randomBytes[16]; // 128비트
        if (RAND_bytes(randomBytes, sizeof(randomBytes)) != 1) {
            // 랜덤 생성 실패 시 시간 기반 fallback
            auto now = std::chrono::high_resolution_clock::now();
            auto timestamp = now.time_since_epoch().count();
            return "session_" + std::to_string(timestamp);
        }

        // 바이트를 16진수 문자열로 변환
        std::stringstream ss;
        ss << std::hex << std::setfill('0');
        for (int i = 0; i < sizeof(randomBytes); ++i) {
            ss << std::setw(2) << static_cast<unsigned>(randomBytes[i]);
        }

        return "sess_" + ss.str();
    }

} // namespace Blokus::Server