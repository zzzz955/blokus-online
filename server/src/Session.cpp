#include "server/Session.h"
#include "server/MessageHandler.h"
#include "server/GameServer.h"
#include <nlohmann/json.hpp>

namespace Blokus {
    namespace Server {

        // ========================================
        // Session 클래스 구현
        // ========================================

        Session::Session(boost::asio::io_context& ioContext, uint32_t sessionId)
            : m_sessionId(sessionId)
            , m_socket(ioContext)
            , m_lastActivity(std::chrono::steady_clock::now())
        {
            spdlog::debug("세션 {} 생성됨", m_sessionId);
            m_messageHandler = std::make_unique<MessageHandler>(shared_from_this());
        }

        Session::~Session() {
            spdlog::debug("세션 {} 소멸됨", m_sessionId);
            if (m_isConnected) {
                close();
            }
        }

        void Session::start() {
            if (m_isConnected.exchange(true)) {
                spdlog::warn("세션 {}이 이미 시작됨", m_sessionId);
                return;
            }

            spdlog::info("세션 {} 시작: {}", m_sessionId, getRemoteAddress());
            updateActivity();
            startRead();
        }

        void Session::close() {
            if (!m_isConnected.exchange(false)) {
                return; // 이미 닫힘
            }

            spdlog::info("세션 {} 종료: {}", m_sessionId, getRemoteAddress());

            try {
                if (m_socket.is_open()) {
                    boost::system::error_code ec;
                    m_socket.shutdown(boost::asio::ip::tcp::socket::shutdown_both, ec);
                    m_socket.close(ec);
                }
            }
            catch (const std::exception& e) {
                spdlog::warn("세션 {} 종료 중 오류: {}", m_sessionId, e.what());
            }

            // TODO: 서버에서 세션 제거 알림
        }

        std::string Session::getRemoteAddress() const {
            try {
                if (m_socket.is_open()) {
                    auto endpoint = m_socket.remote_endpoint();
                    return endpoint.address().to_string() + ":" + std::to_string(endpoint.port());
                }
            }
            catch (const std::exception&) {
                // 소켓이 이미 닫혔거나 오류 상태
            }
            return "unknown";
        }

        void Session::sendMessage(const std::string& message) {
            if (!m_isConnected) {
                spdlog::warn("세션 {}이 연결되지 않음 - 메시지 전송 무시", m_sessionId);
                return;
            }

            std::lock_guard<std::mutex> lock(m_writeQueueMutex);

            // 메시지 길이 헤더 추가 (4바이트 리틀엔디안)
            uint32_t messageLen = static_cast<uint32_t>(message.length());
            std::string packet;
            packet.resize(4 + message.length());

            // 길이 헤더 작성
            packet[0] = static_cast<char>(messageLen & 0xFF);
            packet[1] = static_cast<char>((messageLen >> 8) & 0xFF);
            packet[2] = static_cast<char>((messageLen >> 16) & 0xFF);
            packet[3] = static_cast<char>((messageLen >> 24) & 0xFF);

            // 메시지 데이터 복사
            std::copy(message.begin(), message.end(), packet.begin() + 4);

            bool wasEmpty = m_writeQueue.empty();
            m_writeQueue.push(packet);

            if (wasEmpty && !m_isWriting) {
                doWrite();
            }
        }

        void Session::sendMessage(const std::vector<uint8_t>& data) {
            std::string message(data.begin(), data.end());
            sendMessage(message);
        }

        void Session::startRead() {
            if (!m_isConnected) {
                return;
            }

            auto self = shared_from_this();
            m_socket.async_read_some(
                boost::asio::buffer(m_readBuffer),
                [this, self](const boost::system::error_code& error, size_t bytesTransferred) {
                    handleRead(error, bytesTransferred);
                });
        }

        void Session::handleRead(const boost::system::error_code& error, size_t bytesTransferred) {
            if (error) {
                if (error != boost::asio::error::eof) {
                    spdlog::warn("세션 {} 읽기 오류: {}", m_sessionId, error.message());
                }
                close();
                return;
            }

            if (bytesTransferred == 0) {
                spdlog::info("세션 {} 클라이언트가 연결을 닫음", m_sessionId);
                close();
                return;
            }

            updateActivity();

            // 수신된 데이터를 버퍼에 추가
            m_messageBuffer.append(m_readBuffer.data(), bytesTransferred);

            // 완전한 메시지들 처리
            processBuffer();

            // 다음 읽기 시작
            startRead();
        }

        void Session::processBuffer() {
            while (m_messageBuffer.length() >= 4) {
                // 메시지 길이 읽기 (4바이트 리틀엔디안)
                uint32_t messageLen =
                    static_cast<uint8_t>(m_messageBuffer[0]) |
                    (static_cast<uint8_t>(m_messageBuffer[1]) << 8) |
                    (static_cast<uint8_t>(m_messageBuffer[2]) << 16) |
                    (static_cast<uint8_t>(m_messageBuffer[3]) << 24);

                // 메시지 길이 검증
                if (messageLen > MAX_MESSAGE_SIZE - 4) {
                    spdlog::error("세션 {} 메시지 길이 초과: {}", m_sessionId, messageLen);
                    close();
                    return;
                }

                // 전체 메시지가 도착했는지 확인
                if (m_messageBuffer.length() < 4 + messageLen) {
                    break; // 더 많은 데이터 필요
                }

                // 메시지 추출
                std::string message = m_messageBuffer.substr(4, messageLen);
                m_messageBuffer.erase(0, 4 + messageLen);

                // 메시지 처리
                try {
                    processMessage(message);
                }
                catch (const std::exception& e) {
                    spdlog::error("세션 {} 메시지 처리 오류: {}", m_sessionId, e.what());
                }
            }
        }

        void Session::processMessage(const std::string& message) {
            spdlog::debug("세션 {} 메시지 수신: {}", m_sessionId,
                message.length() > 100 ? message.substr(0, 100) + "..." : message);

            if (m_messageHandler) {
                m_messageHandler->handleMessage(message);
            }
        }

        void Session::doWrite() {
            if (!m_isConnected) {
                return;
            }

            std::lock_guard<std::mutex> lock(m_writeQueueMutex);

            if (m_writeQueue.empty() || m_isWriting) {
                return;
            }

            m_isWriting = true;
            auto self = shared_from_this();
            auto message = m_writeQueue.front();

            boost::asio::async_write(
                m_socket,
                boost::asio::buffer(message),
                [this, self](const boost::system::error_code& error, size_t bytesTransferred) {
                    handleWrite(error, bytesTransferred);
                });
        }

        void Session::handleWrite(const boost::system::error_code& error, size_t bytesTransferred) {
            std::lock_guard<std::mutex> lock(m_writeQueueMutex);
            m_isWriting = false;

            if (error) {
                spdlog::warn("세션 {} 쓰기 오류: {}", m_sessionId, error.message());
                close();
                return;
            }

            if (!m_writeQueue.empty()) {
                m_writeQueue.pop();
            }

            // 대기 중인 메시지가 있으면 계속 전송
            if (!m_writeQueue.empty()) {
                doWrite();
            }
        }

        void Session::updateActivity() {
            m_lastActivity = std::chrono::steady_clock::now();
        }

        // ========================================
        // MessageHandler는 별도 파일에서 구현됨
        // ========================================

    } // namespace Server
} // namespace Blokus