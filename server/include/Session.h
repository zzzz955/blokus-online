#pragma once

#include "ServerTypes.h"  // ConnectionState�� ���⿡ ���ǵ�
#include "DatabaseManager.h"  // UserAccount 구조체를 위해 추가
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
#include <optional>

// ���� ����
namespace Blokus::Server {
    class MessageHandler;
    class GameServer;
}

namespace Blokus::Server {

    // ========================================
    // Session Ŭ���� (���� ������Ʈ ���� ���)
    // ========================================
    class Session : public std::enable_shared_from_this<Session> {
    public:
        // �ݹ� Ÿ�� ���� (���� ������Ʈ ���)
        using SessionEventCallback = std::function<void(const std::string&)>;
        using MessageEventCallback = std::function<void(const std::string&, const std::string&)>;

        // ������/�Ҹ���
        explicit Session(boost::asio::ip::tcp::socket socket, GameServer* server = nullptr);
        ~Session();

        // ���� ����
        void start();
        void stop();
        bool isActive() const { return active_.load(); }

        // �޽��� �ڵ鷯 ����
        void setMessageHandler(std::unique_ptr<MessageHandler> handler);
        MessageHandler* getMessageHandler() const { return messageHandler_.get(); }

        // �⺻ ���� ������
        const std::string& getSessionId() const { return sessionId_; }
        const std::string& getUserId() const { return userId_; }
        const std::string& getUsername() const { return username_; }
        std::string getDisplayName() const { return userAccount_ ? userAccount_->displayName : username_; }
        ConnectionState getState() const { return state_; }
        int getCurrentRoomId() const { return currentRoomId_; }

        // 사용자 계정 정보 접근자
        const std::optional<UserAccount>& getUserAccount() const { return userAccount_; }
        bool hasUserAccount() const { return userAccount_.has_value(); }
        int getUserLevel() const { return userAccount_ ? userAccount_->level : 1; }
        int getUserExperience() const { return userAccount_ ? userAccount_->experiencePoints : 0; }
        uint32_t getUserIdAsInt() const { return userAccount_ ? userAccount_->userId : 0; }
        std::string getUserStatusString() const;

        // 인증 상태 확인
        bool isAuthenticated() const { return hasUserAccount() && !userId_.empty(); }

        // 사용자 설정 관리
        const std::optional<UserSettings>& getUserSettings() const { return userSettings_; }
        void setUserSettings(const UserSettings& settings) { userSettings_ = settings; }

        // ���� Ȯ�� �Լ��� (���� Session.h ���)
        bool isConnected() const { return state_ >= ConnectionState::Connected; }
        bool isInLobby() const { return state_ == ConnectionState::InLobby; }
        bool isInRoom() const { return state_ == ConnectionState::InRoom; }
        bool isInGame() const { return state_ == ConnectionState::InGame; }
        bool justLeftRoom() const { return justLeftRoom_; }

        // ���� Ȯ�� �Լ��� (����Ͻ� ������)
        bool canCreateRoom() const { return isInLobby(); }
        bool canJoinRoom() const { return isInLobby(); }
        bool canLeaveRoom() const { return isInRoom() || isInGame(); }
        bool canStartGame() const { return isInRoom(); }
        bool canMakeGameMove() const { return isInGame(); }

        // ���� ���� �Լ���
        void setStateToConnected();
        void setStateToLobby(bool fromRoom = false);
        void setStateToInRoom(int roomId = -1);
        void setStateToInGame();
        void clearJustLeftRoomFlag() { justLeftRoom_ = false; }

        // ���� ���� (����/���� ��ȯ, �ߺ� Ÿ�� ���� ����)
        bool setAuthenticated(const std::string& userId, const std::string& username, std::string* errorMessage = nullptr);
        void clearAuthentication();  // 인증 상태 완전 초기화
        void setUserAccount(const UserAccount& account);
        void updateUserAccount(const UserAccount& account);

        // �޽��� �ۼ���
        void sendMessage(const std::string& message);
        void sendBinary(const std::vector<uint8_t>& data);

        // �ݹ� ���� (���� ������Ʈ ���)
        void setDisconnectCallback(SessionEventCallback callback) { disconnectCallback_ = callback; }
        void setMessageCallback(MessageEventCallback callback) { messageCallback_ = callback; }

        // Ȱ�� ����
        void updateLastActivity() { lastActivity_ = std::chrono::steady_clock::now(); }
        bool isTimedOut(std::chrono::seconds timeout) const;
        std::chrono::steady_clock::time_point getLastActivity() const { return lastActivity_; }

        // ��Ʈ��ũ ����
        std::string getRemoteAddress() const;
        std::string getRemoteIP() const;  // IP 주소만 반환 (포트 제외)
        boost::asio::ip::tcp::socket& getSocket() { return socket_; }  // GameServer���� �ʿ�

        // ��� �� �����
        size_t getPendingMessageCount() const {
            std::lock_guard<std::mutex> lock(sendMutex_);
            return outgoingMessages_.size();
        }

    private:
        // ��Ʈ��ũ ����
        boost::asio::ip::tcp::socket socket_;
        static constexpr size_t MAX_MESSAGE_LENGTH = 8192;
        char readBuffer_[MAX_MESSAGE_LENGTH];
        std::string messageBuffer_;

        // ���� ����
        std::string sessionId_;
        std::string userId_;
        std::string username_;
        ConnectionState state_;
        int currentRoomId_;
        std::atomic<bool> active_;
        std::chrono::steady_clock::time_point lastActivity_;
        bool justLeftRoom_;
        
        // 사용자 계정 정보
        std::optional<UserAccount> userAccount_;
        
        // 사용자 설정 정보 (세션 캐시용)
        std::optional<UserSettings> userSettings_;

        // 중복 로그인 차단을 위한 추가 정보
        GameServer* gameServer_;        // GameServer 참조 (소유하지 않음)
        std::string remoteIP_;          // 클라이언트 IP 주소 (캐시용)
        bool isRegisteredInServer_;     // GameServer에 등록되었는지 여부

        // �޽��� ó��
        std::unique_ptr<MessageHandler> messageHandler_;

        // �񵿱� ���� ����
        mutable std::mutex sendMutex_;
        std::queue<std::string> outgoingMessages_;
        bool writing_;

        // �ݹ�
        SessionEventCallback disconnectCallback_;
        MessageEventCallback messageCallback_;

        // ���� ��Ʈ��ũ �Լ���
        void startRead();
        void handleRead(const boost::system::error_code& error, size_t bytesTransferred);
        void doWrite();
        void handleWrite(const boost::system::error_code& error, size_t bytesTransferred);

        // �޽��� ó��
        void processMessage(const std::string& message);
        void handleError(const boost::system::error_code& error);
        void cleanup();

        // �ݹ� ȣ��
        void notifyDisconnect();
        void notifyMessage(const std::string& message);

        // ��ƿ��Ƽ
        std::string generateSessionId();
        std::string extractIPFromSocket();  // 소켓에서 IP 주소 추출
    };

    // ========================================
    // ���� �Լ��� (���� ServerTypes.h�� �̹� ����)
    // ========================================
    // connectionStateToString�� ServerTypes.h�� ���ǵ�

} // namespace Blokus::Server