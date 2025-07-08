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

            // �� ����
            RoomPtr createRoom(const std::string& roomName, const std::string& hostId);
            RoomPtr getRoom(int roomId);
            bool deleteRoom(int roomId);
            std::vector<RoomPtr> getAllRooms();

            // �÷��̾� ����
            bool joinRoom(int roomId, ClientSessionPtr client);
            bool leaveRoom(int roomId, const std::string& userId);

            // ���� ����
            bool startGame(int roomId);
            bool makeMove(int roomId, const std::string& userId, const std::string& moveData);

        private:
            std::unordered_map<int, RoomPtr> rooms_;
            mutable std::mutex roomsMutex_;
            int nextRoomId_;
        };

    } // namespace Server
} // namespace Blokus