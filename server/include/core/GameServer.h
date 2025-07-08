#pragma once

#include "ServerTypes.h"
#include "NetworkManager.h"
#include "UserManager.h"
#include "MessageHandler.h"

#include <boost/asio.hpp>
#include <boost/system/error_code.hpp>
#include <thread>
#include <vector>
#include <memory>
#include <atomic>
#include <unordered_map>

namespace Blokus {
    namespace Server {

        class GameServer {
        public:
            explicit GameServer(const ServerConfig& config);
            ~GameServer();

            // 서버 생명주기
            void start();
            void stop();
            void run();

            // 접근자
            const ServerConfig& getConfig() const { return config_; }
            bool isRunning() const { return running_; }
            size_t getConnectedClients() const;

        private:
            // 초기화
            void initializeComponents();
            void startAccepting();

            // 네트워크 처리
            void handleAccept(ClientSessionPtr session,
                const boost::system::error_code& error);
            void onClientConnected(ClientSessionPtr session);
            void onClientDisconnected(ClientSessionPtr session);

            // 정리
            void cleanup();

        private:
            ServerConfig config_;
            std::atomic<bool> running_{ false };

            // Boost.Asio 컴포넌트
            boost::asio::io_context ioContext_;
            boost::asio::ip::tcp::acceptor acceptor_;
            std::vector<std::thread> threadPool_;

            // 서버 컴포넌트
            std::unique_ptr<NetworkManager> networkManager_;
            std::unique_ptr<UserManager> userManager_;
            std::unique_ptr<MessageHandler> messageHandler_;

            // 클라이언트 관리
            std::unordered_map<std::string, ClientSessionPtr> clients_;
            mutable std::mutex clientsMutex_;

            // 통계
            std::atomic<size_t> totalConnections_{ 0 };
            std::chrono::steady_clock::time_point startTime_;
        };

    } // namespace Server
} // namespace Blokus