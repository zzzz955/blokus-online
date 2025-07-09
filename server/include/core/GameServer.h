#pragma once

#include <memory>
#include <atomic>
#include <thread>
#include <vector>
#include <unordered_map>
#include <mutex>
#include <shared_mutex>

#include <boost/asio.hpp>

#include "common/ServerTypes.h"
#include "common/Types.h"

namespace Blokus {
    namespace Server {

        // ���� ���� (��ȯ ���� ����)
        class Session;
        class RoomManager;
        class MessageHandler;
        // DatabaseManager, RedisManager�� �켱 ���� (��������)

        // ========================================
        // ���� ���� ���� Ŭ����
        // ========================================

        class GameServer {
        public:
            explicit GameServer(const ServerConfig& config);
            ~GameServer();

            // ���� �����ֱ�
            void start();
            void stop();
            void run();  // ���� ���� (���ŷ)

            bool isRunning() const { return m_running.load(); }
            const ServerConfig& getConfig() const { return m_config; }
            ServerStats getStats() const;

            // Ŭ���̾�Ʈ ����
            void addClient(std::shared_ptr<Session> session);
            void removeClient(const std::string& sessionId);
            std::shared_ptr<Session> getClient(const std::string& sessionId);

        private:
            // �ʱ�ȭ ����
            void initializeComponents();
            void startAcceptor();
            void startHeartbeatTimer();

            // ��Ʈ��ũ ����
            void handleAccept(std::shared_ptr<Session> newSession,
                const boost::system::error_code& error);
            void acceptNewConnections();

            // ���� �� Ÿ�̹� ����
            void cleanup();
            void performHeartbeat();
            void updateStats();
            void cleanupIdleConnections();

            // ���� �ڵ鸵
            void handleError(const std::string& context, const std::exception& e);

        private:
            // ���� �� ����
            ServerConfig m_config;
            std::atomic<bool> m_running{ false };
            std::atomic<bool> m_accepting{ false };

            // Boost.Asio ����
            boost::asio::io_context m_ioContext;
            boost::asio::ip::tcp::acceptor m_acceptor;
            std::vector<std::thread> m_threadPool;

            // Ÿ�̸ӵ�
            boost::asio::steady_timer m_heartbeatTimer;
            boost::asio::steady_timer m_statsTimer;
            boost::asio::steady_timer m_cleanupTimer;

            // ������Ʈ�� (�ʼ��� ����)
            std::unique_ptr<RoomManager> m_roomManager;
            std::unique_ptr<MessageHandler> m_messageHandler;

            // Ŭ���̾�Ʈ ����
            std::unordered_map<std::string, std::shared_ptr<Session>> m_clients;
            mutable std::shared_mutex m_clientsMutex;

            // ���
            mutable std::mutex m_statsMutex;
            ServerStats m_stats;
        };

    } // namespace Server
} // namespace Blokus