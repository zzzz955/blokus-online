#include "DatabaseManager.h"
#include "ConfigManager.h"
#include <pqxx/pqxx>
#include <spdlog/spdlog.h>
#include <spdlog/fmt/fmt.h>
#include <queue>
#include <mutex>
#include <ctime>

namespace Blokus {
    namespace Server {

        // ========================================
        // 🔥 ConnectionPool 구현 (cpp에만 정의)
        // ========================================
        class ConnectionPool {
        private:
            std::queue<std::unique_ptr<pqxx::connection>> connections_;
            std::mutex mutex_;
            std::string connectionString_;

        public:
            ConnectionPool(const std::string& connStr, size_t size) : connectionString_(connStr) {
                spdlog::info("Creating database connection pool with {} connections", size);
                for (size_t i = 0; i < size; ++i) {
                    try {
                        connections_.push(std::make_unique<pqxx::connection>(connectionString_));
                    }
                    catch (const std::exception& e) {
                        spdlog::error("Failed to create connection {}: {}", i + 1, e.what());
                        throw;
                    }
                }
                spdlog::info("Database connection pool created successfully");
            }

            std::unique_ptr<pqxx::connection> getConnection() {
                std::lock_guard<std::mutex> lock(mutex_);
                if (connections_.empty()) {
                    return std::make_unique<pqxx::connection>(connectionString_);
                }
                auto conn = std::move(connections_.front());
                connections_.pop();
                return conn;
            }

            void returnConnection(std::unique_ptr<pqxx::connection> conn) {
                if (conn && conn->is_open()) {
                    std::lock_guard<std::mutex> lock(mutex_);
                    connections_.push(std::move(conn));
                }
            }
        };

        // ========================================
        // 🔥 UserAccount 메서드 구현
        // ========================================
        double UserAccount::getWinRate() const {
            return totalGames > 0 ? static_cast<double>(wins) / totalGames * 100.0 : 0.0;
        }

        double UserAccount::getAverageScore() const {
            return totalGames > 0 ? static_cast<double>(totalScore) / totalGames : 0.0;
        }

        // ========================================
        // 🔥 DatabaseManager 구현
        // ========================================

        DatabaseManager::DatabaseManager() : isInitialized_(false) {}

        DatabaseManager::~DatabaseManager() {
            shutdown();
        }

        bool DatabaseManager::initialize() {
            if (isInitialized_) {
                spdlog::warn("DatabaseManager already initialized");
                return true;
            }

            try {
                spdlog::info("Initializing database connection...");

                std::string connectionString = fmt::format(
                    "host={} port={} dbname={} user={} password={}",
                    ConfigManager::dbHost, ConfigManager::dbPort,
                    ConfigManager::dbName, ConfigManager::dbUser, ConfigManager::dbPassword
                );

                dbPool_ = std::make_unique<ConnectionPool>(connectionString, ConfigManager::dbPoolSize);

                // 연결 테스트
                auto conn = dbPool_->getConnection();
                pqxx::work txn(*conn);
                auto result = txn.exec("SELECT 1");
                txn.commit();
                dbPool_->returnConnection(std::move(conn));

                if (!result.empty()) {
                    isInitialized_ = true;
                    spdlog::info("✅ DatabaseManager initialized successfully");
                    return true;
                }

            }
            catch (const std::exception& e) {
                spdlog::error("Failed to initialize DatabaseManager: {}", e.what());
                spdlog::error("💡 Please ensure:");
                spdlog::error("  1. PostgreSQL server is running");
                spdlog::error("  2. Database credentials are correct");
                spdlog::error("  3. Database '{}' exists", ConfigManager::dbName);
            }

            return false;
        }

        void DatabaseManager::shutdown() {
            if (isInitialized_) {
                spdlog::info("Shutting down DatabaseManager...");
                dbPool_.reset();
                isInitialized_ = false;
                spdlog::info("DatabaseManager shutdown complete");
            }
        }

        // ========================================
        // 🔥 사용자 관리 구현
        // ========================================

        std::optional<UserAccount> DatabaseManager::getUserByUsername(const std::string& username) {
            if (!isInitialized_) return std::nullopt;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                auto result = txn.exec_params(
                    "SELECT u.user_id, u.username, u.display_name, u.password_hash, "
                    "       COALESCE(s.total_games, 0), COALESCE(s.wins, 0), COALESCE(s.losses, 0), "
                    "       COALESCE(s.draws, 0), COALESCE(s.level, 1), COALESCE(s.experience_points, 0), "
                    "       COALESCE(s.total_score, 0), COALESCE(s.best_score, 0), "
                    "       u.is_active "
                    "FROM users u "
                    "LEFT JOIN user_stats s ON u.user_id = s.user_id "
                    "WHERE LOWER(u.username) = LOWER($1) AND u.is_active = true",
                    username
                );

                if (result.empty()) {
                    txn.abort();
                    dbPool_->returnConnection(std::move(conn));
                    return std::nullopt;
                }

                UserAccount user;
                user.userId = result[0][0].as<uint32_t>();
                user.username = result[0][1].as<std::string>();
                user.displayName = result[0][2].is_null() ? user.username : result[0][2].as<std::string>();
                user.passwordHash = result[0][3].as<std::string>();
                user.totalGames = result[0][4].as<int>();
                user.wins = result[0][5].as<int>();
                user.losses = result[0][6].as<int>();
                user.draws = result[0][7].as<int>();
                user.level = result[0][8].as<int>();
                user.experiencePoints = result[0][9].as<int>();
                user.totalScore = result[0][10].as<int>();
                user.bestScore = result[0][11].as<int>();
                user.isActive = result[0][12].as<bool>();

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                return user;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("getUserByUsername 오류: {}", e.what());
                return std::nullopt;
            }
        }

        std::optional<UserAccount> DatabaseManager::getUserByDisplayName(const std::string& displayName) {
            if (!isInitialized_) return std::nullopt;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                auto result = txn.exec_params(
                    "SELECT u.user_id, u.username, u.display_name, u.password_hash, "
                    "       COALESCE(s.total_games, 0), COALESCE(s.wins, 0), COALESCE(s.losses, 0), "
                    "       COALESCE(s.draws, 0), COALESCE(s.level, 1), COALESCE(s.experience_points, 0), "
                    "       COALESCE(s.total_score, 0), COALESCE(s.best_score, 0), "
                    "       u.is_active "
                    "FROM users u "
                    "LEFT JOIN user_stats s ON u.user_id = s.user_id "
                    "WHERE LOWER(u.display_name) = LOWER($1) AND u.is_active = true",
                    displayName
                );

                if (result.empty()) {
                    txn.abort();
                    dbPool_->returnConnection(std::move(conn));
                    return std::nullopt;
                }

                UserAccount user;
                user.userId = result[0][0].as<uint32_t>();
                user.username = result[0][1].as<std::string>();
                user.displayName = result[0][2].is_null() ? user.username : result[0][2].as<std::string>();
                user.passwordHash = result[0][3].as<std::string>();
                user.totalGames = result[0][4].as<int>();
                user.wins = result[0][5].as<int>();
                user.losses = result[0][6].as<int>();
                user.draws = result[0][7].as<int>();
                user.level = result[0][8].as<int>();
                user.experiencePoints = result[0][9].as<int>();
                user.totalScore = result[0][10].as<int>();
                user.bestScore = result[0][11].as<int>();
                user.isActive = result[0][12].as<bool>();

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                return user;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("getUserByDisplayName 오류: {}", e.what());
                return std::nullopt;
            }
        }

        std::optional<UserAccount> DatabaseManager::getUserById(uint32_t userId) {
            if (!isInitialized_) return std::nullopt;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                auto result = txn.exec_params(
                    "SELECT u.user_id, u.username, u.display_name, u.password_hash, "
                    "       COALESCE(s.total_games, 0), COALESCE(s.wins, 0), COALESCE(s.losses, 0), "
                    "       COALESCE(s.draws, 0), COALESCE(s.level, 1), COALESCE(s.experience_points, 0), "
                    "       COALESCE(s.total_score, 0), COALESCE(s.best_score, 0), "
                    "       u.is_active "
                    "FROM users u "
                    "LEFT JOIN user_stats s ON u.user_id = s.user_id "
                    "WHERE u.user_id = $1 AND u.is_active = true",
                    userId
                );

                if (result.empty()) {
                    txn.abort();
                    dbPool_->returnConnection(std::move(conn));
                    return std::nullopt;
                }

                UserAccount user;
                user.userId = result[0]["user_id"].as<uint32_t>();
                user.username = result[0]["username"].as<std::string>();
                user.displayName = result[0]["display_name"].is_null() ? user.username : result[0]["display_name"].as<std::string>();
                user.passwordHash = result[0]["password_hash"].as<std::string>();
                user.totalGames = result[0][4].as<int>();
                user.wins = result[0][5].as<int>();
                user.losses = result[0][6].as<int>();
                user.draws = result[0][7].as<int>();
                user.level = result[0][8].as<int>();
                user.experiencePoints = result[0][9].as<int>();
                user.totalScore = result[0][10].as<int>();
                user.bestScore = result[0][11].as<int>();
                user.isActive = result[0]["is_active"].as<bool>();

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                return user;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("getUserById 오류: {}", e.what());
                return std::nullopt;
            }
        }

        bool DatabaseManager::createUser(const std::string& username, const std::string& passwordHash) {
            if (!isInitialized_) return false;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                auto result = txn.exec_params(
                    "INSERT INTO users (username, password_hash) VALUES ($1, $2) RETURNING user_id",
                    username, passwordHash
                );

                if (!result.empty()) {
                    int userId = result[0][0].as<int>();

                    // 사용자 통계 초기화
                    txn.exec_params("INSERT INTO user_stats (user_id) VALUES ($1)", userId);

                    txn.commit();
                    dbPool_->returnConnection(std::move(conn));
                    spdlog::info("Created user: {} (ID: {})", username, userId);
                    return true;
                }

                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                return false;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("createUser 오류: {}", e.what());
                return false;
            }
        }

        bool DatabaseManager::updateUserLastLogin(const std::string& username) {
            if (!isInitialized_) return false;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                auto result = txn.exec_params(
                    "UPDATE users SET last_login_at = CURRENT_TIMESTAMP WHERE username = $1",
                    username
                );

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                return result.affected_rows() > 0;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("updateUserLastLogin 오류: {}", e.what());
                return false;
            }
        }

        bool DatabaseManager::updateUserLastLogin(uint32_t userId) {
            if (!isInitialized_) return false;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                auto result = txn.exec_params(
                    "UPDATE users SET last_login_at = CURRENT_TIMESTAMP WHERE user_id = $1",
                    userId
                );

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                return result.affected_rows() > 0;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("updateUserLastLogin 오류: {}", e.what());
                return false;
            }
        }

        std::optional<UserAccount> DatabaseManager::authenticateUser(const std::string& username, const std::string& passwordHash) {
            if (!isInitialized_) return std::nullopt;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                auto result = txn.exec_params(
                    "SELECT u.user_id, u.username, u.display_name, u.password_hash, "
                    "       COALESCE(s.total_games, 0), COALESCE(s.wins, 0), COALESCE(s.losses, 0), "
                    "       COALESCE(s.draws, 0), COALESCE(s.level, 1), COALESCE(s.experience_points, 0), "
                    "       COALESCE(s.total_score, 0), COALESCE(s.best_score, 0), "
                    "       u.is_active "
                    "FROM users u "
                    "LEFT JOIN user_stats s ON u.user_id = s.user_id "
                    "WHERE LOWER(u.username) = LOWER($1) AND u.password_hash = $2 AND u.is_active = true",
                    username, passwordHash
                );

                if (result.empty()) {
                    txn.abort();
                    dbPool_->returnConnection(std::move(conn));
                    return std::nullopt;
                }

                UserAccount user;
                user.userId = result[0]["user_id"].as<uint32_t>();
                user.username = result[0]["username"].as<std::string>();
                user.displayName = result[0]["display_name"].is_null() ? user.username : result[0]["display_name"].as<std::string>();
                user.passwordHash = result[0]["password_hash"].as<std::string>();
                user.totalGames = result[0][4].as<int>();
                user.wins = result[0][5].as<int>();
                user.losses = result[0][6].as<int>();
                user.draws = result[0][7].as<int>();
                user.level = result[0][8].as<int>();
                user.experiencePoints = result[0][9].as<int>();
                user.totalScore = result[0][10].as<int>();
                user.bestScore = result[0][11].as<int>();
                user.isActive = result[0]["is_active"].as<bool>();

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                return user;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("authenticateUser 오류: {}", e.what());
                return std::nullopt;
            }
        }

        bool DatabaseManager::isUsernameAvailable(const std::string& username) {
            if (!isInitialized_) return false;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                auto result = txn.exec_params(
                    "SELECT COUNT(*) FROM users WHERE LOWER(username) = LOWER($1)",
                    username
                );

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                return result[0][0].as<int>() == 0;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("isUsernameAvailable 오류: {}", e.what());
                return false;
            }
        }

        // ========================================
        // 🔥 통계 및 조회 기능
        // ========================================

        DatabaseStats DatabaseManager::getStats() {
            DatabaseStats stats = {};
            if (!isInitialized_) return stats;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                auto r1 = txn.exec("SELECT COUNT(*) FROM users");
                stats.totalUsers = r1[0][0].as<int>();

                auto r2 = txn.exec("SELECT COUNT(*) FROM users WHERE is_active = true");
                stats.activeUsers = r2[0][0].as<int>();

                auto r3 = txn.exec(
                    "SELECT COUNT(*) FROM users "
                    "WHERE last_login_at > CURRENT_TIMESTAMP - INTERVAL '5 minutes' AND is_active = true"
                );
                stats.onlineUsers = r3[0][0].as<int>();

                auto r4 = txn.exec("SELECT COALESCE(SUM(total_games), 0) FROM user_stats");
                stats.totalGames = r4[0][0].as<int>();

                auto r5 = txn.exec("SELECT COUNT(*) FROM user_stats WHERE total_games > 0");
                stats.totalStats = r5[0][0].as<int>();

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                return stats;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("getStats 오류: {}", e.what());
                return stats;
            }
        }

        // ========================================
        // 🔥 게임 관련 기능 (PostgreSQL 함수 사용)
        // ========================================

        bool DatabaseManager::updateGameStats(uint32_t userId, bool won, bool draw, int score) {
            if (!isInitialized_) return false;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                // 직접 업데이트 방식 (PostgreSQL 함수 대신)
                txn.exec_params(
                    "UPDATE user_stats SET "
                    "total_games = total_games + 1, "
                    "wins = wins + CASE WHEN $2 THEN 1 ELSE 0 END, "
                    "losses = losses + CASE WHEN NOT $2 AND NOT $3 THEN 1 ELSE 0 END, "
                    "draws = draws + CASE WHEN $3 THEN 1 ELSE 0 END, "
                    "total_score = total_score + $4, "
                    "best_score = GREATEST(best_score, $4), "
                    "current_win_streak = CASE WHEN $2 THEN current_win_streak + 1 ELSE 0 END, "
                    "longest_win_streak = GREATEST(longest_win_streak, "
                        "CASE WHEN $2 THEN current_win_streak + 1 ELSE current_win_streak END), "
                    "last_played = CURRENT_TIMESTAMP, "
                    "updated_at = CURRENT_TIMESTAMP "
                    "WHERE user_id = $1",
                    userId, won, draw, score
                );

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                return true;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("updateGameStats 오류: {}", e.what());
                return false;
            }
        }

        bool DatabaseManager::saveGameResults(const std::vector<uint32_t>& playerIds, 
                                            const std::vector<int>& scores, 
                                            const std::vector<bool>& isWinner,
                                            bool isDraw) {
            if (!isInitialized_) return false;
            
            if (playerIds.size() != scores.size() || playerIds.size() != isWinner.size()) {
                spdlog::error("saveGameResults: 매개변수 배열 크기가 일치하지 않음");
                return false;
            }

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                spdlog::info("💾 게임 결과 저장 시작: {} 명의 플레이어", playerIds.size());
                
                // 모든 플레이어의 통계를 업데이트
                for (size_t i = 0; i < playerIds.size(); ++i) {
                    bool won = isDraw ? false : isWinner[i]; // 무승부인 경우 승리 처리 안함
                    bool draw = isDraw; // 무승부 여부 사용
                    int score = scores[i];
                    
                    // 해당 플레이어가 존재하는지 확인
                    auto userCheck = txn.exec_params(
                        "SELECT user_id FROM users WHERE user_id = $1 AND is_active = true",
                        playerIds[i]
                    );
                    
                    if (userCheck.empty()) {
                        spdlog::warn("⚠️ 존재하지 않는 사용자 ID: {}", playerIds[i]);
                        continue;
                    }
                    
                    // user_stats에 해당 유저의 레코드가 있는지 확인하고 없으면 생성
                    auto statsCheck = txn.exec_params(
                        "SELECT user_id FROM user_stats WHERE user_id = $1",
                        playerIds[i]
                    );
                    
                    if (statsCheck.empty()) {
                        txn.exec_params(
                            "INSERT INTO user_stats (user_id) VALUES ($1)",
                            playerIds[i]
                        );
                        spdlog::info("📊 새 통계 레코드 생성: 사용자 ID {}", playerIds[i]);
                    }
                    
                    // 통계 업데이트
                    txn.exec_params(
                        "UPDATE user_stats SET "
                        "total_games = total_games + 1, "
                        "wins = wins + CASE WHEN $2 THEN 1 ELSE 0 END, "
                        "losses = losses + CASE WHEN NOT $2 AND NOT $3 THEN 1 ELSE 0 END, "
                        "draws = draws + CASE WHEN $3 THEN 1 ELSE 0 END, "
                        "total_score = total_score + $4, "
                        "best_score = GREATEST(best_score, $4), "
                        "current_win_streak = CASE WHEN $2 THEN current_win_streak + 1 ELSE 0 END, "
                        "longest_win_streak = GREATEST(longest_win_streak, "
                            "CASE WHEN $2 THEN current_win_streak + 1 ELSE current_win_streak END), "
                        "last_played = CURRENT_TIMESTAMP, "
                        "updated_at = CURRENT_TIMESTAMP "
                        "WHERE user_id = $1",
                        playerIds[i], won, draw, score
                    );
                    
                    spdlog::info("📈 플레이어 {} 통계 업데이트: 점수={}, 승리={}", 
                               playerIds[i], score, won);
                }

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                spdlog::info("✅ 게임 결과 저장 완료");
                return true;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("saveGameResults 오류: {}", e.what());
                return false;
            }
        }

        // ========================================
        // 🔥 기타 필수 함수들 (간단 구현)
        // ========================================

        bool DatabaseManager::setUserActive(uint32_t userId, bool active) {
            if (!isInitialized_) return false;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                auto result = txn.exec_params(
                    "UPDATE users SET is_active = $1 WHERE user_id = $2",
                    active, userId
                );

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                return result.affected_rows() > 0;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("setUserActive 오류: {}", e.what());
                return false;
            }
        }


        // ========================================
        // 🔥 경험치 및 레벨 시스템
        // ========================================
        
        int DatabaseManager::getRequiredExpForLevel(int level) const {
            if (level <= 10) return level * 100;           // 1~10레벨: 선형 (100, 200, 300...)
            if (level <= 30) return 1000 + (level-10) * 150; // 11~30레벨: 중간 증가 (1150, 1300, 1450...)
            return 4000 + (level-30) * 200;                // 31+레벨: 큰 증가 (4200, 4400, 4600...)
        }
        
        int DatabaseManager::calculateExperienceGain(bool won, int score, bool completedGame) const {
            if (!completedGame) {
                return 0; // 게임을 완료하지 않으면 경험치 없음
            }
            
            int baseExp = 50;  // 기본 참여 경험치
            int winBonus = won ? 100 : 0;  // 승리 보너스
            int scoreBonus = score / 5;  // 점수 비례 보너스 (점수 5당 1 경험치)
            
            return baseExp + winBonus + scoreBonus;
        }
        
        bool DatabaseManager::updatePlayerExperience(uint32_t userId, int expGained) {
            if (!isInitialized_ || expGained <= 0) return false;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                // 현재 경험치와 레벨 조회
                auto currentStats = txn.exec_params(
                    "SELECT level, experience_points FROM user_stats WHERE user_id = $1",
                    userId
                );
                
                if (currentStats.empty()) {
                    // user_stats 레코드가 없으면 생성
                    txn.exec_params(
                        "INSERT INTO user_stats (user_id, experience_points) VALUES ($1, $2)",
                        userId, expGained
                    );
                    spdlog::info("📊 새 통계 레코드 생성 및 경험치 추가: 사용자 ID {}, 경험치 +{}", userId, expGained);
                } else {
                    int currentLevel = currentStats[0]["level"].as<int>();
                    int currentExp = currentStats[0]["experience_points"].as<int>();
                    int newExp = currentExp + expGained;
                    
                    // 경험치 업데이트
                    txn.exec_params(
                        "UPDATE user_stats SET experience_points = $1, updated_at = CURRENT_TIMESTAMP WHERE user_id = $2",
                        newExp, userId
                    );
                    
                    spdlog::info("📈 플레이어 {} 경험치 업데이트: {} -> {} (+{})", 
                               userId, currentExp, newExp, expGained);
                }

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                
                // 레벨업 체크 (경험치 업데이트 성공과 독립적으로 처리)
                checkAndProcessLevelUp(userId);
                
                // 경험치 업데이트는 성공
                return true;

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("updatePlayerExperience 오류: {}", e.what());
                return false;
            }
        }
        
        bool DatabaseManager::checkAndProcessLevelUp(uint32_t userId) {
            if (!isInitialized_) return false;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                // 현재 레벨과 경험치 조회
                auto result = txn.exec_params(
                    "SELECT level, experience_points FROM user_stats WHERE user_id = $1",
                    userId
                );
                
                if (result.empty()) {
                    txn.abort();
                    dbPool_->returnConnection(std::move(conn));
                    return false;
                }
                
                int currentLevel = result[0]["level"].as<int>();
                int currentExp = result[0]["experience_points"].as<int>();
                int newLevel = currentLevel;
                int remainingExp = currentExp;
                
                // 연속 레벨업 가능성 체크 (소모형)
                while (true) {
                    int requiredExp = getRequiredExpForLevel(newLevel + 1);
                    if (remainingExp >= requiredExp) {
                        remainingExp -= requiredExp;  // 경험치 소모
                        newLevel++;
                        spdlog::info("🎉 레벨업! 플레이어 {} : {} -> {} (소모: {}, 남은 경험치: {})", 
                                   userId, newLevel-1, newLevel, requiredExp, remainingExp);
                    } else {
                        break;
                    }
                }
                
                // 레벨이 변경되었다면 업데이트 (레벨과 남은 경험치 모두)
                if (newLevel > currentLevel) {
                    txn.exec_params(
                        "UPDATE user_stats SET level = $1, experience_points = $2, updated_at = CURRENT_TIMESTAMP WHERE user_id = $3",
                        newLevel, remainingExp, userId
                    );
                    
                    txn.commit();
                    dbPool_->returnConnection(std::move(conn));
                    
                    spdlog::info("✅ 플레이어 {} 레벨업 완료: {} -> {} (남은 경험치: {})", 
                               userId, currentLevel, newLevel, remainingExp);
                    return true;
                } else {
                    txn.abort();
                    dbPool_->returnConnection(std::move(conn));
                    return false;
                }

            }
            catch (const std::exception& e) {
                txn.abort();
                dbPool_->returnConnection(std::move(conn));
                spdlog::error("checkAndProcessLevelUp 오류: {}", e.what());
                return false;
            }
        }

        std::vector<UserAccount> DatabaseManager::getRanking(const std::string& orderBy, int limit, int offset) {
            return {}; // 임시 구현
        }

        std::vector<std::string> DatabaseManager::getOnlineUsers() {
            return {}; // 임시 구현
        }

        // ========================================
        // 사용자 설정 관리 구현
        // ========================================

        std::optional<UserSettings> DatabaseManager::getUserSettings(const std::string& userId) {
            auto conn = dbPool_->getConnection();
            if (!conn) {
                spdlog::error("Failed to get database connection for getUserSettings");
                return std::nullopt;
            }

            try {
                // userId를 string에서 int로 변환
                int userIdInt = std::stoi(userId);
                
                pqxx::work txn(*conn);
                
                // 사용자 설정 조회
                auto result = txn.exec_params(
                    "SELECT theme, language, game_invite_notifications, "
                    "friend_online_notifications, system_notifications, "
                    "bgm_mute, bgm_volume, effect_mute, effect_volume "
                    "FROM user_settings WHERE user_id = $1",
                    userIdInt
                );

                if (result.empty()) {
                    // 설정이 없으면 기본값으로 생성
                    UserSettings defaults = UserSettings::getDefaults();
                    txn.exec_params(
                        "INSERT INTO user_settings (user_id, theme, language, "
                        "game_invite_notifications, friend_online_notifications, "
                        "system_notifications, bgm_mute, bgm_volume, effect_mute, effect_volume, "
                        "created_at, updated_at) "
                        "VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)",
                        userIdInt, defaults.theme, defaults.language,
                        defaults.gameInviteNotifications, defaults.friendOnlineNotifications,
                        defaults.systemNotifications, defaults.bgmMute, defaults.bgmVolume,
                        defaults.effectMute, defaults.effectVolume
                    );
                    txn.commit();
                    
                    spdlog::info("Created default settings for user {}", userId);
                    return defaults;
                }

                // 기존 설정 반환
                auto row = result[0];
                UserSettings settings;
                settings.theme = row["theme"].as<std::string>();
                settings.language = row["language"].as<std::string>();
                settings.gameInviteNotifications = row["game_invite_notifications"].as<bool>();
                settings.friendOnlineNotifications = row["friend_online_notifications"].as<bool>();
                settings.systemNotifications = row["system_notifications"].as<bool>();
                settings.bgmMute = row["bgm_mute"].as<bool>();
                settings.bgmVolume = row["bgm_volume"].as<int>();
                settings.effectMute = row["effect_mute"].as<bool>();
                settings.effectVolume = row["effect_volume"].as<int>();

                txn.commit();
                dbPool_->returnConnection(std::move(conn));
                spdlog::debug("Retrieved settings for user {}", userId);
                return settings;

            } catch (const std::exception& e) {
                spdlog::error("Failed to get user settings for user {}: {}", userId, e.what());
                dbPool_->returnConnection(std::move(conn));
                return std::nullopt;
            }
        }

        bool DatabaseManager::updateUserSettings(const std::string& userId, const UserSettings& settings) {
            if (!settings.isValid()) {
                spdlog::error("Invalid settings provided for user {}", userId);
                return false;
            }

            auto conn = dbPool_->getConnection();
            if (!conn) {
                spdlog::error("Failed to get database connection for updateUserSettings");
                return false;
            }

            try {
                // userId를 string에서 int로 변환
                int userIdInt = std::stoi(userId);
                
                pqxx::work txn(*conn);
                
                // UPSERT (INSERT ... ON CONFLICT UPDATE)
                txn.exec_params(
                    "INSERT INTO user_settings (user_id, theme, language, "
                    "game_invite_notifications, friend_online_notifications, "
                    "system_notifications, bgm_mute, bgm_volume, effect_mute, effect_volume) "
                    "VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10) "
                    "ON CONFLICT (user_id) DO UPDATE SET "
                    "theme = EXCLUDED.theme, language = EXCLUDED.language, "
                    "game_invite_notifications = EXCLUDED.game_invite_notifications, "
                    "friend_online_notifications = EXCLUDED.friend_online_notifications, "
                    "system_notifications = EXCLUDED.system_notifications, "
                    "bgm_mute = EXCLUDED.bgm_mute, bgm_volume = EXCLUDED.bgm_volume, "
                    "effect_mute = EXCLUDED.effect_mute, effect_volume = EXCLUDED.effect_volume, "
                    "updated_at = CURRENT_TIMESTAMP",
                    userIdInt, settings.theme, settings.language,
                    settings.gameInviteNotifications, settings.friendOnlineNotifications,
                    settings.systemNotifications, settings.bgmMute, settings.bgmVolume,
                    settings.effectMute, settings.effectVolume
                );

                txn.commit();
                spdlog::info("Updated settings for user {}", userId);
                dbPool_->returnConnection(std::move(conn));
                return true;

            } catch (const std::exception& e) {
                spdlog::error("Failed to update user settings for user {}: {}", userId, e.what());
                dbPool_->returnConnection(std::move(conn));
                return false;
            }
        }

        bool DatabaseManager::deleteUserSettings(const std::string& userId) {
            auto conn = dbPool_->getConnection();
            if (!conn) {
                spdlog::error("Failed to get database connection for deleteUserSettings");
                return false;
            }

            try {
                // userId를 string에서 int로 변환
                int userIdInt = std::stoi(userId);
                
                pqxx::work txn(*conn);
                
                auto result = txn.exec_params(
                    "DELETE FROM user_settings WHERE user_id = $1",
                    userIdInt
                );

                txn.commit();
                spdlog::info("Deleted settings for user {} (affected rows: {})", userId, result.affected_rows());
                dbPool_->returnConnection(std::move(conn));
                return true;

            } catch (const std::exception& e) {
                spdlog::error("Failed to delete user settings for user {}: {}", userId, e.what());
                dbPool_->returnConnection(std::move(conn));
                return false;
            }
        }

        bool DatabaseManager::sendFriendRequest(uint32_t requesterId, uint32_t addresseeId) {
            return true; // 임시 구현
        }

        bool DatabaseManager::acceptFriendRequest(uint32_t requesterId, uint32_t addresseeId) {
            return true; // 임시 구현
        }

        std::vector<std::string> DatabaseManager::getFriends(uint32_t userId) {
            return {}; // 임시 구현
        }

        bool DatabaseManager::insertDummyData() {
            spdlog::info("Dummy data insertion - using schema defaults");
            return true;
        }

    } // namespace Server
} // namespace Blokus