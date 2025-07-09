#pragma once

#include <memory>
#include <vector>
#include <unordered_set>
#include <unordered_map>
#include <string>
#include <mutex>
#include <shared_mutex>
#include <atomic>
#include <chrono>

#include "common/Types.h"
#include "common/GameLogic.h"
#include "common/ServerTypes.h"

namespace Blokus {
    namespace Server {

        // 전방 선언 (순환 참조 방지)
        class NetworkManager;

        // ========================================
        // 개별 게임방을 나타내는 클래스
        // ========================================
        class GameRoom {
        public:
            explicit GameRoom(const Common::RoomInfo& roomInfo, uint32_t hostSessionId);
            ~GameRoom();

            // 방 정보 관리
            uint32_t getRoomId() const { return m_roomInfo.roomId; }
            const Common::RoomInfo& getRoomInfo() const { return m_roomInfo; }
            void updateRoomInfo(const Common::RoomInfo& newInfo);

            // 플레이어 관리
            bool addPlayer(uint32_t sessionId, const std::string& username);
            bool removePlayer(uint32_t sessionId);
            bool hasPlayer(uint32_t sessionId) const;
            size_t getPlayerCount() const;
            std::vector<uint32_t> getSessionIds() const;

            // 방장 관리
            uint32_t getHostSessionId() const { return m_hostSessionId; }
            bool isHost(uint32_t sessionId) const { return sessionId == m_hostSessionId; }
            bool changeHost(uint32_t newHostSessionId);            // 방장 위임
            void autoSelectNewHost();                              // 자동 방장 선정

            // 게임 상태 관리
            bool startGame();                                      // 게임 시작
            bool endGame();                                        // 게임 종료
            bool isGameStarted() const { return m_isGameStarted; }
            bool isGameFinished() const;

            // 게임 로직 접근
            Common::GameStateManager& getGameManager() { return m_gameManager; }
            const Common::GameStateManager& getGameManager() const { return m_gameManager; }

            // 플레이어 색상 관리
            Common::PlayerColor assignPlayerColor(uint32_t sessionId);
            Common::PlayerColor getPlayerColor(uint32_t sessionId) const;
            uint32_t getPlayerByColor(Common::PlayerColor color) const;

            // AI 플레이어 관리 (나중에 구현)
            bool addAIPlayer(Common::PlayerColor color, int difficulty);
            bool removeAIPlayer(Common::PlayerColor color);
            std::vector<Common::PlayerColor> getAIPlayers() const;

            // 게임 액션 처리
            bool processBlockPlacement(uint32_t sessionId, const Common::BlockPlacement& placement);
            bool processPlayerAction(uint32_t sessionId, const std::string& action);

            // 방 상태 확인
            bool canJoin() const;                                  // 입장 가능 여부
            bool isEmpty() const;                                  // 방이 비어있는지
            bool isPasswordProtected() const;                     // 비밀번호 보호 여부
            bool checkPassword(const std::string& password) const; // 비밀번호 확인

            // 채팅 관리
            void addChatMessage(const std::string& username, const std::string& message);
            std::vector<std::string> getRecentChatMessages(size_t count = 10) const;

            // 타임스탬프 관리
            std::chrono::steady_clock::time_point getCreationTime() const { return m_creationTime; }
            std::chrono::steady_clock::time_point getLastActivity() const { return m_lastActivity; }
            void updateActivity();

            // 네트워크 매니저 설정 (메시지 브로드캐스트용)
            void setNetworkManager(NetworkManager* networkManager) { m_networkManager = networkManager; }

        private:
            // 내부 헬퍼 함수들
            void initializePlayerSlots();                         // 플레이어 슬롯 초기화
            bool validateGameStart() const;                       // 게임 시작 조건 확인
            void resetGameState();                                // 게임 상태 초기화
            Common::PlayerColor findAvailableColor() const;      // 사용 가능한 색상 찾기
            void broadcastToRoom(const std::string& message);    // 방 내 브로드캐스트

            // 플레이어 슬롯 관리
            void updatePlayerSlots();                             // 플레이어 슬롯 정보 갱신
            bool isPlayerSlotAvailable(Common::PlayerColor color) const;

        private:
            // 방 기본 정보
            Common::RoomInfo m_roomInfo;                          // 방 정보
            uint32_t m_hostSessionId;                             // 방장 세션 ID
            std::string m_password;                               // 방 비밀번호

            // 플레이어 관리
            std::unordered_set<uint32_t> m_playerSessions;        // 플레이어 세션 ID들
            std::unordered_map<uint32_t, std::string> m_playerNames; // 세션 ID -> 사용자명
            std::unordered_map<uint32_t, Common::PlayerColor> m_playerColors; // 세션 ID -> 색상
            std::unordered_map<Common::PlayerColor, uint32_t> m_colorToSession; // 색상 -> 세션 ID

            // AI 플레이어
            std::unordered_map<Common::PlayerColor, int> m_aiPlayers; // AI 색상 -> 난이도

            // 게임 상태
            std::atomic<bool> m_isGameStarted{ false };           // 게임 시작 여부
            Common::GameStateManager m_gameManager;              // 게임 상태 관리자

            // 채팅 히스토리
            std::vector<std::string> m_chatHistory;               // 채팅 메시지들
            static constexpr size_t MAX_CHAT_HISTORY = 100;      // 최대 채팅 보관 수

            // 동기화
            mutable std::shared_mutex m_roomMutex;                // 방 상태 보호용 뮤텍스

            // 타임스탬프 정보
            std::chrono::steady_clock::time_point m_creationTime; // 방 생성 시간
            std::chrono::steady_clock::time_point m_lastActivity; // 마지막 활동 시간

            // 네트워크 관리자 (메시지 전송용)
            NetworkManager* m_networkManager = nullptr;          // 약한 참조
        };

    } // namespace Server
} // namespace Blokus