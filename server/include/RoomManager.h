#pragma once

#include "GameRoom.h"
#include "ServerTypes.h"
#include "Types.h"
#include <unordered_map>
#include <vector>
#include <memory>
#include <shared_mutex>
#include <atomic>
#include <functional>
#include <chrono>

namespace Blokus {
    namespace Server {

        // 전방 선언
        class DatabaseManager;

        // ========================================
        // RoomManager 클래스
        // ========================================
        class RoomManager {
        public:
            // 생성자/소멸자
            RoomManager();
            explicit RoomManager(std::shared_ptr<DatabaseManager> dbManager);
            ~RoomManager();
            
            // DatabaseManager 설정
            void setDatabaseManager(std::shared_ptr<DatabaseManager> dbManager);
            std::shared_ptr<DatabaseManager> getDatabaseManager() const;

            // �� ����/����
            int createRoom(const std::string& hostId, const std::string& hostUsername,
                const std::string& roomName, bool isPrivate = false,
                const std::string& password = "");
            bool removeRoom(int roomId);
            void removeAllRooms();

            // �� ����
            GameRoomPtr getRoom(int roomId);
            const GameRoomPtr getRoom(int roomId) const;
            bool hasRoom(int roomId) const;

            // �÷��̾� �� ����
            bool joinRoom(int roomId, SessionPtr session, const std::string& userId,
                const std::string& username, const std::string& password = "");
            bool leaveRoom(const std::string& userId);
            bool leaveRoom(int roomId, const std::string& userId);

            // �÷��̾� ���� ����
            bool setPlayerReady(const std::string& userId, bool ready);
            bool startGame(const std::string& hostId);
            bool endGame(int roomId);

            // ȣ��Ʈ ����
            bool transferHost(int roomId, const std::string& currentHostId, const std::string& newHostId);

            // �� �˻�/���
            std::vector<Common::RoomInfo> getRoomList() const;
            std::vector<GameRoomPtr> findRooms(std::function<bool(const GameRoom&)> predicate) const;
            std::vector<GameRoomPtr> getWaitingRooms() const;
            std::vector<GameRoomPtr> getPlayingRooms() const;

            // �÷��̾� �˻�
            GameRoomPtr findPlayerRoom(const std::string& userId) const;
            bool isPlayerInRoom(const std::string& userId) const;
            bool isPlayerInGame(const std::string& userId) const;

            // ���
            size_t getRoomCount() const;
            size_t getTotalPlayers() const;
            size_t getWaitingRoomCount() const;
            size_t getPlayingRoomCount() const;

            // ��������
            void cleanupEmptyRooms();
            void cleanupInactiveRooms(std::chrono::minutes threshold = std::chrono::minutes(30));
            void cleanupDisconnectedPlayers();
            void performPeriodicCleanup();

            // ��ε�ĳ����
            void broadcastToAllRooms(const std::string& message);
            void broadcastToWaitingRooms(const std::string& message);
            void broadcastToPlayingRooms(const std::string& message);

            // ����
            void setMaxRooms(size_t maxRooms) { m_maxRooms = maxRooms; }
            void setMaxPlayersPerRoom(size_t maxPlayers) { m_maxPlayersPerRoom = maxPlayers; }
            size_t getMaxRooms() const { return m_maxRooms; }

            // �ݹ� ���� (�̺�Ʈ ó����)
            using RoomEventCallback = std::function<void(int roomId, const std::string& event, const std::string& data)>;
            void setRoomEventCallback(RoomEventCallback callback) { m_eventCallback = callback; }

        private:
            // �� �����
            std::unordered_map<int, GameRoomPtr> m_rooms;
            mutable std::shared_mutex m_roomsMutex;

            // �÷��̾�-�� ���� (���� �˻��� ����)
            std::unordered_map<std::string, int> m_playerToRoom;
            mutable std::shared_mutex m_playerMappingMutex;

            // �� ID ����
            std::atomic<int> m_nextRoomId;

            // ����
            size_t m_maxRooms;
            size_t m_maxPlayersPerRoom;

            // 이벤트 콜백
            RoomEventCallback m_eventCallback;
            
            // DatabaseManager 참조
            std::shared_ptr<DatabaseManager> m_databaseManager;

            // 내부 유틸리티 함수들
            bool validateRoomCreation(const std::string& roomName) const;
            bool validateJoinRoom(int roomId, const std::string& userId, const std::string& password) const;
            void updatePlayerMapping(const std::string& userId, int roomId);
            void removePlayerMapping(const std::string& userId);
            int getPlayerRoomId(const std::string& userId) const;

            // ���� �Լ���
            void cleanupSingleRoom(GameRoomPtr room);
            bool shouldRemoveRoom(const GameRoom& room) const;

            // �̺�Ʈ �߻�
            void triggerRoomEvent(int roomId, const std::string& event, const std::string& data = "");

            // ��� ����
            void updateStatistics();
        };

        // ========================================
        // ���� �Լ���
        // ========================================

        // �� ���� Ȯ�� �Լ���
        inline bool isRoomWaiting(const GameRoom& room) {
            return room.getState() == RoomState::Waiting;
        }

        inline bool isRoomPlaying(const GameRoom& room) {
            return room.getState() == RoomState::Playing;
        }

        inline bool isRoomEmpty(const GameRoom& room) {
            return room.isEmpty();
        }

        inline bool isRoomFull(const GameRoom& room) {
            return room.isFull();
        }

    } // namespace Server
} // namespace Blokus