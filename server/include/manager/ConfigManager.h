#pragma once

#include <string>
#include <unordered_map>
#include <mutex>
#include <optional>
#include <chrono>

namespace Blokus {
    namespace Server {

        // ========================================
        // 통합 설정 관리자 클래스
        // ========================================
        class ConfigManager {
        public:
            // 싱글톤 패턴
            static ConfigManager& getInstance();

            // 초기화 (환경변수 + .env 파일 로드)
            bool initialize(const std::string& envFilePath = ".env");
            void shutdown();

            // ========================================
            // 기본 값 접근 메서드
            // ========================================
            std::optional<std::string> getString(const std::string& key);
            std::string getString(const std::string& key, const std::string& defaultValue);
            int getInt(const std::string& key, int defaultValue = 0);
            bool getBool(const std::string& key, bool defaultValue = false);
            double getDouble(const std::string& key, double defaultValue = 0.0);

            // 설정 값 변경 (런타임)
            void set(const std::string& key, const std::string& value);

            // ========================================
            // 구조화된 설정 접근 (캐시됨)
            // ========================================

            // 서버 설정
            struct ServerConfig {
                uint16_t port;
                int maxConnections;
                int threadPoolSize;
                std::chrono::seconds heartbeatInterval;
                std::chrono::seconds clientTimeout;

                void loadFromConfig(ConfigManager& config);
            };

            // 데이터베이스 설정
            struct DatabaseConfig {
                std::string host;
                int port;
                std::string user;
                std::string password;
                std::string database;
                int poolSize;
                bool enableSqlLogging;

                void loadFromConfig(ConfigManager& config);
                std::string getConnectionString() const;
            };

            // 보안 설정
            struct SecurityConfig {
                std::string jwtSecret;
                std::chrono::hours sessionTimeout;
                int maxLoginAttempts;
                std::chrono::minutes loginBanTime;
                int minPasswordLength;
                int passwordSaltRounds;

                void loadFromConfig(ConfigManager& config);
            };

            // 게임 룰 설정
            struct GameConfig {
                int maxPlayersPerRoom;
                int minPlayersToStart;
                std::chrono::seconds turnTimeout;
                int maxRoomsPerUser;
                bool allowSpectators;
                bool allowAI;
                int maxAIPlayersPerRoom;

                void loadFromConfig(ConfigManager& config);
            };

            // 로깅 설정
            struct LoggingConfig {
                std::string level;
                std::string logDirectory;
                size_t maxFileSize;
                size_t maxFiles;
                bool enableConsoleLogging;
                bool enableFileLogging;

                void loadFromConfig(ConfigManager& config);
            };

            // 구조화된 설정 접근자들
            const ServerConfig& getServerConfig();
            const DatabaseConfig& getDatabaseConfig();
            const SecurityConfig& getSecurityConfig();
            const GameConfig& getGameConfig();
            const LoggingConfig& getLoggingConfig();

            // ========================================
            // 유틸리티 메서드
            // ========================================
            bool isInitialized() const { return m_isInitialized; }
            void printLoadedConfig() const;
            bool validateConfig() const;
            std::vector<std::string> getValidationErrors() const;

        private:
            ConfigManager() = default;
            ~ConfigManager() = default;
            ConfigManager(const ConfigManager&) = delete;
            ConfigManager& operator=(const ConfigManager&) = delete;

            // .env 파일 로드
            bool loadFromFile(const std::string& envFilePath);

            // 시스템 환경변수 로드
            bool loadFromEnvironment();

            // 문자열 파싱 헬퍼
            std::string trim(const std::string& str);
            std::pair<std::string, std::string> parseLine(const std::string& line);

            // 캐시 무효화
            void invalidateCache();

        private:
            // 원시 키-값 저장소
            std::unordered_map<std::string, std::string> m_configValues;
            mutable std::mutex m_configMutex;

            // 구조화된 설정 캐시
            mutable std::optional<ServerConfig> m_serverConfig;
            mutable std::optional<DatabaseConfig> m_databaseConfig;
            mutable std::optional<SecurityConfig> m_securityConfig;
            mutable std::optional<GameConfig> m_gameConfig;
            mutable std::optional<LoggingConfig> m_loggingConfig;
            mutable std::mutex m_cacheMutex;

            bool m_isInitialized = false;
        };

        // ========================================
        // 편의 매크로 (선택적 사용)
        // ========================================
#define CONFIG ConfigManager::getInstance()
#define GET_CONFIG_STRING(key, defaultVal) CONFIG.getString(key, defaultVal)
#define GET_CONFIG_INT(key, defaultVal) CONFIG.getInt(key, defaultVal)
#define GET_CONFIG_BOOL(key, defaultVal) CONFIG.getBool(key, defaultVal)

    } // namespace Server
} // namespace Blokus