#pragma once

#include "common/Types.h"
#include "common/ServerTypes.h"
#include "common/GameLogic.h"
#include "core/Session.h"
#include <vector>
#include <unordered_map>
#include <mutex>
#include <memory>
#include <chrono>
#include <string>

namespace Blokus {
    namespace Server {

        using SessionPtr = std::shared_ptr<Session>;

        // ========================================
        // 플레이어 정보 구조체 (서버용)
        // ========================================
        struct PlayerInfo {
            std::string userId;
            std::string username;
            SessionPtr session;
            Common::PlayerColor color;
            bool isHost;
            bool isReady;
            bool isAI;
            int aiDifficulty;
            int score;
            int remainingBlocks;
            std::chrono::steady_clock::time_point lastActivity;

            PlayerInfo()
                : userId("")
                , username("")
                , session(nullptr)
                , color(Common::PlayerColor::None)
                , isHost(false)
                , isReady(false)
                , isAI(false)
                , aiDifficulty(2)
                , score(0)
                , remainingBlocks(Common::BLOCKS_PER_PLAYER)
                , lastActivity(std::chrono::steady_clock::now())
            {
            }

            PlayerInfo(const std::string& uid, const std::string& uname, SessionPtr sess)
                : userId(uid)
                , username(uname)
                , session(sess)
                , color(Common::PlayerColor::None)
                , isHost(false)
                , isReady(false)
                , isAI(false)
                , aiDifficulty(2)
                , score(0)
                , remainingBlocks(Common::BLOCKS_PER_PLAYER)
                , lastActivity(std::chrono::steady_clock::now())
            {
            }

            bool isValid() const {
                return !userId.empty() && !username.empty() && session != nullptr;
            }

            void updateActivity() {
                lastActivity = std::chrono::steady_clock::now();
            }
        };

        // ========================================
        // GameRoom 클래스
        // ========================================
        class GameRoom {
        public:
            // 생성자/소멸자
            explicit GameRoom(int roomId, const std::string& roomName, const std::string& hostId);
            ~GameRoom();

            // 기본 정보 접근자
            int getRoomId() const { return m_roomId; }
            const std::string& getRoomName() const { return m_roomName; }
            const std::string& getHostId() const { return m_hostId; }
            RoomState getState() const { return m_state; }

            // 플레이어 관리
            bool addPlayer(SessionPtr session, const std::string& userId, const std::string& username);
            bool removePlayer(const std::string& userId);
            bool hasPlayer(const std::string& userId) const;
            PlayerInfo* getPlayer(const std::string& userId);
            const PlayerInfo* getPlayer(const std::string& userId) const;

            // 플레이어 상태 관리
            bool setPlayerReady(const std::string& userId, bool ready);
            bool isPlayerReady(const std::string& userId) const;
            bool setPlayerColor(const std::string& userId, Common::PlayerColor color);

            // 호스트 관리
            bool isHost(const std::string& userId) const;
            bool transferHost(const std::string& newHostId);
            void autoSelectNewHost();

            // 방 상태 정보
            size_t getPlayerCount() const;
            size_t getMaxPlayers() const { return Common::MAX_PLAYERS; }
            bool isFull() const;
            bool isEmpty() const;
            bool canStartGame() const;
            bool isPlaying() const { return m_state == RoomState::Playing; }
            bool isWaiting() const { return m_state == RoomState::Waiting; }

            // 게임 제어
            bool startGame();
            bool endGame();
            bool pauseGame();
            bool resumeGame();
            void resetGame();

            // 게임 로직 접근
            Common::GameLogic* getGameLogic() const { return m_gameLogic.get(); }

            // 메시지 전송
            void broadcastMessage(const std::string& message, const std::string& excludeUserId = "");
            void sendToPlayer(const std::string& userId, const std::string& message);
            void sendToHost(const std::string& message);

            // 방 정보 생성
            Common::RoomInfo getRoomInfo() const;
            std::vector<PlayerInfo> getPlayerList() const;

            // 유틸리티
            void updateActivity();
            std::chrono::steady_clock::time_point getLastActivity() const { return m_lastActivity; }
            bool isInactive(std::chrono::minutes threshold) const;

            // 색상 관리
            Common::PlayerColor getAvailableColor() const;
            bool isColorTaken(Common::PlayerColor color) const;
            void assignColorsAutomatically();

            // 정리 함수
            void cleanupDisconnectedPlayers();

            // 내부 유틸리티 함수들
            void broadcastPlayerJoined(const std::string& username);
            void broadcastPlayerLeft(const std::string& username);
            void broadcastPlayerReady(const std::string& username, bool ready);
            void broadcastHostChanged(const std::string& newHostName);
            void broadcastGameStart();
            void broadcastGameEnd();
            void broadcastGameState();

        private:
            // 기본 정보
            int m_roomId;
            std::string m_roomName;
            std::string m_hostId;
            RoomState m_state;

            // 플레이어 관리
            std::vector<PlayerInfo> m_players;
            mutable std::mutex m_playersMutex;

            // 게임 로직
            std::unique_ptr<Common::GameLogic> m_gameLogic;

            // 시간 관리
            std::chrono::steady_clock::time_point m_createdTime;
            std::chrono::steady_clock::time_point m_gameStartTime;
            std::chrono::steady_clock::time_point m_lastActivity;

            // 방 설정
            bool m_isPrivate;
            std::string m_password;
            int m_maxPlayers;

            // 색상 배정
            void assignPlayerColor(PlayerInfo& player);
            Common::PlayerColor getNextAvailableColor() const;

            // 검증 함수들
            bool validatePlayerCount() const;
            bool validateAllPlayersReady() const;
            bool validateGameCanStart() const;

            // 정리 함수들
            void resetPlayerStates();
        };

        // ========================================
        // RoomManager와의 연동을 위한 타입 정의
        // ========================================
        using GameRoomPtr = std::shared_ptr<GameRoom>;

    } // namespace Server
} // namespace Blokus