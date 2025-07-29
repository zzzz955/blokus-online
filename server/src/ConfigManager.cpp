#include "ConfigManager.h"

namespace Blokus {
    namespace Server {

        // ========================================
        // Static 변수 정의 (초기값 없음 - initialize()에서 설정)
        // ========================================

        // 서버 설정
        int ConfigManager::serverPort;
        int ConfigManager::maxClients;
        int ConfigManager::threadPoolSize;

        // 데이터베이스 설정
        std::string ConfigManager::dbHost;
        std::string ConfigManager::dbPort;
        std::string ConfigManager::dbUser;
        std::string ConfigManager::dbPassword;
        std::string ConfigManager::dbName;
        std::string ConfigManager::dbConnectionString;
        int ConfigManager::dbPoolSize;

        // 보안 설정
        std::string ConfigManager::jwtSecret;
        int ConfigManager::sessionTimeoutHours;
        int ConfigManager::passwordSaltRounds;

        // 로깅 설정
        std::string ConfigManager::logLevel;
        std::string ConfigManager::logDirectory;

        // 개발 설정
        bool ConfigManager::debugMode;
        bool ConfigManager::enableSqlLogging;

    } // namespace Server
} // namespace Blokus