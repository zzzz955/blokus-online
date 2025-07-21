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