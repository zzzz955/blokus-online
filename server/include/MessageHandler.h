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

// Protobuf 전방 선언 (blokus 네임스페이스)
namespace google::protobuf {
    class Message;
}

namespace blokus {
    class MessageWrapper;     // message_wrapper.proto에서 생성
    enum MessageType : int;   // message_wrapper.proto에서 생성
    
    // Proto 메시지 타입들
    class AuthRequest;
    class AuthResponse;
    class RegisterRequest;
    class RegisterResponse;
    class LogoutRequest;
    class LogoutResponse;
    class HeartbeatRequest;
    class HeartbeatResponse;
    class CreateRoomRequest;
    class CreateRoomResponse;
    class JoinRoomRequest;
    class JoinRoomResponse;
    class LeaveRoomRequest;
    class LeaveRoomResponse;
    class SendChatRequest;
    class SendChatResponse;
    class PlaceBlockRequest;
    class PlaceBlockResponse;
    class StartGameRequest;
    class StartGameResponse;
    class GameStartedNotification;
    class GameEndedNotification;
    class ErrorResponse;
}

namespace Blokus::Server {

    // 전방 선언 (순환 참조 방지)
    class Session;
    class AuthenticationService;
    class RoomManager;
    class DatabaseManager;
    class GameServer;

    // 🔥 채팅 브로드캐스트용 콜백만 유지
    using ChatCallback = std::function<void(const std::string& sessionId, const std::string& message)>;

    // 🗑️ 제거된 콜백 타입들 (더 이상 사용 안함):
    // using AuthCallback = ...
    // using RegisterCallback = ...
    // using RoomCallback = ...

    // 단순화된 메시지 핸들러 클래스 (직접 처리 방식)
    class MessageHandler {
    public:
        explicit MessageHandler(Session* session, RoomManager* roomManager = nullptr, AuthenticationService* authService = nullptr, DatabaseManager* databaseManager_ = nullptr, GameServer* gameServer = nullptr);
        ~MessageHandler();

        // 메시지 처리 (텍스트 및 Protobuf 지원)
        void handleMessage(const std::string& rawMessage);
        void handleProtobufMessage(const blokus::MessageWrapper& wrapper);

        // 🔥 채팅 콜백만 유지 (브로드캐스트 필요)
        void setChatCallback(ChatCallback callback) { chatCallback_ = callback; }

        // 🗑️ 제거된 콜백 설정 함수들:
        // void setAuthCallback(AuthCallback callback);
        // void setRegisterCallback(RegisterCallback callback);
        // void setRoomCallback(RoomCallback callback);

        // 응답 전송 (현재: 텍스트 기반)
        void sendTextMessage(const std::string& message);
        void sendError(const std::string& errorMessage);

        // Protobuf 메시지 전송
        void sendProtobufMessage(blokus::MessageType type, const google::protobuf::Message& payload);
        void sendProtobufResponse(uint32_t sequenceId, blokus::MessageType type, const google::protobuf::Message& payload);

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
        

        // 게임 관련 핸들러들
        void handleGameMove(const std::vector<std::string>& params);
        // handleGameResultResponse 제거됨 - 즉시 초기화 방식으로 변경

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

    private:
        Session* session_;  // 소유하지 않음, 단순 참조
        RoomManager* roomManager_;  // RoomManager 참조
        AuthenticationService* authService_;  // AuthService 참조
        DatabaseManager* databaseManager_;
        GameServer* gameServer_;  // GameServer 참조

        // 시퀀스 관리
        uint32_t sequenceId_{ 0 };
        uint32_t lastReceivedSequence_{ 0 };
        
        // Protobuf 지원 플래그
        bool protobufEnabled_{ true };

        // 🔥 채팅 콜백만 유지
        ChatCallback chatCallback_;

        // Protobuf 메시지 핸들러들
        std::unordered_map<int, std::function<void(const blokus::MessageWrapper&)>> protobufHandlers_;
        
        // Protobuf 핸들러 함수들
        void handleProtobufAuth(const blokus::MessageWrapper& wrapper);
        void handleProtobufRegister(const blokus::MessageWrapper& wrapper);
        void handleProtobufLogout(const blokus::MessageWrapper& wrapper);
        void handleProtobufHeartbeat(const blokus::MessageWrapper& wrapper);
        void handleProtobufCreateRoom(const blokus::MessageWrapper& wrapper);
        void handleProtobufJoinRoom(const blokus::MessageWrapper& wrapper);
        void handleProtobufLeaveRoom(const blokus::MessageWrapper& wrapper);
        void handleProtobufSendChat(const blokus::MessageWrapper& wrapper);
        void handleProtobufPlaceBlock(const blokus::MessageWrapper& wrapper);
        void handleProtobufStartGame(const blokus::MessageWrapper& wrapper);
        
        // Protobuf 유틸리티 함수들
        void setupProtobufHandlers();
        template<typename T>
        bool unpackMessage(const blokus::MessageWrapper& wrapper, T& message);
        blokus::MessageWrapper createResponseWrapper(uint32_t sequenceId, blokus::MessageType type, const google::protobuf::Message& payload);
    };

} // namespace Blokus::Server