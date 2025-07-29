#pragma once

#include <string>
#include <cstdlib>
#include <spdlog/spdlog.h>

namespace Blokus {
    namespace Server {

        // ========================================
        // ������ ȯ�溯�� ���� �Լ���
        // ========================================
        inline std::string getEnvString(const char* name, const char* defaultValue = "") {
            const char* value = std::getenv(name);
            return value ? std::string(value) : std::string(defaultValue);
        }

        inline int getEnvInt(const char* name, int defaultValue = 0) {
            const char* value = std::getenv(name);
            return value ? std::atoi(value) : defaultValue;
        }

        inline bool getEnvBool(const char* name, bool defaultValue = false) {
            const char* value = std::getenv(name);
            if (!value) return defaultValue;
            std::string val(value);
            return (val == "true" || val == "1" || val == "yes");
        }

        // ========================================
        // ������ ���� ������ Ŭ����
        // ========================================
        class ConfigManager {
        public:
            static void initialize() {
                // 서버 설정
                serverPort = getEnvInt("SERVER_PORT", 9999);
                maxClients = getEnvInt("SERVER_MAX_CLIENTS", 1000);
                threadPoolSize = getEnvInt("SERVER_THREAD_POOL_SIZE", 4);

                // 데이터베이스 설정
                dbHost = getEnvString("DB_HOST", "localhost");
                dbPort = getEnvString("DB_PORT", "5432");
                dbUser = getEnvString("DB_USER", "admin");
                dbPassword = getEnvString("DB_PASSWORD", "admin");
                dbName = getEnvString("DB_NAME", "blokus_online");
                dbPoolSize = getEnvInt("DB_POOL_SIZE", 10);

                // 보안 설정
                jwtSecret = getEnvString("JWT_SECRET", "dev_secret_change_in_production");
                sessionTimeoutHours = getEnvInt("SESSION_TIMEOUT_HOURS", 24);
                passwordSaltRounds = getEnvInt("PASSWORD_SALT_ROUNDS", 12);

                // 로깅 설정
                logLevel = getEnvString("LOG_LEVEL", "info");
                logDirectory = getEnvString("LOG_DIRECTORY", "logs");

                // 개발 설정
                debugMode = getEnvBool("DEBUG_MODE", false);
                enableSqlLogging = getEnvBool("ENABLE_SQL_LOGGING", false);

                // PostgreSQL 연결 문자열 생성
                dbConnectionString = "host=" + dbHost + " port=" + dbPort +
                    " user=" + dbUser + " password=" + dbPassword +
                    " dbname=" + dbName + " client_encoding=UTF8";

                spdlog::info("환경변수 불러오기 완료! 서버 포트: {}, DB: {}@{}:{}/{}",
                    serverPort, dbUser, dbHost, dbPort, dbName);

                if (debugMode) {
                    printConfig();
                }
            }

            static bool validate() {
                if (dbHost.empty() || dbUser.empty() || dbName.empty()) {
                    spdlog::error("�ʼ� DB ������ �����Ǿ����ϴ�: DB_HOST, DB_USER, DB_NAME");
                    return false;
                }
                return true;
            }

            static void printConfig() {
                spdlog::info("=== Configuration Settings ===");
                spdlog::info("SERVER_PORT={}", serverPort);
                spdlog::info("SERVER_MAX_CLIENTS={}", maxClients);
                spdlog::info("DB_HOST={}", dbHost);
                spdlog::info("DB_PORT={}", dbPort);
                spdlog::info("DB_USER={}", dbUser);
                spdlog::info("DB_PASSWORD=***MASKED***");
                spdlog::info("DB_NAME={}", dbName);
                spdlog::info("LOG_LEVEL={}", logLevel);
                spdlog::info("DEBUG_MODE={}", debugMode ? "true" : "false");
                spdlog::info("==============================");
            }

            // ========================================
            // ���� ���� (public static)
            // ========================================

            // ���� ����
            static int serverPort;
            static int maxClients;
            static int threadPoolSize;

            // �����ͺ��̽� ����
            static std::string dbHost;
            static std::string dbPort;
            static std::string dbUser;
            static std::string dbPassword;
            static std::string dbName;
            static std::string dbConnectionString;
            static int dbPoolSize;

            // ���� ����
            static std::string jwtSecret;
            static int sessionTimeoutHours;
            static int passwordSaltRounds;

            // �α� ����
            static std::string logLevel;
            static std::string logDirectory;

            // ���� ����
            static bool debugMode;
            static bool enableSqlLogging;
        };

    } // namespace Server
} // namespace Blokus