#pragma once

#include <memory>
#include <atomic>
#include <thread>
#include <vector>
#include <unordered_map>
#include <mutex>

#include <boost/asio.hpp>

#include "ServerTypes.h"
#include "common/Types.h"

namespace Blokus {
    namespace Server {

        // ���� ����
        class ClientConnection;
        class RoomManager;
        class GameManager;
        class MessageHandler;
        class DatabaseManager;
        class RedisManager;

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

        private:
            // �ʱ�ȭ ����
            void initializeComponents();
            void initializeDatabase();
            void initializeRedis();
            void startAcceptor();
            void startHeartbeatTimer();
            void startStatsTimer();

            // ��Ʈ��ũ ����
            void handleAccept(std::shared_ptr<ClientConnection> connection,
                const boost::system::error_code& error);
            void acceptNewConnections();

            // Ŭ���̾�Ʈ ����
            void addClient(std::shared_ptr<ClientConnection> client);
            void removeClient(const std::string& sessionId);
            std::shared_ptr<ClientConnection> getClient(const std::string& sessionId);

            // ���� �� Ÿ�̸� ����
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

            // ������Ʈ��
            std::unique_ptr<RoomManager> m_roomManager;
            std::unique_ptr<GameManager> m_gameManager;
            std::unique_ptr<MessageHandler> m_messageHandler;
            std::unique_ptr<DatabaseManager> m_databaseManager;
            std::unique_ptr<RedisManager> m_redisManager;

            // Ŭ���̾�Ʈ ����
            std::unordered_map<std::string, std::shared_ptr<ClientConnection>> m_clients;
            mutable std::shared_mutex m_clientsMutex;

            // ���
            mutable std::mutex m_statsMutex;
            ServerStats m_stats;
        };

        // ========================================
        // Ŭ���̾�Ʈ ���� Ŭ����
        // ========================================

        class ClientConnection : public std::enable_shared_from_this<ClientConnection> {
        public:
            explicit ClientConnection(boost::asio::io_context& ioContext, GameServer* server);
            ~ClientConnection();

            // ���� ����
            void start();
            void disconnect();
            bool isConnected() const { return m_connected.load(); }

            // �޽��� �ۼ���
            void sendMessage(const std::string& message);
            void sendProtobufMessage(const google::protobuf::Message& message);

            // ���� ����
            const ClientSession& getSession() const { return m_session; }
            void setSession(const ClientSession& session) { m_session = session; }

            // ���� ����
            boost::asio::ip::tcp::socket& socket() { return m_socket; }

        private:
            // ��Ʈ��ũ I/O
            void startReceive();
            void handleReceive(const boost::system::error_code& error, size_t bytesReceived);
            void handleSend(const boost::system::error_code& error, size_t bytesSent);

            // �޽��� ó��
            void processMessage(const std::string& message);
            void processReceivedData();

            // ���� ����
            void updateLastActivity();
            void handleError(const boost::system::error_code& error);

        private:
            GameServer* m_server;
            boost::asio::ip::tcp::socket m_socket;
            std::atomic<bool> m_connected{ false };

            // ���� ����
            ClientSession m_session;

            // ���� ����
            std::array<char, SOCKET_BUFFER_SIZE> m_receiveBuffer;
            std::string m_messageBuffer;  // �κ� �޽��� �����
            std::queue<std::string> m_sendQueue;
            std::mutex m_sendMutex;
            bool m_sendInProgress = false;
        };

        // ========================================
        // �� ������ Ŭ����
        // ========================================

        class RoomManager {
        public:
            RoomManager();
            ~RoomManager();

            // �� �����ֱ�
            int createRoom(const std::string& hostId, const Blokus::Common::RoomInfo& roomInfo);
            bool deleteRoom(int roomId);

            // �� ����/Ż��
            bool joinRoom(int roomId, const std::string& userId, const std::string& password = "");
            bool leaveRoom(int roomId, const std::string& userId);

            // �� ���� ��ȸ
            std::vector<Blokus::Common::RoomInfo> getRoomList() const;
            std::optional<Blokus::Common::RoomInfo> getRoomInfo(int roomId) const;
            bool isRoomHost(int roomId, const std::string& userId) const;

            // �� ���� ����
            bool updateRoomSettings(int roomId, const std::string& hostId,
                const Blokus::Common::RoomInfo& newSettings);

            // ���� ����
            void cleanupEmptyRooms();
            int getActiveRoomCount() const;

        private:
            // �� ������ ����
            struct Room {
                Blokus::Common::RoomInfo info;
                std::vector<std::string> players;
                std::chrono::system_clock::time_point createdAt;
                std::chrono::system_clock::time_point lastActivity;
            };

            std::unordered_map<int, Room> m_rooms;
            mutable std::shared_mutex m_roomsMutex;
            std::atomic<int> m_nextRoomId{ 1 };
        };

        // ========================================
        // ���� ������ Ŭ���� (����)
        // ========================================

        class GameManager {
        public:
            GameManager();
            ~GameManager();

            // ���� ����/����
            bool startGame(int roomId);
            bool endGame(int roomId);

            // ���� ���� ��ȸ
            bool isGameInProgress(int roomId) const;
            int getActiveGameCount() const;

        private:
            // ���� ���� ����
            std::unordered_map<int, std::string> m_activeGames;  // roomId -> gameData
            mutable std::shared_mutex m_gamesMutex;
        };

        // ========================================
        // �޽��� �ڵ鷯 Ŭ���� (����)
        // ========================================

        class MessageHandler {
        public:
            MessageHandler(GameServer* server);
            ~MessageHandler();

            // �޽��� ó��
            void handleMessage(std::shared_ptr<ClientConnection> client, const std::string& message);

        private:
            GameServer* m_server;

            // ���� protobuf �޽��� ó�� ���� ����
        };

        // ========================================
        // �����ͺ��̽� ������ Ŭ���� (����)  
        // ========================================

        class DatabaseManager {
        public:
            DatabaseManager(const ServerConfig& config);
            ~DatabaseManager();

            bool initialize();
            void shutdown();

        private:
            ServerConfig m_config;
            // ���� PostgreSQL ���� ���� ����
        };

        // ========================================
        // Redis ������ Ŭ���� (����)
        // ========================================

        class RedisManager {
        public:
            RedisManager(const ServerConfig& config);
            ~RedisManager();

            bool initialize();
            void shutdown();

        private:
            ServerConfig m_config;
            // ���� Redis ���� ���� ����
        };

    } // namespace Server
} // namespace Blokus