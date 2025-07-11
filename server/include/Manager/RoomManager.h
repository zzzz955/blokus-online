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

        // 전방 선언
        class GameRoom;
        class Session;
        using SessionPtr = std::shared_ptr<Session>;  // ClientSessionPtr 대신 SessionPtr 사용

        // ========================================
        // 방 관리자 클래스
        // ========================================
        class RoomManager {
        public:
            RoomManager();
            ~RoomManager();

            // 방 생명주기 관리
            int createRoom(const std::string& hostId, const std::string& hostUsername,
                const std::string& roomName, bool isPrivate = false, const std::string& password = "");
            bool removeRoom(int roomId);
            std::shared_ptr<GameRoom> getRoom(int roomId);

            // 플레이어 방 입장/퇴장
            bool joinRoom(int roomId, SessionPtr client, const std::string& userId,
                const std::string& username, const std::string& password = "");
            bool leaveRoom(int roomId, const std::string& userId);

            // 방 목록 조회
            std::vector<Blokus::Common::RoomInfo> getRoomList() const;
            size_t getRoomCount() const;
            size_t getTotalPlayers() const;

            // 관리 기능
            void cleanupEmptyRooms();
            void broadcastToAllRooms(const std::string& message);

            // 템플릿 함수 - 특정 조건의 방들에 작업 수행
            template<typename Func>
            void forEachRoom(Func&& func) const {
                std::shared_lock<std::shared_mutex> lock(m_roomsMutex);
                for (const auto& [roomId, room] : m_rooms) {
                    func(room);
                }
            }

            // 방 검색
            std::vector<std::shared_ptr<GameRoom>> findRooms(
                std::function<bool(const GameRoom&)> predicate) const;

        private:
            std::unordered_map<int, std::shared_ptr<GameRoom>> m_rooms;
            mutable std::shared_mutex m_roomsMutex;

            std::atomic<int> m_nextRoomId;
        };

    } // namespace Server
} // namespace Blokus