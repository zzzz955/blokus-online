#pragma once

#include "common/ServerTypes.h"
#include <functional>
#include <unordered_map>
#include <vector>
#include <memory>
#include <mutex>
#include <queue>
#include <thread>

namespace Blokus {
    namespace Server {

        // ========================================
        // �̺�Ʈ �⺻ Ŭ����
        // ========================================
        class Event {
        public:
            virtual ~Event() = default;
            virtual std::string getType() const = 0;
            virtual std::chrono::system_clock::time_point getTimestamp() const {
                return m_timestamp;
            }

        protected:
            std::chrono::system_clock::time_point m_timestamp = std::chrono::system_clock::now();
        };

        // ========================================
        // ��ü���� �̺�Ʈ Ÿ�Ե�
        // ========================================

        // Ŭ���̾�Ʈ ���� �̺�Ʈ
        class ClientConnectedEvent : public Event {
        public:
            ClientConnectedEvent(uint32_t sessionId, const std::string& remoteAddress)
                : m_sessionId(sessionId), m_remoteAddress(remoteAddress) {
            }

            std::string getType() const override { return "ClientConnected"; }
            uint32_t getSessionId() const { return m_sessionId; }
            const std::string& getRemoteAddress() const { return m_remoteAddress; }

        private:
            uint32_t m_sessionId;
            std::string m_remoteAddress;
        };

        // Ŭ���̾�Ʈ ���� ���� �̺�Ʈ
        class ClientDisconnectedEvent : public Event {
        public:
            ClientDisconnectedEvent(uint32_t sessionId, const std::string& reason)
                : m_sessionId(sessionId), m_reason(reason) {
            }

            std::string getType() const override { return "ClientDisconnected"; }
            uint32_t getSessionId() const { return m_sessionId; }
            const std::string& getReason() const { return m_reason; }

        private:
            uint32_t m_sessionId;
            std::string m_reason;
        };

        // ���� ���� �̺�Ʈ
        class GameStartedEvent : public Event {
        public:
            GameStartedEvent(int roomId, const std::vector<std::string>& playerIds)
                : m_roomId(roomId), m_playerIds(playerIds) {
            }

            std::string getType() const override { return "GameStarted"; }
            int getRoomId() const { return m_roomId; }
            const std::vector<std::string>& getPlayerIds() const { return m_playerIds; }

        private:
            int m_roomId;
            std::vector<std::string> m_playerIds;
        };

        // ���� ���� �̺�Ʈ
        class GameFinishedEvent : public Event {
        public:
            GameFinishedEvent(int roomId, const std::string& winnerId, std::chrono::seconds duration)
                : m_roomId(roomId), m_winnerId(winnerId), m_duration(duration) {
            }

            std::string getType() const override { return "GameFinished"; }
            int getRoomId() const { return m_roomId; }
            const std::string& getWinnerId() const { return m_winnerId; }
            std::chrono::seconds getDuration() const { return m_duration; }

        private:
            int m_roomId;
            std::string m_winnerId;
            std::chrono::seconds m_duration;
        };

        // ���� �̺�Ʈ
        class ErrorEvent : public Event {
        public:
            ErrorEvent(ServerErrorCode errorCode, const std::string& context, const std::string& details)
                : m_errorCode(errorCode), m_context(context), m_details(details) {
            }

            std::string getType() const override { return "Error"; }
            ServerErrorCode getErrorCode() const { return m_errorCode; }
            const std::string& getContext() const { return m_context; }
            const std::string& getDetails() const { return m_details; }

        private:
            ServerErrorCode m_errorCode;
            std::string m_context;
            std::string m_details;
        };

        // ========================================
        // �̺�Ʈ �ý��� Ŭ����
        // ========================================
        class EventSystem {
        public:
            using EventHandler = std::function<void(std::shared_ptr<Event>)>;
            using EventHandlerId = uint64_t;

            // �̱��� ����
            static EventSystem& getInstance();

            // �ʱ�ȭ �� ����
            void initialize();
            void shutdown();

            // �̺�Ʈ ����/���� ����
            EventHandlerId subscribe(const std::string& eventType, EventHandler handler);
            bool unsubscribe(EventHandlerId handlerId);
            void unsubscribeAll(const std::string& eventType);

            // �̺�Ʈ ����
            void publish(std::shared_ptr<Event> event);
            void publishAsync(std::shared_ptr<Event> event);

            // ��� ó�� (�����)
            void publishSync(std::shared_ptr<Event> event);

            // ť ����
            void processEventQueue();
            size_t getQueueSize() const;
            void clearQueue();

            // ����
            void setMaxQueueSize(size_t maxSize) { m_maxQueueSize = maxSize; }
            void enableAsyncProcessing(bool enable);

            // ���
            struct EventStats {
                uint64_t totalEventsPublished = 0;
                uint64_t totalEventsProcessed = 0;
                uint64_t eventsDropped = 0;
                uint64_t activeSubscriptions = 0;
                std::chrono::system_clock::time_point lastEventTime;
            };

            EventStats getStats() const;

        private:
            EventSystem() = default;
            ~EventSystem() = default;
            EventSystem(const EventSystem&) = delete;
            EventSystem& operator=(const EventSystem&) = delete;

            // ���� ����
            struct Subscription {
                EventHandlerId id;
                std::string eventType;
                EventHandler handler;
                bool isActive = true;
            };

            // �̺�Ʈ ó��
            void processEvent(std::shared_ptr<Event> event);
            void notifySubscribers(const std::string& eventType, std::shared_ptr<Event> event);

            // �񵿱� ó��
            void asyncProcessingLoop();

        private:
            // ������ ����
            std::unordered_map<std::string, std::vector<std::shared_ptr<Subscription>>> m_subscriptions;
            std::unordered_map<EventHandlerId, std::shared_ptr<Subscription>> m_handlerMap;
            mutable std::mutex m_subscriptionsMutex;

            // �̺�Ʈ ť
            std::queue<std::shared_ptr<Event>> m_eventQueue;
            mutable std::mutex m_queueMutex;
            std::condition_variable m_queueCondition;

            // �񵿱� ó��
            std::atomic<bool> m_asyncEnabled{ true };
            std::atomic<bool> m_running{ false };
            std::thread m_processingThread;

            // ����
            size_t m_maxQueueSize = 10000;
            EventHandlerId m_nextHandlerId{ 1 };

            // ���
            mutable EventStats m_stats;
            mutable std::mutex m_statsMutex;
        };

        // ========================================
        // ���� ��ũ�ε�
        // ========================================
#define PUBLISH_EVENT(eventPtr) \
            Blokus::Server::EventSystem::getInstance().publish(eventPtr)

#define PUBLISH_EVENT_ASYNC(eventPtr) \
            Blokus::Server::EventSystem::getInstance().publishAsync(eventPtr)

#define SUBSCRIBE_EVENT(eventType, handler) \
            Blokus::Server::EventSystem::getInstance().subscribe(eventType, handler)

        // �̺�Ʈ ���� ���� ��ũ�ε�
#define CREATE_CLIENT_CONNECTED_EVENT(sessionId, address) \
            std::make_shared<Blokus::Server::ClientConnectedEvent>(sessionId, address)

#define CREATE_CLIENT_DISCONNECTED_EVENT(sessionId, reason) \
            std::make_shared<Blokus::Server::ClientDisconnectedEvent>(sessionId, reason)

#define CREATE_GAME_STARTED_EVENT(roomId, playerIds) \
            std::make_shared<Blokus::Server::GameStartedEvent>(roomId, playerIds)

#define CREATE_GAME_FINISHED_EVENT(roomId, winnerId, duration) \
            std::make_shared<Blokus::Server::GameFinishedEvent>(roomId, winnerId, duration)

#define CREATE_ERROR_EVENT(errorCode, context, details) \
            std::make_shared<Blokus::Server::ErrorEvent>(errorCode, context, details)

    } // namespace Server
} // namespace Blokus