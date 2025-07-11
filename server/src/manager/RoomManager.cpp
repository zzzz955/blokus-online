#include "manager/RoomManager.h"
#include "manager/GameRoom.h"  // GameRoom 클래스 포함
#include <spdlog/spdlog.h>
#include <algorithm>

namespace Blokus {
    namespace Server {

        // ========================================
        // RoomManager 클래스 구현
        // ========================================

        RoomManager::RoomManager()
            : m_nextRoomId(1001) // 1001부터 시작
        {
            spdlog::info("RoomManager 초기화");
        }

        RoomManager::~RoomManager() {
            std::unique_lock<std::shared_mutex> lock(m_roomsMutex);
            m_rooms.clear();
            spdlog::info("RoomManager 소멸");
        }

        int RoomManager::createRoom(const std::string& hostId, const std::string& hostUsername,
            const std::string& roomName, bool isPrivate, const std::string& password) {
            std::unique_lock<std::shared_mutex> lock(m_roomsMutex);

            int roomId = m_nextRoomId++;

            auto room = std::make_shared<GameRoom>(roomId, roomName, hostId);
            m_rooms[roomId] = room;

            spdlog::info("방 생성: ID={}, Name={}, Host={}, Private={}",
                roomId, roomName, hostUsername, isPrivate);

            return roomId;
        }

        bool RoomManager::removeRoom(int roomId) {
            std::unique_lock<std::shared_mutex> lock(m_roomsMutex);

            auto it = m_rooms.find(roomId);
            if (it == m_rooms.end()) {
                return false;
            }

            spdlog::info("방 제거: ID={}", roomId);
            m_rooms.erase(it);
            return true;
        }

        std::shared_ptr<GameRoom> RoomManager::getRoom(int roomId) {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            auto it = m_rooms.find(roomId);
            return (it != m_rooms.end()) ? it->second : nullptr;
        }

        bool RoomManager::joinRoom(int roomId, SessionPtr client,
            const std::string& userId, const std::string& username,
            const std::string& password) {
            auto room = getRoom(roomId);
            if (!room) {
                spdlog::warn("방 입장 실패: 방 {} 없음", roomId);
                return false;
            }

            if (!room->canJoin()) {
                spdlog::warn("방 {} 입장 실패: 입장 불가능 상태", roomId);
                return false;
            }

            // TODO: 비밀번호 확인 로직 추가

            if (!room->addPlayer(client, userId, username)) {
                spdlog::warn("방 {} 입장 실패: 플레이어 추가 실패", roomId);
                return false;
            }

            spdlog::info("플레이어 {} 방 {} 입장 성공", username, roomId);
            return true;
        }

        bool RoomManager::leaveRoom(int roomId, const std::string& userId) {
            auto room = getRoom(roomId);
            if (!room) {
                return false;
            }

            bool success = room->removePlayer(userId);

            // 방이 비었다면 제거
            if (success && room->isEmpty()) {
                removeRoom(roomId);
            }

            return success;
        }

        std::vector<Blokus::Common::RoomInfo> RoomManager::getRoomList() const {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            std::vector<Blokus::Common::RoomInfo> roomList;
            roomList.reserve(m_rooms.size());

            for (const auto& [roomId, room] : m_rooms) {
                Blokus::Common::RoomInfo info;
                info.roomId = room->getRoomId();
                info.roomName = room->getRoomName();
                info.hostName = room->getHostId(); // 실제로는 호스트 이름을 가져와야 함
                info.currentPlayers = static_cast<int>(room->getPlayerCount());
                info.maxPlayers = Blokus::Common::MAX_PLAYERS;
                info.isPlaying = (room->getState() == RoomState::Playing);
                info.isPrivate = false; // TODO: 비밀방 정보 추가
                info.gameMode = "클래식";

                roomList.push_back(info);
            }

            return roomList;
        }

        size_t RoomManager::getRoomCount() const {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);
            return m_rooms.size();
        }

        size_t RoomManager::getTotalPlayers() const {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            size_t totalPlayers = 0;
            for (const auto& [roomId, room] : m_rooms) {
                totalPlayers += room->getPlayerCount();
            }
            return totalPlayers;
        }

        void RoomManager::cleanupEmptyRooms() {
            std::unique_lock<std::shared_mutex> lock(m_roomsMutex);

            auto it = m_rooms.begin();
            while (it != m_rooms.end()) {
                if (it->second->isEmpty()) {
                    spdlog::info("빈 방 정리: ID={}", it->first);
                    it = m_rooms.erase(it);
                }
                else {
                    ++it;
                }
            }
        }

        void RoomManager::broadcastToAllRooms(const std::string& message) {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            for (const auto& [roomId, room] : m_rooms) {
                room->broadcastMessage(message);
            }
        }

        std::vector<std::shared_ptr<GameRoom>> RoomManager::findRooms(
            std::function<bool(const GameRoom&)> predicate) const {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            std::vector<std::shared_ptr<GameRoom>> result;
            for (const auto& [roomId, room] : m_rooms) {
                if (predicate(*room)) {
                    result.push_back(room);
                }
            }
            return result;
        }

    } // namespace Server
} // namespace Blokus