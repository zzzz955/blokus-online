#pragma once

#include "common/ServerTypes.h"
#include <boost/asio.hpp>
#include <memory>
#include <unordered_map>
#include <unordered_set>
#include <mutex>
#include <shared_mutex>

namespace Blokus {
    namespace Server {

        // ���� ����
        class GameServer;

        // ========================================
        // ��Ʈ��ũ ������ Ŭ����
        // ========================================
        class NetworkManager {
        public:
            explicit NetworkManager(boost::asio::io_context& ioContext, GameServer* server);
            ~NetworkManager();

            // ��Ʈ��ũ ����
            void broadcastToRoom(int roomId, const std::string& message);
            void broadcastToAll(const std::string& message, const std::string& excludeClientId = "");
            void sendToClient(const std::string& clientId, const std::string& message);
            void disconnectClient(const std::string& clientId);

            // Ŭ���̾�Ʈ ����
            void addClient(ClientSessionPtr client);
            void removeClient(const std::string& clientId);
            ClientSessionPtr getClient(const std::string& clientId);

            // �� ��� ����
            void addClientToRoom(const std::string& clientId, int roomId);
            void removeClientFromRoom(const std::string& clientId, int roomId);
            std::vector<std::string> getClientsInRoom(int roomId) const;

            // ���� ��ȸ
            size_t getConnectedClientsCount() const;
            size_t getRoomClientCount(int roomId) const;
            std::vector<std::string> getAllClientIds() const;

            // ���� ǰ�� ����
            void updateClientLatency(const std::string& clientId, double latencyMs);
            double getClientLatency(const std::string& clientId) const;
            void checkConnectionHealth();

            // �޽��� ť ����
            void enableMessageQueue(bool enable) { m_messageQueueEnabled = enable; }
            void flushMessageQueue();
            size_t getQueuedMessageCount() const;

        private:
            // Ŭ���̾�Ʈ ��Ÿ������
            struct ClientMetadata {
                ClientSessionPtr session;
                int currentRoomId = -1;
                double latencyMs = 0.0;
                std::chrono::steady_clock::time_point lastPing;
                bool isHealthy = true;

                ClientMetadata() = default;
                explicit ClientMetadata(ClientSessionPtr sess)
                    : session(sess), lastPing(std::chrono::steady_clock::now()) {
                }
            };

            // �޽��� ť ������
            struct QueuedMessage {
                std::string targetClientId;
                std::string message;
                std::chrono::steady_clock::time_point timestamp;
                int priority = 0;  // �������� �켱���� ����

                QueuedMessage(const std::string& target, const std::string& msg, int prio = 0)
                    : targetClientId(target), message(msg), priority(prio),
                    timestamp(std::chrono::steady_clock::now()) {
                }
            };

            // ���� ���� �Լ���
            void doSendToClient(const std::string& clientId, const std::string& message);
            void queueMessage(const std::string& clientId, const std::string& message, int priority = 0);
            void processMessageQueue();
            void removeClientFromAllRooms(const std::string& clientId);
            bool isClientHealthy(const std::string& clientId) const;

        private:
            boost::asio::io_context& m_ioContext;
            GameServer* m_server;

            // Ŭ���̾�Ʈ ����
            std::unordered_map<std::string, ClientMetadata> m_clients;
            mutable std::shared_mutex m_clientsMutex;

            // �溰 Ŭ���̾�Ʈ ����
            std::unordered_map<int, std::unordered_set<std::string>> m_roomClients;  // roomId -> clientIds
            mutable std::shared_mutex m_roomClientsMutex;

            // �޽��� ť
            std::vector<QueuedMessage> m_messageQueue;
            std::mutex m_messageQueueMutex;
            bool m_messageQueueEnabled = false;
            size_t m_maxQueueSize = 10000;

            // ���� �ǰ��� ����
            std::chrono::seconds m_healthCheckInterval{ 30 };
            std::chrono::seconds m_maxPingAge{ 60 };
            boost::asio::steady_timer m_healthCheckTimer;

            // ���
            std::atomic<uint64_t> m_messagesSent{ 0 };
            std::atomic<uint64_t> m_messagesQueued{ 0 };
            std::atomic<uint64_t> m_messagesDropped{ 0 };
        };

    } // namespace Server
} // namespace Blokus