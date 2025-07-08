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

            // 메시지 처리
            MessageResult processMessage(ClientSessionPtr client, const std::string& message);

            // 핸들러 등록
            void registerHandler(const std::string& messageType, MessageHandler handler);

        private:
            // 개별 메시지 핸들러들
            MessageResult handleAuthentication(ClientSessionPtr client, const std::string& payload);
            MessageResult handleCreateRoom(ClientSessionPtr client, const std::string& payload);
            MessageResult handleJoinRoom(ClientSessionPtr client, const std::string& payload);
            MessageResult handleGameMove(ClientSessionPtr client, const std::string& payload);
            MessageResult handleChat(ClientSessionPtr client, const std::string& payload);

            std::unordered_map<std::string, std::function<MessageResult(ClientSessionPtr, const std::string&)>> handlers_;
        };

    } // namespace Server
} // namespace Blokus