#pragma once

#include "ServerTypes.h"  // ConnectionState�� ���⿡ ���ǵ�
#include <boost/asio.hpp>
#include <spdlog/spdlog.h>
#include <memory>
#include <string>
#include <atomic>
#include <chrono>
#include <queue>
#include <mutex>
#include <functional>
#include <vector>

// ���� ����
namespace Blokus::Server {
    class MessageHandler;
}

namespace Blokus::Server {

    // ========================================
    // Session Ŭ���� (���� ������Ʈ ���� ���)
    // ========================================
    class Session : public std::enable_shared_from_this<Session> {
    public:
        // �ݹ� Ÿ�� ���� (���� ������Ʈ ���)
        using SessionEventCallback = std::function<void(const std::string&)>;
        using MessageEventCallback = std::function<void(const std::string&, const std::string&)>;

        // ������/�Ҹ���
        explicit Session(boost::asio::ip::tcp::socket socket);
        ~Session();

        // ���� ����
        void start();
        void stop();
        bool isActive() const { return active_.load(); }

        // �޽��� �ڵ鷯 ����
        void setMessageHandler(std::unique_ptr<MessageHandler> handler);
        MessageHandler* getMessageHandler() const { return messageHandler_.get(); }

        // �⺻ ���� ������
        const std::string& getSessionId() const { return sessionId_; }
        const std::string& getUserId() const { return userId_; }
        const std::string& getUsername() const { return username_; }
        ConnectionState getState() const { return state_; }
        int getCurrentRoomId() const { return currentRoomId_; }

        // ���� Ȯ�� �Լ��� (���� Session.h ���)
        bool isConnected() const { return state_ >= ConnectionState::Connected; }
        bool isInLobby() const { return state_ == ConnectionState::InLobby; }
        bool isInRoom() const { return state_ == ConnectionState::InRoom; }
        bool isInGame() const { return state_ == ConnectionState::InGame; }

        // ���� Ȯ�� �Լ��� (����Ͻ� ������)
        bool canCreateRoom() const { return isInLobby(); }
        bool canJoinRoom() const { return isInLobby(); }
        bool canLeaveRoom() const { return isInRoom() || isInGame(); }
        bool canStartGame() const { return isInRoom(); }
        bool canMakeGameMove() const { return isInGame(); }

        // ���� ���� �Լ���
        void setStateToConnected();
        void setStateToLobby();
        void setStateToInRoom(int roomId = -1);
        void setStateToInGame();

        // ���� ����
        void setAuthenticated(const std::string& userId, const std::string& username);

        // �޽��� �ۼ���
        void sendMessage(const std::string& message);
        void sendBinary(const std::vector<uint8_t>& data);

        // �ݹ� ���� (���� ������Ʈ ���)
        void setDisconnectCallback(SessionEventCallback callback) { disconnectCallback_ = callback; }
        void setMessageCallback(MessageEventCallback callback) { messageCallback_ = callback; }

        // Ȱ�� ����
        void updateLastActivity() { lastActivity_ = std::chrono::steady_clock::now(); }
        bool isTimedOut(std::chrono::seconds timeout) const;
        std::chrono::steady_clock::time_point getLastActivity() const { return lastActivity_; }

        // ��Ʈ��ũ ����
        std::string getRemoteAddress() const;
        boost::asio::ip::tcp::socket& getSocket() { return socket_; }  // GameServer���� �ʿ�

        // ��� �� �����
        size_t getPendingMessageCount() const {
            std::lock_guard<std::mutex> lock(sendMutex_);
            return outgoingMessages_.size();
        }

    private:
        // ��Ʈ��ũ ����
        boost::asio::ip::tcp::socket socket_;
        static constexpr size_t MAX_MESSAGE_LENGTH = 8192;
        char readBuffer_[MAX_MESSAGE_LENGTH];
        std::string messageBuffer_;

        // ���� ����
        std::string sessionId_;
        std::string userId_;
        std::string username_;
        ConnectionState state_;
        int currentRoomId_;
        std::atomic<bool> active_;
        std::chrono::steady_clock::time_point lastActivity_;

        // �޽��� ó��
        std::unique_ptr<MessageHandler> messageHandler_;

        // �񵿱� ���� ����
        mutable std::mutex sendMutex_;
        std::queue<std::string> outgoingMessages_;
        bool writing_;

        // �ݹ�
        SessionEventCallback disconnectCallback_;
        MessageEventCallback messageCallback_;

        // ���� ��Ʈ��ũ �Լ���
        void startRead();
        void handleRead(const boost::system::error_code& error, size_t bytesTransferred);
        void doWrite();
        void handleWrite(const boost::system::error_code& error, size_t bytesTransferred);

        // �޽��� ó��
        void processMessage(const std::string& message);
        void handleError(const boost::system::error_code& error);
        void cleanup();

        // �ݹ� ȣ��
        void notifyDisconnect();
        void notifyMessage(const std::string& message);

        // ��ƿ��Ƽ
        std::string generateSessionId();
    };

    // ========================================
    // ���� �Լ��� (���� ServerTypes.h�� �̹� ����)
    // ========================================
    // connectionStateToString�� ServerTypes.h�� ���ǵ�

} // namespace Blokus::Server