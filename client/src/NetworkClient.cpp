#include "NetworkClient.h"
#include "ClientConfigManager.h"
#include <QDebug>
#include <QHostAddress>
#include <QRegExp>
#include <ctime>

// Protobuf includes
#include "message_wrapper.pb.h"
#include "auth.pb.h"
#include "lobby.pb.h"
#include "game.pb.h"
#include "chat.pb.h"
#include "error.pb.h"

namespace Blokus {

    NetworkClient::NetworkClient(QObject* parent)
        : QObject(parent)
        , m_socket(nullptr)
        , m_connectionTimer(new QTimer(this))
        , m_reconnectTimer(new QTimer(this))
        , m_state(ConnectionState::Disconnected)
        , m_currentSessionToken("")
        , m_reconnectAttempts(0)
        , m_sequenceId(1)
        , m_protobufEnabled(true)
    {
        // 설정에서 네트워크 값 로드
        auto& config = ClientConfigManager::instance();
        const auto& serverConfig = config.getServerConfig();
        
        m_serverHost = serverConfig.host;
        m_serverPort = serverConfig.port;
        m_connectionTimeout = serverConfig.timeout_ms;
        m_maxReconnectAttempts = serverConfig.reconnect_attempts;
        m_reconnectInterval = serverConfig.reconnect_interval_ms;
        
        qDebug() << QString::fromUtf8("NetworkClient 설정 로드:");
        qDebug() << QString::fromUtf8("  기본 서버: %1:%2").arg(m_serverHost).arg(m_serverPort);
        qDebug() << QString::fromUtf8("  연결 타임아웃: %1ms").arg(m_connectionTimeout);
        qDebug() << QString::fromUtf8("  재연결 시도: %1회, 간격: %2ms").arg(m_maxReconnectAttempts).arg(m_reconnectInterval);
        
        setupSocket();
        setupProtobufHandlers();
        
        // 연결 타임아웃 타이머 설정
        m_connectionTimer->setSingleShot(true);
        m_connectionTimer->setInterval(m_connectionTimeout);
        connect(m_connectionTimer, &QTimer::timeout, this, &NetworkClient::onConnectionTimeout);
        
        // 재연결 타이머 설정
        m_reconnectTimer->setSingleShot(true);
        m_reconnectTimer->setInterval(m_reconnectInterval);
        connect(m_reconnectTimer, &QTimer::timeout, this, [this]() {
            if (m_reconnectAttempts < m_maxReconnectAttempts) {
                qDebug() << QString::fromUtf8("재연결 시도... (%1/%2)").arg(m_reconnectAttempts + 1).arg(m_maxReconnectAttempts);
                connectToServer(m_serverHost, m_serverPort);
            } else {
                qDebug() << QString::fromUtf8("최대 재연결 시도 횟수 초과");
                emit connectionError(QString::fromUtf8("서버에 연결할 수 없습니다."));
            }
        });
        
        qDebug() << QString::fromUtf8("NetworkClient 초기화 완료");
    }

    NetworkClient::~NetworkClient()
    {
        cleanupSocket();
    }

    void NetworkClient::setupSocket()
    {
        if (m_socket) {
            cleanupSocket();
        }
        
        m_socket = new QTcpSocket(this);
        
        // 시그널 연결
        connect(m_socket, &QTcpSocket::connected, this, &NetworkClient::onConnected);
        connect(m_socket, &QTcpSocket::disconnected, this, &NetworkClient::onDisconnected);
        connect(m_socket, &QTcpSocket::readyRead, this, &NetworkClient::onReadyRead);
        connect(m_socket, QOverload<QAbstractSocket::SocketError>::of(&QAbstractSocket::errorOccurred),
                this, &NetworkClient::onSocketError);
    }

    void NetworkClient::cleanupSocket()
    {
        if (m_socket) {
            m_socket->disconnect();
            if (m_socket->state() != QAbstractSocket::UnconnectedState) {
                m_socket->disconnectFromHost();
                if (!m_socket->waitForDisconnected(3000)) {
                    m_socket->abort();
                }
            }
            m_socket->deleteLater();
            m_socket = nullptr;
        }
    }

    void NetworkClient::connectToServer(const QString& host, quint16 port)
    {
        if (m_state == ConnectionState::Connected || m_state == ConnectionState::Connecting) {
            qDebug() << QString::fromUtf8("이미 연결되어 있거나 연결 중입니다.");
            return;
        }
        
        m_serverHost = host;
        m_serverPort = port;
        
        qDebug() << QString::fromUtf8("서버 연결 시도: %1:%2").arg(host).arg(port);
        qDebug() << QString::fromUtf8("소켓 상태: %1").arg(m_socket->state());
        
        setState(ConnectionState::Connecting);
        
        // 연결 시도 - QHostAddress 대신 문자열로 직접 연결
        m_socket->connectToHost(host, port);
        m_connectionTimer->start();
        
        qDebug() << QString::fromUtf8("연결 시도 완료, 소켓 상태: %1").arg(m_socket->state());
    }

    void NetworkClient::disconnect()
    {
        qDebug() << QString::fromUtf8("서버 연결 해제");
        
        m_connectionTimer->stop();
        m_reconnectTimer->stop();
        
        if (m_socket && m_socket->state() != QAbstractSocket::UnconnectedState) {
            m_socket->disconnectFromHost();
        }
        
        setState(ConnectionState::Disconnected);
        m_currentSessionToken.clear();
        m_reconnectAttempts = 0;
    }

    bool NetworkClient::isConnected() const
    {
        return m_state == ConnectionState::Connected || m_state == ConnectionState::Authenticated;
    }

    void NetworkClient::sendMessage(const QString& message)
    {
        if (!isConnected() || !m_socket) {
            qWarning() << QString::fromUtf8("서버에 연결되지 않음 - 메시지 전송 실패: %1").arg(message);
            return;
        }
        
        QByteArray data = message.toUtf8() + "\n";
        qint64 written = m_socket->write(data);
        
        if (written != data.size()) {
            qWarning() << QString::fromUtf8("메시지 전송 불완전: %1 bytes written of %2").arg(written).arg(data.size());
        } else {
            qDebug() << QString::fromUtf8("메시지 전송: %1").arg(message);
        }
    }

    void NetworkClient::login(const QString& username, const QString& password)
    {
        if (!isConnected()) {
            emit loginResult(false, QString::fromUtf8("서버에 연결되지 않았습니다."));
            return;
        }
        
        QString message = QString("auth:%1:%2").arg(username, password);
        sendMessage(message);
        qDebug() << QString::fromUtf8("로그인 요청 전송: %1").arg(username);
    }

    void NetworkClient::registerUser(const QString& username, const QString& password)
    {
        if (!isConnected()) {
            emit registerResult(false, QString::fromUtf8("서버에 연결되지 않았습니다."));
            return;
        }
        
        // 서버에서 register:사용자명:이메일:비밀번호 형식을 기대하지만 이메일을 빈 값으로 전송
        QString message = QString("register:%1::%2").arg(username, password);
        sendMessage(message);
        qDebug() << QString::fromUtf8("회원가입 요청 전송: %1").arg(username);
    }

    void NetworkClient::logout()
    {
        if (m_state != ConnectionState::Authenticated) {
            emit logoutResult(false);
            return;
        }
        
        sendMessage("logout");
        qDebug() << QString::fromUtf8("로그아웃 요청 전송");
    }

    void NetworkClient::enterLobby()
    {
        if (!isConnected()) {
            qWarning() << QString::fromUtf8("로비 입장 실패: 서버에 연결되지 않음");
            return;
        }
        
        sendMessage("lobby:enter");
        qDebug() << QString::fromUtf8("로비 입장 요청 전송");
    }

    void NetworkClient::leaveLobby()
    {
        if (!isConnected()) {
            qWarning() << QString::fromUtf8("로비 퇴장 실패: 서버에 연결되지 않음");
            return;
        }
        
        sendMessage("lobby:leave");
        qDebug() << QString::fromUtf8("로비 퇴장 요청 전송");
    }

    void NetworkClient::requestLobbyList()
    {
        if (!isConnected()) {
            qWarning() << QString::fromUtf8("로비 목록 요청 실패: 서버에 연결되지 않음");
            return;
        }
        
        sendMessage("lobby:list");
        qDebug() << QString::fromUtf8("로비 목록 요청 전송");
    }

    void NetworkClient::requestRoomList()
    {
        if (!isConnected()) {
            qWarning() << QString::fromUtf8("방 목록 요청 실패: 서버에 연결되지 않음");
            return;
        }
        
        sendMessage("room:list");
        qDebug() << QString::fromUtf8("방 목록 요청 전송");
    }
    
    
    void NetworkClient::createRoom(const QString& roomName, bool isPrivate, const QString& password)
    {
        if (!isConnected()) {
            qWarning() << QString::fromUtf8("방 생성 실패: 서버에 연결되지 않음");
            return;
        }
        
        QString message = QString("room:create:%1:%2")
            .arg(roomName)
            .arg(isPrivate ? "1" : "0");
            
        if (isPrivate && !password.isEmpty()) {
            message += ":" + password;
        }
        
        sendMessage(message);
        qDebug() << QString::fromUtf8("방 생성 요청 전송: %1").arg(roomName);
    }
    
    void NetworkClient::joinRoom(int roomId, const QString& password)
    {
        if (!isConnected()) {
            qWarning() << QString::fromUtf8("방 참여 실패: 서버에 연결되지 않음");
            return;
        }
        
        QString message = QString("room:join:%1").arg(roomId);
        if (!password.isEmpty()) {
            message += ":" + password;
        }
        
        sendMessage(message);
        qDebug() << QString::fromUtf8("방 참여 요청 전송: %1").arg(roomId);
    }
    
    void NetworkClient::leaveRoom()
    {
        if (!isConnected()) {
            qWarning() << QString::fromUtf8("방 나가기 실패: 서버에 연결되지 않음");
            return;
        }
        
        sendMessage("room:leave");
        qDebug() << QString::fromUtf8("방 나가기 요청 전송");
    }
    
    void NetworkClient::setPlayerReady(bool ready)
    {
        if (!isConnected()) {
            qWarning() << QString::fromUtf8("준비 상태 변경 실패: 서버에 연결되지 않음");
            return;
        }
        
        QString message = QString("room:ready:%1").arg(ready ? "1" : "0");
        sendMessage(message);
        qDebug() << QString::fromUtf8("준비 상태 변경 요청 전송: %1").arg(ready ? "준비완료" : "준비해제");
    }

    void NetworkClient::startGame()
    {
        if (!isConnected()) {
            qWarning() << QString::fromUtf8("게임 시작 실패: 서버에 연결되지 않음");
            return;
        }
        
        sendMessage("room:start");
        qDebug() << QString::fromUtf8("게임 시작 요청 전송");
    }


    void NetworkClient::sendChatMessage(const QString& message)
    {
        if (!isConnected()) {
            qWarning() << QString::fromUtf8("채팅 메시지 전송 실패: 서버에 연결되지 않음");
            return;
        }
        
        QString chatMessage = QString("chat:%1").arg(message);
        sendMessage(chatMessage);
        qDebug() << QString::fromUtf8("채팅 메시지 전송: %1").arg(message);
        
        emit chatMessageSent();
    }

    void NetworkClient::sendAfkUnblock()
    {
        if (!isConnected()) {
            qWarning() << QString::fromUtf8("AFK 해제 메시지 전송 실패: 서버에 연결되지 않음");
            return;
        }
        
        sendMessage("AFK_UNBLOCK");
        qDebug() << QString::fromUtf8("AFK 해제 메시지 전송");
    }

    void NetworkClient::setState(ConnectionState state)
    {
        if (m_state != state) {
            m_state = state;
            emit stateChanged(state);
            
            QString stateStr;
            switch (state) {
                case ConnectionState::Disconnected: stateStr = QString::fromUtf8("연결 해제됨"); break;
                case ConnectionState::Connecting: stateStr = QString::fromUtf8("연결 중"); break;
                case ConnectionState::Connected: stateStr = QString::fromUtf8("연결됨"); break;
                case ConnectionState::Authenticated: stateStr = QString::fromUtf8("인증됨"); break;
            }
            qDebug() << QString::fromUtf8("네트워크 상태 변경: %1").arg(stateStr);
        }
    }

    void NetworkClient::onConnected()
    {
        qDebug() << QString::fromUtf8("서버 연결 성공");
        m_connectionTimer->stop();
        m_reconnectAttempts = 0;
        setState(ConnectionState::Connected);
        emit connected();
    }

    void NetworkClient::onDisconnected()
    {
        qDebug() << QString::fromUtf8("서버 연결 해제됨");
        m_connectionTimer->stop();
        
        ConnectionState oldState = m_state;
        setState(ConnectionState::Disconnected);
        m_currentSessionToken.clear();
        
        emit disconnected();
        
        // 예상치 못한 연결 해제인 경우 재연결 시도
        if (oldState == ConnectionState::Connected || oldState == ConnectionState::Authenticated) {
            startReconnectTimer();
        }
    }

    void NetworkClient::onReadyRead()
    {
        if (!m_socket) return;
        
        while (m_socket->canReadLine()) {
            QByteArray data = m_socket->readLine();
            QString message = QString::fromUtf8(data).trimmed();
            
            if (!message.isEmpty()) {
                qDebug() << QString::fromUtf8("메시지 수신: %1").arg(message);
                processMessage(message);
            }
        }
    }

    void NetworkClient::onSocketError(QAbstractSocket::SocketError error)
    {
        QString errorString;
        QString detailError = m_socket->errorString();
        
        switch (error) {
            case QAbstractSocket::ConnectionRefusedError:
                errorString = QString::fromUtf8("연결이 거부되었습니다 (서버가 실행되지 않았거나 포트가 차단됨)");
                break;
            case QAbstractSocket::RemoteHostClosedError:
                errorString = QString::fromUtf8("서버가 연결을 종료했습니다.");
                break;
            case QAbstractSocket::HostNotFoundError:
                errorString = QString::fromUtf8("서버를 찾을 수 없습니다 (호스트명 해석 실패)");
                break;
            case QAbstractSocket::NetworkError:
                errorString = QString::fromUtf8("네트워크 오류가 발생했습니다.");
                break;
            case QAbstractSocket::SocketTimeoutError:
                errorString = QString::fromUtf8("연결 시간이 초과되었습니다.");
                break;
            default:
                errorString = QString::fromUtf8("알 수 없는 네트워크 오류");
                break;
        }
        
        qWarning() << QString::fromUtf8("소켓 오류 [%1]: %2").arg(error).arg(errorString);
        qWarning() << QString::fromUtf8("상세 오류: %1").arg(detailError);
        qWarning() << QString::fromUtf8("연결 대상: %1:%2").arg(m_serverHost).arg(m_serverPort);
        
        m_connectionTimer->stop();
        setState(ConnectionState::Disconnected);
        
        emit connectionError(errorString);
        
        // 심각하지 않은 오류의 경우 재연결 시도
        if (error != QAbstractSocket::ConnectionRefusedError && 
            error != QAbstractSocket::HostNotFoundError) {
            startReconnectTimer();
        }
    }

    void NetworkClient::onConnectionTimeout()
    {
        qWarning() << QString::fromUtf8("연결 시간 초과");
        
        if (m_socket) {
            m_socket->abort();
        }
        
        setState(ConnectionState::Disconnected);
        emit connectionError(QString::fromUtf8("연결 시간이 초과되었습니다."));
        
        startReconnectTimer();
    }

    void NetworkClient::processMessage(const QString& message)
    {
        emit messageReceived(message);
        
        // Protobuf 메시지 확인
        if (message.startsWith("PROTOBUF:")) {
            QString serializedData = message.mid(9); // "PROTOBUF:" 제거
            QByteArray binaryData = QByteArray::fromStdString(serializedData.toStdString());
            
            blokus::MessageWrapper wrapper;
            if (wrapper.ParseFromArray(binaryData.data(), binaryData.size())) {
                processProtobufMessage(wrapper);
            } else {
                qDebug() << QString::fromUtf8("❌ Protobuf 메시지 파싱 실패");
                emit errorReceived(QString::fromUtf8("Protobuf 메시지 형식 오류"));
            }
            return;
        }
        
        // 기존 텍스트 기반 메시지 처리
        if (message.startsWith("ERROR:")) {
            QString error = message.mid(6); // "ERROR:" 제거
            processErrorMessage(error);
        }
        else if (message.startsWith("AUTH_SUCCESS:") || 
                 message.startsWith("REGISTER_SUCCESS:") ||
                 message.startsWith("LOGOUT_SUCCESS")) {
            processAuthResponse(message);
        }
        else if (message.startsWith("LOBBY_") || message.startsWith("ROOM_") || message.startsWith("CHAT:") || 
                 message.startsWith("PLAYER_") || message.startsWith("HOST_") || message.startsWith("GAME_") ||
                 message.startsWith("SYSTEM:") || message.startsWith("USER_STATS_RESPONSE:") || 
                 message.startsWith("MY_STATS_UPDATE:")) {
            processLobbyResponse(message);
        }
        else if (message.startsWith("GAME_STATE_UPDATE:") || 
                 message.startsWith("BLOCK_PLACED:") || 
                 message.startsWith("TURN_CHANGED:") ||
                 message.startsWith("TURN_TIMEOUT:")) {
            processGameStateMessage(message);
        }
        else if (message.startsWith("AFK_MODE_ACTIVATED:") ||
                 message.startsWith("AFK_UNBLOCK_SUCCESS") ||
                 message.startsWith("AFK_STATUS_RESET:")) {
            processAfkMessage(message);
        }
        else if (message == "pong") {
            // Ping-pong은 특별히 처리하지 않음
        }
        else {
            // 기타 메시지들은 상위에서 처리하도록 전달
            qDebug() << QString::fromUtf8("처리되지 않은 메시지: %1").arg(message);
        }
    }

    void NetworkClient::processAuthResponse(const QString& response)
    {
        QStringList parts = response.split(':');
        
        if (parts[0] == "AUTH_SUCCESS" && parts.size() >= 3) {
            QString username = parts[1];
            QString sessionToken = parts[2];
            m_currentSessionToken = sessionToken;
            setState(ConnectionState::Authenticated);
            emit loginResult(true, QString::fromUtf8("로그인 성공"), sessionToken);
        }
        else if (parts[0] == "REGISTER_SUCCESS" && parts.size() >= 2) {
            QString username = parts[1];
            emit registerResult(true, QString::fromUtf8("회원가입이 완료되었습니다. 로그인해주세요."));
        }
        else if (parts[0] == "LOGOUT_SUCCESS") {
            setState(ConnectionState::Connected);
            m_currentSessionToken.clear();
            emit logoutResult(true);
        }
    }

    void NetworkClient::processLobbyResponse(const QString& response)
    {
        QStringList parts = response.split(':');
        
        if (parts[0] == "LOBBY_ENTER_SUCCESS") {
            emit lobbyEntered();
        }
        else if (parts[0] == "LOBBY_LEAVE_SUCCESS") {
            emit lobbyLeft();
        }
        else if (parts[0] == "LOBBY_USER_LIST" && parts.size() >= 2) {
            int userCount = parts[1].toInt();
            QStringList users;
            
            qDebug() << QString::fromUtf8("로비 사용자 목록 수신: 총 %1명, 파트 개수: %2").arg(userCount).arg(parts.size());
            
            // 서버 형식: LOBBY_USER_LIST:count:user1,level1,status1:user2,level2,status2...
            for (int i = 2; i < parts.size(); ++i) {
                if (!parts[i].isEmpty()) {
                    QStringList userInfo = parts[i].split(',');
                    if (userInfo.size() >= 3) {
                        QString username = userInfo[0];
                        int level = userInfo[1].toInt();
                        QString status = userInfo[2];
                        
                        users.append(QString::fromUtf8("Lv.%1 %2 (%3)").arg(level).arg(username).arg(status));
                        qDebug() << QString::fromUtf8("사용자 추가: %1 (레벨: %2, 상태: %3)").arg(username).arg(level).arg(status);
                    } else if (userInfo.size() >= 1) {
                        // 구버전 호환성을 위한 처리
                        users.append(userInfo[0]);
                        qDebug() << QString::fromUtf8("사용자 추가 (구버전): %1").arg(userInfo[0]);
                    }
                }
            }
            
            qDebug() << QString::fromUtf8("최종 사용자 목록: %1").arg(users.join(", "));
            emit lobbyUserListReceived(users);
        }
        else if (parts[0] == "LOBBY_USER_JOINED" && parts.size() >= 2) {
            QString username = parts[1];
            emit lobbyUserJoined(username);
        }
        else if (parts[0] == "LOBBY_USER_LEFT" && parts.size() >= 2) {
            QString username = parts[1];
            emit lobbyUserLeft(username);
        }
        else if (parts[0] == "ROOM_LIST" && parts.size() >= 2) {
            int roomCount = parts[1].toInt();
            QStringList rooms;
            for (int i = 2; i < parts.size(); ++i) {
                rooms.append(parts[i]);
            }
            emit roomListReceived(rooms);
        }
        else if (parts[0] == "ROOM_CREATED" && parts.size() >= 3) {
            int roomId = parts[1].toInt();
            QString roomName = parts[2];
            emit roomCreated(roomId, roomName);
        }
        else if (parts[0] == "ROOM_JOIN_SUCCESS" && parts.size() >= 3) {
            int roomId = parts[1].toInt();
            QString roomName = parts[2];
            emit roomJoined(roomId, roomName);
        }
        else if (parts[0] == "ROOM_LEFT") {
            emit roomLeft();
        }
        else if (parts[0] == "CHAT" && parts.size() >= 3) {
            QString username = parts[1];
            QString message = parts.mid(2).join(":"); // 메시지에 콜론이 포함될 수 있음
            emit chatMessageReceived(username, message);
        }
        else if (parts[0] == "ROOM_INFO" && parts.size() >= 8) {
            // ROOM_INFO:방ID:방이름:호스트:현재인원:최대인원:비공개:게임중:게임모드:플레이어데이터...
            emit roomInfoReceived(parts);
        }
        else if (parts[0] == "PLAYER_JOINED" && parts.size() >= 2) {
            QString username = parts[1];
            emit playerJoined(username);
        }
        else if (parts[0] == "PLAYER_LEFT" && parts.size() >= 2) {
            QString username = parts[1];
            emit playerLeft(username);
        }
        else if (parts[0] == "PLAYER_READY" && parts.size() >= 3) {
            QString username = parts[1];
            bool ready = (parts[2] == "1");
            qDebug() << QString::fromUtf8("NetworkClient: PLAYER_READY 수신 - %1: %2").arg(username).arg(ready ? "준비완료" : "대기중");
            emit playerReady(username, ready);
        }
        else if (parts[0] == "HOST_CHANGED" && parts.size() >= 2) {
            QString newHost = parts[1];
            emit hostChanged(newHost);
        }
        else if (parts[0] == "GAME_STARTED") {
            emit gameStarted();
        }
        else if (parts[0] == "GAME_ENDED") {
            emit gameEnded();
        }
        else if (parts[0] == "GAME_RESULT" && parts.size() >= 2) {
            QString resultJson = parts.mid(1).join(":");
            emit gameResult(resultJson);
        }
        else if (parts[0] == "GAME_RESET") {
            // 게임 리셋 신호 발생
            emit gameReset();
        }
        else if (parts[0] == "LEAVE_ROOM_CONFIRMED") {
            // 방 나가기가 확인되면 로비로 이동하는 신호 발생
            emit roomLeft();
        }
        else if (parts[0] == "SYSTEM" && parts.size() >= 2) {
            QString systemMessage = parts.mid(1).join(":");
            // 시스템 메시지를 채팅으로 처리
            emit chatMessageReceived(QString::fromUtf8("시스템"), systemMessage);
        }
        else if (parts[0] == "USER_STATS_RESPONSE" && parts.size() >= 2) {
            QString statsJson = parts.mid(1).join(":");
            emit userStatsReceived(statsJson);
        }
        else if (parts[0] == "MY_STATS_UPDATE" && parts.size() >= 2) {
            QString statsJson = parts.mid(1).join(":");
            emit myStatsUpdated(statsJson);
        }
    }

    void NetworkClient::processErrorMessage(const QString& error)
    {
        emit errorReceived(error);
        
        // 인증 관련 에러는 각각의 시그널로도 전달
        if (error.contains(QString::fromUtf8("사용자명")) || error.contains(QString::fromUtf8("비밀번호")) || 
            error.contains(QString::fromUtf8("로그인"))) {
            emit loginResult(false, error);
        }
        else if (error.contains(QString::fromUtf8("회원가입")) || error.contains(QString::fromUtf8("이미 사용 중")) ||
                 error.contains(QString::fromUtf8("사용자명 형식")) || error.contains(QString::fromUtf8("비밀번호는"))) {
            emit registerResult(false, error);
        }
        else if (error.contains(QString::fromUtf8("방")) || error.contains(QString::fromUtf8("room"))) {
            emit roomError(error);
        }
    }

    void NetworkClient::startReconnectTimer()
    {
        if (m_reconnectAttempts < m_maxReconnectAttempts) {
            m_reconnectAttempts++;
            m_reconnectTimer->start();
            qDebug() << QString::fromUtf8("재연결 타이머 시작 (%1초 후 시도)").arg(m_reconnectInterval / 1000);
        }
    }

    void NetworkClient::stopReconnectTimer()
    {
        m_reconnectTimer->stop();
        m_reconnectAttempts = 0;
    }

    void NetworkClient::processGameStateMessage(const QString& message)
    {
        if (message.startsWith("GAME_STATE_UPDATE:")) {
            QString jsonData = message.mid(18); // "GAME_STATE_UPDATE:" 제거
            emit gameStateUpdated(jsonData);
            qDebug() << QString::fromUtf8("게임 상태 업데이트 수신: %1").arg(jsonData);
        }
        else if (message.startsWith("BLOCK_PLACED:")) {
            QString jsonData = message.mid(13); // "BLOCK_PLACED:" 제거
            
            // JSON 파싱 (간단한 방식)
            QString playerName, blockType, row, col, rotation, flip, playerColor, scoreGained;
            
            // 간단한 JSON 파싱 (실제로는 QJsonDocument를 사용해야 함)
            QRegExp playerRegex("\"player\":\"([^\"]+)\"");
            QRegExp blockTypeRegex("\"blockType\":(\\d+)");
            QRegExp positionRegex("\"position\":\\{\"row\":(\\d+),\"col\":(\\d+)\\}");
            QRegExp rotationRegex("\"rotation\":(\\d+)");
            QRegExp flipRegex("\"flip\":(\\d+)");
            QRegExp playerColorRegex("\"playerColor\":(\\d+)");
            QRegExp scoreRegex("\"scoreGained\":(\\d+)");
            
            if (playerRegex.indexIn(jsonData) != -1) playerName = playerRegex.cap(1);
            if (blockTypeRegex.indexIn(jsonData) != -1) blockType = blockTypeRegex.cap(1);
            if (positionRegex.indexIn(jsonData) != -1) {
                row = positionRegex.cap(1);
                col = positionRegex.cap(2);
            }
            if (rotationRegex.indexIn(jsonData) != -1) rotation = rotationRegex.cap(1);
            if (flipRegex.indexIn(jsonData) != -1) flip = flipRegex.cap(1);
            if (playerColorRegex.indexIn(jsonData) != -1) playerColor = playerColorRegex.cap(1);
            if (scoreRegex.indexIn(jsonData) != -1) scoreGained = scoreRegex.cap(1);
            
            emit blockPlaced(playerName, blockType.toInt(), row.toInt(), col.toInt(), 
                           rotation.toInt(), flip.toInt(), playerColor.toInt(), scoreGained.toInt());
            
            qDebug() << QString::fromUtf8("블록 배치 알림: %1이 블록을 배치함 (점수: +%2)")
                        .arg(playerName).arg(scoreGained);
        }
        else if (message.startsWith("TURN_CHANGED:")) {
            QString jsonData = message.mid(13); // "TURN_CHANGED:" 제거
            
            qDebug() << QString::fromUtf8("⏰ [TIMER_DEBUG] TURN_CHANGED 메시지 수신: %1").arg(message);
            
            // JSON 파싱 (타이머 정보 포함)
            QRegExp playerRegex("\"newPlayer\":\"([^\"]+)\"");
            QRegExp colorRegex("\"playerColor\":(\\d+)");
            QRegExp turnRegex("\"turnNumber\":(\\d+)");
            QRegExp turnTimeRegex("\"turnTimeSeconds\":(\\d+)");
            QRegExp remainingTimeRegex("\"remainingTimeSeconds\":(\\d+)");
            QRegExp timeoutRegex("\"previousTurnTimedOut\":(true|false)");
            
            QString newPlayerName;
            int playerColor = 0, turnNumber = 0;
            int turnTimeSeconds = 30, remainingTimeSeconds = 30; // 기본값 30초
            bool previousTurnTimedOut = false;
            
            if (playerRegex.indexIn(jsonData) != -1) newPlayerName = playerRegex.cap(1);
            if (colorRegex.indexIn(jsonData) != -1) playerColor = colorRegex.cap(1).toInt();
            if (turnRegex.indexIn(jsonData) != -1) turnNumber = turnRegex.cap(1).toInt();
            if (turnTimeRegex.indexIn(jsonData) != -1) turnTimeSeconds = turnTimeRegex.cap(1).toInt();
            if (remainingTimeRegex.indexIn(jsonData) != -1) remainingTimeSeconds = remainingTimeRegex.cap(1).toInt();
            if (timeoutRegex.indexIn(jsonData) != -1) previousTurnTimedOut = (timeoutRegex.cap(1) == "true");
            
            qDebug() << QString::fromUtf8("⏰ [TIMER_DEBUG] 파싱 결과: 플레이어=%1, 턴시간=%2초, 남은시간=%3초")
                        .arg(newPlayerName).arg(turnTimeSeconds).arg(remainingTimeSeconds);
            
            emit turnChanged(newPlayerName, playerColor, turnNumber, turnTimeSeconds, remainingTimeSeconds, previousTurnTimedOut);
            
            qDebug() << QString::fromUtf8("턴 변경 알림: %1님의 턴 (턴 %2)")
                        .arg(newPlayerName).arg(turnNumber);
        }
        else if (message.startsWith("TURN_TIMEOUT:")) {
            QString jsonData = message.mid(13); // "TURN_TIMEOUT:" 제거
            
            // JSON 파싱
            QRegExp playerRegex("\"timedOutPlayer\":\"([^\"]+)\"");
            QRegExp colorRegex("\"playerColor\":(\\d+)");
            
            QString timedOutPlayerName;
            int playerColor = 0;
            
            if (playerRegex.indexIn(jsonData) != -1) timedOutPlayerName = playerRegex.cap(1);
            if (colorRegex.indexIn(jsonData) != -1) playerColor = colorRegex.cap(1).toInt();
            
            emit turnTimeoutOccurred(timedOutPlayerName, playerColor);
            
            qDebug() << QString::fromUtf8("턴 타임아웃 알림: %1님 시간 초과")
                        .arg(timedOutPlayerName);
        }
    }

    void NetworkClient::processAfkMessage(const QString& message)
    {
        if (message.startsWith("AFK_MODE_ACTIVATED:")) {
            QString jsonData = message.mid(19); // "AFK_MODE_ACTIVATED:" 제거
            emit afkModeActivated(jsonData);
            qDebug() << QString::fromUtf8("AFK 모드 활성화 알림 수신: %1").arg(jsonData);
        }
        else if (message == "AFK_UNBLOCK_SUCCESS") {
            emit afkUnblockSuccess();
            qDebug() << QString::fromUtf8("AFK 모드 해제 성공");
        }
        else if (message.startsWith("AFK_STATUS_RESET:")) {
            QString username = message.mid(17); // "AFK_STATUS_RESET:" 제거
            emit afkStatusReset(username);
            qDebug() << QString::fromUtf8("AFK 상태 리셋 알림: %1").arg(username);
        }
        else if (message.startsWith("AFK_UNBLOCK_ERROR:")) {
            QString jsonData = message.mid(18); // "AFK_UNBLOCK_ERROR:" 제거
            
            // JSON 파싱
            QJsonDocument doc = QJsonDocument::fromJson(jsonData.toUtf8());
            if (doc.isObject()) {
                QJsonObject obj = doc.object();
                QString reason = obj["reason"].toString();
                QString errorMessage = obj["message"].toString();
                
                emit afkUnblockError(reason, errorMessage);
                qDebug() << QString::fromUtf8("AFK 해제 에러: %1 - %2").arg(reason, errorMessage);
            }
        }
    }

    // ========================================
    // Protobuf 지원 구현
    // ========================================
    
    void NetworkClient::setupProtobufHandlers()
    {
        using namespace blokus;
        
        // 인증 관련 Protobuf 핸들러
        m_protobufHandlers[MESSAGE_TYPE_AUTH_RESPONSE] = [this](const auto& wrapper) { handleProtobufAuthResponse(wrapper); };
        m_protobufHandlers[MESSAGE_TYPE_REGISTER_RESPONSE] = [this](const auto& wrapper) { handleProtobufRegisterResponse(wrapper); };
        m_protobufHandlers[MESSAGE_TYPE_LOGOUT_RESPONSE] = [this](const auto& wrapper) { handleProtobufLogoutResponse(wrapper); };
        m_protobufHandlers[MESSAGE_TYPE_HEARTBEAT] = [this](const auto& wrapper) { handleProtobufHeartbeatResponse(wrapper); };
        
        // 로비/방 관련 Protobuf 핸들러
        m_protobufHandlers[MESSAGE_TYPE_CREATE_ROOM_RESPONSE] = [this](const auto& wrapper) { handleProtobufCreateRoomResponse(wrapper); };
        m_protobufHandlers[MESSAGE_TYPE_JOIN_ROOM_RESPONSE] = [this](const auto& wrapper) { handleProtobufJoinRoomResponse(wrapper); };
        m_protobufHandlers[MESSAGE_TYPE_LEAVE_ROOM_RESPONSE] = [this](const auto& wrapper) { handleProtobufLeaveRoomResponse(wrapper); };
        m_protobufHandlers[MESSAGE_TYPE_START_GAME_RESPONSE] = [this](const auto& wrapper) { handleProtobufStartGameResponse(wrapper); };
        
        // 채팅 관련 Protobuf 핸들러
        m_protobufHandlers[MESSAGE_TYPE_SEND_CHAT_RESPONSE] = [this](const auto& wrapper) { handleProtobufSendChatResponse(wrapper); };
        m_protobufHandlers[MESSAGE_TYPE_CHAT_NOTIFICATION] = [this](const auto& wrapper) { handleProtobufChatNotification(wrapper); };
        
        // 게임 알림 Protobuf 핸들러
        m_protobufHandlers[MESSAGE_TYPE_PLAYER_JOINED_NOTIFICATION] = [this](const auto& wrapper) { handleProtobufPlayerJoinedNotification(wrapper); };
        m_protobufHandlers[MESSAGE_TYPE_PLAYER_LEFT_NOTIFICATION] = [this](const auto& wrapper) { handleProtobufPlayerLeftNotification(wrapper); };
        m_protobufHandlers[MESSAGE_TYPE_PLAYER_READY_NOTIFICATION] = [this](const auto& wrapper) { handleProtobufPlayerReadyNotification(wrapper); };
        m_protobufHandlers[MESSAGE_TYPE_GAME_STARTED_NOTIFICATION] = [this](const auto& wrapper) { handleProtobufGameStartedNotification(wrapper); };
        m_protobufHandlers[MESSAGE_TYPE_GAME_ENDED_NOTIFICATION] = [this](const auto& wrapper) { handleProtobufGameEndedNotification(wrapper); };
        
        // 에러 핸들러
        m_protobufHandlers[MESSAGE_TYPE_ERROR_RESPONSE] = [this](const auto& wrapper) { handleProtobufErrorResponse(wrapper); };
        
        qDebug() << QString::fromUtf8("✅ Protobuf 핸들러 %1개 등록 완료").arg(m_protobufHandlers.size());
    }
    
    void NetworkClient::processProtobufMessage(const blokus::MessageWrapper& wrapper)
    {
        try
        {
            qDebug() << QString::fromUtf8("📨 Protobuf 메시지 수신: type=%1, seq=%2")
                        .arg(static_cast<int>(wrapper.type()))
                        .arg(wrapper.sequence_id());
            
            // 핸들러 실행
            auto it = m_protobufHandlers.find(static_cast<int>(wrapper.type()));
            if (it != m_protobufHandlers.end())
            {
                it->second(wrapper);
            }
            else
            {
                qDebug() << QString::fromUtf8("⚠️ 알 수 없는 Protobuf 메시지 타입: %1")
                            .arg(static_cast<int>(wrapper.type()));
            }
        }
        catch (const std::exception& e)
        {
            qDebug() << QString::fromUtf8("❌ Protobuf 메시지 처리 중 예외: %1").arg(e.what());
            emit errorReceived(QString::fromUtf8("Protobuf 메시지 처리 오류"));
        }
    }
    
    void NetworkClient::sendProtobufMessage(blokus::MessageType type, const google::protobuf::Message& payload)
    {
        if (!m_socket || !isConnected()) {
            qDebug() << QString::fromUtf8("❌ 연결되지 않아 Protobuf 메시지를 보낼 수 없음");
            return;
        }
        
        auto wrapper = createRequestWrapper(type, payload);
        
        // MessageWrapper를 직렬화하여 전송
        std::string serializedData;
        if (wrapper.SerializeToString(&serializedData))
        {
            // 특수 헤더를 추가하여 Protobuf 메시지임을 나타냄
            QString protobufMessage = "PROTOBUF:" + QString::fromStdString(serializedData);
            sendMessage(protobufMessage);
            
            qDebug() << QString::fromUtf8("📤 Protobuf 메시지 전송: type=%1, size=%2 bytes")
                        .arg(static_cast<int>(type))
                        .arg(serializedData.size());
        }
        else
        {
            qDebug() << QString::fromUtf8("❌ Protobuf 메시지 직렬화 실패");
        }
    }
    
    void NetworkClient::sendProtobufRequest(blokus::MessageType type, const google::protobuf::Message& payload)
    {
        sendProtobufMessage(type, payload);
    }
    
    // ========================================
    // Protobuf 인증 메소드들
    // ========================================
    
    void NetworkClient::loginProtobuf(const QString& username, const QString& password)
    {
        qDebug() << QString::fromUtf8("🔐 Protobuf 로그인 시도: %1").arg(username);
        
        blokus::AuthRequest request;
        request.set_method(blokus::AUTH_METHOD_USERNAME_PASSWORD);
        request.set_username(username.toStdString());
        request.set_password(password.toStdString());
        request.set_client_version("client-1.0.0");
        request.set_platform("Windows");
        
        sendProtobufRequest(blokus::MESSAGE_TYPE_AUTH_REQUEST, request);
    }
    
    void NetworkClient::registerUserProtobuf(const QString& username, const QString& password)
    {
        qDebug() << QString::fromUtf8("📝 Protobuf 회원가입 시도: %1").arg(username);
        
        blokus::RegisterRequest request;
        request.set_username(username.toStdString());
        request.set_password(password.toStdString());
        request.set_client_version("client-1.0.0");
        request.set_platform("Windows");
        request.set_terms_accepted(true);
        request.set_privacy_accepted(true);
        
        sendProtobufRequest(blokus::MESSAGE_TYPE_REGISTER_REQUEST, request);
    }
    
    void NetworkClient::logoutProtobuf()
    {
        qDebug() << QString::fromUtf8("🚪 Protobuf 로그아웃 요청");
        
        blokus::LogoutRequest request;
        request.set_session_token(m_currentSessionToken.toStdString());
        request.set_reason("user_logout");
        
        sendProtobufRequest(blokus::MESSAGE_TYPE_LOGOUT_REQUEST, request);
    }
    
    void NetworkClient::sendHeartbeat()
    {
        if (!isConnected()) return;
        
        blokus::HeartbeatRequest request;
        request.set_sequence_number(m_sequenceId++);
        request.set_cpu_usage(0.0f);
        request.set_memory_usage_mb(100);
        request.set_fps(60);
        request.set_is_window_focused(true);
        
        sendProtobufMessage(blokus::MESSAGE_TYPE_HEARTBEAT, request);
    }
    
    // ========================================
    // Protobuf 방 관리 메소드들
    // ========================================
    
    void NetworkClient::createRoomProtobuf(const QString& roomName, bool isPrivate, const QString& password)
    {
        qDebug() << QString::fromUtf8("🏠 Protobuf 방 생성 시도: %1").arg(roomName);
        
        blokus::CreateRoomRequest request;
        request.set_room_name(roomName.toStdString());
        request.set_is_private(isPrivate);
        if (isPrivate && !password.isEmpty()) {
            request.set_password(password.toStdString());
        }
        request.set_max_players(4);
        request.set_game_mode("classic");
        
        sendProtobufRequest(blokus::MESSAGE_TYPE_CREATE_ROOM_REQUEST, request);
    }
    
    void NetworkClient::joinRoomProtobuf(int roomId, const QString& password)
    {
        qDebug() << QString::fromUtf8("🚪 Protobuf 방 참여 시도: %1").arg(roomId);
        
        blokus::JoinRoomRequest request;
        request.set_room_id(roomId);
        if (!password.isEmpty()) {
            request.set_password(password.toStdString());
        }
        
        sendProtobufRequest(blokus::MESSAGE_TYPE_JOIN_ROOM_REQUEST, request);
    }
    
    void NetworkClient::leaveRoomProtobuf()
    {
        qDebug() << QString::fromUtf8("🚪 Protobuf 방 나가기 요청");
        
        blokus::LeaveRoomRequest request;
        request.set_reason("user_leave");
        
        sendProtobufRequest(blokus::MESSAGE_TYPE_LEAVE_ROOM_REQUEST, request);
    }
    
    void NetworkClient::startGameProtobuf()
    {
        qDebug() << QString::fromUtf8("🎮 Protobuf 게임 시작 요청");
        
        blokus::StartGameRequest request;
        request.set_force_start(false);
        
        sendProtobufRequest(blokus::MESSAGE_TYPE_START_GAME_REQUEST, request);
    }
    
    void NetworkClient::sendChatMessageProtobuf(const QString& message)
    {
        qDebug() << QString::fromUtf8("💬 Protobuf 채팅 메시지 전송: %1").arg(message);
        
        blokus::SendChatRequest request;
        request.set_content(message.toStdString());
        request.set_type(blokus::CHAT_TYPE_PUBLIC);
        request.set_scope(blokus::CHAT_SCOPE_ROOM);
        
        sendProtobufRequest(blokus::MESSAGE_TYPE_SEND_CHAT_REQUEST, request);
    }
    
    // ========================================
    // Protobuf 핸들러 구현
    // ========================================
    
    void NetworkClient::handleProtobufAuthResponse(const blokus::MessageWrapper& wrapper)
    {
        blokus::AuthResponse response;
        if (!unpackMessage(wrapper, response)) return;
        
        bool success = (response.result().code() == blokus::RESULT_SUCCESS);
        QString message = QString::fromStdString(response.result().message());
        QString sessionToken = QString::fromStdString(response.session_token());
        
        if (success) {
            m_currentSessionToken = sessionToken;
            setState(ConnectionState::Authenticated);
            qDebug() << QString::fromUtf8("✅ Protobuf 로그인 성공: %1")
                        .arg(QString::fromStdString(response.user_info().username()));
        } else {
            qDebug() << QString::fromUtf8("❌ Protobuf 로그인 실패: %1").arg(message);
        }
        
        emit loginResult(success, message, sessionToken);
    }
    
    void NetworkClient::handleProtobufRegisterResponse(const blokus::MessageWrapper& wrapper)
    {
        blokus::RegisterResponse response;
        if (!unpackMessage(wrapper, response)) return;
        
        bool success = (response.result().code() == blokus::RESULT_SUCCESS);
        QString message = QString::fromStdString(response.result().message());
        
        qDebug() << QString::fromUtf8("📝 Protobuf 회원가입 결과: %1 - %2")
                    .arg(success ? "성공" : "실패").arg(message);
        
        emit registerResult(success, message);
    }
    
    void NetworkClient::handleProtobufLogoutResponse(const blokus::MessageWrapper& wrapper)
    {
        blokus::LogoutResponse response;
        if (!unpackMessage(wrapper, response)) return;
        
        bool success = (response.result().code() == blokus::RESULT_SUCCESS);
        m_currentSessionToken.clear();
        setState(ConnectionState::Connected);
        
        qDebug() << QString::fromUtf8("🚪 Protobuf 로그아웃 완료");
        emit logoutResult(success);
    }
    
    void NetworkClient::handleProtobufHeartbeatResponse(const blokus::MessageWrapper& wrapper)
    {
        blokus::HeartbeatResponse response;
        if (!unpackMessage(wrapper, response)) return;
        
        // 하트비트 응답 처리 (필요시 추가 로직)
        qDebug() << QString::fromUtf8("💓 Heartbeat 응답 수신: seq=%1").arg(response.sequence_number());
    }
    
    void NetworkClient::handleProtobufCreateRoomResponse(const blokus::MessageWrapper& wrapper)
    {
        blokus::CreateRoomResponse response;
        if (!unpackMessage(wrapper, response)) return;
        
        bool success = (response.result().code() == blokus::RESULT_SUCCESS);
        QString message = QString::fromStdString(response.result().message());
        
        if (success) {
            int roomId = response.room_info().room_id();
            QString roomName = QString::fromStdString(response.room_info().room_name());
            
            qDebug() << QString::fromUtf8("✅ Protobuf 방 생성 성공: %1 (ID: %2)").arg(roomName).arg(roomId);
            emit roomCreated(roomId, roomName);
        } else {
            qDebug() << QString::fromUtf8("❌ Protobuf 방 생성 실패: %1").arg(message);
            emit roomError(message);
        }
    }
    
    void NetworkClient::handleProtobufJoinRoomResponse(const blokus::MessageWrapper& wrapper)
    {
        blokus::JoinRoomResponse response;
        if (!unpackMessage(wrapper, response)) return;
        
        bool success = (response.result().code() == blokus::RESULT_SUCCESS);
        QString message = QString::fromStdString(response.result().message());
        
        if (success) {
            int roomId = response.room_info().room_id();
            QString roomName = QString::fromStdString(response.room_info().room_name());
            
            qDebug() << QString::fromUtf8("✅ Protobuf 방 참여 성공: %1 (ID: %2)").arg(roomName).arg(roomId);
            emit roomJoined(roomId, roomName);
        } else {
            qDebug() << QString::fromUtf8("❌ Protobuf 방 참여 실패: %1").arg(message);
            emit roomError(message);
        }
    }
    
    void NetworkClient::handleProtobufLeaveRoomResponse(const blokus::MessageWrapper& wrapper)
    {
        blokus::LeaveRoomResponse response;
        if (!unpackMessage(wrapper, response)) return;
        
        bool success = (response.result().code() == blokus::RESULT_SUCCESS);
        
        if (success) {
            qDebug() << QString::fromUtf8("✅ Protobuf 방 나가기 성공");
            emit roomLeft();
        } else {
            qDebug() << QString::fromUtf8("❌ Protobuf 방 나가기 실패");
        }
    }
    
    void NetworkClient::handleProtobufSendChatResponse(const blokus::MessageWrapper& wrapper)
    {
        blokus::SendChatResponse response;
        if (!unpackMessage(wrapper, response)) return;
        
        bool success = (response.result().code() == blokus::RESULT_SUCCESS);
        
        if (success) {
            qDebug() << QString::fromUtf8("✅ Protobuf 채팅 메시지 전송 성공");
            emit chatMessageSent();
        } else {
            QString message = QString::fromStdString(response.result().message());
            qDebug() << QString::fromUtf8("❌ Protobuf 채팅 메시지 전송 실패: %1").arg(message);
            emit errorReceived(message);
        }
    }
    
    void NetworkClient::handleProtobufStartGameResponse(const blokus::MessageWrapper& wrapper)
    {
        blokus::StartGameResponse response;
        if (!unpackMessage(wrapper, response)) return;
        
        bool success = (response.result().code() == blokus::RESULT_SUCCESS);
        QString message = QString::fromStdString(response.result().message());
        
        if (success) {
            qDebug() << QString::fromUtf8("✅ Protobuf 게임 시작 성공");
            // 게임 시작은 별도의 notification으로 처리됨
        } else {
            qDebug() << QString::fromUtf8("❌ Protobuf 게임 시작 실패: %1").arg(message);
            emit roomError(message);
        }
    }
    
    void NetworkClient::handleProtobufErrorResponse(const blokus::MessageWrapper& wrapper)
    {
        blokus::ErrorResponse response;
        if (!unpackMessage(wrapper, response)) return;
        
        QString errorMessage = QString::fromStdString(response.message());
        
        qDebug() << QString::fromUtf8("❌ Protobuf 에러 응답: %1").arg(errorMessage);
        emit errorReceived(errorMessage);
    }
    
    // ========================================
    // Protobuf 유틸리티 함수들
    // ========================================
    
    template<typename T>
    bool NetworkClient::unpackMessage(const blokus::MessageWrapper& wrapper, T& message)
    {
        if (!wrapper.payload().UnpackTo(&message))
        {
            qDebug() << QString::fromUtf8("❌ Protobuf 메시지 언팩 실패: type=%1")
                        .arg(static_cast<int>(wrapper.type()));
            return false;
        }
        return true;
    }
    
    blokus::MessageWrapper NetworkClient::createRequestWrapper(blokus::MessageType type, const google::protobuf::Message& payload)
    {
        blokus::MessageWrapper wrapper;
        wrapper.set_type(type);
        wrapper.set_sequence_id(m_sequenceId++);
        wrapper.mutable_payload()->PackFrom(payload);
        wrapper.set_client_version("client-1.0.0");
        
        return wrapper;
    }
    
    // ========================================
    // Protobuf 알림 핸들러들
    // ========================================
    
    void NetworkClient::handleProtobufChatNotification(const blokus::MessageWrapper& wrapper)
    {
        blokus::ChatNotification notification;
        if (!unpackMessage(wrapper, notification)) return;
        
        QString username = QString::fromStdString(notification.message().sender_username());
        QString message = QString::fromStdString(notification.message().content());
        
        qDebug() << QString::fromUtf8("💬 Protobuf 채팅 알림 수신: %1: %2").arg(username).arg(message);
        emit chatMessageReceived(username, message);
    }
    
    void NetworkClient::handleProtobufPlayerJoinedNotification(const blokus::MessageWrapper& wrapper)
    {
        blokus::PlayerJoinedNotification notification;
        if (!unpackMessage(wrapper, notification)) return;
        
        QString username = QString::fromStdString(notification.username());
        
        qDebug() << QString::fromUtf8("👤 Protobuf 플레이어 참여 알림 수신: %1").arg(username);
        emit playerJoined(username);
    }
    
    void NetworkClient::handleProtobufPlayerLeftNotification(const blokus::MessageWrapper& wrapper)
    {
        blokus::PlayerLeftNotification notification;
        if (!unpackMessage(wrapper, notification)) return;
        
        QString username = QString::fromStdString(notification.username());
        
        qDebug() << QString::fromUtf8("👤 Protobuf 플레이어 퇴장 알림 수신: %1").arg(username);
        emit playerLeft(username);
    }
    
    void NetworkClient::handleProtobufPlayerReadyNotification(const blokus::MessageWrapper& wrapper)
    {
        blokus::PlayerReadyNotification notification;
        if (!unpackMessage(wrapper, notification)) return;
        
        QString username = QString::fromStdString(notification.username());
        bool ready = notification.ready();
        
        qDebug() << QString::fromUtf8("👤 Protobuf 플레이어 준비 상태 알림 수신: %1 -> %2").arg(username).arg(ready ? "준비완료" : "대기중");
        emit playerReady(username, ready);
    }
    
    void NetworkClient::handleProtobufGameStartedNotification(const blokus::MessageWrapper& wrapper)
    {
        blokus::GameStartedNotification notification;
        if (!unpackMessage(wrapper, notification)) return;
        
        qDebug() << QString::fromUtf8("🎮 Protobuf 게임 시작 알림 수신");
        emit gameStarted();
    }
    
    void NetworkClient::handleProtobufGameEndedNotification(const blokus::MessageWrapper& wrapper)
    {
        blokus::GameEndedNotification notification;
        if (!unpackMessage(wrapper, notification)) return;
        
        qDebug() << QString::fromUtf8("🎮 Protobuf 게임 종료 알림 수신");
        emit gameEnded();
        
        // 게임 결과 전달 
        QString resultJson = QString::fromStdString(notification.DebugString());
        emit gameResult(resultJson);
        
        // 승자 정보가 있는 경우
        if (!notification.winner().empty()) {
            QString winner = QString::fromStdString(notification.winner());
            qDebug() << QString::fromUtf8("🏆 게임 승자: %1").arg(winner);
        }
    }

} // namespace Blokus