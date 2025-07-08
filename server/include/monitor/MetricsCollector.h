#pragma once

#include "server/common/ServerTypes.h"
#include <atomic>
#include <chrono>
#include <unordered_map>
#include <vector>
#include <mutex>

namespace Blokus {
    namespace Server {

        // ========================================
        // ��Ʈ���� ������ Ŭ����
        // ========================================
        class MetricsCollector {
        public:
            // �̱��� ����
            static MetricsCollector& getInstance();

            // �ʱ�ȭ �� ����
            void initialize();
            void shutdown();

            // �⺻ ī����
            void incrementCounter(const std::string& name);
            void decrementCounter(const std::string& name);
            void setGauge(const std::string& name, double value);
            void recordHistogram(const std::string& name, double value);

            // �ð� ����
            class Timer {
            public:
                Timer(const std::string& name);
                ~Timer();
                void stop();

            private:
                std::string m_name;
                std::chrono::high_resolution_clock::time_point m_start;
                bool m_stopped = false;
            };

            Timer createTimer(const std::string& name);

            // ��Ʈ��ũ ��Ʈ����
            void recordMessageSent(size_t bytes);
            void recordMessageReceived(size_t bytes);
            void recordConnectionOpened();
            void recordConnectionClosed();
            void recordLatency(double latencyMs);

            // ���� ��Ʈ����
            void recordGameStarted();
            void recordGameFinished(std::chrono::seconds duration);
            void recordPlayerJoined();
            void recordPlayerLeft();
            void recordBlockPlacement();

            // ���� ��Ʈ����
            void recordError(ServerErrorCode errorCode);
            void recordException(const std::string& exceptionType);

            // ��Ʈ���� ��ȸ
            struct NetworkMetrics {
                std::atomic<uint64_t> totalConnections{ 0 };
                std::atomic<uint64_t> currentConnections{ 0 };
                std::atomic<uint64_t> messagesSent{ 0 };
                std::atomic<uint64_t> messagesReceived{ 0 };
                std::atomic<uint64_t> bytesSent{ 0 };
                std::atomic<uint64_t> bytesReceived{ 0 };
                std::atomic<double> averageLatency{ 0.0 };
            };

            struct GameMetrics {
                std::atomic<uint64_t> totalGames{ 0 };
                std::atomic<uint64_t> activeGames{ 0 };
                std::atomic<uint64_t> totalPlayers{ 0 };
                std::atomic<uint64_t> activePlayers{ 0 };
                std::atomic<uint64_t> totalBlockPlacements{ 0 };
                std::atomic<double> averageGameDuration{ 0.0 };
            };

            struct ErrorMetrics {
                std::atomic<uint64_t> totalErrors{ 0 };
                std::atomic<uint64_t> authenticationErrors{ 0 };
                std::atomic<uint64_t> gameLogicErrors{ 0 };
                std::atomic<uint64_t> networkErrors{ 0 };
                std::atomic<uint64_t> databaseErrors{ 0 };
            };

            const NetworkMetrics& getNetworkMetrics() const { return m_networkMetrics; }
            const GameMetrics& getGameMetrics() const { return m_gameMetrics; }
            const ErrorMetrics& getErrorMetrics() const { return m_errorMetrics; }

            // ����� ���� ��Ʈ����
            double getCounter(const std::string& name) const;
            double getGauge(const std::string& name) const;
            std::vector<double> getHistogram(const std::string& name) const;

            // ��Ʈ���� ��������
            std::string exportPrometheus() const;
            std::string exportJSON() const;

            // �ֱ��� ����Ʈ
            void enablePeriodicReporting(std::chrono::seconds interval);
            void disablePeriodicReporting();

        private:
            MetricsCollector() = default;
            ~MetricsCollector() = default;
            MetricsCollector(const MetricsCollector&) = delete;
            MetricsCollector& operator=(const MetricsCollector&) = delete;

            // ���� ������ ����
            struct CounterData {
                std::atomic<uint64_t> value{ 0 };
                std::chrono::system_clock::time_point lastUpdated;
            };

            struct GaugeData {
                std::atomic<double> value{ 0.0 };
                std::chrono::system_clock::time_point lastUpdated;
            };

            struct HistogramData {
                std::vector<double> values;
                std::mutex mutex;
                std::chrono::system_clock::time_point lastUpdated;
            };

            // ��Ʈ���� �����
            std::unordered_map<std::string, CounterData> m_counters;
            std::unordered_map<std::string, GaugeData> m_gauges;
            std::unordered_map<std::string, HistogramData> m_histograms;
            mutable std::mutex m_metricsMutex;

            // ��Ʈ�� ��Ʈ����
            NetworkMetrics m_networkMetrics;
            GameMetrics m_gameMetrics;
            ErrorMetrics m_errorMetrics;

            // �ֱ��� ������
            std::atomic<bool> m_reportingEnabled{ false };
            std::chrono::seconds m_reportInterval{ 60 };
            std::thread m_reportingThread;

            // ���� �Լ���
            void performPeriodicReporting();
            void calculateAverages();
            std::string formatMetric(const std::string& name, double value, const std::string& type) const;
        };

        // ========================================
        // ���� ��ũ�ε�
        // ========================================
#define METRICS_INCREMENT(name) \
            Blokus::Server::MetricsCollector::getInstance().incrementCounter(name)

#define METRICS_DECREMENT(name) \
            Blokus::Server::MetricsCollector::getInstance().decrementCounter(name)

#define METRICS_SET_GAUGE(name, value) \
            Blokus::Server::MetricsCollector::getInstance().setGauge(name, value)

#define METRICS_RECORD_HISTOGRAM(name, value) \
            Blokus::Server::MetricsCollector::getInstance().recordHistogram(name, value)

#define METRICS_TIMER(name) \
            auto timer_##__LINE__ = Blokus::Server::MetricsCollector::getInstance().createTimer(name)

    } // namespace Server
} // namespace Blokus