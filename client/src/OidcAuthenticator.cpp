#include "OidcAuthenticator.h"
#include <QDesktopServices>
#include <QRandomGenerator>
#include <QHttpPart>
#include <QNetworkRequest>
#include <QStandardPaths>
#include <QDir>
#include <QDebug>
#include <QApplication>

#ifdef Q_OS_WIN
#include <windows.h>
#include <wincred.h>
#endif

namespace Blokus {

const QString OidcAuthenticator::CREDENTIAL_SERVICE_NAME = "BlokusOnline_OIDC";
const QString OidcAuthenticator::CREDENTIAL_USERNAME = "oauth_tokens";

OidcAuthenticator::OidcAuthenticator(QObject* parent)
    : QObject(parent)
    , m_networkManager(new QNetworkAccessManager(this))
    , m_loopbackServer(new QTcpServer(this))
    , m_currentSocket(nullptr)
    , m_loopbackPort(0)
    , m_authTimeoutTimer(new QTimer(this))
{
    // 기본 OIDC 설정: 빌드 모드에 따른 하드코딩된 값 사용
    #ifdef _DEBUG
        // Debug 모드: localhost 사용
        m_config.issuer = "http://localhost:9000";
        m_config.authorizationEndpoint = "http://localhost:9000/authorize";
        m_config.tokenEndpoint = "http://localhost:9000/token";
        qDebug() << QString::fromUtf8("🔧 디버그 모드: localhost OIDC 서버 사용");
    #else
        // Release 모드: 프로덕션 서버 사용 (Nginx를 통한 HTTPS 9000 포트)
        m_config.issuer = "https://blokus-online.mooo.com:9000";
        m_config.authorizationEndpoint = "https://blokus-online.mooo.com:9000/authorize";
        m_config.tokenEndpoint = "https://blokus-online.mooo.com:9000/token";
        qDebug() << QString::fromUtf8("🚀 릴리즈 모드: 프로덕션 OIDC 서버 사용 (https://blokus-online.mooo.com:9000)");
    #endif
    m_config.clientId = "blokus-desktop-client";
    m_config.redirectUri = "http://localhost:{PORT}/callback"; // PORT는 동적으로 설정
    m_config.scopes = QStringList({"openid", "profile", "email"});

    // 타이머 설정
    m_authTimeoutTimer->setSingleShot(true);
    m_authTimeoutTimer->setInterval(AUTH_TIMEOUT_MS);

    // 시그널 연결
    connect(m_loopbackServer, &QTcpServer::newConnection, this, &OidcAuthenticator::onLoopbackServerNewConnection);
    connect(m_authTimeoutTimer, &QTimer::timeout, this, [this]() {
        stopLoopbackServer();
        emit authenticationFailed("인증 시간이 초과되었습니다.");
    });

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
    qDebug() << "OIDC 인증 플로우 시작";

    // 로컬 서버 시작
    if (!startLoopbackServer()) {
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
    connect(reply, &QNetworkReply::finished, this, &OidcAuthenticator::onTokenRefreshFinished);
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
    // 임의의 포트에서 서버 시작
    if (!m_loopbackServer->listen(QHostAddress::LocalHost)) {
        qDebug() << "로컬 서버 시작 실패:" << m_loopbackServer->errorString();
        return false;
    }
    
    m_loopbackPort = m_loopbackServer->serverPort();
    qDebug() << "로컬 서버 시작됨, 포트:" << m_loopbackPort;
    return true;
}

void OidcAuthenticator::stopLoopbackServer()
{
    if (m_loopbackServer->isListening()) {
        m_loopbackServer->close();
        qDebug() << "로컬 서버 중지됨";
    }
    
    if (m_currentSocket) {
        m_currentSocket->disconnectFromHost();
        m_currentSocket = nullptr;
    }
    
    m_authTimeoutTimer->stop();
}

void OidcAuthenticator::onLoopbackServerNewConnection()
{
    m_currentSocket = m_loopbackServer->nextPendingConnection();
    connect(m_currentSocket, &QTcpSocket::readyRead, this, &OidcAuthenticator::onLoopbackSocketReadyRead);
    connect(m_currentSocket, &QTcpSocket::disconnected, m_currentSocket, &QTcpSocket::deleteLater);
}

void OidcAuthenticator::onLoopbackSocketReadyRead()
{
    QTcpSocket* socket = qobject_cast<QTcpSocket*>(sender());
    if (!socket) return;

    QByteArray data = socket->readAll();
    QString request = QString::fromUtf8(data);
    
    qDebug() << "HTTP 요청 수신:" << request.left(100);

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
    QString response = handleAuthCodeResponse(path);
    
    if (response.isEmpty()) {
        sendHttpResponse(socket, 400, "Authentication failed");
    } else {
        sendHttpResponse(socket, 200, response);
    }
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
    connect(reply, &QNetworkReply::finished, this, &OidcAuthenticator::onTokenExchangeFinished);
}

void OidcAuthenticator::onTokenExchangeFinished()
{
    QNetworkReply* reply = qobject_cast<QNetworkReply*>(sender());
    if (!reply) return;

    reply->deleteLater();
    stopLoopbackServer();

    if (reply->error() != QNetworkReply::NoError) {
        qDebug() << "토큰 교환 네트워크 오류:" << reply->errorString();
        emit authenticationFailed("토큰 교환 실패: " + reply->errorString());
        return;
    }

    QByteArray responseData = reply->readAll();
    QJsonDocument doc = QJsonDocument::fromJson(responseData);
    
    if (!doc.isObject()) {
        emit authenticationFailed("잘못된 토큰 응답 형식");
        return;
    }

    QJsonObject obj = doc.object();
    
    if (obj.contains("error")) {
        QString error = obj["error"].toString();
        QString errorDescription = obj.value("error_description").toString();
        emit authenticationFailed(QString("토큰 오류: %1 - %2").arg(error, errorDescription));
        return;
    }

    m_currentTokens = parseTokenResponse(obj);
    
    if (m_currentTokens.accessToken.isEmpty()) {
        emit authenticationFailed("Access Token을 받지 못했습니다.");
        return;
    }

    // 토큰 안전하게 저장
    saveTokensSecurely(m_currentTokens);

    qDebug() << "토큰 교환 성공";
    emit authenticationSucceeded(m_currentTokens.accessToken, m_currentTokens);
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
    
    // Windows Credential Manager에 저장
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
        qDebug() << "토큰 저장 실패";
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

} // namespace Blokus