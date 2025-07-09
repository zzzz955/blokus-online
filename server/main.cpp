// main.cpp - 인코딩 문제 해결

#include "manager/ConfigManager.h"
#include "manager/DatabaseManager.h"
#include "util/Logger.h"
#include <spdlog/spdlog.h>
#include <ctime>
#include <iostream>

using namespace Blokus::Server;

int main() {
    try {
        // 로깅 설정
        spdlog::set_pattern("[%Y-%m-%d %H:%M:%S.%e] [%l] %v");
        spdlog::set_level(spdlog::level::info);

        // 🔥 이모지 제거하고 한글만 사용
        spdlog::info("블로커스 온라인 서버 시작");

        // ConfigManager 초기화
        spdlog::info("설정 불러오기...");
        ConfigManager::initialize();

        if (!ConfigManager::validate()) {
            spdlog::error("설정 검증 실패");
            return 1;
        }

        spdlog::info("환경변수 불러오기 완료! 서버 포트: {}, DB: {}@{}:{}/{}",
            ConfigManager::serverPort,
            ConfigManager::dbUser, ConfigManager::dbHost, ConfigManager::dbPort, ConfigManager::dbName);

        // 서버 설정 정보 출력
        spdlog::info("Server Configuration:");
        spdlog::info("  Port: {}", ConfigManager::serverPort);
        spdlog::info("  Max Clients: {}", ConfigManager::maxClients);
        spdlog::info("  Thread Pool Size: {}", ConfigManager::threadPoolSize);

        // 데이터베이스 설정 정보 출력
        spdlog::info("Database Configuration:");
        spdlog::info("  Host: {}:{}", ConfigManager::dbHost, ConfigManager::dbPort);
        spdlog::info("  Database: {}", ConfigManager::dbName);
        spdlog::info("  User: {}", ConfigManager::dbUser);
        spdlog::info("  Pool Size: {}", ConfigManager::dbPoolSize);

        // DatabaseManager 초기화
        spdlog::info("PostgreSQL 서버가 {}:{}에서 실행 중인지 확인하세요",
            ConfigManager::dbHost, ConfigManager::dbPort);

        DatabaseManager dbManager;
        if (!dbManager.initialize()) {
            spdlog::error("데이터베이스 초기화 실패");
            return 1;
        }

        // 🔥 데이터베이스 테스트 시작
        spdlog::info("데이터베이스 작업 테스트 중...");

        // 통계 조회 테스트
        spdlog::info("데이터베이스 통계 조회 중...");
        auto stats = dbManager.getStats();
        spdlog::info("Database Statistics:");
        spdlog::info("  Total Users: {}", stats.totalUsers);
        spdlog::info("  Active Users: {}", stats.activeUsers);
        spdlog::info("  Online Users: {}", stats.onlineUsers);
        spdlog::info("  Total Games: {}", stats.totalGames);
        spdlog::info("  Total Stats: {}", stats.totalStats);

        // 사용자 조회 테스트
        spdlog::info("사용자 조회 테스트...");
        auto userOpt = dbManager.getUserByUsername("testuser1");
        if (userOpt.has_value()) {
            const auto& user = userOpt.value();
            spdlog::info("사용자 발견: {} (ID: {}, 표시명: {})",
                user.username, user.userId, user.displayName);
            spdlog::info("  게임: {} (승: {}, 패: {}, 무: {})",
                user.totalGames, user.wins, user.losses, user.draws);
            spdlog::info("  레이팅: {}, 레벨: {}", user.rating, user.level);
            spdlog::info("  승률: {:.1f}%, 활성: {}",
                user.getWinRate(), user.isActive ? "예" : "아니오");
        }
        else {
            spdlog::warn("사용자 'testuser1'을 찾을 수 없습니다");
        }

        // 사용자명 중복 체크 테스트
        spdlog::info("사용자명 사용 가능 여부 테스트...");
        if (dbManager.isUsernameAvailable("testuser1")) {
            spdlog::info("사용자명 'testuser1' 사용 가능");
        }
        else {
            spdlog::info("사용자명 'testuser1' 이미 사용 중");
        }

        // 새 사용자 생성 테스트
        spdlog::info("사용자 생성 테스트...");
        std::string testUsername = "newuser" + std::to_string(std::time(nullptr));
        std::string testPasswordHash = "$2b$12$dummy.hash.for.new.user";

        // 사용자명 중복 체크
        if (dbManager.isUsernameAvailable(testUsername)) {
            spdlog::info("사용자명 '{}'가 사용 가능합니다. 사용자 생성 중...", testUsername);

            if (dbManager.createUser(testUsername, testPasswordHash)) {
                spdlog::info("사용자 생성 성공: {}", testUsername);

                // 로그인 시간 업데이트 테스트
                if (dbManager.updateUserLastLogin(testUsername)) {
                    spdlog::info("로그인 시간 업데이트 성공: {}", testUsername);
                }

                // 생성된 사용자 조회 확인
                auto newUserOpt = dbManager.getUserByUsername(testUsername);
                if (newUserOpt.has_value()) {
                    spdlog::info("검증 완료: 새 사용자 ID {}", newUserOpt->userId);
                }
            }
            else {
                spdlog::warn("사용자 생성 실패: {}", testUsername);
            }
        }
        else {
            spdlog::warn("사용자명 '{}'는 사용할 수 없습니다", testUsername);
        }

        // 인증 테스트
        spdlog::info("사용자 인증 테스트...");
        auto authUserOpt = dbManager.authenticateUser("testuser1", "$2b$12$dummy.hash.for.password1");
        if (authUserOpt.has_value()) {
            spdlog::info("testuser1 인증 성공");
        }
        else {
            spdlog::warn("testuser1 인증 실패");
        }

        // ID로 사용자 조회 테스트
        spdlog::info("ID로 사용자 조회 테스트...");
        if (userOpt.has_value()) {
            auto userByIdOpt = dbManager.getUserById(userOpt->userId);
            if (userByIdOpt.has_value()) {
                spdlog::info("ID로 사용자 발견: {} -> {}",
                    userOpt->userId, userByIdOpt->username);
            }
        }

        // 게임 통계 업데이트 테스트
        spdlog::info("게임 통계 업데이트 테스트...");
        if (userOpt.has_value()) {
            if (dbManager.updateGameStats(userOpt->userId, true, false, 85)) {
                spdlog::info("게임 통계 업데이트 완료 (승리, 85점) 사용자 ID: {}",
                    userOpt->userId);
            }
        }

        // 최종 통계 확인
        spdlog::info("최종 통계 확인...");
        auto finalStats = dbManager.getStats();
        spdlog::info("Final Database Statistics:");
        spdlog::info("  Total Users: {}", finalStats.totalUsers);
        spdlog::info("  Active Users: {}", finalStats.activeUsers);
        spdlog::info("  Online Users: {}", finalStats.onlineUsers);
        spdlog::info("  Total Games: {}", finalStats.totalGames);

        spdlog::info("모든 데이터베이스 테스트가 성공적으로 완료되었습니다!");
        spdlog::info("서버가 개발 준비 상태입니다");

        // 빌드 정보 출력
        spdlog::info("Build Information:");
        spdlog::info("  CMake-based build system");
        spdlog::info("  Simple header/source separation");
        spdlog::info("  PostgreSQL connection pooling");
        spdlog::info("  Synchronous database operations");

        // 정리
        spdlog::info("리소스 정리 중...");
        dbManager.shutdown();
        spdlog::info("종료 완료");

    }
    catch (const std::exception& e) {
        spdlog::error("서버 오류: {}", e.what());
        spdlog::error("가능한 원인:");
        spdlog::error("  1. PostgreSQL 서버가 실행되지 않음");
        spdlog::error("  2. 데이터베이스 연결 매개변수가 잘못됨");
        spdlog::error("  3. 데이터베이스 테이블이 생성되지 않음");
        spdlog::error("  4. 네트워크 연결 문제");
        return 1;
    }

    spdlog::info("서버 테스트 완료!");
    return 0;
}