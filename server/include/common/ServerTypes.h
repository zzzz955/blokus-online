#pragma once

#include <cstdint>
#include <string>
#include <chrono>
#include <memory>
#include <functional>

namespace Blokus {
    namespace Server {

        // ���� ���� ���
        constexpr uint16_t DEFAULT_SERVER_PORT = 8888;
        constexpr int MAX_CONCURRENT_USERS = 1000;
        constexpr int MAX_ROOM_COUNT = 100;
        constexpr int MAX_PLAYERS_PER_ROOM = 4;
        constexpr int MESSAGE_BUFFER_SIZE = 8192;
        constexpr auto HEARTBEAT_INTERVAL = std::chrono::seconds(30);
        constexpr auto CLIENT_TIMEOUT = std::chrono::seconds(60);

        // ���� ���� ����ü
        struct ServerConfig {
            uint16_t port = DEFAULT_SERVER_PORT;
            int maxConnections = MAX_CONCURRENT_USERS;
            int threadPoolSize = 4;
            std::string databaseUrl;
            std::string redisUrl;
            bool enableLogging = true;
            bool enableMetrics = true;
        };

        // Ŭ���̾�Ʈ ����
        enum class ClientState {
            DISCONNECTED,
            CONNECTING,
            AUTHENTICATING,
            LOBBY,
            IN_ROOM,
            IN_GAME
        };

        // �޽��� ó�� ���
        enum class MessageResult {
            SUCCESS,
            ERROR,
            INVALID_MESSAGE,
            AUTHENTICATION_REQUIRED,
            PERMISSION_DENIED,
            RATE_LIMITED
        };

        // ���� ����
        class ClientSession;
        class Room;
        class GameServer;

        // Ÿ�� ��Ī
        using ClientSessionPtr = std::shared_ptr<ClientSession>;
        using RoomPtr = std::shared_ptr<Room>;
        using MessageHandler = std::function<MessageResult(ClientSessionPtr, const std::string&)>;

    } // namespace Server
} // namespace Blokus