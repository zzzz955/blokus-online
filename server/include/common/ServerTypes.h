#pragma once

#include <cstdint>
#include <string>
#include <chrono>
#include <memory>
#include <functional>

namespace Blokus {
    namespace Server {

        // ========================================
        // ���� ����
        // ========================================
        class ClientSession;
        class GameRoom;
        class UserInfo;

        // ========================================
        // Ÿ�� ��Ī ����
        // ========================================
        using ClientSessionPtr = std::shared_ptr<ClientSession>;
        using GameRoomPtr = std::shared_ptr<GameRoom>;
        using UserInfoPtr = std::shared_ptr<UserInfo>;

        // ========================================
        // ���� ���
        // ========================================
        constexpr uint16_t DEFAULT_SERVER_PORT = 7777;
        constexpr int MAX_CONCURRENT_USERS = 1000;
        constexpr int DEFAULT_THREAD_POOL_SIZE = 4;
        constexpr int MAX_ROOM_COUNT = 100;
        constexpr int MAX_PLAYERS_PER_ROOM = 4;

        // ��Ʈ��ũ ���� ���
        constexpr int SOCKET_BUFFER_SIZE = 65536;  // 64KB
        constexpr int MAX_MESSAGE_SIZE = 1024 * 1024;  // 1MB
        constexpr std::chrono::seconds CLIENT_TIMEOUT{ 30 };
        constexpr std::chrono::seconds HEARTBEAT_INTERVAL{ 10 };

        // ���� ���� ���
        constexpr std::chrono::seconds TURN_TIMEOUT{ 120 };  // 2��
        constexpr std::chrono::seconds ROOM_IDLE_TIMEOUT{ 600 };  // 10��

        // ========================================
        // ������ ����
        // ========================================

        // Ŭ���̾�Ʈ ���� ����
        enum class ConnectionState {
            Connected,       // �����
            InLobby,        // �κ� ����
            InRoom,         // �濡 ����
            InGame,         // ���� ��
        };

        // ���� ����
        enum class SessionState {
            Active,
            Idle,
            Expired,
            Invalid
        };

        // �� ����
        enum class RoomState {
            Waiting,    // ��� ��
            Playing,    // ���� ��
            Finished,   // ���� ����
            Disbanded   // �� ��ü
        };

        // �޽��� Ÿ�� (����ȭ�� ����)
        enum class MessageType {
            Unknown = 0,
            Auth = 100,
            Lobby = 200,
            Room = 300,
            Game = 400,
            Chat = 500,
            Error = 900
        };

        // �޽��� ó�� ���
        enum class MessageResult {
            Success,
            Failed,
            InvalidFormat,
            UnknownType,
            InternalError
        };

        // ���� ���� �ڵ�
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
        // ����ü ���� (���� ���� ���ŵ�)
        // ========================================

        // ���� ��� ���� (��Ÿ�� ����)
        struct ServerStats {
            // ���� ���
            int currentConnections = 0;
            int totalConnectionsToday = 0;
            int peakConcurrentConnections = 0;

            // �� ���
            int activeRooms = 0;
            int gamesInProgress = 0;
            int totalGamesToday = 0;

            // ���� ���
            double cpuUsage = 0.0;
            double memoryUsage = 0.0;
            double networkLatency = 0.0;

            // �޽��� ���
            uint64_t messagesReceived = 0;
            uint64_t messagesSent = 0;
            uint64_t bytesReceived = 0;
            uint64_t bytesSent = 0;

            std::chrono::system_clock::time_point serverStartTime;
            std::chrono::system_clock::time_point lastStatsUpdate;
        };

        // Ŭ���̾�Ʈ ���� ����
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

            // ���
            uint64_t messagesSent = 0;
            uint64_t messagesReceived = 0;
            double averageLatency = 0.0;
        };

        // ========================================
        // �Լ� Ÿ�� ����
        // ========================================
        using MessageHandlerFunc = std::function<MessageResult(ClientSessionPtr, const std::string&)>;
        using ErrorCallback = std::function<void(const std::string&, const std::exception&)>;

        // ========================================
        // ��ƿ��Ƽ �Լ���
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