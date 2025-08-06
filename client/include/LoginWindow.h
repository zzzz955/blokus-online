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

namespace Blokus {

    class LoginWindow : public QMainWindow
    {
        Q_OBJECT

    public:
        explicit LoginWindow(QWidget* parent = nullptr);
        ~LoginWindow();

        // �α��� ��� ó��
        void setLoginResult(bool success, const QString& message);

    signals:
        // �α��� ���� �ñ׳�
        void loginRequested(const QString& username, const QString& password);
        void loginSuccessful(const QString& username);

    private slots:
        // UI �̺�Ʈ �ڵ鷯
        void onLoginClicked();
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
        void setupLoadingWidget();
        void setupStyles();
        void createAnimations();
        
        // URL ���� �Լ�
        QString getAuthUrl() const;

        // ��ƿ��Ƽ �Լ�
        void clearInputs();
        void setFormEnabled(bool enabled);
        void showLoadingState(bool loading);
        bool validateLoginInput();
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
        QSvgWidget* m_titleSvgWidget;

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

        // �ε� �� ���� ǥ��
        QWidget* m_loadingWidget;
        QProgressBar* m_progressBar;
        QLabel* m_loadingLabel;
        QMovie* m_loadingMovie;

        // ����
        bool m_isLoading;

        // Ÿ�̸�
        QTimer* m_animationTimer;
    };

} // namespace Blokus