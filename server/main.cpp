#include <iostream>
#include <memory>
#include <ctime>
#include <spdlog/spdlog.h>
#include "manager/ConfigManager.h"
#include "manager/DatabaseManager.h"

#ifdef _WIN32
#include <windows.h>
#else
#include <unistd.h>
#include <limits.h>
#endif

using namespace Blokus::Server;

int main() {
    // 디버그 로그 레벨 설정
    spdlog::set_level(spdlog::level::debug);

    spdlog::info("🚀 블로커스 온라인 서버 시작");

    try {
        // 1. 설정 관리자 초기화 (환경변수 우선)
        auto& config = ConfigManager::getInstance();

        // 환경변수를 먼저 시도하고, 없으면 .env 파일 fallback
        config.initialize("");  // 빈 문자열 = 환경변수만 사용

        // 설정 검증
        if (!config.validateConfig()) {
            spdlog::error("Configuration validation failed");
            spdlog::error("Please check your environment variables or run with run_dev.bat");
            return 1;
        }

        // 디버그 모드에서만 설정 출력
        if (config.getBool("DEBUG_MODE", false)) {
            spdlog::info("=== Configuration Source: Environment Variables ===");
            config.printLoadedConfig();
        }

        // 2. 구조화된 설정 출력
        const auto& serverConfig = config.getServerConfig();
        const auto& dbConfig = config.getDatabaseConfig();
        const auto& securityConfig = config.getSecurityConfig();

        spdlog::info("🔧 Server Configuration:");
        spdlog::info("  Port: {}", serverConfig.port);
        spdlog::info("  Max Connections: {}", serverConfig.maxConnections);
        spdlog::info("  Thread Pool Size: {}", serverConfig.threadPoolSize);

        spdlog::info("🗄️ Database Configuration:");
        spdlog::info("  Host: {}:{}", dbConfig.host, dbConfig.port);
        spdlog::info("  Database: {}", dbConfig.database);
        spdlog::info("  User: {}", dbConfig.user);
        spdlog::info("  Pool Size: {}", dbConfig.poolSize);

        spdlog::info("🔐 Security Configuration:");
        spdlog::info("  Session Timeout: {} hours", securityConfig.sessionTimeout.count());
        spdlog::info("  Min Password Length: {}", securityConfig.minPasswordLength);

        // 3. 데이터베이스 초기화
        spdlog::info("Initializing database connection...");
        spdlog::info("💡 Make sure PostgreSQL server is running on {}:{}",
            config.getString("DB_HOST", "localhost"),
            config.getInt("DB_PORT", 5432));

        // 데이터베이스 매니저를 포인터로 생성 (수명 관리)
        auto dbManager = std::make_unique<DatabaseManager>();

        if (!dbManager->initialize()) {
            spdlog::error("Failed to initialize database");
            return 1;
        }

        // 4. 데이터베이스 연결 테스트
        spdlog::info("Testing database operations...");

        // 통계 조회 테스트
        auto statsTask = dbManager->getStats();
        auto stats = statsTask.get();

        spdlog::info("📊 Database Statistics:");
        spdlog::info("  Total Users: {}", stats.totalUsers);
        spdlog::info("  Active Users: {}", stats.activeUsers);
        spdlog::info("  Total Games: {}", stats.totalGames);

        // 사용자 조회 테스트
        spdlog::info("Testing user lookup...");
        auto userTask = dbManager->getUserByUsername("testuser1");
        auto userOpt = userTask.get();

        if (userOpt.has_value()) {
            const auto& user = userOpt.value();
            spdlog::info("👤 Found user: {} (ID: {}, Email: {})",
                user.username, user.userId, user.email);
            spdlog::info("  Games: {} (Wins: {}, Losses: {})",
                user.totalGames, user.wins, user.losses);
        }
        else {
            spdlog::warn("User 'testuser1' not found");
        }

        // 새 사용자 생성 테스트
        spdlog::info("Testing user creation...");
        std::string testUsername = "newuser" + std::to_string(std::time(nullptr));
        std::string testEmail = testUsername + "@test.com";

        auto createTask = dbManager->createUser(testUsername, testEmail, "$2b$12$dummy.hash.for.new.user");
        bool created = createTask.get();

        if (created) {
            spdlog::info("✅ Successfully created user: {}", testUsername);
        }
        else {
            spdlog::warn("Failed to create user: {}", testUsername);
        }

        spdlog::info("🎉 Database tests completed successfully!");
        spdlog::info("🔧 Server is ready for development");

        // 명시적 정리 (선택적)
        dbManager->shutdown();

    }
    catch (const std::exception& e) {
        spdlog::error("❌ Server error: {}", e.what());
        return 1;
    }

    return 0;
}