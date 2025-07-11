#pragma once

#include <memory>
#include <string>
#include <atomic>
#include <chrono>
#include <queue>
#include <boost/asio.hpp>
#include <spdlog/spdlog.h>

// 기존 정의 사용
#include "common/ServerTypes.h"

namespace Blokus::Server {

    // 전방 선언  
    class MessageHandler;

    // 세션 이벤트 콜백 타입들
    using SessionEventCallback = std::function<void(const std::string& sessionId)>;
    using MessageEventCallback = std::function<void(const std::string& sessionId, const std::string& message)>;

    // ServerTypes.h에서 ConnectionState 사용

    // 클라이언트 세션 클래스
    class Session : public std::enable_shared_from_this<Session> {
    public:
        explicit Session(boost::asio::ip::tcp::socket socket);
        ~Session();

        // 세션 제어
        void setMessageHandler(std::unique_ptr<MessageHandler> handler);
        void start();
        void stop();
        bool isActive() const { return active_.load(); }

        // 콜백 설정 (GameServer에서 설정)
        void setDisconnectCallback(SessionEventCallback callback) { disconnectCallback_ = callback; }
        void setMessageCallback(MessageEventCallback callback) { messageCallback_ = callback; }

        // 메시지 송수신
        void sendMessage(const std::string& message);
        void sendBinary(const std::vector<uint8_t>& data);

        // 세션 정보
        const std::string& getSessionId() const { return sessionId_; }
        const std::string& getUserId() const { return userId_; }
        const std::string& getUsername() const { return username_; }
        ConnectionState getState() const { return state_; }

        // 인증 관련
        void setAuthenticated(const std::string& userId, const std::string& username);
        bool isAuthenticated() const { return state_ >= ConnectionState::Authenticated; }

        void setState(ConnectionState state) { state_ = state; }
        void updateLastActivity() { lastActivity_ = std::chrono::steady_clock::now(); }

        // 하트비트 체크
        bool isTimedOut(std::chrono::seconds timeout) const;

        // 소켓 접근
        boost::asio::ip::tcp::socket& getSocket() { return socket_; }
        std::string getRemoteAddress() const;

        // MessageHandler 접근
        MessageHandler* getMessageHandler() const { return messageHandler_.get(); }

    private:
        // 비동기 읽기/쓰기
        void startRead();
        void handleRead(const boost::system::error_code& error, size_t bytesTransferred);
        void handleWrite(const boost::system::error_code& error, size_t bytesTransferred);
        void doWrite();

        // 메시지 처리
        void processMessage(const std::string& message);
        void processReceivedData();

        // 에러 처리  
        void handleError(const boost::system::error_code& error);
        void cleanup();

        // 콜백 호출
        void notifyDisconnect();
        void notifyMessage(const std::string& message);

        // 유틸리티
        std::string generateSessionId();

    private:
        // 네트워크
        boost::asio::ip::tcp::socket socket_;

        // 콜백 함수들 (GameServer 직접 참조 대신)
        SessionEventCallback disconnectCallback_;
        MessageEventCallback messageCallback_;

        // 세션 정보
        std::string sessionId_;
        std::string userId_;
        std::string username_;
        ConnectionState state_;
        std::atomic<bool> active_{ true };

        // 시간 관리
        std::chrono::steady_clock::time_point lastActivity_;

        // 버퍼 관리
        std::array<char, 8192> readBuffer_;
        std::string messageBuffer_;
        std::queue<std::string> outgoingMessages_;  // 벡터 대신 큐 사용
        std::mutex sendMutex_;
        bool writing_{ false };  // 현재 쓰기 중인지 플래그

        // 메시지 핸들러
        std::unique_ptr<MessageHandler> messageHandler_;
    };

} // namespace Blokus::Server