#pragma once

#include "common/ServerTypes.h"
#include "common/Types.h"
#include "common/GameLogic.h"
#include <vector>
#include <memory>
#include <mutex>
#include <chrono>

namespace Blokus {
    namespace Server {

        // ���� ����
        class Session;
        using SessionPtr = std::shared_ptr<Session>;  // ClientSessionPtr ��� SessionPtr ���

        // ========================================
        // �÷��̾� ���� ����ü
        // ========================================
        struct PlayerInfo {
            std::string userId;
            std::string username;
            Blokus::Common::PlayerColor color;
            bool isReady;
            bool isAI;
            SessionPtr session;  // ClientSessionPtr -> SessionPtr

            PlayerInfo()
                : color(Blokus::Common::PlayerColor::None)
                , isReady(false)
                , isAI(false)
            {
            }
        };

        // ========================================
        // ���� ���ӹ� Ŭ����
        // ========================================
        class GameRoom {
        public:
            explicit GameRoom(int roomId, const std::string& roomName, const std::string& hostId);
            ~GameRoom();

            // �� ����
            int getRoomId() const { return m_roomId; }
            const std::string& getRoomName() const { return m_roomName; }
            const std::string& getHostId() const { return m_hostId; }
            RoomState getState() const { return m_state; }

            // �÷��̾� ����
            bool addPlayer(SessionPtr client, const std::string& userId, const std::string& username);
            bool removePlayer(const std::string& userId);
            bool setPlayerReady(const std::string& userId, bool ready);
            size_t getPlayerCount() const;
            bool isFull() const;

            // ���� ����
            bool startGame();
            bool endGame();
            void resetGame();

            // �޽��� ��ε�ĳ��Ʈ
            void broadcastMessage(const std::string& message, const std::string& excludeUserId = "");
            void sendToPlayer(const std::string& userId, const std::string& message);

            // ���� ����
            bool makeMove(const std::string& userId, const Blokus::Common::BlockPlacement& move);
            Blokus::Common::PlayerColor getCurrentPlayer() const;
            bool isGameFinished() const;

            // �� ���� Ȯ��
            bool canJoin() const;
            bool isEmpty() const;

        private:
            // ��ε�ĳ��Ʈ ���� �Լ���
            void broadcastPlayerJoined(const std::string& username);
            void broadcastPlayerLeft(const std::string& username);
            void broadcastPlayerReady(const std::string& username, bool ready);
            void broadcastNewHost(const std::string& username);
            void broadcastGameStart();
            void broadcastGameEnd();
            void broadcastMove(const std::string& userId, const Blokus::Common::BlockPlacement& move);
            void broadcastTurnChange();

        private:
            int m_roomId;
            std::string m_roomName;
            std::string m_hostId;
            RoomState m_state;

            std::vector<PlayerInfo> m_players;
            mutable std::mutex m_playersMutex;

            std::unique_ptr<Blokus::Common::GameLogic> m_gameLogic;
            std::chrono::steady_clock::time_point m_gameStartTime;
            std::chrono::steady_clock::time_point m_lastActivity;
        };

    } // namespace Server
} // namespace Blokus