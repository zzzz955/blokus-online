#include "manager/ConfigManager.h"

namespace Blokus {
    namespace Server {

        // ========================================
        // Static 변수 정의 (기본값 설정)
        // ========================================

        // 서버 설정
        int ConfigManager::serverPort = 7777;
        int ConfigManager::maxClients = 1000;
        int ConfigManager::threadPoolSize = 4;

        // 데이터베이스 설정
        std::string ConfigManager::dbHost = "localhost";
        std::string ConfigManager::dbPort = "5432";
        std::string ConfigManager::dbUser = "admin";
        std::string ConfigManager::dbPassword = "admin";
        std::string ConfigManager::dbName = "blokus_online";
        std::string ConfigManager::dbConnectionString = "";
        int ConfigManager::dbPoolSize = 10;

        // 보안 설정
        std::string ConfigManager::jwtSecret = "dev_secret_change_in_production";
        int ConfigManager::sessionTimeoutHours = 24;
        int ConfigManager::passwordSaltRounds = 12;

        // 로깅 설정
        std::string ConfigManager::logLevel = "info";
        std::string ConfigManager::logDirectory = "logs";

        // 개발 설정
        bool ConfigManager::debugMode = false;
        bool ConfigManager::enableSqlLogging = false;

    } // namespace Server
} // namespace Blokus