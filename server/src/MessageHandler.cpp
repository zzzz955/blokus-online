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
                // JSON 파싱
                auto json = nlohmann::json::parse(message);

                if (!json.contains("type")) {
                    spdlog::warn("메시지에 type 필드가 없음");
                    return;
                }

                std::string type = json["type"];
                spdlog::debug("메시지 타입: {}", type);

                // 메시지 타입별 처리
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
                    spdlog::warn("알 수 없는 메시지 타입: {}", type);
                    sendResponse("error", false, "알 수 없는 메시지 타입");
                }
            }
            catch (const nlohmann::json::exception& e) {
                spdlog::error("JSON 파싱 오류: {}", e.what());
                sendResponse("error", false, "잘못된 JSON 형식");
            }
        }

        void MessageHandler::handleLoginRequest(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("username") || !json.contains("password")) {
                    sendResponse("login_response", false, "사용자명과 비밀번호가 필요합니다");
                    return;
                }

                std::string username = json["username"];
                std::string password = json["password"];

                spdlog::info("세션 {} 로그인 요청: {}", session->getId(), username);

                // TODO: 실제 인증 로직 구현 (UserManager 연동)
                // 임시로 기본적인 검증만 수행
                if (username.length() < 4) {
                    sendResponse("login_response", false, "사용자명은 4자 이상이어야 합니다");
                    return;
                }

                if (password.length() < 8) {
                    sendResponse("login_response", false, "비밀번호는 8자 이상이어야 합니다");
                    return;
                }

                // 임시 로그인 성공 처리
                session->setUsername(username);
                session->setUserId(12345); // 임시 사용자 ID

                nlohmann::json response = {
                    {"type", "login_response"},
                    {"success", true},
                    {"message", "로그인 성공"},
                    {"user_id", session->getUserId()},
                    {"username", username}
                };

                session->sendMessage(response.dump());
                spdlog::info("세션 {} 로그인 성공: {}", session->getId(), username);
            }
            catch (const std::exception& e) {
                spdlog::error("로그인 처리 오류: {}", e.what());
                sendResponse("login_response", false, "로그인 처리 중 오류 발생");
            }
        }

        void MessageHandler::handleRegisterRequest(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("username") || !json.contains("password") || !json.contains("email")) {
                    sendResponse("register_response", false, "사용자명, 비밀번호, 이메일이 필요합니다");
                    return;
                }

                std::string username = json["username"];
                std::string password = json["password"];
                std::string email = json["email"];

                spdlog::info("세션 {} 회원가입 요청: {} ({})", session->getId(), username, email);

                // TODO: 실제 회원가입 로직 구현
                // 임시로 성공 응답
                sendResponse("register_response", true, "회원가입이 완료되었습니다");

            }
            catch (const std::exception& e) {
                spdlog::error("회원가입 처리 오류: {}", e.what());
                sendResponse("register_response", false, "회원가입 처리 중 오류 발생");
            }
        }

        void MessageHandler::handleCreateRoomRequest(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("room_name")) {
                    sendResponse("create_room_response", false, "방 이름이 필요합니다");
                    return;
                }

                std::string roomName = json["room_name"];
                bool isPrivate = json.value("is_private", false);
                std::string password = json.value("password", "");

                spdlog::info("세션 {} 방 생성 요청: {} (비공개: {})",
                    session->getId(), roomName, isPrivate);

                // TODO: RoomManager를 통한 실제 방 생성 로직 구현
                // 임시로 성공 응답
                nlohmann::json response = {
                    {"type", "create_room_response"},
                    {"success", true},
                    {"message", "방이 생성되었습니다"},
                    {"room_id", 1001},
                    {"room_name", roomName}
                };

                session->sendMessage(response.dump());

            }
            catch (const std::exception& e) {
                spdlog::error("방 생성 처리 오류: {}", e.what());
                sendResponse("create_room_response", false, "방 생성 중 오류 발생");
            }
        }

        void MessageHandler::handleJoinRoomRequest(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("room_id")) {
                    sendResponse("join_room_response", false, "방 ID가 필요합니다");
                    return;
                }

                uint32_t roomId = json["room_id"];
                std::string password = json.value("password", "");

                spdlog::info("세션 {} 방 참여 요청: 방 ID {}", session->getId(), roomId);

                // TODO: RoomManager를 통한 실제 방 참여 로직 구현
                // 임시로 성공 응답
                session->setRoomId(roomId);

                nlohmann::json response = {
                    {"type", "join_room_response"},
                    {"success", true},
                    {"message", "방에 참여했습니다"},
                    {"room_id", roomId}
                };

                session->sendMessage(response.dump());

            }
            catch (const std::exception& e) {
                spdlog::error("방 참여 처리 오류: {}", e.what());
                sendResponse("join_room_response", false, "방 참여 중 오류 발생");
            }
        }

        void MessageHandler::handleLeaveRoomRequest(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                spdlog::info("세션 {} 방 나가기 요청: 현재 방 {}",
                    session->getId(), session->getRoomId());

                // TODO: RoomManager를 통한 실제 방 나가기 로직 구현
                uint32_t oldRoomId = session->getRoomId();
                session->setRoomId(0); // 방 나가기

                nlohmann::json response = {
                    {"type", "leave_room_response"},
                    {"success", true},
                    {"message", "방을 나갔습니다"},
                    {"room_id", oldRoomId}
                };

                session->sendMessage(response.dump());

            }
            catch (const std::exception& e) {
                spdlog::error("방 나가기 처리 오류: {}", e.what());
                sendResponse("leave_room_response", false, "방 나가기 중 오류 발생");
            }
        }

        void MessageHandler::handleGameAction(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("action")) {
                    spdlog::warn("게임 액션에 action 필드가 없음");
                    return;
                }

                std::string action = json["action"];
                spdlog::debug("세션 {} 게임 액션: {}", session->getId(), action);

                // TODO: GameRoom을 통한 실제 게임 액션 처리 구현
                // 블록 배치, 턴 넘기기 등

                if (action == "place_block") {
                    // 블록 배치 처리
                    if (json.contains("block_type") && json.contains("position")) {
                        spdlog::info("세션 {} 블록 배치: {} at ({}, {})",
                            session->getId(),
                            json["block_type"].get<std::string>(),
                            json["position"]["row"].get<int>(),
                            json["position"]["col"].get<int>());
                    }
                }
                else if (action == "skip_turn") {
                    // 턴 스킵 처리
                    spdlog::info("세션 {} 턴 스킵", session->getId());
                }

                // 임시 응답
                nlohmann::json response = {
                    {"type", "game_action_response"},
                    {"success", true},
                    {"action", action}
                };

                session->sendMessage(response.dump());

            }
            catch (const std::exception& e) {
                spdlog::error("게임 액션 처리 오류: {}", e.what());
                sendResponse("game_action_response", false, "게임 액션 처리 중 오류 발생");
            }
        }

        void MessageHandler::handleChatMessage(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("message")) {
                    spdlog::warn("채팅 메시지에 message 필드가 없음");
                    return;
                }

                std::string chatMessage = json["message"];

                // 메시지 길이 제한
                if (chatMessage.length() > 200) {
                    sendResponse("chat_response", false, "메시지가 너무 깁니다");
                    return;
                }

                spdlog::info("세션 {} 채팅: {}", session->getId(), chatMessage);

                // TODO: 방 또는 로비 채팅 브로드캐스트 구현
                // 현재는 에코로 돌려보냄
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
                spdlog::error("채팅 메시지 처리 오류: {}", e.what());
            }
        }

        void MessageHandler::handleHeartbeat(const std::string& message) {
            auto session = m_session.lock();
            if (!session) return;

            // 하트비트 응답
            nlohmann::json response = {
                {"type", "heartbeat_response"},
                {"timestamp", std::time(nullptr)}
            };

            session->sendMessage(response.dump());
            spdlog::debug("세션 {} 하트비트 응답", session->getId());
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
                    response["data"] = data; // 파싱 실패 시 문자열로 저장
                }
            }

            session->sendMessage(response.dump());
        }

        bool MessageHandler::parseJsonMessage(const std::string& message, const std::string& expectedType) {
            try {
                auto json = nlohmann::json::parse(message);

                if (!json.contains("type")) {
                    spdlog::warn("메시지에 type 필드가 없음");
                    return false;
                }

                std::string type = json["type"];
                if (type != expectedType) {
                    spdlog::warn("예상된 메시지 타입: {}, 실제: {}", expectedType, type);
                    return false;
                }

                return true;
            }
            catch (const nlohmann::json::exception& e) {
                spdlog::error("JSON 파싱 오류: {}", e.what());
                return false;
            }
        }

    } // namespace Server
} // namespace Blokus