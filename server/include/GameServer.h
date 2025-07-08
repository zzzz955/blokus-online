#pragma once

#include <memory>
#include <vector>
#include <unordered_map>
#include <unordered_set>
#include <mutex>
#include <atomic>
#include <thread>

#include <boost/asio.hpp>
#include <spdlog/spdlog.h>

#include "common/Types.h"
#include "common/GameLogic.h"
#include "server/Session.h"
#include "server/RoomManager.h"
#include "server/UserManager.h"
#include "server/ServerTypes.h"

namespace Blokus {
    namespace Server {

        // ���� ���� ����ü
        struct ServerConfig {
            uint16_t port = DEFAULT_SERVER_PORT;                   // ���� ��Ʈ
            int maxConnections = MAX_CONCURRENT_USERS;             // �ִ� ���� ������ ��
            int threadPoolSize = 4;                                // ������ Ǯ ũ��
            std::string dbConnectionString = "";                   // �����ͺ��̽� ���� ���ڿ�
            std::string redisHost = "localhost";                   // Redis ȣ��Ʈ
            int redisPort = 6379;                                  // Redis ��Ʈ

            ServerConfig() = default;
        };

        // ���� ���� ���� Ŭ����
        class GameServer {
        public:
            explicit GameServer(const ServerConfig& config);
            ~GameServer();

            // ���� �����ֱ� ����
            void start();                                          // ���� ����
            void stop();                                           // ���� ����
            void run();                                            // ���� ���� (���ŷ)

            // ���� ���� ��ȸ
            bool isRunning() const { return m_isRunning; }
            size_t getConnectionCount() const;
            size_t getRoomCount() const;
            ServerConfig getConfig() const { return m_config; }

            // ���� ����
            void addSession(std::shared_ptr<Session> session);
            void removeSession(uint32_t sessionId);
            std::shared_ptr<Session> findSession(uint32_t sessionId);

            // ��� �޽��� ����
            void broadcastToAll(const std::string& message);
            void broadcastToRoom(uint32_t roomId, const std::string& message);

        private:
            // �ʱ�ȭ �Լ���
            void initializeServer();                              // ���� ������Ʈ �ʱ�ȭ
            void initializeDatabase();                            // �����ͺ��̽� ���� �ʱ�ȭ
            void initializeRedis();                               // Redis ���� �ʱ�ȭ
            void setupThreadPool();                               // ������ Ǯ ����

            // ��Ʈ��ũ ó��
            void startAccept();                                    // �� ���� ���� ����
            void handleAccept(std::shared_ptr<Session> newSession, // ���� ���� ó��
                const boost::system::error_code& error);

            // ���� �۾�
            void cleanup();                                        // ���� ���� �� ���� �۾�
            void cleanupInactiveSessions();                       // ��Ȱ�� ���� ����

            // ��� �� ����͸�
            void startStatisticsTimer();                          // ��� Ÿ�̸� ����
            void printStatistics();                               // ���� ��� ���

        private:
            // ���� ���� �� ����
            ServerConfig m_config;                                 // ���� ����
            std::atomic<bool> m_isRunning{ false };               // ���� ���� ����
            std::atomic<uint32_t> m_nextSessionId{ 1 };           // ���� ���� ID

            // Boost.Asio ��Ʈ��ũ ������Ʈ
            boost::asio::io_context m_ioContext;                  // I/O ���ؽ�Ʈ
            boost::asio::ip::tcp::acceptor m_acceptor;            // TCP ������
            std::vector<std::thread> m_threadPool;                // ������ Ǯ

            // ���� ����
            std::unordered_map<uint32_t, std::shared_ptr<Session>> m_sessions; // Ȱ�� ���ǵ�
            mutable std::mutex m_sessionsMutex;                   // ���� �� ��ȣ�� ���ؽ�

            // ���� ���� ������Ʈ��
            std::unique_ptr<RoomManager> m_roomManager;           // �� ������
            std::unique_ptr<UserManager> m_userManager;           // ����� ������

            // ���� Ÿ�̸�
            boost::asio::steady_timer m_cleanupTimer;             // ���� �۾��� Ÿ�̸�
            boost::asio::steady_timer m_statisticsTimer;          // ��� ��¿� Ÿ�̸�

            // ��� ����
            std::atomic<uint64_t> m_totalConnections{ 0 };        // �� ���� ��
            std::atomic<uint64_t> m_totalMessagesReceived{ 0 };   // �� ���� �޽��� ��
            std::atomic<uint64_t> m_totalMessagesSent{ 0 };       // �� �۽� �޽��� ��
        };

    } // namespace Server
} // namespace Blokus