#include "Session.h"
#include "MessageHandler.h"
#include <openssl/rand.h>
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
        , currentRoomId_(-1)
        , active_(true)
        , lastActivity_(std::chrono::steady_clock::now())
        , messageHandler_(nullptr)
        , writing_(false)
    {
        spdlog::debug("🔌 세션 생성: {} (상태: Connected)", sessionId_);
    }

    Session::~Session() {
        spdlog::debug("🔌 세션 소멸: {}", sessionId_);
        if (active_.load()) {
            stop();
        }
        cleanup();
    }

    // ========================================
    // 세션 제어
    // ========================================

    void Session::setMessageHandler(std::unique_ptr<MessageHandler> handler) {
        messageHandler_ = std::move(handler);
        spdlog::debug("📨 MessageHandler 설정 완료: {}", sessionId_);
    }

    void Session::start() {
        if (!active_.load()) {
            spdlog::warn("❌ 이미 비활성화된 세션 시작 시도: {}", sessionId_);
            return;
        }

        try {
            std::string remoteAddr = getRemoteAddress();
            spdlog::info("🔌 세션 시작: {} (클라이언트: {})", sessionId_, remoteAddr);

            state_ = ConnectionState::Connected;
            updateLastActivity();
            startRead();

        }
        catch (const std::exception& e) {
            spdlog::error("❌ 세션 시작 중 오류 ({}): {}", sessionId_, e.what());
            handleError(boost::system::error_code());
        }
    }

    void Session::stop() {
        bool expected = true;
        if (!active_.compare_exchange_strong(expected, false)) {
            return; // 이미 중지됨
        }

        spdlog::info("🔌 세션 중지: {}", sessionId_);

        try {
            if (socket_.is_open()) {
                boost::system::error_code ec;
                socket_.shutdown(boost::asio::ip::tcp::socket::shutdown_both, ec);
                socket_.close(ec);
            }

            notifyDisconnect();

        }
        catch (const std::exception& e) {
            spdlog::error("❌ 세션 중지 중 오류 ({}): {}", sessionId_, e.what());
        }
    }

    // ========================================
    // 상태 관리 (기존 프로젝트 구조 기반)
    // ========================================

    void Session::setAuthenticated(const std::string& userId, const std::string& username) {
        userId_ = userId;
        username_ = username;
        state_ = ConnectionState::InLobby;  // 인증 완료 즉시 로비로
        currentRoomId_ = -1;
        updateLastActivity();

        spdlog::info("✅ 세션 인증 완료: {} (사용자: '{}')", sessionId_, username);
    }

    void Session::setStateToConnected() {
        state_ = ConnectionState::Connected;
        updateLastActivity();

        spdlog::debug("✅ 세션 상태 변경: {} -> 로그인 화면", sessionId_);
    }

    void Session::setStateToLobby() {
        state_ = ConnectionState::InLobby;
        currentRoomId_ = -1;
        updateLastActivity();

        spdlog::debug("🏠 세션 상태 변경: {} -> 로비", sessionId_);
    }

    void Session::setStateToInRoom(int roomId) {
        state_ = ConnectionState::InRoom;
        currentRoomId_ = roomId;
        updateLastActivity();

        spdlog::debug("🏠 세션 상태 변경: {} -> 방 {}", sessionId_, roomId);
    }

    void Session::setStateToInGame() {
        if (state_ == ConnectionState::InRoom) {
            state_ = ConnectionState::InGame;
            updateLastActivity();

            spdlog::debug("🎮 세션 상태 변경: {} -> 게임 중 (방 {})", sessionId_, currentRoomId_);
        }
        else {
            spdlog::warn("❌ 잘못된 상태에서 게임 상태로 변경 시도: {} (현재: {})",
                sessionId_, static_cast<int>(state_));
        }
    }

    // ========================================
    // 메시지 송수신
    // ========================================

    void Session::sendMessage(const std::string& message) {
        if (!active_.load() || !socket_.is_open()) {
            spdlog::debug("❌ 비활성 세션에 메시지 전송 시도: {}", sessionId_);
            return;
        }

        try {
            std::lock_guard<std::mutex> lock(sendMutex_);

            outgoingMessages_.push(message + "\n");

            if (!writing_) {
                writing_ = true;
                // spdlog::debug("📤 쓰기 시작");
                doWrite();
            }
            else {
                // spdlog::debug("📤 쓰기 대기 중");
            }

        }
        catch (const std::exception& e) {
            spdlog::error("❌ 메시지 전송 준비 중 오류 ({}): {}", sessionId_, e.what());
            handleError(boost::system::error_code());
        }
    }

    void Session::sendBinary(const std::vector<uint8_t>& data) {
        // 현재는 바이너리 데이터를 텍스트로 변환하여 전송
        std::string message = "BINARY_DATA:" + std::to_string(data.size());
        sendMessage(message);
    }

    // ========================================
    // 활동 추적
    // ========================================

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
    // 내부 네트워크 함수들
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

        if (!error) {
            messageBuffer_.append(readBuffer_, bytesTransferred);
            updateLastActivity();

            // 줄바꿈으로 구분된 메시지 처리
            size_t pos = 0;
            while ((pos = messageBuffer_.find('\n')) != std::string::npos) {
                std::string message = messageBuffer_.substr(0, pos);
                messageBuffer_.erase(0, pos + 1);

                if (!message.empty()) {
                    processMessage(message);
                }
            }

            startRead();
        }
        else {
            handleError(error);
        }
    }

    void Session::doWrite() {
        if (!active_.load() || outgoingMessages_.empty()) {
            writing_ = false;
            return;
        }

        auto self = shared_from_this();
        const std::string& message = outgoingMessages_.front();

        boost::asio::async_write(socket_,
            boost::asio::buffer(message),
            [this, self](const boost::system::error_code& error, size_t /*bytesTransferred*/) {
                handleWrite(error, 0);
            });
    }

    void Session::handleWrite(const boost::system::error_code& error, size_t /*bytesTransferred*/) {
        if (!active_.load()) {
            return;
        }

        {
            std::lock_guard<std::mutex> lock(sendMutex_);
            if (!outgoingMessages_.empty()) {
                outgoingMessages_.pop();
            }
        }

        if (!error) {
            std::lock_guard<std::mutex> lock(sendMutex_);
            if (!outgoingMessages_.empty()) {
                doWrite();
            }
            else {
                writing_ = false;
            }
        }
        else {
            handleError(error);
        }
    }

    // ========================================
    // 메시지 처리
    // ========================================

    void Session::processMessage(const std::string& message) {
        spdlog::debug("📨 메시지 처리 시작: {}", message);
        if (messageHandler_) {
            try {
                messageHandler_->handleMessage(message);
            }
            catch (const std::exception& e) {
                spdlog::error("❌ 메시지 핸들러 오류 ({}): {}", sessionId_, e.what());
                sendMessage("ERROR:Message processing failed");
            }
            spdlog::debug("📨 메시지 처리 완료: {}", message);
        }
        else {
            notifyMessage(message);
        }
    }

    void Session::handleError(const boost::system::error_code& error) {
        if (error && error != boost::asio::error::eof &&
            error != boost::asio::error::connection_reset) {
            spdlog::error("❌ 세션 오류 ({}): {}", sessionId_, error.message());
        }

        stop();
    }

    void Session::cleanup() {
        messageHandler_.reset();
        messageBuffer_.clear();

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
                spdlog::error("❌ 연결 해제 콜백 오류 ({}): {}", sessionId_, e.what());
            }
        }
    }

    void Session::notifyMessage(const std::string& message) {
        if (messageCallback_) {
            try {
                messageCallback_(sessionId_, message);
            }
            catch (const std::exception& e) {
                spdlog::error("❌ 메시지 콜백 오류 ({}): {}", sessionId_, e.what());
            }
        }
    }

    // ========================================
    // 유틸리티
    // ========================================

    std::string Session::generateSessionId() {
        unsigned char randomBytes[16];
        if (RAND_bytes(randomBytes, sizeof(randomBytes)) != 1) {
            auto now = std::chrono::high_resolution_clock::now();
            auto timestamp = now.time_since_epoch().count();
            return "session_" + std::to_string(timestamp);
        }

        std::stringstream ss;
        ss << std::hex << std::setfill('0');
        for (int i = 0; i < sizeof(randomBytes); ++i) {
            ss << std::setw(2) << static_cast<unsigned>(randomBytes[i]);
        }

        return "sess_" + ss.str();
    }

} // namespace Blokus::Server