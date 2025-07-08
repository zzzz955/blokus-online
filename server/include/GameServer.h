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

        // 서버 설정 구조체
        struct ServerConfig {
            uint16_t port = DEFAULT_SERVER_PORT;                   // 서버 포트
            int maxConnections = MAX_CONCURRENT_USERS;             // 최대 동시 접속자 수
            int threadPoolSize = 4;                                // 스레드 풀 크기
            std::string dbConnectionString = "";                   // 데이터베이스 연결 문자열
            std::string redisHost = "localhost";                   // Redis 호스트
            int redisPort = 6379;                                  // Redis 포트

            ServerConfig() = default;
        };

        // 메인 게임 서버 클래스
        class GameServer {
        public:
            explicit GameServer(const ServerConfig& config);
            ~GameServer();

            // 서버 생명주기 관리
            void start();                                          // 서버 시작
            void stop();                                           // 서버 중지
            void run();                                            // 서버 실행 (블로킹)

            // 서버 상태 조회
            bool isRunning() const { return m_isRunning; }
            size_t getConnectionCount() const;
            size_t getRoomCount() const;
            ServerConfig getConfig() const { return m_config; }

            // 연결 관리
            void addSession(std::shared_ptr<Session> session);
            void removeSession(uint32_t sessionId);
            std::shared_ptr<Session> findSession(uint32_t sessionId);

            // 방송 메시지 전송
            void broadcastToAll(const std::string& message);
            void broadcastToRoom(uint32_t roomId, const std::string& message);

        private:
            // 초기화 함수들
            void initializeServer();                              // 서버 컴포넌트 초기화
            void initializeDatabase();                            // 데이터베이스 연결 초기화
            void initializeRedis();                               // Redis 연결 초기화
            void setupThreadPool();                               // 스레드 풀 설정

            // 네트워크 처리
            void startAccept();                                    // 새 연결 수락 시작
            void handleAccept(std::shared_ptr<Session> newSession, // 연결 수락 처리
                const boost::system::error_code& error);

            // 정리 작업
            void cleanup();                                        // 서버 종료 시 정리 작업
            void cleanupInactiveSessions();                       // 비활성 세션 정리

            // 통계 및 모니터링
            void startStatisticsTimer();                          // 통계 타이머 시작
            void printStatistics();                               // 서버 통계 출력

        private:
            // 서버 설정 및 상태
            ServerConfig m_config;                                 // 서버 설정
            std::atomic<bool> m_isRunning{ false };               // 서버 실행 상태
            std::atomic<uint32_t> m_nextSessionId{ 1 };           // 다음 세션 ID

            // Boost.Asio 네트워크 컴포넌트
            boost::asio::io_context m_ioContext;                  // I/O 컨텍스트
            boost::asio::ip::tcp::acceptor m_acceptor;            // TCP 수락기
            std::vector<std::thread> m_threadPool;                // 스레드 풀

            // 세션 관리
            std::unordered_map<uint32_t, std::shared_ptr<Session>> m_sessions; // 활성 세션들
            mutable std::mutex m_sessionsMutex;                   // 세션 맵 보호용 뮤텍스

            // 게임 관리 컴포넌트들
            std::unique_ptr<RoomManager> m_roomManager;           // 방 관리자
            std::unique_ptr<UserManager> m_userManager;           // 사용자 관리자

            // 정리 타이머
            boost::asio::steady_timer m_cleanupTimer;             // 정리 작업용 타이머
            boost::asio::steady_timer m_statisticsTimer;          // 통계 출력용 타이머

            // 통계 정보
            std::atomic<uint64_t> m_totalConnections{ 0 };        // 총 연결 수
            std::atomic<uint64_t> m_totalMessagesReceived{ 0 };   // 총 수신 메시지 수
            std::atomic<uint64_t> m_totalMessagesSent{ 0 };       // 총 송신 메시지 수
        };

    } // namespace Server
} // namespace Blokus