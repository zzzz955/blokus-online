#include "OidcAuthenticator.h"
#include <QDesktopServices>
#include <QRandomGenerator>
#include <QHttpPart>
#include <QNetworkRequest>
#include <QStandardPaths>
#include <QDir>
#include <QDebug>
#include <QApplication>
#include <QTimer>
#include <QThread>

#ifdef Q_OS_WIN
#include <windows.h>
#include <wincred.h>
#endif

namespace Blokus {

const QString OidcAuthenticator::CREDENTIAL_SERVICE_NAME = "BlokusOnline_OIDC";
const QString OidcAuthenticator::CREDENTIAL_USERNAME = "oauth_tokens";

// Qt::QueuedConnection을 위한 Meta-Type 등록 (생성자 전에 실행)
static const int oidcTokensTypeId = qRegisterMetaType<Blokus::OidcTokens>("OidcTokens");

OidcAuthenticator::OidcAuthenticator(QObject* parent)
    : QObject(parent)
    , m_networkManager(new QNetworkAccessManager(this))
    , m_loopbackServer(new QTcpServer(this))
    , m_currentSocket(nullptr)
    , m_loopbackPort(0)
    , m_authTimeoutTimer(new QTimer(this))
    , m_isCleaningUp(false)
    , m_authenticationCompleted(false)
{
    // 기본 OIDC 설정: 빌드 모드에 따른 하드코딩된 값 사용
    #ifdef _DEBUG
        // Debug 모드: localhost 사용
        m_config.issuer = "https://blokus-online.mooo.com/oidc";
        m_config.authorizationEndpoint = "https://blokus-online.mooo.com/oidc/authorize";
        m_config.tokenEndpoint = "https://blokus-online.mooo.com/oidc/token";
        qDebug() << QString::fromUtf8(" 디버그 모드: localhost OIDC 서버 사용");
    #else
        // Release 모드: 프로덕션 서버 사용 (Nginx 서브패스 프록시)
        m_config.issuer = "https://blokus-online.mooo.com/oidc";
        m_config.authorizationEndpoint = "https://blokus-online.mooo.com/oidc/authorize";
        m_config.tokenEndpoint = "https://blokus-online.mooo.com/oidc/token";
        qDebug() << QString::fromUtf8(" 릴리즈 모드: 프로덕션 OIDC 서버 사용 (https://blokus-online.mooo.com/oidc)");
    #endif
    m_config.clientId = "blokus-desktop-client";
    m_config.redirectUri = "http://localhost:{PORT}/callback"; // PORT는 동적으로 설정
    m_config.scopes = QStringList({"openid", "profile", "email"});

    // 타이머 설정
    m_authTimeoutTimer->setSingleShot(true);
    m_authTimeoutTimer->setInterval(AUTH_TIMEOUT_MS);

    // 시그널 연결
    // 시그널 연결을 Qt::QueuedConnection으로 변경 (스레드 안전성)
    connect(m_loopbackServer, &QTcpServer::newConnection, 
            this, &OidcAuthenticator::onLoopbackServerNewConnection, Qt::QueuedConnection);
    connect(m_authTimeoutTimer, &QTimer::timeout, this, [this]() {
        qDebug() << " [THREAD-SAFE] 인증 타임아웃 - Thread ID:" << QThread::currentThreadId();
        
        // UI 업데이트를 메인 스레드에서 실행
        QMetaObject::invokeMethod(this, [this]() {
            stopLoopbackServer();
            emit authenticationFailed("인증 시간이 초과되었습니다.");
        }, Qt::QueuedConnection);
    }, Qt::QueuedConnection);

    qDebug() << "OidcAuthenticator 초기화 완료";
}

OidcAuthenticator::~OidcAuthenticator()
{
    stopLoopbackServer();
}

void OidcAuthenticator::setConfig(const OidcConfig& config)
{
    m_config = config;
    qDebug() << "OIDC 설정 업데이트:" << config.issuer;
}

void OidcAuthenticator::startAuthenticationFlow()
{
    qDebug() << " [DEBUG] OIDC 인증 플로우 시작";
    qDebug() << " [DEBUG] Thread ID:" << QThread::currentThreadId();

    // 로컬 서버 시작
    if (!startLoopbackServer()) {
        qDebug() << " [DEBUG] 로컬 서버 시작 실패 - 인증 중단";
        emit authenticationFailed("로컬 서버 시작 실패");
        return;
    }

    // PKCE 파라미터 생성
    m_codeVerifier = generateCodeVerifier();
    m_codeChallenge = generateCodeChallenge(m_codeVerifier);
    m_state = generateRandomString(32);

    // 리다이렉트 URI 업데이트 (동적 포트)
    QString redirectUri = QString("http://localhost:%1/callback").arg(m_loopbackPort);
    m_config.redirectUri = redirectUri;

    // 인증 URL 생성 및 브라우저 실행
    QUrl authUrl = buildAuthorizationUrl();
    qDebug() << "브라우저에서 인증 URL 열기:" << authUrl.toString();

    if (!QDesktopServices::openUrl(authUrl)) {
        stopLoopbackServer();
        emit authenticationFailed("브라우저 실행 실패");
        return;
    }

    // 타이머 시작
    m_authTimeoutTimer->start();
}

void OidcAuthenticator::tryAutoLogin()
{
    qDebug() << "자동 로그인 시도";

    OidcTokens tokens = loadTokensSecurely();
    if (tokens.accessToken.isEmpty() || tokens.refreshToken.isEmpty()) {
        qDebug() << "저장된 토큰이 없음";
        emit authenticationFailed("저장된 토큰이 없습니다.");
        return;
    }

    m_currentTokens = tokens;

    // Access Token이 만료된 경우 새로고침 시도
    if (!hasValidTokens()) {
        qDebug() << "토큰 만료됨, 새로고침 시도";
        refreshTokens();
        return;
    }

    // 유효한 토큰이 있는 경우
    qDebug() << "유효한 토큰으로 자동 로그인 성공";
    emit authenticationSucceeded(m_currentTokens.accessToken, m_currentTokens);
}

void OidcAuthenticator::refreshTokens()
{
    if (m_currentTokens.refreshToken.isEmpty()) {
        emit tokenRefreshFailed("Refresh Token이 없습니다.");
        return;
    }

    qDebug() << "토큰 새로고침 시작";

    QNetworkRequest request;
    request.setUrl(QUrl(m_config.tokenEndpoint));
    request.setHeader(QNetworkRequest::ContentTypeHeader, "application/x-www-form-urlencoded");

    QUrlQuery params;
    params.addQueryItem("grant_type", "refresh_token");
    params.addQueryItem("refresh_token", m_currentTokens.refreshToken);
    params.addQueryItem("client_id", m_config.clientId);

    QByteArray data = params.toString(QUrl::FullyEncoded).toUtf8();

    QNetworkReply* reply = m_networkManager->post(request, data);
    // 토큰 새로고침 응답을 Qt::QueuedConnection으로 연결 (스레드 안전성)
    connect(reply, &QNetworkReply::finished, this, &OidcAuthenticator::onTokenRefreshFinished, Qt::QueuedConnection);
}

void OidcAuthenticator::logout()
{
    qDebug() << "로그아웃 시작";
    
    clearStoredTokens();
    m_currentTokens = OidcTokens();
    
    qDebug() << "로그아웃 완료";
}

QString OidcAuthenticator::getCurrentAccessToken() const
{
    return m_currentTokens.accessToken;
}

bool OidcAuthenticator::hasValidTokens() const
{
    // 간단한 토큰 유효성 검사 (실제로는 JWT 디코딩해서 exp 확인해야 함)
    return !m_currentTokens.accessToken.isEmpty() && !m_currentTokens.refreshToken.isEmpty();
}

// PKCE 관련 메서드들
QString OidcAuthenticator::generateCodeVerifier()
{
    return generateRandomString(128);
}

QString OidcAuthenticator::generateCodeChallenge(const QString& verifier)
{
    QByteArray hash = QCryptographicHash::hash(verifier.toUtf8(), QCryptographicHash::Sha256);
    return hash.toBase64(QByteArray::Base64UrlEncoding | QByteArray::OmitTrailingEquals);
}

QString OidcAuthenticator::generateRandomString(int length)
{
    const QString chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
    QString result;
    result.reserve(length);
    
    for (int i = 0; i < length; ++i) {
        int index = QRandomGenerator::global()->bounded(chars.length());
        result.append(chars.at(index));
    }
    
    return result;
}

QUrl OidcAuthenticator::buildAuthorizationUrl()
{
    QUrl url(m_config.authorizationEndpoint);
    QUrlQuery query;
    
    query.addQueryItem("response_type", "code");
    query.addQueryItem("client_id", m_config.clientId);
    query.addQueryItem("redirect_uri", m_config.redirectUri);
    query.addQueryItem("scope", m_config.scopes.join(" "));
    query.addQueryItem("state", m_state);
    query.addQueryItem("code_challenge", m_codeChallenge);
    query.addQueryItem("code_challenge_method", "S256");
    
    url.setQuery(query);
    return url;
}

// 로컬 HTTP 서버 관련
bool OidcAuthenticator::startLoopbackServer()
{
    qDebug() << " [DEBUG] 로컬 서버 시작 시도...";
    
    // 임의의 포트에서 서버 시작
    if (!m_loopbackServer->listen(QHostAddress::LocalHost)) {
        qDebug() << " [DEBUG] 로컬 서버 시작 실패:" << m_loopbackServer->errorString();
        qDebug() << " [DEBUG] 서버 상태:" << m_loopbackServer->serverError();
        return false;
    }
    
    m_loopbackPort = m_loopbackServer->serverPort();
    qDebug() << " [DEBUG] 로컬 서버 시작됨, 포트:" << m_loopbackPort;
    qDebug() << " [DEBUG] 서버 주소:" << m_loopbackServer->serverAddress().toString();
    qDebug() << " [DEBUG] 리스닝 상태:" << m_loopbackServer->isListening();
    qDebug() << " [DEBUG] 최대 대기 연결:" << m_loopbackServer->maxPendingConnections();
    
    // 서버가 실제로 바인딩되었는지 확인
    if (!m_loopbackServer->isListening()) {
        qDebug() << " [DEBUG] 서버가 리스닝 상태가 아님!";
        return false;
    }
    
    return true;
}

void OidcAuthenticator::stopLoopbackServer()
{
    qDebug() << " [THREAD-SAFE] 로컬 서버 안전 정리 시작 - Thread ID:" << QThread::currentThreadId();
    
    // 1단계: 타이머 중지 (새 연결 방지)
    if (m_authTimeoutTimer->isActive()) {
        m_authTimeoutTimer->stop();
        qDebug() << " [THREAD-SAFE] 인증 타이머 중지";
    }
    
    // 2단계: 서버 리스닝 중지 (새 연결 방지)
    if (m_loopbackServer && m_loopbackServer->isListening()) {
        m_loopbackServer->close();
        qDebug() << " [THREAD-SAFE] 로컬 서버 리스닝 중지";
    }
    
    // 3단계: 소켓 안전 정리 (비동기)
    if (m_currentSocket) {
        qDebug() << " [THREAD-SAFE] 현재 소켓 안전 해제 시작";
        
        // 소켓 연결 해제 (즉시 강제 종료하지 않고 큐에서 처리)
        QMetaObject::invokeMethod(this, [this]() {
            if (m_currentSocket) {
                qDebug() << " [THREAD-SAFE] 소켓 연결 해제 실행";
                m_currentSocket->disconnectFromHost();
                m_currentSocket->deleteLater();  // 안전한 삭제
                m_currentSocket = nullptr;
                qDebug() << " [THREAD-SAFE] 소켓 정리 완료";
            }
        }, Qt::QueuedConnection);
    }
    
    qDebug() << " [THREAD-SAFE] 로컬 서버 안전 정리 완료";
}

void OidcAuthenticator::onLoopbackServerNewConnection()
{
    qDebug() << " [DEBUG] 새 연결 수신!";
    
    m_currentSocket = m_loopbackServer->nextPendingConnection();
    if (!m_currentSocket) {
        qDebug() << " [DEBUG] nextPendingConnection()이 null 반환!";
        return;
    }
    
    qDebug() << " [DEBUG] 소켓 연결됨:" << m_currentSocket->peerAddress().toString() << ":" << m_currentSocket->peerPort();
    qDebug() << " [DEBUG] 로컬 주소:" << m_currentSocket->localAddress().toString() << ":" << m_currentSocket->localPort();
    
    connect(m_currentSocket, &QTcpSocket::readyRead, this, &OidcAuthenticator::onLoopbackSocketReadyRead);
    connect(m_currentSocket, &QTcpSocket::disconnected, m_currentSocket, &QTcpSocket::deleteLater);
    
    // 소켓 상태 확인
    qDebug() << " [DEBUG] 소켓 상태:" << m_currentSocket->state();
    qDebug() << " [DEBUG] 소켓 오류:" << m_currentSocket->errorString();
}

void OidcAuthenticator::onLoopbackSocketReadyRead()
{
    qDebug() << " [THREAD-SAFE] HTTP 요청 처리 시작 - Thread ID:" << QThread::currentThreadId();
    
    QTcpSocket* socket = qobject_cast<QTcpSocket*>(sender());
    if (!socket) {
        qDebug() << " [THREAD-SAFE] 소켓이 null - 무시";
        return;
    }

    QByteArray data = socket->readAll();
    QString request = QString::fromUtf8(data);
    
    qDebug() << " [THREAD-SAFE] HTTP 요청 수신:" << request.left(100);

    // HTTP 요청에서 경로 추출
    QStringList lines = request.split("\r\n");
    if (lines.isEmpty()) {
        sendHttpResponse(socket, 400, "Bad Request");
        return;
    }

    QString requestLine = lines.first();
    QStringList parts = requestLine.split(" ");
    if (parts.size() < 2) {
        sendHttpResponse(socket, 400, "Bad Request");
        return;
    }

    QString path = parts[1];
    
    // 콜백 처리를 비동기로 실행 (스레드 안전성)
    QMetaObject::invokeMethod(this, [this, socket, path]() {
        qDebug() << " [THREAD-SAFE] 비동기 콜백 처리 시작";
        QString response = handleAuthCodeResponse(path);
        
        if (response.isEmpty()) {
            sendHttpResponse(socket, 400, "Authentication failed");
        } else {
            sendHttpResponse(socket, 200, response);
        }
        qDebug() << " [THREAD-SAFE] 비동기 콜백 처리 완료";
    }, Qt::QueuedConnection);
}

QString OidcAuthenticator::handleAuthCodeResponse(const QString& requestPath)
{
    QUrl url("http://localhost" + requestPath);
    QUrlQuery query(url);

    // 에러 확인
    if (query.hasQueryItem("error")) {
        QString error = query.queryItemValue("error");
        QString errorDescription = query.queryItemValue("error_description");
        QString errorMsg = QString("OAuth Error: %1").arg(error);
        if (!errorDescription.isEmpty()) {
            errorMsg += QString(" - %1").arg(errorDescription);
        }
        
        stopLoopbackServer();
        emit authenticationFailed(errorMsg);
        return QString();
    }

    // Authorization Code 확인
    if (!query.hasQueryItem("code")) {
        stopLoopbackServer();
        emit authenticationFailed("Authorization code가 없습니다.");
        return QString();
    }

    // State 검증
    QString receivedState = query.queryItemValue("state");
    if (receivedState != m_state) {
        stopLoopbackServer();
        emit authenticationFailed("State 검증 실패");
        return QString();
    }

    QString authCode = query.queryItemValue("code");
    qDebug() << "Authorization code 수신:" << authCode.left(20) + "...";

    // 토큰 교환 시작
    exchangeCodeForTokens(authCode);

    return QString::fromUtf8(
        "<!DOCTYPE html>"
        "<html><head><title>인증 완료</title></head>"
        "<body style='font-family: sans-serif; text-align: center; padding: 50px;'>"
        "<h1>🎉 인증이 완료되었습니다!</h1>"
        "<p>이제 이 창을 닫고 게임으로 돌아가세요.</p>"
        "<script>setTimeout(() => window.close(), 3000);</script>"
        "</body></html>"
    );
}

void OidcAuthenticator::exchangeCodeForTokens(const QString& authCode)
{
    qDebug() << "토큰 교환 시작";

    QNetworkRequest request;
    request.setUrl(QUrl(m_config.tokenEndpoint));
    request.setHeader(QNetworkRequest::ContentTypeHeader, "application/x-www-form-urlencoded");

    QUrlQuery params;
    params.addQueryItem("grant_type", "authorization_code");
    params.addQueryItem("code", authCode);
    params.addQueryItem("redirect_uri", m_config.redirectUri);
    params.addQueryItem("client_id", m_config.clientId);
    params.addQueryItem("code_verifier", m_codeVerifier);

    QByteArray data = params.toString(QUrl::FullyEncoded).toUtf8();

    QNetworkReply* reply = m_networkManager->post(request, data);
    // 토큰 교환 응답을 Qt::QueuedConnection으로 연결 (스레드 안전성)
    connect(reply, &QNetworkReply::finished, this, &OidcAuthenticator::onTokenExchangeFinished, Qt::QueuedConnection);
}

void OidcAuthenticator::onTokenExchangeFinished()
{
    qDebug() << " [THREAD-SAFE] === onTokenExchangeFinished 호출됨 === Thread ID:" << QThread::currentThreadId();
    
    // 재진입 방지 가드
    if (m_authenticationCompleted.exchange(true)) {
        qDebug() << " [THREAD-SAFE] 이미 인증 완료됨 - 중복 처리 방지";
        return;
    }
    
    QNetworkReply* reply = qobject_cast<QNetworkReply*>(sender());
    if (!reply) {
        qDebug() << " [THREAD-SAFE] reply가 null - 인증 상태 리셋";
        m_authenticationCompleted = false;
        return;
    }

    try {
        qDebug() << "HTTP 응답 코드:" << reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt();
        
        // 로컬 서버 중지를 나중으로 연기 (크래시 방지)
        qDebug() << "로컬 서버는 나중에 정리됩니다";
        
        qDebug() << "에러 체크 중...";
        if (reply->error() != QNetworkReply::NoError) {
            qDebug() << " [THREAD-SAFE] 토큰 교환 네트워크 오류:" << reply->errorString();
            qDebug() << " [THREAD-SAFE] 에러 코드:" << reply->error();
            reply->deleteLater();
            
            // 인증 실패 시그널을 메인 스레드에서 발송
            QMetaObject::invokeMethod(this, [this, error = reply->errorString()]() {
                m_authenticationCompleted = false;  // 실패 시 리셋
                emit authenticationFailed("토큰 교환 실패: " + error);
            }, Qt::QueuedConnection);
            return;
        }

        qDebug() << "응답 데이터 읽기 시작...";
        QByteArray responseData = reply->readAll();
        qDebug() << "응답 데이터 크기:" << responseData.size() << "bytes";
        qDebug() << "응답 내용 (첫 100자):" << responseData.left(100);
        
        // reply 사용 완료 후 삭제 예약
        reply->deleteLater();
        
        qDebug() << "JSON 파싱 시작...";
        QJsonDocument doc = QJsonDocument::fromJson(responseData);
        
        if (!doc.isObject()) {
            qDebug() << " [THREAD-SAFE] JSON 파싱 실패 - 유효한 객체가 아님";
            
            // JSON 파싱 실패 시그널을 메인 스레드에서 발송
            QMetaObject::invokeMethod(this, [this]() {
                m_authenticationCompleted = false;  // 실패 시 리셋
                emit authenticationFailed("잘못된 토큰 응답 형식");
            }, Qt::QueuedConnection);
            return;
        }

        QJsonObject obj = doc.object();
        qDebug() << "JSON 객체 키 목록:" << obj.keys();
        
        if (obj.contains("error")) {
            QString error = obj["error"].toString();
            QString errorDescription = obj.value("error_description").toString();
            qDebug() << " [THREAD-SAFE] 토큰 교환 서버 오류:" << error << "-" << errorDescription;
            
            // 서버 오류 시그널을 메인 스레드에서 발송
            QMetaObject::invokeMethod(this, [this, error, errorDescription]() {
                m_authenticationCompleted = false;  // 실패 시 리셋
                emit authenticationFailed(QString("토큰 오류: %1 - %2").arg(error, errorDescription));
            }, Qt::QueuedConnection);
            return;
        }

        qDebug() << "토큰 응답 파싱 중...";
        m_currentTokens = parseTokenResponse(obj);
        
        qDebug() << "Access Token 길이:" << m_currentTokens.accessToken.length();
        if (m_currentTokens.accessToken.isEmpty()) {
            qDebug() << " [THREAD-SAFE] Access Token이 비어있음!";
            
            // Access Token 빈 시그널을 메인 스레드에서 발송
            QMetaObject::invokeMethod(this, [this]() {
                m_authenticationCompleted = false;  // 실패 시 리셋
                emit authenticationFailed("Access Token을 받지 못했습니다.");
            }, Qt::QueuedConnection);
            return;
        }

        qDebug() << "토큰 저장 시작...";
        // 토큰 안전하게 저장 (예외 처리 추가)
        try {
            saveTokensSecurely(m_currentTokens);
            qDebug() << "토큰 저장 완료";
        } catch (const std::exception& e) {
            qDebug() << "토큰 저장 실패, 계속 진행:" << e.what();
            // 저장 실패해도 인증은 성공으로 처리
        }

        qDebug() << "토큰 교환 성공";
        qDebug() << " [THREAD-SAFE] authenticationSucceeded 시그널 emit 중... Thread ID:" << QThread::currentThreadId();
        
        // 즉시 시그널 emit (Qt::QueuedConnection으로 연결되어 안전)
        emit authenticationSucceeded(m_currentTokens.accessToken, m_currentTokens);
        qDebug() << " [THREAD-SAFE] authenticationSucceeded 시그널 emit 완료";
        
        // 로컬 서버 정리를 충분한 지연 후 실행 (시그널 처리 완료 대기)
        qDebug() << " [THREAD-SAFE] 로컬 서버 안전 정리 예약 (1초 지연)...";
        QTimer::singleShot(1000, [this]() {
            qDebug() << " [THREAD-SAFE] 지연된 로컬 서버 정리 시작... Thread ID:" << QThread::currentThreadId();
            try {
                // 메인 스레드에서 정리 실행 보장
                QMetaObject::invokeMethod(this, [this]() {
                    stopLoopbackServer();
                    qDebug() << " [THREAD-SAFE] 지연된 로컬 서버 정리 완료";
                }, Qt::QueuedConnection);
            } catch (const std::exception& e) {
                qDebug() << " [THREAD-SAFE] 지연된 로컬 서버 정리 중 예외:" << e.what();
            } catch (...) {
                qDebug() << " [THREAD-SAFE] 지연된 로컬 서버 정리 중 알 수 없는 예외 (무시됨)";
            }
        });
        
    } catch (const std::exception& e) {
        qDebug() << " [THREAD-SAFE] onTokenExchangeFinished 예외 발생:" << e.what();
        
        // 예외 시그널을 메인 스레드에서 발송
        QMetaObject::invokeMethod(this, [this, error = QString(e.what())]() {
            m_authenticationCompleted = false;  // 예외 시 리셋
            emit authenticationFailed("토큰 처리 중 예외 발생: " + error);
        }, Qt::QueuedConnection);
    } catch (...) {
        qDebug() << " [THREAD-SAFE] onTokenExchangeFinished 알 수 없는 예외 발생";
        
        // 알 수 없는 예외 시그널을 메인 스레드에서 발송
        QMetaObject::invokeMethod(this, [this]() {
            m_authenticationCompleted = false;  // 예외 시 리셋
            emit authenticationFailed("토큰 처리 중 알 수 없는 예외 발생");
        }, Qt::QueuedConnection);
    }
}

void OidcAuthenticator::onTokenRefreshFinished()
{
    QNetworkReply* reply = qobject_cast<QNetworkReply*>(sender());
    if (!reply) return;

    reply->deleteLater();

    if (reply->error() != QNetworkReply::NoError) {
        qDebug() << "토큰 새로고침 네트워크 오류:" << reply->errorString();
        emit tokenRefreshFailed("토큰 새로고침 실패: " + reply->errorString());
        return;
    }

    QByteArray responseData = reply->readAll();
    QJsonDocument doc = QJsonDocument::fromJson(responseData);
    
    if (!doc.isObject()) {
        emit tokenRefreshFailed("잘못된 새로고침 응답 형식");
        return;
    }

    QJsonObject obj = doc.object();
    
    if (obj.contains("error")) {
        QString error = obj["error"].toString();
        emit tokenRefreshFailed("토큰 새로고침 오류: " + error);
        return;
    }

    m_currentTokens = parseTokenResponse(obj);
    
    if (m_currentTokens.accessToken.isEmpty()) {
        emit tokenRefreshFailed("새로고침된 Access Token을 받지 못했습니다.");
        return;
    }

    // 새로운 토큰 저장
    saveTokensSecurely(m_currentTokens);

    qDebug() << "토큰 새로고침 성공";
    emit tokensRefreshed(m_currentTokens.accessToken);
}

void OidcAuthenticator::sendHttpResponse(QTcpSocket* socket, int statusCode, const QString& body)
{
    QString statusText = (statusCode == 200) ? "OK" : "Bad Request";
    QString response = QString(
        "HTTP/1.1 %1 %2\r\n"
        "Content-Type: text/html; charset=utf-8\r\n"
        "Content-Length: %3\r\n"
        "Connection: close\r\n"
        "\r\n"
        "%4"
    ).arg(statusCode).arg(statusText).arg(body.toUtf8().size()).arg(body);

    socket->write(response.toUtf8());
    socket->flush();
    socket->disconnectFromHost();
}

OidcTokens OidcAuthenticator::parseTokenResponse(const QJsonObject& json)
{
    OidcTokens tokens;
    tokens.accessToken = json["access_token"].toString();
    tokens.refreshToken = json["refresh_token"].toString();
    tokens.idToken = json["id_token"].toString();
    tokens.expiresIn = json["expires_in"].toInt();
    tokens.tokenType = json.value("token_type").toString("Bearer");
    tokens.scope = json["scope"].toString();
    return tokens;
}

// 토큰 저장/로드 (Windows Credential Manager 사용)
void OidcAuthenticator::saveTokensSecurely(const OidcTokens& tokens)
{
#ifdef Q_OS_WIN
    // JSON으로 직렬화
    QJsonObject obj;
    obj["access_token"] = tokens.accessToken;
    obj["refresh_token"] = tokens.refreshToken;
    obj["id_token"] = tokens.idToken;
    obj["expires_in"] = tokens.expiresIn;
    obj["token_type"] = tokens.tokenType;
    obj["scope"] = tokens.scope;
    obj["saved_at"] = QDateTime::currentDateTime().toString(Qt::ISODate);
    
    QJsonDocument doc(obj);
    QByteArray data = doc.toJson(QJsonDocument::Compact);
    
    // Windows Credential Manager에 저장 (안전성 개선)
    try {
        CREDENTIALW cred = {};
        cred.Type = CRED_TYPE_GENERIC;
        cred.TargetName = (LPWSTR)CREDENTIAL_SERVICE_NAME.utf16();
        cred.UserName = (LPWSTR)CREDENTIAL_USERNAME.utf16();
        cred.CredentialBlob = (LPBYTE)data.data();
        cred.CredentialBlobSize = data.size();
        cred.Persist = CRED_PERSIST_LOCAL_MACHINE;
        
        if (CredWriteW(&cred, 0)) {
            qDebug() << "토큰이 안전하게 저장됨";
        } else {
            DWORD error = GetLastError();
            qDebug() << "토큰 저장 실패, 오류 코드:" << error;
            // 저장 실패해도 예외는 발생시키지 않음
        }
    } catch (...) {
        qDebug() << "Credential Manager 접근 실패 - 토큰 저장 건너뜀";
    }
#else
    // 다른 플랫폼의 경우 임시로 로컬 파일에 저장 (보안상 권장되지 않음)
    QString configDir = QStandardPaths::writableLocation(QStandardPaths::AppConfigLocation);
    QDir().mkpath(configDir);
    
    QJsonObject obj;
    obj["access_token"] = tokens.accessToken;
    obj["refresh_token"] = tokens.refreshToken;
    obj["id_token"] = tokens.idToken;
    obj["expires_in"] = tokens.expiresIn;
    obj["token_type"] = tokens.tokenType;
    obj["scope"] = tokens.scope;
    obj["saved_at"] = QDateTime::currentDateTime().toString(Qt::ISODate);
    
    QJsonDocument doc(obj);
    QFile file(configDir + "/oidc_tokens.json");
    if (file.open(QIODevice::WriteOnly)) {
        file.write(doc.toJson());
        qDebug() << "토큰이 로컬 파일에 저장됨 (보안 주의)";
    }
#endif
}

OidcTokens OidcAuthenticator::loadTokensSecurely()
{
    OidcTokens tokens;
    
#ifdef Q_OS_WIN
    PCREDENTIALW cred;
    if (CredReadW((LPWSTR)CREDENTIAL_SERVICE_NAME.utf16(), CRED_TYPE_GENERIC, 0, &cred)) {
        QByteArray data((char*)cred->CredentialBlob, cred->CredentialBlobSize);
        CredFree(cred);
        
        QJsonDocument doc = QJsonDocument::fromJson(data);
        if (doc.isObject()) {
            QJsonObject obj = doc.object();
            tokens.accessToken = obj["access_token"].toString();
            tokens.refreshToken = obj["refresh_token"].toString();
            tokens.idToken = obj["id_token"].toString();
            tokens.expiresIn = obj["expires_in"].toInt();
            tokens.tokenType = obj["token_type"].toString();
            tokens.scope = obj["scope"].toString();
            
            qDebug() << "저장된 토큰 로드됨";
        }
    }
#else
    QString configDir = QStandardPaths::writableLocation(QStandardPaths::AppConfigLocation);
    QFile file(configDir + "/oidc_tokens.json");
    if (file.open(QIODevice::ReadOnly)) {
        QByteArray data = file.readAll();
        QJsonDocument doc = QJsonDocument::fromJson(data);
        if (doc.isObject()) {
            QJsonObject obj = doc.object();
            tokens.accessToken = obj["access_token"].toString();
            tokens.refreshToken = obj["refresh_token"].toString();
            tokens.idToken = obj["id_token"].toString();
            tokens.expiresIn = obj["expires_in"].toInt();
            tokens.tokenType = obj["token_type"].toString();
            tokens.scope = obj["scope"].toString();
            
            qDebug() << "로컬 파일에서 토큰 로드됨";
        }
    }
#endif
    
    return tokens;
}

void OidcAuthenticator::clearStoredTokens()
{
#ifdef Q_OS_WIN
    if (CredDeleteW((LPWSTR)CREDENTIAL_SERVICE_NAME.utf16(), CRED_TYPE_GENERIC, 0)) {
        qDebug() << "저장된 토큰 삭제됨";
    }
#else
    QString configDir = QStandardPaths::writableLocation(QStandardPaths::AppConfigLocation);
    QFile file(configDir + "/oidc_tokens.json");
    if (file.exists()) {
        file.remove();
        qDebug() << "로컬 토큰 파일 삭제됨";
    }
#endif
}

void OidcAuthenticator::cleanupWithGuard()
{
    qDebug() << " [THREAD-SAFE] cleanupWithGuard 호출 - Thread ID:" << QThread::currentThreadId();
    
    // 이미 정리 중이면 무시
    if (m_isCleaningUp.exchange(true)) {
        qDebug() << " [THREAD-SAFE] 이미 정리 중 - 중복 호출 방지";
        return;
    }
    
    // 정리 작업 수행
    try {
        stopLoopbackServer();
        qDebug() << " [THREAD-SAFE] cleanupWithGuard 완료";
    } catch (const std::exception& e) {
        qDebug() << " [THREAD-SAFE] cleanupWithGuard 예외:" << e.what();
    } catch (...) {
        qDebug() << " [THREAD-SAFE] cleanupWithGuard 알 수 없는 예외";
    }
    
    // 정리 상태 리셋
    m_isCleaningUp = false;
}

} // namespace Blokus