#include "core/GameServer.h"
#include "core/Session.h"
#include "handler/MessageHandler.h"  // MessageHandler 완전한 정의 필요
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
        // , networkManager_(nullptr)  // 현재는 사용하지 않음
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

        // MessageHandler 콜백 설정
        if (session->getMessageHandler()) {
            auto handler = session->getMessageHandler();

            handler->setAuthCallback([this](const std::string& id, const std::string& username, bool success) {
                handleAuthentication(id, username, success);
                });

            handler->setRoomCallback([this](const std::string& id, const std::string& action, const std::string& data) {
                handleRoomAction(id, action, data);
                });

            handler->setChatCallback([this](const std::string& id, const std::string& message) {
                handleChatBroadcast(id, message);
                });
        }
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
            // 여기서 실제 DB 매니저 초기화
            // 현재는 싱글톤 패턴이므로 연결 테스트만 수행
            spdlog::info("Testing database connectivity...");

            // TODO: 실제 DatabaseManager 인스턴스 생성 및 관리
            // dbManager_ = std::make_unique<DatabaseManager>();
            // if (!dbManager_->initialize()) return false;

            // 현재는 간단한 연결 테스트만
            DatabaseManager testManager;
            if (testManager.initialize()) {
                auto stats = testManager.getStats();
                spdlog::info("Database connected successfully");
                spdlog::info("DB Stats: {} users, {} games", stats.totalUsers, stats.totalGames);
                testManager.shutdown();
                return true;
            }
            else {
                spdlog::warn("Database connection failed - server will run without DB features");
                return true; // DB 없이도 서버 실행 허용
            }

        }
        catch (const std::exception& e) {
            spdlog::error("Database initialization error: {}", e.what());
            spdlog::warn("Continuing without database support");
            return true; // DB 실패해도 서버 계속 실행
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
        // 메시지 핸들링은 Session의 MessageHandler가 처리하므로
        // 여기서는 단순 로깅만 수행
        spdlog::debug("세션 {}에서 메시지 수신: {}", sessionId, message.substr(0, 100));
    }

    // ========================================
    // MessageHandler 콜백 처리 함수들
    // ========================================

    void GameServer::handleAuthentication(const std::string& sessionId, const std::string& username, bool success) {
        auto session = getSession(sessionId);
        if (!session) {
            spdlog::warn("존재하지 않는 세션에서 인증 시도: {}", sessionId);
            return;
        }

        if (success) {
            spdlog::info("사용자 인증 성공: {} (세션: {})", username, sessionId);

            // TODO: DatabaseManager를 통한 실제 인증 처리
            // - 사용자 정보 DB에서 조회
            // - 마지막 로그인 시간 업데이트
            // - 사용자 통계 갱신

            // 현재는 간단한 응답만
            session->sendMessage("WELCOME:" + username);

            // 통계 업데이트
            {
                std::lock_guard<std::mutex> lock(statsMutex_);
                // stats_.authenticatedUsers++; // 향후 추가
            }

        }
        else {
            spdlog::warn("사용자 인증 실패: {} (세션: {})", username, sessionId);
            // 연결 유지하되 재시도 허용
        }
    }

    void GameServer::handleRoomAction(const std::string& sessionId, const std::string& action, const std::string& data) {
        auto session = getSession(sessionId);
        if (!session || !session->isAuthenticated()) {
            spdlog::warn("비인증 세션에서 방 액션 시도: {} - {}", sessionId, action);
            if (session) {
                session->sendMessage("ERROR:Authentication required for room actions");
            }
            return;
        }

        spdlog::info("방 액션 처리: {} -> {} (데이터: {})", session->getUsername(), action, data);

        // TODO: RoomManager 구현 후 실제 방 관리 로직 연결
        if (action == "list") {
            // 방 목록 요청
            std::string roomList = "ROOM_LIST:1:TestRoom1:2/4,2:TestRoom2:1/4,3:MyGame:3/4";
            session->sendMessage(roomList);

        }
        else if (action == "create") {
            // 방 생성 요청
            std::string roomName = data.empty() ? "New Room" : data;
            spdlog::info("방 생성 요청: {} by {}", roomName, session->getUsername());

            // 더미 방 ID 생성
            static int nextRoomId = 100;
            int roomId = ++nextRoomId;

            session->sendMessage("ROOM_CREATED:" + std::to_string(roomId) + ":" + roomName);

        }
        else if (action == "join") {
            // 방 참가 요청
            std::string roomId = data;
            spdlog::info("방 참가 요청: 방ID {} by {}", roomId, session->getUsername());

            // 간단한 검증
            if (roomId.empty()) {
                session->sendMessage("ERROR:Room ID required");
            }
            else {
                session->sendMessage("ROOM_JOINED:" + roomId + ":BLUE"); // 더미 색상
            }

        }
        else if (action == "leave") {
            // 방 나가기 요청
            spdlog::info("방 나가기 요청: {}", session->getUsername());
            session->sendMessage("ROOM_LEFT:OK");

        }
        else {
            session->sendMessage("ERROR:Unknown room action: " + action);
        }
    }

    void GameServer::handleChatBroadcast(const std::string& sessionId, const std::string& message) {
        auto session = getSession(sessionId);
        if (!session || !session->isAuthenticated()) {
            spdlog::warn("비인증 세션에서 채팅 시도: {}", sessionId);
            return;
        }

        std::string username = session->getUsername();
        std::string chatMessage = "CHAT:" + username + ":" + message;

        spdlog::info("채팅 브로드캐스트: {} -> {}", username, message);

        // 모든 인증된 세션에 브로드캐스트
        std::lock_guard<std::mutex> lock(sessionsMutex_);
        int broadcastCount = 0;

        for (const auto& [otherSessionId, otherSession] : sessions_) {
            if (otherSession && otherSession->isAuthenticated()) {
                otherSession->sendMessage(chatMessage);
                broadcastCount++;
            }
        }

        spdlog::debug("채팅 메시지를 {}개 세션에 브로드캐스트", broadcastCount);
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