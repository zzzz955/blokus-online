#pragma once

#include "server/common/ServerTypes.h"
#include <spdlog/spdlog.h>
#include <spdlog/sinks/rotating_file_sink.h>
#include <spdlog/sinks/stdout_color_sinks.h>
#include <memory>
#include <string>

namespace Blokus {
    namespace Server {

        // ========================================
        // �α� �ý��� Ŭ����
        // ========================================
        class Logger {
        public:
            enum class Level {
                Trace = 0,
                Debug = 1,
                Info = 2,
                Warn = 3,
                Error = 4,
                Critical = 5
            };

            // �̱��� ����
            static Logger& getInstance();

            // �ʱ�ȭ �� ����
            bool initialize(const std::string& logDirectory = "logs");
            void shutdown();

            // �⺻ �α� �Լ���
            template<typename... Args>
            void trace(const std::string& format, Args&&... args) {
                if (m_mainLogger) m_mainLogger->trace(format, std::forward<Args>(args)...);
            }

            template<typename... Args>
            void debug(const std::string& format, Args&&... args) {
                if (m_mainLogger) m_mainLogger->debug(format, std::forward<Args>(args)...);
            }

            template<typename... Args>
            void info(const std::string& format, Args&&... args) {
                if (m_mainLogger) m_mainLogger->info(format, std::forward<Args>(args)...);
            }

            template<typename... Args>
            void warn(const std::string& format, Args&&... args) {
                if (m_mainLogger) m_mainLogger->warn(format, std::forward<Args>(args)...);
            }

            template<typename... Args>
            void error(const std::string& format, Args&&... args) {
                if (m_mainLogger) m_mainLogger->error(format, std::forward<Args>(args)...);
            }

            template<typename... Args>
            void critical(const std::string& format, Args&&... args) {
                if (m_mainLogger) m_mainLogger->critical(format, std::forward<Args>(args)...);
            }

            // Ư�� �α� �Լ���
            void logClientConnection(uint32_t sessionId, const std::string& remoteAddress);
            void logClientDisconnection(uint32_t sessionId, const std::string& reason);
            void logGameEvent(int roomId, const std::string& eventType, const std::string& details);
            void logServerError(ServerErrorCode errorCode, const std::string& context);
            void logPerformanceMetric(const std::string& metric, double value);

            // ����
            void setLogLevel(Level level);
            void enableFileLogging(bool enable);
            void enableConsoleLogging(bool enable);
            void setMaxFileSize(size_t maxSize);
            void setMaxFiles(size_t maxFiles);

            // ���
            struct LogStats {
                uint64_t totalMessages = 0;
                uint64_t errorMessages = 0;
                uint64_t warningMessages = 0;
                std::chrono::system_clock::time_point lastError;
            };

            LogStats getStats() const { return m_stats; }

        private:
            Logger() = default;
            ~Logger() = default;
            Logger(const Logger&) = delete;
            Logger& operator=(const Logger&) = delete;

            // �ΰ� �ν��Ͻ���
            std::shared_ptr<spdlog::logger> m_mainLogger;
            std::shared_ptr<spdlog::logger> m_gameLogger;      // ���� �̺�Ʈ ����
            std::shared_ptr<spdlog::logger> m_networkLogger;   // ��Ʈ��ũ �̺�Ʈ ����
            std::shared_ptr<spdlog::logger> m_errorLogger;     // ���� ����

            // ����
            bool m_isInitialized = false;
            std::string m_logDirectory;
            Level m_currentLevel = Level::Info;
            size_t m_maxFileSize = 1024 * 1024 * 10; // 10MB
            size_t m_maxFiles = 5;

            // ���
            mutable LogStats m_stats;
            mutable std::mutex m_statsMutex;

            // ���� �Լ���
            void updateStats(Level level);
            std::string formatClientInfo(uint32_t sessionId, const std::string& address);
        };

        // ========================================
        // ���� ��ũ�ε�
        // ========================================
#define LOG_TRACE(...) Blokus::Server::Logger::getInstance().trace(__VA_ARGS__)
#define LOG_DEBUG(...) Blokus::Server::Logger::getInstance().debug(__VA_ARGS__)
#define LOG_INFO(...) Blokus::Server::Logger::getInstance().info(__VA_ARGS__)
#define LOG_WARN(...) Blokus::Server::Logger::getInstance().warn(__VA_ARGS__)
#define LOG_ERROR(...) Blokus::Server::Logger::getInstance().error(__VA_ARGS__)
#define LOG_CRITICAL(...) Blokus::Server::Logger::getInstance().critical(__VA_ARGS__)

// Ư�� �α� ��ũ�ε�
#define LOG_CLIENT_CONNECT(sessionId, address) \
            Blokus::Server::Logger::getInstance().logClientConnection(sessionId, address)

#define LOG_CLIENT_DISCONNECT(sessionId, reason) \
            Blokus::Server::Logger::getInstance().logClientDisconnection(sessionId, reason)

#define LOG_GAME_EVENT(roomId, eventType, details) \
            Blokus::Server::Logger::getInstance().logGameEvent(roomId, eventType, details)

#define LOG_SERVER_ERROR(errorCode, context) \
            Blokus::Server::Logger::getInstance().logServerError(errorCode, context)

    } // namespace Server
} // namespace Blokus