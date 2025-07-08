#pragma once

#include "server/common/ServerTypes.h"
#include <string>
#include <map>
#include <mutex>

namespace Blokus {
    namespace Server {

        // ========================================
        // ���� ������ Ŭ����
        // ========================================
        class ConfigManager {
        public:
            // �̱��� ����
            static ConfigManager& getInstance();

            // �ʱ�ȭ
            bool loadFromFile(const std::string& configFile);
            bool loadFromEnvironment();
            bool saveToFile(const std::string& configFile);

            // ���� ���� ����
            const ServerConfig& getServerConfig() const { return m_serverConfig; }
            void setServerConfig(const ServerConfig& config);

            // ���� ���� �� ����
            template<typename T>
            T getValue(const std::string& key, const T& defaultValue = T{}) const;

            template<typename T>
            void setValue(const std::string& key, const T& value);

            // ���� ����
            bool validateConfig() const;
            std::vector<std::string> getConfigErrors() const;

            // ��Ÿ�� ���� ����
            void reloadConfig();
            bool isConfigChanged() const;

            // ���� �� ����
            struct GameRules {
                int maxPlayersPerRoom = 4;
                int minPlayersToStart = 2;
                int turnTimeoutSeconds = 120;
                int maxRoomsPerUser = 1;
                bool allowSpectators = true;
                bool allowAI = true;
                int maxAIPlayersPerRoom = 2;
            };

            const GameRules& getGameRules() const { return m_gameRules; }
            void setGameRules(const GameRules& rules);

            // ���� ����
            struct SecuritySettings {
                int maxLoginAttempts = 5;
                int loginBanTimeMinutes = 15;
                int sessionTimeoutHours = 24;
                bool requireEmailVerification = false;
                int minPasswordLength = 8;
                bool enableRateLimiting = true;
                int maxMessagesPerMinute = 60;
            };

            const SecuritySettings& getSecuritySettings() const { return m_securitySettings; }
            void setSecuritySettings(const SecuritySettings& settings);

        private:
            ConfigManager() = default;
            ~ConfigManager() = default;
            ConfigManager(const ConfigManager&) = delete;
            ConfigManager& operator=(const ConfigManager&) = delete;

            // ���� ���� �ε�
            bool loadServerConfig(const std::map<std::string, std::string>& configMap);
            bool loadGameRules(const std::map<std::string, std::string>& configMap);
            bool loadSecuritySettings(const std::map<std::string, std::string>& configMap);

            // ���� ó��
            std::map<std::string, std::string> parseConfigFile(const std::string& filename);
            std::map<std::string, std::string> getEnvironmentVariables();
            bool writeConfigFile(const std::string& filename, const std::map<std::string, std::string>& config);

            // ���� ����
            bool validateServerConfig() const;
            bool validateGameRules() const;
            bool validateSecuritySettings() const;

            // Ÿ�� ��ȯ ����
            template<typename T>
            T convertValue(const std::string& value) const;

            std::string convertToString(const std::string& value) const { return value; }
            std::string convertToString(int value) const { return std::to_string(value); }
            std::string convertToString(bool value) const { return value ? "true" : "false"; }
            std::string convertToString(double value) const { return std::to_string(value); }

        private:
            ServerConfig m_serverConfig;
            GameRules m_gameRules;
            SecuritySettings m_securitySettings;

            std::map<std::string, std::string> m_customSettings;
            mutable std::mutex m_configMutex;

            std::string m_lastConfigFile;
            std::time_t m_lastFileModified = 0;
            bool m_isInitialized = false;
        };

        // ========================================
        // ���ø� �Լ� ����
        // ========================================
        template<typename T>
        T ConfigManager::getValue(const std::string& key, const T& defaultValue) const {
            std::lock_guard<std::mutex> lock(m_configMutex);

            auto it = m_customSettings.find(key);
            if (it != m_customSettings.end()) {
                return convertValue<T>(it->second);
            }

            return defaultValue;
        }

        template<typename T>
        void ConfigManager::setValue(const std::string& key, const T& value) {
            std::lock_guard<std::mutex> lock(m_configMutex);
            m_customSettings[key] = convertToString(value);
        }

        template<typename T>
        T ConfigManager::convertValue(const std::string& value) const {
            if constexpr (std::is_same_v<T, std::string>) {
                return value;
            }
            else if constexpr (std::is_same_v<T, int>) {
                return std::stoi(value);
            }
            else if constexpr (std::is_same_v<T, bool>) {
                return value == "true" || value == "1";
            }
            else if constexpr (std::is_same_v<T, double>) {
                return std::stod(value);
            }
            else {
                return T{};
            }
        }

    } // namespace Server
} // namespace Blokus