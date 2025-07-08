#pragma once

#include <cstdint>
#include <string>
#include <chrono>
#include <memory>
#include <functional>

namespace Blokus {
    namespace Server {

        // 서버 설정 상수
        constexpr uint16_t DEFAULT_SERVER_PORT = 8888;
        constexpr int MAX_CONCURRENT_USERS = 1000;
        constexpr int MAX_ROOM_COUNT = 100;
        constexpr int MAX_PLAYERS_PER_ROOM = 4;
        constexpr int MESSAGE_BUFFER_SIZE = 8192;
        constexpr auto HEARTBEAT_INTERVAL = std::chrono::seconds(30);
        constexpr auto CLIENT_TIMEOUT = std::chrono::seconds(60);

        // 서버 설정 구조체
        struct ServerConfig {
            uint16_t port = DEFAULT_SERVER_PORT;
            int maxConnections = MAX_CONCURRENT_USERS;
            int threadPoolSize = 4;
            std::string databaseUrl;
            std::string redisUrl;
            bool enableLogging = true;
            bool enableMetrics = true;
        };

        // 클라이언트 상태
        enum class ClientState {
            DISCONNECTED,
            CONNECTING,
            AUTHENTICATING,
            LOBBY,
            IN_ROOM,
            IN_GAME
        };

        // 메시지 처리 결과
        enum class MessageResult {
            SUCCESS,
            ERROR,
            INVALID_MESSAGE,
            AUTHENTICATION_REQUIRED,
            PERMISSION_DENIED,
            RATE_LIMITED
        };

        // 전방 선언
        class ClientSession;
        class Room;
        class GameServer;

        // 타입 별칭
        using ClientSessionPtr = std::shared_ptr<ClientSession>;
        using RoomPtr = std::shared_ptr<Room>;
        using MessageHandler = std::function<MessageResult(ClientSessionPtr, const std::string&)>;

    } // namespace Server
} // namespace Blokus