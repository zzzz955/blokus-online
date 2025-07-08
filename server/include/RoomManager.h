#pragma once

#include <memory>
#include <unordered_map>
#include <vector>
#include <mutex>
#include <atomic>

#include "common/Types.h"
#include "common/GameLogic.h"
#include "server/GameRoom.h"

namespace Blokus {
    namespace Server {

        // �� ������ ����ϴ� Ŭ����
        class RoomManager {
        public:
            RoomManager();
            ~RoomManager();

            // �� ���� �� ����
            std::shared_ptr<GameRoom> createRoom(const Common::RoomInfo& roomInfo, uint32_t hostSessionId);
            bool removeRoom(uint32_t roomId);

            // �� ��ȸ
            std::shared_ptr<GameRoom> findRoom(uint32_t roomId);
            std::vector<std::shared_ptr<GameRoom>> getAllRooms();
            std::vector<Common::RoomInfo> getRoomList(); // Ŭ���̾�Ʈ�� �� ���

            // �� ����/������
            bool joinRoom(uint32_t roomId, uint32_t sessionId, const std::string& password = "");
            bool leaveRoom(uint32_t roomId, uint32_t sessionId);

            // ��� ����
            size_t getRoomCount() const;
            size_t getTotalPlayers() const;

            // �� �˻�
            std::vector<std::shared_ptr<GameRoom>> findRoomsByHost(const std::string& hostName);
            std::vector<std::shared_ptr<GameRoom>> findAvailableRooms(); // ���� ������ ���

            // ���� �۾�
            void cleanupEmptyRooms();                              // �� ��� ����
            void cleanupFinishedGames();                           // ���� ���ӵ� ����

        private:
            // ���� ���� �Լ���
            uint32_t generateRoomId();                             // �� �� ID ����
            bool isValidRoomInfo(const Common::RoomInfo& roomInfo); // �� ���� ��ȿ�� ����
            void notifyRoomListChanged();                          // �� ��� ���� �˸�

        private:
            std::unordered_map<uint32_t, std::shared_ptr<GameRoom>> m_rooms; // Ȱ�� ���
            mutable std::mutex m_roomsMutex;                       // �� �� ��ȣ�� ���ؽ�
            std::atomic<uint32_t> m_nextRoomId{ 1001 };            // ���� �� ID (1001���� ����)

            // ��� ����
            std::atomic<uint64_t> m_totalRoomsCreated{ 0 };        // �� ������ �� ��
            std::atomic<uint64_t> m_totalGamesPlayed{ 0 };         // �� �÷��̵� ���� ��
        };

    } // namespace Server
} // namespace Blokus