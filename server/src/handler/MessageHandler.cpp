#include "handler/MessageHandler.h"
#include "core/Session.h"
#include <spdlog/spdlog.h>

namespace Blokus::Server {

    // ========================================
    // 생성자 및 소멸자
    // ========================================

    MessageHandler::MessageHandler(Session* session)
        : session_(session)
    {
        // 핸들러 테이블 초기화는 하지 않음 (단순화)
        spdlog::debug("MessageHandler 생성: 세션 {}",
            session_ ? session_->getSessionId() : "nullptr");
    }

    MessageHandler::~MessageHandler() {
        spdlog::debug("MessageHandler 소멸");
    }

    // ========================================
    // 메시지 처리 (현재: 텍스트 우선)
    // ========================================

    void MessageHandler::handleMessage(const std::string& rawMessage) {
        if (!session_) {
            spdlog::error("Session이 null입니다");
            return;
        }

        // 현재 단계: 텍스트 메시지만 처리
        handleTextMessage(rawMessage);

        // TODO: 향후 Protobuf 지원 추가
        // if (isProtobufMessage(rawMessage)) {
        //     handleProtobufMessage(rawMessage);
        // } else {
        //     handleTextMessage(rawMessage);
        // }
    }

    void MessageHandler::handleTextMessage(const std::string& rawMessage) {
        try {
            spdlog::debug("텍스트 메시지 수신 ({}): {}",
                session_->getSessionId(),
                rawMessage.length() > 100 ? rawMessage.substr(0, 100) + "..." : rawMessage);

            // 간단한 명령어 파싱
            if (rawMessage == "ping") {
                sendTextMessage("pong");
            }
            else if (rawMessage.starts_with("auth:")) {
                handleAuthMessage(rawMessage.substr(5)); // "auth:" 제거
            }
            else if (rawMessage.starts_with("room:")) {
                handleRoomMessage(rawMessage.substr(5)); // "room:" 제거
            }
            else if (rawMessage.starts_with("chat:")) {
                handleChatMessage(rawMessage.substr(5)); // "chat:" 제거
            }
            else {
                spdlog::warn("알 수 없는 메시지 형식: {}", rawMessage);
                sendError("Unknown message format. Try: ping, auth:user:pass, room:list, chat:message");
            }

        }
        catch (const std::exception& e) {
            spdlog::error("텍스트 메시지 처리 중 오류: {}", e.what());
            sendError("Text message processing error");
        }
    }

    // ========================================
    // 메시지 전송
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
    // 개별 메시지 처리 (콜백 호출)
    // ========================================

    void MessageHandler::handleAuthMessage(const std::string& authData) {
        if (!session_) return;

        // 간단한 파싱: "username:password"
        size_t colonPos = authData.find(':');
        if (colonPos == std::string::npos) {
            sendError("Invalid auth format. Use 'username:password'");
            return;
        }

        std::string username = authData.substr(0, colonPos);
        std::string password = authData.substr(colonPos + 1);

        spdlog::info("인증 시도: {} (세션: {})", username, session_->getSessionId());

        // 간단한 검증
        bool success = (username.length() >= 3 && password.length() >= 4);

        if (success) {
            session_->setAuthenticated("user_" + username, username);
            sendTextMessage("AUTH_SUCCESS:" + username);
        }
        else {
            sendTextMessage("AUTH_FAILED:Invalid credentials");
        }

        // 콜백 호출 (GameServer에 알림)
        if (authCallback_) {
            authCallback_(session_->getSessionId(), username, success);
        }
    }

    void MessageHandler::handleRoomMessage(const std::string& roomData) {
        if (!session_->isAuthenticated()) {
            sendError("Authentication required");
            return;
        }

        spdlog::info("방 관련 요청: {} (세션: {})", roomData, session_->getSessionId());

        // 간단한 방 명령어 처리
        if (roomData == "list") {
            // 방 목록 요청
            if (roomCallback_) {
                roomCallback_(session_->getSessionId(), "list", "");
            }
        }
        else if (roomData.starts_with("create:")) {
            // 방 생성 요청
            std::string roomName = roomData.substr(7); // "create:" 제거
            if (roomCallback_) {
                roomCallback_(session_->getSessionId(), "create", roomName);
            }
        }
        else if (roomData.starts_with("join:")) {
            // 방 참가 요청
            std::string roomId = roomData.substr(5); // "join:" 제거
            if (roomCallback_) {
                roomCallback_(session_->getSessionId(), "join", roomId);
            }
        }
        else if (roomData == "leave") {
            // 방 나가기 요청
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

        spdlog::info("채팅 메시지: {} -> {}", session_->getUsername(), chatData);

        // 콜백 호출 (브로드캐스트를 위해)
        if (chatCallback_) {
            chatCallback_(session_->getSessionId(), chatData);
        }
    }

    // ========================================
    // Protobuf 지원 (향후 확장용)
    // ========================================

    void MessageHandler::sendProtobufMessage(blokus::MessageType type, const google::protobuf::Message& payload) {
        // TODO: protobuf 메시지 구현
        spdlog::warn("Protobuf 메시지는 아직 구현되지 않았습니다");
        sendError("Protobuf not implemented yet");
    }

    //bool MessageHandler::parseProtobufMessage(const std::string& data, blokus::MessageWrapper& wrapper) {
    //    try {
    //        return wrapper.ParseFromString(data);
    //    }
    //    catch (const std::exception& e) {
    //        spdlog::debug("Protobuf 파싱 실패: {}", e.what());
    //        return false;
    //    }
    //}

    //void MessageHandler::handleProtobufMessage(const blokus::MessageWrapper& wrapper) {
    //    if (!validateMessage(wrapper)) {
    //        sendError("Invalid protobuf message");
    //        return;
    //    }

    //    // 시퀀스 ID 업데이트
    //    lastReceivedSequence_ = wrapper.sequence_id();

    //    // 메시지 타입별 라우팅
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

    //    // ACK 응답 (필요한 경우)
    //    if (wrapper.requires_ack()) {
    //        sendAckResponse(wrapper.sequence_id(), true, "");
    //    }
    //}

    //void MessageHandler::sendAckResponse(uint32_t sequenceId, bool success, const std::string& errorMessage) {
    //    // TODO: Protobuf ACK 메시지 구현
    //    std::string ackMsg = success ? "ACK:" + std::to_string(sequenceId) :
    //        "NACK:" + std::to_string(sequenceId) + ":" + errorMessage;
    //    sendTextMessage(ackMsg);
    //}

    //void MessageHandler::routeAuthMessage(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf 인증 메시지 라우팅
    //}

    //void MessageHandler::routeRoomMessage(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf 방 메시지 라우팅
    //}

    //void MessageHandler::routeChatMessage(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf 채팅 메시지 라우팅
    //}

    //void MessageHandler::routeHeartbeat(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf 하트비트 처리
    //}

    //bool MessageHandler::validateMessage(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf 메시지 검증
    //    return true;
    //}

    //std::string MessageHandler::extractPayloadData(const blokus::MessageWrapper& wrapper) {
    //    // TODO: protobuf 페이로드 추출
    //    return "";
    //}

} // namespace Blokus::Server