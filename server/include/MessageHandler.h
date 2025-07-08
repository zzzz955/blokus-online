#pragma once

#include <memory>
#include <string>
#include <vector>

namespace Blokus {
    namespace Server {

        // 전방 선언
        class Session;

        // 메시지 처리를 담당하는 클래스 (Protobuf 기반)
        class MessageHandler {
        public:
            explicit MessageHandler(std::shared_ptr<Session> session);
            ~MessageHandler() = default;

            // 메시지 처리 (Protobuf 바이너리 데이터)
            void handleMessage(const std::vector<uint8_t>& messageData);

        private:
            // 개별 메시지 처리 함수들
            void handleLoginRequest(const std::vector<uint8_t>& data);   // 로그인 요청
            void handleRegisterRequest(const std::vector<uint8_t>& data); // 회원가입 요청
            void handleCreateRoomRequest(const std::vector<uint8_t>& data); // 방 생성 요청
            void handleJoinRoomRequest(const std::vector<uint8_t>& data); // 방 참여 요청
            void handleLeaveRoomRequest(const std::vector<uint8_t>& data); // 방 나가기 요청
            void handleGameAction(const std::vector<uint8_t>& data);     // 게임 액션 처리
            void handleChatMessage(const std::vector<uint8_t>& data);    // 채팅 메시지 처리
            void handleHeartbeat(const std::vector<uint8_t>& data);      // 하트비트 처리

            // 응답 전송 헬퍼 (Protobuf 메시지를 직렬화해서 전송)
            void sendLoginResponse(bool success, const std::string& message, uint32_t userId = 0);
            void sendRegisterResponse(bool success, const std::string& message);
            void sendCreateRoomResponse(bool success, const std::string& message, uint32_t roomId = 0);
            void sendJoinRoomResponse(bool success, const std::string& message, uint32_t roomId = 0);
            void sendLeaveRoomResponse(bool success, const std::string& message);
            void sendGameActionResponse(bool success, const std::string& action);
            void sendChatBroadcast(const std::string& username, const std::string& message, uint32_t roomId);
            void sendHeartbeatResponse();
            void sendErrorMessage(const std::string& error);

            // Protobuf 메시지 파싱/직렬화 헬퍼
            bool parseMessage(const std::vector<uint8_t>& data, const std::string& expectedType);
            std::vector<uint8_t> serializeMessage(const google::protobuf::Message& message);

        private:
            std::weak_ptr<Session> m_session;                     // 세션 약한 참조
        };

    } // namespace Server
} // namespace Blokus