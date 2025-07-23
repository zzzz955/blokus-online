#include "NetworkClient.h"
#include <QDebug>
#include <QHostAddress>

namespace Blokus {

    NetworkClient::NetworkClient(QObject* parent)
        : QObject(parent)
        , m_socket(nullptr)
        , m_connectionTimer(new QTimer(this))
        , m_reconnectTimer(new QTimer(this))
        , m_state(ConnectionState::Disconnected)
        , m_serverHost("localhost")
        , m_serverPort(9999)
        , m_currentSessionToken("")
        , m_reconnectInterval(5000) // 5초
        , m_maxReconnectAttempts(3)
        , m_reconnectAttempts(0)
        , m_connectionTimeout(10000) // 10초
    {
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
        
        if (message.startsWith("ERROR:")) {
            QString error = message.mid(6); // "ERROR:" 제거
            processErrorMessage(error);
        }
        else if (message.startsWith("AUTH_SUCCESS:") || 
                 message.startsWith("REGISTER_SUCCESS:") ||
                 message.startsWith("LOGOUT_SUCCESS")) {
            processAuthResponse(message);
        }
        else if (message.startsWith("LOBBY_") || message.startsWith("ROOM_") || message.startsWith("CHAT:")) {
            processLobbyResponse(message);
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
            
            // 서버 형식: LOBBY_USER_LIST:count:user1,status1:user2,status2...
            for (int i = 2; i < parts.size(); ++i) {
                if (!parts[i].isEmpty()) {
                    QStringList userInfo = parts[i].split(',');
                    if (!userInfo.isEmpty()) {
                        users.append(userInfo[0]); // 사용자명만 추출
                        qDebug() << QString::fromUtf8("사용자 추가: %1").arg(userInfo[0]);
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
        else if (parts[0] == "SYSTEM" && parts.size() >= 2) {
            QString systemMessage = parts.mid(1).join(":");
            // 시스템 메시지를 채팅으로 처리
            emit chatMessageReceived(QString::fromUtf8("시스템"), systemMessage);
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

} // namespace Blokus