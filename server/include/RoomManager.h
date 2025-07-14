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

        // ========================================
        // RoomManager 클래스
        // ========================================
        class RoomManager {
        public:
            // 생성자/소멸자
            RoomManager();
            ~RoomManager();

            // 방 생성/삭제
            int createRoom(const std::string& hostId, const std::string& hostUsername,
                const std::string& roomName, bool isPrivate = false,
                const std::string& password = "");
            bool removeRoom(int roomId);
            void removeAllRooms();

            // 방 접근
            GameRoomPtr getRoom(int roomId);
            const GameRoomPtr getRoom(int roomId) const;
            bool hasRoom(int roomId) const;

            // 플레이어 방 관리
            bool joinRoom(int roomId, SessionPtr session, const std::string& userId,
                const std::string& username, const std::string& password = "");
            bool leaveRoom(const std::string& userId);
            bool leaveRoom(int roomId, const std::string& userId);

            // 플레이어 상태 관리
            bool setPlayerReady(const std::string& userId, bool ready);
            bool startGame(const std::string& hostId);
            bool endGame(int roomId);

            // 호스트 권한
            bool transferHost(int roomId, const std::string& currentHostId, const std::string& newHostId);

            // 방 검색/목록
            std::vector<Common::RoomInfo> getRoomList() const;
            std::vector<GameRoomPtr> findRooms(std::function<bool(const GameRoom&)> predicate) const;
            std::vector<GameRoomPtr> getWaitingRooms() const;
            std::vector<GameRoomPtr> getPlayingRooms() const;

            // 플레이어 검색
            GameRoomPtr findPlayerRoom(const std::string& userId) const;
            bool isPlayerInRoom(const std::string& userId) const;
            bool isPlayerInGame(const std::string& userId) const;

            // 통계
            size_t getRoomCount() const;
            size_t getTotalPlayers() const;
            size_t getWaitingRoomCount() const;
            size_t getPlayingRoomCount() const;

            // 유지보수
            void cleanupEmptyRooms();
            void cleanupInactiveRooms(std::chrono::minutes threshold = std::chrono::minutes(30));
            void cleanupDisconnectedPlayers();
            void performPeriodicCleanup();

            // 브로드캐스팅
            void broadcastToAllRooms(const std::string& message);
            void broadcastToWaitingRooms(const std::string& message);
            void broadcastToPlayingRooms(const std::string& message);

            // 설정
            void setMaxRooms(size_t maxRooms) { m_maxRooms = maxRooms; }
            void setMaxPlayersPerRoom(size_t maxPlayers) { m_maxPlayersPerRoom = maxPlayers; }
            size_t getMaxRooms() const { return m_maxRooms; }

            // 콜백 설정 (이벤트 처리용)
            using RoomEventCallback = std::function<void(int roomId, const std::string& event, const std::string& data)>;
            void setRoomEventCallback(RoomEventCallback callback) { m_eventCallback = callback; }

        private:
            // 방 저장소
            std::unordered_map<int, GameRoomPtr> m_rooms;
            mutable std::shared_mutex m_roomsMutex;

            // 플레이어-방 매핑 (빠른 검색을 위해)
            std::unordered_map<std::string, int> m_playerToRoom;
            mutable std::shared_mutex m_playerMappingMutex;

            // 방 ID 생성
            std::atomic<int> m_nextRoomId;

            // 설정
            size_t m_maxRooms;
            size_t m_maxPlayersPerRoom;

            // 이벤트 콜백
            RoomEventCallback m_eventCallback;

            // 내부 유틸리티 함수들
            bool validateRoomCreation(const std::string& roomName) const;
            bool validateJoinRoom(int roomId, const std::string& userId, const std::string& password) const;
            void updatePlayerMapping(const std::string& userId, int roomId);
            void removePlayerMapping(const std::string& userId);
            int getPlayerRoomId(const std::string& userId) const;

            // 정리 함수들
            void cleanupSingleRoom(GameRoomPtr room);
            bool shouldRemoveRoom(const GameRoom& room) const;

            // 이벤트 발생
            void triggerRoomEvent(int roomId, const std::string& event, const std::string& data = "");

            // 통계 수집
            void updateStatistics();
        };

        // ========================================
        // 편의 함수들
        // ========================================

        // 방 상태 확인 함수들
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