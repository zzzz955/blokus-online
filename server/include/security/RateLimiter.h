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
        // ����Ʈ ������ Ŭ����
        // ========================================
        class RateLimiter {
        public:
            // ���� ��Ģ
            struct LimitRule {
                int maxRequests;                               // �ִ� ��û ��
                std::chrono::seconds timeWindow;              // �ð� ������
                std::chrono::seconds banDuration{ 0 };          // ���� �� ���� �ð�

                LimitRule(int max, std::chrono::seconds window)
                    : maxRequests(max), timeWindow(window) {
                }

                LimitRule(int max, std::chrono::seconds window, std::chrono::seconds ban)
                    : maxRequests(max), timeWindow(window), banDuration(ban) {
                }
            };

            // �˻� ���
            struct CheckResult {
                bool allowed = true;
                int remainingRequests = 0;
                std::chrono::seconds resetTime{ 0 };
                std::chrono::seconds retryAfter{ 0 };
                std::string reason;
            };

            RateLimiter();
            ~RateLimiter() = default;

            // ��Ģ ����
            void addRule(const std::string& category, const LimitRule& rule);
            void removeRule(const std::string& category);
            void updateRule(const std::string& category, const LimitRule& rule);

            // ��û �˻�
            CheckResult checkRequest(const std::string& clientId, const std::string& category);
            CheckResult checkIPRequest(const std::string& ipAddress, const std::string& category);

            // ���� ����/����
            void banClient(const std::string& clientId, std::chrono::seconds duration, const std::string& reason);
            void unbanClient(const std::string& clientId);
            bool isClientBanned(const std::string& clientId) const;

            // ��� �� ����
            void reset(const std::string& clientId);
            void resetAll();
            void cleanup();  // ����� ��� ����

            // ����
            void setGlobalEnabled(bool enabled) { m_globalEnabled = enabled; }
            bool isEnabled() const { return m_globalEnabled; }

            // ���
            struct RateLimitStats {
                uint64_t totalRequests = 0;
                uint64_t blockedRequests = 0;
                uint64_t activeBans = 0;
                uint64_t activeClients = 0;
                std::chrono::system_clock::time_point lastBlockTime;
            };

            RateLimitStats getStats() const;

            // �̸� ���ǵ� ��Ģ��
            static LimitRule loginRule() { return LimitRule(5, std::chrono::minutes(5), std::chrono::minutes(15)); }
            static LimitRule messageRule() { return LimitRule(60, std::chrono::minutes(1)); }
            static LimitRule roomCreateRule() { return LimitRule(10, std::chrono::minutes(10)); }
            static LimitRule gameActionRule() { return LimitRule(30, std::chrono::seconds(30)); }

        private:
            // Ŭ���̾�Ʈ�� ��û ���
            struct ClientRecord {
                std::vector<std::chrono::steady_clock::time_point> requests;
                std::chrono::steady_clock::time_point lastRequest;
                std::chrono::steady_clock::time_point bannedUntil;
                std::string banReason;
                int violationCount = 0;
            };

            // ī�װ��� Ŭ���̾�Ʈ ���
            struct CategoryData {
                LimitRule rule;
                std::unordered_map<std::string, ClientRecord> clients;
                mutable std::mutex mutex;

                explicit CategoryData(const LimitRule& r) : rule(r) {}
            };

            // ���� ���� �Լ���
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

            // ���
            mutable RateLimitStats m_stats;
            mutable std::mutex m_statsMutex;

            // ���� ����
            std::chrono::steady_clock::time_point m_lastCleanup;
            std::chrono::minutes m_cleanupInterval{ 10 };
        };

        // ========================================
        // ����Ʈ ������ �Ŵ��� (�۷ι� �ν��Ͻ�)
        // ========================================
        class RateLimiterManager {
        public:
            static RateLimiterManager& getInstance();

            // �ʱ�ȭ (�⺻ ��Ģ ����)
            void initialize();

            // ����Ʈ ������ ����
            RateLimiter& getRateLimiter() { return m_rateLimiter; }

            // ���� �Լ���
            bool checkLogin(const std::string& clientId);
            bool checkMessage(const std::string& clientId);
            bool checkRoomCreate(const std::string& clientId);
            bool checkGameAction(const std::string& clientId);
            bool checkIPAddress(const std::string& ipAddress, const std::string& category);

            // ���� ����
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
        // ���� ��ũ�ε�
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