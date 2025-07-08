#pragma once

#include "server/common/ServerTypes.h"
#include "common/Types.h"
#include "common/GameLogic.h"
#include <vector>
#include <memory>
#include <mutex>
#include <shared_mutex>
#include <chrono>

namespace Blokus {
    namespace Server {

        // ========================================
        // �÷��̾� ���� ����ü
        // ========================================
        struct PlayerInfo {
            std::string userId;
            std::string username;
            Blokus::Common::PlayerColor color;
            bool isReady;
            bool isAI;
            ClientSessionPtr session;

            PlayerInfo()
                : color(Blokus::Common::PlayerColor::None)
                , isReady(false)
                , isAI(false)
            {
            }
        };

        // ========================================
        // ���� ���ӹ� Ŭ����
        // ========================================
        class GameRoom {
        public:
            explicit GameRoom(int roomId, const std::string& roomName, const std::string& hostId);
            ~GameRoom();

            // �� ����
            int getRoomId() const { return m_roomId; }
            const std::string& getRoomName() const { return m_roomName; }
            const std::string& getHostId() const { return m_hostId; }
            RoomState getState() const { return m_state; }

            // �÷��̾� ����
            bool addPlayer(ClientSessionPtr client, const std::string& userId, const std::string& username);
            bool removePlayer(const std::string& userId);
            bool setPlayerReady(const std::string& userId, bool ready);
            size_t getPlayerCount() const;
            bool isFull() const;

            // ���� ����
            bool startGame();
            bool endGame();
            void resetGame();

            // �޽��� ��ε�ĳ��Ʈ
            void broadcastMessage(const std::string& message, const std::string& excludeUserId = "");
            void sendToPlayer(const std::string& userId, const std::string& message);

            // ���� ����
            bool makeMove(const std::string& userId, const Blokus::Common::BlockPlacement& move);
            Blokus::Common::PlayerColor getCurrentPlayer() const;
            bool isGameFinished() const;

            // �� ���� Ȯ��
            bool canJoin() const;
            bool isEmpty() const;

        private:
            int m_roomId;
            std::string m_roomName;
            std::string m_hostId;
            RoomState m_state;

            std::vector<PlayerInfo> m_players;
            mutable std::mutex m_playersMutex;

            std::unique_ptr<Blokus::Common::GameLogic> m_gameLogic;
            std::chrono::steady_clock::time_point m_gameStartTime;
            std::chrono::steady_clock::time_point m_lastActivity;
        };

        // ========================================
        // �� ������ Ŭ����
        // ========================================
        class RoomManager {
        public:
            RoomManager();
            ~RoomManager();

            // �� �����ֱ� ����
            int createRoom(const std::string& hostId, const Blokus::Common::RoomInfo& roomInfo);
            bool deleteRoom(int roomId);

            // �� ����/Ż��
            bool joinRoom(int roomId, const std::string& userId, const std::string& password = "");
            bool leaveRoom(int roomId, const std::string& userId);

            // �� ���� ��ȸ
            std::vector<Blokus::Common::RoomInfo> getRoomList() const;
            std::optional<Blokus::Common::RoomInfo> getRoomInfo(int roomId) const;
            bool isRoomHost(int roomId, const std::string& userId) const;

            // �� ���� ����
            bool updateRoomSettings(int roomId, const std::string& hostId,
                const Blokus::Common::RoomInfo& newSettings);

            // ���� ���� (GameService ��� ����)
            bool startGame(int roomId);
            bool makeMove(int roomId, const std::string& userId, const std::string& moveData);
            GameRoomPtr getRoom(int roomId);

            // ���� ����
            void cleanupEmptyRooms();
            int getActiveRoomCount() const;

        private:
            // �� ������ ����
            struct Room {
                Blokus::Common::RoomInfo info;
                std::vector<std::string> players;
                std::chrono::system_clock::time_point createdAt;
                std::chrono::system_clock::time_point lastActivity;
            };

            std::unordered_map<int, GameRoomPtr> m_rooms;
            mutable std::shared_mutex m_roomsMutex;
            std::atomic<int> m_nextRoomId{ 1 };
        };

    } // namespace Server
} // namespace Blokus