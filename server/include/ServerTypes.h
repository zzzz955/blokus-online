#pragma once

#include "common/Types.h"
#include <chrono>
#include <string>

namespace Blokus {
    namespace Server {

        // ========================================
        // 서버 설정 상수들
        // ========================================

        // 네트워크 설정
        constexpr uint16_t DEFAULT_SERVER_PORT = 12345;           // 기본 서버 포트
        constexpr int MAX_CONCURRENT_USERS = 1000;                // 최대 동시 접속자
        constexpr int MAX_MESSAGE_SIZE = 8192;                    // 최대 메시지 크기 (8KB)
        constexpr int SOCKET_TIMEOUT_SECONDS = 30;                // 소켓 타임아웃

        // 게임 관련 설정
        constexpr int MAX_ROOMS = 200;                             // 최대 방 개수
        constexpr int MAX_PLAYERS_PER_ROOM = 4;                   // 방당 최대 플레이어 수
        constexpr int DEFAULT_TURN_TIME_SECONDS = 30;             // 기본 턴 제한시간
        constexpr int MAX_TURN_TIME_SECONDS = 120;                // 최대 턴 제한시간

        // 정리 작업 간격
        constexpr int CLEANUP_INTERVAL_SECONDS = 30;              // 정리 작업 주기
        constexpr int STATISTICS_INTERVAL_SECONDS = 60;           // 통계 출력 주기
        constexpr int INACTIVE_SESSION_TIMEOUT_MINUTES = 10;      // 비활성 세션 타임아웃

        // 채팅 관련
        constexpr int MAX_CHAT_MESSAGE_LENGTH = 200;              // 최대 채팅 메시지 길이
        constexpr int MAX_CHAT_HISTORY = 100;                     // 방당 최대 채팅 보관 수

        // ========================================
        // 서버 전용 열거형들
        // ========================================

        // 메시지 타입
        enum class MessageType : uint8_t {
            // 인증 관련
            LOGIN_REQUEST = 1,
            LOGIN_RESPONSE = 2,
            REGISTER_REQUEST = 3,
            REGISTER_RESPONSE = 4,
            LOGOUT_REQUEST = 5,

            // 방 관리
            CREATE_ROOM_REQUEST = 10,
            CREATE_ROOM_RESPONSE = 11,
            JOIN_ROOM_REQUEST = 12,
            JOIN_ROOM_RESPONSE = 13,
            LEAVE_ROOM_REQUEST = 14,
            LEAVE_ROOM_RESPONSE = 15,
            ROOM_LIST_REQUEST = 16,
            ROOM_LIST_RESPONSE = 17,

            // 게임 플레이
            GAME_START = 20,
            GAME_END = 21,
            BLOCK_PLACEMENT = 22,
            TURN_CHANGE = 23,
            GAME_STATE_UPDATE = 24,

            // 채팅
            CHAT_MESSAGE = 30,
            CHAT_BROADCAST = 31,

            // 시스템
            HEARTBEAT = 40,
            SYSTEM_NOTIFICATION = 41,
            ERROR_MESSAGE = 42
        };

        // 서버 상태
        enum class ServerState : uint8_t {
            STOPPED = 0,        // 중지됨
            STARTING = 1,       // 시작 중
            RUNNING = 2,        // 실행 중
            STOPPING = 3        // 중지 중
        };

        // 세션 상태
        enum class SessionState : uint8_t {
            CONNECTING = 0,     // 연결 중
            CONNECTED = 1,      // 연결됨
            AUTHENTICATED = 2,  // 인증됨
            IN_LOBBY = 3,       // 로비에 있음
            IN_ROOM = 4,        // 방에 있음
            IN_GAME = 5,        // 게임 중
            DISCONNECTING = 6,  // 연결 해제 중
            DISCONNECTED = 7    // 연결 해제됨
        };

        // 방 상태
        enum class RoomState : uint8_t {
            WAITING = 0,        // 대기 중
            STARTING = 1,       // 게임 시작 중
            PLAYING = 2,        // 게임 중
            FINISHED = 3,       // 게임 완료
            CLOSED = 4          // 방 닫힘
        };

        // ========================================
        // 서버 전용 구조체들
        // ========================================

        // 서버 통계 정보
        struct ServerStatistics {
            std::chrono::steady_clock::time_point startTime;      // 서버 시작 시간
            uint64_t totalConnections{ 0 };                       // 총 연결 수
            uint64_t currentConnections{ 0 };                     // 현재 연결 수
            uint64_t totalRoomsCreated{ 0 };                      // 총 생성된 방 수
            uint64_t currentRooms{ 0 };                           // 현재 방 수
            uint64_t totalGamesPlayed{ 0 };                       // 총 플레이된 게임 수
            uint64_t messagesReceived{ 0 };                       // 수신 메시지 수
            uint64_t messagesSent{ 0 };                           // 송신 메시지 수
            uint64_t totalUsers{ 0 };                             // 총 사용자 수

            ServerStatistics() : startTime(std::chrono::steady_clock::now()) {}

            // 서버 가동 시간 계산
            std::chrono::seconds getUptime() const {
                auto now = std::chrono::steady_clock::now();
                return std::chrono::duration_cast<std::chrono::seconds>(now - startTime);
            }
        };

        // 네트워크 메시지 헤더
        struct MessageHeader {
            uint32_t messageLength;                                // 메시지 길이
            MessageType messageType;                               // 메시지 타입
            uint32_t sessionId;                                    // 세션 ID
            uint32_t sequenceNumber;                               // 순서 번호
            uint8_t flags;                                         // 플래그
            uint8_t reserved[3];                                   // 예약된 바이트 (패딩)

            MessageHeader() = default;
            MessageHeader(uint32_t length, MessageType type, uint32_t sid)
                : messageLength(length), messageType(type), sessionId(sid)
                , sequenceNumber(0), flags(0), reserved{ 0, 0, 0 } {
            }
        };
        static_assert(sizeof(MessageHeader) == 16, "MessageHeader 크기는 16바이트여야 함");

        // 게임 이벤트 정보
        struct GameEvent {
            uint32_t roomId;                                       // 방 ID
            uint32_t playerId;                                     // 플레이어 ID
            Common::PlayerColor playerColor;                       // 플레이어 색상
            std::string eventType;                                 // 이벤트 타입
            std::string eventData;                                 // 이벤트 데이터 (JSON)
            std::chrono::steady_clock::time_point timestamp;       // 타임스탬프

            GameEvent() : timestamp(std::chrono::steady_clock::now()) {}
        };

        // 로그인 시도 정보 (보안용)
        struct LoginAttempt {
            std::string ipAddress;                                 // IP 주소
            std::string username;                                  // 시도한 사용자명
            bool success;                                          // 성공 여부
            std::chrono::steady_clock::time_point timestamp;       // 시도 시간

            LoginAttempt(const std::string& ip, const std::string& user, bool result)
                : ipAddress(ip), username(user), success(result)
                , timestamp(std::chrono::steady_clock::now()) {
            }
        };

        // ========================================
        // 유틸리티 함수들
        // ========================================

        namespace Utils {
            // 메시지 타입을 문자열로 변환
            std::string messageTypeToString(MessageType type);

            // 서버 상태를 문자열로 변환
            std::string serverStateToString(ServerState state);

            // 세션 상태를 문자열로 변환
            std::string sessionStateToString(SessionState state);

            // 방 상태를 문자열로 변환
            std::string roomStateToString(RoomState state);

            // 현재 시간을 ISO 8601 형식 문자열로 변환
            std::string currentTimeToString();

            // 바이트 크기를 사람이 읽기 쉬운 형식으로 변환
            std::string formatBytes(uint64_t bytes);

            // 지속 시간을 사람이 읽기 쉬운 형식으로 변환
            std::string formatDuration(std::chrono::seconds duration);
        }

    } // namespace Server

    // ========================================
    // Common 네임스페이스에 서버 상수 추가
    // ========================================
    namespace Common {
        // 서버 관련 상수들 (클라이언트에서도 사용)
        constexpr uint16_t DEFAULT_SERVER_PORT = Server::DEFAULT_SERVER_PORT;
        constexpr int MAX_CONCURRENT_USERS = Server::MAX_CONCURRENT_USERS;
        constexpr int MAX_CHAT_MESSAGE_LENGTH = Server::MAX_CHAT_MESSAGE_LENGTH;
    }

} // namespace Blokus