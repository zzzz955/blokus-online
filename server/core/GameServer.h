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

            // ���� �����ֱ�
            void start();
            void stop();
            void run();

            // ������
            const ServerConfig& getConfig() const { return config_; }
            bool isRunning() const { return running_; }
            size_t getConnectedClients() const;

        private:
            // �ʱ�ȭ
            void initializeComponents();
            void startAccepting();

            // ��Ʈ��ũ ó��
            void handleAccept(ClientSessionPtr session,
                const boost::system::error_code& error);
            void onClientConnected(ClientSessionPtr session);
            void onClientDisconnected(ClientSessionPtr session);

            // ����
            void cleanup();

        private:
            ServerConfig config_;
            std::atomic<bool> running_{ false };

            // Boost.Asio ������Ʈ
            boost::asio::io_context ioContext_;
            boost::asio::ip::tcp::acceptor acceptor_;
            std::vector<std::thread> threadPool_;

            // ���� ������Ʈ
            std::unique_ptr<NetworkManager> networkManager_;
            std::unique_ptr<UserManager> userManager_;
            std::unique_ptr<MessageHandler> messageHandler_;

            // Ŭ���̾�Ʈ ����
            std::unordered_map<std::string, ClientSessionPtr> clients_;
            mutable std::mutex clientsMutex_;

            // ���
            std::atomic<size_t> totalConnections_{ 0 };
            std::chrono::steady_clock::time_point startTime_;
        };

    } // namespace Server
} // namespace Blokus