// main.cpp - 블로커스 온라인 서버 테스트

#include "GameServer.h"
#include "ConfigManager.h"
#include "DatabaseManager.h"
#include <spdlog/spdlog.h>
#include <iostream>
#include <csignal>
#include <memory>

#ifdef _WIN32
#include <windows.h>
#include <io.h>
#include <fcntl.h>
#endif

// 전역 서버 인스턴스 (시그널 핸들러에서 사용)
std::unique_ptr<Blokus::Server::GameServer> g_server;

// 시그널 핸들러 (Ctrl+C 처리)
void signalHandler(int signal) {
    if (signal == SIGINT || signal == SIGTERM) {
        spdlog::info("Shutdown signal received. Stopping server safely...");
        if (g_server) {
            g_server->stop();
        }
    }
}

int main() {
    try {
        // ========================================
        // 1. 콘솔 인코딩 및 로깅 시스템 초기화
        // ========================================

#ifdef _WIN32
        // Windows 콘솔 UTF-8 설정
        SetConsoleOutputCP(CP_UTF8);
        SetConsoleCP(CP_UTF8);

        // 콘솔 모드 설정 (이모지 지원)
        HANDLE hOut = GetStdHandle(STD_OUTPUT_HANDLE);
        DWORD dwMode = 0;
        GetConsoleMode(hOut, &dwMode);
        dwMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        SetConsoleMode(hOut, dwMode);
#endif
        spdlog::set_pattern("[%Y-%m-%d %H:%M:%S] [%l] [%t] %v");
        spdlog::set_level(spdlog::level::info);  // 개발용 상세 로그

        spdlog::info("블로커스 온라인 서버 v1.0.0");
        spdlog::info("========================================");

        // ========================================
        // 2. 설정 및 환경 초기화
        // ========================================
        spdlog::info("설정 및 환경 초기화 진행 중...");

        Blokus::Server::ConfigManager::initialize();
        if (!Blokus::Server::ConfigManager::validate()) {
            spdlog::error("환경 초기화 실패");
            return 1;
        }

        // 설정 정보 출력
        spdlog::info("[서버 초기화]");
        spdlog::info("  서버 포트: {}", Blokus::Server::ConfigManager::serverPort);
        spdlog::info("  최대 세션: {}", Blokus::Server::ConfigManager::maxClients);
        spdlog::info("  스레드풀 크기: {}", Blokus::Server::ConfigManager::threadPoolSize);
        spdlog::info("  디버그 모드 여부: {}", Blokus::Server::ConfigManager::debugMode ? "ON" : "OFF");

        // ========================================
        // 3. 게임 서버 생성 및 실행
        // ========================================
        spdlog::info("게임 서버 실행 중...");

        g_server = std::make_unique<Blokus::Server::GameServer>();

        // 시그널 핸들러 등록 (Ctrl+C 처리)
        std::signal(SIGINT, signalHandler);
        std::signal(SIGTERM, signalHandler);

        // run()이 모든 초기화를 수행하고 실행함
        g_server->run();

        // ========================================
        // 5. 정리 및 종료
        // ========================================
        spdlog::info("Cleaning up server resources...");
        g_server.reset();

        spdlog::info("Server shutdown complete");
        spdlog::info("========================================");

    }
    catch (const std::exception& e) {
        // ========================================
        // 예외 처리
        // ========================================
        spdlog::error("Server execution error: {}", e.what());
        spdlog::error("Possible causes:");
        spdlog::error("  1. Port {} already in use", Blokus::Server::ConfigManager::serverPort);
        spdlog::error("  2. Insufficient network permissions");
        spdlog::error("  3. Configuration file error");
        spdlog::error("  4. Missing required libraries");

        if (g_server) {
            g_server->stop();
            g_server.reset();
        }

        return 1;
    }

    return 0;
}

// ========================================
// 개발자 노트:
// 
// 🎯 테스트 시나리오:
// 1. 서버 시작 → "서버가 실행 중입니다" 메시지 확인
// 2. telnet localhost 7777 → 연결 성공 확인  
// 3. "ping" 입력 → "pong" 응답 확인
// 4. "auth:test:1234" → "AUTH_SUCCESS:test" 응답 확인
// 5. "room:list" → 방 목록 응답 확인
// 6. "chat:hello" → 채팅 브로드캐스트 확인
// 7. Ctrl+C → 안전한 종료 확인
//
// 🐛 디버깅 팁:
// - 로그 레벨을 debug로 설정해서 상세 정보 확인
// - 다중 클라이언트 연결해서 브로드캐스트 테스트
// - 네트워크 도구로 연결 상태 모니터링
// 
// ========================================