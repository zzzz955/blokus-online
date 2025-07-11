#pragma once

#include <memory>
#include <string>
#include <atomic>
#include <chrono>
#include <queue>
#include <boost/asio.hpp>
#include <spdlog/spdlog.h>

// ���� ���� ���
#include "common/ServerTypes.h"

namespace Blokus::Server {

    // ���� ����  
    class MessageHandler;

    // ���� �̺�Ʈ �ݹ� Ÿ�Ե�
    using SessionEventCallback = std::function<void(const std::string& sessionId)>;
    using MessageEventCallback = std::function<void(const std::string& sessionId, const std::string& message)>;

    // ServerTypes.h���� ConnectionState ���

    // Ŭ���̾�Ʈ ���� Ŭ����
    class Session : public std::enable_shared_from_this<Session> {
    public:
        explicit Session(boost::asio::ip::tcp::socket socket);
        ~Session();

        // ���� ����
        void setMessageHandler(std::unique_ptr<MessageHandler> handler);
        void start();
        void stop();
        bool isActive() const { return active_.load(); }

        // �ݹ� ���� (GameServer���� ����)
        void setDisconnectCallback(SessionEventCallback callback) { disconnectCallback_ = callback; }
        void setMessageCallback(MessageEventCallback callback) { messageCallback_ = callback; }

        // �޽��� �ۼ���
        void sendMessage(const std::string& message);
        void sendBinary(const std::vector<uint8_t>& data);

        // ���� ����
        const std::string& getSessionId() const { return sessionId_; }
        const std::string& getUserId() const { return userId_; }
        const std::string& getUsername() const { return username_; }
        ConnectionState getState() const { return state_; }

        // ���� ����
        void setAuthenticated(const std::string& userId, const std::string& username);
        bool isAuthenticated() const { return state_ >= ConnectionState::Authenticated; }

        void setState(ConnectionState state) { state_ = state; }
        void updateLastActivity() { lastActivity_ = std::chrono::steady_clock::now(); }

        // ��Ʈ��Ʈ üũ
        bool isTimedOut(std::chrono::seconds timeout) const;

        // ���� ����
        boost::asio::ip::tcp::socket& getSocket() { return socket_; }
        std::string getRemoteAddress() const;

        // MessageHandler ����
        MessageHandler* getMessageHandler() const { return messageHandler_.get(); }

    private:
        // �񵿱� �б�/����
        void startRead();
        void handleRead(const boost::system::error_code& error, size_t bytesTransferred);
        void handleWrite(const boost::system::error_code& error, size_t bytesTransferred);
        void doWrite();

        // �޽��� ó��
        void processMessage(const std::string& message);
        void processReceivedData();

        // ���� ó��  
        void handleError(const boost::system::error_code& error);
        void cleanup();

        // �ݹ� ȣ��
        void notifyDisconnect();
        void notifyMessage(const std::string& message);

        // ��ƿ��Ƽ
        std::string generateSessionId();

    private:
        // ��Ʈ��ũ
        boost::asio::ip::tcp::socket socket_;

        // �ݹ� �Լ��� (GameServer ���� ���� ���)
        SessionEventCallback disconnectCallback_;
        MessageEventCallback messageCallback_;

        // ���� ����
        std::string sessionId_;
        std::string userId_;
        std::string username_;
        ConnectionState state_;
        std::atomic<bool> active_{ true };

        // �ð� ����
        std::chrono::steady_clock::time_point lastActivity_;

        // ���� ����
        std::array<char, 8192> readBuffer_;
        std::string messageBuffer_;
        std::queue<std::string> outgoingMessages_;  // ���� ��� ť ���
        std::mutex sendMutex_;
        bool writing_{ false };  // ���� ���� ������ �÷���

        // �޽��� �ڵ鷯
        std::unique_ptr<MessageHandler> messageHandler_;
    };

} // namespace Blokus::Server