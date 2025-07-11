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
            else {
                // 하위 호환성을 위한 기존 방식 처리
                handleTextMessage(rawMessage);
            }
        }
        catch (const std::exception& e) {
            spdlog::error("메시지 처리 중 예외: {}", e.what());
            sendError("메시지 처리 중 오류가 발생했습니다");
        }
    }

    void MessageHandler::handleTextMessage(const std::string& rawMessage) {
        try {
            // 기존 방식 (하위 호환성)
            if (rawMessage == "ping") {
                sendTextMessage("pong");
            }
            else if (rawMessage.starts_with("auth:")) {
                handleAuthMessage(rawMessage.substr(5));
            }
            else if (rawMessage.starts_with("room:")) {
                handleRoomMessage(rawMessage.substr(5));
            }
            else if (rawMessage.starts_with("chat:")) {
                handleChatMessage(rawMessage.substr(5));
            }
            else {
                spdlog::warn("알 수 없는 메시지 형식: {}", rawMessage);
                sendError("알 수 없는 명령어입니다. 도움말: ping, auth:user:pass, room:list, chat:message");
            }
        }
        catch (const std::exception& e) {
            spdlog::error("텍스트 메시지 처리 중 오류: {}", e.what());
            sendError("텍스트 메시지 처리 오류");
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
        if (!session_->isAuthenticated()) {
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
        if (!session_->isAuthenticated()) {
            sendError("방 생성은 로그인 후 이용 가능합니다");
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
        if (!session_->isAuthenticated()) {
            sendError("방 입장은 로그인 후 이용 가능합니다");
            return;
        }

        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        if (params.empty()) {
            sendError("방 ID가 필요합니다");
            return;
        }

        try {
            int roomId = std::stoi(params[0]);
            std::string password = (params.size() > 1) ? params[1] : "";

            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            // Session 포인터를 직접 사용하지 않고 콜백으로 처리
            sendResponse("ROOM_JOIN_REQUEST:" + std::to_string(roomId));
            spdlog::info("방 입장 요청: {} -> 방 {}", username, roomId);
        }
        catch (const std::exception& e) {
            sendError("잘못된 방 ID입니다");
        }
    }

    void MessageHandler::handleLeaveRoom(const std::vector<std::string>& params) {
        if (!session_->isAuthenticated()) {
            sendError("로그인이 필요합니다");
            return;
        }

        std::string userId = session_->getUserId();
        std::string username = session_->getUsername();

        // TODO: 현재 방 ID를 세션에서 추적하거나 파라미터로 받아야 함
        sendResponse("ROOM_LEFT:OK");
        spdlog::info("방 나가기 요청: {}", username);
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
        if (!session_->isAuthenticated()) {
            sendError("로그인이 필요합니다");
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
        if (!session_->isAuthenticated()) {
            sendError("로그인이 필요합니다");
            return;
        }

        // TODO: 호스트 권한 확인 및 게임 시작 로직
        sendResponse("GAME_START_REQUEST:OK");
        spdlog::info("게임 시작 요청: {}", session_->getUsername());
    }

    // ========================================
    // 게임 관련 핸들러들 (새로 추가)
    // ========================================

    void MessageHandler::handleGameMove(const std::vector<std::string>& params) {
        if (!session_->isAuthenticated()) {
            sendError("로그인이 필요합니다");
            return;
        }

        if (params.size() < 2) {
            sendError("사용법: game:move:x:y:blocktype:rotation");
            return;
        }

        // TODO: 게임 이동 로직 구현
        std::string moveData;
        for (size_t i = 0; i < params.size(); ++i) {
            if (i > 0) moveData += ":";
            moveData += params[i];
        }

        sendResponse("GAME_MOVE:OK");
        spdlog::info("게임 이동: {} -> {}", session_->getUsername(), moveData);
    }

    // ========================================
    // 기본 핸들러들
    // ========================================

    void MessageHandler::handlePing(const std::vector<std::string>& params) {
        sendResponse("pong");
    }

    void MessageHandler::handleChat(const std::vector<std::string>& params) {
        if (!session_->isAuthenticated()) {
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
    // 기존 콜백 방식 (하위 호환성)
    // ========================================

    void MessageHandler::handleAuthMessage(const std::string& authData) {
        // 간단한 파싱: "username:password"
        size_t colonPos = authData.find(':');
        if (colonPos == std::string::npos) {
            sendError("Invalid auth format. Use 'username:password'");
            return;
        }

        std::string username = authData.substr(0, colonPos);
        std::string password = authData.substr(colonPos + 1);

        spdlog::info("인증 시도 (기존방식): {} (세션: {})", username, session_->getSessionId());

        // AuthenticationService 사용
        if (authService_) {
            auto result = authService_->loginUser(username, password);

            if (result.success) {
                session_->setAuthenticated(result.userId, result.username);
                sendTextMessage("AUTH_SUCCESS:" + result.username);
            }
            else {
                sendTextMessage("AUTH_FAILED:" + result.message);
            }
        }
        else {
            // Fallback: 간단한 검증
            bool success = (username.length() >= 3 && password.length() >= 4);

            if (success) {
                session_->setAuthenticated("user_" + username, username);
                sendTextMessage("AUTH_SUCCESS:" + username);
            }
            else {
                sendTextMessage("AUTH_FAILED:Invalid credentials");
            }
        }
    }

    void MessageHandler::handleRoomMessage(const std::string& roomData) {
        if (!session_->isAuthenticated()) {
            sendError("Authentication required");
            return;
        }

        spdlog::info("방 관련 요청 (기존방식): {} (세션: {})", roomData, session_->getSessionId());

        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        // 간단한 방 명령어 처리 - 직접 처리로 변경
        if (roomData == "list") {
            // 🔥 방 목록 요청 - 직접 처리
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

            sendTextMessage(response.str());
            spdlog::info("✅ 방 목록 전송 완료 (기존방식)");

            // 🗑️ 제거: if (roomCallback_) { roomCallback_(...); }
        }
        else if (roomData.starts_with("create:")) {
            // 🔥 방 생성 요청 - 직접 처리
            std::string roomName = roomData.substr(7); // "create:" 제거

            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            int roomId = roomManager_->createRoom(userId, username, roomName, false, "");

            if (roomId > 0) {
                sendTextMessage("ROOM_CREATED:" + std::to_string(roomId) + ":" + roomName);
                spdlog::info("✅ 방 생성 성공 (기존방식): {} by {} (ID: {})", roomName, username, roomId);
            }
            else {
                sendError("방 생성에 실패했습니다");
            }

            // 🗑️ 제거: if (roomCallback_) { roomCallback_(...); }
        }
        else if (roomData.starts_with("join:")) {
            // 🔥 방 참가 요청 - 직접 처리
            std::string roomIdStr = roomData.substr(5); // "join:" 제거

            try {
                int roomId = std::stoi(roomIdStr);
                std::string username = session_->getUsername();

                sendTextMessage("ROOM_JOIN_REQUEST:" + std::to_string(roomId));
                spdlog::info("✅ 방 입장 요청 처리 (기존방식): {} -> 방 {}", username, roomId);

                // TODO: 실제 방 입장 로직은 RoomManager에서 처리
            }
            catch (const std::exception& e) {
                sendError("잘못된 방 ID입니다");
            }

            // 🗑️ 제거: if (roomCallback_) { roomCallback_(...); }
        }
        else if (roomData == "leave") {
            // 🔥 방 나가기 요청 - 직접 처리
            std::string username = session_->getUsername();

            sendTextMessage("ROOM_LEFT:OK");
            spdlog::info("✅ 방 나가기 요청 처리 (기존방식): {}", username);

            // TODO: 실제 방 나가기 로직은 RoomManager에서 처리

            // 🗑️ 제거: if (roomCallback_) { roomCallback_(...); }
        }
        else {
            sendError("Unknown room command. 사용법: list, create:방이름, join:방ID, leave");
        }
    }

    void MessageHandler::handleChatMessage(const std::string& chatData) {
        if (!session_->isAuthenticated()) {
            sendError("Authentication required");
            return;
        }

        spdlog::info("채팅 메시지 (기존방식): {} -> {}", session_->getUsername(), chatData);
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