#pragma once

#include "ServerTypes.h"
#include <boost/asio.hpp>
#include <memory>

namespace Blokus {
    namespace Server {

        class NetworkManager {
        public:
            explicit NetworkManager(boost::asio::io_context& ioContext);
            ~NetworkManager();

            // ��Ʈ��ũ ����
            void broadcastToRoom(int roomId, const std::string& message);
            void sendToClient(const std::string& clientId, const std::string& message);
            void disconnectClient(const std::string& clientId);

            // Ŭ���̾�Ʈ ����
            void addClient(ClientSessionPtr client);
            void removeClient(const std::string& clientId);
            ClientSessionPtr getClient(const std::string& clientId);

            // ���
            size_t getConnectedClients() const;

        private:
            boost::asio::io_context& ioContext_;
            std::unordered_map<std::string, ClientSessionPtr> clients_;
            mutable std::mutex clientsMutex_;
        };

    } // namespace Server
} // namespace Blokus