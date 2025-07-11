﻿#include "handler/MessageHandler.h"
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
        // 🔥 enum 기반 핸들러 등록
        handlers_[MessageType::Ping] = [this](const auto& params) { handlePing(params); };

        // 인증 관련
        handlers_[MessageType::Auth] = [this](const auto& params) { handleAuth(params); };
        handlers_[MessageType::Register] = [this](const auto& params) { handleRegister(params); };
        handlers_[MessageType::Guest] = [this](const auto& params) { handleLoginGuest(params); };
        handlers_[MessageType::Logout] = [this](const auto& params) { handleLogout(params); };
        handlers_[MessageType::Validate] = [this](const auto& params) { handleSessionValidate(params); };

        // 방 관련
        handlers_[MessageType::RoomCreate] = [this](const auto& params) { handleCreateRoom(params); };
        handlers_[MessageType::RoomJoin] = [this](const auto& params) { handleJoinRoom(params); };
        handlers_[MessageType::RoomLeave] = [this](const auto& params) { handleLeaveRoom(params); };
        handlers_[MessageType::RoomList] = [this](const auto& params) { handleRoomList(params); };
        handlers_[MessageType::RoomReady] = [this](const auto& params) { handlePlayerReady(params); };
        handlers_[MessageType::RoomStart] = [this](const auto& params) { handleStartGame(params); };
        handlers_[MessageType::RoomTransferHost] = [this](const auto& params) { handleTransferHost(params); };

        // 게임 관련
        handlers_[MessageType::GameMove] = [this](const auto& params) { handleGameMove(params); };

        // 기본 기능
        handlers_[MessageType::Chat] = [this](const auto& params) { handleChat(params); };

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
            spdlog::debug("📨 메시지 수신 ({}): {}, 현재 상태: {}",
                session_->getSessionId(),
                rawMessage.length() > 100 ? rawMessage.substr(0, 100) + "..." : rawMessage, (int)session_->getState());

            // 🔥 새로운 방식: enum 기반 파싱
            auto [messageType, params] = parseMessage(rawMessage);

            spdlog::debug("파싱 결과: {} ({})",
                messageTypeToString(messageType), static_cast<int>(messageType));

            // 핸들러 실행
            auto it = handlers_.find(messageType);
            if (it != handlers_.end()) {
                it->second(params);
            }
            else {
                spdlog::warn("알 수 없는 메시지 타입: {} (원본: {})",
                    messageTypeToString(messageType), rawMessage);
                sendError("알 수 없는 명령어입니다");
            }
        }
        catch (const std::exception& e) {
            spdlog::error("메시지 처리 중 예외: {}", e.what());
            sendError("메시지 처리 중 오류가 발생했습니다");
        }
    }

    std::pair<MessageType, std::vector<std::string>> MessageHandler::parseMessage(const std::string& rawMessage) {
        // 기본 파싱
        auto parts = splitMessage(rawMessage, ':');
        if (parts.empty()) {
            return { MessageType::Unknown, {} };
        }

        // 첫 번째 부분으로 MessageType 결정
        std::string commandStr = parts[0];

        // room:xxx, game:xxx 형태 처리
        if (parts.size() >= 2) {
            if (commandStr == "room" || commandStr == "game") {
                commandStr += ":" + parts[1];
                // 파라미터는 2번째 인덱스부터
                std::vector<std::string> params(parts.begin() + 2, parts.end());
                return { parseMessageType(commandStr), params };
            }
        }

        // 단일 명령어 (auth, ping 등)
        std::vector<std::string> params(parts.begin() + 1, parts.end());
        return { parseMessageType(commandStr), params };
    }

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
        spdlog::debug("📤 에러 응답 전송: {}", errorMessage);
        sendTextMessage("ERROR:" + errorMessage);
    }

    // ========================================
    // 인증 관련 핸들러들
    // ========================================

    void MessageHandler::handleAuth(const std::vector<std::string>& params) {
        if (!authService_) {
            sendError("인증 서비스를 사용할 수 없습니다");
            return;
        }

        if (!session_->isConnected()) {
            sendError("인증을 진행할 수 있는 상태가 아닙니다");
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

        if (!session_->isConnected()) {
            sendError("회원 가입을 진행할 수 있는 상태가 아닙니다");
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

        if (!session_->isConnected()) {
            sendError("게스트 로그인을 진행할 수 있는 상태가 아닙니다");
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
        session_->setStateToConnected();

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
    // 방 관련 핸들러들 (완전 구현)
    // ========================================

    void MessageHandler::handleCreateRoom(const std::vector<std::string>& params) {
        // 1. 상태 검증
        if (!session_->canCreateRoom()) {
            spdlog::warn("🚫 방 생성 거부됨 - 상태 검증 실패");

            if (session_->isInRoom()) {
                spdlog::warn("🚫 이유: 이미 방에 참여 중");
                sendError("이미 방에 참여 중입니다. 먼저 방을 나가주세요");
            }
            else if (session_->isInGame()) {
                spdlog::warn("🚫 이유: 게임 중");
                sendError("게임 중에는 방을 만들 수 없습니다");
            }
            else {
                spdlog::warn("🚫 이유: 기타 (상태: {})", static_cast<int>(session_->getState()));
                sendError("현재 상태에서는 방을 만들 수 없습니다");
            }

            spdlog::warn("🚫 return 실행");
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        // 3. 파라미터 검증
        if (params.empty()) {
            sendError("사용법: room:create:방이름[:비공개(0/1)[:비밀번호]]");
            return;
        }

        try {
            std::string roomName = params[0];
            bool isPrivate = (params.size() > 1 && params[1] == "1");
            std::string password = (params.size() > 2) ? params[2] : "";

            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            spdlog::info("🏠 방 생성 요청: '{}' by '{}' (비공개: {})",
                roomName, username, isPrivate);

            // 4. RoomManager를 통한 방 생성
            int roomId = roomManager_->createRoom(userId, username, roomName, isPrivate, password);

            if (roomId > 0) {
                // 5. 호스트를 방에 추가 (세션도 함께)
                if (roomManager_->joinRoom(roomId, session_->shared_from_this(), userId, username, password)) {
                    // 6. 세션 상태 변경
                    session_->setStateToInRoom(roomId);

                    // 7. 브로드캐스트 (데드락 방지를 위해 여기서 호출)
                    auto room = roomManager_->getRoom(roomId);
                    if (room) {
                        room->broadcastPlayerJoined(username);
                    }

                    // 8. 성공 응답
                    std::ostringstream response;
                    response << "ROOM_CREATED:" << roomId << ":" << roomName;
                    sendResponse(response.str());

                    spdlog::info("✅ 방 생성 성공: '{}' by '{}' (ID: {})", roomName, username, roomId);
                }
                else {
                    // 방 생성은 되었지만 호스트 추가 실패 - 방 제거
                    roomManager_->removeRoom(roomId);
                    sendError("방 생성 후 호스트 추가에 실패했습니다");
                    spdlog::error("❌ 방 {} 호스트 추가 실패", roomId);
                }
            }
            else {
                // 방 생성 실패 - 실패 코드에 따른 메시지
                switch (roomId) {
                case -1: sendError("유효하지 않은 방 이름입니다"); break;
                case -2: sendError("이미 다른 방에 참여 중입니다"); break;
                case -3: sendError("서버에 방이 가득 찼습니다"); break;
                default: sendError("방 생성에 실패했습니다"); break;
                }
                spdlog::error("❌ 방 생성 실패: '{}' (코드: {})", roomName, roomId);
            }
        }
        catch (const std::exception& e) {
            sendError("방 생성 중 오류가 발생했습니다");
            spdlog::error("방 생성 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleJoinRoom(const std::vector<std::string>& params) {
        // 1. 상태 검증
        if (!session_->canJoinRoom()) {
            if (session_->isInRoom()) {
                sendError("이미 방에 참여 중입니다");
            }
            else if (session_->isInGame()) {
                sendError("게임 중에는 다른 방에 참여할 수 없습니다");
            }
            else {
                sendError("현재 상태에서는 방에 참여할 수 없습니다");
            }
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        // 3. 파라미터 검증
        if (params.empty()) {
            sendError("사용법: room:join:방ID[:비밀번호]");
            return;
        }

        try {
            int roomId = std::stoi(params[0]);
            std::string password = (params.size() > 1) ? params[1] : "";

            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            spdlog::info("🏠 방 참여 요청: '{}' -> 방 {}", username, roomId);

            // 4. 방 존재 확인
            auto room = roomManager_->getRoom(roomId);
            if (!room) {
                sendError("존재하지 않는 방입니다");
                return;
            }

            // 5. 게임 중인 방 참여 제한
            if (room->isPlaying()) {
                sendError("진행 중인 게임에는 참여할 수 없습니다");
                return;
            }

            // 6. 방이 가득 찬지 확인
            if (room->isFull()) {
                sendError("방이 가득 찼습니다");
                return;
            }

            // 7. RoomManager를 통한 방 참여
            if (roomManager_->joinRoom(roomId, session_->shared_from_this(), userId, username, password)) {
                // 8. 세션 상태 변경
                session_->setStateToInRoom(roomId);

                // 9. 브로드캐스트 (데드락 방지를 위해 여기서 호출)
                room->broadcastPlayerJoined(username);

                // 10. 성공 응답 (방 정보 포함)
                std::ostringstream response;
                response << "ROOM_JOIN_SUCCESS:" << roomId << ":" << room->getRoomName()
                    << ":" << room->getPlayerCount() << "/" << room->getMaxPlayers();
                sendResponse(response.str());

                spdlog::info("✅ 방 참여 성공: '{}' -> 방 {} ({}명)",
                    username, roomId, room->getPlayerCount());
            }
            else {
                sendError("방 참여에 실패했습니다");
                spdlog::warn("❌ 방 참여 실패: '{}' -> 방 {}", username, roomId);
            }
        }
        catch (const std::invalid_argument& e) {
            sendError("잘못된 방 ID 형식입니다");
        }
        catch (const std::out_of_range& e) {
            sendError("방 ID가 범위를 벗어났습니다");
        }
        catch (const std::exception& e) {
            sendError("방 참여 중 오류가 발생했습니다");
            spdlog::error("방 참여 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleLeaveRoom(const std::vector<std::string>& params) {
        // 1. 상태 검증
        if (!session_->canLeaveRoom()) {
            if (session_->isInLobby()) {
                sendError("방에 참여하지 않았습니다");
            }
            else {
                sendError("현재 상태에서는 방을 나갈 수 없습니다");
            }
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        try {
            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();
            int currentRoomId = session_->getCurrentRoomId();

            spdlog::info("🏠 방 나가기 요청: '{}' <- 방 {}", username, currentRoomId);

            // 3. RoomManager를 통한 방 나가기
            if (roomManager_->leaveRoom(userId)) {
                // 4. 브로드캐스트 (방이 아직 존재할 때만)
                auto room = roomManager_->getRoom(currentRoomId);
                if (room) {
                    room->broadcastPlayerLeft(username);
                }

                // 5. 세션 상태 변경
                session_->setStateToLobby();

                // 6. 성공 응답
                sendResponse("ROOM_LEFT:OK");

                spdlog::info("✅ 방 나가기 성공: '{}'", username);
            }
            else {
                sendError("방 나가기에 실패했습니다");
                spdlog::warn("❌ 방 나가기 실패: '{}'", username);
            }
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

        try {
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
                    << "," << (room.isPlaying ? "1" : "0")
                    << "," << room.gameMode;
            }

            sendResponse(response.str());
            spdlog::debug("📋 방 목록 전송: {} 개", roomList.size());
        }
        catch (const std::exception& e) {
            sendError("방 목록 조회 중 오류가 발생했습니다");
            spdlog::error("방 목록 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handlePlayerReady(const std::vector<std::string>& params) {
        // 1. 상태 검증
        if (!session_->isInRoom()) {
            sendError("방에 있어야 준비 상태를 변경할 수 있습니다");
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        try {
            bool ready = (!params.empty() && params[0] == "1");
            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            spdlog::info("🎮 플레이어 준비 상태 변경: '{}' -> {}",
                username, ready ? "준비" : "대기");

            // 3. RoomManager를 통한 준비 상태 설정
            if (roomManager_->setPlayerReady(userId, ready)) {
                // 4. 브로드캐스트 (데드락 방지를 위해 여기서 호출)
                auto room = roomManager_->findPlayerRoom(userId);
                if (room) {
                    room->broadcastPlayerReady(username, ready);
                }

                // 5. 성공 응답
                std::string readyStatus = ready ? "1" : "0";
                sendResponse("PLAYER_READY:" + readyStatus);

                spdlog::debug("✅ 플레이어 준비 상태 변경 성공: '{}'", username);
            }
            else {
                sendError("준비 상태 변경에 실패했습니다");
                spdlog::warn("❌ 플레이어 준비 상태 변경 실패: '{}'", username);
            }
        }
        catch (const std::exception& e) {
            sendError("준비 상태 변경 중 오류가 발생했습니다");
            spdlog::error("플레이어 준비 상태 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleStartGame(const std::vector<std::string>& params) {
        // 1. 상태 검증
        if (!session_->canStartGame()) {
            if (session_->isInLobby()) {
                sendError("방에 참여한 후 게임을 시작할 수 있습니다");
            }
            else if (session_->isInGame()) {
                sendError("이미 게임이 진행 중입니다");
            }
            else {
                sendError("현재 상태에서는 게임을 시작할 수 없습니다");
            }
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        try {
            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();
            int roomId = session_->getCurrentRoomId();

            spdlog::info("🎮 게임 시작 요청: '{}' (방 {})", username, roomId);

            // 3. 호스트 권한 확인
            auto room = roomManager_->getRoom(roomId);
            if (!room) {
                sendError("방을 찾을 수 없습니다");
                return;
            }

            if (!room->isHost(userId)) {
                sendError("호스트만 게임을 시작할 수 있습니다");
                return;
            }

            // 4. 게임 시작 조건 확인
            if (!room->canStartGame()) {
                sendError("게임 시작 조건이 충족되지 않았습니다. 모든 플레이어가 준비되었는지 확인하세요");
                return;
            }

            // 5. RoomManager를 통한 게임 시작
            if (roomManager_->startGame(userId)) {
                // 6. 브로드캐스트 (데드락 방지를 위해 여기서 호출)
                room->broadcastGameStart();

                // 7. 방의 모든 플레이어 세션 상태를 게임 중으로 변경
                auto playerList = room->getPlayerList();
                for (const auto& player : playerList) {
                    if (player.session) {
                        player.session->setStateToInGame();
                    }
                }

                // 8. 성공 응답
                sendResponse("GAME_START_SUCCESS");

                spdlog::info("✅ 게임 시작 성공: '{}' (방 {}, {}명)",
                    username, roomId, room->getPlayerCount());
            }
            else {
                sendError("게임 시작에 실패했습니다");
                spdlog::warn("❌ 게임 시작 실패: '{}' (방 {})", username, roomId);
            }
        }
        catch (const std::exception& e) {
            sendError("게임 시작 중 오류가 발생했습니다");
            spdlog::error("게임 시작 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleEndGame(const std::vector<std::string>& params) {
        // 1. 상태 검증
        if (!session_->isInGame()) {
            sendError("게임 중이 아닙니다");
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        try {
            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();
            int roomId = session_->getCurrentRoomId();

            spdlog::info("🎮 게임 종료 요청: '{}' (방 {})", username, roomId);

            // 3. 호스트 권한 확인 (또는 특별한 조건)
            auto room = roomManager_->getRoom(roomId);
            if (!room) {
                sendError("방을 찾을 수 없습니다");
                return;
            }

            if (!room->isHost(userId)) {
                sendError("호스트만 게임을 종료할 수 있습니다");
                return;
            }

            // 4. RoomManager를 통한 게임 종료
            if (roomManager_->endGame(roomId)) {
                // 5. 브로드캐스트 (데드락 방지를 위해 여기서 호출)
                room->broadcastGameEnd();

                // 6. 방의 모든 플레이어 세션 상태를 방 대기로 변경
                auto playerList = room->getPlayerList();
                for (const auto& player : playerList) {
                    if (player.session) {
                        player.session->setStateToInRoom(roomId);
                    }
                }

                // 7. 성공 응답
                sendResponse("GAME_END_SUCCESS");

                spdlog::info("✅ 게임 종료 성공: '{}' (방 {})", username, roomId);
            }
            else {
                sendError("게임 종료에 실패했습니다");
                spdlog::warn("❌ 게임 종료 실패: '{}' (방 {})", username, roomId);
            }
        }
        catch (const std::exception& e) {
            sendError("게임 종료 중 오류가 발생했습니다");
            spdlog::error("게임 종료 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleTransferHost(const std::vector<std::string>& params) {
        // 1. 상태 검증
        if (!session_->isInRoom()) {
            sendError("방에 있어야 호스트를 이양할 수 있습니다");
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        // 3. 파라미터 검증
        if (params.empty()) {
            sendError("사용법: room:transfer:대상플레이어ID");
            return;
        }

        try {
            std::string currentHostId = session_->getUserId();
            std::string newHostId = params[0];
            int roomId = session_->getCurrentRoomId();

            spdlog::info("👑 호스트 이양 요청: '{}' -> '{}' (방 {})",
                currentHostId, newHostId, roomId);

            // 4. RoomManager를 통한 호스트 이양
            if (roomManager_->transferHost(roomId, currentHostId, newHostId)) {
                // 5. 브로드캐스트 (데드락 방지를 위해 여기서 호출)
                auto room = roomManager_->getRoom(roomId);
                if (room) {
                    // 새 호스트 이름 찾기
                    const PlayerInfo* newHost = room->getPlayer(newHostId);
                    if (newHost) {
                        room->broadcastHostChanged(newHost->username);
                    }
                }

                // 6. 성공 응답
                sendResponse("HOST_TRANSFER_SUCCESS:" + newHostId);

                spdlog::info("✅ 호스트 이양 성공: '{}' -> '{}' (방 {})",
                    currentHostId, newHostId, roomId);
            }
            else {
                sendError("호스트 이양에 실패했습니다");
                spdlog::warn("❌ 호스트 이양 실패: '{}' -> '{}' (방 {})",
                    currentHostId, newHostId, roomId);
            }
        }
        catch (const std::exception& e) {
            sendError("호스트 이양 중 오류가 발생했습니다");
            spdlog::error("호스트 이양 처리 중 예외: {}", e.what());
        }
    }

    // ========================================
    // 게임 관련 핸들러들
    // ========================================

    void MessageHandler::handleGameMove(const std::vector<std::string>& params) {
        // 1. 상태 검증
        if (!session_->canMakeGameMove()) {
            if (session_->isInLobby()) {
                sendError("게임에 참여한 후 이동할 수 있습니다");
            }
            else if (session_->isInRoom()) {
                sendError("게임이 시작된 후 이동할 수 있습니다");
            }
            else {
                sendError("현재 상태에서는 게임 이동을 할 수 없습니다");
            }
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_) {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        // 3. 파라미터 검증 (블록 타입, 위치, 회전, 뒤집기 등)
        if (params.size() < 4) {
            sendError("사용법: game:move:블록타입:x좌표:y좌표:회전도[:뒤집기]");
            return;
        }

        try {
            std::string userId = session_->getUserId();
            int roomId = session_->getCurrentRoomId();

            // 방과 게임 로직 가져오기
            auto room = roomManager_->getRoom(roomId);
            if (!room || !room->isPlaying()) {
                sendError("게임이 진행 중이 아닙니다");
                return;
            }

            // TODO: 실제 게임 로직 구현
            // - 블록 배치 유효성 검사
            // - 턴 순서 확인
            // - 게임 규칙 적용
            // - 게임 상태 업데이트
            // - 다른 플레이어들에게 알림

            spdlog::info("🎮 게임 이동: '{}' (방 {})", userId, roomId);

            // 임시 성공 응답
            sendResponse("GAME_MOVE_SUCCESS");

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