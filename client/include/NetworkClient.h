#pragma once

#include <QObject>
#include <QTcpSocket>
#include <QTimer>
#include <QString>
#include <QHostAddress>
#include <QJsonDocument>
#include <QJsonObject>
#include <functional>
#include <unordered_map>

// Protobuf forward declarations
namespace google::protobuf {
    class Message;
}

namespace blokus {
    class MessageWrapper;
    enum MessageType : int;
    
    // Proto message types
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
    class SendChatRequest;
    class SendChatResponse;
    class ErrorResponse;
}

namespace Blokus {

    class NetworkClient : public QObject
    {
        Q_OBJECT

    public:
        enum class ConnectionState {
            Disconnected,
            Connecting,
            Connected,
            Authenticated
        };

        explicit NetworkClient(QObject* parent = nullptr);
        ~NetworkClient();

        // 연결 관리
        void connectToServer(const QString& host = "localhost", quint16 port = 9999);
        void disconnect();
        bool isConnected() const;
        ConnectionState getState() const { return m_state; }

        // 메시지 전송 (텍스트 및 Protobuf)
        void sendMessage(const QString& message);
        void sendProtobufMessage(blokus::MessageType type, const google::protobuf::Message& payload);
        void sendProtobufRequest(blokus::MessageType type, const google::protobuf::Message& payload);
        
        // 인증 관련 (텍스트 및 Protobuf 지원)
        void login(const QString& username, const QString& password);
        void loginProtobuf(const QString& username, const QString& password);
        void registerUser(const QString& username, const QString& password);
        void registerUserProtobuf(const QString& username, const QString& password);
        void logout();
        void logoutProtobuf();
        void sendHeartbeat();

        // 로비 관련
        void enterLobby();
        void leaveLobby();
        void requestLobbyList();
        void requestRoomList();
        
        // 방 관련
        void createRoom(const QString& roomName, bool isPrivate = false, const QString& password = "");
        void createRoomProtobuf(const QString& roomName, bool isPrivate = false, const QString& password = "");
        void joinRoom(int roomId, const QString& password = "");
        void joinRoomProtobuf(int roomId, const QString& password = "");
        void leaveRoom();
        void leaveRoomProtobuf();
        void setPlayerReady(bool ready);
        void startGame();
        void startGameProtobuf();
        
        // 채팅 관련
        void sendChatMessage(const QString& message);
        void sendChatMessageProtobuf(const QString& message);
        
        // AFK 관련
        void sendAfkUnblock();

    signals:
        // 연결 상태 시그널
        void connected();
        void disconnected();
        void connectionError(const QString& error);
        void stateChanged(ConnectionState state);

        // 인증 시그널
        void loginResult(bool success, const QString& message, const QString& sessionToken = "");
        void registerResult(bool success, const QString& message);
        void logoutResult(bool success);

        // 메시지 시그널
        void messageReceived(const QString& message);
        void errorReceived(const QString& error);

        // 로비 시그널
        void lobbyEntered();
        void lobbyLeft();
        void lobbyUserListReceived(const QStringList& users);
        void lobbyUserJoined(const QString& username);
        void lobbyUserLeft(const QString& username);
        void roomListReceived(const QStringList& rooms);
        void userStatsReceived(const QString& statsJson);
        void myStatsUpdated(const QString& statsJson);
        
        // 방 시그널
        void roomCreated(int roomId, const QString& roomName);
        void roomJoined(int roomId, const QString& roomName);
        void roomLeft();
        void roomError(const QString& error);
        void roomInfoReceived(const QStringList& roomInfo);
        
        // 게임룸 상호작용 시그널
        void playerJoined(const QString& username);
        void playerLeft(const QString& username);
        void playerReady(const QString& username, bool ready);
        void hostChanged(const QString& newHost);
        void gameStarted();
        void gameEnded();
        void gameResult(const QString& resultJson);
        void gameReset(); // 게임 리셋 신호
        
        // 게임 상태 동기화 시그널
        void gameStateUpdated(const QString& gameStateJson);
        void blockPlaced(const QString& playerName, int blockType, int row, int col, int rotation, int flip, int playerColor, int scoreGained);
        void turnChanged(const QString& newPlayerName, int playerColor, int turnNumber, int turnTimeSeconds, int remainingTimeSeconds, bool previousTurnTimedOut);
        void turnTimeoutOccurred(const QString& timedOutPlayerName, int playerColor);
        
        // 채팅 시그널
        void chatMessageReceived(const QString& username, const QString& message);
        void chatMessageSent();
        
        // AFK 관련 시그널
        void afkModeActivated(const QString& jsonData);
        void afkUnblockSuccess();
        void afkStatusReset(const QString& username);
        void afkUnblockError(const QString& reason, const QString& message);

    private slots:
        void onConnected();
        void onDisconnected();
        void onReadyRead();
        void onSocketError(QAbstractSocket::SocketError error);
        void onConnectionTimeout();

    private:
        void setState(ConnectionState state);
        void processMessage(const QString& message);
        void processProtobufMessage(const blokus::MessageWrapper& wrapper);
        void processAuthResponse(const QString& response);
        void processLobbyResponse(const QString& response);
        void processGameStateMessage(const QString& message);
        void processAfkMessage(const QString& message);
        void processErrorMessage(const QString& error);
        void setupSocket();
        void cleanupSocket();
        
        // Protobuf message handlers
        void setupProtobufHandlers();
        void handleProtobufAuthResponse(const blokus::MessageWrapper& wrapper);
        void handleProtobufRegisterResponse(const blokus::MessageWrapper& wrapper);
        void handleProtobufLogoutResponse(const blokus::MessageWrapper& wrapper);
        void handleProtobufHeartbeatResponse(const blokus::MessageWrapper& wrapper);
        void handleProtobufCreateRoomResponse(const blokus::MessageWrapper& wrapper);
        void handleProtobufJoinRoomResponse(const blokus::MessageWrapper& wrapper);
        void handleProtobufLeaveRoomResponse(const blokus::MessageWrapper& wrapper);
        void handleProtobufSendChatResponse(const blokus::MessageWrapper& wrapper);
        void handleProtobufStartGameResponse(const blokus::MessageWrapper& wrapper);
        void handleProtobufErrorResponse(const blokus::MessageWrapper& wrapper);
        
        // Protobuf notification handlers
        void handleProtobufChatNotification(const blokus::MessageWrapper& wrapper);
        void handleProtobufPlayerJoinedNotification(const blokus::MessageWrapper& wrapper);
        void handleProtobufPlayerLeftNotification(const blokus::MessageWrapper& wrapper);
        void handleProtobufPlayerReadyNotification(const blokus::MessageWrapper& wrapper);
        void handleProtobufGameStartedNotification(const blokus::MessageWrapper& wrapper);
        void handleProtobufGameEndedNotification(const blokus::MessageWrapper& wrapper);
        
        // Protobuf utilities
        template<typename T>
        bool unpackMessage(const blokus::MessageWrapper& wrapper, T& message);
        blokus::MessageWrapper createRequestWrapper(blokus::MessageType type, const google::protobuf::Message& payload);
        
        // 재연결 관리
        void startReconnectTimer();
        void stopReconnectTimer();

    private:
        QTcpSocket* m_socket;
        QTimer* m_connectionTimer;
        QTimer* m_reconnectTimer;
        
        ConnectionState m_state;
        QString m_serverHost;
        quint16 m_serverPort;
        QString m_currentSessionToken;
        
        // 재연결 설정
        int m_reconnectInterval;
        int m_maxReconnectAttempts;
        int m_reconnectAttempts;
        
        // 연결 시간초과 설정
        int m_connectionTimeout;
        
        // Protobuf 지원
        std::unordered_map<int, std::function<void(const blokus::MessageWrapper&)>> m_protobufHandlers;
        uint32_t m_sequenceId;
        bool m_protobufEnabled;
    };

} // namespace Blokus