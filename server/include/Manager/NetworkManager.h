#pragma once

#include <memory>
#include <string>
#include <vector>
#include <functional>
#include <boost/asio.hpp>

namespace Blokus::Server {

    // ���� ����
    class Session;
    class GameServer;

    // ��Ʈ��ũ �̺�Ʈ �ݹ� Ÿ�Ե�
    using ConnectionCallback = std::function<void(std::shared_ptr<Session>)>;
    using DisconnectionCallback = std::function<void(const std::string& sessionId)>;
    using MessageCallback = std::function<void(const std::string& sessionId, const std::string& message)>;

    // ��Ʈ��ũ ������
    class NetworkManager {
    public:
        explicit NetworkManager(GameServer* server);
        ~NetworkManager();

        // �ʱ�ȭ �� ����
        bool initialize(const std::string& host, int port);
        void start();
        void stop();

        // ���� ����
        void startAccepting();
        void broadcastMessage(const std::string& message);
        void sendToSession(const std::string& sessionId, const std::string& message);

        // �ݹ� ����
        void setConnectionCallback(ConnectionCallback callback) { connectionCallback_ = callback; }
        void setDisconnectionCallback(DisconnectionCallback callback) { disconnectionCallback_ = callback; }
        void setMessageCallback(MessageCallback callback) { messageCallback_ = callback; }

        // ���
        size_t getActiveConnections() const;
        std::vector<std::string> getSessionIds() const;

    private:
        // ���� ó�� �Լ���
        void handleAccept(std::shared_ptr<Session> newSession,
            const boost::system::error_code& error);
        void createNewSession();

        // �ݹ� ȣ��
        void onConnection(std::shared_ptr<Session> session);
        void onDisconnection(const std::string& sessionId);
        void onMessage(const std::string& sessionId, const std::string& message);

    private:
        GameServer* server_;
        boost::asio::ip::tcp::acceptor acceptor_;

        // �ݹ��
        ConnectionCallback connectionCallback_;
        DisconnectionCallback disconnectionCallback_;
        MessageCallback messageCallback_;

        // ����
        bool running_{ false };
        std::string host_;
        int port_;
    };

} // namespace Blokus::Server