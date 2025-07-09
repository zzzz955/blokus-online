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

        // 전방 선언 (순환 참조 방지)
        class Session;
        class RoomManager;
        class MessageHandler;
        // DatabaseManager, RedisManager는 우선 제거 (오버스펙)

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

            // 클라이언트 관리
            void addClient(std::shared_ptr<Session> session);
            void removeClient(const std::string& sessionId);
            std::shared_ptr<Session> getClient(const std::string& sessionId);

        private:
            // 초기화 관련
            void initializeComponents();
            void startAcceptor();
            void startHeartbeatTimer();

            // 네트워크 관련
            void handleAccept(std::shared_ptr<Session> newSession,
                const boost::system::error_code& error);
            void acceptNewConnections();

            // 정리 및 타이밍 관련
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

            // 컴포넌트들 (필수만 유지)
            std::unique_ptr<RoomManager> m_roomManager;
            std::unique_ptr<MessageHandler> m_messageHandler;

            // 클라이언트 관리
            std::unordered_map<std::string, std::shared_ptr<Session>> m_clients;
            mutable std::shared_mutex m_clientsMutex;

            // 통계
            mutable std::mutex m_statsMutex;
            ServerStats m_stats;
        };

    } // namespace Server
} // namespace Blokus