#pragma once

#include <memory>
#include <string>
#include <vector>
#include <functional>
#include <boost/asio.hpp>

namespace Blokus::Server {

    // 전방 선언
    class Session;
    class GameServer;

    // 네트워크 이벤트 콜백 타입들
    using ConnectionCallback = std::function<void(std::shared_ptr<Session>)>;
    using DisconnectionCallback = std::function<void(const std::string& sessionId)>;
    using MessageCallback = std::function<void(const std::string& sessionId, const std::string& message)>;

    // 네트워크 관리자
    class NetworkManager {
    public:
        explicit NetworkManager(GameServer* server);
        ~NetworkManager();

        // 초기화 및 제어
        bool initialize(const std::string& host, int port);
        void start();
        void stop();

        // 연결 관리
        void startAccepting();
        void broadcastMessage(const std::string& message);
        void sendToSession(const std::string& sessionId, const std::string& message);

        // 콜백 설정
        void setConnectionCallback(ConnectionCallback callback) { connectionCallback_ = callback; }
        void setDisconnectionCallback(DisconnectionCallback callback) { disconnectionCallback_ = callback; }
        void setMessageCallback(MessageCallback callback) { messageCallback_ = callback; }

        // 통계
        size_t getActiveConnections() const;
        std::vector<std::string> getSessionIds() const;

    private:
        // 내부 처리 함수들
        void handleAccept(std::shared_ptr<Session> newSession,
            const boost::system::error_code& error);
        void createNewSession();

        // 콜백 호출
        void onConnection(std::shared_ptr<Session> session);
        void onDisconnection(const std::string& sessionId);
        void onMessage(const std::string& sessionId, const std::string& message);

    private:
        GameServer* server_;
        boost::asio::ip::tcp::acceptor acceptor_;

        // 콜백들
        ConnectionCallback connectionCallback_;
        DisconnectionCallback disconnectionCallback_;
        MessageCallback messageCallback_;

        // 상태
        bool running_{ false };
        std::string host_;
        int port_;
    };

} // namespace Blokus::Server