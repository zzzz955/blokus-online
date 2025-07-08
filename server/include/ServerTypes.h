#pragma once

#include "common/Types.h"
#include <chrono>
#include <string>

namespace Blokus {
    namespace Server {

        // ========================================
        // ���� ���� �����
        // ========================================

        // ��Ʈ��ũ ����
        constexpr uint16_t DEFAULT_SERVER_PORT = 12345;           // �⺻ ���� ��Ʈ
        constexpr int MAX_CONCURRENT_USERS = 1000;                // �ִ� ���� ������
        constexpr int MAX_MESSAGE_SIZE = 8192;                    // �ִ� �޽��� ũ�� (8KB)
        constexpr int SOCKET_TIMEOUT_SECONDS = 30;                // ���� Ÿ�Ӿƿ�

        // ���� ���� ����
        constexpr int MAX_ROOMS = 200;                             // �ִ� �� ����
        constexpr int MAX_PLAYERS_PER_ROOM = 4;                   // ��� �ִ� �÷��̾� ��
        constexpr int DEFAULT_TURN_TIME_SECONDS = 30;             // �⺻ �� ���ѽð�
        constexpr int MAX_TURN_TIME_SECONDS = 120;                // �ִ� �� ���ѽð�

        // ���� �۾� ����
        constexpr int CLEANUP_INTERVAL_SECONDS = 30;              // ���� �۾� �ֱ�
        constexpr int STATISTICS_INTERVAL_SECONDS = 60;           // ��� ��� �ֱ�
        constexpr int INACTIVE_SESSION_TIMEOUT_MINUTES = 10;      // ��Ȱ�� ���� Ÿ�Ӿƿ�

        // ä�� ����
        constexpr int MAX_CHAT_MESSAGE_LENGTH = 200;              // �ִ� ä�� �޽��� ����
        constexpr int MAX_CHAT_HISTORY = 100;                     // ��� �ִ� ä�� ���� ��

        // ========================================
        // ���� ���� ��������
        // ========================================

        // �޽��� Ÿ��
        enum class MessageType : uint8_t {
            // ���� ����
            LOGIN_REQUEST = 1,
            LOGIN_RESPONSE = 2,
            REGISTER_REQUEST = 3,
            REGISTER_RESPONSE = 4,
            LOGOUT_REQUEST = 5,

            // �� ����
            CREATE_ROOM_REQUEST = 10,
            CREATE_ROOM_RESPONSE = 11,
            JOIN_ROOM_REQUEST = 12,
            JOIN_ROOM_RESPONSE = 13,
            LEAVE_ROOM_REQUEST = 14,
            LEAVE_ROOM_RESPONSE = 15,
            ROOM_LIST_REQUEST = 16,
            ROOM_LIST_RESPONSE = 17,

            // ���� �÷���
            GAME_START = 20,
            GAME_END = 21,
            BLOCK_PLACEMENT = 22,
            TURN_CHANGE = 23,
            GAME_STATE_UPDATE = 24,

            // ä��
            CHAT_MESSAGE = 30,
            CHAT_BROADCAST = 31,

            // �ý���
            HEARTBEAT = 40,
            SYSTEM_NOTIFICATION = 41,
            ERROR_MESSAGE = 42
        };

        // ���� ����
        enum class ServerState : uint8_t {
            STOPPED = 0,        // ������
            STARTING = 1,       // ���� ��
            RUNNING = 2,        // ���� ��
            STOPPING = 3        // ���� ��
        };

        // ���� ����
        enum class SessionState : uint8_t {
            CONNECTING = 0,     // ���� ��
            CONNECTED = 1,      // �����
            AUTHENTICATED = 2,  // ������
            IN_LOBBY = 3,       // �κ� ����
            IN_ROOM = 4,        // �濡 ����
            IN_GAME = 5,        // ���� ��
            DISCONNECTING = 6,  // ���� ���� ��
            DISCONNECTED = 7    // ���� ������
        };

        // �� ����
        enum class RoomState : uint8_t {
            WAITING = 0,        // ��� ��
            STARTING = 1,       // ���� ���� ��
            PLAYING = 2,        // ���� ��
            FINISHED = 3,       // ���� �Ϸ�
            CLOSED = 4          // �� ����
        };

        // ========================================
        // ���� ���� ����ü��
        // ========================================

        // ���� ��� ����
        struct ServerStatistics {
            std::chrono::steady_clock::time_point startTime;      // ���� ���� �ð�
            uint64_t totalConnections{ 0 };                       // �� ���� ��
            uint64_t currentConnections{ 0 };                     // ���� ���� ��
            uint64_t totalRoomsCreated{ 0 };                      // �� ������ �� ��
            uint64_t currentRooms{ 0 };                           // ���� �� ��
            uint64_t totalGamesPlayed{ 0 };                       // �� �÷��̵� ���� ��
            uint64_t messagesReceived{ 0 };                       // ���� �޽��� ��
            uint64_t messagesSent{ 0 };                           // �۽� �޽��� ��
            uint64_t totalUsers{ 0 };                             // �� ����� ��

            ServerStatistics() : startTime(std::chrono::steady_clock::now()) {}

            // ���� ���� �ð� ���
            std::chrono::seconds getUptime() const {
                auto now = std::chrono::steady_clock::now();
                return std::chrono::duration_cast<std::chrono::seconds>(now - startTime);
            }
        };

        // ��Ʈ��ũ �޽��� ���
        struct MessageHeader {
            uint32_t messageLength;                                // �޽��� ����
            MessageType messageType;                               // �޽��� Ÿ��
            uint32_t sessionId;                                    // ���� ID
            uint32_t sequenceNumber;                               // ���� ��ȣ
            uint8_t flags;                                         // �÷���
            uint8_t reserved[3];                                   // ����� ����Ʈ (�е�)

            MessageHeader() = default;
            MessageHeader(uint32_t length, MessageType type, uint32_t sid)
                : messageLength(length), messageType(type), sessionId(sid)
                , sequenceNumber(0), flags(0), reserved{ 0, 0, 0 } {
            }
        };
        static_assert(sizeof(MessageHeader) == 16, "MessageHeader ũ��� 16����Ʈ���� ��");

        // ���� �̺�Ʈ ����
        struct GameEvent {
            uint32_t roomId;                                       // �� ID
            uint32_t playerId;                                     // �÷��̾� ID
            Common::PlayerColor playerColor;                       // �÷��̾� ����
            std::string eventType;                                 // �̺�Ʈ Ÿ��
            std::string eventData;                                 // �̺�Ʈ ������ (JSON)
            std::chrono::steady_clock::time_point timestamp;       // Ÿ�ӽ�����

            GameEvent() : timestamp(std::chrono::steady_clock::now()) {}
        };

        // �α��� �õ� ���� (���ȿ�)
        struct LoginAttempt {
            std::string ipAddress;                                 // IP �ּ�
            std::string username;                                  // �õ��� ����ڸ�
            bool success;                                          // ���� ����
            std::chrono::steady_clock::time_point timestamp;       // �õ� �ð�

            LoginAttempt(const std::string& ip, const std::string& user, bool result)
                : ipAddress(ip), username(user), success(result)
                , timestamp(std::chrono::steady_clock::now()) {
            }
        };

        // ========================================
        // ��ƿ��Ƽ �Լ���
        // ========================================

        namespace Utils {
            // �޽��� Ÿ���� ���ڿ��� ��ȯ
            std::string messageTypeToString(MessageType type);

            // ���� ���¸� ���ڿ��� ��ȯ
            std::string serverStateToString(ServerState state);

            // ���� ���¸� ���ڿ��� ��ȯ
            std::string sessionStateToString(SessionState state);

            // �� ���¸� ���ڿ��� ��ȯ
            std::string roomStateToString(RoomState state);

            // ���� �ð��� ISO 8601 ���� ���ڿ��� ��ȯ
            std::string currentTimeToString();

            // ����Ʈ ũ�⸦ ����� �б� ���� �������� ��ȯ
            std::string formatBytes(uint64_t bytes);

            // ���� �ð��� ����� �б� ���� �������� ��ȯ
            std::string formatDuration(std::chrono::seconds duration);
        }

    } // namespace Server

    // ========================================
    // Common ���ӽ����̽��� ���� ��� �߰�
    // ========================================
    namespace Common {
        // ���� ���� ����� (Ŭ���̾�Ʈ������ ���)
        constexpr uint16_t DEFAULT_SERVER_PORT = Server::DEFAULT_SERVER_PORT;
        constexpr int MAX_CONCURRENT_USERS = Server::MAX_CONCURRENT_USERS;
        constexpr int MAX_CHAT_MESSAGE_LENGTH = Server::MAX_CHAT_MESSAGE_LENGTH;
    }

} // namespace Blokus