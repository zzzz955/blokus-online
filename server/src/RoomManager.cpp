#include "RoomManager.h"
#include <spdlog/spdlog.h>
#include <algorithm>
#include <random>

namespace Blokus {
    namespace Server {

        // ========================================
        // 생성자/소멸자
        // ========================================

        RoomManager::RoomManager()
            : m_nextRoomId(1001)  // 1001부터 시작
            , m_maxRooms(100)     // 최대 100개 방
            , m_maxPlayersPerRoom(Common::MAX_PLAYERS)
            , m_eventCallback(nullptr)
        {
            spdlog::info("🏠 RoomManager 초기화 (최대 방: {}, 최대 플레이어/방: {})",
                m_maxRooms, m_maxPlayersPerRoom);
        }

        RoomManager::~RoomManager() {
            removeAllRooms();
            spdlog::info("🏠 RoomManager 소멸");
        }

        // ========================================
        // 방 생성/삭제
        // ========================================

        int RoomManager::createRoom(const std::string& hostId, const std::string& hostUsername,
            const std::string& roomName, bool isPrivate, const std::string& password) {

            // 1. 입력 검증
            if (!validateRoomCreation(roomName)) {
                spdlog::warn("❌ 방 생성 실패: 유효하지 않은 방 이름 '{}'", roomName);
                return -1;
            }

            // 2. 호스트가 이미 다른 방에 있는지 확인
            if (isPlayerInRoom(hostId)) {
                spdlog::warn("❌ 방 생성 실패: 호스트 '{}' 이미 다른 방에 참여 중", hostId);
                return -2;
            }

            std::unique_lock<std::shared_mutex> roomLock(m_roomsMutex);

            // 3. 방 개수 제한 확인
            if (m_rooms.size() >= m_maxRooms) {
                spdlog::warn("❌ 방 생성 실패: 최대 방 개수 도달 ({}/{})", m_rooms.size(), m_maxRooms);
                return -3;
            }

            // 4. 새 방 생성
            int roomId = m_nextRoomId++;
            auto room = std::make_shared<GameRoom>(roomId, roomName, hostId);

            m_rooms[roomId] = room;

            spdlog::info("✅ 방 생성 성공: ID={}, Name='{}', Host='{}', Private={}",
                roomId, roomName, hostUsername, isPrivate);

            // 5. 이벤트 발생
            triggerRoomEvent(roomId, "ROOM_CREATED", roomName);

            return roomId;
        }

        bool RoomManager::removeRoom(int roomId) {
            std::unique_lock<std::shared_mutex> roomLock(m_roomsMutex);

            auto it = m_rooms.find(roomId);
            if (it == m_rooms.end()) {
                spdlog::warn("❌ 방 제거 실패: 방 ID {} 없음", roomId);
                return false;
            }

            GameRoomPtr room = it->second;

            // 방에 있는 모든 플레이어의 매핑 제거
            auto playerList = room->getPlayerList();
            for (const auto& player : playerList) {
                removePlayerMapping(player.getUserId());
            }

            m_rooms.erase(it);

            spdlog::info("✅ 방 제거: ID={}, Name='{}'", roomId, room->getRoomName());
            triggerRoomEvent(roomId, "ROOM_REMOVED", room->getRoomName());

            return true;
        }

        void RoomManager::removeAllRooms() {
            std::unique_lock<std::shared_mutex> roomLock(m_roomsMutex);
            std::unique_lock<std::shared_mutex> playerLock(m_playerMappingMutex);

            size_t roomCount = m_rooms.size();
            m_rooms.clear();
            m_playerToRoom.clear();

            spdlog::info("🧹 모든 방 제거: {} 개", roomCount);
        }

        // ========================================
        // 방 접근
        // ========================================

        GameRoomPtr RoomManager::getRoom(int roomId) {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            auto it = m_rooms.find(roomId);
            return (it != m_rooms.end()) ? it->second : nullptr;
        }

        const GameRoomPtr RoomManager::getRoom(int roomId) const {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            auto it = m_rooms.find(roomId);
            return (it != m_rooms.end()) ? it->second : nullptr;
        }

        bool RoomManager::hasRoom(int roomId) const {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);
            return m_rooms.find(roomId) != m_rooms.end();
        }

        // ========================================
        // 플레이어 방 관리
        // ========================================

        bool RoomManager::joinRoom(int roomId, SessionPtr session, const std::string& userId,
            const std::string& username, const std::string& password) {

            // 1. 기본 검증
            if (!validateJoinRoom(roomId, userId, password)) {
                return false;
            }

            // 2. 이미 다른 방에 있는지 확인
            if (isPlayerInRoom(userId)) {
                spdlog::warn("❌ 방 참여 실패: 플레이어 '{}' 이미 다른 방에 참여 중", userId);
                return false;
            }

            // 3. 방 찾기
            auto room = getRoom(roomId);
            if (!room) {
                spdlog::warn("❌ 방 참여 실패: 방 ID {} 없음", roomId);
                return false;
            }

            // 4. 방에 플레이어 추가
            if (!room->addPlayer(session, userId, username)) {
                spdlog::warn("❌ 방 참여 실패: 방 {} 플레이어 추가 거부", roomId);
                return false;
            }

            // 5. 플레이어-방 매핑 업데이트
            updatePlayerMapping(userId, roomId);

            spdlog::info("✅ 방 참여 성공: 플레이어 '{}' -> 방 {} ({}명)",
                username, roomId, room->getPlayerCount());

            triggerRoomEvent(roomId, "PLAYER_JOINED", username);

            return true;
        }

        bool RoomManager::leaveRoom(const std::string& userId) {
            // 플레이어가 속한 방 찾기
            int roomId = getPlayerRoomId(userId);
            if (roomId == -1) {
                spdlog::warn("❌ 방 나가기 실패: 플레이어 '{}' 방에 없음", userId);
                return false;
            }

            return leaveRoom(roomId, userId);
        }

        bool RoomManager::leaveRoom(int roomId, const std::string& userId) {
            auto room = getRoom(roomId);
            if (!room) {
                spdlog::warn("❌ 방 나가기 실패: 방 ID {} 없음", roomId);
                return false;
            }

            // 플레이어 정보 가져오기 (제거 전에)
            const PlayerInfo* player = room->getPlayer(userId);
            if (!player) {
                spdlog::warn("❌ 방 나가기 실패: 플레이어 '{}' 방 {}에 없음", userId, roomId);
                return false;
            }

            std::string username = player->getUsername();

            // 방에서 플레이어 제거
            if (!room->removePlayer(userId)) {
                spdlog::warn("❌ 방 나가기 실패: 방 {} 플레이어 제거 거부", roomId);
                return false;
            }

            // 플레이어-방 매핑 제거
            removePlayerMapping(userId);

            spdlog::info("✅ 방 나가기 성공: 플레이어 '{}' <- 방 {} ({}명)",
                username, roomId, room->getPlayerCount());

            triggerRoomEvent(roomId, "PLAYER_LEFT", username);

            // 방이 비었다면 제거
            if (room->isEmpty()) {
                removeRoom(roomId);
            }

            return true;
        }

        // ========================================
        // 플레이어 상태 관리
        // ========================================

        bool RoomManager::setPlayerReady(const std::string& userId, bool ready) {
            auto room = findPlayerRoom(userId);
            if (!room) {
                spdlog::warn("❌ 플레이어 준비 상태 변경 실패: 플레이어 '{}' 방에 없음", userId);
                return false;
            }

            if (!room->setPlayerReady(userId, ready)) {
                spdlog::warn("❌ 플레이어 준비 상태 변경 실패: 방에서 거부");
                return false;
            }

            triggerRoomEvent(room->getRoomId(), "PLAYER_READY",
                userId + ":" + (ready ? "1" : "0"));

            return true;
        }

        bool RoomManager::startGame(const std::string& hostId) {
            auto room = findPlayerRoom(hostId);
            if (!room) {
                spdlog::warn("❌ 게임 시작 실패: 호스트 '{}' 방에 없음", hostId);
                return false;
            }

            // 호스트 권한 확인
            if (!room->isHost(hostId)) {
                spdlog::warn("❌ 게임 시작 실패: '{}' 호스트 권한 없음", hostId);
                return false;
            }

            if (!room->startGame()) {
                spdlog::warn("❌ 게임 시작 실패: 방 {} 시작 조건 미충족", room->getRoomId());
                return false;
            }

            spdlog::info("✅ 게임 시작: 방 {} (호스트: '{}')", room->getRoomId(), hostId);
            triggerRoomEvent(room->getRoomId(), "GAME_STARTED", hostId);

            return true;
        }

        bool RoomManager::endGame(int roomId) {
            auto room = getRoom(roomId);
            if (!room) {
                spdlog::warn("❌ 게임 종료 실패: 방 ID {} 없음", roomId);
                return false;
            }

            if (!room->endGame()) {
                spdlog::warn("❌ 게임 종료 실패: 방 {} 종료 조건 미충족", roomId);
                return false;
            }

            spdlog::info("✅ 게임 종료: 방 {}", roomId);
            triggerRoomEvent(roomId, "GAME_ENDED", "");

            return true;
        }

        // ========================================
        // 호스트 권한
        // ========================================

        bool RoomManager::transferHost(int roomId, const std::string& currentHostId, const std::string& newHostId) {
            auto room = getRoom(roomId);
            if (!room) {
                spdlog::warn("❌ 호스트 이양 실패: 방 ID {} 없음", roomId);
                return false;
            }

            // 현재 호스트 권한 확인
            if (!room->isHost(currentHostId)) {
                spdlog::warn("❌ 호스트 이양 실패: '{}' 호스트 권한 없음", currentHostId);
                return false;
            }

            if (!room->transferHost(newHostId)) {
                spdlog::warn("❌ 호스트 이양 실패: 방 {} 이양 거부", roomId);
                return false;
            }

            spdlog::info("✅ 호스트 이양: 방 {} ('{}' -> '{}')", roomId, currentHostId, newHostId);
            triggerRoomEvent(roomId, "HOST_TRANSFERRED", currentHostId + ":" + newHostId);

            return true;
        }

        // ========================================
        // 방 검색/목록
        // ========================================

        std::vector<Common::RoomInfo> RoomManager::getRoomList() const {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            std::vector<Common::RoomInfo> roomList;
            roomList.reserve(m_rooms.size());

            for (const auto& [roomId, room] : m_rooms) {
                roomList.push_back(room->getRoomInfo());
            }

            // 방 ID 순으로 정렬
            std::sort(roomList.begin(), roomList.end(),
                [](const Common::RoomInfo& a, const Common::RoomInfo& b) {
                    return a.roomId < b.roomId;
                });

            return roomList;
        }

        std::vector<GameRoomPtr> RoomManager::findRooms(std::function<bool(const GameRoom&)> predicate) const {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            std::vector<GameRoomPtr> result;
            for (const auto& [roomId, room] : m_rooms) {
                if (predicate(*room)) {
                    result.push_back(room);
                }
            }

            return result;
        }

        std::vector<GameRoomPtr> RoomManager::getWaitingRooms() const {
            return findRooms([](const GameRoom& room) {
                return room.getState() == RoomState::Waiting;
                });
        }

        std::vector<GameRoomPtr> RoomManager::getPlayingRooms() const {
            return findRooms([](const GameRoom& room) {
                return room.getState() == RoomState::Playing;
                });
        }

        // ========================================
        // 플레이어 검색
        // ========================================

        GameRoomPtr RoomManager::findPlayerRoom(const std::string& userId) const {
            int roomId = getPlayerRoomId(userId);
            return (roomId != -1) ? getRoom(roomId) : nullptr;
        }

        bool RoomManager::isPlayerInRoom(const std::string& userId) const {
            return getPlayerRoomId(userId) != -1;
        }

        bool RoomManager::isPlayerInGame(const std::string& userId) const {
            auto room = findPlayerRoom(userId);
            return room && room->isPlaying();
        }

        // ========================================
        // 통계
        // ========================================

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

        size_t RoomManager::getWaitingRoomCount() const {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            return std::count_if(m_rooms.begin(), m_rooms.end(),
                [](const auto& pair) {
                    return pair.second->getState() == RoomState::Waiting;
                });
        }

        size_t RoomManager::getPlayingRoomCount() const {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            return std::count_if(m_rooms.begin(), m_rooms.end(),
                [](const auto& pair) {
                    return pair.second->getState() == RoomState::Playing;
                });
        }

        // ========================================
        // 유지보수
        // ========================================

        void RoomManager::cleanupEmptyRooms() {
            std::unique_lock<std::shared_mutex> lock(m_roomsMutex);

            auto it = m_rooms.begin();
            size_t removedCount = 0;

            while (it != m_rooms.end()) {
                if (it->second->isEmpty()) {
                    spdlog::debug("🧹 빈 방 정리: ID={}", it->first);
                    it = m_rooms.erase(it);
                    ++removedCount;
                }
                else {
                    ++it;
                }
            }

            if (removedCount > 0) {
                spdlog::info("🧹 빈 방 정리 완료: {} 개", removedCount);
            }
        }

        void RoomManager::cleanupInactiveRooms(std::chrono::minutes threshold) {
            std::unique_lock<std::shared_mutex> lock(m_roomsMutex);

            auto it = m_rooms.begin();
            size_t removedCount = 0;

            while (it != m_rooms.end()) {
                if (it->second->isInactive(threshold)) {
                    spdlog::debug("🧹 비활성 방 정리: ID={} ({}분 비활성)",
                        it->first, threshold.count());
                    it = m_rooms.erase(it);
                    ++removedCount;
                }
                else {
                    ++it;
                }
            }

            if (removedCount > 0) {
                spdlog::info("🧹 비활성 방 정리 완료: {} 개", removedCount);
            }
        }

        void RoomManager::cleanupDisconnectedPlayers() {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            for (const auto& [roomId, room] : m_rooms) {
                room->cleanupDisconnectedPlayers();
            }
        }

        void RoomManager::performPeriodicCleanup() {
            spdlog::debug("🧹 주기적 정리 작업 시작");

            cleanupDisconnectedPlayers();
            cleanupEmptyRooms();
            cleanupInactiveRooms();

            spdlog::debug("🧹 주기적 정리 작업 완료 (현재 방: {}개, 플레이어: {}명)",
                getRoomCount(), getTotalPlayers());
        }

        // ========================================
        // 브로드캐스팅
        // ========================================

        void RoomManager::broadcastToAllRooms(const std::string& message) {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            for (const auto& [roomId, room] : m_rooms) {
                room->broadcastMessage(message);
            }

            spdlog::debug("📢 모든 방에 메시지 브로드캐스트: '{}' ({}개 방)",
                message.length() > 50 ? message.substr(0, 50) + "..." : message,
                m_rooms.size());
        }

        void RoomManager::broadcastToWaitingRooms(const std::string& message) {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            size_t sentCount = 0;
            for (const auto& [roomId, room] : m_rooms) {
                if (room->getState() == RoomState::Waiting) {
                    room->broadcastMessage(message);
                    ++sentCount;
                }
            }

            spdlog::debug("📢 대기 중인 방에 메시지 브로드캐스트: '{}' ({}개 방)",
                message.length() > 50 ? message.substr(0, 50) + "..." : message,
                sentCount);
        }

        void RoomManager::broadcastToPlayingRooms(const std::string& message) {
            std::shared_lock<std::shared_mutex> lock(m_roomsMutex);

            size_t sentCount = 0;
            for (const auto& [roomId, room] : m_rooms) {
                if (room->getState() == RoomState::Playing) {
                    room->broadcastMessage(message);
                    ++sentCount;
                }
            }

            spdlog::debug("📢 게임 중인 방에 메시지 브로드캐스트: '{}' ({}개 방)",
                message.length() > 50 ? message.substr(0, 50) + "..." : message,
                sentCount);
        }

        // ========================================
        // 내부 유틸리티 함수들
        // ========================================

        bool RoomManager::validateRoomCreation(const std::string& roomName) const {
            // 방 이름 길이 확인
            if (roomName.empty() || roomName.length() > Common::MAX_ROOM_NAME_LENGTH) {
                return false;
            }

            // 금지된 문자 확인 (기본적인 검증)
            if (roomName.find_first_of("\r\n\t") != std::string::npos) {
                return false;
            }

            return true;
        }

        bool RoomManager::validateJoinRoom(int roomId, const std::string& userId, const std::string& password) const {
            // 기본 검증
            if (roomId <= 0 || userId.empty()) {
                return false;
            }

            // 방 존재 확인
            if (!hasRoom(roomId)) {
                return false;
            }

            return true;
        }

        void RoomManager::updatePlayerMapping(const std::string& userId, int roomId) {
            std::unique_lock<std::shared_mutex> lock(m_playerMappingMutex);
            m_playerToRoom[userId] = roomId;
        }

        void RoomManager::removePlayerMapping(const std::string& userId) {
            std::unique_lock<std::shared_mutex> lock(m_playerMappingMutex);
            m_playerToRoom.erase(userId);
        }

        int RoomManager::getPlayerRoomId(const std::string& userId) const {
            std::shared_lock<std::shared_mutex> lock(m_playerMappingMutex);

            auto it = m_playerToRoom.find(userId);
            return (it != m_playerToRoom.end()) ? it->second : -1;
        }

        void RoomManager::cleanupSingleRoom(GameRoomPtr room) {
            if (!room) return;

            room->cleanupDisconnectedPlayers();

            if (room->isEmpty()) {
                removeRoom(room->getRoomId());
            }
        }

        bool RoomManager::shouldRemoveRoom(const GameRoom& room) const {
            // 빈 방이거나 30분 이상 비활성 상태인 방
            return room.isEmpty() || room.isInactive(std::chrono::minutes(30));
        }

        void RoomManager::triggerRoomEvent(int roomId, const std::string& event, const std::string& data) {
            if (m_eventCallback) {
                try {
                    m_eventCallback(roomId, event, data);
                }
                catch (const std::exception& e) {
                    spdlog::error("❌ 방 이벤트 콜백 실행 중 예외: {}", e.what());
                }
            }
        }

        void RoomManager::updateStatistics() {
            // 통계 업데이트 로직 (필요시 구현)
            // 현재는 getter 함수들로 실시간 계산하므로 별도 작업 없음
        }

    } // namespace Server
} // namespace Blokus