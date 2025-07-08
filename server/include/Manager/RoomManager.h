#pragma once

#include "ServerTypes.h"
#include "common/Types.h"
#include "common/GameLogic.h"
#include <vector>
#include <memory>
#include <mutex>
#include <chrono>

namespace Blokus {
    namespace Server {

        enum class RoomState {
            WAITING,
            PLAYING,
            FINISHED
        };

        struct PlayerInfo {
            std::string userId;
            std::string username;
            Blokus::Common::PlayerColor color;
            bool isReady;
            bool isAI;
            ClientSessionPtr session;
        };

        class Room {
        public:
            explicit RoomManager(int roomId, const std::string& roomName, const std::string& hostId);
            ~RoomManager();

            // �� ����
            int getRoomId() const { return roomId_; }
            const std::string& getRoomName() const { return roomName_; }
            const std::string& getHostId() const { return hostId_; }
            RoomState getState() const { return state_; }

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

        private:
            int roomId_;
            std::string roomName_;
            std::string hostId_;
            RoomState state_;

            std::vector<PlayerInfo> players_;
            mutable std::mutex playersMutex_;

            std::unique_ptr<Blokus::Common::GameLogic> gameLogic_;
            std::chrono::steady_clock::time_point gameStartTime_;
        };

    } // namespace Server
} // namespace Blokus