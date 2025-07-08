#pragma once

#include "ServerTypes.h"
#include <boost/asio.hpp>
#include <boost/enable_shared_from_this.hpp>
#include <string>
#include <memory>
#include <atomic>
#include <chrono>
#include <queue>
#include <mutex>

namespace Blokus {
    namespace Server {

        class ClientSession : public std::enable_shared_from_this<ClientSession> {
        public:
            explicit ClientSession(boost::asio::io_context& ioContext);
            ~ClientSession();

            // ��Ʈ��ũ �������̽�
            void start();
            void disconnect();
            void sendMessage(const std::string& message);

            // ���� ����
            boost::asio::ip::tcp::socket& socket() { return socket_; }

            // Ŭ���̾�Ʈ ����
            const std::string& getSessionId() const { return sessionId_; }
            const std::string& getUserId() const { return userId_; }
            ClientState getState() const { return state_; }
            std::string getRemoteAddress() const;

            // ���� ����
            void setState(ClientState state) { state_ = state; }
            void setUserId(const std::string& userId) { userId_ = userId; }
            void setRoomId(int roomId) { roomId_ = roomId; }
            int getRoomId() const { return roomId_; }

            // ���� ����
            bool isConnected() const { return connected_; }
            std::chrono::steady_clock::time_point getLastActivity() const { return lastActivity_; }
            void updateActivity() { lastActivity_ = std::chrono::steady_clock::now(); }

        private:
            // �񵿱� I/O ó��
            void startReading();
            void handleRead(const boost::system::error_code& error, size_t bytesTransferred);
            void handleWrite(const boost::system::error_code& error, size_t bytesTransferred);

            // �޽��� ó��
            void processMessage(const std::string& message);
            void scheduleWrite();

            // ����
            void cleanup();

        private:
            boost::asio::ip::tcp::socket socket_;
            std::string sessionId_;
            std::string userId_;
            ClientState state_;
            int roomId_;

            // ���� ����
            std::atomic<bool> connected_{ false };
            std::chrono::steady_clock::time_point lastActivity_;

            // �޽��� ����
            std::array<char, MESSAGE_BUFFER_SIZE> readBuffer_;
            std::string readMessage_;

            // ���� ť
            std::queue<std::string> writeQueue_;
            std::mutex writeMutex_;
            std::atomic<bool> writing_{ false };

            // ���
            std::atomic<size_t> messagesSent_{ 0 };
            std::atomic<size_t> messagesReceived_{ 0 };
            std::atomic<size_t> bytesSent_{ 0 };
            std::atomic<size_t> bytesReceived_{ 0 };
        };

    } // namespace Server
} // namespace Blokus