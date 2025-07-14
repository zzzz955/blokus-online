#pragma once

#include "common/Types.h"
#include "ServerTypes.h"
#include "common/GameLogic.h"
#include "Session.h"
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
        // �÷��̾� ���� ����ü (������)
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
        // GameRoom Ŭ����
        // ========================================
        class GameRoom {
        public:
            // ������/�Ҹ���
            explicit GameRoom(int roomId, const std::string& roomName, const std::string& hostId);
            ~GameRoom();

            // �⺻ ���� ������
            int getRoomId() const { return m_roomId; }
            const std::string& getRoomName() const { return m_roomName; }
            const std::string& getHostId() const { return m_hostId; }
            RoomState getState() const { return m_state; }

            // �÷��̾� ����
            bool addPlayer(SessionPtr session, const std::string& userId, const std::string& username);
            bool removePlayer(const std::string& userId);
            bool hasPlayer(const std::string& userId) const;
            PlayerInfo* getPlayer(const std::string& userId);
            const PlayerInfo* getPlayer(const std::string& userId) const;

            // �÷��̾� ���� ����
            bool setPlayerReady(const std::string& userId, bool ready);
            bool isPlayerReady(const std::string& userId) const;
            bool setPlayerColor(const std::string& userId, Common::PlayerColor color);

            // ȣ��Ʈ ����
            bool isHost(const std::string& userId) const;
            bool transferHost(const std::string& newHostId);
            void autoSelectNewHost();

            // �� ���� ����
            size_t getPlayerCount() const;
            size_t getMaxPlayers() const { return Common::MAX_PLAYERS; }
            bool isFull() const;
            bool isEmpty() const;
            bool canStartGame() const;
            bool isPlaying() const { return m_state == RoomState::Playing; }
            bool isWaiting() const { return m_state == RoomState::Waiting; }

            // ���� ����
            bool startGame();
            bool endGame();
            bool pauseGame();
            bool resumeGame();
            void resetGame();

            // ���� ���� ����
            Common::GameLogic* getGameLogic() const { return m_gameLogic.get(); }

            // �޽��� ����
            void broadcastMessage(const std::string& message, const std::string& excludeUserId = "");
            void sendToPlayer(const std::string& userId, const std::string& message);
            void sendToHost(const std::string& message);

            // �� ���� ����
            Common::RoomInfo getRoomInfo() const;
            std::vector<PlayerInfo> getPlayerList() const;

            // ��ƿ��Ƽ
            void updateActivity();
            std::chrono::steady_clock::time_point getLastActivity() const { return m_lastActivity; }
            bool isInactive(std::chrono::minutes threshold) const;

            // ���� ����
            Common::PlayerColor getAvailableColor() const;
            bool isColorTaken(Common::PlayerColor color) const;
            void assignColorsAutomatically();

            // ���� �Լ�
            void cleanupDisconnectedPlayers();

            // ���� ��ƿ��Ƽ �Լ���
            void broadcastPlayerJoined(const std::string& username);
            void broadcastPlayerLeft(const std::string& username);
            void broadcastPlayerReady(const std::string& username, bool ready);
            void broadcastHostChanged(const std::string& newHostName);
            void broadcastGameStart();
            void broadcastGameEnd();
            void broadcastGameState();

        private:
            // �⺻ ����
            int m_roomId;
            std::string m_roomName;
            std::string m_hostId;
            RoomState m_state;

            // �÷��̾� ����
            std::vector<PlayerInfo> m_players;
            mutable std::mutex m_playersMutex;

            // ���� ����
            std::unique_ptr<Common::GameLogic> m_gameLogic;

            // �ð� ����
            std::chrono::steady_clock::time_point m_createdTime;
            std::chrono::steady_clock::time_point m_gameStartTime;
            std::chrono::steady_clock::time_point m_lastActivity;

            // �� ����
            bool m_isPrivate;
            std::string m_password;
            int m_maxPlayers;

            // ���� ����
            void assignPlayerColor(PlayerInfo& player);
            Common::PlayerColor getNextAvailableColor() const;

            // ���� �Լ���
            bool validatePlayerCount() const;
            bool validateAllPlayersReady() const;
            bool validateGameCanStart() const;

            // ���� �Լ���
            void resetPlayerStates();
        };

        // ========================================
        // RoomManager���� ������ ���� Ÿ�� ����
        // ========================================
        using GameRoomPtr = std::shared_ptr<GameRoom>;

    } // namespace Server
} // namespace Blokus