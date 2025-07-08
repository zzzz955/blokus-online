#include <iostream>
#include <string>
#include <memory>
#include <thread>
#include <signal.h>

#include <spdlog/spdlog.h>
#include <spdlog/sinks/stdout_color_sinks.h>
#include <spdlog/sinks/rotating_file_sink.h>

#include "server/GameServer.h"
#include "server/ServerTypes.h"
#include "common/Types.h"

// 전역 서버 인스턴스 (시그널 핸들링용)
std::unique_ptr<Blokus::Server::GameServer> g_server;

// 시그널 핸들러 (Ctrl+C 등으로 서버 종료 시)
void signalHandler(int signal) {
    spdlog::info("시그널 {} 수신 - 서버 종료 중...", signal);

    if (g_server) {
        g_server->stop();
    }
}

// 로깅 시스템 초기화
void initializeLogging() {
    try {
        // 콘솔 출력용 싱크
        auto console_sink = std::make_shared<spdlog::sinks::stdout_color_sink_mt>();
        console_sink->set_level(spdlog::level::info);
        console_sink->set_pattern("[%Y-%m-%d %H:%M:%S.%e] [%^%l%$] [thread %t] %v");

        // 파일 출력용 싱크 (10MB씩 최대 3개 파일 로테이션)
        auto file_sink = std::make_shared<spdlog::sinks::rotating_file_sink_mt>(
            "logs/blokus-server.log", 1024 * 1024 * 10, 3);
        file_sink->set_level(spdlog::level::debug);
        file_sink->set_pattern("[%Y-%m-%d %H:%M:%S.%e] [%l] [thread %t] %v");

        // 멀티 싱크 로거 생성
        std::vector<spdlog::sink_ptr> sinks{ console_sink, file_sink };
        auto logger = std::make_shared<spdlog::logger>("blokus_server", sinks.begin(), sinks.end());
        logger->set_level(spdlog::level::debug);

        // 기본 로거로 설정
        spdlog::set_default_logger(logger);
        spdlog::flush_every(std::chrono::seconds(1));

        spdlog::info("🚀 블로커스 온라인 서버 로깅 시스템 초기화 완료");
    }
    catch (const spdlog::spdlog_ex& ex) {
        std::cerr << "로깅 초기화 실패: " << ex.what() << std::endl;
        throw;
    }
}

// 서버 설정 출력
void printServerInfo() {
    spdlog::info("=== 블로커스 온라인 서버 ===");
    spdlog::info("버전: 1.0.0");
    spdlog::info("게임 모드: 클래식 (20x20 보드)");
    spdlog::info("최대 동시 접속자: {}", Blokus::Common::MAX_CONCURRENT_USERS);
    spdlog::info("포트: {}", Blokus::Common::DEFAULT_SERVER_PORT);
    spdlog::info("========================");
}

int main(int argc, char* argv[]) {
    try {
        // 로깅 시스템 초기화
        initializeLogging();

        // 서버 정보 출력
        printServerInfo();

        // 시그널 핸들러 등록
        signal(SIGINT, signalHandler);
        signal(SIGTERM, signalHandler);
#ifdef _WIN32
        signal(SIGBREAK, signalHandler);
#endif

        // 서버 설정
        Blokus::Server::ServerConfig config;
        config.port = Blokus::Server::DEFAULT_SERVER_PORT;
        config.maxConnections = Blokus::Server::MAX_CONCURRENT_USERS;
        config.threadPoolSize = std::thread::hardware_concurrency();

        // 명령행 인수 처리
        for (int i = 1; i < argc; ++i) {
            std::string arg = argv[i];

            if (arg == "--port" && i + 1 < argc) {
                config.port = static_cast<uint16_t>(std::stoi(argv[++i]));
                spdlog::info("포트 설정: {}", config.port);
            }
            else if (arg == "--max-connections" && i + 1 < argc) {
                config.maxConnections = std::stoi(argv[++i]);
                spdlog::info("최대 연결 수 설정: {}", config.maxConnections);
            }
            else if (arg == "--threads" && i + 1 < argc) {
                config.threadPoolSize = std::stoi(argv[++i]);
                spdlog::info("스레드 풀 크기 설정: {}", config.threadPoolSize);
            }
            else if (arg == "--help" || arg == "-h") {
                std::cout << "블로커스 온라인 서버\n";
                std::cout << "사용법: " << argv[0] << " [옵션]\n";
                std::cout << "옵션:\n";
                std::cout << "  --port <포트>           서버 포트 (기본값: " << Blokus::Server::DEFAULT_SERVER_PORT << ")\n";
                std::cout << "  --max-connections <수>  최대 연결 수 (기본값: " << Blokus::Server::MAX_CONCURRENT_USERS << ")\n";
                std::cout << "  --threads <수>          스레드 풀 크기 (기본값: CPU 코어 수)\n";
                std::cout << "  --help, -h              이 도움말 표시\n";
                return 0;
            }
        }

        // 서버 인스턴스 생성 및 시작
        spdlog::info("서버 초기화 중...");
        g_server = std::make_unique<Blokus::Server::GameServer>(config);

        spdlog::info("서버 시작 중... (포트: {})", config.port);
        g_server->start();

        spdlog::info("🎮 블로커스 온라인 서버가 성공적으로 시작되었습니다!");
        spdlog::info("서버 종료하려면 Ctrl+C를 누르세요.");

        // 서버 실행 (메인 스레드 블로킹)
        g_server->run();

        spdlog::info("서버가 정상적으로 종료되었습니다.");
        return 0;
    }
    catch (const std::exception& e) {
        spdlog::error("서버 실행 중 오류 발생: {}", e.what());
        return 1;
    }
    catch (...) {
        spdlog::error("알 수 없는 오류로 서버가 종료되었습니다.");
        return 1;
    }
}