#include "MessageHandler.h"
#include "Session.h"
#include "RoomManager.h"
#include "AuthenticationService.h"
#include "GameServer.h"
#include "DatabaseManager.h"
#include "VersionManager.h"
#include "ServerTypes.h"
#include <spdlog/spdlog.h>
#include <sstream>
#include <algorithm>
#include <iomanip>
#include <ctime>
#include <cstdio>

namespace Blokus::Server
{

    // ========================================
    // 생성자 및 소멸자
    // ========================================

    MessageHandler::MessageHandler(Session *session, RoomManager *roomManager, AuthenticationService *authService, DatabaseManager *databaseManager, GameServer *gameServer, VersionManager *versionManager)
        : session_(session), roomManager_(roomManager), authService_(authService), databaseManager_(databaseManager), gameServer_(gameServer), versionManager_(versionManager)
    {
        // 🔥 enum 기반 핸들러 등록
        handlers_[MessageType::Ping] = [this](const auto &params)
        { handlePing(params); };

        // 인증 관련
        handlers_[MessageType::Auth] = [this](const auto &params)
        { handleAuth(params); };
        handlers_[MessageType::Register] = [this](const auto &params)
        { handleRegister(params); };
        handlers_[MessageType::Guest] = [this](const auto &params)
        { handleLoginGuest(params); };
        handlers_[MessageType::Logout] = [this](const auto &params)
        { handleLogout(params); };
        handlers_[MessageType::Validate] = [this](const auto &params)
        { handleSessionValidate(params); };

        // 방 관련
        handlers_[MessageType::RoomCreate] = [this](const auto &params)
        { handleCreateRoom(params); };
        handlers_[MessageType::RoomJoin] = [this](const auto &params)
        { handleJoinRoom(params); };
        handlers_[MessageType::RoomLeave] = [this](const auto &params)
        { handleLeaveRoom(params); };
        handlers_[MessageType::RoomList] = [this](const auto &params)
        { handleRoomList(params); };
        handlers_[MessageType::RoomReady] = [this](const auto &params)
        { handlePlayerReady(params); };
        handlers_[MessageType::RoomStart] = [this](const auto &params)
        { handleStartGame(params); };
        handlers_[MessageType::RoomTransferHost] = [this](const auto &params)
        { handleTransferHost(params); };

        // 로비 관련
        handlers_[MessageType::LobbyEnter] = [this](const auto &params)
        { handleLobbyEnter(params); };
        handlers_[MessageType::LobbyLeave] = [this](const auto &params)
        { handleLobbyLeave(params); };
        handlers_[MessageType::LobbyList] = [this](const auto &params)
        { handleLobbyList(params); };

        // 사용자 정보 관련
        handlers_[MessageType::UserStats] = [this](const auto &params)
        { handleGetUserStats(params); };

        // 사용자 설정 관련
        handlers_[MessageType::UserSettings] = [this](const auto &params)
        { handleUserSettings(params); };

        // 버전 관련
        handlers_[MessageType::VersionCheck] = [this](const auto &params)
        { handleVersionCheck(params); };

        // 게임 관련
        handlers_[MessageType::GameMove] = [this](const auto &params)
        { handleGameMove(params); };
        // handlers_[MessageType::GameResultResponse] 제거됨 - 즉시 초기화 방식으로 변경

        // 기본 기능
        handlers_[MessageType::Chat] = [this](const auto &params)
        { handleChat(params); };
        
        // AFK 검증 메시지 처리 (임시로 Chat 타입 재활용)
        // 실제 메시지는 "AFK_VERIFY" 형태로 전송

        spdlog::debug("MessageHandler 생성: 세션 {} (핸들러 수: {})",
                      session_ ? session_->getSessionId() : "nullptr", handlers_.size());
    }

    MessageHandler::~MessageHandler()
    {
        spdlog::debug("MessageHandler 소멸");
    }

    // ========================================
    // 메시지 처리 (업데이트됨)
    // ========================================

    void MessageHandler::handleMessage(const std::string &rawMessage)
    {
        if (!session_)
        {
            spdlog::error("Session이 null입니다");
            return;
        }

        try
        {
            spdlog::debug("📨 메시지 수신 ({}): {}, 현재 상태: {}",
                          session_->getSessionId(),
                          rawMessage.length() > 100 ? rawMessage.substr(0, 100) + "..." : rawMessage, (int)session_->getState());


            // AFK 관련 메시지 특별 처리
            if (rawMessage == "AFK_VERIFY") {
                handleAfkVerify();
                return;
            }
            if (rawMessage == "AFK_UNBLOCK") {
                handleAfkUnblock();
                return;
            }

            // 기존 텍스트 기반 메시지 처리
            auto [messageType, params] = parseMessage(rawMessage);

            spdlog::debug("파싱 결과: {} ({})",
                          messageTypeToString(messageType), static_cast<int>(messageType));

            // 핸들러 실행
            auto it = handlers_.find(messageType);
            if (it != handlers_.end())
            {
                it->second(params);
            }
            else
            {
                spdlog::warn("알 수 없는 메시지 타입: {} (원본: {})",
                             messageTypeToString(messageType), rawMessage);
                sendError("알 수 없는 명령어입니다");
            }
        }
        catch (const std::exception &e)
        {
            spdlog::error("메시지 처리 중 예외: {}", e.what());
            sendError("메시지 처리 중 오류가 발생했습니다");
        }
    }

    std::pair<MessageType, std::vector<std::string>> MessageHandler::parseMessage(const std::string &rawMessage)
    {
        // 기본 파싱
        auto parts = splitMessage(rawMessage, ':');
        if (parts.empty())
        {
            return {MessageType::Unknown, {}};
        }

        // 첫 번째 부분으로 MessageType 결정
        std::string commandStr = parts[0];
        
        // UTF-8 BOM 제거 (EF BB BF)
        if (commandStr.length() >= 3 && 
            (unsigned char)commandStr[0] == 0xEF && 
            (unsigned char)commandStr[1] == 0xBB && 
            (unsigned char)commandStr[2] == 0xBF) {
            commandStr = commandStr.substr(3);
            spdlog::warn("DEBUG: UTF-8 BOM removed from commandStr");
        }
        
        // 강화된 trim 처리 - 모든 제어 문자와 공백 제거
        commandStr.erase(0, commandStr.find_first_not_of(" \t\r\n\v\f\0"));
        commandStr.erase(commandStr.find_last_not_of(" \t\r\n\v\f\0") + 1);
        
        // 추가 안전장치: ASCII 제어 문자(0-31) 모두 제거
        commandStr.erase(std::remove_if(commandStr.begin(), commandStr.end(), 
            [](unsigned char c) { return c < 32; }), commandStr.end());
        
        spdlog::warn("DEBUG: parts.size()={}, commandStr='{}' (len={})", parts.size(), commandStr, commandStr.length());
        
        // 각 바이트 값 출력
        std::string hexDump = "";
        for(size_t i = 0; i < commandStr.length(); ++i) {
            char buffer[10];
            sprintf(buffer, "%02X ", (unsigned char)commandStr[i]);
            hexDump += buffer;
        }
        spdlog::warn("DEBUG: commandStr hex bytes: [{}]", hexDump);

        // room:xxx, game:xxx 형태 처리
        if (parts.size() >= 2)
        {
            spdlog::warn("DEBUG: commandStr='{}', parts[1]='{}', checking composite...", commandStr, parts[1]);
            bool isAuthMatch = (commandStr == "auth");
            spdlog::warn("DEBUG: (commandStr == \"auth\") = {}", isAuthMatch);
            
            if (commandStr == "room" || commandStr == "game" || commandStr == "lobby" || commandStr == "user" || commandStr == "version" || commandStr == "auth")
            {
                commandStr += ":" + parts[1];
                spdlog::warn("DEBUG: Composite command created: '{}'", commandStr);
                // 파라미터는 2번째 인덱스부터
                std::vector<std::string> params(parts.begin() + 2, parts.end());
                return {parseMessageType(commandStr), params};
            }
        }

        // 단일 명령어 (auth, ping 등)
        std::vector<std::string> params(parts.begin() + 1, parts.end());
        return {parseMessageType(commandStr), params};
    }

    std::vector<std::string> MessageHandler::splitMessage(const std::string &message, char delimiter)
    {
        std::vector<std::string> tokens;
        std::stringstream ss(message);
        std::string token;

        while (std::getline(ss, token, delimiter))
        {
            if (!token.empty())
            {
                tokens.push_back(token);
            }
        }

        return tokens;
    }

    void MessageHandler::sendResponse(const std::string &response)
    {
        sendTextMessage(response);
    }

    void MessageHandler::sendTextMessage(const std::string &message)
    {
        if (session_)
        {
            session_->sendMessage(message);
        }
    }

    void MessageHandler::sendError(const std::string &errorMessage)
    {
        spdlog::debug("📤 에러 응답 전송: {}", errorMessage);
        sendTextMessage("ERROR:" + errorMessage);
    }

    // ========================================
    // 인증 관련 핸들러들
    // ========================================

    void MessageHandler::handleAuth(const std::vector<std::string> &params)
    {
        if (!authService_)
        {
            sendError("인증 서비스를 사용할 수 없습니다");
            return;
        }

        if (!session_->isConnected())
        {
            sendError("인증을 진행할 수 있는 상태가 아닙니다");
            return;
        }

        if (params.size() < 1)
        {
            sendError("사용법: auth:사용자명:비밀번호 또는 auth:jwt:토큰");
            return;
        }

        AuthResult result;

        // 모바일 클라이언트 전용 JWT 인증 (mobile_jwt)  
        if (params.size() == 2 && params[0] == "mobile_jwt")
        {
            std::string accessToken = params[1];
            result = authService_->authenticateMobileClient(accessToken);
            spdlog::info("모바일 클라이언트 JWT 인증 시도: {}", accessToken.substr(0, 20) + "...");
        }
        // 표준 JWT 인증 (jwt) - 모바일 및 기존 클라이언트 공통
        else if (params.size() == 2 && params[0] == "jwt")
        {
            std::string jwtToken = params[1];
            result = authService_->loginWithJwt(jwtToken);
            spdlog::info("클라이언트 JWT 토큰 인증 시도: {}", jwtToken.substr(0, 20) + "...");
        }
        // 기존 JWT 토큰인지 확인 (JWT는 '.'로 구분된 3개 부분으로 구성)
        else if (params.size() == 1 && std::count(params[0].begin(), params[0].end(), '.') == 2)
        {
            // 데스크톱 클라이언트 JWT 토큰 인증
            std::string jwtToken = params[0];
            result = authService_->loginWithJwt(jwtToken);
            spdlog::info("데스크톱 클라이언트 JWT 토큰 인증 시도: {}", jwtToken.substr(0, 20) + "...");
        }
        else if (params.size() >= 2 && params[0] != "mobile_jwt" && params[0] != "jwt")
        {
            // 기존 username/password 인증
            std::string username = params[0];
            std::string password = params[1];
            result = authService_->loginUser(username, password);
            spdlog::info("사용자명/비밀번호 인증 시도: {}", username);
        }
        else
        {
            sendError("사용법: auth:사용자명:비밀번호, auth:jwt:토큰, 또는 auth:mobile_jwt:access_token");
            return;
        }

        if (result.success)
        {
            session_->setAuthenticated(result.userId, result.username);

            // DB에서 사용자 계정 정보를 불러와 세션에 저장
            if (databaseManager_)
            {
                auto userAccount = databaseManager_->getUserByUsername(result.username);
                if (userAccount.has_value())
                {
                    session_->setUserAccount(userAccount.value());
                    spdlog::debug("💾 사용자 계정 정보 로드 완료: {} (레벨: {}, 경험치: {})",
                                  result.username, userAccount->level, userAccount->experiencePoints);
                }
                else
                {
                    spdlog::warn("⚠️ 사용자 계정 정보를 찾을 수 없음: {}", result.username);
                }
            }

            // 완전한 사용자 정보를 ':' 구분자 형태로 전송 (기존 프로토콜 준수)
            auto userAccountFromDB = databaseManager_->getUserByUsername(result.username);
            if (databaseManager_ && userAccountFromDB.has_value()) {
                // ':' 구분자 기반 프로토콜로 사용자 정보 전송
                std::ostringstream userInfoStream;
                userInfoStream << "AUTH_SUCCESS:" << result.username << ":" << result.sessionToken 
                    << ":" << userAccountFromDB->displayName
                    << ":" << userAccountFromDB->level
                    << ":" << userAccountFromDB->totalGames
                    << ":" << userAccountFromDB->wins
                    << ":" << userAccountFromDB->losses
                    << ":" << userAccountFromDB->totalScore
                    << ":" << userAccountFromDB->bestScore
                    << ":" << userAccountFromDB->experiencePoints;
                sendResponse(userInfoStream.str());
            } else {
                // 폴백: 기본 정보만 전송 (0으로 초기화)
                sendResponse("AUTH_SUCCESS:" + result.username + ":" + result.sessionToken + ":" + result.username + ":1:0:0:0:0:0:0");
            }

            // 로그인 성공 시 자동으로 로비에 입장되므로 다른 사용자들에게 브로드캐스트
            broadcastLobbyUserJoined(result.username);

            // 로그인 완료 후 즉시 로비 정보 전송 (사용자 목록 + 방 목록 + 사용자 통계)
            sendLobbyUserList();
            sendRoomList();

            // 로그인 시 사용자 통계 정보 자동 전송
            try
            {
                std::string statsResponse = generateUserStatsResponse();
                sendResponse(statsResponse);
                spdlog::debug("✅ 로그인 후 사용자 통계 전송 완료: '{}'", result.username);
            }
            catch (const std::exception &e)
            {
                spdlog::warn("로그인 후 사용자 통계 전송 실패: {}", e.what());
            }

            spdlog::info("✅ 로그인 성공: {} ({}) - 로비 진입 및 정보 전송 완료", result.username, session_->getSessionId());
        }
        else
        {
            sendError(result.message);
            spdlog::warn("❌ 로그인 실패: {} - {}", result.username.empty() ? "알 수 없음" : result.username, result.message);
        }

        // 🔥 콜백 제거: 직접 처리 완료
    }

    void MessageHandler::handleRegister(const std::vector<std::string> &params)
    {
        if (!authService_)
        {
            sendError("인증 서비스를 사용할 수 없습니다");
            return;
        }

        if (!session_->isConnected())
        {
            sendError("회원 가입을 진행할 수 있는 상태가 아닙니다");
            return;
        }

        if (params.size() < 2)
        {
            sendError("사용법: register:사용자명:비밀번호 (또는 register:사용자명:이메일:비밀번호)");
            return;
        }

        std::string username = params[0];
        std::string password;

        if (params.size() >= 3)
        {
            // register:사용자명:이메일:비밀번호 형식 (이메일이 빈 값일 수도 있음)
            password = params[2];
        }
        else
        {
            // register:사용자명:비밀번호 형식 (이메일 생략)
            password = params[1];
        }

        auto result = authService_->registerUser(username, password);

        if (result.success)
        {
            sendResponse("REGISTER_SUCCESS:" + username);
            spdlog::info("✅ 회원가입 성공: {}", username);
        }
        else
        {
            sendError(result.message);
            spdlog::error("❌ 회원가입 실패: {} - {}", username, result.message);
        }

        // 🔥 콜백 제거: 직접 처리 완료
    }

    void MessageHandler::handleLoginGuest(const std::vector<std::string> &params)
    {
        if (!authService_)
        {
            sendError("인증 서비스를 사용할 수 없습니다");
            return;
        }

        if (!session_->isConnected())
        {
            sendError("게스트 로그인을 진행할 수 있는 상태가 아닙니다");
            return;
        }

        std::string guestName = params.empty() ? "" : params[0];

        auto result = authService_->loginGuest(guestName);

        if (result.success)
        {
            session_->setAuthenticated(result.userId, result.username);

            // DB에서 사용자 계정 정보 조회 (게스트도 DB에 저장될 수 있음)
            if (databaseManager_)
            {
                auto userAccount = databaseManager_->getUserByUsername(result.username);
                if (userAccount.has_value())
                {
                    session_->setUserAccount(userAccount.value());
                    spdlog::debug("💾 게스트 계정 정보 로드 완료: {} (레벨: {}, 경험치: {})",
                                  result.username, userAccount->level, userAccount->experiencePoints);
                }
                else
                {
                    spdlog::debug("💾 게스트 계정 정보 없음: {} (기본값 사용)", result.username);
                }
            }

            sendResponse("GUEST_LOGIN_SUCCESS:" + result.username + ":" + result.sessionToken);

            // 게스트 로그인 성공 시 자동으로 로비에 입장되므로 다른 사용자들에게 브로드캐스트
            broadcastLobbyUserJoined(result.username);

            // 게스트 로그인 완료 후 즉시 로비 정보 전송 (사용자 목록 + 방 목록 + 사용자 통계)
            sendLobbyUserList();
            sendRoomList();

            // 게스트 로그인 시 사용자 통계 정보 자동 전송
            try
            {
                std::string statsResponse = generateUserStatsResponse();
                sendResponse(statsResponse);
                spdlog::debug("✅ 게스트 로그인 후 사용자 통계 전송 완료: '{}'", result.username);
            }
            catch (const std::exception &e)
            {
                spdlog::warn("게스트 로그인 후 사용자 통계 전송 실패: {}", e.what());
            }

            spdlog::info("게스트 로그인: {} ({}) - 로비 진입 및 정보 전송 완료", result.username, session_->getSessionId());
        }
        else
        {
            sendError(result.message);
        }
    }

    void MessageHandler::handleLogout(const std::vector<std::string> &params)
    {
        if (!session_->isInLobby())
        {
            sendError("로그인 상태가 아닙니다");
            return;
        }

        std::string username = session_->getUsername();

        // 세션 상태 초기화
        session_->setStateToConnected();

        sendResponse("LOGOUT_SUCCESS");
        spdlog::info("사용자 로그아웃: {} ({})", username, session_->getSessionId());
    }

    void MessageHandler::handleSessionValidate(const std::vector<std::string> &params)
    {
        if (!authService_)
        {
            sendError("인증 서비스를 사용할 수 없습니다");
            return;
        }

        if (params.empty())
        {
            sendError("사용법: validate:세션토큰");
            return;
        }

        std::string sessionToken = params[0];
        auto sessionInfo = authService_->validateSession(sessionToken);

        if (sessionInfo)
        {
            sendResponse("SESSION_VALID:" + sessionInfo->username + ":" + sessionInfo->userId);
        }
        else
        {
            sendError("세션이 유효하지 않습니다");
        }
    }

    // ========================================
    // 방 관련 핸들러들 (완전 구현)
    // ========================================

    void MessageHandler::handleCreateRoom(const std::vector<std::string> &params)
    {
        // 1. 상태 검증
        if (!session_->canCreateRoom())
        {
            spdlog::warn("🚫 방 생성 거부됨 - 상태 검증 실패");

            if (session_->isInRoom())
            {
                spdlog::warn("🚫 이유: 이미 방에 참여 중");
                sendError("이미 방에 참여 중입니다. 먼저 방을 나가주세요");
            }
            else if (session_->isInGame())
            {
                spdlog::warn("🚫 이유: 게임 중");
                sendError("게임 중에는 방을 만들 수 없습니다");
            }
            else
            {
                spdlog::warn("🚫 이유: 기타 (상태: {})", static_cast<int>(session_->getState()));
                sendError("현재 상태에서는 방을 만들 수 없습니다");
            }

            spdlog::warn("🚫 return 실행");
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_)
        {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        // 3. 파라미터 검증
        if (params.empty())
        {
            sendError("사용법: room:create:방이름[:비공개(0/1)[:비밀번호]]");
            return;
        }

        try
        {
            std::string roomName = params[0];
            bool isPrivate = (params.size() > 1 && params[1] == "1");
            std::string password = (params.size() > 2) ? params[2] : "";

            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            spdlog::info("🏠 방 생성 요청: '{}' by '{}' (비공개: {})",
                         roomName, username, isPrivate);

            // 4. RoomManager를 통한 방 생성
            int roomId = roomManager_->createRoom(userId, username, roomName, isPrivate, password);

            if (roomId > 0)
            {
                // 5. 호스트를 방에 추가 (세션도 함께)
                if (roomManager_->joinRoom(roomId, session_->shared_from_this(), userId, username, password))
                {
                    // 6. 세션 상태 변경
                    session_->setStateToInRoom(roomId);

                    // 7. 브로드캐스트 (데드락 방지를 위해 여기서 호출)
                    auto room = roomManager_->getRoom(roomId);
                    if (room)
                    {
                        room->broadcastPlayerJoined(username);
                    }

                    // 8. 성공 응답
                    std::ostringstream response;
                    response << "ROOM_CREATED:" << roomId << ":" << roomName;
                    sendResponse(response.str());

                    // 9. 방 정보 전체 동기화 전솥
                    sendRoomInfo(room);

                    spdlog::info("✅ 방 생성 성공: '{}' by '{}' (ID: {})", roomName, username, roomId);
                }
                else
                {
                    // 방 생성은 되었지만 호스트 추가 실패 - 방 제거
                    roomManager_->removeRoom(roomId);
                    sendError("방 생성 후 호스트 추가에 실패했습니다");
                    spdlog::error("❌ 방 {} 호스트 추가 실패", roomId);
                }
            }
            else
            {
                // 방 생성 실패 - 실패 코드에 따른 메시지
                switch (roomId)
                {
                case -1:
                    sendError("유효하지 않은 방 이름입니다");
                    break;
                case -2:
                    sendError("이미 다른 방에 참여 중입니다");
                    break;
                case -3:
                    sendError("서버에 방이 가득 찼습니다");
                    break;
                default:
                    sendError("방 생성에 실패했습니다");
                    break;
                }
                spdlog::error("❌ 방 생성 실패: '{}' (코드: {})", roomName, roomId);
            }
        }
        catch (const std::exception &e)
        {
            sendError("방 생성 중 오류가 발생했습니다");
            spdlog::error("방 생성 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleJoinRoom(const std::vector<std::string> &params)
    {
        // 1. 상태 검증
        if (!session_->canJoinRoom())
        {
            if (session_->isInRoom())
            {
                sendError("이미 방에 참여 중입니다");
            }
            else if (session_->isInGame())
            {
                sendError("게임 중에는 다른 방에 참여할 수 없습니다");
            }
            else
            {
                sendError("현재 상태에서는 방에 참여할 수 없습니다");
            }
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_)
        {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        // 3. 파라미터 검증
        if (params.empty())
        {
            sendError("사용법: room:join:방ID[:비밀번호]");
            return;
        }

        try
        {
            int roomId = std::stoi(params[0]);
            std::string password = (params.size() > 1) ? params[1] : "";

            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            spdlog::info("🏠 방 참여 요청: '{}' -> 방 {}", username, roomId);

            // 4. 방 존재 확인
            auto room = roomManager_->getRoom(roomId);
            if (!room)
            {
                sendError("존재하지 않는 방입니다");
                return;
            }

            // 5. 게임 중인 방 참여 제한
            if (room->isPlaying())
            {
                sendError("진행 중인 게임에는 참여할 수 없습니다");
                return;
            }

            // 6. 방이 가득 찬지 확인
            if (room->isFull())
            {
                sendError("방이 가득 찼습니다");
                return;
            }

            // 7. RoomManager를 통한 방 참여
            if (roomManager_->joinRoom(roomId, session_->shared_from_this(), userId, username, password))
            {
                // 8. 세션 상태 변경
                session_->setStateToInRoom(roomId);

                // 9. 성공 응답 (방 정보 포함) - 브로드캐스트 전에 먼저 응답
                std::ostringstream response;
                response << "ROOM_JOIN_SUCCESS:" << roomId << ":" << room->getRoomName()
                         << ":" << room->getPlayerCount() << "/" << room->getMaxPlayers();
                sendResponse(response.str());

                // 10. 브로드캐스트 (새 플레이어 입장 알림)
                room->broadcastPlayerJoined(username);

                // 11. 방 전체 사용자에게 업데이트된 방 정보 전송 (플레이어 목록 동기화)
                broadcastRoomInfoToRoom(room);

                // 12. 새로 입장한 플레이어에게 게임 리셋 상태 동기화
                // 방이 대기 상태이고 이전에 게임이 진행되었다면 리셋 신호 전송
                if (!room->isPlaying() && room->hasCompletedGame())
                {
                    session_->sendMessage("GAME_RESET");
                    session_->sendMessage("SYSTEM:새로운 게임을 시작할 수 있습니다!");
                    spdlog::info("🔄 새 플레이어 {}에게 게임 리셋 상태 동기화 완료", username);
                }

                spdlog::info("✅ 방 참여 성공: '{}' -> 방 {} ({}명)",
                             username, roomId, room->getPlayerCount());
            }
            else
            {
                sendError("방 참여에 실패했습니다");
                spdlog::warn("❌ 방 참여 실패: '{}' -> 방 {}", username, roomId);
            }
        }
        catch (const std::invalid_argument &e)
        {
            sendError("잘못된 방 ID 형식입니다");
        }
        catch (const std::out_of_range &e)
        {
            sendError("방 ID가 범위를 벗어났습니다");
        }
        catch (const std::exception &e)
        {
            sendError("방 참여 중 오류가 발생했습니다");
            spdlog::error("방 참여 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleLeaveRoom(const std::vector<std::string> &params)
    {
        if (!session_->canLeaveRoom())
        {
            if (session_->isInLobby())
            {
                sendError("방에 참여하지 않았습니다");
            }
            else
            {
                sendError("현재 상태에서는 방을 나갈 수 없습니다");
            }
            return;
        }

        if (!roomManager_)
        {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        try
        {
            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();
            int currentRoomId = session_->getCurrentRoomId();

            spdlog::info("🏠 방 나가기 요청: '{}' <- 방 {}", username, currentRoomId);

            if (roomManager_->leaveRoom(userId))
            {
                session_->setStateToLobby(true);  // 방에서 나와서 로비로 이동

                auto room = roomManager_->getRoom(currentRoomId);
                if (room && !room->isEmpty())
                {
                    broadcastRoomInfoToRoom(room);
                }

                sendResponse("ROOM_LEFT:OK");
                spdlog::info("✅ 방 나가기 성공: '{}'", username);

                // 🎯 방 나간 후 DB에서 최신 스탯 정보 강제 조회하여 전송
                try
                {
                    // DB에서 최신 사용자 정보 강제 조회 (게임 결과 반영 보장)
                    auto dbManager = gameServer_->getDatabaseManager();
                    if (dbManager)
                    {
                        auto updatedAccount = dbManager->getUserByUsername(username);
                        if (updatedAccount.has_value())
                        {
                            session_->setUserAccount(updatedAccount.value());
                            spdlog::debug("🔄 방 나가기 후 세션 정보 DB 강제 동기화: '{}'", username);
                        }
                    }

                    std::string statsResponse = generateUserStatsResponse();
                    sendResponse(statsResponse);
                    spdlog::debug("✅ 방 나가기 후 사용자 통계 전송 완료: '{}'", username);
                }
                catch (const std::exception &e)
                {
                    spdlog::warn("방 나가기 후 사용자 통계 전송 실패: {}", e.what());
                }
            }
            else
            {
                sendError("방 나가기에 실패했습니다");
                spdlog::warn("❌ 방 나가기 실패: '{}'", username);
            }
        }
        catch (const std::exception &e)
        {
            sendError("방 나가기 중 오류가 발생했습니다");
            spdlog::error("방 나가기 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleRoomList(const std::vector<std::string> &params)
    {
        if (!roomManager_)
        {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        try
        {
            auto roomList = roomManager_->getRoomList();

            std::ostringstream response;
            response << "ROOM_LIST:" << roomList.size();

            for (const auto &room : roomList)
            {
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
        catch (const std::exception &e)
        {
            sendError("방 목록 조회 중 오류가 발생했습니다");
            spdlog::error("방 목록 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handlePlayerReady(const std::vector<std::string> &params)
    {
        // 1. 상태 검증
        if (!session_->isInRoom())
        {
            sendError("방에 있어야 준비 상태를 변경할 수 있습니다");
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_)
        {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        try
        {
            bool ready = (!params.empty() && params[0] == "1");
            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            spdlog::info("🎮 플레이어 준비 상태 변경: '{}' -> {}",
                         username, ready ? "준비" : "대기");

            // 3. RoomManager를 통한 준비 상태 설정 (브로드캐스트는 내부에서 처리)
            if (roomManager_->setPlayerReady(userId, ready))
            {
                // 4. 성공 응답
                std::string readyStatus = ready ? "1" : "0";
                sendResponse("PLAYER_READY:" + readyStatus);

                spdlog::debug("✅ 플레이어 준비 상태 변경 성공: '{}'", username);
            }
            else
            {
                sendError("준비 상태 변경에 실패했습니다");
                spdlog::warn("❌ 플레이어 준비 상태 변경 실패: '{}'", username);
            }
        }
        catch (const std::exception &e)
        {
            sendError("준비 상태 변경 중 오류가 발생했습니다");
            spdlog::error("플레이어 준비 상태 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleStartGame(const std::vector<std::string> &params)
    {
        // 1. 상태 검증
        if (!session_->canStartGame())
        {
            if (session_->isInLobby())
            {
                sendError("방에 참여한 후 게임을 시작할 수 있습니다");
            }
            else if (session_->isInGame())
            {
                sendError("이미 게임이 진행 중입니다");
            }
            else
            {
                sendError("현재 상태에서는 게임을 시작할 수 없습니다");
            }
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_)
        {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        try
        {
            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();
            int roomId = session_->getCurrentRoomId();

            spdlog::info("🎮 게임 시작 요청: '{}' (방 {})", username, roomId);

            // 3. 호스트 권한 확인
            auto room = roomManager_->getRoom(roomId);
            if (!room)
            {
                sendError("방을 찾을 수 없습니다");
                return;
            }

            if (!room->isHost(userId))
            {
                sendError("호스트만 게임을 시작할 수 있습니다");
                return;
            }

            // 4. 게임 시작 조건 확인
            if (!room->canStartGame())
            {
                sendError("게임 시작 조건이 충족되지 않았습니다. 모든 플레이어가 준비되었는지 확인하세요");
                return;
            }

            // 5. RoomManager를 통한 게임 시작 (브로드캐스트는 startGame 내부에서 처리됨)
            if (roomManager_->startGame(userId))
            {
                // 게임 시작 성공 - 세션 상태는 이미 startGame()에서 설정됨
                spdlog::info("✅ 게임 시작 성공: 사용자 {}", userId);

                // 8. 성공 응답
                sendResponse("GAME_START_SUCCESS");

                spdlog::info("✅ 게임 시작 성공: '{}' (방 {}, {}명)",
                             username, roomId, room->getPlayerCount());
            }
            else
            {
                sendError("게임 시작에 실패했습니다");
                spdlog::warn("❌ 게임 시작 실패: '{}' (방 {})", username, roomId);
            }
        }
        catch (const std::exception &e)
        {
            sendError("게임 시작 중 오류가 발생했습니다");
            spdlog::error("게임 시작 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleEndGame(const std::vector<std::string> &params)
    {
        // 1. 상태 검증
        if (!session_->isInGame())
        {
            sendError("게임 중이 아닙니다");
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_)
        {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        try
        {
            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();
            int roomId = session_->getCurrentRoomId();

            spdlog::info("🎮 게임 종료 요청: '{}' (방 {})", username, roomId);

            // 3. 호스트 권한 확인 (또는 특별한 조건)
            auto room = roomManager_->getRoom(roomId);
            if (!room)
            {
                sendError("방을 찾을 수 없습니다");
                return;
            }

            if (!room->isHost(userId))
            {
                sendError("호스트만 게임을 종료할 수 있습니다");
                return;
            }

            // 4. RoomManager를 통한 게임 종료
            if (roomManager_->endGame(roomId))
            {
                // 5. 방의 모든 플레이어 세션 상태를 방 대기로 변경
                auto playerList = room->getPlayerList();
                for (const auto &player : playerList)
                {
                    if (player.getSession())
                    {
                        player.getSession()->setStateToInRoom(roomId);
                    }
                }

                // 7. 성공 응답
                sendResponse("GAME_END_SUCCESS");

                spdlog::info("✅ 게임 종료 성공: '{}' (방 {})", username, roomId);
            }
            else
            {
                sendError("게임 종료에 실패했습니다");
                spdlog::warn("❌ 게임 종료 실패: '{}' (방 {})", username, roomId);
            }
        }
        catch (const std::exception &e)
        {
            sendError("게임 종료 중 오류가 발생했습니다");
            spdlog::error("게임 종료 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleTransferHost(const std::vector<std::string> &params)
    {
        // 1. 상태 검증
        if (!session_->isInRoom())
        {
            sendError("방에 있어야 호스트를 이양할 수 있습니다");
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_)
        {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        // 3. 파라미터 검증
        if (params.empty())
        {
            sendError("사용법: room:transfer:대상플레이어ID");
            return;
        }

        try
        {
            std::string currentHostId = session_->getUserId();
            std::string newHostId = params[0];
            int roomId = session_->getCurrentRoomId();

            spdlog::info("👑 호스트 이양 요청: '{}' -> '{}' (방 {})",
                         currentHostId, newHostId, roomId);

            // 4. RoomManager를 통한 호스트 이양
            if (roomManager_->transferHost(roomId, currentHostId, newHostId))
            {
                // 5. 브로드캐스트 (데드락 방지를 위해 여기서 호출)
                auto room = roomManager_->getRoom(roomId);
                if (room)
                {
                    // 새 호스트 이름 찾기
                    const PlayerInfo *newHost = room->getPlayer(newHostId);
                    if (newHost)
                    {
                        room->broadcastHostChanged(newHost->getUsername(), newHost->getDisplayName());
                    }
                }

                // 6. 성공 응답
                sendResponse("HOST_TRANSFER_SUCCESS:" + newHostId);

                spdlog::info("✅ 호스트 이양 성공: '{}' -> '{}' (방 {})",
                             currentHostId, newHostId, roomId);
            }
            else
            {
                sendError("호스트 이양에 실패했습니다");
                spdlog::warn("❌ 호스트 이양 실패: '{}' -> '{}' (방 {})",
                             currentHostId, newHostId, roomId);
            }
        }
        catch (const std::exception &e)
        {
            sendError("호스트 이양 중 오류가 발생했습니다");
            spdlog::error("호스트 이양 처리 중 예외: {}", e.what());
        }
    }

    // ========================================
    // 게임 관련 핸들러들
    // ========================================

    void MessageHandler::handleGameMove(const std::vector<std::string> &params)
    {
        // 1. 상태 검증
        if (!session_->canMakeGameMove())
        {
            if (session_->isInLobby())
            {
                sendError("게임에 참여한 후 이동할 수 있습니다");
            }
            else if (session_->isInRoom())
            {
                sendError("게임이 시작된 후 이동할 수 있습니다");
            }
            else
            {
                sendError("현재 상태에서는 게임 이동을 할 수 없습니다");
            }
            return;
        }

        // 2. 매니저 유효성 확인
        if (!roomManager_)
        {
            sendError("방 관리자가 초기화되지 않았습니다");
            return;
        }

        // 3. 파라미터 검증 (블록 타입, 위치, 회전, 뒤집기 등)
        if (params.size() < 4)
        {
            sendError("사용법: game:move:블록타입:x좌표:y좌표:회전도[:뒤집기]");
            return;
        }

        try
        {
            std::string userId = session_->getUserId();
            int roomId = session_->getCurrentRoomId();

            // 방과 게임 로직 가져오기
            auto room = roomManager_->getRoom(roomId);
            if (!room || !room->isPlaying())
            {
                sendError("게임이 진행 중이 아닙니다");
                return;
            }

            // 파라미터 파싱: 블록타입:x좌표:y좌표:회전도[:뒤집기]
            std::string blockTypeStr = params[0];
            int x = std::stoi(params[1]);
            int y = std::stoi(params[2]);
            int rotation = std::stoi(params[3]);
            int flip = (params.size() > 4) ? std::stoi(params[4]) : 0;

            // 블록 배치 정보 생성
            Common::BlockPlacement placement;
            placement.type = static_cast<Common::BlockType>(std::stoi(blockTypeStr));
            placement.position = {y, x}; // row, col 순서
            placement.rotation = static_cast<Common::Rotation>(rotation);
            placement.flip = static_cast<Common::FlipState>(flip);

            // 플레이어 색상 설정
            auto *player = room->getPlayer(userId);
            if (!player)
            {
                sendError("플레이어 정보를 찾을 수 없습니다");
                return;
            }
            placement.player = player->getColor();

            // 블록 배치 시도
            bool success = room->handleBlockPlacement(userId, placement);
            if (success)
            {
                spdlog::info("🎮 블록 배치 성공: '{}' (방 {}, 위치: {},{}, 타입: {})",
                             userId, roomId, y, x, static_cast<int>(placement.type));

                // 성공 응답 (브로드캐스트는 handleBlockPlacement에서 처리됨)
                sendResponse("GAME_MOVE_SUCCESS");
            }
            else
            {
                sendError("블록 배치에 실패했습니다");
            }
        }
        catch (const std::exception &e)
        {
            sendError("게임 이동 중 오류가 발생했습니다");
            spdlog::error("게임 이동 처리 중 예외: {}", e.what());
        }
    }

    // handleGameResultResponse 메서드 제거됨 - 즉시 초기화 방식으로 변경

    // ========================================
    // 로비 관련 핸들러들
    // ========================================

    void MessageHandler::handleLobbyEnter(const std::vector<std::string> &params)
    {
        // 방에 있는 경우 로비 진입 거부
        if (session_->isInRoom())
        {
            sendError("이미 방에 참여 중입니다. 먼저 방을 나가주세요");
            return;
        }

        // 연결되지 않은 사용자는 로비 진입 거부
        if (!session_->isConnected())
        {
            sendError("로그인 후 로비에 입장할 수 있습니다");
            return;
        }

        try
        {
            std::string username = session_->getUsername();
            bool wasAlreadyInLobby = session_->isInLobby();

            spdlog::info("🏢 로비 입장/새로고침: '{}' (기존 로비 상태: {})", username, wasAlreadyInLobby);

            // 로비 상태로 명시적 설정
            if (!session_->isInLobby())
            {
                session_->setStateToLobby();
            }

            // 2. 로비 입장 성공 응답 (먼저 전송)
            sendResponse("LOBBY_ENTER_SUCCESS");

            // 3. 다른 사용자들에게 새 사용자 입장 브로드캐스트 (새로 입장한 경우만)
            // 방에서 나와서 로비로 온 경우는 입장 메시지를 보내지 않음
            if (!wasAlreadyInLobby && !session_->justLeftRoom())
            {
                broadcastLobbyUserJoined(username);
            }

            // 4. 로비 사용자 목록 즉시 전송 (본인이 포함된 최신 목록)
            sendLobbyUserList();

            // 5. 방 목록 전송
            sendRoomList();

            // 6. 방에서 나온 플래그 리셋 (로비 입장 처리 완료 후)
            session_->clearJustLeftRoomFlag();

            spdlog::debug("✅ 로비 입장/새로고침 완료: '{}'", username);
        }
        catch (const std::exception &e)
        {
            sendError("로비 입장 중 오류가 발생했습니다");
            spdlog::error("로비 입장 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleLobbyLeave(const std::vector<std::string> &params)
    {
        try
        {
            std::string username = session_->getUsername();
            spdlog::info("🏢 로비 퇴장: '{}'", username);

            // 다른 사용자들에게 사용자 퇴장 브로드캐스트
            broadcastLobbyUserLeft(username);

            sendResponse("LOBBY_LEAVE_SUCCESS");
            spdlog::debug("✅ 로비 퇴장 완료: '{}'", username);
        }
        catch (const std::exception &e)
        {
            sendError("로비 퇴장 중 오류가 발생했습니다");
            spdlog::error("로비 퇴장 처리 중 예외: {}", e.what());
        }
    }

    void MessageHandler::handleLobbyList(const std::vector<std::string> &params)
    {
        try
        {
            // 현재 로비에 있는 사용자 목록 전송
            sendLobbyUserList();
        }
        catch (const std::exception &e)
        {
            sendError("로비 목록 조회 중 오류가 발생했습니다");
            spdlog::error("로비 목록 처리 중 예외: {}", e.what());
        }
    }

    // ========================================
    // 기본 핸들러들
    // ========================================

    void MessageHandler::handlePing(const std::vector<std::string> &params)
    {
        sendResponse("pong");
    }

    void MessageHandler::handleChat(const std::vector<std::string> &params)
    {
        if (!session_->isConnected())
        {
            sendError("채팅은 로그인 후 이용 가능합니다");
            return;
        }

        if (params.empty())
        {
            sendError("메시지 내용이 필요합니다");
            return;
        }

        // 파라미터들을 하나의 메시지로 합치기
        std::string message;
        for (size_t i = 0; i < params.size(); ++i)
        {
            if (i > 0)
                message += " ";
            message += params[i];
        }

        std::string username = session_->getUsername();
        spdlog::info("채팅 메시지: [{}] {}", username, message);

        // 채팅 메시지 브로드캐스팅
        try
        {
            if (session_->isInLobby())
            {
                // 로비 채팅 - 모든 로비 사용자에게 브로드캐스팅
                broadcastLobbyChatMessage(username, message);
            }
            else if (session_->isInRoom() || session_->isInGame())
            {
                // 방 채팅 (게임 중 포함) - 같은 방의 플레이어들에게 브로드캐스팅
                broadcastRoomChatMessage(username, message);
            }

            // 성공 응답
            sendResponse("CHAT_SUCCESS");
        }
        catch (const std::exception &e)
        {
            spdlog::error("채팅 메시지 브로드캐스팅 중 오류: {}", e.what());
            sendError("채팅 메시지 전송에 실패했습니다");
        }
    }

    // ========================================
    // 로비 브로드캐스팅 헬퍼 함수들
    // ========================================

    void MessageHandler::sendLobbyUserList()
    {
        try
        {
            if (!gameServer_)
            {
                spdlog::warn("GameServer가 null이므로 로비 사용자 목록 조회 불가");
                return;
            }

            // GameServer에서 실제 로비 사용자 목록을 가져옴
            auto lobbyUsers = gameServer_->getLobbyUsers();

            std::ostringstream response;
            response << "LOBBY_USER_LIST:" << lobbyUsers.size();

            int validUserCount = 0;
            for (const auto &lobbySession : lobbyUsers)
            {
                if (lobbySession && lobbySession->isActive() && !lobbySession->getUsername().empty())
                {
                    std::string username = lobbySession->getUsername();
                    std::string displayName = lobbySession->getDisplayName();
                    int userLevel = lobbySession->getUserLevel();
                    std::string userStatus = lobbySession->getUserStatusString();

                    response << ":" << username << "," << displayName << "," << userLevel << "," << userStatus;
                    validUserCount++;
                }
            }
            sendResponse(response.str());
        }
        catch (const std::exception &e)
        {
            spdlog::error("로비 사용자 목록 전송 중 오류: {}", e.what());
        }
    }

    void MessageHandler::sendRoomList()
    {
        if (roomManager_)
        {
            // RoomManager의 기존 기능 사용
            handleRoomList({});
        }
    }

    void MessageHandler::broadcastLobbyUserJoined(const std::string &username)
    {
        try
        {
            if (!gameServer_)
            {
                spdlog::warn("GameServer가 null이므로 로비 브로드캐스트 불가");
                return;
            }

            std::string message = "LOBBY_USER_JOINED:" + username;
            spdlog::info("📢 로비 사용자 입장 브로드캐스트: {}", username);

            // GameServer를 통해 로비의 모든 사용자에게 브로드캐스트
            auto lobbyUsers = gameServer_->getLobbyUsers();
            for (const auto &lobbySession : lobbyUsers)
            {
                if (lobbySession && lobbySession->isActive())
                {
                    lobbySession->sendMessage(message);
                }
            }

            spdlog::debug("로비 사용자 {}명에게 입장 브로드캐스트 완료", lobbyUsers.size());
        }
        catch (const std::exception &e)
        {
            spdlog::error("로비 사용자 입장 브로드캐스트 중 오류: {}", e.what());
        }
    }

    void MessageHandler::broadcastLobbyUserLeft(const std::string &username)
    {
        try
        {
            if (!gameServer_)
            {
                spdlog::warn("GameServer가 null이므로 로비 브로드캐스트 불가");
                return;
            }

            std::string message = "LOBBY_USER_LEFT:" + username;
            spdlog::info("📢 로비 사용자 퇴장 브로드캐스트: {}", username);

            // GameServer를 통해 로비의 모든 사용자에게 브로드캐스트
            auto lobbyUsers = gameServer_->getLobbyUsers();
            for (const auto &lobbySession : lobbyUsers)
            {
                if (lobbySession && lobbySession->isActive())
                {
                    lobbySession->sendMessage(message);
                }
            }

            spdlog::debug("로비 사용자 {}명에게 퇴장 브로드캐스트 완료", lobbyUsers.size());
        }
        catch (const std::exception &e)
        {
            spdlog::error("로비 사용자 퇴장 브로드캐스트 중 오류: {}", e.what());
        }
    }

    // 채팅 브로드캐스팅 헬퍼 함수들
    void MessageHandler::broadcastLobbyChatMessage(const std::string &username, const std::string &message)
    {
        try
        {
            if (!gameServer_)
            {
                spdlog::warn("GameServer가 null이므로 로비 채팅 브로드캐스트 불가");
                return;
            }

            // Get display name from session if available, fallback to username
            std::string displayName = username;
            if (session_ && session_->isActive()) {
                displayName = session_->getDisplayName();
            }
            std::string chatMessage = "CHAT:" + username + ":" + displayName + ":" + message;
            
            // GameServer를 통해 실제 로비에 있는 사용자에게만 브로드캐스트
            auto lobbyUsers = gameServer_->getActualLobbyUsers();
            spdlog::info("📢 로비 채팅 브로드캐스트: [{}] {} -> {}명의 로비 사용자에게", username, message, lobbyUsers.size());
            for (const auto &lobbySession : lobbyUsers)
            {
                if (lobbySession && lobbySession->isActive())
                {
                    lobbySession->sendMessage(chatMessage);
                }
            }

            spdlog::debug("로비 사용자 {}명에게 채팅 브로드캐스트 완료", lobbyUsers.size());
        }
        catch (const std::exception &e)
        {
            spdlog::error("로비 채팅 브로드캐스트 중 오류: {}", e.what());
        }
    }

    void MessageHandler::broadcastRoomChatMessage(const std::string &username, const std::string &message)
    {
        try
        {
            if (!roomManager_)
            {
                spdlog::warn("RoomManager가 null이므로 방 채팅 브로드캐스트 불가");
                return;
            }

            int currentRoomId = session_->getCurrentRoomId();
            auto room = roomManager_->getRoom(currentRoomId);
            if (!room)
            {
                spdlog::warn("방 {}를 찾을 수 없음", currentRoomId);
                return;
            }

            // Get display name from session if available, fallback to username
            std::string displayName = username;
            if (session_ && session_->isActive()) {
                displayName = session_->getDisplayName();
            }
            std::string chatMessage = "CHAT:" + username + ":" + displayName + ":" + message;
            spdlog::info("📢 방 {} 채팅 브로드캐스트: [{}] {} ({})", currentRoomId, displayName, message, username);

            // GameRoom의 broadcastMessage 사용
            room->broadcastMessage(chatMessage);

            spdlog::debug("방 {} 채팅 브로드캐스트 완료", currentRoomId);
        }
        catch (const std::exception &e)
        {
            spdlog::error("방 채팅 브로드캐스트 중 오류: {}", e.what());
        }
    }

    void MessageHandler::sendRoomInfo(const std::shared_ptr<GameRoom> &room)
    {
        try
        {
            if (!room)
            {
                spdlog::warn("방이 null이므로 방 정보 전송 불가");
                return;
            }

            // ROOM_INFO:방ID:방이름:호스트:현재인원:최대인원:비공개:게임중:게임모드:플레이어데이터...
            std::ostringstream response;
            response << "ROOM_INFO:" << room->getRoomId() << ":" << room->getRoomName()
                     << ":" << room->getHostName() << ":" << room->getPlayerCount()
                     << ":" << room->getMaxPlayers() << ":" << (room->isPrivate() ? "1" : "0")
                     << ":" << (room->isPlaying() ? "1" : "0") << ":클래식";

            // 플레이어 데이터 추가 (userId,username,displayName,isHost,isReady,colorIndex)
            auto playerList = room->getPlayerList();
            for (const auto &player : playerList)
            {
                // Get display name from session if available, fallback to username
                std::string displayName = player.getUsername();
                if (auto session = player.getSession()) {
                    displayName = session->getDisplayName();
                }
                response << ":" << player.getUserId() << "," << player.getUsername()
                         << "," << displayName << "," << (player.isHost() ? "1" : "0")
                         << "," << (player.isReady() ? "1" : "0")
                         << "," << static_cast<int>(player.getColor());
            }

            sendResponse(response.str());
            spdlog::debug("방 정보 전송: 방 {} ({})", room->getRoomId(), room->getRoomName());
        }
        catch (const std::exception &e)
        {
            spdlog::error("방 정보 전송 중 오류: {}", e.what());
        }
    }

    void MessageHandler::broadcastRoomInfoToRoom(const std::shared_ptr<GameRoom> &room)
    {
        try
        {
            if (!room)
            {
                spdlog::warn("방이 null이므로 방 정보 브로드캐스트 불가");
                return;
            }

            // ROOM_INFO 메시지 생성 (sendRoomInfo와 동일한 형식)
            std::ostringstream response;
            response << "ROOM_INFO:" << room->getRoomId() << ":" << room->getRoomName()
                     << ":" << room->getHostName() << ":" << room->getPlayerCount()
                     << ":" << room->getMaxPlayers() << ":" << (room->isPrivate() ? "1" : "0")
                     << ":" << (room->isPlaying() ? "1" : "0") << ":클래식";

            // 플레이어 데이터 추가 (userId,username,displayName,isHost,isReady,colorIndex)
            auto playerList = room->getPlayerList();
            spdlog::debug("🔍 방 {} 플레이어 목록 생성 중: {}명", room->getRoomId(), playerList.size());
            for (const auto &player : playerList)
            {
                // Get display name from session if available, fallback to username
                std::string displayName = player.getUsername();
                if (auto session = player.getSession()) {
                    displayName = session->getDisplayName();
                }
                std::string playerData = player.getUserId() + "," + player.getUsername() + "," + displayName + "," + (player.isHost() ? "1" : "0") + "," + (player.isReady() ? "1" : "0") + "," + std::to_string(static_cast<int>(player.getColor()));

                spdlog::debug("  - 플레이어 데이터: {}", playerData);
                response << ":" << playerData;
            }

            std::string roomInfoMessage = response.str();

            spdlog::info("📤 방 {} ROOM_INFO 메시지 생성: {}", room->getRoomId(), roomInfoMessage);

            // 방의 모든 플레이어에게 브로드캐스트
            int sentCount = 0;
            for (const auto &player : playerList)
            {
                if (player.getSession() && player.getSession()->isActive())
                {
                    player.getSession()->sendMessage(roomInfoMessage);
                    sentCount++;
                }
            }

            spdlog::debug("방 {} 플레이어 {}명 (전체 {}명)에게 방 정보 브로드캐스트 완료",
                          room->getRoomId(), sentCount, playerList.size());
        }
        catch (const std::exception &e)
        {
            spdlog::error("방 정보 브로드캐스트 중 오류: {}", e.what());
        }
    }

    // ========================================
    // 사용자 정보 관련 핸들러
    // ========================================

    std::string MessageHandler::generateUserStatsResponse()
    {
        auto username = session_->getUsername();
        auto userAccountOpt = session_->getUserAccount();

        if (!userAccountOpt.has_value())
        {
            auto dbManager = gameServer_->getDatabaseManager();
            if (!dbManager)
                throw std::runtime_error("No DB manager");

            auto dbUserAccount = dbManager->getUserByUsername(username);
            if (!dbUserAccount.has_value())
                throw std::runtime_error("User not found");
            session_->setUserAccount(dbUserAccount.value());
            userAccountOpt = dbUserAccount;
        }

        const auto &userAccount = userAccountOpt.value();
        int requiredExp = 100;
        if (auto dbManager = gameServer_->getDatabaseManager())
        {
            requiredExp = dbManager->getRequiredExpForLevel(userAccount.level + 1);
        }

        std::ostringstream response;
        response << "MY_STATS_UPDATE:{"; // 자동 업데이트용 메시지 타입
        response << "\"username\":\"" << userAccount.username << "\",";
        response << "\"displayName\":\"" << userAccount.displayName << "\",";
        response << "\"level\":" << userAccount.level << ",";
        response << "\"totalGames\":" << userAccount.totalGames << ",";
        response << "\"wins\":" << userAccount.wins << ",";
        response << "\"losses\":" << userAccount.losses << ",";
        response << "\"draws\":" << userAccount.draws << ",";
        response << "\"currentExp\":" << userAccount.experiencePoints << ",";
        response << "\"requiredExp\":" << requiredExp << ",";
        response << "\"winRate\":" << std::fixed << std::setprecision(1) << userAccount.getWinRate() << ",";
        response << "\"averageScore\":" << std::fixed << std::setprecision(1) << userAccount.getAverageScore() << ",";
        response << "\"totalScore\":" << userAccount.totalScore << ",";
        response << "\"bestScore\":" << userAccount.bestScore << ",";
        response << "\"status\":\"" << session_->getUserStatusString() << "\"";
        response << "}";

        return response.str();
    }

    void MessageHandler::handleGetUserStats(const std::vector<std::string>& params)
    {
        try
        {
            if (params.size() < 1)
            {
                sendError("사용자 정보 요청에 사용자명이 필요합니다");
                return;
            }

            std::string targetUsername = params[0];
            spdlog::debug("🔍 사용자 정보 요청: '{}'", targetUsername);

            // RoomManager를 통해 해당 사용자의 세션을 찾기
            if (!gameServer_)
            {
                sendError("서버 오류: 방 관리자를 사용할 수 없습니다");
                return;
            }

            // 로비에서 해당 사용자의 세션 검색 (username 또는 display_name으로)
            auto lobbyUsers = gameServer_->getLobbyUsers();
            std::shared_ptr<Session> targetSession = nullptr;

            for (const auto& lobbySession : lobbyUsers)
            {
                if (lobbySession && lobbySession->isActive())
                {
                    // username 또는 display_name으로 매치 확인
                    if (lobbySession->getUsername() == targetUsername ||
                        lobbySession->getDisplayName() == targetUsername)
                    {
                        targetSession = lobbySession;
                        break;
                    }
                }
            }

            if (!targetSession)
            {
                sendError("요청한 사용자를 찾을 수 없습니다");
                return;
            }

            // 대상 사용자의 세션에서 사용자 정보 가져오기
            auto userAccountOpt = targetSession->getUserAccount();
            
            if (!userAccountOpt.has_value())
            {
                // 세션에 캐시된 정보가 없으면 DB에서 조회
                auto dbManager = gameServer_->getDatabaseManager();
                if (!dbManager)
                {
                    sendError("서버 오류: 데이터베이스를 사용할 수 없습니다");
                    return;
                }

                // username 또는 display_name으로 DB 검색
                auto dbUserAccount = dbManager->getUserByUsername(targetUsername);
                if (!dbUserAccount.has_value())
                {
                    // username으로 못 찾으면 display_name으로 검색
                    dbUserAccount = dbManager->getUserByDisplayName(targetUsername);
                }
                
                if (!dbUserAccount.has_value())
                {
                    sendError("사용자 정보를 찾을 수 없습니다");
                    return;
                }
                
                targetSession->setUserAccount(dbUserAccount.value());
                userAccountOpt = dbUserAccount;
            }

            const auto& userAccount = userAccountOpt.value();
            int requiredExp = 100;
            if (auto dbManager = gameServer_->getDatabaseManager())
            {
                requiredExp = dbManager->getRequiredExpForLevel(userAccount.level + 1);
            }

            // 응답 메시지 생성
            std::ostringstream response;
            response << "USER_STATS_RESPONSE:{";
            response << "\"username\":\"" << userAccount.username << "\",";
            response << "\"displayName\":\"" << userAccount.displayName << "\",";
            response << "\"level\":" << userAccount.level << ",";
            response << "\"totalGames\":" << userAccount.totalGames << ",";
            response << "\"wins\":" << userAccount.wins << ",";
            response << "\"losses\":" << userAccount.losses << ",";
            response << "\"draws\":" << userAccount.draws << ",";
            response << "\"currentExp\":" << userAccount.experiencePoints << ",";
            response << "\"requiredExp\":" << requiredExp << ",";
            response << "\"winRate\":" << std::fixed << std::setprecision(1) << userAccount.getWinRate() << ",";
            response << "\"averageScore\":" << std::fixed << std::setprecision(1) << userAccount.getAverageScore() << ",";
            response << "\"totalScore\":" << userAccount.totalScore << ",";
            response << "\"bestScore\":" << userAccount.bestScore << ",";
            response << "\"status\":\"" << targetSession->getUserStatusString() << "\"";
            response << "}";

            sendResponse(response.str());
            spdlog::debug("✅ 사용자 정보 응답 전송 완료: '{}' -> '{}'", 
                         targetUsername, session_->getUsername());
        }
        catch (const std::exception& e)
        {
            spdlog::error("사용자 정보 요청 처리 중 오류: {}", e.what());
            sendError("사용자 정보 조회 중 오류가 발생했습니다");
        }
    }

    // AFK 검증 처리
    void MessageHandler::handleAfkVerify()
    {
        try
        {
            spdlog::debug("🔍 AFK 검증 요청: 세션 {}", session_->getSessionId());

            // 세션 상태 검증
            if (!session_ || !session_->isActive())
            {
                sendError("세션이 유효하지 않습니다");
                return;
            }

            // 게임 중인지 확인
            if (!session_->isInGame())
            {
                sendError("게임 중이 아닙니다");
                return;
            }

            // 방 정보 확인
            int roomId = session_->getCurrentRoomId();
            if (roomId <= 0 || !roomManager_)
            {
                sendError("방 정보를 찾을 수 없습니다");
                return;
            }

            auto room = roomManager_->getRoom(roomId);
            if (!room)
            {
                sendError("방을 찾을 수 없습니다");
                return;
            }

            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            // 현재 플레이어 턴인지 확인
            if (!room->isPlayerTurn(userId))
            {
                sendError("현재 당신의 턴이 아닙니다");
                return;
            }

            // AFK 검증 가능한지 확인 (차단된 상태인지, 검증 횟수 제한 등)
            if (!room->canPlayerVerifyAfk(userId))
            {
                sendError("AFK 검증을 할 수 없습니다 (검증 횟수 초과 또는 차단되지 않음)");
                return;
            }

            // AFK 상태 검증 및 리셋
            bool success = room->verifyPlayerAfkStatus(userId);
            
            if (success)
            {
                sendResponse("AFK_VERIFY_SUCCESS");
                spdlog::info("✅ AFK 검증 성공: {} ({})", username, userId);
                
                // 방 내 다른 플레이어들에게 AFK 해제 알림
                room->broadcastMessage("AFK_STATUS_RESET:" + username, userId);
            }
            else
            {
                sendError("AFK 검증에 실패했습니다");
                spdlog::warn("❌ AFK 검증 실패: {} ({})", username, userId);
            }
        }
        catch (const std::exception& e)
        {
            spdlog::error("AFK 검증 처리 중 오류: {}", e.what());
            sendError("AFK 검증 중 오류가 발생했습니다");
        }
    }

    // AFK 모드 해제 처리 (모달에서 호출)
    void MessageHandler::handleAfkUnblock()
    {
        try
        {
            spdlog::debug("🔓 AFK 모드 해제 요청: 세션 {}", session_->getSessionId());

            // 세션 상태 검증
            if (!session_ || !session_->isActive())
            {
                sendError("세션이 유효하지 않습니다");
                return;
            }

            // 게임 중인지 확인
            if (!session_->isInGame())
            {
                sendError("게임 중이 아닙니다");
                return;
            }

            // 방 정보 확인
            int roomId = session_->getCurrentRoomId();
            if (roomId <= 0 || !roomManager_)
            {
                sendError("방 정보를 찾을 수 없습니다");
                return;
            }

            auto room = roomManager_->getRoom(roomId);
            if (!room)
            {
                sendError("방을 찾을 수 없습니다");
                return;
            }

            // 🔥 CRITICAL: 게임 상태 검증 추가 (crash 방지)
            if (!room->isPlaying())
            {
                sendResponse("AFK_UNBLOCK_ERROR:{\"reason\":\"game_not_active\",\"message\":\"게임이 이미 종료되었습니다\"}");
                spdlog::warn("⚠️ AFK 해제 시도하지만 게임이 종료됨: {} ({})", session_->getUsername(), session_->getUserId());
                return;
            }

            std::string userId = session_->getUserId();
            std::string username = session_->getUsername();

            // AFK 상태 해제 (모달 전용 메서드 사용)
            bool success = room->unblockPlayerAfkStatus(userId);
            
            if (success)
            {
                sendResponse("AFK_UNBLOCK_SUCCESS");
                spdlog::info("🔓 AFK 모드 해제 성공: {} ({})", username, userId);
            }
            else
            {
                sendError("AFK 모드 해제에 실패했습니다");
                spdlog::warn("❌ AFK 모드 해제 실패: {} ({})", username, userId);
            }
        }
        catch (const std::exception& e)
        {
            spdlog::error("AFK 모드 해제 처리 중 오류: {}", e.what());
            sendError("AFK 모드 해제 중 오류가 발생했습니다");
        }
    }

    // ========================================
    // 버전 관련 핸들러
    // ========================================

    void MessageHandler::handleVersionCheck(const std::vector<std::string>& params)
    {
        if (params.size() < 1) {
            sendError("사용법: version:check:클라이언트_버전");
            return;
        }
        
        const VersionManager::Version& version = versionManager_->getServerVersion();
        std::string clientVersion = params[0];
        spdlog::debug("🔍 버전 체크: 클라이언트={}, 서버={}", clientVersion, version.version);

        // 버전 호환성 체크
        bool compatible = version.isCompatibleWith(clientVersion);
        
        if (compatible) {
            sendTextMessage("version:ok");
            spdlog::info("✅ 버전 호환: {} <-> {}", clientVersion, ConfigManager::serverVersion);
        } else {
            std::string response = "version:mismatch:" + versionManager_->getDownloadURL();
            sendTextMessage(response);
            spdlog::warn("❌ 버전 불일치: 클라이언트={}, 서버={}, 리다이렉트={}",
                        clientVersion, ConfigManager::serverVersion, versionManager_->getDownloadURL());
        }
    }

    // ========================================
    // 사용자 설정 관련 핸들러 구현
    // ========================================

    void MessageHandler::handleUserSettings(const std::vector<std::string>& params)
    {
        try {
            if (!session_->isAuthenticated()) {
                sendError("인증되지 않은 사용자입니다");
                return;
            }

            // 첫 번째 파라미터가 "request"인 경우 설정 조회 처리
            if (!params.empty() && params[0] == "request") {
                handleGetUserSettings(params);
                return;
            }

            // 설정 업데이트의 경우 6개 파라미터 필요
            if (params.size() < 6) {
                sendError("사용자 설정 업데이트에 필요한 파라미터가 부족합니다");
                return;
            }

            // 파라미터 파싱: theme:language:bgm_mute:bgm_volume:sfx_mute:sfx_volume
            UserSettings settings;
            settings.theme = params[0];
            settings.language = params[1];
            settings.bgmMute = (params[2] == "true");
            settings.bgmVolume = std::stoi(params[3]);
            settings.effectMute = (params[4] == "true");
            settings.effectVolume = std::stoi(params[5]);

            // 유효성 검증
            if (!settings.isValid()) {
                sendError("잘못된 설정값입니다");
                return;
            }

            // 데이터베이스 업데이트
            if (databaseManager_ && databaseManager_->updateUserSettings(session_->getUserId(), settings)) {
                // 세션에 설정 캐싱
                session_->setUserSettings(settings);
                
                // 성공 응답
                std::string response = "UserSettingsResponse:success:" + settings.theme + ":" + 
                                     settings.language + ":" + 
                                     (settings.bgmMute ? "true" : "false") + ":" + 
                                     std::to_string(settings.bgmVolume) + ":" +
                                     (settings.effectMute ? "true" : "false") + ":" + 
                                     std::to_string(settings.effectVolume);
                
                sendTextMessage(response);
                spdlog::info("Updated settings for user {}", session_->getUsername());
            } else {
                sendError("설정 업데이트에 실패했습니다");
            }

        } catch (const std::exception& e) {
            spdlog::error("Error in handleUserSettings: {}", e.what());
            sendError("서버 오류가 발생했습니다");
        }
    }

    void MessageHandler::handleGetUserSettings(const std::vector<std::string>& params)
    {
        try {
            if (!session_->isAuthenticated()) {
                sendError("인증되지 않은 사용자입니다");
                return;
            }

            // 세션에서 캐싱된 설정 확인
            auto cachedSettings = session_->getUserSettings();
            if (cachedSettings.has_value()) {
                const auto& settings = cachedSettings.value();
                std::string response = "UserSettingsResponse:success:" + settings.theme + ":" + 
                                     settings.language + ":" + 
                                     (settings.bgmMute ? "true" : "false") + ":" + 
                                     std::to_string(settings.bgmVolume) + ":" +
                                     (settings.effectMute ? "true" : "false") + ":" + 
                                     std::to_string(settings.effectVolume);
                
                sendTextMessage(response);
                return;
            }

            // 데이터베이스에서 조회 (캐싱된 설정이 없는 경우)
            if (databaseManager_) {
                auto settings = databaseManager_->getUserSettings(session_->getUserId());
                if (settings.has_value()) {
                    // 세션에 캐싱
                    session_->setUserSettings(settings.value());
                    
                    const auto& s = settings.value();
                    std::string response = "UserSettingsResponse:success:" + s.theme + ":" + 
                                         s.language + ":" + 
                                         (s.bgmMute ? "true" : "false") + ":" + 
                                         std::to_string(s.bgmVolume) + ":" +
                                         (s.effectMute ? "true" : "false") + ":" + 
                                         std::to_string(s.effectVolume);
                    
                    sendTextMessage(response);
                    spdlog::debug("Retrieved settings for user {}", session_->getUsername());
                } else {
                    sendError("사용자 설정을 찾을 수 없습니다");
                }
            } else {
                sendError("데이터베이스 연결 오류입니다");
            }

        } catch (const std::exception& e) {
            spdlog::error("Error in handleGetUserSettings: {}", e.what());
            sendError("서버 오류가 발생했습니다");
        }
    }
    
} // namespace Blokus::Server