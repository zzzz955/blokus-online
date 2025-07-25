#pragma once

#include <memory>
#include <atomic>
#include <thread>
#include <vector>
#include <unordered_map>
#include <mutex>
#include <boost/asio.hpp>
#include <spdlog/spdlog.h>

// 기존 정의된 타입들 사용
#include "ServerTypes.h"
#include "ConfigManager.h"
#include "Types.h"

// 전방 선언
namespace Blokus::Server {
    class Session;
    class NetworkManager;
    class DatabaseManager;
    class RoomManager;
    class AuthenticationService;
    class MessageHandler;

    struct AuthResult;
    struct RegisterResult;
    struct SessionInfo;

    // 메인 게임 서버 클래스
    class GameServer {
    public:
        explicit GameServer();
        ~GameServer();

        // 기본 서버 제어
        bool initialize();
        void start();
        void stop();
        void run();
        bool isRunning() const { return running_.load(); }

        // ConfigManager를 통해 설정 접근
        static int getServerPort() { return ConfigManager::serverPort; }
        static int getMaxClients() { return ConfigManager::maxClients; }
        static int getThreadPoolSize() { return ConfigManager::threadPoolSize; }

        // ========================================
        // 인증 관련 편의 함수들
        // ========================================
        AuthResult authenticateUser(const std::string& username, const std::string& password);
        RegisterResult registerUser(const std::string& username, const std::string& email, const std::string& password);
        AuthResult loginGuest(const std::string& guestName = "");
        bool logoutUser(const std::string& sessionToken);
        std::optional<SessionInfo> validateSession(const std::string& sessionToken);

        // ========================================
        // 방 관련 편의 함수들
        // ========================================
        int createRoom(const std::string& hostId, const std::string& hostUsername,
            const std::string& roomName, bool isPrivate = false, const std::string& password = "");
        bool joinRoom(int roomId, std::shared_ptr<Session> client, const std::string& userId,
            const std::string& username, const std::string& password = "");
        bool leaveRoom(int roomId, const std::string& userId);
        std::vector<Blokus::Common::RoomInfo> getRoomList() const;
        std::shared_ptr<class GameRoom> getRoom(int roomId);

        // 클라이언트 관리
        void addSession(std::shared_ptr<Session> session);
        void removeSession(const std::string& sessionId);

        // 세션 조회 - 일시적 사용용 (shared_ptr)
        std::shared_ptr<Session> getSession(const std::string& sessionId);

        // 세션 조회 - 관찰용 (weak_ptr) - 생명주기에 영향 없음
        std::weak_ptr<Session> getSessionWeak(const std::string& sessionId);

        // 안전한 세션 작업 - 람다로 작업 전달
        bool withSession(const std::string& sessionId,
            std::function<void(std::shared_ptr<Session>)> action);
        
        // 로비 사용자 목록 조회 - 로비 브로드캐스팅용
        std::vector<std::shared_ptr<Session>> getLobbyUsers() const;
        
        // 로비 브로드캐스트 메서드들
        void broadcastLobbyUserLeft(const std::string& username);
        void broadcastLobbyUserListPeriodically();

        // 접근자
        boost::asio::io_context& getIOContext() { return ioContext_; }
        std::shared_ptr<DatabaseManager> getDatabaseManager() const { return databaseManager_; }

        // 통계 접근자
        int getCurrentConnections() const {
            std::lock_guard<std::mutex> lock(statsMutex_);
            return stats_.currentConnections;
        }

        ServerStats getStats() const {
            std::lock_guard<std::mutex> lock(statsMutex_);
            return stats_;
        }

    private:
        // 내부 초기화 함수들
        bool initializeConfig();
        bool initializeDatabase();
        bool initializeServices(); // 🔥 새로 추가: RoomManager, AuthService 초기화
        bool initializeNetwork();

        // 네트워크 처리
        void startAccepting();
        void handleNewConnection(std::shared_ptr<Session> session,
            const boost::system::error_code& error);

        // 세션 이벤트 핸들러 (콜백으로 호출됨)
        void onSessionDisconnect(const std::string& sessionId);
        void onSessionMessage(const std::string& sessionId, const std::string& message);

        // MessageHandler 콜백 처리 함수들
        void handleAuthentication(const std::string& sessionId, const std::string& username, bool success);
        void handleRegistration(const std::string& sessionId, const std::string& username,
            const std::string& email, const std::string& password);
        void handleRoomAction(const std::string& sessionId, const std::string& action, const std::string& data);
        void handleChatBroadcast(const std::string& sessionId, const std::string& message);

        // 정리 작업
        void startHeartbeatTimer();
        void startCleanupTimer(); // 🔥 새로 추가: 주기적 정리 작업
        void handleHeartbeat();
        void performCleanup(); // 🔥 새로 추가: 통합 정리 작업
        void cleanupSessions();
        void cleanupServices(); // 🔥 새로 추가: 서비스 정리

        // 통계 및 로깅
        void logServerStats(); // 🔥 새로 추가: 서버 통계 로그

    private:
        // 기본 상태
        std::atomic<bool> running_{ false };

        // Boost.Asio 핵심
        boost::asio::io_context ioContext_;
        boost::asio::ip::tcp::acceptor acceptor_;
        std::vector<std::thread> threadPool_;

        // 타이머들
        std::unique_ptr<boost::asio::steady_timer> heartbeatTimer_;
        std::unique_ptr<boost::asio::steady_timer> cleanupTimer_; // 🔥 새로 추가

        // 🔥 핵심 서비스들 (새로 추가)
        std::shared_ptr<DatabaseManager> databaseManager_;
        std::unique_ptr<RoomManager> roomManager_;
        std::unique_ptr<AuthenticationService> authService_;

        // 매니저들 (향후 구현 예정)
        //std::unique_ptr<NetworkManager> networkManager_;

        // 세션 관리
        std::unordered_map<std::string, std::shared_ptr<Session>> sessions_;
        mutable std::mutex sessionsMutex_;

        // 서버 통계 (ServerTypes.h의 ServerStats 사용)
        ServerStats stats_;
        mutable std::mutex statsMutex_;
    };

} // namespace Blokus::Server