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
        // 사용자 정보 구조체
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
        // 게임 기록 구조체
        // ========================================
        struct GameRecord {
            uint32_t gameId;
            std::vector<uint32_t> playerIds;
            uint32_t winnerId;
            std::chrono::system_clock::time_point startTime;
            std::chrono::system_clock::time_point endTime;
            std::string gameData; // JSON 형태의 게임 상세 데이터
        };

        // ========================================
        // 데이터베이스 관리자 클래스
        // ========================================
        class DatabaseManager {
        public:
            explicit DatabaseManager();
            ~DatabaseManager();

            // 초기화
            bool initialize();
            void shutdown();
            bool isConnected() const { return m_isInitialized.load(); }

            // ========================================
            // 사용자 관리
            // ========================================

            // 회원가입/로그인
            std::future<bool> createUser(const std::string& username, const std::string& email,
                const std::string& passwordHash);
            std::future<std::optional<UserAccount>> getUserByUsername(const std::string& username);
            std::future<std::optional<UserAccount>> getUserByEmail(const std::string& email);
            std::future<std::optional<UserAccount>> getUserById(uint32_t userId);

            // 사용자 정보 업데이트
            std::future<bool> updateUserLastLogin(uint32_t userId);
            std::future<bool> updateUserGameStats(uint32_t userId, bool won);
            std::future<bool> updateUserPassword(uint32_t userId, const std::string& newPasswordHash);

            // 사용자 상태 관리
            std::future<bool> setUserActive(uint32_t userId, bool active);
            std::future<bool> deleteUser(uint32_t userId);

            // ========================================
            // 게임 기록 관리
            // ========================================

            // 게임 기록 저장/조회
            std::future<uint32_t> createGameRecord(const std::vector<uint32_t>& playerIds,
                const std::string& gameData);
            std::future<bool> finishGameRecord(uint32_t gameId, uint32_t winnerId,
                const std::string& finalGameData);
            std::future<std::vector<GameRecord>> getUserGameHistory(uint32_t userId, int limit = 10);
            std::future<std::optional<GameRecord>> getGameRecord(uint32_t gameId);

            // ========================================
            // 세션 관리 (옵션)
            // ========================================

            std::future<bool> createSession(const std::string& sessionToken, uint32_t userId);
            std::future<std::optional<uint32_t>> getUserBySession(const std::string& sessionToken);
            std::future<bool> deleteSession(const std::string& sessionToken);
            std::future<bool> cleanupExpiredSessions();

            // ========================================
            // 통계 조회
            // ========================================

            struct DatabaseStats {
                int totalUsers;
                int activeUsers; // 최근 30일 내 로그인
                int totalGames;
                int gamesThisWeek;
                double averageGameDuration;
            };

            std::future<DatabaseStats> getStats();

        private:
            // 연결 풀 관리 (선언만)
            class ConnectionPool;
            std::unique_ptr<ConnectionPool> m_connectionPool;

            // 비동기 작업 관리
            void executeAsync(std::function<void()> task);
            std::vector<std::future<void>> m_pendingTasks;

            // SQL 헬퍼
            template<typename T>
            std::future<T> executeQuery(std::function<T(pqxx::connection&)> queryFunc);

            // 스키마 관리 (제거됨 - 물리 DB 설계 완료 후 사용)
            // bool createTables();
            // bool migrateSchema();
            // int getCurrentSchemaVersion();

            // 개발용 더미 데이터
            bool insertDummyData();

            std::atomic<bool> m_isInitialized{ false };
            std::string m_connectionString;
        };

    } // namespace Server
} // namespace Blokus