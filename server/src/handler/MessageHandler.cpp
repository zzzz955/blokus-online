#include "handler/MessageHandler.h"
#include "core/Session.h"
#include "manager/RoomManager.h"
#include "service/AuthenticationService.h"
#include <spdlog/spdlog.h>
#include <sstream>
#include <algorithm>

namespace Blokus::Server {

    // ========================================
    // 생성자 및 소멸자
    // ========================================

    MessageHandler::MessageHandler(Session* session, RoomManager* roomManager, AuthenticationService* authService)
        : session_(session)
        , roomManager_(roomManager)
        , authService_(authService)
    {
        // 🔥 메시지 핸들러 등록 (새로운 방식)
        handlers_["ping"] = [this](const auto& params) { handlePing(params); };

        // 인증 관련
        handlers_["auth"] = [this](const auto& params) { handleAuth(params); };
        handlers_["register"] = [this](const auto& params) { handleRegister(params); };
        handlers_["guest"] = [this](const auto& params) { handleLoginGuest(params); };
        handlers_["logout"] = [this](const auto& params) { handleLogout(params); };
        handlers_["validate"] = [this](const auto& params) { handleSessionValidate(params); };

        // 방 관련
        handlers_["room:create"] = [this](const auto& params) { handleCreateRoom(params); };
        handlers_["room:join"] = [this](const auto& params) { handleJoinRoom(params); };
        handlers_["room:leave"] = [this](const auto& params) { handleLeaveRoom(params); };
        handlers_["room:list"] = [this](const auto& params) { handleRoomList(params); };
        handlers_["room:ready"] = [this](const auto& params) { handlePlayerReady(params); };
        handlers_["room:start"] = [this](const auto& params) { handleStartGame(params); };

        // 게임 관련
        handlers_["game:move"] = [this](const auto& params) { handleGameMove(params); };

        // 기본 기능
        handlers_["chat"] = [this](const auto& params) { handleChat(params); };

        spdlog::debug("MessageHandler 생성: 세션 {} (핸들러 수: {})",
            session_ ? session_->getSessionId() : "nullptr", handlers_.size());
    }

    MessageHandler::~MessageHandler() {
        spdlog::debug("MessageHandler 소멸");
    }

    // ========================================
    // 메시지 처리 (업데이트됨)
    // ========================================

    void MessageHandler::handleMessage(const std::string& rawMessage) {
        if (!session_) {
            spdlog::error("Session이 null입니다");
            return;
        }

        try {
            spdlog::debug("메시지 수신 ({}): {}",
                session_->getSessionId(),
                rawMessage.length() > 100 ? rawMessage.substr(0, 100) + "..." : rawMessage);

            // 새로운 방식: 구조화된 메시지 파싱
            auto parts = splitMessage(rawMessage, ':');
            if (parts.empty()) {
                sendError("잘못된 메시지 형식");
                return;
            }

            std::string command = parts[0];

            // room: 접두사 처리
            if (parts.size() >= 2 && command == "room") {
                command = "room:" + parts[1];
                parts.erase(parts.begin(), parts.begin() + 2);
            }
            // game: 접두사 처리  
            else if (parts.size() >= 2 && command == "game") {
                command = "game:" + parts[1];
                parts.erase(parts.begin(), parts.begin() + 2);
            }
            else {
                parts.erase(parts.begin()); // 첫 번째 command 제거
            }

            // 핸들러 실행
            auto it = handlers_.find(command);
            if (it != handlers_.end()) {
                it->second(parts);
            }
        }
        catch (const std::exception& e) {
            spdlog::error("메시지 처리 중 예외: {}", e.what());
            sendError("메시지 처리 중 오류가 발생했습니다");
        }
    }

    // ========================================
    // 유틸리티 함수들
    // ========================================

    std::vector<std::string> MessageHandler::splitMessage(const std::string& message, char delimiter) {
        std::vector<std::string> tokens;
        std::stringstream ss(message);
        std::string token;

        while (std::getline(ss, token, delimiter)) {
            if (!token.empty()) {
                tokens.push_back(token);
            }
        }

        return tokens;
    }

    void MessageHandler::sendResponse(const std::string& response) {
        sendTextMessage(response);
    }

    void MessageHandler::sendTextMessage(const std::string& message) {
        if (session_) {
            session_->sendMessage(message);
        }
    }

    void MessageHandler::sendError(const std::string& errorMessage) {
        sendTextMessage("ERROR:" + errorMessage);
    }

    // ========================================
    // 인증 관련 핸들러들 (새로 추가)
    // ========================================

    void MessageHandler::handleAuth(const std::vector<std::string>& params) {
        if (!authService_) {
            sendError("인증 서비스를 사용할 수 없습니다");
            return;
        }

        if (params.size() < 2) {
            sendError("사용법: auth:사용자명:비밀번호");
            return;
        }

        std::string username = params[0];
        std::string password = params[1];

        auto result = authService_->loginUser(username, password);

        if (result.success) {
            session_->setAuthenticated(result.userId, result.username);
            sendResponse("AUTH_SUCCESS:" + result.username + ":" + result.sessionToken);
            spdlog::info("✅ 로그인 성공: {} ({})", username, session_->getSessionId());
        }
        else {
            sendError(result.message);
            spdlog::warn("❌ 로그인 실패: {} - {}", username, result.message);
        }

        // 🔥 콜백 제거: 직접 처리 완료
    }

    void MessageHandler::handleRegister(const std::vector<std::string>& params) {
        if (!authService_) {
            sendError("인증 서비스를 사용할 수 없습니다");
            return;
        }

        if (params.size() < 3) {
            sendError("사용법: register:사용자명:이메일:비밀번호");
            return;
        }

        std::string username = params[0];
        std::string email = params[1];
        std::string password = params[2];

        auto result = authService_->registerUser(username, password);

        if (result.success) {
            sendResponse("REGISTER_SUCCESS:" + username);
            spdlog::info("✅ 회원가입 성공: {} ({})", username, email);

            // 등록 후 자동 로그인
            auto loginResult = authService_->loginUser(username, password);
            if (loginResult.success) {
                session_->setAuthenticated(loginResult.userId, loginResult.username);
                sendResponse("AUTO_LOGIN:" + loginResult.sessionToken);
                spdlog::info("✅ 자동 로그인 성공: {}", username);
            }
        }
        else {
            sendError(result.message);
            spdlog::error("❌ 회원가입 실패: {} - {}", username, result.message);
        }

        // 🔥 콜백 제거: 직접 처리 완료
    }

    void MessageHandler::handleLoginGuest(const std::vector<std::string>& params) {
        if (!authService_) {
            sendError("인증 서비스를 사용할 수 없습니다");
            return;
        }

        std::string guestName = params.empty() ? "" : params[0];

        auto result = authService_->loginGuest(guestName);

        if (result.success) {
            session_->setAuthenticated(result.userId, result.username);
            sendResponse("GUEST_LOGIN_SUCCESS:" + result.username + ":" + result.sessionToken);
            spdlog::info("게스트 로그인: {} ({})", result.username, session_->getSessionId());
        }
        else {
            sendError(result.message);
        }
    }

    void MessageHandler::handleLogout(const std::vector<std::string>& params) {
        if (!session_->isInLobby()) {
            sendError("로그인 상태가 아닙니다");
            return;
        }

        std::string username = session_->getUsername();

        // 세션 상태 초기화
        session_->setState(ConnectionState::Connected);

        sendResponse("LOGOUT_SUCCESS");
        spdlog::info("사용자 로그아웃: {} ({})", username, session_->getSessionId());
    }

    void MessageHandler::handleSessionValidate(const std::vector<std::string>& params) {
        if (!authService_) {
            sendError("인증 서비스를 사용할 수 없습니다");
            return;
        }

        if (params.empty()) {
            sendError("사용법: validate:세션토큰");
            return;
        }

        std::string sessionToken = params[0];
        auto sessionInfo = authService_->validateSession(sessionToken);

        if (sessionInfo) {
            sendResponse("SESSION_VALID:" + sessionInfo->username + ":" + sessionInfo->userId);
        }
        else {
            sendError("세션이 유효하지 않습니다");
        }
    }

    // ========================================
    // 방 관련 핸들러들 (새로 추가)
    // ========================================

    void MessageHandler::handleCreateRoom(const std::vector<std::string>& params) {
        if (!session_->canCreateRoom()) {
            sendError("현재 상태에선 방을 생성할 수 없습니다");
            return;
        }

        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        if (params.empty()) {
            sendError("방 이름이 필요합니다");
            return;
        }

        std::string roomName = params[0];
        bool isPrivate = (params.size() > 1 && params[1] == "1");
        std::string password = (params.size() > 2) ? params[2] : "";

        std::string userId = session_->getUserId();
        std::string username = session_->getUsername();

        spdlog::info("방 생성 요청: {} by {}", roomName, username);

        int roomId = roomManager_->createRoom(userId, username, roomName, isPrivate, password);

        if (roomId > 0) {
            session_->setStateToInRoom();
            sendResponse("ROOM_CREATED:" + std::to_string(roomId) + ":" + roomName);
            spdlog::info("✅ 방 생성 성공: {} by {} (ID: {})", roomName, username, roomId);
        }
        else {
            spdlog::error("❌ 방 생성 실패: {}", roomName);
            sendError("방 생성에 실패했습니다");
        }

        // 🔥 콜백 제거됨: MessageHandler에서 직접 완료 처리
    }

    void MessageHandler::handleJoinRoom(const std::vector<std::string>& params) {
        // 🔥 2단계: 상태 기반 예외 처리
        if (!session_->canJoinRoom()) {
            sendError("현재 상태에서는 방에 입장할 수 없습니다");
            return;
        }

        // 🔥 3단계: 매니저 유효성 체크
        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        // 🔥 4단계: 파라미터 검증
        if (params.empty()) {
            sendError("사용법: room:join:방ID[:비밀번호]");
            return;
        }

        try {
            int roomId = std::stoi(params[0]);
            std::string password = (params.size() > 1) ? params[1] : "";

            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            spdlog::info("방 입장 요청: {} -> 방 {} (현재 상태: {})",
                username, roomId, session_->isInLobby() ? "Lobby" : "Room");

            // TODO: RoomManager를 통한 실제 방 입장 로직
            // bool joinResult = roomManager_->joinRoom(roomId, userId, password);

            // 성공 시 상태 변경
            session_->setStateToInRoom(roomId);
            sendResponse("ROOM_JOIN_SUCCESS:" + std::to_string(roomId));

            spdlog::info("✅ 방 입장 성공: {} -> 방 {}", username, roomId);
        }
        catch (const std::invalid_argument& e) {
            sendError("잘못된 방 ID 형식입니다");
            spdlog::warn("방 ID 파싱 오류: {}", params[0]);
        }
        catch (const std::out_of_range& e) {
            sendError("방 ID가 범위를 벗어났습니다");
            spdlog::warn("방 ID 범위 오류: {}", params[0]);
        }
        catch (const std::exception& e) {
            sendError("방 입장 중 오류가 발생했습니다");
            spdlog::error("방 입장 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleLeaveRoom(const std::vector<std::string>& params) {
        if (!session_->canLeaveRoom()) {
            sendError("현재 상태에서는 방을 나갈 수 없습니다");
            return;
        }

        try {
            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            spdlog::info("방 나가기 요청: {} (현재 상태: {})",
                username, session_->isInGame() ? "InGame" : "InRoom");

            // TODO: RoomManager를 통한 실제 방 나가기 로직
            // roomManager_->leaveRoom(userId);

            // 성공 시 상태 변경
            session_->setStateToLobby();
            sendResponse("ROOM_LEFT:OK");

            spdlog::info("✅ 방 나가기 성공: {}", username);
        }
        catch (const std::exception& e) {
            sendError("방 나가기 중 오류가 발생했습니다");
            spdlog::error("방 나가기 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleRoomList(const std::vector<std::string>& params) {
        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        auto roomList = roomManager_->getRoomList();

        std::ostringstream response;
        response << "ROOM_LIST:" << roomList.size();

        for (const auto& room : roomList) {
            response << ":" << room.roomId
                << "," << room.roomName
                << "," << room.hostName
                << "," << room.currentPlayers
                << "," << room.maxPlayers
                << "," << (room.isPrivate ? "1" : "0")
                << "," << (room.isPlaying ? "1" : "0");
        }

        sendResponse(response.str());
    }

    void MessageHandler::handlePlayerReady(const std::vector<std::string>& params) {
        if (!session_->isInRoom()) {
            sendError("방에 있어야 레디를 할 수 있습니다");
            return;
        }

        bool ready = (!params.empty() && params[0] == "1");
        std::string userId = session_->getUserId();

        // TODO: RoomManager를 통해 준비 상태 설정
        std::string readyStatus = ready ? "1" : "0";
        sendResponse("PLAYER_READY:" + readyStatus);
        spdlog::info("플레이어 준비 상태 변경: {} -> {}", session_->getUsername(), ready ? "준비" : "대기");
    }

    void MessageHandler::handleStartGame(const std::vector<std::string>& params) {
        if (!session_->canStartGame()) {
            if (session_->isInLobby()) {
                sendError("방에 입장한 후 게임을 시작할 수 있습니다");
                return;
            }
            else if (session_->isInGame()) {
                sendError("이미 게임이 진행 중입니다");
                return;
            }
            else {
                sendError("현재 상태에서는 게임을 시작할 수 없습니다");
                return;
            }
        }

        try {
            std::string username = session_->getUsername();

            spdlog::info("게임 시작 요청: {} (방 상태에서)", username);

            // TODO: 호스트 권한 확인 및 게임 시작 로직
            // TODO: RoomManager를 통한 게임 시작 검증

            // 성공 시 상태 변경
            session_->setStateToInGame();
            sendResponse("GAME_START_SUCCESS");

            spdlog::info("✅ 게임 시작 성공: {}", username);
        }
        catch (const std::exception& e) {
            sendError("게임 시작 중 오류가 발생했습니다");
            spdlog::error("게임 시작 처리 중 예외: {}", e.what());
        }
    }

    // ========================================
    // 게임 관련 핸들러들 (새로 추가)
    // ========================================

    void MessageHandler::handleGameMove(const std::vector<std::string>& params) {
        if (!session_->canMakeGameMove()) {
            if (session_->isInLobby()) {
                sendError("게임에 참여한 후 이동할 수 있습니다");
                return;
            }
            else if (session_->isInRoom()) {
                sendError("게임이 시작된 후 이동할 수 있습니다");
                return;
            }
            else {
                sendError("현재 상태에서는 게임 이동을 할 수 없습니다");
                return;
            }
        }

        if (params.size() < 4) {
            sendError("사용법: game:move:x:y:blocktype:rotation");
            return;
        }

        try {
            // 파라미터 파싱 및 검증
            int x = std::stoi(params[0]);
            int y = std::stoi(params[1]);
            std::string blockType = params[2];
            int rotation = std::stoi(params[3]);

            // 범위 검증
            if (x < 0 || x >= 20 || y < 0 || y >= 20) {
                sendError("좌표가 보드 범위를 벗어났습니다 (0-19)");
                return;
            }

            if (rotation < 0 || rotation >= 4) {
                sendError("회전값이 잘못되었습니다 (0-3)");
                return;
            }

            std::string username = session_->getUsername();
            spdlog::info("게임 이동: {} -> ({},{}) 블록:{} 회전:{}",
                username, x, y, blockType, rotation);

            // TODO: 게임 로직 처리
            sendResponse("GAME_MOVE:OK");

            spdlog::info("✅ 게임 이동 성공: {}", username);
        }
        catch (const std::invalid_argument& e) {
            sendError("숫자 형식이 잘못되었습니다");
            spdlog::warn("게임 이동 파라미터 파싱 오류: {}", e.what());
        }
        catch (const std::out_of_range& e) {
            sendError("숫자가 범위를 벗어났습니다");
            spdlog::warn("게임 이동 파라미터 범위 오류: {}", e.what());
        }
        catch (const std::exception& e) {
            sendError("게임 이동 중 오류가 발생했습니다");
            spdlog::error("게임 이동 처리 중 예외: {}", e.what());
        }
    }

    // ========================================
    // 기본 핸들러들
    // ========================================

    void MessageHandler::handlePing(const std::vector<std::string>& params) {
        sendResponse("pong");
    }

    void MessageHandler::handleChat(const std::vector<std::string>& params) {
        if (!session_->isConnected()) {
            sendError("채팅은 로그인 후 이용 가능합니다");
            return;
        }

        if (params.empty()) {
            sendError("메시지 내용이 필요합니다");
            return;
        }

        // 파라미터들을 하나의 메시지로 합치기
        std::string message;
        for (size_t i = 0; i < params.size(); ++i) {
            if (i > 0) message += " ";
            message += params[i];
        }

        std::string username = session_->getUsername();
        spdlog::info("채팅 메시지: [{}] {}", username, message);
    }

    // ========================================
    // Protobuf 지원 (향후 확장용)
    // ========================================

    void MessageHandler::sendProtobufMessage(blokus::MessageType type, const google::protobuf::Message& payload) {
        // TODO: protobuf 메시지 구현
        spdlog::warn("Protobuf 메시지는 아직 구현되지 않았습니다");
        sendError("Protobuf not implemented yet");
    }

    // TODO: Protobuf 관련 구현 (향후)
    /*
    bool MessageHandler::parseProtobufMessage(const std::string& data, blokus::MessageWrapper& wrapper) {
        try {
            return wrapper.ParseFromString(data);
        }
        catch (const std::exception& e) {
            spdlog::debug("Protobuf 파싱 실패: {}", e.what());
            return false;
        }
    }

    void MessageHandler::handleProtobufMessage(const blokus::MessageWrapper& wrapper) {
        // 메시지 타입별 라우팅
        auto it = protobufHandlers_.find(static_cast<int>(wrapper.type()));
        if (it != protobufHandlers_.end()) {
            it->second(wrapper);
        } else {
            spdlog::warn("처리되지 않은 protobuf 메시지 타입: {}", wrapper.type());
            sendError("Unhandled message type");
        }
    }
    */

} // namespace Blokus::Server