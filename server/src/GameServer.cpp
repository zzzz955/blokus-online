#include "server/GameServer.h"
#include <chrono>

namespace Blokus {
    namespace Server {

        GameServer::GameServer(const ServerConfig& config)
            : m_config(config)
            , m_ioContext()
            , m_acceptor(m_ioContext, boost::asio::ip::tcp::endpoint(
                boost::asio::ip::tcp::v4(), config.port))
            , m_cleanupTimer(m_ioContext)
            , m_statisticsTimer(m_ioContext)
        {
            spdlog::debug("GameServer 생성자 호출 - 포트: {}", config.port);
            initializeServer();
        }

        GameServer::~GameServer() {
            spdlog::debug("GameServer 소멸자 호출");
            if (m_isRunning) {
                stop();
            }
        }

        void GameServer::initializeServer() {
            spdlog::info("서버 컴포넌트 초기화 중...");

            try {
                // 게임 관리 컴포넌트 초기화
                m_roomManager = std::make_unique<RoomManager>();
                m_userManager = std::make_unique<UserManager>();

                // 데이터베이스 초기화 (추후 구현)
                // initializeDatabase();

                // Redis 초기화 (추후 구현)
                // initializeRedis();

                // 스레드 풀 설정
                setupThreadPool();

                spdlog::info("서버 컴포넌트 초기화 완료");
            }
            catch (const std::exception& e) {
                spdlog::error("서버 컴포넌트 초기화 실패: {}", e.what());
                throw;
            }
        }

        void GameServer::setupThreadPool() {
            spdlog::info("스레드 풀 설정 중... (크기: {})", m_config.threadPoolSize);

            // 스레드 풀 크기 검증
            if (m_config.threadPoolSize < 1) {
                m_config.threadPoolSize = std::thread::hardware_concurrency();
                spdlog::warn("잘못된 스레드 풀 크기, CPU 코어 수로 설정: {}", m_config.threadPoolSize);
            }

            m_threadPool.reserve(m_config.threadPoolSize);
        }

        void GameServer::start() {
            if (m_isRunning.exchange(true)) {
                spdlog::warn("서버가 이미 실행 중입니다.");
                return;
            }

            spdlog::info("서버 시작 중... (포트: {})", m_config.port);

            try {
                // 수락기 설정
                m_acceptor.set_option(boost::asio::ip::tcp::acceptor::reuse_address(true));

                // 새 연결 수락 시작
                startAccept();

                // 정리 타이머 시작 (30초마다)
                m_cleanupTimer.expires_after(std::chrono::seconds(30));
                m_cleanupTimer.async_wait([this](const boost::system::error_code& error) {
                    if (!error && m_isRunning) {
                        cleanupInactiveSessions();
                        m_cleanupTimer.expires_after(std::chrono::seconds(30));
                        m_cleanupTimer.async_wait([this](const boost::system::error_code& error) {
                            // 재귀적으로 타이머 재설정
                            });
                    }
                    });

                // 통계 타이머 시작
                startStatisticsTimer();

                spdlog::info("서버가 성공적으로 시작되었습니다.");
            }
            catch (const std::exception& e) {
                m_isRunning = false;
                spdlog::error("서버 시작 실패: {}", e.what());
                throw;
            }
        }

        void GameServer::stop() {
            if (!m_isRunning.exchange(false)) {
                spdlog::warn("서버가 이미 중지되었습니다.");
                return;
            }

            spdlog::info("서버 중지 중...");

            try {
                // 수락기 닫기
                if (m_acceptor.is_open()) {
                    m_acceptor.close();
                }

                // 타이머들 취소
                m_cleanupTimer.cancel();
                m_statisticsTimer.cancel();

                // I/O 컨텍스트 중지
                m_ioContext.stop();

                // 스레드 풀 종료 대기
                for (auto& thread : m_threadPool) {
                    if (thread.joinable()) {
                        thread.join();
                    }
                }
                m_threadPool.clear();

                // 정리 작업
                cleanup();

                spdlog::info("서버가 성공적으로 중지되었습니다.");
            }
            catch (const std::exception& e) {
                spdlog::error("서버 중지 중 오류: {}", e.what());
            }
        }

        void GameServer::run() {
            if (!m_isRunning) {
                spdlog::error("서버가 시작되지 않았습니다. start()를 먼저 호출하세요.");
                return;
            }

            // 스레드 풀 실행
            for (int i = 0; i < m_config.threadPoolSize; ++i) {
                m_threadPool.emplace_back([this]() {
                    spdlog::debug("스레드 {} 시작", std::this_thread::get_id());
                    try {
                        m_ioContext.run();
                    }
                    catch (const std::exception& e) {
                        spdlog::error("스레드에서 예외 발생: {}", e.what());
                    }
                    spdlog::debug("스레드 {} 종료", std::this_thread::get_id());
                    });
            }

            // 메인 스레드에서도 I/O 처리
            try {
                m_ioContext.run();
            }
            catch (const std::exception& e) {
                spdlog::error("메인 스레드에서 예외 발생: {}", e.what());
            }
        }

        void GameServer::startAccept() {
            if (!m_isRunning) {
                return;
            }

            // 새 세션 생성
            auto newSession = std::make_shared<Session>(m_ioContext, m_nextSessionId++);

            // 연결 수락 시작
            m_acceptor.async_accept(newSession->getSocket(),
                [this, newSession](const boost::system::error_code& error) {
                    handleAccept(newSession, error);
                });
        }

        void GameServer::handleAccept(std::shared_ptr<Session> newSession,
            const boost::system::error_code& error) {
            if (!error) {
                // 연결 수 제한 확인
                if (getConnectionCount() >= static_cast<size_t>(m_config.maxConnections)) {
                    spdlog::warn("최대 연결 수 도달, 새 연결 거부: {}",
                        newSession->getRemoteAddress());
                    newSession->close();
                }
                else {
                    spdlog::info("새 클라이언트 연결: {} (세션 ID: {})",
                        newSession->getRemoteAddress(), newSession->getId());

                    // 세션 추가 및 시작
                    addSession(newSession);
                    newSession->start();

                    m_totalConnections++;
                }
            }
            else {
                spdlog::warn("연결 수락 실패: {}", error.message());
            }

            // 다음 연결 대기
            startAccept();
        }

        void GameServer::addSession(std::shared_ptr<Session> session) {
            std::lock_guard<std::mutex> lock(m_sessionsMutex);
            m_sessions[session->getId()] = session;

            spdlog::debug("세션 추가됨: {} (총 {}개)", session->getId(), m_sessions.size());
        }

        void GameServer::removeSession(uint32_t sessionId) {
            std::lock_guard<std::mutex> lock(m_sessionsMutex);
            auto it = m_sessions.find(sessionId);
            if (it != m_sessions.end()) {
                spdlog::info("클라이언트 연결 해제: {} (세션 ID: {})",
                    it->second->getRemoteAddress(), sessionId);
                m_sessions.erase(it);
                spdlog::debug("세션 제거됨: {} (총 {}개)", sessionId, m_sessions.size());
            }
        }

        std::shared_ptr<Session> GameServer::findSession(uint32_t sessionId) {
            std::lock_guard<std::mutex> lock(m_sessionsMutex);
            auto it = m_sessions.find(sessionId);
            return (it != m_sessions.end()) ? it->second : nullptr;
        }

        size_t GameServer::getConnectionCount() const {
            std::lock_guard<std::mutex> lock(m_sessionsMutex);
            return m_sessions.size();
        }

        size_t GameServer::getRoomCount() const {
            return m_roomManager ? m_roomManager->getRoomCount() : 0;
        }

        void GameServer::broadcastToAll(const std::string& message) {
            std::lock_guard<std::mutex> lock(m_sessionsMutex);

            for (auto& [sessionId, session] : m_sessions) {
                if (session && session->isConnected()) {
                    session->sendMessage(message);
                }
            }

            m_totalMessagesSent += m_sessions.size();
            spdlog::debug("전체 브로드캐스트: {}명에게 전송", m_sessions.size());
        }

        void GameServer::broadcastToRoom(uint32_t roomId, const std::string& message) {
            if (!m_roomManager) {
                spdlog::warn("RoomManager가 초기화되지 않음");
                return;
            }

            auto room = m_roomManager->findRoom(roomId);
            if (!room) {
                spdlog::warn("존재하지 않는 방 ID: {}", roomId);
                return;
            }

            int sentCount = 0;
            for (auto sessionId : room->getSessionIds()) {
                auto session = findSession(sessionId);
                if (session && session->isConnected()) {
                    session->sendMessage(message);
                    sentCount++;
                }
            }

            m_totalMessagesSent += sentCount;
            spdlog::debug("방 {} 브로드캐스트: {}명에게 전송", roomId, sentCount);
        }

        void GameServer::cleanupInactiveSessions() {
            std::vector<uint32_t> inactiveSessions;

            {
                std::lock_guard<std::mutex> lock(m_sessionsMutex);
                for (const auto& [sessionId, session] : m_sessions) {
                    if (!session || !session->isConnected()) {
                        inactiveSessions.push_back(sessionId);
                    }
                }
            }

            for (uint32_t sessionId : inactiveSessions) {
                removeSession(sessionId);
            }

            if (!inactiveSessions.empty()) {
                spdlog::info("비활성 세션 {}개 정리됨", inactiveSessions.size());
            }
        }

        void GameServer::startStatisticsTimer() {
            m_statisticsTimer.expires_after(std::chrono::minutes(1));
            m_statisticsTimer.async_wait([this](const boost::system::error_code& error) {
                if (!error && m_isRunning) {
                    printStatistics();
                    startStatisticsTimer(); // 재귀적으로 타이머 재설정
                }
                });
        }

        void GameServer::printStatistics() {
            size_t activeConnections = getConnectionCount();
            size_t activeRooms = getRoomCount();

            spdlog::info("=== 서버 통계 ===");
            spdlog::info("활성 연결: {}/{}", activeConnections, m_config.maxConnections);
            spdlog::info("활성 방: {}", activeRooms);
            spdlog::info("총 연결 수: {}", m_totalConnections.load());
            spdlog::info("수신 메시지: {}", m_totalMessagesReceived.load());
            spdlog::info("송신 메시지: {}", m_totalMessagesSent.load());
            spdlog::info("================");
        }

        void GameServer::cleanup() {
            spdlog::info("서버 정리 작업 수행 중...");

            // 모든 세션 정리
            {
                std::lock_guard<std::mutex> lock(m_sessionsMutex);
                for (auto& [sessionId, session] : m_sessions) {
                    if (session) {
                        session->close();
                    }
                }
                m_sessions.clear();
            }

            // 게임 관리 컴포넌트 정리
            if (m_roomManager) {
                m_roomManager.reset();
            }

            if (m_userManager) {
                m_userManager.reset();
            }

            spdlog::info("서버 정리 작업 완료");
        }

    } // namespace Server
} // namespace Blokus