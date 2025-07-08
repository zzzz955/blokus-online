#pragma once

#include <memory>
#include <string>
#include <vector>

namespace Blokus {
    namespace Server {

        // ���� ����
        class Session;

        // �޽��� ó���� ����ϴ� Ŭ���� (Protobuf ���)
        class MessageHandler {
        public:
            explicit MessageHandler(std::shared_ptr<Session> session);
            ~MessageHandler() = default;

            // �޽��� ó�� (Protobuf ���̳ʸ� ������)
            void handleMessage(const std::vector<uint8_t>& messageData);

        private:
            // ���� �޽��� ó�� �Լ���
            void handleLoginRequest(const std::vector<uint8_t>& data);   // �α��� ��û
            void handleRegisterRequest(const std::vector<uint8_t>& data); // ȸ������ ��û
            void handleCreateRoomRequest(const std::vector<uint8_t>& data); // �� ���� ��û
            void handleJoinRoomRequest(const std::vector<uint8_t>& data); // �� ���� ��û
            void handleLeaveRoomRequest(const std::vector<uint8_t>& data); // �� ������ ��û
            void handleGameAction(const std::vector<uint8_t>& data);     // ���� �׼� ó��
            void handleChatMessage(const std::vector<uint8_t>& data);    // ä�� �޽��� ó��
            void handleHeartbeat(const std::vector<uint8_t>& data);      // ��Ʈ��Ʈ ó��

            // ���� ���� ���� (Protobuf �޽����� ����ȭ�ؼ� ����)
            void sendLoginResponse(bool success, const std::string& message, uint32_t userId = 0);
            void sendRegisterResponse(bool success, const std::string& message);
            void sendCreateRoomResponse(bool success, const std::string& message, uint32_t roomId = 0);
            void sendJoinRoomResponse(bool success, const std::string& message, uint32_t roomId = 0);
            void sendLeaveRoomResponse(bool success, const std::string& message);
            void sendGameActionResponse(bool success, const std::string& action);
            void sendChatBroadcast(const std::string& username, const std::string& message, uint32_t roomId);
            void sendHeartbeatResponse();
            void sendErrorMessage(const std::string& error);

            // Protobuf �޽��� �Ľ�/����ȭ ����
            bool parseMessage(const std::vector<uint8_t>& data, const std::string& expectedType);
            std::vector<uint8_t> serializeMessage(const google::protobuf::Message& message);

        private:
            std::weak_ptr<Session> m_session;                     // ���� ���� ����
        };

    } // namespace Server
} // namespace Blokus