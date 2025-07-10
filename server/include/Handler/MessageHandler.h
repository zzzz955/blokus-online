#pragma once

#pragma once

#include <string>
#include <memory>
#include <functional>
#include <unordered_map>
#include <cstdint>

// 기존 정의 사용
#include "common/ServerTypes.h"

// Protobuf 전방 선언 (blokus 네임스페이스)
namespace google::protobuf {
    class Message;
}

namespace blokus {
    class MessageWrapper;     // message_wrapper.proto에서 생성
    enum MessageType : int;   // message_wrapper.proto에서 생성
}

namespace Blokus::Server {

    // 전방 선언 (순환 참조 방지)
    class Session;

    // 메시지 이벤트 콜백 타입들
    using AuthCallback = std::function<void(const std::string& sessionId, const std::string& username, bool success)>;
    using RoomCallback = std::function<void(const std::string& sessionId, const std::string& action, const std::string& data)>;
    using ChatCallback = std::function<void(const std::string& sessionId, const std::string& message)>;

    // 단순화된 메시지 핸들러 클래스 (브로커 역할만)
    class MessageHandler {
    public:
        explicit MessageHandler(Session* session);
        ~MessageHandler();

        // 메시지 처리 (현재: 텍스트 우선)
        void handleMessage(const std::string& rawMessage);

        // 콜백 설정 (GameServer에서 설정)
        void setAuthCallback(AuthCallback callback) { authCallback_ = callback; }
        void setRoomCallback(RoomCallback callback) { roomCallback_ = callback; }
        void setChatCallback(ChatCallback callback) { chatCallback_ = callback; }

        // 응답 전송 (현재: 텍스트 기반)
        void sendTextMessage(const std::string& message);
        void sendError(const std::string& errorMessage);

        // TODO: 향후 Protobuf 지원
        void sendProtobufMessage(blokus::MessageType type, const google::protobuf::Message& payload);

    private:
        // 현재 단계: 텍스트 메시지 처리
        void handleTextMessage(const std::string& rawMessage);
        void handleAuthMessage(const std::string& authData);
        void handleRoomMessage(const std::string& roomData);
        void handleChatMessage(const std::string& chatData);

        // TODO: 2단계에서 구현 예정 (현재는 사용하지 않음)
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
        Session* session_;  // 소유하지 않음, 단순 참조

        // 시퀀스 관리
        uint32_t sequenceId_{ 0 };
        uint32_t lastReceivedSequence_{ 0 };

        // 콜백들 (GameServer와의 통신)
        AuthCallback authCallback_;
        RoomCallback roomCallback_;
        ChatCallback chatCallback_;

        // 메시지 라우팅 테이블
        std::unordered_map<int, std::function<void(const blokus::MessageWrapper&)>> handlers_;
    };

} // namespace Blokus::Server