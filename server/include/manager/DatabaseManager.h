#pragma once

#include "server/common/ServerTypes.h"
#include <string>
#include <vector>
#include <memory>
#include <future>

namespace Blokus {
    namespace Server {

        // ========================================
        // 데이터베이스 매니저 클래스
        // ========================================
        class DatabaseManager {
        public:
            explicit DatabaseManager(const ServerConfig& config);
            ~DatabaseManager();

            // 연결 관리
            bool initialize();
            void shutdown();
            bool isConnected() const;

            // 사용자 관련
            bool createUser(const std::string& username, const std::string& passwordHash, const std::string& email);
            bool getUserByUsername(const std::string& username, UserInfo& userInfo);
            bool getUserById(const std::string& userId, UserInfo& userInfo);
            bool updateUser(const UserInfo& userInfo);
            bool deleteUser(const std::string& userId);

            // 게임 기록 관련
            struct GameRecord {
                std::string gameId;
                std::vector<std::string> playerIds;
                std::string winnerId;
                std::chrono::system_clock::time_point startTime;
                std::chrono::system_clock::time_point endTime;
                std::string gameData; // JSON 형태의 게임 상세 데이터
            };

            bool saveGameRecord(const GameRecord& record);
            std::vector<GameRecord> getPlayerGameHistory(const std::string& playerId, int limit = 50);
            bool updatePlayerStats(const std::string& playerId, bool won, int score);

            // 랭킹 관련
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

            // 세션 관리
            bool saveSession(const std::string& sessionToken, const std::string& userId,
                std::chrono::system_clock::time_point expiresAt);
            bool validateSession(const std::string& sessionToken, std::string& userId);
            bool deleteSession(const std::string& sessionToken);
            bool cleanupExpiredSessions();

            // 통계
            struct DatabaseStats {
                int totalUsers;
                int activeUsers; // 최근 30일 내 로그인
                int totalGames;
                int gamesThisWeek;
                double averageGameDuration;
            };

            DatabaseStats getStats();

        private:
            // 연결 풀 관리
            class ConnectionPool;
            std::unique_ptr<ConnectionPool> m_connectionPool;

            // 비동기 작업 관리
            void executeAsync(std::function<void()> task);
            std::vector<std::future<void>> m_pendingTasks;

            // SQL 쿼리 헬퍼
            bool executeQuery(const std::string& query);
            bool executeQuery(const std::string& query, const std::vector<std::string>& params);

            // 스키마 관리
            bool createTables();
            bool migrateSchema();
            int getCurrentSchemaVersion();

            ServerConfig m_config;
            std::atomic<bool> m_isInitialized{ false };
        };

    } // namespace Server
} // namespace Blokus