#pragma once

#include <memory>
#include <string>
#include <queue>
#include <mutex>
#include <atomic>
#include <chrono>

#include <boost/asio.hpp>
#include <spdlog/spdlog.h>

#include "common/Types.h"
#include "common/ServerTypes.h"

namespace Blokus {
    namespace Server {

        // ���� ���� (��ȯ ���� ����)
        class MessageHandler;

        // Ŭ���̾�Ʈ ������ ��Ÿ���� ���� Ŭ����
        class Session : public std::enable_shared_from_this<Session> {
        public:
            Session(boost::asio::io_context& ioContext, uint32_t sessionId);
            ~Session();

            // ���� �����ֱ� ����
            void start();                                          // ���� ����
            void close();                                          // ���� ����

            // �޽��� �ۼ��� (Protobuf ���)
            void sendMessage(const std::string& serializedMessage); // ����ȭ�� �޽��� ����
            void sendMessage(const std::vector<uint8_t>& data);   // ���̳ʸ� ������ ����

            // ���� ���� ��ȸ
            uint32_t getId() const { return m_sessionId; }
            std::string getRemoteAddress() const;
            bool isConnected() const { return m_isConnected; }
            std::chrono::steady_clock::time_point getLastActivity() const { return m_lastActivity; }

            // ����� ���� (�α��� �� ����)
            void setUserId(uint32_t userId) { m_userId = userId; }
            uint32_t getUserId() const { return m_userId; }
            void setUsername(const std::string& username) { m_username = username; }
            const std::string& getUsername() const { return m_username; }

            // �� ����
            void setRoomId(uint32_t roomId) { m_currentRoomId = roomId; }
            uint32_t getRoomId() const { return m_currentRoomId; }
            bool isInRoom() const { return m_currentRoomId != 0; }

            // ���� ���� (�������� ���)
            boost::asio::ip::tcp::socket& getSocket() { return m_socket; }

            // �޽��� �ڵ鷯 ���� (������ ����)
            void setMessageHandler(std::shared_ptr<MessageHandler> handler) { m_messageHandler = handler; }

        private:
            // ��Ʈ��ũ I/O ó��
            void startRead();                                      // �б� ����
            void handleRead(const boost::system::error_code& error, // �б� �Ϸ� ó��
                size_t bytesTransferred);
            void handleWrite(const boost::system::error_code& error, // ���� �Ϸ� ó��
                size_t bytesTransferred);

            // �޽��� ó�� (Protobuf)
            void processMessage(const std::vector<uint8_t>& messageData); // ���� �޽��� ó��
            void processBuffer();                                  // ���� ���� ó��

            // ť ����
            void doWrite();                                        // �۽� ť ó��
            void updateActivity();                                 // ������ Ȱ�� �ð� ����

        private:
            // �⺻ ����
            uint32_t m_sessionId;                                  // ���� ���� ID
            boost::asio::ip::tcp::socket m_socket;                // TCP ����
            std::atomic<bool> m_isConnected{ false };             // ���� ����

            // ����� ����
            uint32_t m_userId{ 0 };                               // ����� ID (�α��� ��)
            std::string m_username;                               // ����ڸ�
            uint32_t m_currentRoomId{ 0 };                        // ���� �� ID

            // ��Ʈ��ũ ����
            static constexpr size_t MAX_MESSAGE_SIZE = Server::MAX_MESSAGE_SIZE; // �ִ� �޽��� ũ��
            std::array<char, MAX_MESSAGE_SIZE> m_readBuffer;      // �б� ����
            std::vector<uint8_t> m_messageBuffer;                 // �޽��� ���� ����

            // �۽� ť
            std::queue<std::vector<uint8_t>> m_writeQueue;        // �۽� �޽��� ť (���̳ʸ�)
            std::mutex m_writeQueueMutex;                         // �۽� ť ��ȣ ���ؽ�
            std::atomic<bool> m_isWriting{ false };               // �۽� �� ����

            // Ȱ�� ����
            std::chrono::steady_clock::time_point m_lastActivity; // ������ Ȱ�� �ð�

            // �޽��� �ڵ鷯 (������ ����)
            std::shared_ptr<MessageHandler> m_messageHandler;     // �޽��� ó����
        };

    } // namespace Server
} // namespace Blokus