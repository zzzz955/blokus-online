#pragma once

#include <string>
#include <unordered_map>
#include <mutex>
#include <optional>
#include <chrono>

namespace Blokus {
    namespace Server {

        // ========================================
        // ���� ���� ������ Ŭ����
        // ========================================
        class ConfigManager {
        public:
            // �̱��� ����
            static ConfigManager& getInstance();

            // �ʱ�ȭ (ȯ�溯�� + .env ���� �ε�)
            bool initialize(const std::string& envFilePath = ".env");
            void shutdown();

            // ========================================
            // �⺻ �� ���� �޼���
            // ========================================
            std::optional<std::string> getString(const std::string& key);
            std::string getString(const std::string& key, const std::string& defaultValue);
            int getInt(const std::string& key, int defaultValue = 0);
            bool getBool(const std::string& key, bool defaultValue = false);
            double getDouble(const std::string& key, double defaultValue = 0.0);

            // ���� �� ���� (��Ÿ��)
            void set(const std::string& key, const std::string& value);

            // ========================================
            // ����ȭ�� ���� ���� (ĳ�õ�)
            // ========================================

            // ���� ����
            struct ServerConfig {
                uint16_t port;
                int maxConnections;
                int threadPoolSize;
                std::chrono::seconds heartbeatInterval;
                std::chrono::seconds clientTimeout;

                void loadFromConfig(ConfigManager& config);
            };

            // �����ͺ��̽� ����
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

            // ���� ����
            struct SecurityConfig {
                std::string jwtSecret;
                std::chrono::hours sessionTimeout;
                int maxLoginAttempts;
                std::chrono::minutes loginBanTime;
                int minPasswordLength;
                int passwordSaltRounds;

                void loadFromConfig(ConfigManager& config);
            };

            // ���� �� ����
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

            // �α� ����
            struct LoggingConfig {
                std::string level;
                std::string logDirectory;
                size_t maxFileSize;
                size_t maxFiles;
                bool enableConsoleLogging;
                bool enableFileLogging;

                void loadFromConfig(ConfigManager& config);
            };

            // ����ȭ�� ���� �����ڵ�
            const ServerConfig& getServerConfig();
            const DatabaseConfig& getDatabaseConfig();
            const SecurityConfig& getSecurityConfig();
            const GameConfig& getGameConfig();
            const LoggingConfig& getLoggingConfig();

            // ========================================
            // ��ƿ��Ƽ �޼���
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

            // .env ���� �ε�
            bool loadFromFile(const std::string& envFilePath);

            // �ý��� ȯ�溯�� �ε�
            bool loadFromEnvironment();

            // ���ڿ� �Ľ� ����
            std::string trim(const std::string& str);
            std::pair<std::string, std::string> parseLine(const std::string& line);

            // ĳ�� ��ȿȭ
            void invalidateCache();

        private:
            // ���� Ű-�� �����
            std::unordered_map<std::string, std::string> m_configValues;
            mutable std::mutex m_configMutex;

            // ����ȭ�� ���� ĳ��
            mutable std::optional<ServerConfig> m_serverConfig;
            mutable std::optional<DatabaseConfig> m_databaseConfig;
            mutable std::optional<SecurityConfig> m_securityConfig;
            mutable std::optional<GameConfig> m_gameConfig;
            mutable std::optional<LoggingConfig> m_loggingConfig;
            mutable std::mutex m_cacheMutex;

            bool m_isInitialized = false;
        };

        // ========================================
        // ���� ��ũ�� (������ ���)
        // ========================================
#define CONFIG ConfigManager::getInstance()
#define GET_CONFIG_STRING(key, defaultVal) CONFIG.getString(key, defaultVal)
#define GET_CONFIG_INT(key, defaultVal) CONFIG.getInt(key, defaultVal)
#define GET_CONFIG_BOOL(key, defaultVal) CONFIG.getBool(key, defaultVal)

    } // namespace Server
} // namespace Blokus