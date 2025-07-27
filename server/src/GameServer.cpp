#include "GameServer.h"
#include "Session.h"
#include "MessageHandler.h"
#include "RoomManager.h"
#include "AuthenticationService.h"
#include "DatabaseManager.h"
#include "ConfigManager.h"
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
        , cleanupTimer_(nullptr)
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

            // 3. 서비스 초기화 (새로 추가)
            if (!initializeServices()) {
                spdlog::error("서비스 초기화 실패");
                return false;
            }

            // 4. 네트워크 초기화
            if (!initializeNetwork()) {
                spdlog::error("네트워크 초기화 실패");
                return false;
            }

            // 5. 통계 초기화
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

        // 하트비트 및 정리 타이머 시작
        startHeartbeatTimer();
        startCleanupTimer();

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
            boost::system::error_code ec;
            acceptor_.close(ec);
        }

        // 2. 서비스들 정리
        cleanupServices();

        // 3. 모든 세션 종료
        {
            std::lock_guard<std::mutex> lock(sessionsMutex_);
            for (auto& [sessionId, session] : sessions_) {
                if (session) {
                    session->stop();
                }
            }
            sessions_.clear();
        }

        // 4. 타이머들 취소
        if (heartbeatTimer_) {
            heartbeatTimer_->cancel();
        }
        if (cleanupTimer_) {
            cleanupTimer_->cancel();
        }

        // 5. IO 컨텍스트 종료
        ioContext_.stop();

        // 6. 스레드 풀 정리
        for (auto& thread : threadPool_) {
            if (thread.joinable()) {
                thread.join();
            }
        }
        threadPool_.clear();

        spdlog::info("GameServer 종료 완료");
    }

    void GameServer::run() {
        spdlog::info("🔧 [DEBUG] run() 메서드 시작");

        if (!initialize()) {
            spdlog::error("서버 초기화 실패");
            return;
        }
        spdlog::info("🔧 [DEBUG] 초기화 완료");

        start();
        spdlog::info("🔧 [DEBUG] start() 완료");

        spdlog::info("서버가 실행 중입니다. Ctrl+C로 종료하세요");
        spdlog::info("🔧 [DEBUG] ioContext_.run() 호출 직전");

        try {
            ioContext_.run();
            spdlog::info("🔧 [DEBUG] ioContext_.run() 완료됨"); // ⚠️ 이게 바로 찍히면 문제
        }
        catch (const std::exception& e) {
            spdlog::error("메인 루프 예외: {}", e.what());
        }

        spdlog::info("🔧 [DEBUG] run() 메서드 종료");
    }

    // ========================================
    // 인증 관련 편의 함수들 (새로 추가)
    // ========================================

    AuthResult GameServer::authenticateUser(const std::string& username, const std::string& password) {
        if (!authService_) {
            return AuthResult(false, "인증 서비스가 초기화되지 않았습니다", "", "", "");
        }

        return authService_->loginUser(username, password);
    }

    RegisterResult GameServer::registerUser(const std::string& username, const std::string& email, const std::string& password) {
        if (!authService_) {
            return RegisterResult(false, "인증 서비스가 초기화되지 않았습니다", "");
        }

        return authService_->registerUser(username, password);
    }

    AuthResult GameServer::loginGuest(const std::string& guestName) {
        if (!authService_) {
            return AuthResult(false, "인증 서비스가 초기화되지 않았습니다", "", "", "");
        }

        return authService_->loginGuest(guestName);
    }

    bool GameServer::logoutUser(const std::string& sessionToken) {
        if (!authService_) {
            return false;
        }

        return authService_->logoutUser(sessionToken);
    }

    std::optional<SessionInfo> GameServer::validateSession(const std::string& sessionToken) {
        if (!authService_) {
            return std::nullopt;
        }

        return authService_->validateSession(sessionToken);
    }

    // ========================================
    // 방 관련 편의 함수들 (새로 추가)
    // ========================================

    int GameServer::createRoom(const std::string& hostId, const std::string& hostUsername,
        const std::string& roomName, bool isPrivate, const std::string& password) {
        if (!roomManager_) {
            return -1;
        }

        return roomManager_->createRoom(hostId, hostUsername, roomName, isPrivate, password);
    }

    bool GameServer::joinRoom(int roomId, std::shared_ptr<Session> client,
        const std::string& userId, const std::string& username,
        const std::string& password) {
        if (!roomManager_) {
            return false;
        }

        return roomManager_->joinRoom(roomId, client, userId, username, password);
    }

    bool GameServer::leaveRoom(int roomId, const std::string& userId) {
        if (!roomManager_) {
            return false;
        }

        return roomManager_->leaveRoom(roomId, userId);
    }

    std::vector<Blokus::Common::RoomInfo> GameServer::getRoomList() const {
        if (!roomManager_) {
            return {};
        }

        return roomManager_->getRoomList();
    }

    std::shared_ptr<GameRoom> GameServer::getRoom(int roomId) {
        if (!roomManager_) {
            return nullptr;
        }

        return roomManager_->getRoom(roomId);
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

        // 🔥 핵심 수정: Session에 MessageHandler가 없으면 생성
        if (!session->getMessageHandler()) {
            spdlog::info("🔧 [addSession] MessageHandler 생성 - SessionId: {}", sessionId);

            // MessageHandler 생성 및 설정
            auto messageHandler = std::make_unique<MessageHandler>(
                session.get(),          // Session 포인터
                roomManager_.get(),     // RoomManager 포인터  
                authService_.get(),     // AuthenticationService 포인터
                databaseManager_.get(),
                this                    // GameServer 포인터
            );

            // Session에 MessageHandler 설정
            session->setMessageHandler(std::move(messageHandler));

            spdlog::info("✅ [addSession] MessageHandler 생성 완료 - SessionId: {}", sessionId);
        }

        // 세션 기본 콜백만 설정 (연결 해제, 메시지 수신)
        session->setDisconnectCallback([this](const std::string& id) {
            onSessionDisconnect(id);
            });

        session->setMessageCallback([this](const std::string& id, const std::string& msg) {
            onSessionMessage(id, msg);
            });

        // 🔥 핵심 변경: MessageHandler 콜백 모두 제거!
        // MessageHandler가 직접 AuthService, RoomManager와 상호작용하므로
        // 중간 콜백이 불필요함. 중복 처리 방지!

        spdlog::info("✅ [addSession] 세션 설정 완료 (콜백 없음) - SessionId: {}", sessionId);
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
    
    std::vector<std::shared_ptr<Session>> GameServer::getLobbyUsers() const {
        std::vector<std::shared_ptr<Session>> lobbyUsers;
        
        std::lock_guard<std::mutex> lock(sessionsMutex_);
        for (const auto& [sessionId, session] : sessions_) {
            // Connected 상태가 아닌 모든 사용자 (InLobby + InRoom + InGame)를 포함
            if (session && session->isActive() && 
                (session->isInLobby() || session->isInRoom() || session->isInGame())) {
                lobbyUsers.push_back(session);
            }
        }
        
        return lobbyUsers;
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
            spdlog::info("데이터베이스 연결 테스트 중...");

            // DatabaseManager 인스턴스 생성 및 테스트
            databaseManager_ = std::make_shared<DatabaseManager>();
            if (databaseManager_->initialize()) {
                auto stats = databaseManager_->getStats();
                spdlog::info("데이터베이스 연결 성공");
                spdlog::info("DB 통계: {} 사용자, {} 게임", stats.totalUsers, stats.totalGames);
                return true;
            }
            else {
                spdlog::warn("데이터베이스 연결 실패 - DB 없이 서버 실행");
                databaseManager_.reset(); // DB 사용 안 함
                return true; // DB 없이도 서버 실행 허용
            }
        }
        catch (const std::exception& e) {
            spdlog::error("데이터베이스 초기화 오류: {}", e.what());
            spdlog::warn("데이터베이스 지원 없이 계속 진행");
            databaseManager_.reset();
            return true; // DB 실패해도 서버 계속 실행
        }
    }

    bool GameServer::initializeServices() {
        spdlog::info("서비스들 초기화 시작");

        try {
            // AuthenticationService 초기화
            authService_ = std::make_unique<AuthenticationService>(databaseManager_);
            if (!authService_ || !authService_->initialize()) {
                spdlog::error("AuthenticationService 초기화 실패");
                return false;
            }
            spdlog::info("AuthenticationService 초기화 완료");

            // RoomManager 초기화
            roomManager_ = std::make_unique<RoomManager>();
            if (!roomManager_) {
                spdlog::error("RoomManager 생성 실패");
                return false;
            }
            
            // RoomManager에 DatabaseManager 설정
            roomManager_->setDatabaseManager(databaseManager_);
            spdlog::info("RoomManager 초기화 완료 (DB 연결 포함)");

            spdlog::info("모든 서비스 초기화 완료");
            return true;
        }
        catch (const std::exception& e) {
            spdlog::error("서비스 초기화 중 예외 발생: {}", e.what());
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
        
        // 세션 제거 전에 사용자 정보 저장 (로비 사용자 제거 브로드캐스트 및 방 나가기용)
        std::string username;
        std::string userId;
        bool wasInLobby = false;
        bool wasInRoom = false;
        bool wasInGame = false;
        int roomId = -1;
        {
            std::lock_guard<std::mutex> lock(sessionsMutex_);
            auto it = sessions_.find(sessionId);
            if (it != sessions_.end()) {
                username = it->second->getUsername();
                userId = it->second->getUserId();
                wasInLobby = it->second->isInLobby();
                wasInRoom = it->second->isInRoom();
                wasInGame = it->second->isInGame();
                if (wasInRoom || wasInGame) {
                    roomId = it->second->getCurrentRoomId();
                }
            }
        }
        
        // 방에 있던 사용자가 연결 해제된 경우 자동으로 방에서 나가기
        if ((wasInRoom || wasInGame) && !userId.empty() && roomManager_) {
            if (wasInGame) {
                spdlog::warn("🎮 게임 중 세션 강제 종료로 인한 방 {} 나가기: {} (좀비방 방지)", roomId, username);
            } else {
                spdlog::info("🏠 방 대기 중 세션 연결 해제로 인한 방 {} 나가기: {}", roomId, username);
            }
            roomManager_->leaveRoom(userId);
        }
        
        removeSession(sessionId);
        
        // 로비에 있던 사용자가 연결 해제된 경우 다른 로비 사용자들에게 브로드캐스트
        if (wasInLobby && !username.empty()) {
            broadcastLobbyUserLeft(username);
        }
    }

    void GameServer::onSessionMessage(const std::string& sessionId, const std::string& message) {
        // 통계 업데이트
        {
            std::lock_guard<std::mutex> lock(statsMutex_);
            stats_.messagesReceived++;
        }

        spdlog::debug("세션 {}에서 메시지 수신: {}", sessionId,
            message.length() > 50 ? message.substr(0, 50) + "..." : message);
    }

    // ========================================
    // 로비 브로드캐스트 메서드들
    // ========================================
    
    void GameServer::broadcastLobbyUserLeft(const std::string& username) {
        try {
            std::string message = "LOBBY_USER_LEFT:" + username;
            auto lobbyUsers = getLobbyUsers();
            
            spdlog::info("🔊 로비 사용자 퇴장 브로드캐스트: '{}' -> {}명에게", username, lobbyUsers.size());
            
            for (const auto& session : lobbyUsers) {
                if (session && session->isActive()) {
                    session->sendMessage(message);
                }
            }
        }
        catch (const std::exception& e) {
            spdlog::error("로비 사용자 퇴장 브로드캐스트 중 오류: {}", e.what());
        }
    }
    
    void GameServer::broadcastLobbyUserListPeriodically() {
        try {
            auto lobbyUsers = getLobbyUsers();
            if (lobbyUsers.empty()) {
                spdlog::debug("🔄 주기적 브로드캐스트: 로비 사용자 없음");
                return; // 로비 사용자가 없으면 브로드캐스트하지 않음
            }
            
            // LOBBY_USER_LIST 메시지 생성
            std::ostringstream response;
            response << "LOBBY_USER_LIST:" << lobbyUsers.size();
            
            int validUserCount = 0;
            for (const auto& lobbySession : lobbyUsers) {
                if (lobbySession && lobbySession->isActive() && !lobbySession->getUsername().empty()) {
                    std::string username = lobbySession->getUsername();
                    int userLevel = lobbySession->getUserLevel();
                    std::string userStatus = lobbySession->getUserStatusString();
                    
                    response << ":" << username << "," << userLevel << "," << userStatus;
                    validUserCount++;
                }
            }
            
            std::string message = response.str();
            
            // 모든 로비 사용자에게 브로드캐스트
            int sentCount = 0;
            for (const auto& session : lobbyUsers) {
                if (session && session->isActive() && session->isInLobby()) {
                    session->sendMessage(message);
                    sentCount++;
                }
            }
        }
        catch (const std::exception& e) {
            spdlog::error("주기적 로비 사용자 목록 브로드캐스트 중 오류: {}", e.what());
        }
    }

    // ========================================
    // 정리 작업 (업데이트됨)
    // ========================================

    void GameServer::startHeartbeatTimer() {
        heartbeatTimer_ = std::make_unique<boost::asio::steady_timer>(ioContext_);
        handleHeartbeat();
    }

    void GameServer::startCleanupTimer() {
        cleanupTimer_ = std::make_unique<boost::asio::steady_timer>(ioContext_);
        cleanupTimer_->expires_after(std::chrono::seconds(30)); // 30초마다 정리 (좀비방 방지)
        cleanupTimer_->async_wait([this](const boost::system::error_code& error) {
            if (!error && running_.load()) {
                performCleanup();
                startCleanupTimer(); // 재귀 호출
            }
            });
    }

    void GameServer::handleHeartbeat() {
        if (!running_.load()) {
            return;
        }

        // 10초마다 하트비트 (로비 동기화 포함)
        heartbeatTimer_->expires_after(std::chrono::seconds(10));
        heartbeatTimer_->async_wait([this](const boost::system::error_code& error) {
            if (!error && running_.load()) {
                cleanupSessions();
                broadcastLobbyUserListPeriodically(); // 주기적 로비 사용자 목록 브로드캐스트
                logServerStats(); // 통계 로그
                handleHeartbeat(); // 다음 하트비트 예약
            }
            });
    }

    void GameServer::performCleanup() {
        try {
            spdlog::debug("주기적 정리 작업 시작");

            // 만료된 인증 세션 정리
            if (authService_) {
                authService_->cleanupExpiredSessions();
            }

            // 빈 방 정리
            if (roomManager_) {
                roomManager_->cleanupEmptyRooms();
            }

            // 네트워크 세션 정리
            cleanupSessions();

            spdlog::debug("주기적 정리 작업 완료");
        }
        catch (const std::exception& e) {
            spdlog::error("정리 작업 중 예외 발생: {}", e.what());
        }
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
                // 타임아웃 체크 - 게임 중인 세션은 더 짧은 타임아웃 적용
                std::chrono::seconds timeoutDuration = std::chrono::seconds(300); // 기본 5분
                if (it->second->isInGame()) {
                    timeoutDuration = std::chrono::seconds(120); // 게임 중은 2분으로 단축
                }
                
                if (it->second->isTimedOut(timeoutDuration)) {
                    if (it->second->isInGame()) {
                        spdlog::warn("🎮 게임 중 세션 타임아웃 (좀비방 방지): {} ({}분)", it->first, timeoutDuration.count() / 60);
                    } else {
                        spdlog::info("세션 타임아웃: {} ({}분)", it->first, timeoutDuration.count() / 60);
                    }
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

    void GameServer::cleanupServices() {
        spdlog::info("서비스 리소스 정리 시작");

        // AuthenticationService 정리
        if (authService_) {
            authService_->shutdown();
            authService_.reset();
            spdlog::info("AuthenticationService 정리 완료");
        }

        // RoomManager 정리
        if (roomManager_) {
            roomManager_->broadcastToAllRooms("SERVER_SHUTDOWN");
            roomManager_.reset();
            spdlog::info("RoomManager 정리 완료");
        }

        // DatabaseManager 정리
        if (databaseManager_) {
            databaseManager_->shutdown();
            databaseManager_.reset();
            spdlog::info("DatabaseManager 정리 완료");
        }

        spdlog::info("서비스 리소스 정리 완료");
    }

    void GameServer::logServerStats() {
        std::lock_guard<std::mutex> lock(statsMutex_);

        size_t roomCount = roomManager_ ? roomManager_->getRoomCount() : 0;
        size_t playersInRooms = roomManager_ ? roomManager_->getTotalPlayers() : 0;
        size_t activeAuthSessions = authService_ ? authService_->getActiveSessionCount() : 0;

        auto now = std::chrono::system_clock::now();
        auto uptime = std::chrono::duration_cast<std::chrono::seconds>(now - stats_.serverStartTime).count();

        spdlog::debug("=== 서버 통계 ===");
        spdlog::debug("현재 연결: {}", stats_.currentConnections);
        spdlog::debug("인증된 세션: {}", activeAuthSessions);
        spdlog::debug("총 연결 수: {}", stats_.totalConnectionsToday);
        spdlog::debug("피크 연결: {}", stats_.peakConcurrentConnections);
        spdlog::debug("활성 방 수: {}", roomCount);
        spdlog::debug("방 내 플레이어: {}", playersInRooms);
        spdlog::debug("처리된 메시지: {}", stats_.messagesReceived);
        spdlog::debug("업타임: {}초 ({}분)", uptime, uptime / 60);
        spdlog::debug("================");
    }

} // namespace Blokus::Server