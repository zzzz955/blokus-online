#pragma once

#include "ServerTypes.h"
#include "Room.h"
#include <unordered_map>
#include <memory>
#include <mutex>

namespace Blokus {
    namespace Server {

        class GameService {
        public:
            GameService();
            ~GameService();

            // 방 관리
            RoomPtr createRoom(const std::string& roomName, const std::string& hostId);
            RoomPtr getRoom(int roomId);
            bool deleteRoom(int roomId);
            std::vector<RoomPtr> getAllRooms();

            // 플레이어 관리
            bool joinRoom(int roomId, ClientSessionPtr client);
            bool leaveRoom(int roomId, const std::string& userId);

            // 게임 진행
            bool startGame(int roomId);
            bool makeMove(int roomId, const std::string& userId, const std::string& moveData);

        private:
            std::unordered_map<int, RoomPtr> rooms_;
            mutable std::mutex roomsMutex_;
            int nextRoomId_;
        };

    } // namespace Server
} // namespace Blokus