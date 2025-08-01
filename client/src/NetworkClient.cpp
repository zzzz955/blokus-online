#include "NetworkClient.h"
#include "ClientConfigManager.h"
#include "ClientVersion.h"
#include <QDebug>
#include <QHostAddress>
#include <QRegExp>
#include <QDesktopServices>
#include <QUrl>
#include <QMessageBox>
#include <ctime>

namespace Blokus {

    NetworkClient::NetworkClient(QObject* parent)
        : QObject(parent)
        , m_socket(nullptr)
        , m_connectionTimer(new QTimer(this))
        , m_reconnectTimer(new QTimer(this))
        , m_state(ConnectionState::Disconnected)
        , m_currentSessionToken("")
        , m_reconnectAttempts(0)
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

    void NetworkClient::sendBinaryMessage(const QByteArray& data)
    {
        if (!isConnected() || !m_socket) {
            qWarning() << QString::fromUtf8("서버에 연결되지 않음 - 바이너리 메시지 전송 실패");
            return;
        }
        
        qint64 written = m_socket->write(data);
        
        if (written != data.size()) {
            qWarning() << QString::fromUtf8("바이너리 메시지 전송 불완전: %1 bytes written of %2").arg(written).arg(data.size());
        } else {
            qDebug() << QString::fromUtf8("바이너리 메시지 전송: %1 bytes").arg(data.size());
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
        
        // 연결 성공 후 즉시 버전 검사 수행
        performVersionCheck();
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
        else if (message.startsWith("version:")) {
            QStringList parts = message.split(":");
            if (parts.size() >= 2) {
                processVersionCheckResponse(parts);
            }
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
    // 버전 관련 메서드들
    // ========================================
    
    void NetworkClient::performVersionCheck()
    {
        qDebug() << QString::fromUtf8("🔍 서버 버전 호환성 검사 시작 - 클라이언트 버전: %1")
                    .arg(QString::fromStdString(Blokus::Client::ClientVersion::getVersion()));
        
        // 버전 확인 요청 (텍스트 기반)
        QString versionMessage = QString("version:check:%1\n")
                                .arg(QString::fromStdString(Blokus::Client::ClientVersion::getVersion()));
        sendMessage(versionMessage);
    }
    
    void NetworkClient::processVersionCheckResponse(const QStringList& params)
    {
        // params[0] = "version", params[1] = "ok" or "mismatch", params[2] = downloadUrl (if mismatch)
        
        if (params.size() < 2) {
            qDebug() << QString::fromUtf8("❌ 버전 응답 형식 오류: %1").arg(params.join(":"));
            return;
        }
        
        QString status = params[1];
        
        qDebug() << QString::fromUtf8("📋 버전 호환성 검사 결과 - 상태: %1")
                    .arg(status);
        
        if (status == "ok") {
            // 버전 호환 - 정상 연결 완료
            qDebug() << QString::fromUtf8("✅ 버전 호환성 확인 완료 - 서버 연결 성공");
            emit versionCheckCompleted(true);
            emit connected(); // 이제 진짜 연결 완료 시그널 발송
        } 
        else if (status == "mismatch") {
            // 버전 불호환 - 다운로드 페이지로 리다이렉트
            QString downloadUrl;
            if (params.size() >= 3) {
                downloadUrl = params[2];
            } else {
                downloadUrl = "https://blokus-online.mooo.com/download"; // 기본값
            }
            
            emit versionIncompatible("", downloadUrl);
            
            // 다운로드 확인 다이얼로그 표시
            QMessageBox msgBox;
            msgBox.setWindowTitle(QString::fromUtf8("클라이언트 업데이트 필요"));
            msgBox.setText(QString::fromUtf8("서버와 호환되지 않는 클라이언트 버전입니다."));
            msgBox.setInformativeText(QString::fromUtf8("클라이언트: %1\n\n최신 버전을 다운로드하시겠습니까?")
                                     .arg(QString::fromStdString(Blokus::Client::ClientVersion::getVersion())));
            msgBox.setStandardButtons(QMessageBox::Yes | QMessageBox::No);
            msgBox.setDefaultButton(QMessageBox::Yes);
            
            if (msgBox.exec() == QMessageBox::Yes) {
                // 다운로드 페이지 열기
                qDebug() << QString::fromUtf8("🌐 다운로드 페이지 열기: %1").arg(downloadUrl);
                QDesktopServices::openUrl(QUrl(downloadUrl));
            }
            
            // 연결 종료
            disconnect();
            
            emit versionCheckCompleted(false);
        } else {
            qDebug() << QString::fromUtf8("❌ 알 수 없는 버전 응답: %1").arg(status);
        }
    }
} // namespace Blokus