#pragma once

#include "server/common/ServerTypes.h"
#include <string>
#include <vector>
#include <memory>
#include <future>

namespace Blokus {
    namespace Server {

        // ========================================
        // �����ͺ��̽� �Ŵ��� Ŭ����
        // ========================================
        class DatabaseManager {
        public:
            explicit DatabaseManager(const ServerConfig& config);
            ~DatabaseManager();

            // ���� ����
            bool initialize();
            void shutdown();
            bool isConnected() const;

            // ����� ����
            bool createUser(const std::string& username, const std::string& passwordHash, const std::string& email);
            bool getUserByUsername(const std::string& username, UserInfo& userInfo);
            bool getUserById(const std::string& userId, UserInfo& userInfo);
            bool updateUser(const UserInfo& userInfo);
            bool deleteUser(const std::string& userId);

            // ���� ��� ����
            struct GameRecord {
                std::string gameId;
                std::vector<std::string> playerIds;
                std::string winnerId;
                std::chrono::system_clock::time_point startTime;
                std::chrono::system_clock::time_point endTime;
                std::string gameData; // JSON ������ ���� �� ������
            };

            bool saveGameRecord(const GameRecord& record);
            std::vector<GameRecord> getPlayerGameHistory(const std::string& playerId, int limit = 50);
            bool updatePlayerStats(const std::string& playerId, bool won, int score);

            // ��ŷ ����
            struct RankingEntry {
                std::string userId;
                std::string username;
                int rating;
                int gamesPlayed;
                int gamesWon;
                double winRate;
            };

            std::vector<RankingEntry> getTopPlayers(int limit = 100);
            int getPlayerRank(const std::string& userId);

            // ���� ����
            bool saveSession(const std::string& sessionToken, const std::string& userId,
                std::chrono::system_clock::time_point expiresAt);
            bool validateSession(const std::string& sessionToken, std::string& userId);
            bool deleteSession(const std::string& sessionToken);
            bool cleanupExpiredSessions();

            // ���
            struct DatabaseStats {
                int totalUsers;
                int activeUsers; // �ֱ� 30�� �� �α���
                int totalGames;
                int gamesThisWeek;
                double averageGameDuration;
            };

            DatabaseStats getStats();

        private:
            // ���� Ǯ ����
            class ConnectionPool;
            std::unique_ptr<ConnectionPool> m_connectionPool;

            // �񵿱� �۾� ����
            void executeAsync(std::function<void()> task);
            std::vector<std::future<void>> m_pendingTasks;

            // SQL ���� ����
            bool executeQuery(const std::string& query);
            bool executeQuery(const std::string& query, const std::vector<std::string>& params);

            // ��Ű�� ����
            bool createTables();
            bool migrateSchema();
            int getCurrentSchemaVersion();

            ServerConfig m_config;
            std::atomic<bool> m_isInitialized{ false };
        };

    } // namespace Server
} // namespace Blokus