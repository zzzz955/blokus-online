#pragma once

#include <QMainWindow>
#include <QWidget>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QGridLayout>
#include <QLabel>
#include <QLineEdit>
#include <QPushButton>
#include <QFrame>
#include <QProgressBar>
#include <QMovie>
#include <QTimer>
#include <QKeyEvent>
#include <QMessageBox>
#include <QSvgWidget>
#include <QPixmap>
#include <QDesktopServices>
#include <QUrl>
#include "ResponsiveUI.h"
#include "OidcAuthenticator.h"

namespace Blokus {

    class LoginWindow : public QMainWindow
    {
        Q_OBJECT

    public:
        explicit LoginWindow(QWidget* parent = nullptr);
        ~LoginWindow();

        // 로그인 결과 처리
        void setLoginResult(bool success, const QString& message);

    signals:
        // 로그인 성공 시그널
        void loginRequested(const QString& username, const QString& password);
        void jwtLoginRequested(const QString& jwtToken);
        void loginSuccessful(const QString& username);

    private slots:
        // UI 이벤트 핸들러
        void onLoginClicked();
        void onOidcLoginClicked();
        void onShowRegisterForm();
        void onShowPasswordResetForm();

        // 입력 검증
        void onUsernameTextChanged();
        void onPasswordTextChanged();
        void onEmailTextChanged();

        // OIDC 이벤트 핸들러
        void onOidcAuthenticationSucceeded(const QString& accessToken, const OidcTokens& tokens);
        void onOidcAuthenticationFailed(const QString& error);
        void onOidcTokensRefreshed(const QString& accessToken);

        // 로딩 애니메이션
        void updateLoadingAnimation();

    protected:
        // 이벤트 핸들러
        void keyPressEvent(QKeyEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;

    private:
        // UI 설정 함수들
        void setupUI();
        void setupTitleArea();
        void setupLoginForm();
        void setupLoadingWidget();
        void setupStyles();
        void createAnimations();
        
        // URL 생성 함수
        QString getAuthUrl() const;

        // 유틸리티 함수
        void clearInputs();
        void setFormEnabled(bool enabled);
        void showLoadingState(bool loading);
        bool validateLoginInput();
        void showMessage(const QString& title, const QString& message, bool isError = false);

        // 스타일 관련
        void updateFormStyles();

    private:
        // 메인 컨테이너
        QWidget* m_centralWidget;
        QVBoxLayout* m_mainLayout;

        // 타이틀 영역
        QWidget* m_titleWidget;
        QLabel* m_titleLabel;
        QLabel* m_subtitleLabel;
        QSvgWidget* m_titleSvgWidget;

        // 폼 컨테이너
        QWidget* m_formContainer;
        QVBoxLayout* m_formLayout;

        // 로그인 폼
        QWidget* m_loginForm;
        QLineEdit* m_usernameEdit;
        QLineEdit* m_passwordEdit;
        QPushButton* m_loginButton;
        QPushButton* m_oidcLoginButton;
        QPushButton* m_showRegisterButton;
        QPushButton* m_showPasswordResetButton;

        // 로딩 및 상태 표시
        QWidget* m_loadingWidget;
        QProgressBar* m_progressBar;
        QLabel* m_loadingLabel;
        QMovie* m_loadingMovie;

        // 상태
        bool m_isLoading;

        // OIDC 상태
        OidcAuthenticator* m_oidcAuthenticator;

        // 타이머
        QTimer* m_animationTimer;
    };

} // namespace Blokus