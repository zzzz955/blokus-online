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

        // ���� ���� (��ȯ ���� ����)
        class NetworkManager;

        // ========================================
        // ���� ���ӹ��� ��Ÿ���� Ŭ����
        // ========================================
        class GameRoom {
        public:
            explicit GameRoom(const Common::RoomInfo& roomInfo, uint32_t hostSessionId);
            ~GameRoom();

            // �� ���� ����
            uint32_t getRoomId() const { return m_roomInfo.roomId; }
            const Common::RoomInfo& getRoomInfo() const { return m_roomInfo; }
            void updateRoomInfo(const Common::RoomInfo& newInfo);

            // �÷��̾� ����
            bool addPlayer(uint32_t sessionId, const std::string& username);
            bool removePlayer(uint32_t sessionId);
            bool hasPlayer(uint32_t sessionId) const;
            size_t getPlayerCount() const;
            std::vector<uint32_t> getSessionIds() const;

            // ���� ����
            uint32_t getHostSessionId() const { return m_hostSessionId; }
            bool isHost(uint32_t sessionId) const { return sessionId == m_hostSessionId; }
            bool changeHost(uint32_t newHostSessionId);            // ���� ����
            void autoSelectNewHost();                              // �ڵ� ���� ����

            // ���� ���� ����
            bool startGame();                                      // ���� ����
            bool endGame();                                        // ���� ����
            bool isGameStarted() const { return m_isGameStarted; }
            bool isGameFinished() const;

            // ���� ���� ����
            Common::GameStateManager& getGameManager() { return m_gameManager; }
            const Common::GameStateManager& getGameManager() const { return m_gameManager; }

            // �÷��̾� ���� ����
            Common::PlayerColor assignPlayerColor(uint32_t sessionId);
            Common::PlayerColor getPlayerColor(uint32_t sessionId) const;
            uint32_t getPlayerByColor(Common::PlayerColor color) const;

            // AI �÷��̾� ���� (���߿� ����)
            bool addAIPlayer(Common::PlayerColor color, int difficulty);
            bool removeAIPlayer(Common::PlayerColor color);
            std::vector<Common::PlayerColor> getAIPlayers() const;

            // ���� �׼� ó��
            bool processBlockPlacement(uint32_t sessionId, const Common::BlockPlacement& placement);
            bool processPlayerAction(uint32_t sessionId, const std::string& action);

            // �� ���� Ȯ��
            bool canJoin() const;                                  // ���� ���� ����
            bool isEmpty() const;                                  // ���� ����ִ���
            bool isPasswordProtected() const;                     // ��й�ȣ ��ȣ ����
            bool checkPassword(const std::string& password) const; // ��й�ȣ Ȯ��

            // ä�� ����
            void addChatMessage(const std::string& username, const std::string& message);
            std::vector<std::string> getRecentChatMessages(size_t count = 10) const;

            // Ÿ�ӽ����� ����
            std::chrono::steady_clock::time_point getCreationTime() const { return m_creationTime; }
            std::chrono::steady_clock::time_point getLastActivity() const { return m_lastActivity; }
            void updateActivity();

            // ��Ʈ��ũ �Ŵ��� ���� (�޽��� ��ε�ĳ��Ʈ��)
            void setNetworkManager(NetworkManager* networkManager) { m_networkManager = networkManager; }

        private:
            // ���� ���� �Լ���
            void initializePlayerSlots();                         // �÷��̾� ���� �ʱ�ȭ
            bool validateGameStart() const;                       // ���� ���� ���� Ȯ��
            void resetGameState();                                // ���� ���� �ʱ�ȭ
            Common::PlayerColor findAvailableColor() const;      // ��� ������ ���� ã��
            void broadcastToRoom(const std::string& message);    // �� �� ��ε�ĳ��Ʈ

            // �÷��̾� ���� ����
            void updatePlayerSlots();                             // �÷��̾� ���� ���� ����
            bool isPlayerSlotAvailable(Common::PlayerColor color) const;

        private:
            // �� �⺻ ����
            Common::RoomInfo m_roomInfo;                          // �� ����
            uint32_t m_hostSessionId;                             // ���� ���� ID
            std::string m_password;                               // �� ��й�ȣ

            // �÷��̾� ����
            std::unordered_set<uint32_t> m_playerSessions;        // �÷��̾� ���� ID��
            std::unordered_map<uint32_t, std::string> m_playerNames; // ���� ID -> ����ڸ�
            std::unordered_map<uint32_t, Common::PlayerColor> m_playerColors; // ���� ID -> ����
            std::unordered_map<Common::PlayerColor, uint32_t> m_colorToSession; // ���� -> ���� ID

            // AI �÷��̾�
            std::unordered_map<Common::PlayerColor, int> m_aiPlayers; // AI ���� -> ���̵�

            // ���� ����
            std::atomic<bool> m_isGameStarted{ false };           // ���� ���� ����
            Common::GameStateManager m_gameManager;              // ���� ���� ������

            // ä�� �����丮
            std::vector<std::string> m_chatHistory;               // ä�� �޽�����
            static constexpr size_t MAX_CHAT_HISTORY = 100;      // �ִ� ä�� ���� ��

            // ����ȭ
            mutable std::shared_mutex m_roomMutex;                // �� ���� ��ȣ�� ���ؽ�

            // Ÿ�ӽ����� ����
            std::chrono::steady_clock::time_point m_creationTime; // �� ���� �ð�
            std::chrono::steady_clock::time_point m_lastActivity; // ������ Ȱ�� �ð�

            // ��Ʈ��ũ ������ (�޽��� ���ۿ�)
            NetworkManager* m_networkManager = nullptr;          // ���� ����
        };

    } // namespace Server
} // namespace Blokus