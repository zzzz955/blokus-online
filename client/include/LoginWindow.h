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

namespace Blokus {

    class LoginWindow : public QMainWindow
    {
        Q_OBJECT

    public:
        explicit LoginWindow(QWidget* parent = nullptr);
        ~LoginWindow();

        // 로그인 결과 처리
        void setLoginResult(bool success, const QString& message);
        void setRegisterResult(bool success, const QString& message);
        void setPasswordResetResult(bool success, const QString& message);

    signals:
        // 로그인 관련 시그널
        void loginRequested(const QString& username, const QString& password);
        void registerRequested(const QString& username, const QString& password, const QString& email);
        void passwordResetRequested(const QString& email);
        void loginSuccessful(const QString& username);

    private slots:
        // UI 이벤트 핸들러
        void onLoginClicked();
        void onRegisterClicked();
        void onPasswordResetClicked();
        void onBackToLoginClicked();
        void onShowRegisterForm();
        void onShowPasswordResetForm();

        // 입력 검증
        void onUsernameTextChanged();
        void onPasswordTextChanged();
        void onEmailTextChanged();

        // 로딩 애니메이션
        void updateLoadingAnimation();

    protected:
        // 이벤트 핸들러
        void keyPressEvent(QKeyEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;

    private:
        // UI 구성 함수들
        void setupUI();
        void setupTitleArea();
        void setupLoginForm();
        void setupRegisterForm();
        void setupPasswordResetForm();
        void setupLoadingWidget();
        void setupStyles();
        void createAnimations();

        // 폼 전환
        void showLoginForm();
        void showRegisterForm();
        void showPasswordResetForm();

        // 유틸리티 함수
        void clearInputs();
        void setFormEnabled(bool enabled);
        void showLoadingState(bool loading);
        bool validateLoginInput();
        bool validateRegisterInput();
        bool validatePasswordResetInput();
        void showMessage(const QString& title, const QString& message, bool isError = false);

        // 스타일 관련
        void updateFormStyles();
        QString getButtonStyle(const QString& baseColor, const QString& hoverColor) const;
        QString getInputStyle() const;

    private:
        // 메인 위젯들
        QWidget* m_centralWidget;
        QVBoxLayout* m_mainLayout;

        // 타이틀 영역
        QWidget* m_titleWidget;
        QLabel* m_titleLabel;
        QLabel* m_subtitleLabel;

        // 폼 컨테이너
        QWidget* m_formContainer;
        QVBoxLayout* m_formLayout;

        // 로그인 폼
        QWidget* m_loginForm;
        QLineEdit* m_usernameEdit;
        QLineEdit* m_passwordEdit;
        QPushButton* m_loginButton;
        QPushButton* m_showRegisterButton;
        QPushButton* m_showPasswordResetButton;

        // 회원가입 폼
        QWidget* m_registerForm;
        QLineEdit* m_regUsernameEdit;
        QLineEdit* m_regPasswordEdit;
        QLineEdit* m_regConfirmPasswordEdit;
        QLineEdit* m_regEmailEdit;
        QPushButton* m_registerButton;
        QPushButton* m_backToLoginFromRegisterButton;

        // 비밀번호 재설정 폼
        QWidget* m_passwordResetForm;
        QLineEdit* m_resetEmailEdit;
        QPushButton* m_passwordResetButton;
        QPushButton* m_backToLoginFromResetButton;

        // 로딩 및 상태 표시
        QWidget* m_loadingWidget;
        QProgressBar* m_progressBar;
        QLabel* m_loadingLabel;
        QMovie* m_loadingMovie;

        // 상태
        enum class FormState {
            Login,
            Register,
            PasswordReset
        };
        FormState m_currentForm;
        bool m_isLoading;

        // 타이머
        QTimer* m_animationTimer;
    };

} // namespace Blokus