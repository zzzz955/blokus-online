#pragma once

#include <QObject>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QTcpServer>
#include <QTcpSocket>
#include <QUrl>
#include <QUrlQuery>
#include <QJsonDocument>
#include <QJsonObject>
#include <QTimer>
#include <QCryptographicHash>
#include <memory>

namespace Blokus {

    struct OidcTokens {
        QString accessToken;
        QString refreshToken;
        QString idToken;
        int expiresIn = 0;
        QString tokenType = "Bearer";
        QString scope;
    };

    struct OidcConfig {
        QString authorizationEndpoint;
        QString tokenEndpoint;
        QString issuer;
        QString clientId;
        QString redirectUri;
        QStringList scopes;
    };

    class OidcAuthenticator : public QObject
    {
        Q_OBJECT

    public:
        explicit OidcAuthenticator(QObject* parent = nullptr);
        ~OidcAuthenticator();

        // OIDC 인증 플로우 시작
        void startAuthenticationFlow();
        
        // 저장된 토큰으로 자동 로그인 시도
        void tryAutoLogin();
        
        // 토큰 새로고침
        void refreshTokens();
        
        // 로그아웃 (토큰 삭제)
        void logout();
        
        // 현재 Access Token 반환
        QString getCurrentAccessToken() const;
        
        // 토큰 유효성 확인
        bool hasValidTokens() const;
        
        // OIDC 설정
        void setConfig(const OidcConfig& config);

    signals:
        // 인증 성공 (JWT Access Token 반환)
        void authenticationSucceeded(const QString& accessToken, const OidcTokens& tokens);
        
        // 인증 실패
        void authenticationFailed(const QString& error);
        
        // 토큰 새로고침 성공
        void tokensRefreshed(const QString& accessToken);
        
        // 토큰 새로고침 실패  
        void tokenRefreshFailed(const QString& error);

    private slots:
        void onLoopbackServerNewConnection();
        void onLoopbackSocketReadyRead();
        void onTokenExchangeFinished();
        void onTokenRefreshFinished();

    private:
        // PKCE 관련
        QString generateCodeVerifier();
        QString generateCodeChallenge(const QString& verifier);
        QString generateRandomString(int length);
        
        // 인증 URL 생성
        QUrl buildAuthorizationUrl();
        
        // 로컬 HTTP 서버 관리
        bool startLoopbackServer();
        void stopLoopbackServer();
        QString handleAuthCodeResponse(const QString& requestPath);
        
        // 토큰 교환
        void exchangeCodeForTokens(const QString& authCode);
        
        // 토큰 저장/로드
        void saveTokensSecurely(const OidcTokens& tokens);
        OidcTokens loadTokensSecurely();
        void clearStoredTokens();
        
        // HTTP 응답 처리
        void sendHttpResponse(QTcpSocket* socket, int statusCode, const QString& body);
        
        // JSON 파싱
        OidcTokens parseTokenResponse(const QJsonObject& json);

    private:
        OidcConfig m_config;
        QNetworkAccessManager* m_networkManager;
        QTcpServer* m_loopbackServer;
        QTcpSocket* m_currentSocket;
        
        // PKCE parameters
        QString m_codeVerifier;
        QString m_codeChallenge;
        QString m_state;
        
        // Token storage
        OidcTokens m_currentTokens;
        
        // Server settings
        quint16 m_loopbackPort = 0;
        QTimer* m_authTimeoutTimer;
        
        static const int AUTH_TIMEOUT_MS = 300000; // 5분
        static const QString CREDENTIAL_SERVICE_NAME;
        static const QString CREDENTIAL_USERNAME;
    };

} // namespace Blokus