#pragma once

#include <string>
#include <memory>
#include <functional>
#include <unordered_map>

// 기존 정의 사용
#include "common/ServerTypes.h"

namespace Blokus::Server {

    // 전방 선언
    class Session;
    class GameServer;

    // ServerTypes.h의 MessageType 사용

    // 메시지 구조체
    struct Message {
        MessageType type;
        std::string data;
        std::string sessionId;

        Message(MessageType t = MessageType::Unknown, const std::string& d = "", const std::string& sid = "")
            : type(t), data(d), sessionId(sid) {
        }
    };

    // 메시지 핸들러 클래스
    class MessageHandler {
    public:
        explicit MessageHandler(Session* session, GameServer* server);
        ~MessageHandler();

        // 메시지 처리
        void handleMessage(const std::string& rawMessage);
        void sendResponse(MessageType type, const std::string& data);
        void sendError(const std::string& errorMessage);

    private:
        // 메시지 파싱
        Message parseMessage(const std::string& rawMessage);
        std::string serializeMessage(MessageType type, const std::string& data);

        // 개별 메시지 핸들러들
        void handlePing(const Message& msg);
        void handleLogin(const Message& msg);
        void handleRegister(const Message& msg);
        void handleLogout(const Message& msg);

        void handleGetRoomList(const Message& msg);
        void handleCreateRoom(const Message& msg);
        void handleJoinRoom(const Message& msg);
        void handleLeaveRoom(const Message& msg);

        void handleStartGame(const Message& msg);
        void handleGameMove(const Message& msg);

        // 유틸리티
        MessageType stringToMessageType(const std::string& typeStr);
        std::string messageTypeToString(MessageType type);
        bool validateMessage(const Message& msg);

    private:
        Session* session_;
        GameServer* server_;

        // 메시지 핸들러 맵
        std::unordered_map<MessageType, std::function<void(const Message&)>> handlers_;

        void initializeHandlers();
    };

} // namespace Blokus::Server