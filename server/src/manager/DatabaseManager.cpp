#include "manager/DatabaseManager.h"
#include "manager/ConfigManager.h"
#include <spdlog/spdlog.h>

// pqxx 헤더 충돌 방지
#ifdef _WIN32
#include <winsock2.h>
#include <windows.h>
#endif

#include <pqxx/pqxx>
#include <thread>
#include <future>
#include <queue>
#include <condition_variable>

namespace Blokus {
    namespace Server {

        // ========================================
        // ConnectionPool 구현
        // ========================================
        class DatabaseManager::ConnectionPool {
        public:
            ConnectionPool(const std::string& connectionString, size_t poolSize)
                : m_connectionString(connectionString), m_maxPoolSize(poolSize) {

                spdlog::info("Creating database connection pool with {} connections", poolSize);

                // 초기 연결 생성
                for (size_t i = 0; i < poolSize; ++i) {
                    try {
                        auto conn = std::make_unique<pqxx::connection>(connectionString);
                        if (conn->is_open()) {
                            m_availableConnections.push(std::move(conn));
                            m_currentPoolSize++;
                            spdlog::debug("Created database connection {}/{}", i + 1, poolSize);
                        }
                        else {
                            throw std::runtime_error("Connection failed to open");
                        }
                    }
                    catch (const std::exception& e) {
                        spdlog::error("Failed to create database connection {}: {}", i + 1, e.what());
                        throw;
                    }
                }

                spdlog::info("Database connection pool initialized with {} connections", m_currentPoolSize.load());
            }

            ~ConnectionPool() {
                std::lock_guard<std::mutex> lock(m_poolMutex);
                while (!m_availableConnections.empty()) {
                    m_availableConnections.pop();
                }
                spdlog::info("Database connection pool destroyed");
            }

            std::unique_ptr<pqxx::connection> acquire() {
                std::unique_lock<std::mutex> lock(m_poolMutex);

                // 사용 가능한 연결이 있을 때까지 대기 (최대 5초)
                if (!m_poolCondition.wait_for(lock, std::chrono::seconds(5),
                    [this] { return !m_availableConnections.empty(); })) {
                    throw std::runtime_error("Database connection pool timeout");
                }

                auto conn = std::move(m_availableConnections.front());
                m_availableConnections.pop();

                // 연결 상태 확인
                if (!conn->is_open()) {
                    try {
                        conn = std::make_unique<pqxx::connection>(m_connectionString);
                        spdlog::debug("Recreated database connection");
                    }
                    catch (const std::exception& e) {
                        spdlog::error("Failed to recreate database connection: {}", e.what());
                        throw;
                    }
                }

                return conn;
            }

            void release(std::unique_ptr<pqxx::connection> conn) {
                if (conn && conn->is_open()) {
                    std::lock_guard<std::mutex> lock(m_poolMutex);
                    m_availableConnections.push(std::move(conn));
                    m_poolCondition.notify_one();
                }
                else {
                    spdlog::warn("Attempting to release invalid database connection");
                }
            }

        private:
            std::string m_connectionString;
            std::queue<std::unique_ptr<pqxx::connection>> m_availableConnections;
            std::mutex m_poolMutex;
            std::condition_variable m_poolCondition;
            size_t m_maxPoolSize;
            std::atomic<size_t> m_currentPoolSize{ 0 };
        };

        // ========================================
        // DatabaseManager 구현
        // ========================================
        DatabaseManager::DatabaseManager() {
        }

        DatabaseManager::~DatabaseManager() {
            shutdown();
        }

        bool DatabaseManager::initialize() {
            if (m_isInitialized.load()) {
                spdlog::warn("DatabaseManager already initialized");
                return true;
            }

            try {
                spdlog::info("Initializing database connection...");
                spdlog::debug("Database configuration:");
                spdlog::debug("  Host: {}", ConfigManager::dbHost);
                spdlog::debug("  Port: {}", ConfigManager::dbPort);
                spdlog::debug("  Database: {}", ConfigManager::dbName);
                spdlog::debug("  User: {}", ConfigManager::dbUser);
                spdlog::debug("  Pool Size: {}", ConfigManager::dbPoolSize);

                // 연결 문자열 직접 사용
                m_connectionString = ConfigManager::dbConnectionString;
                spdlog::debug("Connection string: {}", m_connectionString);

                // 연결 풀 생성
                spdlog::info("Creating database connection pool...");
                m_connectionPool = std::make_unique<ConnectionPool>(m_connectionString, ConfigManager::dbPoolSize);

                // 연결 테스트
                spdlog::info("Testing database connection...");
                auto testConn = m_connectionPool->acquire();
                if (!testConn->is_open()) {
                    spdlog::error("Failed to establish database connection");
                    return false;
                }

                spdlog::info("Database connection test successful");
                m_connectionPool->release(std::move(testConn));

                // 더미 데이터 삽입 (개발용)
                if (ConfigManager::enableSqlLogging) {
                    spdlog::info("Inserting dummy data for development...");
                    if (!insertDummyData()) {
                        spdlog::warn("Failed to insert dummy data (may already exist)");
                    }
                }

                m_isInitialized = true;
                spdlog::info("✅ DatabaseManager initialized successfully");
                return true;

            }
            catch (const std::exception& e) {
                spdlog::error("❌ Failed to initialize DatabaseManager: {}", e.what());
                spdlog::error("Please check:");
                spdlog::error("  1. PostgreSQL server is running");
                spdlog::error("  2. Database credentials are correct");
                spdlog::error("  3. Database '{}' exists", ConfigManager::dbName);
                spdlog::error("  4. Network connectivity to database server");
                return false;
            }
        }

        void DatabaseManager::shutdown() {
            if (!m_isInitialized.load()) return;

            spdlog::info("Shutting down DatabaseManager...");

            // 대기 중인 비동기 작업 완료 대기
            for (auto& future : m_pendingTasks) {
                if (future.valid()) {
                    future.wait();
                }
            }
            m_pendingTasks.clear();

            // 연결 풀 해제
            m_connectionPool.reset();

            m_isInitialized = false;
            spdlog::info("DatabaseManager shutdown complete");
        }

        bool DatabaseManager::insertDummyData() {
            try {
                auto conn = m_connectionPool->acquire();
                pqxx::work txn(*conn);

                // 더미 사용자 데이터
                const std::vector<std::tuple<std::string, std::string, std::string>> dummyUsers = {
                    {"testuser1", "$2b$12$dummy.hash.for.password1", "test1@example.com"},
                    {"testuser2", "$2b$12$dummy.hash.for.password2", "test2@example.com"},
                    {"player123", "$2b$12$dummy.hash.for.password3", "player@example.com"},
                    {"gamer456", "$2b$12$dummy.hash.for.password4", "gamer@example.com"},
                    {"blokus789", "$2b$12$dummy.hash.for.password5", "blokus@example.com"}
                };

                // 사용자 삽입 (중복 시 무시)
                for (const auto& [username, passwordHash, email] : dummyUsers) {
                    try {
                        auto result = txn.exec_params(
                            "INSERT INTO users (username, password_hash, email) VALUES ($1, $2, $3) "
                            "ON CONFLICT (username) DO NOTHING RETURNING user_id",
                            username, passwordHash, email
                        );

                        if (!result.empty()) {
                            int userId = result[0][0].as<int>();

                            // 게임 통계 생성 (유효한 값으로)
                            int totalGames = std::rand() % 50;
                            int wins = std::rand() % (totalGames + 1);  // wins <= totalGames
                            int losses = std::rand() % (totalGames - wins + 1);  // wins + losses <= totalGames
                            int rating = 1200 + (std::rand() % 600) - 300;  // 900 ~ 1800

                            // 사용자 통계 삽입
                            txn.exec_params(
                                "INSERT INTO user_stats (user_id, total_games, wins, losses, rating) "
                                "VALUES ($1, $2, $3, $4, $5) ON CONFLICT (user_id) DO NOTHING",
                                userId, totalGames, wins, losses, rating
                            );

                            spdlog::debug("Inserted dummy user: {} (ID: {}, Games: {}, W/L: {}/{})",
                                username, userId, totalGames, wins, losses);
                        }
                    }
                    catch (const std::exception& e) {
                        spdlog::debug("User {} already exists or error: {}", username, e.what());
                    }
                }

                txn.commit();
                m_connectionPool->release(std::move(conn));

                spdlog::info("Dummy data insertion completed");
                return true;

            }
            catch (const std::exception& e) {
                spdlog::error("Failed to insert dummy data: {}", e.what());
                return false;
            }
        }

        // ========================================
        // 비동기 쿼리 실행 헬퍼
        // ========================================
        template<typename T>
        std::future<T> DatabaseManager::executeQuery(std::function<T(pqxx::connection&)> queryFunc) {
            return std::async(std::launch::async, [this, queryFunc]() -> T {
                if (!m_isInitialized.load()) {
                    throw std::runtime_error("DatabaseManager not initialized");
                }

                auto conn = m_connectionPool->acquire();
                try {
                    T result = queryFunc(*conn);
                    m_connectionPool->release(std::move(conn));
                    return result;
                }
                catch (...) {
                    m_connectionPool->release(std::move(conn));
                    throw;
                }
                });
        }

        // ========================================
        // 사용자 관리 구현
        // ========================================
        std::future<bool> DatabaseManager::createUser(const std::string& username,
            const std::string& email,
            const std::string& passwordHash) {
            return executeQuery<bool>([username, email, passwordHash](pqxx::connection& conn) -> bool {
                pqxx::work txn(conn);

                auto result = txn.exec_params(
                    "INSERT INTO users (username, password_hash, email) VALUES ($1, $2, $3) RETURNING user_id",
                    username, passwordHash, email
                );

                if (!result.empty()) {
                    int userId = result[0][0].as<int>();

                    // 사용자 통계 테이블에도 초기 데이터 삽입
                    txn.exec_params(
                        "INSERT INTO user_stats (user_id) VALUES ($1)",
                        userId
                    );

                    txn.commit();
                    spdlog::info("Created new user: {} (ID: {})", username, userId);
                    return true;
                }

                return false;
                });
        }

        std::future<std::optional<UserAccount>> DatabaseManager::getUserByUsername(const std::string& username) {
            return executeQuery<std::optional<UserAccount>>([username](pqxx::connection& conn) -> std::optional<UserAccount> {
                pqxx::work txn(conn);

                auto result = txn.exec_params(
                    "SELECT u.user_id, u.username, u.email, u.password_hash, u.created_at, "
                    "       s.total_games, s.wins, s.losses "
                    "FROM users u "
                    "LEFT JOIN user_stats s ON u.user_id = s.user_id "
                    "WHERE u.username = $1",
                    username
                );

                if (result.empty()) {
                    return std::nullopt;
                }

                auto row = result[0];
                UserAccount account;
                account.userId = row[0].as<uint32_t>();
                account.username = row[1].as<std::string>();
                account.email = row[2].as<std::string>();
                account.passwordHash = row[3].as<std::string>();
                account.createdAt = std::chrono::system_clock::from_time_t(row[4].as<time_t>());
                account.totalGames = row[5].is_null() ? 0 : row[5].as<int>();
                account.wins = row[6].is_null() ? 0 : row[6].as<int>();
                account.losses = row[7].is_null() ? 0 : row[7].as<int>();
                account.isActive = true;

                txn.commit();
                return account;
                });
        }

        std::future<DatabaseManager::DatabaseStats> DatabaseManager::getStats() {
            return executeQuery<DatabaseStats>([](pqxx::connection& conn) -> DatabaseStats {
                pqxx::work txn(conn);

                DatabaseStats stats;

                // 전체 사용자 수
                auto result = txn.exec("SELECT COUNT(*) FROM users");
                stats.totalUsers = result[0][0].as<int>();

                // 활성 사용자 수 (최근 30일)
                result = txn.exec(
                    "SELECT COUNT(*) FROM users WHERE created_at > NOW() - INTERVAL '30 days'"
                );
                stats.activeUsers = result[0][0].as<int>();

                // 전체 게임 수
                result = txn.exec("SELECT COALESCE(SUM(total_games), 0) FROM user_stats");
                stats.totalGames = result[0][0].as<int>();

                // 이번 주 게임 수 (임시로 0)
                stats.gamesThisWeek = 0;

                // 평균 게임 시간 (임시로 0)
                stats.averageGameDuration = 0.0;

                txn.commit();
                return stats;
                });
        }

    } // namespace Server
} // namespace Blokus