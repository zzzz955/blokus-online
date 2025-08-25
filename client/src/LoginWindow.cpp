#include "LoginWindow.h"
#include <QApplication>
#include <QDesktopWidget>
#include <QPropertyAnimation>
#include <QGraphicsOpacityEffect>
#include <QRegularExpression>
#include <QRegularExpressionValidator>
#include <QTimer>
#include <QScreen>
#include <QGuiApplication>
#include <QSvgWidget>
#include <QDesktopServices>
#include <QUrl>

namespace Blokus
{

    LoginWindow::LoginWindow(QWidget *parent)
        : QMainWindow(parent),
          m_centralWidget(nullptr),
          m_mainLayout(nullptr),
          m_titleWidget(nullptr),
          m_titleLabel(nullptr),
          m_subtitleLabel(nullptr),
          m_titleSvgWidget(nullptr),
          m_formContainer(nullptr),
          m_formLayout(nullptr),
          m_loginForm(nullptr),
          m_usernameEdit(nullptr),
          m_passwordEdit(nullptr),
          m_loginButton(nullptr),
          m_showRegisterButton(nullptr),
          m_showPasswordResetButton(nullptr),
          m_loadingWidget(nullptr),
          m_progressBar(nullptr),
          m_loadingLabel(nullptr),
          m_loadingMovie(nullptr),
          m_isLoading(false),
          m_oidcAuthenticator(new OidcAuthenticator(this)),
          m_animationTimer(new QTimer(this))
    {
        setupUI();
        setupStyles();
        createAnimations();
        
        // OIDC 시그널 연결
        connect(m_oidcAuthenticator, &OidcAuthenticator::authenticationSucceeded,
                this, &LoginWindow::onOidcAuthenticationSucceeded);
        connect(m_oidcAuthenticator, &OidcAuthenticator::authenticationFailed,
                this, &LoginWindow::onOidcAuthenticationFailed);
        connect(m_oidcAuthenticator, &OidcAuthenticator::tokensRefreshed,
                this, &LoginWindow::onOidcTokensRefreshed);

        setWindowTitle(QString::fromUtf8("블로커스 온라인 - 로그인"));
        setFixedSize(400, 600);

        // 화면 중앙에 배치
        QRect screenGeometry = QGuiApplication::primaryScreen()->availableGeometry();
        int x = (screenGeometry.width() - width()) / 2;
        int y = (screenGeometry.height() - height()) / 2;
        move(x, y);
    }

    LoginWindow::~LoginWindow()
    {
        if (m_loadingMovie)
        {
            delete m_loadingMovie;
        }
    }

    void LoginWindow::setupUI()
    {
        // 중앙 위젯 설정
        m_centralWidget = new QWidget(this);
        setCentralWidget(m_centralWidget);

        // 메인 레이아웃 고정 설정
        m_mainLayout = new QVBoxLayout(m_centralWidget);
        m_mainLayout->setContentsMargins(5, 5, 5, 5);
        m_mainLayout->setSpacing(3);

        // 타이틀 영역
        setupTitleArea();

        // 폼 컨테이너
        m_formContainer = new QWidget();
        // 폼 컨테이너 고정 레이아웃
        m_formLayout = new QVBoxLayout(m_formContainer);
        m_formLayout->setContentsMargins(5, 5, 5, 5);
        m_formLayout->setSpacing(3);
        m_formContainer->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Preferred);

        // 로그인 폼 설정
        setupLoginForm();

        // 로딩 위젯
        setupLoadingWidget();

        // 메인 레이아웃에 추가
        m_mainLayout->addWidget(m_titleWidget);
        m_mainLayout->addStretch(1);
        m_mainLayout->addWidget(m_formContainer);
        m_mainLayout->addStretch(2);
        m_mainLayout->addWidget(m_loadingWidget);
        
        // 로그인 폼 표시
        if (m_usernameEdit)
        {
            m_usernameEdit->setFocus();
        }
    }

    void LoginWindow::setupTitleArea()
    {
        m_titleWidget = new QWidget();
        // 타이틀 레이아웃 고정 설정
        QVBoxLayout *titleLayout = new QVBoxLayout(m_titleWidget);
        titleLayout->setContentsMargins(3, 3, 3, 3);
        titleLayout->setSpacing(2);

        // SVG 타이틀 이미지
        m_titleSvgWidget = new QSvgWidget("resource/login_title.svg");
        m_titleSvgWidget->setFixedHeight(240); // 고정 높이 설정
        m_titleSvgWidget->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Fixed);
        
        // 메인 타이틀 - 36px 고정 크기 (숨김 처리)
        m_titleLabel = new QLabel(QString::fromUtf8("Blokus-Online"));
        m_titleLabel->setAlignment(Qt::AlignCenter);
        m_titleLabel->setVisible(false); // SVG로 대체되므로 숨김
        // 타이틀 폰트 36px 고정 크기 설정
        QFont titleFont("맑은 고딕", 36, QFont::Bold);
        m_titleLabel->setFont(titleFont);
        m_titleLabel->setStyleSheet(QString(
                                        "QLabel { "
                                        "color: rgba(%1, %2, %3, 255); "
                                        "margin-bottom: 5px; "
                                        "}")
                                        .arg(ModernPastelTheme::getTextPrimary().red())
                                        .arg(ModernPastelTheme::getTextPrimary().green())
                                        .arg(ModernPastelTheme::getTextPrimary().blue()));

        // 서브타이틀 - 20px 고정 크기 (숨김 처리)
        m_subtitleLabel = new QLabel(QString::fromUtf8("전략적 블록 배치 게임"));
        m_subtitleLabel->setAlignment(Qt::AlignCenter);
        m_subtitleLabel->setVisible(false); // SVG로 대체되므로 숨김
        // 서브타이틀 폰트 20px 고정 크기 설정
        QFont subtitleFont("맑은 고딕", 20, QFont::Normal);
        m_subtitleLabel->setFont(subtitleFont);
        m_subtitleLabel->setStyleSheet(QString(
                                           "QLabel { "
                                           "color: rgba(%1, %2, %3, 255); "
                                           "margin-bottom: 10px; "
                                           "}")
                                           .arg(ModernPastelTheme::getTextSecondary().red())
                                           .arg(ModernPastelTheme::getTextSecondary().green())
                                           .arg(ModernPastelTheme::getTextSecondary().blue()));

        titleLayout->addWidget(m_titleSvgWidget);
        titleLayout->addWidget(m_titleLabel);
        titleLayout->addWidget(m_subtitleLabel);
    }

    void LoginWindow::setupLoginForm()
    {
        m_loginForm = new QWidget();
        m_loginForm->setStyleSheet(
            "QWidget { "
            "background-color: rgba(255, 255, 255, 250); "
            "border: 1px solid rgba(220, 221, 225, 255); "
            "border-radius: 8px; "
            "}");

        QVBoxLayout *layout = new QVBoxLayout(m_loginForm);
        layout->setContentsMargins(8, 8, 8, 8);
        layout->setSpacing(6);

        // 아이디 입력
        QLabel *usernameLabel = new QLabel(QString::fromUtf8("아이디"));
        QFont headerFont("맑은 고딕", 12, QFont::Bold); // 헤더 12px
        usernameLabel->setFont(headerFont);
        usernameLabel->setStyleSheet(QString(
                                         "QLabel { font-weight: bold; color: rgba(%1, %2, %3, 255); }")
                                         .arg(ModernPastelTheme::getTextPrimary().red())
                                         .arg(ModernPastelTheme::getTextPrimary().green())
                                         .arg(ModernPastelTheme::getTextPrimary().blue()));

        m_usernameEdit = new QLineEdit();
        m_usernameEdit->setPlaceholderText(QString::fromUtf8("아이디를 입력하세요"));
        m_usernameEdit->setMaxLength(20);
        m_usernameEdit->setStyleSheet(
            "QLineEdit { "
            "background-color: rgba(255, 255, 255, 250); "
            "color: rgba(47, 54, 64, 255); "
            "border: 2px solid rgba(220, 221, 225, 255); "
            "border-radius: 4px; "
            "padding: 4px; "
            "} "
            "QLineEdit:focus { "
            "border-color: rgba(116, 185, 255, 255); "
            "background-color: rgba(255, 255, 255, 255); "
            "}");
        QFont inputFont("맑은 고딕", 14, QFont::Normal); // 입력 텍스트 14px
        m_usernameEdit->setFont(inputFont);
        m_usernameEdit->setMinimumSize(80, 22);

        // 비밀번호 입력
        QLabel *passwordLabel = new QLabel(QString::fromUtf8("비밀번호"));
        passwordLabel->setFont(headerFont); // 동일한 헤더 폰트 사용
        passwordLabel->setStyleSheet(QString(
                                         "QLabel { font-weight: bold; color: rgba(%1, %2, %3, 255); }")
                                         .arg(ModernPastelTheme::getTextPrimary().red())
                                         .arg(ModernPastelTheme::getTextPrimary().green())
                                         .arg(ModernPastelTheme::getTextPrimary().blue()));

        m_passwordEdit = new QLineEdit();
        m_passwordEdit->setPlaceholderText(QString::fromUtf8("비밀번호를 입력하세요"));
        m_passwordEdit->setEchoMode(QLineEdit::Password);
        m_passwordEdit->setMaxLength(50);
        m_passwordEdit->setStyleSheet(
            "QLineEdit { "
            "background-color: rgba(255, 255, 255, 250); "
            "color: rgba(47, 54, 64, 255); "
            "border: 2px solid rgba(220, 221, 225, 255); "
            "border-radius: 4px; "
            "padding: 4px; "
            "} "
            "QLineEdit:focus { "
            "border-color: rgba(116, 185, 255, 255); "
            "background-color: rgba(255, 255, 255, 255); "
            "}");
        m_passwordEdit->setFont(inputFont);
        m_passwordEdit->setMinimumSize(80, 22);

        // 로그인 버튼
        m_loginButton = new QPushButton(QString::fromUtf8("로그인"));
        m_loginButton->setStyleSheet(
            "QPushButton { background-color: #3498db; color: white; } "
            "QPushButton:hover { background-color: #2980b9; }");
        m_loginButton->setMinimumSize(70, 26);
        
        // OIDC 로그인 버튼
        m_oidcLoginButton = new QPushButton(QString::fromUtf8("Google로 로그인"));
        m_oidcLoginButton->setStyleSheet(
            "QPushButton { background-color: #4285f4; color: white; } "
            "QPushButton:hover { background-color: #3367d6; }");
        m_oidcLoginButton->setMinimumSize(70, 26);

        // 회원가입 버튼
        m_showRegisterButton = new QPushButton(QString::fromUtf8("회원가입"));
        m_showRegisterButton->setStyleSheet(
            "QPushButton { background-color: #27ae60; color: white; } "
            "QPushButton:hover { background-color: #229954; }");
        m_showRegisterButton->setMinimumSize(60, 22);

        // 비밀번호 재설정 버튼
        m_showPasswordResetButton = new QPushButton(QString::fromUtf8("비밀번호 찾기"));
        m_showPasswordResetButton->setStyleSheet(
            "QPushButton { background-color: #95a5a6; color: white; } "
            "QPushButton:hover { background-color: #7f8c8d; }");
        m_showPasswordResetButton->setMinimumSize(70, 18);

        // 레이아웃에 추가
        layout->addWidget(usernameLabel);
        layout->addWidget(m_usernameEdit);
        layout->addWidget(passwordLabel);
        layout->addWidget(m_passwordEdit);
        layout->addSpacing(6); // 고정 스페이싱
        layout->addWidget(m_loginButton);
        layout->addSpacing(3); // 고정 스페이싱
        layout->addWidget(m_oidcLoginButton);
        layout->addSpacing(3); // 고정 스페이싱
        layout->addWidget(m_showRegisterButton);
        layout->addWidget(m_showPasswordResetButton);

        // 시그널 연결
        connect(m_usernameEdit, &QLineEdit::textChanged, this, &LoginWindow::onUsernameTextChanged);
        connect(m_passwordEdit, &QLineEdit::textChanged, this, &LoginWindow::onPasswordTextChanged);
        connect(m_usernameEdit, &QLineEdit::returnPressed, this, &LoginWindow::onLoginClicked);
        connect(m_passwordEdit, &QLineEdit::returnPressed, this, &LoginWindow::onLoginClicked);
        connect(m_loginButton, &QPushButton::clicked, this, &LoginWindow::onLoginClicked);
        connect(m_oidcLoginButton, &QPushButton::clicked, this, &LoginWindow::onOidcLoginClicked);
        connect(m_showRegisterButton, &QPushButton::clicked, this, &LoginWindow::onShowRegisterForm);
        connect(m_showPasswordResetButton, &QPushButton::clicked, this, &LoginWindow::onShowPasswordResetForm);

        m_formLayout->addWidget(m_loginForm);
    }
    
    QString LoginWindow::getAuthUrl() const
    {
#ifdef QT_DEBUG
        return "http://localhost:3000/auth/signin";
#else
        return "https://blokus-online.mooo.com/auth/signin";
#endif
    }


    void LoginWindow::setupLoadingWidget()
    {
        m_loadingWidget = new QWidget();
        QVBoxLayout *layout = new QVBoxLayout(m_loadingWidget);
        layout->setContentsMargins(0, 0, 0, 0);
        layout->setSpacing(10);

        m_progressBar = new QProgressBar();
        m_progressBar->setRange(0, 0); // 무한 로딩
        m_progressBar->setMinimumHeight(8);
        m_progressBar->setMaximumHeight(8);

        m_loadingLabel = new QLabel(QString::fromUtf8("서버에 연결 중..."));
        m_loadingLabel->setAlignment(Qt::AlignCenter);
        QFont loadingFont("맑은 고딕", 12, QFont::Normal); // 헤더 12px
        m_loadingLabel->setFont(loadingFont);
        m_loadingLabel->setStyleSheet("color: #3498db;");

        layout->addWidget(m_progressBar);
        layout->addWidget(m_loadingLabel);

        m_loadingWidget->hide(); // 초기에는 숨김
    }

    void LoginWindow::setupStyles()
    {
        // 메인 윈도우 배경 및 버튼 스타일
        setStyleSheet(
            "QMainWindow { "
            "background: qlineargradient(x1:0, y1:0, x2:0, y2:1, "
            "stop:0 #ecf0f1, stop:1 #bdc3c7); "
            "} "
            // 버튼 기본 스타일
            "QPushButton { "
            "border: none; border-radius: 6px; font-weight: bold; "
            "font-size: 20px !important; padding: 4px 8px; "
            "font-family: '맑은 고딕' !important; "
            "background-color: #95a5a6; color: white; "
            "} "
            "QPushButton:hover { background-color: #7f8c8d; } ");

        // 폼 컨테이너 스타일
        m_formContainer->setStyleSheet(
            "QWidget { "
            "background-color: white; "
            "border-radius: 12px; "
            "border: 1px solid #ddd; "
            "}");

        updateFormStyles();
    }

    void LoginWindow::updateFormStyles()
    {
        // 고정 입력 필드 스타일
        QString inputStyle =
            "QLineEdit { "
            "background-color: rgba(255, 255, 255, 250); "
            "color: rgba(47, 54, 64, 255); "
            "border: 2px solid rgba(220, 221, 225, 255); "
            "border-radius: 4px; "
            "padding: 4px; "
            "font-family: '맑은 고딕'; "
            "} "
            "QLineEdit:focus { "
            "border-color: rgba(116, 185, 255, 255); "
            "background-color: rgba(255, 255, 255, 255); "
            "}";

        // 폰트 크기 일괄 적용 (CSS로 버튼 폰트 관리, 입력 필드만 개별 설정)
        QFont inputFont("맑은 고딕", 12, QFont::Normal);       // 입력 텍스트 14px

        if (m_usernameEdit)
        {
            m_usernameEdit->setStyleSheet(inputStyle);
            m_usernameEdit->setFont(inputFont);
        }
        if (m_passwordEdit)
        {
            m_passwordEdit->setStyleSheet(inputStyle);
            m_passwordEdit->setFont(inputFont);
        }
        // 버튼 폰트는 CSS로 관리됨

        // 타이틀과 서브타이틀 스타일 업데이트 (고정 크기)
        if (m_titleLabel)
        {
            QFont titleFont("맑은 고딕", 36, QFont::Bold); // 타이틀 36px
            m_titleLabel->setFont(titleFont);
        }
        if (m_subtitleLabel)
        {
            QFont subtitleFont("맑은 고딕", 20, QFont::Normal); // 서브타이틀 20px
            m_subtitleLabel->setFont(subtitleFont);
        }

        // 폼 카드 스타일 업데이트 (고정 스타일)
        QString cardStyle =
            "QWidget { "
            "background-color: rgba(255, 255, 255, 250); "
            "border: 1px solid rgba(220, 221, 225, 255); "
            "border-radius: 8px; "
            "}";

        if (m_loginForm)
        {
            m_loginForm->setStyleSheet(cardStyle);
        }

        // 프로그레스 바 스타일 (고정 스타일)
        if (m_progressBar)
        {
            m_progressBar->setStyleSheet(
                "QProgressBar { "
                "border: none; "
                "border-radius: 4px; "
                "background-color: #f1f3f4; "
                "} "
                "QProgressBar::chunk { "
                "background-color: #74b9ff; "
                "border-radius: 4px; "
                "}");
        }
    }

    // 이전 ResponsiveLayoutManager 기반 메서드들 제거됨 - 고정 사이즈 사용

    void LoginWindow::createAnimations()
    {
        // 애니메이션 타이머 설정
        m_animationTimer->setInterval(50);
        connect(m_animationTimer, &QTimer::timeout, this, &LoginWindow::updateLoadingAnimation);
    }


    // 이벤트 핸들러들
    void LoginWindow::onLoginClicked()
    {
        if (!validateLoginInput())
            return;

        QString username = m_usernameEdit->text().trimmed();
        QString password = m_passwordEdit->text();

        showLoadingState(true);
        emit loginRequested(username, password);
    }


    void LoginWindow::onShowRegisterForm()
    {
        QDesktopServices::openUrl(QUrl(getAuthUrl()));
    }

    void LoginWindow::onShowPasswordResetForm()
    {
        QDesktopServices::openUrl(QUrl(getAuthUrl()));
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
    void LoginWindow::keyPressEvent(QKeyEvent *event)
    {
        QMainWindow::keyPressEvent(event);
    }

    // 화면 크기 변경 이벤트 처리
    void LoginWindow::resizeEvent(QResizeEvent *event)
    {
        QMainWindow::resizeEvent(event);
        updateFormStyles();
    }

    // 결과 처리 함수들
    void LoginWindow::setLoginResult(bool success, const QString &message)
    {
        showLoadingState(false);

        if (success)
        {
            showMessage(QString::fromUtf8("로그인 성공"), message, false);
            emit loginSuccessful(m_usernameEdit->text().trimmed());
        }
        else
        {
            showMessage(QString::fromUtf8("로그인 실패"), message, true);
        }
    }


    // 유틸리티 함수들
    void LoginWindow::clearInputs()
    {
        // 로그인 폼 입력 초기화
        if (m_usernameEdit)
            m_usernameEdit->clear();
        if (m_passwordEdit)
            m_passwordEdit->clear();
    }

    void LoginWindow::setFormEnabled(bool enabled)
    {
        // 로그인 폼
        if (m_usernameEdit)
            m_usernameEdit->setEnabled(enabled);
        if (m_passwordEdit)
            m_passwordEdit->setEnabled(enabled);
        if (m_loginButton)
            m_loginButton->setEnabled(enabled);
        if (m_showRegisterButton)
            m_showRegisterButton->setEnabled(enabled);
        if (m_showPasswordResetButton)
            m_showPasswordResetButton->setEnabled(enabled);
        if (m_oidcLoginButton)
            m_oidcLoginButton->setEnabled(enabled);
    }

    void LoginWindow::showLoadingState(bool loading)
    {
        m_isLoading = loading;

        if (loading)
        {
            setFormEnabled(false);
            m_loadingWidget->show();
            m_animationTimer->start();
        }
        else
        {
            setFormEnabled(true);
            m_loadingWidget->hide();
            m_animationTimer->stop();
        }
    }

    bool LoginWindow::validateLoginInput()
    {
        QString username = m_usernameEdit->text().trimmed();
        QString password = m_passwordEdit->text();

        if (username.isEmpty())
        {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("아이디를 입력해주세요."), true);
            m_usernameEdit->setFocus();
            return false;
        }

        if (password.isEmpty())
        {
            showMessage(QString::fromUtf8("입력 오류"), QString::fromUtf8("비밀번호를 입력해주세요."), true);
            m_passwordEdit->setFocus();
            return false;
        }

        return true;
    }


    void LoginWindow::showMessage(const QString &title, const QString &message, bool isError)
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
            "}");

        msgBox.exec();
    }
    
    // OIDC 이벤트 핸들러들
    void LoginWindow::onOidcLoginClicked()
    {
        showLoadingState(true);
        m_loadingLabel->setText(QString::fromUtf8("OAuth 인증 중..."));
        m_oidcAuthenticator->startAuthenticationFlow();
    }
    
    void LoginWindow::onOidcAuthenticationSucceeded(const QString& accessToken, const OidcTokens& tokens)
    {
        qDebug() << QString::fromUtf8("=== onOidcAuthenticationSucceeded 호출됨 ===");
        qDebug() << QString::fromUtf8("Access Token 길이: %1").arg(accessToken.length());
        qDebug() << QString::fromUtf8("Access Token 앞 20자: %1").arg(accessToken.left(20));
        
        Q_UNUSED(tokens); // 현재 사용하지 않는 매개변수
        
        qDebug() << QString::fromUtf8("로딩 상태 해제 중...");
        showLoadingState(false);
        
        qDebug() << QString::fromUtf8("jwtLoginRequested 시그널 emit 중...");
        // JWT 토큰으로 서버에 로그인 요청
        emit jwtLoginRequested(accessToken);
        qDebug() << QString::fromUtf8("jwtLoginRequested 시그널 emit 완료");
    }
    
    void LoginWindow::onOidcAuthenticationFailed(const QString& error)
    {
        qDebug() << QString::fromUtf8("=== onOidcAuthenticationFailed 호출됨 ===");
        qDebug() << QString::fromUtf8("에러 메시지: %1").arg(error);
        
        showLoadingState(false);
        showMessage(QString::fromUtf8("OAuth 로그인 실패"), error, true);
    }
    
    void LoginWindow::onOidcTokensRefreshed(const QString& accessToken)
    {
        // 토큰이 자동으로 갱신된 경우
        // 필요시 자동 재로그인 처리
        emit jwtLoginRequested(accessToken);
    }

} // namespace Blokus

#include "ui/LoginWindow.moc"