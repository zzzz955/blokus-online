#include "server/MessageHandler.h"
#include "server/Session.h"
#include <nlohmann/json.hpp>
#include <spdlog/spdlog.h>
#include <ctime>

namespace Blokus {
    namespace Server {

        MessageHandler::MessageHandler(std::shared_ptr<Session> session)
            : m_session(session)
        {
        }

        void MessageHandler::handleMessage(const std::string& message) {
            try {
                // JSON �Ľ�
                auto json = nlohmann::json::parse(message);

                if (!json.contains("type")) {
                    spdlog::warn("�޽����� type �ʵ尡 ����");
                    return;
                }

                std::string type = json["type"];
                spdlog::debug("�޽��� Ÿ��: {}", type);

                // �޽��� Ÿ�Ժ� ó��
                if (type == "login") {
                    handleLoginRequest(message);
                }
                else if (type == "register") {
                    handleRegisterRequest(message);
                }
                else if (type == "create_room") {
                    handleCreateRoomRequest(message);
                }
                else if (type == "join_room") {
                    handleJoinRoomRequest(message);
                }
                else if (type == "leave_room") {
                    handleLeaveRoomRequest(message);
                }
                else if (type == "game_action") {
                    handleGameAction(message);
                }
                else if (type == "chat") {
                    handleChatMessage(message);
                }
                else if (type == "heartbeat") {
                    handleHeartbeat(message);
                }
                else {
                    spdlog::warn("�� �� ���� �޽��� Ÿ��: {}", type);
                    sendResponse("error", false, "�� �� ���� �޽��� Ÿ��");
                }
            }
            catch (const nlohmann::json::exception& e) {
                spdlog::error("JSON �Ľ� ����: {}", e.what());
                sendResponse("error", false, "�߸��� JSON ����");
            }
        }

        void MessageHandler::handleLoginRequest(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("username") || !json.contains("password")) {
                    sendResponse("login_response", false, "����ڸ�� ��й�ȣ�� �ʿ��մϴ�");
                    return;
                }

                std::string username = json["username"];
                std::string password = json["password"];

                spdlog::info("���� {} �α��� ��û: {}", session->getId(), username);

                // TODO: ���� ���� ���� ���� (UserManager ����)
                // �ӽ÷� �⺻���� ������ ����
                if (username.length() < 4) {
                    sendResponse("login_response", false, "����ڸ��� 4�� �̻��̾�� �մϴ�");
                    return;
                }

                if (password.length() < 8) {
                    sendResponse("login_response", false, "��й�ȣ�� 8�� �̻��̾�� �մϴ�");
                    return;
                }

                // �ӽ� �α��� ���� ó��
                session->setUsername(username);
                session->setUserId(12345); // �ӽ� ����� ID

                nlohmann::json response = {
                    {"type", "login_response"},
                    {"success", true},
                    {"message", "�α��� ����"},
                    {"user_id", session->getUserId()},
                    {"username", username}
                };

                session->sendMessage(response.dump());
                spdlog::info("���� {} �α��� ����: {}", session->getId(), username);
            }
            catch (const std::exception& e) {
                spdlog::error("�α��� ó�� ����: {}", e.what());
                sendResponse("login_response", false, "�α��� ó�� �� ���� �߻�");
            }
        }

        void MessageHandler::handleRegisterRequest(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("username") || !json.contains("password") || !json.contains("email")) {
                    sendResponse("register_response", false, "����ڸ�, ��й�ȣ, �̸����� �ʿ��մϴ�");
                    return;
                }

                std::string username = json["username"];
                std::string password = json["password"];
                std::string email = json["email"];

                spdlog::info("���� {} ȸ������ ��û: {} ({})", session->getId(), username, email);

                // TODO: ���� ȸ������ ���� ����
                // �ӽ÷� ���� ����
                sendResponse("register_response", true, "ȸ�������� �Ϸ�Ǿ����ϴ�");

            }
            catch (const std::exception& e) {
                spdlog::error("ȸ������ ó�� ����: {}", e.what());
                sendResponse("register_response", false, "ȸ������ ó�� �� ���� �߻�");
            }
        }

        void MessageHandler::handleCreateRoomRequest(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("room_name")) {
                    sendResponse("create_room_response", false, "�� �̸��� �ʿ��մϴ�");
                    return;
                }

                std::string roomName = json["room_name"];
                bool isPrivate = json.value("is_private", false);
                std::string password = json.value("password", "");

                spdlog::info("���� {} �� ���� ��û: {} (�����: {})",
                    session->getId(), roomName, isPrivate);

                // TODO: RoomManager�� ���� ���� �� ���� ���� ����
                // �ӽ÷� ���� ����
                nlohmann::json response = {
                    {"type", "create_room_response"},
                    {"success", true},
                    {"message", "���� �����Ǿ����ϴ�"},
                    {"room_id", 1001},
                    {"room_name", roomName}
                };

                session->sendMessage(response.dump());

            }
            catch (const std::exception& e) {
                spdlog::error("�� ���� ó�� ����: {}", e.what());
                sendResponse("create_room_response", false, "�� ���� �� ���� �߻�");
            }
        }

        void MessageHandler::handleJoinRoomRequest(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("room_id")) {
                    sendResponse("join_room_response", false, "�� ID�� �ʿ��մϴ�");
                    return;
                }

                uint32_t roomId = json["room_id"];
                std::string password = json.value("password", "");

                spdlog::info("���� {} �� ���� ��û: �� ID {}", session->getId(), roomId);

                // TODO: RoomManager�� ���� ���� �� ���� ���� ����
                // �ӽ÷� ���� ����
                session->setRoomId(roomId);

                nlohmann::json response = {
                    {"type", "join_room_response"},
                    {"success", true},
                    {"message", "�濡 �����߽��ϴ�"},
                    {"room_id", roomId}
                };

                session->sendMessage(response.dump());

            }
            catch (const std::exception& e) {
                spdlog::error("�� ���� ó�� ����: {}", e.what());
                sendResponse("join_room_response", false, "�� ���� �� ���� �߻�");
            }
        }

        void MessageHandler::handleLeaveRoomRequest(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                spdlog::info("���� {} �� ������ ��û: ���� �� {}",
                    session->getId(), session->getRoomId());

                // TODO: RoomManager�� ���� ���� �� ������ ���� ����
                uint32_t oldRoomId = session->getRoomId();
                session->setRoomId(0); // �� ������

                nlohmann::json response = {
                    {"type", "leave_room_response"},
                    {"success", true},
                    {"message", "���� �������ϴ�"},
                    {"room_id", oldRoomId}
                };

                session->sendMessage(response.dump());

            }
            catch (const std::exception& e) {
                spdlog::error("�� ������ ó�� ����: {}", e.what());
                sendResponse("leave_room_response", false, "�� ������ �� ���� �߻�");
            }
        }

        void MessageHandler::handleGameAction(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("action")) {
                    spdlog::warn("���� �׼ǿ� action �ʵ尡 ����");
                    return;
                }

                std::string action = json["action"];
                spdlog::debug("���� {} ���� �׼�: {}", session->getId(), action);

                // TODO: GameRoom�� ���� ���� ���� �׼� ó�� ����
                // ��� ��ġ, �� �ѱ�� ��

                if (action == "place_block") {
                    // ��� ��ġ ó��
                    if (json.contains("block_type") && json.contains("position")) {
                        spdlog::info("���� {} ��� ��ġ: {} at ({}, {})",
                            session->getId(),
                            json["block_type"].get<std::string>(),
                            json["position"]["row"].get<int>(),
                            json["position"]["col"].get<int>());
                    }
                }
                else if (action == "skip_turn") {
                    // �� ��ŵ ó��
                    spdlog::info("���� {} �� ��ŵ", session->getId());
                }

                // �ӽ� ����
                nlohmann::json response = {
                    {"type", "game_action_response"},
                    {"success", true},
                    {"action", action}
                };

                session->sendMessage(response.dump());

            }
            catch (const std::exception& e) {
                spdlog::error("���� �׼� ó�� ����: {}", e.what());
                sendResponse("game_action_response", false, "���� �׼� ó�� �� ���� �߻�");
            }
        }

        void MessageHandler::handleChatMessage(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("message")) {
                    spdlog::warn("ä�� �޽����� message �ʵ尡 ����");
                    return;
                }

                std::string chatMessage = json["message"];

                // �޽��� ���� ����
                if (chatMessage.length() > 200) {
                    sendResponse("chat_response", false, "�޽����� �ʹ� ��ϴ�");
                    return;
                }

                spdlog::info("���� {} ä��: {}", session->getId(), chatMessage);

                // TODO: �� �Ǵ� �κ� ä�� ��ε�ĳ��Ʈ ����
                // ����� ���ڷ� ��������
                nlohmann::json response = {
                    {"type", "chat_message"},
                    {"username", session->getUsername()},
                    {"message", chatMessage},
                    {"timestamp", std::time(nullptr)},
                    {"room_id", session->getRoomId()}
                };

                session->sendMessage(response.dump());

            }
            catch (const std::exception& e) {
                spdlog::error("ä�� �޽��� ó�� ����: {}", e.what());
            }
        }

        void MessageHandler::handleHeartbeat(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            // ��Ʈ��Ʈ ����
            nlohmann::json response = {
                {"type", "heartbeat_response"},
                {"timestamp", std::time(nullptr)}
            };

            session->sendMessage(response.dump());
            spdlog::debug("���� {} ��Ʈ��Ʈ ����", session->getId());
        }

        void MessageHandler::sendResponse(const std::string& type, bool success,
            const std::string& message, const std::string& data) {
            auto session = m_session.lock();
            if (!session) return;

            nlohmann::json response = {
                {"type", type},
                {"success", success},
                {"message", message}
            };

            if (!data.empty()) {
                try {
                    response["data"] = nlohmann::json::parse(data);
                }
                catch (const nlohmann::json::exception&) {
                    response["data"] = data; // �Ľ� ���� �� ���ڿ��� ����
                }
            }

            session->sendMessage(response.dump());
        }

        bool MessageHandler::parseJsonMessage(const std::string& message, const std::string& expectedType) {
            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("type")) {
                    spdlog::warn("�޽����� type �ʵ尡 ����");
                    return false;
                }

                std::string type = json["type"];
                if (type != expectedType) {
                    spdlog::warn("����� �޽��� Ÿ��: {}, ����: {}", expectedType, type);
                    return false;
                }

                return true;
            }
            catch (const nlohmann::json::exception& e) {
                spdlog::error("JSON �Ľ� ����: {}", e.what());
                return false;
            }
        }

    } // namespace Server
} // namespace Blokus