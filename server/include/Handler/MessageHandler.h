#pragma once

#include "ServerTypes.h"
#include <functional>
#include <unordered_map>
#include <string>

namespace Blokus {
    namespace Server {

        class MessageHandler {
        public:
            MessageHandler();
            ~MessageHandler();

            // �޽��� ó��
            MessageResult processMessage(ClientSessionPtr client, const std::string& message);

            // �ڵ鷯 ���
            void registerHandler(const std::string& messageType, MessageHandler handler);

        private:
            // ���� �޽��� �ڵ鷯��
            MessageResult handleAuthentication(ClientSessionPtr client, const std::string& payload);
            MessageResult handleCreateRoom(ClientSessionPtr client, const std::string& payload);
            MessageResult handleJoinRoom(ClientSessionPtr client, const std::string& payload);
            MessageResult handleGameMove(ClientSessionPtr client, const std::string& payload);
            MessageResult handleChat(ClientSessionPtr client, const std::string& payload);

            std::unordered_map<std::string, std::function<MessageResult(ClientSessionPtr, const std::string&)>> handlers_;
        };

    } // namespace Server
} // namespace Blokus