#pragma once

#pragma once

#include <string>
#include <memory>
#include <functional>
#include <unordered_map>
#include <cstdint>

// ���� ���� ���
#include "common/ServerTypes.h"

// Protobuf ���� ���� (blokus ���ӽ����̽�)
namespace google::protobuf {
    class Message;
}

namespace blokus {
    class MessageWrapper;     // message_wrapper.proto���� ����
    enum MessageType : int;   // message_wrapper.proto���� ����
}

namespace Blokus::Server {

    // ���� ���� (��ȯ ���� ����)
    class Session;

    // �޽��� �̺�Ʈ �ݹ� Ÿ�Ե�
    using AuthCallback = std::function<void(const std::string& sessionId, const std::string& username, bool success)>;
    using RoomCallback = std::function<void(const std::string& sessionId, const std::string& action, const std::string& data)>;
    using ChatCallback = std::function<void(const std::string& sessionId, const std::string& message)>;

    // �ܼ�ȭ�� �޽��� �ڵ鷯 Ŭ���� (���Ŀ ���Ҹ�)
    class MessageHandler {
    public:
        explicit MessageHandler(Session* session);
        ~MessageHandler();

        // �޽��� ó�� (����: �ؽ�Ʈ �켱)
        void handleMessage(const std::string& rawMessage);

        // �ݹ� ���� (GameServer���� ����)
        void setAuthCallback(AuthCallback callback) { authCallback_ = callback; }
        void setRoomCallback(RoomCallback callback) { roomCallback_ = callback; }
        void setChatCallback(ChatCallback callback) { chatCallback_ = callback; }

        // ���� ���� (����: �ؽ�Ʈ ���)
        void sendTextMessage(const std::string& message);
        void sendError(const std::string& errorMessage);

        // TODO: ���� Protobuf ����
        void sendProtobufMessage(blokus::MessageType type, const google::protobuf::Message& payload);

    private:
        // ���� �ܰ�: �ؽ�Ʈ �޽��� ó��
        void handleTextMessage(const std::string& rawMessage);
        void handleAuthMessage(const std::string& authData);
        void handleRoomMessage(const std::string& roomData);
        void handleChatMessage(const std::string& chatData);

        // TODO: 2�ܰ迡�� ���� ���� (����� ������� ����)
        /*
        bool parseProtobufMessage(const std::string& data, blokus::MessageWrapper* wrapper);
        void handleProtobufMessage(const blokus::MessageWrapper* wrapper);
        void routeAuthMessage(const blokus::MessageWrapper* wrapper);
        void routeRoomMessage(const blokus::MessageWrapper* wrapper);
        void routeChatMessage(const blokus::MessageWrapper* wrapper);
        void routeHeartbeat(const blokus::MessageWrapper* wrapper);
        void sendAckResponse(uint32_t sequenceId, bool success, const std::string& errorMessage);
        bool validateMessage(const blokus::MessageWrapper* wrapper);
        std::string extractPayloadData(const blokus::MessageWrapper* wrapper);
        */

    private:
        Session* session_;  // �������� ����, �ܼ� ����

        // ������ ����
        uint32_t sequenceId_{ 0 };
        uint32_t lastReceivedSequence_{ 0 };

        // �ݹ�� (GameServer���� ���)
        AuthCallback authCallback_;
        RoomCallback roomCallback_;
        ChatCallback chatCallback_;

        // �޽��� ����� ���̺�
        std::unordered_map<int, std::function<void(const blokus::MessageWrapper&)>> handlers_;
    };

} // namespace Blokus::Server