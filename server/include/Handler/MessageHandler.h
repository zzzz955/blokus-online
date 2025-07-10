#pragma once

#include <string>
#include <memory>
#include <functional>
#include <unordered_map>

// ���� ���� ���
#include "common/ServerTypes.h"

namespace Blokus::Server {

    // ���� ����
    class Session;
    class GameServer;

    // ServerTypes.h�� MessageType ���

    // �޽��� ����ü
    struct Message {
        MessageType type;
        std::string data;
        std::string sessionId;

        Message(MessageType t = MessageType::Unknown, const std::string& d = "", const std::string& sid = "")
            : type(t), data(d), sessionId(sid) {
        }
    };

    // �޽��� �ڵ鷯 Ŭ����
    class MessageHandler {
    public:
        explicit MessageHandler(Session* session, GameServer* server);
        ~MessageHandler();

        // �޽��� ó��
        void handleMessage(const std::string& rawMessage);
        void sendResponse(MessageType type, const std::string& data);
        void sendError(const std::string& errorMessage);

    private:
        // �޽��� �Ľ�
        Message parseMessage(const std::string& rawMessage);
        std::string serializeMessage(MessageType type, const std::string& data);

        // ���� �޽��� �ڵ鷯��
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

        // ��ƿ��Ƽ
        MessageType stringToMessageType(const std::string& typeStr);
        std::string messageTypeToString(MessageType type);
        bool validateMessage(const Message& msg);

    private:
        Session* session_;
        GameServer* server_;

        // �޽��� �ڵ鷯 ��
        std::unordered_map<MessageType, std::function<void(const Message&)>> handlers_;

        void initializeHandlers();
    };

} // namespace Blokus::Server