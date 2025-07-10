#include "core/GameServer.h"
#include "core/Session.h"
#include "manager/ConfigManager.h"
#include "manager/DatabaseManager.h"
#include <spdlog/spdlog.h>
#include <chrono>
#include <functional>

using boost::asio::ip::tcp;

namespace Blokus::Server {

    // ========================================
    // 생성자 및 소멸자
    // ========================================

    GameServer::GameServer()
        : running_(false)
        , ioContext_()
        , acceptor_(ioContext_)
        , heartbeatTimer_(nullptr)
        //, networkManager_(nullptr)
    {
        spdlog::info("GameServer 인스턴스 생성");
    }

    GameServer::~GameServer() {
        if (running_.load()) {
            stop();
        }
        spdlog::info("GameServer 인스턴스 소멸");
    }

    // ========================================
    // 서버 초기화 및 시작
    // ========================================

    bool GameServer::initialize() {
        spdlog::info("GameServer 초기화 시작");

        try {
            // 1. 설정 초기화
            if (!initializeConfig()) {
                spdlog::error("설정 초기화 실패");
                return false;
            }

            // 2. 데이터베이스 초기화
            if (!initializeDatabase()) {
                spdlog::error("데이터베이스 초기화 실패");
                return false;
            }

            // 3. 네트워크 초기화
            if (!initializeNetwork()) {
                spdlog::error("네트워크 초기화 실패");
                return false;
            }

            // 4. 통계 초기화
            {
                std::lock_guard<std::mutex> lock(statsMutex_);
                stats_.serverStartTime = std::chrono::system_clock::now();
                stats_.currentConnections = 0;
                stats_.totalConnectionsToday = 0;
                stats_.peakConcurrentConnections = 0;
            }

            spdlog::info("GameServer 초기화 완료");
            return true;

        }
        catch (const std::exception& e) {
            spdlog::error("GameServer 초기화 중 예외 발생: {}", e.what());
            return false;
        }
    }

    void GameServer::start() {
        if (running_.load()) {
            spdlog::warn("서버가 이미 실행 중입니다");
            return;
        }

        spdlog::info("GameServer 시작");
        running_.store(true);

        // 스레드 풀 생성
        int threadCount = ConfigManager::threadPoolSize;
        spdlog::info("스레드 풀 크기: {}", threadCount);

        threadPool_.reserve(threadCount);
        for (int i = 0; i < threadCount; ++i) {
            threadPool_.emplace_back([this]() {
                try {
                    ioContext_.run();
                }
                catch (const std::exception& e) {
                    spdlog::error("스레드 풀 예외: {}", e.what());
                }
                });
        }

        // 연결 수락 시작
        startAccepting();

        // 하트비트 타이머 시작
        startHeartbeatTimer();

        spdlog::info("GameServer가 {}:{} 에서 클라이언트 연결을 대기합니다",
            "0.0.0.0", ConfigManager::serverPort);
    }

    void GameServer::stop() {
        if (!running_.load()) {
            return;
        }

        spdlog::info("GameServer 종료 시작");
        running_.store(false);

        // 1. 새로운 연결 차단
        if (acceptor_.is_open()) {
            acceptor_.close();
        }

        // 2. 모든 세션 종료
        {
            std::lock_guard<std::mutex> lock(sessionsMutex_);
            for (auto& [sessionId, session] : sessions_) {
                if (session) {
                    session->stop();
                }
            }
            sessions_.clear();
        }

        // 3. 타이머 취소
        if (heartbeatTimer_) {
            heartbeatTimer_->cancel();
        }

        // 4. IO 컨텍스트 종료
        ioContext_.stop();

        // 5. 스레드 풀 정리
        for (auto& thread : threadPool_) {
            if (thread.joinable()) {
                thread.join();
            }
        }
        threadPool_.clear();

        spdlog::info("GameServer 종료 완료");
    }

    void GameServer::run() {
        if (!initialize()) {
            spdlog::error("서버 초기화 실패");
            return;
        }

        start();

        spdlog::info("서버가 실행 중입니다. Ctrl+C로 종료하세요");

        // 메인 스레드에서 대기
        try {
            ioContext_.run();
        }
        catch (const std::exception& e) {
            spdlog::error("메인 루프 예외: {}", e.what());
        }
    }

    // ========================================
    // 세션 관리
    // ========================================

    void GameServer::addSession(std::shared_ptr<Session> session) {
        if (!session) {
            spdlog::warn("null 세션 추가 시도");
            return;
        }

        std::lock_guard<std::mutex> lock(sessionsMutex_);
        const std::string& sessionId = session->getSessionId();
        sessions_[sessionId] = session;

        // 통계 업데이트 (스레드 안전)
        {
            std::lock_guard<std::mutex> statsLock(statsMutex_);
            stats_.currentConnections++;
            stats_.totalConnectionsToday++;

            // 피크 연결 수 업데이트
            if (stats_.currentConnections > stats_.peakConcurrentConnections) {
                stats_.peakConcurrentConnections = stats_.currentConnections;
            }
        }

        spdlog::info("새 세션 추가: {} (총 연결: {})",
            sessionId, getCurrentConnections());

        // 세션 콜백 설정
        session->setDisconnectCallback([this](const std::string& id) {
            onSessionDisconnect(id);
            });

        session->setMessageCallback([this](const std::string& id, const std::string& msg) {
            onSessionMessage(id, msg);
            });
    }

    void GameServer::removeSession(const std::string& sessionId) {
        std::lock_guard<std::mutex> lock(sessionsMutex_);

        auto it = sessions_.find(sessionId);
        if (it != sessions_.end()) {
            sessions_.erase(it);

            // 통계 업데이트
            {
                std::lock_guard<std::mutex> statsLock(statsMutex_);
                if (stats_.currentConnections > 0) {
                    stats_.currentConnections--;
                }
            }

            spdlog::info("세션 제거: {} (남은 연결: {})",
                sessionId, getCurrentConnections());
        }
    }

    std::shared_ptr<Session> GameServer::getSession(const std::string& sessionId) {
        std::lock_guard<std::mutex> lock(sessionsMutex_);
        auto it = sessions_.find(sessionId);
        return (it != sessions_.end()) ? it->second : nullptr;
    }

    std::weak_ptr<Session> GameServer::getSessionWeak(const std::string& sessionId) {
        std::lock_guard<std::mutex> lock(sessionsMutex_);
        auto it = sessions_.find(sessionId);
        return (it != sessions_.end()) ? std::weak_ptr<Session>(it->second) : std::weak_ptr<Session>();
    }

    bool GameServer::withSession(const std::string& sessionId,
        std::function<void(std::shared_ptr<Session>)> action) {
        auto session = getSession(sessionId);
        if (session && action) {
            action(session);
            return true;
        }
        return false;
    }

    // ========================================
    // 내부 초기화 함수들
    // ========================================

    bool GameServer::initializeConfig() {
        try {
            ConfigManager::initialize();
            if (!ConfigManager::validate()) {
                spdlog::error("설정 검증 실패");
                return false;
            }

            spdlog::info("설정 로드 완료 - 포트: {}, 최대 클라이언트: {}",
                ConfigManager::serverPort, ConfigManager::maxClients);
            return true;

        }
        catch (const std::exception& e) {
            spdlog::error("설정 초기화 예외: {}", e.what());
            return false;
        }
    }

    bool GameServer::initializeDatabase() {
        try {
            // DatabaseManager는 싱글톤이므로 별도 인스턴스 생성 없이 사용
            // 실제 DB 연결은 나중에 필요할 때 수행
            spdlog::info("데이터베이스 설정 확인 완료");
            return true;

        }
        catch (const std::exception& e) {
            spdlog::error("데이터베이스 초기화 예외: {}", e.what());
            return false;
        }
    }

    bool GameServer::initializeNetwork() {
        try {
            tcp::endpoint endpoint(tcp::v4(), ConfigManager::serverPort);
            acceptor_.open(endpoint.protocol());
            acceptor_.set_option(tcp::acceptor::reuse_address(true));
            acceptor_.bind(endpoint);
            acceptor_.listen(boost::asio::socket_base::max_listen_connections);

            spdlog::info("네트워크 초기화 완료 - {}:{}",
                endpoint.address().to_string(), endpoint.port());
            return true;

        }
        catch (const std::exception& e) {
            spdlog::error("네트워크 초기화 예외: {}", e.what());
            return false;
        }
    }

    // ========================================
    // 네트워크 처리
    // ========================================

    void GameServer::startAccepting() {
        if (!running_.load()) {
            return;
        }

        auto newSession = std::make_shared<Session>(tcp::socket(ioContext_));

        acceptor_.async_accept(newSession->getSocket(),
            [this, newSession](const boost::system::error_code& error) {
                handleNewConnection(newSession, error);
            });
    }

    void GameServer::handleNewConnection(std::shared_ptr<Session> session,
        const boost::system::error_code& error) {
        if (!running_.load()) {
            return;
        }

        if (!error) {
            try {
                std::string remoteAddr = session->getRemoteAddress();
                spdlog::info("새 클라이언트 연결: {}", remoteAddr);

                // 연결 제한 체크
                if (getCurrentConnections() >= ConfigManager::maxClients) {
                    spdlog::warn("최대 연결 수 초과, 연결 거부: {}", remoteAddr);
                    session->stop();
                }
                else {
                    addSession(session);
                    session->start();
                }

            }
            catch (const std::exception& e) {
                spdlog::error("새 연결 처리 중 예외: {}", e.what());
            }
        }
        else {
            spdlog::error("연결 수락 오류: {}", error.message());
        }

        // 다음 연결 대기
        startAccepting();
    }

    // ========================================
    // 세션 이벤트 핸들러
    // ========================================

    void GameServer::onSessionDisconnect(const std::string& sessionId) {
        spdlog::info("세션 연결 해제: {}", sessionId);
        removeSession(sessionId);
    }

    void GameServer::onSessionMessage(const std::string& sessionId, const std::string& message) {
        // 메시지 핸들링은 추후 MessageHandler 구현 시 연동
        spdlog::debug("세션 {}에서 메시지 수신: {}", sessionId, message.substr(0, 100));

        // 간단한 ping 응답 예시
        if (message == "ping") {
            withSession(sessionId, [](auto session) {
                session->sendMessage("pong");
                });
        }
    }

    // ========================================
    // 정리 작업
    // ========================================

    void GameServer::startHeartbeatTimer() {
        heartbeatTimer_ = std::make_unique<boost::asio::steady_timer>(ioContext_);
        handleHeartbeat();
    }

    void GameServer::handleHeartbeat() {
        if (!running_.load()) {
            return;
        }

        // 30초마다 하트비트 및 정리 작업
        heartbeatTimer_->expires_after(std::chrono::seconds(30));
        heartbeatTimer_->async_wait([this](const boost::system::error_code& error) {
            if (!error && running_.load()) {
                cleanupSessions();
                handleHeartbeat(); // 다음 하트비트 예약
            }
            });
    }

    void GameServer::cleanupSessions() {
        std::lock_guard<std::mutex> lock(sessionsMutex_);

        auto it = sessions_.begin();
        while (it != sessions_.end()) {
            if (!it->second || !it->second->isActive()) {
                spdlog::debug("비활성 세션 정리: {}", it->first);
                it = sessions_.erase(it);

                // 통계 업데이트
                {
                    std::lock_guard<std::mutex> statsLock(statsMutex_);
                    if (stats_.currentConnections > 0) {
                        stats_.currentConnections--;
                    }
                }
            }
            else {
                // 타임아웃 체크 (5분)
                if (it->second->isTimedOut(std::chrono::seconds(300))) {
                    spdlog::info("세션 타임아웃: {}", it->first);
                    it->second->stop();
                    it = sessions_.erase(it);

                    // 통계 업데이트
                    {
                        std::lock_guard<std::mutex> statsLock(statsMutex_);
                        if (stats_.currentConnections > 0) {
                            stats_.currentConnections--;
                        }
                    }
                }
                else {
                    ++it;
                }
            }
        }
    }

} // namespace Blokus::Server