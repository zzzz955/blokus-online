#include <iostream>
#include <memory>
#include <ctime>
#include <spdlog/spdlog.h>
#include "manager/ConfigManager.h"
#include "manager/DatabaseManager.h"

using namespace Blokus::Server;

int main() {
    spdlog::info("🚀 블로커스 온라인 서버 시작");

    try {
        // 1. 설정 로드 (한 줄!)
        ConfigManager::initialize();

        // 2. 설정 검증
        if (!ConfigManager::validate()) {
            spdlog::error("Configuration validation failed");
            spdlog::error("Please check your environment variables or run with run_dev.bat");
            return 1;
        }

        // 3. 서버 설정 출력
        spdlog::info("🔧 Server Configuration:");
        spdlog::info("  Port: {}", ConfigManager::serverPort);
        spdlog::info("  Max Connections: {}", ConfigManager::maxClients);
        spdlog::info("  Thread Pool Size: {}", ConfigManager::threadPoolSize);

        spdlog::info("🗄️ Database Configuration:");
        spdlog::info("  Host: {}:{}", ConfigManager::dbHost, ConfigManager::dbPort);
        spdlog::info("  Database: {}", ConfigManager::dbName);
        spdlog::info("  User: {}", ConfigManager::dbUser);
        spdlog::info("  Pool Size: {}", ConfigManager::dbPoolSize);

        // 디버그 모드에서만 전체 설정 출력
        if (ConfigManager::debugMode) {
            ConfigManager::printConfig();
        }

        // 4. 데이터베이스 초기화
        spdlog::info("💡 Make sure PostgreSQL server is running on {}:{}",
            ConfigManager::dbHost, ConfigManager::dbPort);

        // 데이터베이스 매니저를 포인터로 생성 (수명 관리)
        auto dbManager = std::make_unique<DatabaseManager>();

        if (!dbManager->initialize()) {
            spdlog::error("Failed to initialize database");
            return 1;
        }

        // 5. 데이터베이스 연결 테스트
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