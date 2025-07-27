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
#include "ResponsiveUI.h"

namespace Blokus {

    class LoginWindow : public QMainWindow
    {
        Q_OBJECT

    public:
        explicit LoginWindow(QWidget* parent = nullptr);
        ~LoginWindow();

        // �α��� ��� ó��
        void setLoginResult(bool success, const QString& message);
        void setRegisterResult(bool success, const QString& message);
        void setPasswordResetResult(bool success, const QString& message);

    signals:
        // �α��� ���� �ñ׳�
        void loginRequested(const QString& username, const QString& password);
        void registerRequested(const QString& username, const QString& password, const QString& email);
        void passwordResetRequested(const QString& email);
        void loginSuccessful(const QString& username);

    private slots:
        // UI �̺�Ʈ �ڵ鷯
        void onLoginClicked();
        void onRegisterClicked();
        void onPasswordResetClicked();
        void onBackToLoginClicked();
        void onShowRegisterForm();
        void onShowPasswordResetForm();

        // �Է� ����
        void onUsernameTextChanged();
        void onPasswordTextChanged();
        void onEmailTextChanged();

        // �ε� �ִϸ��̼�
        void updateLoadingAnimation();

    protected:
        // �̺�Ʈ �ڵ鷯
        void keyPressEvent(QKeyEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;

    private:
        // UI ���� �Լ���
        void setupUI();
        void setupTitleArea();
        void setupLoginForm();
        void setupRegisterForm();
        void setupPasswordResetForm();
        void setupLoadingWidget();
        void setupStyles();
        void createAnimations();

        // �� ��ȯ
        void showLoginForm();
        void showRegisterForm();
        void showPasswordResetForm();

        // ��ƿ��Ƽ �Լ�
        void clearInputs();
        void setFormEnabled(bool enabled);
        void showLoadingState(bool loading);
        bool validateLoginInput();
        bool validateRegisterInput();
        bool validatePasswordResetInput();
        void showMessage(const QString& title, const QString& message, bool isError = false);

        // ��Ÿ�� ����
        void updateFormStyles();

    private:
        // ���� ������
        QWidget* m_centralWidget;
        QVBoxLayout* m_mainLayout;

        // Ÿ��Ʋ ����
        QWidget* m_titleWidget;
        QLabel* m_titleLabel;
        QLabel* m_subtitleLabel;

        // �� �����̳�
        QWidget* m_formContainer;
        QVBoxLayout* m_formLayout;

        // �α��� ��
        QWidget* m_loginForm;
        QLineEdit* m_usernameEdit;
        QLineEdit* m_passwordEdit;
        QPushButton* m_loginButton;
        QPushButton* m_showRegisterButton;
        QPushButton* m_showPasswordResetButton;

        // ȸ������ ��
        QWidget* m_registerForm;
        QLineEdit* m_regUsernameEdit;
        QLineEdit* m_regPasswordEdit;
        QLineEdit* m_regConfirmPasswordEdit;
        QLineEdit* m_regEmailEdit;
        QPushButton* m_registerButton;
        QPushButton* m_backToLoginFromRegisterButton;

        // ��й�ȣ �缳�� ��
        QWidget* m_passwordResetForm;
        QLineEdit* m_resetEmailEdit;
        QPushButton* m_passwordResetButton;
        QPushButton* m_backToLoginFromResetButton;

        // �ε� �� ���� ǥ��
        QWidget* m_loadingWidget;
        QProgressBar* m_progressBar;
        QLabel* m_loadingLabel;
        QMovie* m_loadingMovie;

        // ����
        enum class FormState {
            Login,
            Register,
            PasswordReset
        };
        FormState m_currentForm;
        bool m_isLoading;

        // Ÿ�̸�
        QTimer* m_animationTimer;
    };

} // namespace Blokus