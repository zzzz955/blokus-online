#pragma once

#include <cstdint>
#include <string>
#include <chrono>
#include <memory>
#include <functional>

namespace Blokus {
    namespace Server {

        // ========================================
        // 전방 선언
        // ========================================
        class ClientSession;
        class GameRoom;
        class UserInfo;

        // ========================================
        // 타입 별칭 정의
        // ========================================
        using ClientSessionPtr = std::shared_ptr<ClientSession>;
        using GameRoomPtr = std::shared_ptr<GameRoom>;
        using UserInfoPtr = std::shared_ptr<UserInfo>;

        // ========================================
        // 서버 상수
        // ========================================
        constexpr uint16_t DEFAULT_SERVER_PORT = 7777;
        constexpr int MAX_CONCURRENT_USERS = 1000;
        constexpr int DEFAULT_THREAD_POOL_SIZE = 4;
        constexpr int MAX_ROOM_COUNT = 100;
        constexpr int MAX_PLAYERS_PER_ROOM = 4;

        // 네트워크 관련 상수
        constexpr int SOCKET_BUFFER_SIZE = 65536;  // 64KB
        constexpr int MAX_MESSAGE_SIZE = 1024 * 1024;  // 1MB
        constexpr std::chrono::seconds CLIENT_TIMEOUT{ 30 };
        constexpr std::chrono::seconds HEARTBEAT_INTERVAL{ 10 };

        // 게임 관련 상수
        constexpr std::chrono::seconds TURN_TIMEOUT{ 120 };  // 2분
        constexpr std::chrono::seconds ROOM_IDLE_TIMEOUT{ 600 };  // 10분

        // ========================================
        // 열거형 정의
        // ========================================

        // 클라이언트 연결 상태
        enum class ConnectionState {
            Connected,       // 연결됨
            InLobby,        // 로비에 있음
            InRoom,         // 방에 있음
            InGame,         // 게임 중
        };

        // 세션 상태
        enum class SessionState {
            Active,
            Idle,
            Expired,
            Invalid
        };

        // 방 상태
        enum class RoomState {
            Waiting,    // 대기 중
            Playing,    // 게임 중
            Finished,   // 게임 종료
            Disbanded   // 방 해체
        };

        // 메시지 타입 (간소화된 버전)
        enum class MessageType {
            Unknown = 0,
            Auth = 100,
            Lobby = 200,
            Room = 300,
            Game = 400,
            Chat = 500,
            Error = 900
        };

        // 메시지 처리 결과
        enum class MessageResult {
            Success,
            Failed,
            InvalidFormat,
            UnknownType,
            InternalError
        };

        // 서버 오류 코드
        enum class ServerErrorCode {
            None = 0,
            ConnectionFailed = 1000,
            AuthenticationFailed = 1001,
            SessionExpired = 1002,
            TooManyConnections = 1003,
            RoomNotFound = 2000,
            RoomFull = 2001,
            RoomPasswordIncorrect = 2002,
            AlreadyInRoom = 2003,
            NotInRoom = 2004,
            NotRoomHost = 2005,
            GameNotStarted = 3000,
            GameAlreadyStarted = 3001,
            InvalidMove = 3002,
            NotYourTurn = 3003,
            DatabaseError = 4000,
            InternalError = 4002,
            ServiceUnavailable = 4003
        };

        // ========================================
        // 구조체 정의 (설정 관련 제거됨)
        // ========================================

        // 서버 통계 정보 (런타임 상태)
        struct ServerStats {
            // 연결 통계
            int currentConnections = 0;
            int totalConnectionsToday = 0;
            int peakConcurrentConnections = 0;

            // 방 통계
            int activeRooms = 0;
            int gamesInProgress = 0;
            int totalGamesToday = 0;

            // 성능 통계
            double cpuUsage = 0.0;
            double memoryUsage = 0.0;
            double networkLatency = 0.0;

            // 메시지 통계
            uint64_t messagesReceived = 0;
            uint64_t messagesSent = 0;
            uint64_t bytesReceived = 0;
            uint64_t bytesSent = 0;

            std::chrono::system_clock::time_point serverStartTime;
            std::chrono::system_clock::time_point lastStatsUpdate;
        };

        // 클라이언트 세션 정보
        struct ClientSession {
            std::string sessionId;
            std::string username;
            std::string userId;
            ConnectionState state = ConnectionState::Connected;

            std::chrono::system_clock::time_point connectedAt;
            std::chrono::system_clock::time_point lastActivity;

            int currentRoomId = -1;
            std::string clientVersion;
            std::string ipAddress;

            // 통계
            uint64_t messagesSent = 0;
            uint64_t messagesReceived = 0;
            double averageLatency = 0.0;
        };

        // ========================================
        // 함수 타입 정의
        // ========================================
        using MessageHandlerFunc = std::function<MessageResult(ClientSessionPtr, const std::string&)>;
        using ErrorCallback = std::function<void(const std::string&, const std::exception&)>;

        // ========================================
        // 유틸리티 함수들
        // ========================================
        std::string errorCodeToString(ServerErrorCode code);
        std::string connectionStateToString(ConnectionState state);
        std::string sessionStateToString(SessionState state);
        std::string roomStateToString(RoomState state);
        std::string messageTypeToString(MessageType type);

        bool isValidUsername(const std::string& username);
        bool isValidRoomName(const std::string& roomName);
        std::string generateSessionId();
        std::string hashPassword(const std::string& password);
        bool verifyPassword(const std::string& password, const std::string& hash);

    } // namespace Server
} // namespace Blokus