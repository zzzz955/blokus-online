#pragma once

#include "common/ServerTypes.h"
#include <functional>
#include <unordered_map>
#include <string>
#include <memory>

// Protobuf 관련 헤더
#include <google/protobuf/message.h>

namespace Blokus {
    namespace Server {

        // 전방 선언
        class GameServer;

        // ========================================
        // 메시지 핸들러 클래스 (ProtocolHandler 기능 통합)
        // ========================================
        class MessageHandler {
        public:
            explicit MessageHandler(GameServer* server);
            ~MessageHandler();

            // 메시지 처리 (메인 진입점)
            MessageResult processMessage(ClientSessionPtr client, const std::string& message);

            // Protobuf 직렬화/역직렬화 (ProtocolHandler 기능 통합)
            std::string serializeMessage(const google::protobuf::Message& message);
            bool deserializeMessage(const std::string& data, google::protobuf::Message& message);

            // 메시지 래핑/언래핑
            std::string wrapMessage(MessageType messageType, const std::string& payload);
            bool unwrapMessage(const std::string& data, MessageType& messageType, std::string& payload);

            // 핸들러 등록
            void registerHandler(MessageType messageType, MessageHandler handler);

        private:
            // 개별 메시지 핸들러들
            MessageResult handleAuthentication(ClientSessionPtr client, const std::string& payload);
            MessageResult handleCreateRoom(ClientSessionPtr client, const std::string& payload);
            MessageResult handleJoinRoom(ClientSessionPtr client, const std::string& payload);
            MessageResult handleLeaveRoom(ClientSessionPtr client, const std::string& payload);
            MessageResult handleGameMove(ClientSessionPtr client, const std::string& payload);
            MessageResult handleChat(ClientSessionPtr client, const std::string& payload);
            MessageResult handleHeartbeat(ClientSessionPtr client, const std::string& payload);
            MessageResult handleLobbyRequest(ClientSessionPtr client, const std::string& payload);

            // 메시지 압축/해제 (선택적)
            std::string compressMessage(const std::string& data);
            std::string decompressMessage(const std::string& data);

            // 메시지 검증
            bool validateMessage(const std::string& message);
            bool validateSession(ClientSessionPtr client);

            // 에러 응답 생성
            std::string createErrorResponse(ServerErrorCode errorCode, const std::string& message);

        private:
            GameServer* m_server;
            std::unordered_map<MessageType, std::function<MessageResult(ClientSessionPtr, const std::string&)>> m_handlers;

            // 설정
            bool m_compressionEnabled = false;
            size_t m_maxMessageSize = MAX_MESSAGE_SIZE;
        };

    } // namespace Server
} // namespace Blokus