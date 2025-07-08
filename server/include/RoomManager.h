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

        // 방 관리를 담당하는 클래스
        class RoomManager {
        public:
            RoomManager();
            ~RoomManager();

            // 방 생성 및 삭제
            std::shared_ptr<GameRoom> createRoom(const Common::RoomInfo& roomInfo, uint32_t hostSessionId);
            bool removeRoom(uint32_t roomId);

            // 방 조회
            std::shared_ptr<GameRoom> findRoom(uint32_t roomId);
            std::vector<std::shared_ptr<GameRoom>> getAllRooms();
            std::vector<Common::RoomInfo> getRoomList(); // 클라이언트용 방 목록

            // 방 참여/나가기
            bool joinRoom(uint32_t roomId, uint32_t sessionId, const std::string& password = "");
            bool leaveRoom(uint32_t roomId, uint32_t sessionId);

            // 통계 정보
            size_t getRoomCount() const;
            size_t getTotalPlayers() const;

            // 방 검색
            std::vector<std::shared_ptr<GameRoom>> findRoomsByHost(const std::string& hostName);
            std::vector<std::shared_ptr<GameRoom>> findAvailableRooms(); // 입장 가능한 방들

            // 정리 작업
            void cleanupEmptyRooms();                              // 빈 방들 정리
            void cleanupFinishedGames();                           // 끝난 게임들 정리

        private:
            // 내부 헬퍼 함수들
            uint32_t generateRoomId();                             // 새 방 ID 생성
            bool isValidRoomInfo(const Common::RoomInfo& roomInfo); // 방 정보 유효성 검증
            void notifyRoomListChanged();                          // 방 목록 변경 알림

        private:
            std::unordered_map<uint32_t, std::shared_ptr<GameRoom>> m_rooms; // 활성 방들
            mutable std::mutex m_roomsMutex;                       // 방 맵 보호용 뮤텍스
            std::atomic<uint32_t> m_nextRoomId{ 1001 };            // 다음 방 ID (1001부터 시작)

            // 통계 정보
            std::atomic<uint64_t> m_totalRoomsCreated{ 0 };        // 총 생성된 방 수
            std::atomic<uint64_t> m_totalGamesPlayed{ 0 };         // 총 플레이된 게임 수
        };

    } // namespace Server
} // namespace Blokus