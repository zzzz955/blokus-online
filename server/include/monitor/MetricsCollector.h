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
        // 메트릭스 수집기 클래스
        // ========================================
        class MetricsCollector {
        public:
            // 싱글톤 패턴
            static MetricsCollector& getInstance();

            // 초기화 및 정리
            void initialize();
            void shutdown();

            // 기본 카운터
            void incrementCounter(const std::string& name);
            void decrementCounter(const std::string& name);
            void setGauge(const std::string& name, double value);
            void recordHistogram(const std::string& name, double value);

            // 시간 측정
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

            // 네트워크 메트릭스
            void recordMessageSent(size_t bytes);
            void recordMessageReceived(size_t bytes);
            void recordConnectionOpened();
            void recordConnectionClosed();
            void recordLatency(double latencyMs);

            // 게임 메트릭스
            void recordGameStarted();
            void recordGameFinished(std::chrono::seconds duration);
            void recordPlayerJoined();
            void recordPlayerLeft();
            void recordBlockPlacement();

            // 에러 메트릭스
            void recordError(ServerErrorCode errorCode);
            void recordException(const std::string& exceptionType);

            // 메트릭스 조회
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

            // 사용자 정의 메트릭스
            double getCounter(const std::string& name) const;
            double getGauge(const std::string& name) const;
            std::vector<double> getHistogram(const std::string& name) const;

            // 메트릭스 내보내기
            std::string exportPrometheus() const;
            std::string exportJSON() const;

            // 주기적 리포트
            void enablePeriodicReporting(std::chrono::seconds interval);
            void disablePeriodicReporting();

        private:
            MetricsCollector() = default;
            ~MetricsCollector() = default;
            MetricsCollector(const MetricsCollector&) = delete;
            MetricsCollector& operator=(const MetricsCollector&) = delete;

            // 내부 데이터 구조
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

            // 메트릭스 저장소
            std::unordered_map<std::string, CounterData> m_counters;
            std::unordered_map<std::string, GaugeData> m_gauges;
            std::unordered_map<std::string, HistogramData> m_histograms;
            mutable std::mutex m_metricsMutex;

            // 빌트인 메트릭스
            NetworkMetrics m_networkMetrics;
            GameMetrics m_gameMetrics;
            ErrorMetrics m_errorMetrics;

            // 주기적 리포팅
            std::atomic<bool> m_reportingEnabled{ false };
            std::chrono::seconds m_reportInterval{ 60 };
            std::thread m_reportingThread;

            // 헬퍼 함수들
            void performPeriodicReporting();
            void calculateAverages();
            std::string formatMetric(const std::string& name, double value, const std::string& type) const;
        };

        // ========================================
        // 편의 매크로들
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