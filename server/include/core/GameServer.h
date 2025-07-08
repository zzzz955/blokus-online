#pragma once

#include <memory>
#include <atomic>
#include <thread>
#include <vector>
#include <unordered_map>
#include <mutex>
#include <shared_mutex>
#include <queue>

#include <boost/asio.hpp>
#include <google/protobuf/message.h>

#include "common/ServerTypes.h"
#include "common/Types.h"

namespace Blokus {
    namespace Server {

        // 전방 선언
        class ClientConnection;
        class RoomManager;
        class GameManager;
        class MessageHandler;
        class DatabaseManager;
        class RedisManager;

        // ========================================
        // 메인 게임 서버 클래스
        // ========================================

        class GameServer {
        public:
            explicit GameServer(const ServerConfig& config);
            ~GameServer();

            // 서버 생명주기
            void start();
            void stop();
            void run();  // 메인 루프 (블로킹)

            bool isRunning() const { return m_running.load(); }
            const ServerConfig& getConfig() const { return m_config; }
            ServerStats getStats() const;

        private:
            // 초기화 관련
            void initializeComponents();
            void initializeDatabase();
            void initializeRedis();
            void startAcceptor();
            void startHeartbeatTimer();
            void startStatsTimer();

            // 네트워크 관련
            void handleAccept(std::shared_ptr<ClientConnection> connection,
                const boost::system::error_code& error);
            void acceptNewConnections();

            // 클라이언트 관리
            void addClient(ClientSessionPtr client);
            void removeClient(const std::string& sessionId);
            ClientSessionPtr getClient(const std::string& sessionId);

            // 정리 및 타이머 관련
            void cleanup();
            void performHeartbeat();
            void updateStats();
            void cleanupIdleConnections();

            // 에러 핸들링
            void handleError(const std::string& context, const std::exception& e);

        private:
            // 설정 및 상태
            ServerConfig m_config;
            std::atomic<bool> m_running{ false };
            std::atomic<bool> m_accepting{ false };

            // Boost.Asio 관련
            boost::asio::io_context m_ioContext;
            boost::asio::ip::tcp::acceptor m_acceptor;
            std::vector<std::thread> m_threadPool;

            // 타이머들
            boost::asio::steady_timer m_heartbeatTimer;
            boost::asio::steady_timer m_statsTimer;
            boost::asio::steady_timer m_cleanupTimer;

            // 컴포넌트들
            std::unique_ptr<RoomManager> m_roomManager;
            std::unique_ptr<GameManager> m_gameManager;
            std::unique_ptr<MessageHandler> m_messageHandler;
            std::unique_ptr<DatabaseManager> m_databaseManager;
            std::unique_ptr<RedisManager> m_redisManager;

            // 클라이언트 관리
            std::unordered_map<std::string, ClientSessionPtr> m_clients;
            mutable std::shared_mutex m_clientsMutex;

            // 통계
            mutable std::mutex m_statsMutex;
            ServerStats m_stats;
        };

        // ========================================
        // 클라이언트 연결 클래스
        // ========================================

        class ClientConnection : public std::enable_shared_from_this<ClientConnection> {
        public:
            explicit ClientConnection(boost::asio::io_context& ioContext, GameServer* server);
            ~ClientConnection();

            // 연결 관리
            void start();
            void disconnect();
            bool isConnected() const { return m_connected.load(); }

            // 메시지 송수신
            void sendMessage(const std::string& message);
            void sendProtobufMessage(const google::protobuf::Message& message);

            // 세션 정보
            const ClientSession& getSession() const { return m_session; }
            void setSession(const ClientSession& session) { m_session = session; }

            // 소켓 접근
            boost::asio::ip::tcp::socket& socket() { return m_socket; }

        private:
            // 네트워크 I/O
            void startReceive();
            void handleReceive(const boost::system::error_code& error, size_t bytesReceived);
            void handleSend(const boost::system::error_code& error, size_t bytesSent);

            // 메시지 처리
            void processMessage(const std::string& message);
            void processReceivedData();

            // 상태 관리
            void updateLastActivity();
            void handleError(const boost::system::error_code& error);

        private:
            GameServer* m_server;
            boost::asio::ip::tcp::socket m_socket;
            std::atomic<bool> m_connected{ false };

            // 세션 정보
            ClientSession m_session;

            // 버퍼 관리
            std::array<char, SOCKET_BUFFER_SIZE> m_receiveBuffer;
            std::string m_messageBuffer;  // 부분 메시지 저장용
            std::queue<std::string> m_sendQueue;
            std::mutex m_sendMutex;
            bool m_sendInProgress = false;
        };

        // ========================================
        // 게임 관리자 클래스 (스텁)
        // ========================================

        class GameManager {
        public:
            GameManager();
            ~GameManager();

            // 게임 시작/종료
            bool startGame(int roomId);
            bool endGame(int roomId);

            // 게임 상태 조회
            bool isGameInProgress(int roomId) const;
            int getActiveGameCount() const;

        private:
            // 추후 구현 예정
            std::unordered_map<int, std::string> m_activeGames;  // roomId -> gameData
            mutable std::shared_mutex m_gamesMutex;
        };

        // ========================================
        // 데이터베이스 관리자 클래스 (스텁)
        // ========================================

        class DatabaseManager {
        public:
            DatabaseManager(const ServerConfig& config);
            ~DatabaseManager();

            bool initialize();
            void shutdown();

        private:
            ServerConfig m_config;
            // 추후 PostgreSQL 연동 구현 예정
        };

        // ========================================
        // Redis 관리자 클래스 (스텁)
        // ========================================

        class RedisManager {
        public:
            RedisManager(const ServerConfig& config);
            ~RedisManager();

            bool initialize();
            void shutdown();

        private:
            ServerConfig m_config;
            // 추후 Redis 연동 구현 예정
        };

    } // namespace Server
} // namespace Blokus