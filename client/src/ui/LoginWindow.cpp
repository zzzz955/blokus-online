#include "ui/LoginWindow.h"
#include <QApplication>
#include <QDesktopWidget>
#include <QPropertyAnimation>
#include <QGraphicsOpacityEffect>
#include <QRegularExpression>
#include <QRegularExpressionValidator>
#include <QTimer>

namespace Blokus {

    LoginWindow::LoginWindow(QWidget* parent)
        : QMainWindow(parent)
        , m_centralWidget(nullptr)
        , m_mainLayout(nullptr)
        , m_titleWidget(nullptr)
        , m_titleLabel(nullptr)
        , m_subtitleLabel(nullptr)
        , m_formContainer(nullptr)
        , m_formLayout(nullptr)
        , m_loginForm(nullptr)
        , m_usernameEdit(nullptr)
        , m_passwordEdit(nullptr)
        , m_loginButton(nullptr)
        , m_showRegisterButton(nullptr)
        , m_showPasswordResetButton(nullptr)
        , m_registerForm(nullptr)
        , m_regUsernameEdit(nullptr)
        , m_regPasswordEdit(nullptr)
        , m_regConfirmPasswordEdit(nullptr)
        , m_regEmailEdit(nullptr)
        , m_registerButton(nullptr)
        , m_backToLoginFromRegisterButton(nullptr)
        , m_passwordResetForm(nullptr)
        , m_resetEmailEdit(nullptr)
        , m_passwordResetButton(nullptr)
        , m_backToLoginFromResetButton(nullptr)
        , m_loadingWidget(nullptr)
        , m_progressBar(nullptr)
        , m_loadingLabel(nullptr)
        , m_loadingMovie(nullptr)
        , m_currentForm(FormState::Login)
        , m_isLoading(false)
        , m_animationTimer(new QTimer(this))
    {
        setupUI();
        setupStyles();
        createAnimations();

        // 기본적으로 로그인 폼 표시
        showLoginForm();

        // 창 설정
        setWindowTitle(QString::fromUtf8("블로커스 온라인 - 로그인"));
        setMinimumSize(450, 600);
        resize(450, 600);

        // 화면 중앙에 배치
        QRect screenGeometry = QApplication::desktop()->screenGeometry();
        int x = (screenGeometry.width() - width()) / 2;
        int y = (screenGeometry.height() - height()) / 2;
        move(x, y);
    }

    LoginWindow::~LoginWindow()
    {
        if (m_loadingMovie) {
            delete m_loadingMovie;
        }
    }

    void LoginWindow::setupUI()
    {
        // 중앙 위젯 설정
        m_centralWidget = new QWidget(this);
        setCentralWidget(m_centralWidget);

        m_mainLayout = new QVBoxLayout(m_centralWidget);
        m_mainLayout->setContentsMargins(40, 30, 40, 30);
        m_mainLayout->setSpacing(20);

        // 타이틀 영역
        setupTitleArea();

        // 폼 컨테이너
        m_formContainer = new QWidget();
        m_formLayout = new QVBoxLayout(m_formContainer);
        m_formLayout->setContentsMargins(0, 0, 0, 0);
        m_formLayout->setSpacing(15);

        // 개별 폼들 설정
        setupLoginForm();
        setupRegisterForm();
        setupPasswordResetForm();

        // 로딩 위젯
        setupLoadingWidget();

        // 메인 레이아웃에 추가
        m_mainLayout->addWidget(m_titleWidget);
        m_mainLayout->addStretch(1);
        m_mainLayout->addWidget(m_formContainer);
        m_mainLayout->addStretch(2);
        m_mainLayout->addWidget(m_loadingWidget);
    }

    void LoginWindow::setupTitleArea()
    {
        m_titleWidget = new QWidget();
        QVBoxLayout* titleLayout = new QVBoxLayout(m_titleWidget);
        titleLayout->setContentsMargins(0, 0, 0, 0);
        titleLayout->setSpacing(8);

        // 메인 타이틀
        m_titleLabel = new QLabel(QString::fromUtf8("🎮 블로커스 온라인"));
        m_titleLabel->setAlignment(Qt::AlignCenter);
        m_titleLabel->setStyleSheet(
            "QLabel { "
            "font-size: 28px; "
            "font-weight: bold; "
            "color: #2c3e50; "
            "margin-bottom: 5px; "
            "}"
        );

        // 서브타이틀
        m_subtitleLabel = new QLabel(QString::fromUtf8("전략적 블록 배치 게임"));
        m_subtitleLabel->setAlignment(Qt::AlignCenter);
        m_subtitleLabel->setStyleSheet(
            "QLabel { "
            "font-size: 14px; "
            "color: #7f8c8d; "
            "margin-bottom: 10px; "
            "}"
        );

        titleLayout->addWidget(m_titleLabel);
        titleLayout->addWidget(m_subtitleLabel);
    }

    void LoginWindow::setupLoginForm()
    {
        m_loginForm = new QWidget();
        QVBoxLayout* layout = new QVBoxLayout(m_loginForm);
        layout->setContentsMargins(20, 20, 20, 20);
        layout->setSpacing(15);

        // 아이디 입력
        QLabel* usernameLabel = new QLabel(QString::fromUtf8("아이디"));
        usernameLabel->setStyleSheet("font-weight: bold; color: #34495e;");
        m_usernameEdit = new QLineEdit();
        m_usernameEdit->setPlaceholderText(QString::fromUtf8("아이디를 입력하세요"));
        m_usernameEdit->setMaxLength(20);

        // 비밀번호 입력
        QLabel* passwordLabel = new QLabel(QString::fromUtf8("비밀번호"));
        passwordLabel->setStyleSheet("font-weight: bold; color: #34495e;");
        m_passwordEdit = new QLineEdit();
        m_passwordEdit->setPlaceholderText(QString::fromUtf8("비밀번호를 입력하세요"));
        m_passwordEdit->setEchoMode(QLineEdit::Password);
        m_passwordEdit->setMaxLength(50);

        // 로그인 버튼
        m_loginButton = new QPushButton(QString::fromUtf8("🎯 로그인"));
        m_loginButton->setMinimumHeight(45);

        // 회원가입 버튼
        m_showRegisterButton = new QPushButton(QString::fromUtf8("회원가입"));
        m_showRegisterButton->setMinimumHeight(40);

        // 비밀번호 재설정 버튼
        m_showPasswordResetButton = new QPushButton(QString::fromUtf8("비밀번호를 잊으셨나요?"));
        m_showPasswordResetButton->setMinimumHeight(35);

        // 레이아웃에 추가
        layout->addWidget(usernameLabel);
        layout->addWidget(m_usernameEdit);
        layout->addWidget(passwordLabel);
        layout->addWidget(m_passwordEdit);
        layout->addSpacing(10);
        layout->addWidget(m_loginButton);
        layout->addSpacing(5);
        layout->addWidget(m_showRegisterButton);
        layout->addWidget(m_showPasswordResetButton);

        // 시그널 연결
        connect(m_usernameEdit, &QLineEdit::textChanged, this, &LoginWindow::onUsernameTextChanged);
        connect(m_passwordEdit, &QLineEdit::textChanged, this, &LoginWindow::onPasswordTextChanged);
        connect(m_usernameEdit, &QLineEdit::returnPressed, this, &LoginWindow::onLoginClicked);
        connect(m_passwordEdit, &QLineEdit::returnPressed, this, &LoginWindow::onLoginClicked);
        connect(m_loginButton, &QPushButton::clicked, this, &LoginWindow::onLoginClicked);
        connect(m_showRegisterButton, &QPushButton::clicked, this, &LoginWindow::onShowRegisterForm);
        connect(m_showPasswordResetButton, &QPushButton::clicked, this, &LoginWindow::onShowPasswordResetForm);

        m_formLayout->addWidget(m_loginForm);
    }

    void LoginWindow::setupRegisterForm()
    {
        m_registerForm = new QWidget();
        QVBoxLayout* layout = new QVBoxLayout(m_registerForm);
        layout->setContentsMargins(20, 20, 20, 20);
        layout->setSpacing(12);

        // 아이디 입력
        QLabel* usernameLabel = new QLabel(QString::fromUtf8("아이디"));
        usernameLabel->setStyleSheet("font-weight: bold; color: #34495e;");
        m_regUsernameEdit = new QLineEdit();
        m_regUsernameEdit->setPlaceholderText(QString::fromUtf8("4-20자의 영문, 숫자"));
        m_regUsernameEdit->setMaxLength(20);

        // 비밀번호 입력
        QLabel* passwordLabel = new QLabel(QString::fromUtf8("비밀번호"));
        passwordLabel->setStyleSheet("font-weight: bold; color: #34495e;");
        m_regPasswordEdit = new QLineEdit();
        m_regPasswordEdit->setPlaceholderText(QString::fromUtf8("8자 이상, 영문+숫자 조합"));
        m_regPasswordEdit->setEchoMode(QLineEdit::Password);
        m_regPasswordEdit->setMaxLength(50);

        // 비밀번호 확인
        QLabel* confirmLabel = new QLabel(QString::fromUtf8("비밀번호 확인"));
        confirmLabel->setStyleSheet("font-weight: bold; color: #34495e;");
        m_regConfirmPasswordEdit = new QLineEdit();
        m_regConfirmPasswordEdit->setPlaceholderText(QString::fromUtf8("비밀번호를 다시 입력하세요"));
        m_regConfirmPasswordEdit->setEchoMode(QLineEdit::Password);
        m_regConfirmPasswordEdit->setMaxLength(50);

        // 이메일 입력
        QLabel* emailLabel = new QLabel(QString::fromUtf8("이메일"));
        emailLabel->setStyleSheet("font-weight: bold; color: #34495e;");
        m_regEmailEdit = new QLineEdit();
        m_regEmailEdit->setPlaceholderText(QString::fromUtf8("example@domain.com"));
        m_regEmailEdit->setMaxLength(100);

        // 버튼들
        m_registerButton = new QPushButton(QString::fromUtf8("✨ 회원가입"));
        m_registerButton->setMinimumHeight(45);

        m_backToLoginFromRegisterButton = new QPushButton(QString::fromUtf8("로그인으로 돌아가기"));
        m_backToLoginFromRegisterButton->setMinimumHeight(40);

        // 레이아웃에 추가
        layout->addWidget(usernameLabel);
        layout->addWidget(m_regUsernameEdit);
        layout->addWidget(passwordLabel);
        layout->addWidget(m_regPasswordEdit);
        layout->addWidget(confirmLabel);
        layout->addWidget(m_regConfirmPasswordEdit);
        layout->addWidget(emailLabel);
        layout->addWidget(m_regEmailEdit);
        layout->addSpacing(10);
        layout->addWidget(m_registerButton);
        layout->addSpacing(5);
        layout->addWidget(m_backToLoginFromRegisterButton);

        // 시그널 연결
        connect(m_regUsernameEdit, &QLineEdit::textChanged, this, &LoginWindow::onUsernameTextChanged);
        connect(m_regPasswordEdit, &QLineEdit::textChanged, this, &LoginWindow::onPasswordTextChanged);
        connect(m_regEmailEdit, &QLineEdit::textChanged, this, &LoginWindow::onEmailTextChanged);
        connect(m_registerButton, &QPushButton::clicked, this, &LoginWindow::onRegisterClicked);
        connect(m_backToLoginFromRegisterButton, &QPushButton::clicked, this, &LoginWindow::onBackToLoginClicked);

        m_formLayout->addWidget(m_registerForm);
        m_registerForm->hide(); // 초기에는 숨김
    }

    void LoginWindow::setupPasswordResetForm()
    {
        m_passwordResetForm = new QWidget();
        QVBoxLayout* layout = new QVBoxLayout(m_passwordResetForm);
        layout->setContentsMargins(20, 20, 20, 20);
        layout->setSpacing(15);

        // 설명 라벨
        QLabel* descLabel = new QLabel(QString::fromUtf8("가입 시 사용한 이메일 주소를 입력하시면\n비밀번호 재설정 링크를 보내드립니다."));
        descLabel->setAlignment(Qt::AlignCenter);
        descLabel->setStyleSheet("color: #7f8c8d; font-size: 13px; margin-bottom: 10px;");
        descLabel->setWordWrap(true);

        // 이메일 입력
        QLabel* emailLabel = new QLabel(QString::fromUtf8("이메일"));
        emailLabel->setStyleSheet("font-weight: bold; color: #34495e;");
        m_resetEmailEdit = new QLineEdit();
        m_resetEmailEdit->setPlaceholderText(QString::fromUtf8("example@domain.com"));
        m_resetEmailEdit->setMaxLength(100);

        // 버튼들
        m_passwordResetButton = new QPushButton(QString::fromUtf8("📧 재설정 링크 전송"));
        m_passwordResetButton->setMinimumHeight(45);

        m_backToLoginFromResetButton = new QPushButton(QString::fromUtf8("로그인으로 돌아가기"));
        m_backToLoginFromResetButton->setMinimumHeight(40);

        // 레이아웃에 추가
        layout->addWidget(descLabel);
        layout->addSpacing(10);
        layout->addWidget(emailLabel);
        layout->addWidget(m_resetEmailEdit);
        layout->addSpacing(15);
        layout->addWidget(m_passwordResetButton);
        layout->addSpacing(5);
        layout->addWidget(m_backToLoginFromResetButton);

        // 시그널 연결
        connect(m_resetEmailEdit, &QLineEdit::textChanged, this, &LoginWindow::onEmailTextChanged);
        connect(m_resetEmailEdit, &QLineEdit::returnPressed, this, &LoginWindow::onPasswordResetClicked);
        connect(m_passwordResetButton, &QPushButton::clicked, this, &LoginWindow::onPasswordResetClicked);
        connect(m_backToLoginFromResetButton, &QPushButton::clicked, this, &LoginWindow::onBackToLoginClicked);

        m_formLayout->addWidget(m_passwordResetForm);
        m_passwordResetForm->hide(); // 초기에는 숨김
    }

    void LoginWindow::setupLoadingWidget()
    {
        m_loadingWidget = new QWidget();
        QVBoxLayout* layout = new QVBoxLayout(m_loadingWidget);
        layout->setContentsMargins(0, 0, 0, 0);
        layout->setSpacing(10);

        m_progressBar = new QProgressBar();
        m_progressBar->setRange(0, 0); // 무한 로딩
        m_progressBar->setMinimumHeight(8);
        m_progressBar->setMaximumHeight(8);

        m_loadingLabel = new QLabel(QString::fromUtf8("서버에 연결 중..."));
        m_loadingLabel->setAlignment(Qt::AlignCenter);
        m_loadingLabel->setStyleSheet("color: #3498db; font-size: 12px;");

        layout->addWidget(m_progressBar);
        layout->addWidget(m_loadingLabel);

        m_loadingWidget->hide(); // 초기에는 숨김
    }

    void LoginWindow::setupStyles()
    {
        // 메인 윈도우 배경
        setStyleSheet(
            "QMainWindow { "
            "background: qlineargradient(x1:0, y1:0, x2:0, y2:1, "
            "stop:0 #ecf0f1, stop:1 #bdc3c7); "
            "}"
        );

        // 폼 컨테이너 스타일
        m_formContainer->setStyleSheet(
            "QWidget { "
            "background-color: white; "
            "border-radius: 12px; "
            "border: 1px solid #ddd; "
            "}"
        );

        updateFormStyles();
    }

    void LoginWindow::updateFormStyles()
    {
        QString inputStyle = getInputStyle();

        // 로그인 폼 스타일
        if (m_usernameEdit) m_usernameEdit->setStyleSheet(inputStyle);
        if (m_passwordEdit) m_passwordEdit->setStyleSheet(inputStyle);
        if (m_loginButton) m_loginButton->setStyleSheet(getButtonStyle("#3498db", "#2980b9"));
        if (m_showRegisterButton) m_showRegisterButton->setStyleSheet(getButtonStyle("#27ae60", "#229954"));
        if (m_showPasswordResetButton) m_showPasswordResetButton->setStyleSheet(getButtonStyle("#95a5a6", "#7f8c8d"));

        // 회원가입 폼 스타일
        if (m_regUsernameEdit) m_regUsernameEdit->setStyleSheet(inputStyle);
        if (m_regPasswordEdit) m_regPasswordEdit->setStyleSheet(inputStyle);
        if (m_regConfirmPasswordEdit) m_regConfirmPasswordEdit->setStyleSheet(inputStyle);
        if (m_regEmailEdit) m_regEmailEdit->setStyleSheet(inputStyle);
        if (m_registerButton) m_registerButton->setStyleSheet(getButtonStyle("#e74c3c", "#c0392b"));
        if (m_backToLoginFromRegisterButton) m_backToLoginFromRegisterButton->setStyleSheet(getButtonStyle("#95a5a6", "#7f8c8d"));

        // 비밀번호 재설정 폼 스타일
        if (m_resetEmailEdit) m_resetEmailEdit->setStyleSheet(inputStyle);
        if (m_passwordResetButton) m_passwordResetButton->setStyleSheet(getButtonStyle("#f39c12", "#e67e22"));
        if (m_backToLoginFromResetButton) m_backToLoginFromResetButton->setStyleSheet(getButtonStyle("#95a5a6", "#7f8c8d"));

        // 프로그레스 바 스타일
        if (m_progressBar) {
            m_progressBar->setStyleSheet(
                "QProgressBar { "
                "border: none; "
                "border-radius: 4px; "
                "background-color: #ecf0f1; "
                "} "
                "QProgressBar::chunk { "
                "background-color: #3498db; "
                "border-radius: 4px; "
                "}"
            );
        }
    }

    QString LoginWindow::getButtonStyle(const QString& baseColor, const QString& hoverColor) const
    {
        return QString(
            "QPushButton { "
            "background-color: %1; "
            "border: none; "
            "border-radius: 6px; "
            "color: white; "
            "font-weight: bold; "
            "font-size: 14px; "
            "padding: 8px; "
            "} "
            "QPushButton:hover { "
            "background-color: %2; "
            "} "
            "QPushButton:pressed { "
            "background-color: %2; "
            "transform: translateY(1px); "
            "} "
            "QPushButton:disabled { "
            "background-color: #bdc3c7; "
            "color: #7f8c8d; "
            "}"
        ).arg(baseColor, hoverColor);
    }

    QString LoginWindow::getInputStyle() const
    {
        return QString(
            "QLineEdit { "
            "border: 2px solid #ddd; "
            "border-radius: 6px; "
            "padding: 8px 12px; "
            "font-size: 14px; "
            "background-color: #fafafa; "
            "} "
            "QLineEdit:focus { "
            "border-color: #3498db; "
            "background-color: white; "
            "} "
            "QLineEdit:hover { "
            "border-color: #bbb; "
            "}"
        );
    }

    void LoginWindow::createAnimations()
    {
        // 애니메이션 타이머 설정
        m_animationTimer->setInterval(50);
        connect(m_animationTimer, &QTimer::timeout, this, &LoginWindow::updateLoadingAnimation);
    }

    void LoginWindow::showLoginForm()
    {
        m_currentForm = FormState::Login;
        m_loginForm->show();
        m_registerForm->hide();
        m_passwordResetForm->hide();

        if (m_usernameEdit) {
            m_usernameEdit->setFocus();
        }
    }

    void LoginWindow::showRegisterForm()
    {
        m_currentForm = FormState::Register;
        m_loginForm->hide();
        m_registerForm->show();
        m_passwordResetForm->hide();

        if (m_regUsernameEdit) {
            m_regUsernameEdit->setFocus();
        }
    }

    void LoginWindow::showPasswordResetForm()
    {
        m_currentForm = FormState::PasswordReset;
        m_loginForm->hide();
        m_registerForm->hide();
        m_passwordResetForm->show();

        if (m_resetEmailEdit) {
            m_resetEmailEdit->setFocus();
        }
    }

    // 이벤트 핸들러들
    void LoginWindow::onLoginClicked()
    {
        if (!validateLoginInput()) return;

        QString username = m_usernameEdit->text().trimmed();
        QString password = m_passwordEdit->text();

        showLoadingState(true);
        emit loginRequested(username, password);
    }

    void LoginWindow::onRegisterClicked()
    {
        if (!validateRegisterInput()) return;

        QString username = m_regUsernameEdit->text().trimmed();
        QString password = m_regPasswordEdit->text();
        QString email = m_regEmailEdit->text().trimmed();

        showLoadingState(true);
        emit registerRequested(username, password, email);
    }

    void LoginWindow::onPasswordResetClicked()
    {
        if (!validatePasswordResetInput()) return;

        QString email = m_resetEmailEdit->text().trimmed();

        showLoadingState(true);
        emit passwordResetRequested(email);
    }

    void LoginWindow::onBackToLoginClicked()
    {
        clearInputs();
        showLoginForm();
    }

    void LoginWindow::onShowRegisterForm()
    {
        clearInputs();
        showRegisterForm();
    }

    void LoginWindow::onShowPasswordResetForm()
    {
        clearInputs();
        showPasswordResetForm();
    }

    void LoginWindow::onUsernameTextChanged()
    {
        // 실시간 입력 검증 (나중에 구현)
    }

    void LoginWindow::onPasswordTextChanged()
    {
        // 실시간 입력 검증 (나중에 구현)
    }

    void LoginWindow::onEmailTextChanged()
    {
        // 실시간 입력 검증 (나중에 구현)
    }

    void LoginWindow::updateLoadingAnimation()
    {
        // 로딩 애니메이션 업데이트 (나중에 구현)
    }

    // 키보드 이벤트 처리
    void LoginWindow::keyPressEvent(QKeyEvent* event)
    {
        if (event->key() == Qt::Key_Escape) {
            if (m_currentForm != FormState::Login) {
                onBackToLoginClicked();
                return;
            }
        }

        QMainWindow::keyPressEvent(event);
    }

    void LoginWindow::resizeEvent(QResizeEvent* event)
    {
        QMainWindow::resizeEvent(event);
        // 크기 변경 시 필요한 조정 작업
    }

    // 결과 처리 함수들
    void LoginWindow::setLoginResult(bool success, const QString& message)
    {
        showLoadingState(false);

        if (success) {
            showMessage(QString::fromUtf8("로그인 성공"), message, false);
            emit loginSuccessful(m_usernameEdit->text().trimmed());
        }
        else {
            showMessage(QString::fromUtf8("로그인 실패"), message, true);
        }
    }

    void LoginWindow::setRegisterResult(bool success, const QString& message)
    {
        showLoadingState(false);

        if (success) {
            showMessage(QString::fromUtf8("회원가입 성공"),
                QString::fromUtf8("회원가입이 완료되었습니다!\n이메일 인증 후 로그인해주세요."), false);
            showLoginForm();
            clearInputs();
        }
        else {
            showMessage(QString::fromUtf8("회원가입 실패"), message, true);
        }
    }

    void LoginWindow::setPasswordResetResult(bool success, const QString& message)
    {
        showLoadingState(false);

        if (success) {
            showMessage(QString::fromUtf8("이메일 전송 완료"),
                QString::fromUtf8("비밀번호 재설정 링크를 이메일로 보내드렸습니다.\n메일함을 확인해주세요."), false);
            showLoginForm();
            clearInputs();
        }
        else {
            showMessage(QString::fromUtf8("이메일 전송 실패"), message, true);
        }
    }

    // 유틸리티 함수들
    void LoginWindow::clearInputs()
    {
        // 로그인 폼 입력 초기화
        if (m_usernameEdit) m_usernameEdit->clear();
        if (m_passwordEdit) m_passwordEdit->clear();

        // 회원가입 폼 입력 초기화
        if (m_regUsernameEdit) m_regUsernameEdit->clear();
        if (m_regPasswordEdit) m_regPasswordEdit->clear();
        if (m_regConfirmPasswordEdit) m_regConfirmPasswordEdit->clear();
        if (m_regEmailEdit) m_regEmailEdit->clear();

        // 비밀번호 재설정 폼 입력 초기화
        if (m_resetEmailEdit) m_resetEmailEdit->clear();
    }

    void LoginWindow::setFormEnabled(bool enabled)
    {
        // 로그인 폼
        if (m_usernameEdit) m_usernameEdit->setEnabled(enabled);
        if (m_passwordEdit) m_passwordEdit->setEnabled(enabled);
        if (m_loginButton) m_loginButton->setEnabled(enabled);
        if (m_showRegisterButton) m_showRegisterButton->setEnabled(enabled);
        if (m_showPasswordResetButton) m_showPasswordResetButton->setEnabled(enabled);

        // 회원가입 폼
        if (m_regUsernameEdit) m_regUsernameEdit->setEnabled(enabled);
        if (m_regPasswordEdit) m_regPasswordEdit->setEnabled(enabled);
        if (m_regConfirmPasswordEdit) m_regConfirmPasswordEdit->setEnabled(enabled);
        if (m_regEmailEdit) m_regEmailEdit->setEnabled(enabled);
        if (m_registerButton) m_registerButton->setEnabled(enabled);
        if (m_backToLoginFromRegisterButton) m_backToLoginFromRegisterButton->setEnabled(enabled);

        // 비밀번호 재설정 폼
        if (m_resetEmailEdit) m_resetEmailEdit->setEnabled(enabled);
        if (m_passwordResetButton) m_passwordResetButton->setEnabled(enabled);
        if (m_backToLoginFromResetButton) m_backToLoginFromResetButton->setEnabled(enabled);
    }

    void LoginWindow::showLoadingState(bool loading)
    {
        m_isLoading = loading;

        if (loading) {
            setFormEnabled(false);
            m_loadingWidget->show();
            m_animationTimer->start();
        }
        else {
            setFormEnabled(true);
            m_loadingWidget->hide();
            m_animationTimer->stop();
        }
    }

    bool LoginWindow::validateLoginInput()
    {
        QString username = m_usernameEdit->text().trimmed();
        QString password = m_passwordEdit->text();

        if (username.isEmpty()) {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("아이디를 입력해주세요."), true);
            m_usernameEdit->setFocus();
            return false;
        }

        if (password.isEmpty()) {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("비밀번호를 입력해주세요."), true);
            m_passwordEdit->setFocus();
            return false;
        }

        return true;
    }

    bool LoginWindow::validateRegisterInput()
    {
        QString username = m_regUsernameEdit->text().trimmed();
        QString password = m_regPasswordEdit->text();
        QString confirmPassword = m_regConfirmPasswordEdit->text();
        QString email = m_regEmailEdit->text().trimmed();

        // 아이디 검증
        if (username.length() < 4 || username.length() > 20) {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("아이디는 4-20자여야 합니다."), true);
            m_regUsernameEdit->setFocus();
            return false;
        }

        QRegularExpression usernameRegex("^[a-zA-Z0-9]+$");
        if (!usernameRegex.match(username).hasMatch()) {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("아이디는 영문과 숫자만 사용 가능합니다."), true);
            m_regUsernameEdit->setFocus();
            return false;
        }

        // 비밀번호 검증
        if (password.length() < 8) {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("비밀번호는 8자 이상이어야 합니다."), true);
            m_regPasswordEdit->setFocus();
            return false;
        }

        QRegularExpression passwordRegex("^(?=.*[a-zA-Z])(?=.*[0-9]).+$");
        if (!passwordRegex.match(password).hasMatch()) {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("비밀번호는 영문과 숫자를 포함해야 합니다."), true);
            m_regPasswordEdit->setFocus();
            return false;
        }

        // 비밀번호 확인
        if (password != confirmPassword) {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("비밀번호가 일치하지 않습니다."), true);
            m_regConfirmPasswordEdit->setFocus();
            return false;
        }

        // 이메일 검증
        QRegularExpression emailRegex("^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$");
        if (!emailRegex.match(email).hasMatch()) {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("올바른 이메일 주소를 입력해주세요."), true);
            m_regEmailEdit->setFocus();
            return false;
        }

        return true;
    }

    bool LoginWindow::validatePasswordResetInput()
    {
        QString email = m_resetEmailEdit->text().trimmed();

        if (email.isEmpty()) {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("이메일을 입력해주세요."), true);
            m_resetEmailEdit->setFocus();
            return false;
        }

        QRegularExpression emailRegex("^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$");
        if (!emailRegex.match(email).hasMatch()) {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("올바른 이메일 주소를 입력해주세요."), true);
            m_resetEmailEdit->setFocus();
            return false;
        }

        return true;
    }

    void LoginWindow::showMessage(const QString& title, const QString& message, bool isError)
    {
        QMessageBox::Icon icon = isError ? QMessageBox::Warning : QMessageBox::Information;

        QMessageBox msgBox(this);
        msgBox.setIcon(icon);
        msgBox.setWindowTitle(title);
        msgBox.setText(message);
        msgBox.setStandardButtons(QMessageBox::Ok);
        msgBox.setDefaultButton(QMessageBox::Ok);

        // 메시지박스 스타일 설정
        msgBox.setStyleSheet(
            "QMessageBox { "
            "background-color: white; "
            "} "
            "QMessageBox QLabel { "
            "color: #2c3e50; "
            "font-size: 13px; "
            "} "
            "QMessageBox QPushButton { "
            "background-color: #3498db; "
            "border: none; "
            "border-radius: 4px; "
            "color: white; "
            "font-weight: bold; "
            "padding: 6px 15px; "
            "min-width: 60px; "
            "} "
            "QMessageBox QPushButton:hover { "
            "background-color: #2980b9; "
            "}"
        );

        msgBox.exec();
    }

} // namespace Blokus

#include "ui/LoginWindow.moc"