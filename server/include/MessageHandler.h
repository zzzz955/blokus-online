#pragma once

#include <string>
#include <memory>
#include <functional>
#include <unordered_map>
#include <unordered_set>
#include <vector>
#include <cstdint>

// 기존 정의 사용
#include "ServerTypes.h"

namespace Blokus::Server {

    // 전방 선언 (순환 참조 방지)
    class Session;
    class AuthenticationService;
    class RoomManager;
    class DatabaseManager;
    class GameServer;
    class VersionManager;

    // 🔥 채팅 브로드캐스트용 콜백만 유지
    using ChatCallback = std::function<void(const std::string& sessionId, const std::string& message)>;

    // 단순화된 메시지 핸들러 클래스 (직접 처리 방식)
    class MessageHandler {
    public:
        explicit MessageHandler(Session* session, RoomManager* roomManager = nullptr, AuthenticationService* authService = nullptr, DatabaseManager* databaseManager_ = nullptr, GameServer* gameServer = nullptr, VersionManager* versionManager = nullptr);
        ~MessageHandler();

        // 메시지 처리
        void handleMessage(const std::string& rawMessage);

        // 🔥 채팅 콜백만 유지 (브로드캐스트 필요)
        void setChatCallback(ChatCallback callback) { chatCallback_ = callback; }

        // 응답 전송 (현재: 텍스트 기반)
        void sendTextMessage(const std::string& message);
        void sendError(const std::string& errorMessage);

    private:
        // enum 기반 핸들러 테이블
        std::unordered_map<MessageType, std::function<void(const std::vector<std::string>&)>> handlers_;

        // 메시지 파싱
        std::pair<MessageType, std::vector<std::string>> parseMessage(const std::string& rawMessage);

        // 메시지 파싱 유틸리티
        std::vector<std::string> splitMessage(const std::string& message, char delimiter = ':');
        void sendResponse(const std::string& response);

        // 핸들러 함수들
        void handleAuth(const std::vector<std::string>& params);
        void handleRegister(const std::vector<std::string>& params);
        void handleLoginGuest(const std::vector<std::string>& params);
        void handleLogout(const std::vector<std::string>& params);
        void handleSessionValidate(const std::vector<std::string>& params);

        // 방 관련 핸들러들 (직접 처리)
        void handleCreateRoom(const std::vector<std::string>& params);
        void handleJoinRoom(const std::vector<std::string>& params);
        void handleLeaveRoom(const std::vector<std::string>& params);
        void handleRoomList(const std::vector<std::string>& params);
        void handlePlayerReady(const std::vector<std::string>& params);
        void handleStartGame(const std::vector<std::string>& params);
        void handleEndGame(const std::vector<std::string>& params);
        void handleTransferHost(const std::vector<std::string>& params);

        // 로비 관련 핸들러들
        void handleLobbyEnter(const std::vector<std::string>& params);
        void handleLobbyLeave(const std::vector<std::string>& params);
        void handleLobbyList(const std::vector<std::string>& params);
        
        // 사용자 정보 관련 핸들러들
        void handleGetUserStats(const std::vector<std::string>& params);
        void handleAfkVerify();  // AFK 검증 처리
        void handleAfkUnblock(); // AFK 모드 해제 처리
        
        // 사용자 설정 관련 핸들러들
        void handleUserSettings(const std::vector<std::string>& params);     // 설정 업데이트
        void handleGetUserSettings(const std::vector<std::string>& params);  // 설정 조회

        // 버전 관련 핸들러들
        void handleVersionCheck(const std::vector<std::string>& params);

        // 게임 관련 핸들러들
        void handleGameMove(const std::vector<std::string>& params);

        // 기본 핸들러들
        void handlePing(const std::vector<std::string>& params);
        void handleChat(const std::vector<std::string>& params);

        // 로비 브로드캐스팅 헬퍼 함수들
        void sendLobbyUserList();
        void sendRoomList();
        void broadcastLobbyUserJoined(const std::string& username);
        void broadcastLobbyUserLeft(const std::string& username);
        
        // 채팅 브로드캐스팅 헬퍼 함수들
        void broadcastLobbyChatMessage(const std::string& username, const std::string& message);
        void broadcastRoomChatMessage(const std::string& username, const std::string& message);
        
        // 방 정보 동기화 헬퍼 함수
        void sendRoomInfo(const std::shared_ptr<GameRoom>& room);
        void broadcastRoomInfoToRoom(const std::shared_ptr<GameRoom>& room);

        // 유저 스탯 정보 조회 헬퍼 함수
        std::string generateUserStatsResponse();

        // 인증 관련 헬퍼 함수들
        bool requiresAuthentication(MessageType messageType) const;
        void logSecurityViolation(MessageType messageType, const std::string& details = "");

    private:
        Session* session_;  // 소유하지 않음, 단순 참조
        RoomManager* roomManager_;  // RoomManager 참조
        AuthenticationService* authService_;  // AuthService 참조
        DatabaseManager* databaseManager_;
        GameServer* gameServer_;  // GameServer 참조
        VersionManager* versionManager_;

        // 🔥 채팅 콜백만 유지
        ChatCallback chatCallback_;
    };

} // namespace Blokus::Server