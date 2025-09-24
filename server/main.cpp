// main.cpp - 블로블로 서버 테스트

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
        spdlog::info("서버 종료 수행");
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
        
        // ========================================
        // 2. 설정 및 환경 초기화 (로그 레벨 설정 전에 먼저)
        // ========================================
        Blokus::Server::ConfigManager::initialize();
        
        // LOG_LEVEL 환경변수에 따른 로그 레벨 설정
        std::string logLevel = Blokus::Server::ConfigManager::logLevel;
        if (logLevel == "debug") {
            spdlog::set_level(spdlog::level::debug);
        } else if (logLevel == "info") {
            spdlog::set_level(spdlog::level::info);
        } else if (logLevel == "warn") {
            spdlog::set_level(spdlog::level::warn);
        } else if (logLevel == "error") {
            spdlog::set_level(spdlog::level::err);
        } else {
            spdlog::set_level(spdlog::level::info);  // 기본값
        }

        spdlog::info("블로블로 서버 v{}", Blokus::Server::ConfigManager::serverVersion);
        spdlog::info("========================================");
        spdlog::info("현재 로그 레벨: {}", logLevel);
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
        spdlog::info("서버 리소스 정리 중...");
        g_server.reset();

        spdlog::info("서버 종료 성공");
        spdlog::info("========================================");

    }
    catch (const std::exception& e) {
        // ========================================
        // 예외 처리
        // ========================================
        spdlog::error("서버 실행 에러: {}", e.what());

        if (g_server) {
            g_server->stop();
            g_server.reset();
        }

        return 1;
    }

    return 0;
}