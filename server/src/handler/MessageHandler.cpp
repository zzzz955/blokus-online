#include "handler/MessageHandler.h"
#include "core/Session.h"
#include <spdlog/spdlog.h>

namespace Blokus::Server {

    // ========================================
    // ������ �� �Ҹ���
    // ========================================

    MessageHandler::MessageHandler(Session* session)
        : session_(session)
    {
        // �ڵ鷯 ���̺� �ʱ�ȭ�� ���� ���� (�ܼ�ȭ)
        spdlog::debug("MessageHandler ����: ���� {}",
            session_ ? session_->getSessionId() : "nullptr");
    }

    MessageHandler::~MessageHandler() {
        spdlog::debug("MessageHandler �Ҹ�");
    }

    // ========================================
    // �޽��� ó�� (����: �ؽ�Ʈ �켱)
    // ========================================

    void MessageHandler::handleMessage(const std::string& rawMessage) {
        if (!session_) {
            spdlog::error("Session�� null�Դϴ�");
            return;
        }

        // ���� �ܰ�: �ؽ�Ʈ �޽����� ó��
        handleTextMessage(rawMessage);

        // TODO: ���� Protobuf ���� �߰�
        // if (isProtobufMessage(rawMessage)) {
        //     handleProtobufMessage(rawMessage);
        // } else {
        //     handleTextMessage(rawMessage);
        // }
    }

    void MessageHandler::handleTextMessage(const std::string& rawMessage) {
        try {
            spdlog::debug("�ؽ�Ʈ �޽��� ���� ({}): {}",
                session_->getSessionId(),
                rawMessage.length() > 100 ? rawMessage.substr(0, 100) + "..." : rawMessage);

            // ������ ��ɾ� �Ľ�
            if (rawMessage == "ping") {
                sendTextMessage("pong");
            }
            else if (rawMessage.starts_with("auth:")) {
                handleAuthMessage(rawMessage.substr(5)); // "auth:" ����
            }
            else if (rawMessage.starts_with("room:")) {
                handleRoomMessage(rawMessage.substr(5)); // "room:" ����
            }
            else if (rawMessage.starts_with("chat:")) {
                handleChatMessage(rawMessage.substr(5)); // "chat:" ����
            }
            else {
                spdlog::warn("�� �� ���� �޽��� ����: {}", rawMessage);
                sendError("Unknown message format. Try: ping, auth:user:pass, room:list, chat:message");
            }

        }
        catch (const std::exception& e) {
            spdlog::error("�ؽ�Ʈ �޽��� ó�� �� ����: {}", e.what());
            sendError("Text message processing error");
        }
    }

    // ========================================
    // �޽��� ����
    // ========================================

    void MessageHandler::sendTextMessage(const std::string& message) {
        if (session_) {
            session_->sendMessage(message);
        }
    }

    void MessageHandler::sendError(const std::string& errorMessage) {
        sendTextMessage("ERROR:" + errorMessage);
    }

    // ========================================
    // ���� �޽��� ó�� (�ݹ� ȣ��)
    // ========================================

    void MessageHandler::handleAuthMessage(const std::string& authData) {
        if (!session_) return;

        // ������ �Ľ�: "username:password"
        size_t colonPos = authData.find(':');
        if (colonPos == std::string::npos) {
            sendError("Invalid auth format. Use 'username:password'");
            return;
        }

        std::string username = authData.substr(0, colonPos);
        std::string password = authData.substr(colonPos + 1);

        spdlog::info("���� �õ�: {} (����: {})", username, session_->getSessionId());

        // ������ ����
        bool success = (username.length() >= 3 && password.length() >= 4);

        if (success) {
            session_->setAuthenticated("user_" + username, username);
            sendTextMessage("AUTH_SUCCESS:" + username);
        }
        else {
            sendTextMessage("AUTH_FAILED:Invalid credentials");
        }

        // �ݹ� ȣ�� (GameServer�� �˸�)
        if (authCallback_) {
            authCallback_(session_->getSessionId(), username, success);
        }
    }

    void MessageHandler::handleRoomMessage(const std::string& roomData) {
        if (!session_->isAuthenticated()) {
            sendError("Authentication required");
            return;
        }

        spdlog::info("�� ���� ��û: {} (����: {})", roomData, session_->getSessionId());

        // ������ �� ��ɾ� ó��
        if (roomData == "list") {
            // �� ��� ��û
            if (roomCallback_) {
                roomCallback_(session_->getSessionId(), "list", "");
            }
        }
        else if (roomData.starts_with("create:")) {
            // �� ���� ��û
            std::string roomName = roomData.substr(7); // "create:" ����
            if (roomCallback_) {
                roomCallback_(session_->getSessionId(), "create", roomName);
            }
        }
        else if (roomData.starts_with("join:")) {
            // �� ���� ��û
            std::string roomId = roomData.substr(5); // "join:" ����
            if (roomCallback_) {
                roomCallback_(session_->getSessionId(), "join", roomId);
            }
        }
        else if (roomData == "leave") {
            // �� ������ ��û
            if (roomCallback_) {
                roomCallback_(session_->getSessionId(), "leave", "");
            }
        }
        else {
            sendError("Unknown room command");
        }
    }

    void MessageHandler::handleChatMessage(const std::string& chatData) {
        if (!session_->isAuthenticated()) {
            sendError("Authentication required");
            return;
        }

        spdlog::info("ä�� �޽���: {} -> {}", session_->getUsername(), chatData);

        // �ݹ� ȣ�� (��ε�ĳ��Ʈ�� ����)
        if (chatCallback_) {
            chatCallback_(session_->getSessionId(), chatData);
        }
    }

    // ========================================
    // Protobuf ���� (���� Ȯ���)
    // ========================================

    void MessageHandler::sendProtobufMessage(blokus::MessageType type, const google::protobuf::Message& payload) {
        // TODO: protobuf �޽��� ����
        spdlog::warn("Protobuf �޽����� ���� �������� �ʾҽ��ϴ�");
        sendError("Protobuf not implemented yet");
    }

    //bool MessageHandler::parseProtobufMessage(const std::string& data, blokus::MessageWrapper& wrapper) {
    //    try {
    //        return wrapper.ParseFromString(data);
    //    }
    //    catch (const std::exception& e) {
    //        spdlog::debug("Protobuf �Ľ� ����: {}", e.what());
    //        return false;
    //    }
    //}

    //void MessageHandler::handleProtobufMessage(const blokus::MessageWrapper& wrapper) {
    //    if (!validateMessage(wrapper)) {
    //        sendError("Invalid protobuf message");
    //        return;
    //    }

    //    // ������ ID ������Ʈ
    //    lastReceivedSequence_ = wrapper.sequence_id();

    //    // �޽��� Ÿ�Ժ� �����
    //    switch (wrapper.type()) {
    //    case blokus::MESSAGE_TYPE_AUTH_REQUEST:
    //        routeAuthMessage(wrapper);
    //        break;
    //    case blokus::MESSAGE_TYPE_GET_ROOM_LIST_REQUEST:
    //    case blokus::MESSAGE_TYPE_CREATE_ROOM_REQUEST:
    //    case blokus::MESSAGE_TYPE_JOIN_ROOM_REQUEST:
    //    case blokus::MESSAGE_TYPE_LEAVE_ROOM_REQUEST:
    //        routeRoomMessage(wrapper);
    //        break;
    //    case blokus::MESSAGE_TYPE_CHAT_MESSAGE:
    //        routeChatMessage(wrapper);
    //        break;
    //    case blokus::MESSAGE_TYPE_HEARTBEAT:
    //        routeHeartbeat(wrapper);
    //        break;
    //    default:
    //        spdlog::warn("Unhandled protobuf message type: {}", wrapper.type());
    //        sendError("Unhandled message type");
    //    }

    //    // ACK ���� (�ʿ��� ���)
    //    if (wrapper.requires_ack()) {
    //        sendAckResponse(wrapper.sequence_id(), true, "");
    //    }
    //}

    //void MessageHandler::sendAckResponse(uint32_t sequenceId, bool success, const std::string& errorMessage) {
    //    // TODO: Protobuf ACK �޽��� ����
    //    std::string ackMsg = success ? "ACK:" + std::to_string(sequenceId) :
    //        "NACK:" + std::to_string(sequenceId) + ":" + errorMessage;
    //    sendTextMessage(ackMsg);
    //}

    //void MessageHandler::routeAuthMessage(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf ���� �޽��� �����
    //}

    //void MessageHandler::routeRoomMessage(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf �� �޽��� �����
    //}

    //void MessageHandler::routeChatMessage(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf ä�� �޽��� �����
    //}

    //void MessageHandler::routeHeartbeat(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf ��Ʈ��Ʈ ó��
    //}

    //bool MessageHandler::validateMessage(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf �޽��� ����
    //    return true;
    //}

    //std::string MessageHandler::extractPayloadData(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf ���̷ε� ����
    //    return "";
    //}

} // namespace Blokus::Server