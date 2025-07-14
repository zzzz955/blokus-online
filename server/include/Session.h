#pragma once

#include "ServerTypes.h"  // ConnectionState는 여기에 정의됨
#include <boost/asio.hpp>
#include <spdlog/spdlog.h>
#include <memory>
#include <string>
#include <atomic>
#include <chrono>
#include <queue>
#include <mutex>
#include <functional>
#include <vector>

// 전방 선언
namespace Blokus::Server {
    class MessageHandler;
}

namespace Blokus::Server {

    // ========================================
    // Session 클래스 (기존 프로젝트 구조 기반)
    // ========================================
    class Session : public std::enable_shared_from_this<Session> {
    public:
        // 콜백 타입 정의 (기존 프로젝트 방식)
        using SessionEventCallback = std::function<void(const std::string&)>;
        using MessageEventCallback = std::function<void(const std::string&, const std::string&)>;

        // 생성자/소멸자
        explicit Session(boost::asio::ip::tcp::socket socket);
        ~Session();

        // 세션 제어
        void start();
        void stop();
        bool isActive() const { return active_.load(); }

        // 메시지 핸들러 설정
        void setMessageHandler(std::unique_ptr<MessageHandler> handler);
        MessageHandler* getMessageHandler() const { return messageHandler_.get(); }

        // 기본 정보 접근자
        const std::string& getSessionId() const { return sessionId_; }
        const std::string& getUserId() const { return userId_; }
        const std::string& getUsername() const { return username_; }
        ConnectionState getState() const { return state_; }
        int getCurrentRoomId() const { return currentRoomId_; }

        // 상태 확인 함수들 (기존 Session.h 방식)
        bool isConnected() const { return state_ >= ConnectionState::Connected; }
        bool isInLobby() const { return state_ == ConnectionState::InLobby; }
        bool isInRoom() const { return state_ == ConnectionState::InRoom; }
        bool isInGame() const { return state_ == ConnectionState::InGame; }

        // 권한 확인 함수들 (비즈니스 로직용)
        bool canCreateRoom() const { return isInLobby(); }
        bool canJoinRoom() const { return isInLobby(); }
        bool canLeaveRoom() const { return isInRoom() || isInGame(); }
        bool canStartGame() const { return isInRoom(); }
        bool canMakeGameMove() const { return isInGame(); }

        // 상태 변경 함수들
        void setStateToConnected();
        void setStateToLobby();
        void setStateToInRoom(int roomId = -1);
        void setStateToInGame();

        // 인증 관련
        void setAuthenticated(const std::string& userId, const std::string& username);

        // 메시지 송수신
        void sendMessage(const std::string& message);
        void sendBinary(const std::vector<uint8_t>& data);

        // 콜백 설정 (기존 프로젝트 방식)
        void setDisconnectCallback(SessionEventCallback callback) { disconnectCallback_ = callback; }
        void setMessageCallback(MessageEventCallback callback) { messageCallback_ = callback; }

        // 활동 추적
        void updateLastActivity() { lastActivity_ = std::chrono::steady_clock::now(); }
        bool isTimedOut(std::chrono::seconds timeout) const;
        std::chrono::steady_clock::time_point getLastActivity() const { return lastActivity_; }

        // 네트워크 정보
        std::string getRemoteAddress() const;
        boost::asio::ip::tcp::socket& getSocket() { return socket_; }  // GameServer에서 필요

        // 통계 및 디버깅
        size_t getPendingMessageCount() const {
            std::lock_guard<std::mutex> lock(sendMutex_);
            return outgoingMessages_.size();
        }

    private:
        // 네트워크 관련
        boost::asio::ip::tcp::socket socket_;
        static constexpr size_t MAX_MESSAGE_LENGTH = 8192;
        char readBuffer_[MAX_MESSAGE_LENGTH];
        std::string messageBuffer_;

        // 세션 정보
        std::string sessionId_;
        std::string userId_;
        std::string username_;
        ConnectionState state_;
        int currentRoomId_;
        std::atomic<bool> active_;
        std::chrono::steady_clock::time_point lastActivity_;

        // 메시지 처리
        std::unique_ptr<MessageHandler> messageHandler_;

        // 비동기 쓰기 관리
        mutable std::mutex sendMutex_;
        std::queue<std::string> outgoingMessages_;
        bool writing_;

        // 콜백
        SessionEventCallback disconnectCallback_;
        MessageEventCallback messageCallback_;

        // 내부 네트워크 함수들
        void startRead();
        void handleRead(const boost::system::error_code& error, size_t bytesTransferred);
        void doWrite();
        void handleWrite(const boost::system::error_code& error, size_t bytesTransferred);

        // 메시지 처리
        void processMessage(const std::string& message);
        void handleError(const boost::system::error_code& error);
        void cleanup();

        // 콜백 호출
        void notifyDisconnect();
        void notifyMessage(const std::string& message);

        // 유틸리티
        std::string generateSessionId();
    };

    // ========================================
    // 편의 함수들 (기존 ServerTypes.h에 이미 있음)
    // ========================================
    // connectionStateToString은 ServerTypes.h에 정의됨

} // namespace Blokus::Server