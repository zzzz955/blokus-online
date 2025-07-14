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
                    "SELECT u.user_id, u.username, u.password_hash, u.display_name, u.avatar_url, "
                    "       COALESCE(s.total_games, 0), COALESCE(s.wins, 0), COALESCE(s.losses, 0), "
                    "       COALESCE(s.draws, 0), COALESCE(s.rating, 1200), COALESCE(s.level, 1), "
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
                user.userId = result[0]["user_id"].as<uint32_t>();
                user.username = result[0]["username"].as<std::string>();
                user.passwordHash = result[0]["password_hash"].as<std::string>();
                user.displayName = result[0]["display_name"].is_null() ? "" : result[0]["display_name"].as<std::string>();
                user.avatarUrl = result[0]["avatar_url"].is_null() ? "" : result[0]["avatar_url"].as<std::string>();
                user.totalGames = result[0][5].as<int>();
                user.wins = result[0][6].as<int>();
                user.losses = result[0][7].as<int>();
                user.draws = result[0][8].as<int>();
                user.rating = result[0][9].as<int>();
                user.level = result[0][10].as<int>();
                user.isActive = result[0]["is_active"].as<bool>();

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

        std::optional<UserAccount> DatabaseManager::getUserById(uint32_t userId) {
            if (!isInitialized_) return std::nullopt;

            auto conn = dbPool_->getConnection();
            pqxx::work txn(*conn);
            try {
                auto result = txn.exec_params(
                    "SELECT u.user_id, u.username, u.password_hash, u.display_name, u.avatar_url, "
                    "       COALESCE(s.total_games, 0), COALESCE(s.wins, 0), COALESCE(s.losses, 0), "
                    "       COALESCE(s.draws, 0), COALESCE(s.rating, 1200), COALESCE(s.level, 1), "
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
                user.passwordHash = result[0]["password_hash"].as<std::string>();
                user.displayName = result[0]["display_name"].is_null() ? "" : result[0]["display_name"].as<std::string>();
                user.avatarUrl = result[0]["avatar_url"].is_null() ? "" : result[0]["avatar_url"].as<std::string>();
                user.totalGames = result[0][5].as<int>();
                user.wins = result[0][6].as<int>();
                user.losses = result[0][7].as<int>();
                user.draws = result[0][8].as<int>();
                user.rating = result[0][9].as<int>();
                user.level = result[0][10].as<int>();
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
                    "SELECT u.user_id, u.username, u.password_hash, u.display_name, u.avatar_url, "
                    "       COALESCE(s.total_games, 0), COALESCE(s.wins, 0), COALESCE(s.losses, 0), "
                    "       COALESCE(s.draws, 0), COALESCE(s.rating, 1200), COALESCE(s.level, 1), "
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
                user.passwordHash = result[0]["password_hash"].as<std::string>();
                user.displayName = result[0]["display_name"].is_null() ? "" : result[0]["display_name"].as<std::string>();
                user.avatarUrl = result[0]["avatar_url"].is_null() ? "" : result[0]["avatar_url"].as<std::string>();
                user.totalGames = result[0][5].as<int>();
                user.wins = result[0][6].as<int>();
                user.losses = result[0][7].as<int>();
                user.draws = result[0][8].as<int>();
                user.rating = result[0][9].as<int>();
                user.level = result[0][10].as<int>();
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
                // PostgreSQL 함수가 있다면 사용, 없다면 직접 업데이트
                txn.exec_params(
                    "SELECT update_game_stats($1, $2, $3, $4)",
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

        bool DatabaseManager::updateUserRating(uint32_t userId, int opponentRating, bool won, bool draw) {
            // 간단한 ELO 계산 또는 PostgreSQL 함수 호출
            return true; // 임시 구현
        }

        std::vector<UserAccount> DatabaseManager::getRanking(const std::string& orderBy, int limit, int offset) {
            return {}; // 임시 구현
        }

        std::vector<std::string> DatabaseManager::getOnlineUsers() {
            return {}; // 임시 구현
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