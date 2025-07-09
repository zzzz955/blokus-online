#pragma once

#include "common/ServerTypes.h"
#include <functional>
#include <unordered_map>
#include <string>
#include <memory>

// Protobuf ���� ���
#include <google/protobuf/message.h>

namespace Blokus {
    namespace Server {

        // ���� ����
        class GameServer;

        // ========================================
        // �޽��� �ڵ鷯 Ŭ���� (ProtocolHandler ��� ����)
        // ========================================
        class MessageHandler {
        public:
            explicit MessageHandler(GameServer* server);
            ~MessageHandler();

            // �޽��� ó�� (���� ������)
            MessageResult processMessage(ClientSessionPtr client, const std::string& message);

            // Protobuf ����ȭ/������ȭ (ProtocolHandler ��� ����)
            std::string serializeMessage(const google::protobuf::Message& message);
            bool deserializeMessage(const std::string& data, google::protobuf::Message& message);

            // �޽��� ����/����
            std::string wrapMessage(MessageType messageType, const std::string& payload);
            bool unwrapMessage(const std::string& data, MessageType& messageType, std::string& payload);

            // �ڵ鷯 ���
            void registerHandler(MessageType messageType, MessageHandler handler);

        private:
            // ���� �޽��� �ڵ鷯��
            MessageResult handleAuthentication(ClientSessionPtr client, const std::string& payload);
            MessageResult handleCreateRoom(ClientSessionPtr client, const std::string& payload);
            MessageResult handleJoinRoom(ClientSessionPtr client, const std::string& payload);
            MessageResult handleLeaveRoom(ClientSessionPtr client, const std::string& payload);
            MessageResult handleGameMove(ClientSessionPtr client, const std::string& payload);
            MessageResult handleChat(ClientSessionPtr client, const std::string& payload);
            MessageResult handleHeartbeat(ClientSessionPtr client, const std::string& payload);
            MessageResult handleLobbyRequest(ClientSessionPtr client, const std::string& payload);

            // �޽��� ����/���� (������)
            std::string compressMessage(const std::string& data);
            std::string decompressMessage(const std::string& data);

            // �޽��� ����
            bool validateMessage(const std::string& message);
            bool validateSession(ClientSessionPtr client);

            // ���� ���� ����
            std::string createErrorResponse(ServerErrorCode errorCode, const std::string& message);

        private:
            GameServer* m_server;
            std::unordered_map<MessageType, std::function<MessageResult(ClientSessionPtr, const std::string&)>> m_handlers;

            // ����
            bool m_compressionEnabled = false;
            size_t m_maxMessageSize = MAX_MESSAGE_SIZE;
        };

    } // namespace Server
} // namespace Blokus