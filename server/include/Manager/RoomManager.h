#pragma once

#include "common/ServerTypes.h"
#include "common/Types.h"
#include <unordered_map>
#include <shared_mutex>
#include <atomic>
#include <functional>
#include <memory>

namespace Blokus {
    namespace Server {

        // ���� ����
        class GameRoom;
        class Session;
        using SessionPtr = std::shared_ptr<Session>;  // ClientSessionPtr ��� SessionPtr ���

        // ========================================
        // �� ������ Ŭ����
        // ========================================
        class RoomManager {
        public:
            RoomManager();
            ~RoomManager();

            // �� �����ֱ� ����
            int createRoom(const std::string& hostId, const std::string& hostUsername,
                const std::string& roomName, bool isPrivate = false, const std::string& password = "");
            bool removeRoom(int roomId);
            std::shared_ptr<GameRoom> getRoom(int roomId);

            // �÷��̾� �� ����/����
            bool joinRoom(int roomId, SessionPtr client, const std::string& userId,
                const std::string& username, const std::string& password = "");
            bool leaveRoom(int roomId, const std::string& userId);

            // �� ��� ��ȸ
            std::vector<Blokus::Common::RoomInfo> getRoomList() const;
            size_t getRoomCount() const;
            size_t getTotalPlayers() const;

            // ���� ���
            void cleanupEmptyRooms();
            void broadcastToAllRooms(const std::string& message);

            // ���ø� �Լ� - Ư�� ������ ��鿡 �۾� ����
            template<typename Func>
            void forEachRoom(Func&& func) const {
                std::shared_lock<std::shared_mutex> lock(m_roomsMutex);
                for (const auto& [roomId, room] : m_rooms) {
                    func(room);
                }
            }

            // �� �˻�
            std::vector<std::shared_ptr<GameRoom>> findRooms(
                std::function<bool(const GameRoom&)> predicate) const;

        private:
            std::unordered_map<int, std::shared_ptr<GameRoom>> m_rooms;
            mutable std::shared_mutex m_roomsMutex;

            std::atomic<int> m_nextRoomId;
        };

    } // namespace Server
} // namespace Blokus