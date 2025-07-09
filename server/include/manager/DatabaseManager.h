#pragma once

#include "manager/ConfigManager.h"
#include <string>
#include <vector>
#include <memory>
#include <future>
#include <functional>
#include <pqxx/pqxx>

namespace Blokus {
    namespace Server {

        // ========================================
        // ����� ���� ����ü
        // ========================================
        struct UserAccount {
            uint32_t userId;
            std::string username;
            std::string email;
            std::string passwordHash;
            std::chrono::system_clock::time_point createdAt;
            std::chrono::system_clock::time_point lastLoginAt;
            bool isActive;
            int totalGames;
            int wins;
            int losses;
        };

        // ========================================
        // ���� ��� ����ü
        // ========================================
        struct GameRecord {
            uint32_t gameId;
            std::vector<uint32_t> playerIds;
            uint32_t winnerId;
            std::chrono::system_clock::time_point startTime;
            std::chrono::system_clock::time_point endTime;
            std::string gameData; // JSON ������ ���� �� ������
        };

        // ========================================
        // �����ͺ��̽� ������ Ŭ����
        // ========================================
        class DatabaseManager {
        public:
            explicit DatabaseManager();
            ~DatabaseManager();

            // �ʱ�ȭ
            bool initialize();
            void shutdown();
            bool isConnected() const { return m_isInitialized.load(); }

            // ========================================
            // ����� ����
            // ========================================

            // ȸ������/�α���
            std::future<bool> createUser(const std::string& username, const std::string& email,
                const std::string& passwordHash);
            std::future<std::optional<UserAccount>> getUserByUsername(const std::string& username);
            std::future<std::optional<UserAccount>> getUserByEmail(const std::string& email);
            std::future<std::optional<UserAccount>> getUserById(uint32_t userId);

            // ����� ���� ������Ʈ
            std::future<bool> updateUserLastLogin(uint32_t userId);
            std::future<bool> updateUserGameStats(uint32_t userId, bool won);
            std::future<bool> updateUserPassword(uint32_t userId, const std::string& newPasswordHash);

            // ����� ���� ����
            std::future<bool> setUserActive(uint32_t userId, bool active);
            std::future<bool> deleteUser(uint32_t userId);

            // ========================================
            // ���� ��� ����
            // ========================================

            // ���� ��� ����/��ȸ
            std::future<uint32_t> createGameRecord(const std::vector<uint32_t>& playerIds,
                const std::string& gameData);
            std::future<bool> finishGameRecord(uint32_t gameId, uint32_t winnerId,
                const std::string& finalGameData);
            std::future<std::vector<GameRecord>> getUserGameHistory(uint32_t userId, int limit = 10);
            std::future<std::optional<GameRecord>> getGameRecord(uint32_t gameId);

            // ========================================
            // ���� ���� (�ɼ�)
            // ========================================

            std::future<bool> createSession(const std::string& sessionToken, uint32_t userId);
            std::future<std::optional<uint32_t>> getUserBySession(const std::string& sessionToken);
            std::future<bool> deleteSession(const std::string& sessionToken);
            std::future<bool> cleanupExpiredSessions();

            // ========================================
            // ��� ��ȸ
            // ========================================

            struct DatabaseStats {
                int totalUsers;
                int activeUsers; // �ֱ� 30�� �� �α���
                int totalGames;
                int gamesThisWeek;
                double averageGameDuration;
            };

            std::future<DatabaseStats> getStats();

        private:
            // ���� Ǯ ���� (����)
            class ConnectionPool;
            std::unique_ptr<ConnectionPool> m_connectionPool;

            // �񵿱� �۾� ����
            void executeAsync(std::function<void()> task);
            std::vector<std::future<void>> m_pendingTasks;

            // SQL ����
            template<typename T>
            std::future<T> executeQuery(std::function<T(pqxx::connection&)> queryFunc);

            // ��Ű�� ���� (���ŵ� - ���� DB ���� �Ϸ� �� ���)
            // bool createTables();
            // bool migrateSchema();
            // int getCurrentSchemaVersion();

            // ���߿� ���� ������
            bool insertDummyData();

            std::atomic<bool> m_isInitialized{ false };
            std::string m_connectionString;
        };

    } // namespace Server
} // namespace Blokus