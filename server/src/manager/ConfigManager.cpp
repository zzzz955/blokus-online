#include "manager/ConfigManager.h"
#include <fstream>
#include <iostream>
#include <sstream>
#include <algorithm>
#include <cstdlib>
#include <cstring>  // strlen 함수용
#include <spdlog/spdlog.h>

namespace Blokus {
    namespace Server {

        ConfigManager& ConfigManager::getInstance() {
            static ConfigManager instance;
            return instance;
        }

        bool ConfigManager::initialize(const std::string& envFilePath) {
            if (m_isInitialized) {
                spdlog::warn("ConfigManager already initialized");
                return true;
            }

            std::lock_guard<std::mutex> lock(m_configMutex);

            // 1. 시스템 환경변수 확인 (최우선)
            bool hasSystemEnv = loadFromEnvironment();

            // 2. .env 파일 로드 (옵션, envFilePath가 비어있지 않을 때만)
            bool hasEnvFile = false;
            if (!envFilePath.empty()) {
                hasEnvFile = loadFromFile(envFilePath);
            }

            m_isInitialized = true;
            invalidateCache();

            if (hasSystemEnv) {
                spdlog::info("✅ ConfigManager initialized with system environment variables");
            }
            else if (hasEnvFile) {
                spdlog::info("✅ ConfigManager initialized with .env file: {}", envFilePath);
            }
            else {
                spdlog::warn("⚠️ ConfigManager initialized with default values only");
                spdlog::info("💡 Tip: Use run_dev.bat for development or set environment variables");
            }

            return true; // 항상 성공 (기본값 사용)
        }

        void ConfigManager::shutdown() {
            std::lock_guard<std::mutex> configLock(m_configMutex);
            std::lock_guard<std::mutex> cacheLock(m_cacheMutex);

            m_configValues.clear();
            m_serverConfig.reset();
            m_databaseConfig.reset();
            m_securityConfig.reset();
            m_gameConfig.reset();
            m_loggingConfig.reset();

            m_isInitialized = false;
            spdlog::info("ConfigManager shutdown complete");
        }

        // ========================================
        // 기본 값 접근 메서드
        // ========================================
        std::optional<std::string> ConfigManager::getString(const std::string& key) {
            std::lock_guard<std::mutex> lock(m_configMutex);

            // 1. 먼저 시스템 환경변수에서 찾기 (최우선)
            const char* envValue = std::getenv(key.c_str());
            if (envValue != nullptr) {
                return std::string(envValue);
            }

            // 2. .env 파일에서 로드된 값 찾기 (fallback)
            auto it = m_configValues.find(key);
            if (it != m_configValues.end()) {
                return it->second;
            }

            return std::nullopt;
        }

        std::string ConfigManager::getString(const std::string& key, const std::string& defaultValue) {
            auto value = getString(key);
            return value.has_value() ? value.value() : defaultValue;
        }

        int ConfigManager::getInt(const std::string& key, int defaultValue) {
            auto value = getString(key);
            if (value.has_value()) {
                try {
                    return std::stoi(value.value());
                }
                catch (const std::exception& e) {
                    spdlog::warn("Invalid integer value for {}: {} (using default: {})", key, value.value(), defaultValue);
                }
            }
            return defaultValue;
        }

        bool ConfigManager::getBool(const std::string& key, bool defaultValue) {
            auto value = getString(key);
            if (value.has_value()) {
                std::string val = value.value();
                std::transform(val.begin(), val.end(), val.begin(), ::tolower);
                return (val == "true" || val == "1" || val == "yes" || val == "on");
            }
            return defaultValue;
        }

        double ConfigManager::getDouble(const std::string& key, double defaultValue) {
            auto value = getString(key);
            if (value.has_value()) {
                try {
                    return std::stod(value.value());
                }
                catch (const std::exception& e) {
                    spdlog::warn("Invalid double value for {}: {} (using default: {})", key, value.value(), defaultValue);
                }
            }
            return defaultValue;
        }

        void ConfigManager::set(const std::string& key, const std::string& value) {
            {
                std::lock_guard<std::mutex> lock(m_configMutex);
                m_configValues[key] = value;
            }
            invalidateCache(); // 캐시 무효화
        }

        // ========================================
        // 구조화된 설정 로더들
        // ========================================
        void ConfigManager::ServerConfig::loadFromConfig(ConfigManager& config) {
            port = config.getInt("SERVER_PORT", 7777);
            maxConnections = config.getInt("SERVER_MAX_CLIENTS", 1000);
            threadPoolSize = config.getInt("SERVER_THREAD_POOL_SIZE", 4);
            heartbeatInterval = std::chrono::seconds(config.getInt("SERVER_HEARTBEAT_INTERVAL", 10));
            clientTimeout = std::chrono::seconds(config.getInt("SERVER_CLIENT_TIMEOUT", 30));
        }

        void ConfigManager::DatabaseConfig::loadFromConfig(ConfigManager& config) {
            host = config.getString("DB_HOST", "localhost");
            port = config.getInt("DB_PORT", 5432);
            user = config.getString("DB_USER", "admin");
            password = config.getString("DB_PASSWORD", "admin");
            database = config.getString("DB_NAME", "blokus_online");
            poolSize = config.getInt("DB_POOL_SIZE", 10);
            enableSqlLogging = config.getBool("ENABLE_SQL_LOGGING", false);
        }

        std::string ConfigManager::DatabaseConfig::getConnectionString() const {
            std::ostringstream oss;
            oss << "host=" << host
                << " port=" << port
                << " user=" << user
                << " password=" << password
                << " dbname=" << database;
            return oss.str();
        }

        void ConfigManager::SecurityConfig::loadFromConfig(ConfigManager& config) {
            jwtSecret = config.getString("JWT_SECRET", "change_this_secret_key_in_production");
            sessionTimeout = std::chrono::hours(config.getInt("SESSION_TIMEOUT_HOURS", 24));
            maxLoginAttempts = config.getInt("MAX_LOGIN_ATTEMPTS", 5);
            loginBanTime = std::chrono::minutes(config.getInt("LOGIN_BAN_TIME_MINUTES", 15));
            minPasswordLength = config.getInt("MIN_PASSWORD_LENGTH", 8);
            passwordSaltRounds = config.getInt("PASSWORD_SALT_ROUNDS", 12);
        }

        void ConfigManager::GameConfig::loadFromConfig(ConfigManager& config) {
            maxPlayersPerRoom = config.getInt("GAME_MAX_PLAYERS_PER_ROOM", 4);
            minPlayersToStart = config.getInt("GAME_MIN_PLAYERS_TO_START", 2);
            turnTimeout = std::chrono::seconds(config.getInt("GAME_TURN_TIMEOUT_SECONDS", 120));
            maxRoomsPerUser = config.getInt("GAME_MAX_ROOMS_PER_USER", 1);
            allowSpectators = config.getBool("GAME_ALLOW_SPECTATORS", true);
            allowAI = config.getBool("GAME_ALLOW_AI", true);
            maxAIPlayersPerRoom = config.getInt("GAME_MAX_AI_PLAYERS_PER_ROOM", 2);
        }

        void ConfigManager::LoggingConfig::loadFromConfig(ConfigManager& config) {
            level = config.getString("LOG_LEVEL", "info");
            logDirectory = config.getString("LOG_DIRECTORY", "logs");
            maxFileSize = config.getInt("LOG_FILE_MAX_SIZE", 10485760); // 10MB
            maxFiles = config.getInt("LOG_MAX_FILES", 5);
            enableConsoleLogging = config.getBool("LOG_ENABLE_CONSOLE", true);
            enableFileLogging = config.getBool("LOG_ENABLE_FILE", true);
        }

        // ========================================
        // 구조화된 설정 접근자들 (캐시됨)
        // ========================================
        const ConfigManager::ServerConfig& ConfigManager::getServerConfig() {
            std::lock_guard<std::mutex> lock(m_cacheMutex);
            if (!m_serverConfig.has_value()) {
                m_serverConfig = ServerConfig{};
                m_serverConfig->loadFromConfig(*this);
            }
            return m_serverConfig.value();
        }

        const ConfigManager::DatabaseConfig& ConfigManager::getDatabaseConfig() {
            std::lock_guard<std::mutex> lock(m_cacheMutex);
            if (!m_databaseConfig.has_value()) {
                m_databaseConfig = DatabaseConfig{};
                m_databaseConfig->loadFromConfig(*this);
            }
            return m_databaseConfig.value();
        }

        const ConfigManager::SecurityConfig& ConfigManager::getSecurityConfig() {
            std::lock_guard<std::mutex> lock(m_cacheMutex);
            if (!m_securityConfig.has_value()) {
                m_securityConfig = SecurityConfig{};
                m_securityConfig->loadFromConfig(*this);
            }
            return m_securityConfig.value();
        }

        const ConfigManager::GameConfig& ConfigManager::getGameConfig() {
            std::lock_guard<std::mutex> lock(m_cacheMutex);
            if (!m_gameConfig.has_value()) {
                m_gameConfig = GameConfig{};
                m_gameConfig->loadFromConfig(*this);
            }
            return m_gameConfig.value();
        }

        const ConfigManager::LoggingConfig& ConfigManager::getLoggingConfig() {
            std::lock_guard<std::mutex> lock(m_cacheMutex);
            if (!m_loggingConfig.has_value()) {
                m_loggingConfig = LoggingConfig{};
                m_loggingConfig->loadFromConfig(*this);
            }
            return m_loggingConfig.value();
        }

        // ========================================
        // 내부 구현 메서드들
        // ========================================
        bool ConfigManager::loadFromFile(const std::string& envFilePath) {
            std::ifstream file(envFilePath);
            if (!file.is_open()) {
                spdlog::error("Cannot open .env file: {}", envFilePath);
                return false;
            }

            std::string line;
            int lineNumber = 0;
            int loadedCount = 0;

            spdlog::debug("Loading .env file: {}", envFilePath);

            while (std::getline(file, line)) {
                lineNumber++;

                // 원본 라인 로깅 (디버그)
                spdlog::debug("Line {}: '{}'", lineNumber, line);

                line = trim(line);
                if (line.empty() || line[0] == '#') {
                    spdlog::debug("Skipping empty/comment line {}", lineNumber);
                    continue;
                }

                auto [key, value] = parseLine(line);
                if (!key.empty()) {
                    m_configValues[key] = value;
                    loadedCount++;
                    spdlog::debug("Loaded: {}={}", key, value.empty() ? "(empty)" : "***");

                    // 시스템 환경변수에도 설정
#ifdef _WIN32
                    _putenv_s(key.c_str(), value.c_str());
#else
                    setenv(key.c_str(), value.c_str(), 0);
#endif
                }
                else {
                    spdlog::warn("Invalid line {} in {}: '{}'", lineNumber, envFilePath, line);
                }
            }

            spdlog::info("Loaded {} environment variables from {}", loadedCount, envFilePath);

            // 디버그: 로드된 키들 출력
            if (loadedCount > 0) {
                spdlog::debug("Loaded keys:");
                for (const auto& [key, value] : m_configValues) {
                    spdlog::debug("  - {}", key);
                }
            }

            return loadedCount > 0;
        }

        bool ConfigManager::loadFromEnvironment() {
            // 필요한 환경변수들 확인
            const std::vector<std::string> requiredVars = {
                // Database
                "DB_HOST", "DB_PORT", "DB_USER", "DB_PASSWORD", "DB_NAME", "DB_POOL_SIZE",
                // Server
                "SERVER_PORT", "SERVER_MAX_CLIENTS", "SERVER_THREAD_POOL_SIZE",
                // Security
                "JWT_SECRET", "SESSION_TIMEOUT_HOURS", "PASSWORD_SALT_ROUNDS",
                // Game
                "GAME_MAX_PLAYERS_PER_ROOM", "GAME_TURN_TIMEOUT_SECONDS",
                // Logging
                "LOG_LEVEL", "LOG_DIRECTORY", "LOG_FILE_MAX_SIZE",
                // Development
                "DEBUG_MODE", "ENABLE_SQL_LOGGING"
            };

            int foundCount = 0;
            for (const auto& varName : requiredVars) {
                const char* value = std::getenv(varName.c_str());
                if (value != nullptr) {
                    // .env 파일 값을 덮어쓰지 않고 별도 저장
                    // (getString에서 우선순위 처리)
                    foundCount++;
                    spdlog::debug("Found system env: {}", varName);
                }
            }

            spdlog::info("Found {} system environment variables", foundCount);
            return foundCount > 0;
        }

        void ConfigManager::invalidateCache() {
            std::lock_guard<std::mutex> lock(m_cacheMutex);
            m_serverConfig.reset();
            m_databaseConfig.reset();
            m_securityConfig.reset();
            m_gameConfig.reset();
            m_loggingConfig.reset();
        }

        std::string ConfigManager::trim(const std::string& str) {
            size_t start = str.find_first_not_of(" \t\r\n");
            if (start == std::string::npos) return "";

            size_t end = str.find_last_not_of(" \t\r\n");
            return str.substr(start, end - start + 1);
        }

        std::pair<std::string, std::string> ConfigManager::parseLine(const std::string& line) {
            size_t equalPos = line.find('=');
            if (equalPos == std::string::npos) {
                return { "", "" };
            }

            std::string key = trim(line.substr(0, equalPos));
            std::string value = trim(line.substr(equalPos + 1));

            // 따옴표 제거
            if (value.length() >= 2) {
                char first = value.front();
                char last = value.back();
                if ((first == '"' && last == '"') || (first == '\'' && last == '\'')) {
                    value = value.substr(1, value.length() - 2);
                }
            }

            return { key, value };
        }

        void ConfigManager::printLoadedConfig() const {
            spdlog::info("=== Configuration Settings ===");

            // 주요 환경변수들 출력
            const std::vector<std::string> importantVars = {
                "DB_HOST", "DB_PORT", "DB_USER", "DB_PASSWORD", "DB_NAME", "DB_POOL_SIZE",
                "SERVER_PORT", "SERVER_MAX_CLIENTS", "SERVER_THREAD_POOL_SIZE",
                "JWT_SECRET", "SESSION_TIMEOUT_HOURS", "PASSWORD_SALT_ROUNDS",
                "GAME_MAX_PLAYERS_PER_ROOM", "GAME_TURN_TIMEOUT_SECONDS",
                "LOG_LEVEL", "LOG_DIRECTORY", "DEBUG_MODE", "ENABLE_SQL_LOGGING"
            };

            for (const auto& key : importantVars) {
                // 환경변수 우선 확인
                const char* envValue = std::getenv(key.c_str());
                if (envValue != nullptr) {
                    // 민감한 정보 마스킹
                    if (key.find("PASSWORD") != std::string::npos ||
                        key.find("SECRET") != std::string::npos ||
                        key.find("TOKEN") != std::string::npos) {
                        spdlog::info("{}=***MASKED*** (from env)", key);
                    }
                    else {
                        spdlog::info("{}={} (from env)", key, envValue);
                    }
                    continue;
                }

                // .env 파일에서 확인
                std::lock_guard<std::mutex> lock(m_configMutex);
                auto it = m_configValues.find(key);
                if (it != m_configValues.end()) {
                    if (key.find("PASSWORD") != std::string::npos ||
                        key.find("SECRET") != std::string::npos ||
                        key.find("TOKEN") != std::string::npos) {
                        spdlog::info("{}=***MASKED*** (from .env)", key);
                    }
                    else {
                        spdlog::info("{}={} (from .env)", key, it->second);
                    }
                }
            }

            spdlog::info("==============================");
        }

        bool ConfigManager::validateConfig() const {
            // 필수 설정 검증 (환경변수 우선으로 확인)
            const std::vector<std::string> requiredKeys = {
                "DB_HOST", "DB_PORT", "DB_USER", "DB_PASSWORD", "DB_NAME"
            };

            for (const auto& key : requiredKeys) {
                // 환경변수 먼저 확인
                const char* envValue = std::getenv(key.c_str());
                if (envValue != nullptr && strlen(envValue) > 0) {
                    continue; // 환경변수에 있으면 OK
                }

                // .env 파일에서 확인
                auto it = m_configValues.find(key);
                if (it != m_configValues.end() && !it->second.empty()) {
                    continue; // .env 파일에 있으면 OK
                }

                // 둘 다 없으면 오류
                spdlog::error("Missing required configuration: {}", key);
                return false;
            }

            return true;
        }

    } // namespace Server
} // namespace Blokus