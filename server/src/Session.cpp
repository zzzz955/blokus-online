#include "server/Session.h"
#include "server/MessageHandler.h"
#include "server/GameServer.h"
#include <nlohmann/json.hpp>

namespace Blokus {
    namespace Server {

        // ========================================
        // Session Ŭ���� ����
        // ========================================

        Session::Session(boost::asio::io_context& ioContext, uint32_t sessionId)
            : m_sessionId(sessionId)
            , m_socket(ioContext)
            , m_lastActivity(std::chrono::steady_clock::now())
        {
            spdlog::debug("���� {} ������", m_sessionId);
            m_messageHandler = std::make_unique<MessageHandler>(shared_from_this());
        }

        Session::~Session() {
            spdlog::debug("���� {} �Ҹ��", m_sessionId);
            if (m_isConnected) {
                close();
            }
        }

        void Session::start() {
            if (m_isConnected.exchange(true)) {
                spdlog::warn("���� {}�� �̹� ���۵�", m_sessionId);
                return;
            }

            spdlog::info("���� {} ����: {}", m_sessionId, getRemoteAddress());
            updateActivity();
            startRead();
        }

        void Session::close() {
            if (!m_isConnected.exchange(false)) {
                return; // �̹� ����
            }

            spdlog::info("���� {} ����: {}", m_sessionId, getRemoteAddress());

            try {
                if (m_socket.is_open()) {
                    boost::system::error_code ec;
                    m_socket.shutdown(boost::asio::ip::tcp::socket::shutdown_both, ec);
                    m_socket.close(ec);
                }
            }
            catch (const std::exception& e) {
                spdlog::warn("���� {} ���� �� ����: {}", m_sessionId, e.what());
            }

            // TODO: �������� ���� ���� �˸�
        }

        std::string Session::getRemoteAddress() const {
            try {
                if (m_socket.is_open()) {
                    auto endpoint = m_socket.remote_endpoint();
                    return endpoint.address().to_string() + ":" + std::to_string(endpoint.port());
                }
            }
            catch (const std::exception&) {
                // ������ �̹� �����ų� ���� ����
            }
            return "unknown";
        }

        void Session::sendMessage(const std::string& message) {
            if (!m_isConnected) {
                spdlog::warn("���� {}�� ������� ���� - �޽��� ���� ����", m_sessionId);
                return;
            }

            std::lock_guard<std::mutex> lock(m_writeQueueMutex);

            // �޽��� ���� ��� �߰� (4����Ʈ ��Ʋ�����)
            uint32_t messageLen = static_cast<uint32_t>(message.length());
            std::string packet;
            packet.resize(4 + message.length());

            // ���� ��� �ۼ�
            packet[0] = static_cast<char>(messageLen & 0xFF);
            packet[1] = static_cast<char>((messageLen >> 8) & 0xFF);
            packet[2] = static_cast<char>((messageLen >> 16) & 0xFF);
            packet[3] = static_cast<char>((messageLen >> 24) & 0xFF);

            // �޽��� ������ ����
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
                    spdlog::warn("���� {} �б� ����: {}", m_sessionId, error.message());
                }
                close();
                return;
            }

            if (bytesTransferred == 0) {
                spdlog::info("���� {} Ŭ���̾�Ʈ�� ������ ����", m_sessionId);
                close();
                return;
            }

            updateActivity();

            // ���ŵ� �����͸� ���ۿ� �߰�
            m_messageBuffer.append(m_readBuffer.data(), bytesTransferred);

            // ������ �޽����� ó��
            processBuffer();

            // ���� �б� ����
            startRead();
        }

        void Session::processBuffer() {
            while (m_messageBuffer.length() >= 4) {
                // �޽��� ���� �б� (4����Ʈ ��Ʋ�����)
                uint32_t messageLen =
                    static_cast<uint8_t>(m_messageBuffer[0]) |
                    (static_cast<uint8_t>(m_messageBuffer[1]) << 8) |
                    (static_cast<uint8_t>(m_messageBuffer[2]) << 16) |
                    (static_cast<uint8_t>(m_messageBuffer[3]) << 24);

                // �޽��� ���� ����
                if (messageLen > MAX_MESSAGE_SIZE - 4) {
                    spdlog::error("���� {} �޽��� ���� �ʰ�: {}", m_sessionId, messageLen);
                    close();
                    return;
                }

                // ��ü �޽����� �����ߴ��� Ȯ��
                if (m_messageBuffer.length() < 4 + messageLen) {
                    break; // �� ���� ������ �ʿ�
                }

                // �޽��� ����
                std::string message = m_messageBuffer.substr(4, messageLen);
                m_messageBuffer.erase(0, 4 + messageLen);

                // �޽��� ó��
                try {
                    processMessage(message);
                }
                catch (const std::exception& e) {
                    spdlog::error("���� {} �޽��� ó�� ����: {}", m_sessionId, e.what());
                }
            }
        }

        void Session::processMessage(const std::string& message) {
            spdlog::debug("���� {} �޽��� ����: {}", m_sessionId,
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
                spdlog::warn("���� {} ���� ����: {}", m_sessionId, error.message());
                close();
                return;
            }

            if (!m_writeQueue.empty()) {
                m_writeQueue.pop();
            }

            // ��� ���� �޽����� ������ ��� ����
            if (!m_writeQueue.empty()) {
                doWrite();
            }
        }

        void Session::updateActivity() {
            m_lastActivity = std::chrono::steady_clock::now();
        }

        // ========================================
        // MessageHandler�� ���� ���Ͽ��� ������
        // ========================================

    } // namespace Server
} // namespace Blokus