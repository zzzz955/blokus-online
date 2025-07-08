#pragma once

#include "server/common/ServerTypes.h"
#include <string>
#include <unordered_map>
#include <chrono>
#include <mutex>
#include <atomic>

namespace Blokus {
    namespace Server {

        // ========================================
        // 레이트 리미터 클래스
        // ========================================
        class RateLimiter {
        public:
            // 제한 규칙
            struct LimitRule {
                int maxRequests;                               // 최대 요청 수
                std::chrono::seconds timeWindow;              // 시간 윈도우
                std::chrono::seconds banDuration{ 0 };          // 위반 시 차단 시간

                LimitRule(int max, std::chrono::seconds window)
                    : maxRequests(max), timeWindow(window) {
                }

                LimitRule(int max, std::chrono::seconds window, std::chrono::seconds ban)
                    : maxRequests(max), timeWindow(window), banDuration(ban) {
                }
            };

            // 검사 결과
            struct CheckResult {
                bool allowed = true;
                int remainingRequests = 0;
                std::chrono::seconds resetTime{ 0 };
                std::chrono::seconds retryAfter{ 0 };
                std::string reason;
            };

            RateLimiter();
            ~RateLimiter() = default;

            // 규칙 설정
            void addRule(const std::string& category, const LimitRule& rule);
            void removeRule(const std::string& category);
            void updateRule(const std::string& category, const LimitRule& rule);

            // 요청 검사
            CheckResult checkRequest(const std::string& clientId, const std::string& category);
            CheckResult checkIPRequest(const std::string& ipAddress, const std::string& category);

            // 수동 차단/해제
            void banClient(const std::string& clientId, std::chrono::seconds duration, const std::string& reason);
            void unbanClient(const std::string& clientId);
            bool isClientBanned(const std::string& clientId) const;

            // 통계 및 관리
            void reset(const std::string& clientId);
            void resetAll();
            void cleanup();  // 만료된 기록 정리

            // 설정
            void setGlobalEnabled(bool enabled) { m_globalEnabled = enabled; }
            bool isEnabled() const { return m_globalEnabled; }

            // 통계
            struct RateLimitStats {
                uint64_t totalRequests = 0;
                uint64_t blockedRequests = 0;
                uint64_t activeBans = 0;
                uint64_t activeClients = 0;
                std::chrono::system_clock::time_point lastBlockTime;
            };

            RateLimitStats getStats() const;

            // 미리 정의된 규칙들
            static LimitRule loginRule() { return LimitRule(5, std::chrono::minutes(5), std::chrono::minutes(15)); }
            static LimitRule messageRule() { return LimitRule(60, std::chrono::minutes(1)); }
            static LimitRule roomCreateRule() { return LimitRule(10, std::chrono::minutes(10)); }
            static LimitRule gameActionRule() { return LimitRule(30, std::chrono::seconds(30)); }

        private:
            // 클라이언트별 요청 기록
            struct ClientRecord {
                std::vector<std::chrono::steady_clock::time_point> requests;
                std::chrono::steady_clock::time_point lastRequest;
                std::chrono::steady_clock::time_point bannedUntil;
                std::string banReason;
                int violationCount = 0;
            };

            // 카테고리별 클라이언트 기록
            struct CategoryData {
                LimitRule rule;
                std::unordered_map<std::string, ClientRecord> clients;
                mutable std::mutex mutex;

                explicit CategoryData(const LimitRule& r) : rule(r) {}
            };

            // 내부 헬퍼 함수들
            CheckResult performCheck(const std::string& clientId, CategoryData& categoryData);
            void cleanupExpiredRequests(ClientRecord& record,
                std::chrono::steady_clock::time_point now,
                std::chrono::seconds window);
            bool isWithinTimeWindow(std::chrono::steady_clock::time_point requestTime,
                std::chrono::steady_clock::time_point now,
                std::chrono::seconds window) const;

        private:
            std::unordered_map<std::string, std::unique_ptr<CategoryData>> m_categories;
            mutable std::mutex m_categoriesMutex;

            std::atomic<bool> m_globalEnabled{ true };

            // 통계
            mutable RateLimitStats m_stats;
            mutable std::mutex m_statsMutex;

            // 정리 관련
            std::chrono::steady_clock::time_point m_lastCleanup;
            std::chrono::minutes m_cleanupInterval{ 10 };
        };

        // ========================================
        // 레이트 리미터 매니저 (글로벌 인스턴스)
        // ========================================
        class RateLimiterManager {
        public:
            static RateLimiterManager& getInstance();

            // 초기화 (기본 규칙 설정)
            void initialize();

            // 레이트 리미터 접근
            RateLimiter& getRateLimiter() { return m_rateLimiter; }

            // 편의 함수들
            bool checkLogin(const std::string& clientId);
            bool checkMessage(const std::string& clientId);
            bool checkRoomCreate(const std::string& clientId);
            bool checkGameAction(const std::string& clientId);
            bool checkIPAddress(const std::string& ipAddress, const std::string& category);

            // 차단 관리
            void banClientForAbuse(const std::string& clientId, const std::string& reason);
            void temporaryBan(const std::string& clientId, std::chrono::seconds duration, const std::string& reason);

        private:
            RateLimiterManager() = default;
            ~RateLimiterManager() = default;
            RateLimiterManager(const RateLimiterManager&) = delete;
            RateLimiterManager& operator=(const RateLimiterManager&) = delete;

            RateLimiter m_rateLimiter;
        };

        // ========================================
        // 편의 매크로들
        // ========================================
#define CHECK_RATE_LIMIT(clientId, category) \
            Blokus::Server::RateLimiterManager::getInstance().getRateLimiter().checkRequest(clientId, category)

#define CHECK_LOGIN_RATE(clientId) \
            Blokus::Server::RateLimiterManager::getInstance().checkLogin(clientId)

#define CHECK_MESSAGE_RATE(clientId) \
            Blokus::Server::RateLimiterManager::getInstance().checkMessage(clientId)

#define CHECK_ROOM_CREATE_RATE(clientId) \
            Blokus::Server::RateLimiterManager::getInstance().checkRoomCreate(clientId)

#define CHECK_GAME_ACTION_RATE(clientId) \
            Blokus::Server::RateLimiterManager::getInstance().checkGameAction(clientId)

    } // namespace Server
} // namespace Blokus