#include <QApplication>
#include "ui/LoginWindow.h"
#include "ui/MainWindow.h"
#include <QDebug>

class AppController : public QObject
{
    Q_OBJECT

public:
    AppController()
        : m_loginWindow(nullptr)
        , m_gameWindow(nullptr)
    {
        setupLoginWindow();
    }

    ~AppController()
    {
        if (m_loginWindow) delete m_loginWindow;
        if (m_gameWindow) delete m_gameWindow;
    }

private slots:
    void onLoginRequested(const QString& username, const QString& password)
    {
        qDebug() << QString::fromUtf8("로그인 시도: %1").arg(username);

        // TODO: 실제 서버 통신으로 교체
        // 현재는 더미 로직으로 모든 로그인을 성공 처리
        QTimer::singleShot(1500, this, [this, username]() {
            // 간단한 더미 검증 (실제로는 서버에서 처리)
            if (username.length() >= 4) {
                m_loginWindow->setLoginResult(true, QString::fromUtf8("환영합니다, %1님!").arg(username));
            }
            else {
                m_loginWindow->setLoginResult(false, QString::fromUtf8("아이디 또는 비밀번호가 올바르지 않습니다."));
            }
            });
    }

    void onRegisterRequested(const QString& username, const QString& password, const QString& email)
    {
        qDebug() << QString::fromUtf8("회원가입 시도: %1, %2").arg(username, email);

        // TODO: 실제 서버 통신으로 교체
        QTimer::singleShot(2000, this, [this]() {
            m_loginWindow->setRegisterResult(true, QString::fromUtf8("회원가입이 완료되었습니다!"));
            });
    }

    void onPasswordResetRequested(const QString& email)
    {
        qDebug() << QString::fromUtf8("비밀번호 재설정 요청: %1").arg(email);

        // TODO: 실제 이메일 서비스 연동으로 교체
        QTimer::singleShot(1000, this, [this]() {
            m_loginWindow->setPasswordResetResult(true, QString::fromUtf8("이메일을 확인해주세요."));
            });
    }

    void onLoginSuccessful(const QString& username)
    {
        qDebug() << QString::fromUtf8("로그인 성공! 게임 화면으로 전환: %1").arg(username);

        // 로그인 창 숨기기
        if (m_loginWindow) {
            m_loginWindow->hide();
        }

        // 게임 창 생성 및 표시
        if (!m_gameWindow) {
            m_gameWindow = new Blokus::MainWindow();

            // 게임 창 종료 시 애플리케이션 종료
            connect(m_gameWindow, &QMainWindow::destroyed, qApp, &QApplication::quit);

            // 게임 창에서 로그아웃 시 로그인 창으로 돌아가기 (향후 구현)
            // connect(m_gameWindow, &MainWindow::logoutRequested, this, &AppController::onLogoutRequested);
        }

        // 창 타이틀에 사용자 이름 표시
        m_gameWindow->setWindowTitle(QString::fromUtf8("블로커스 온라인 - %1님").arg(username));
        m_gameWindow->show();
        m_gameWindow->raise();
        m_gameWindow->activateWindow();
    }

    void onLogoutRequested()
    {
        qDebug() << QString::fromUtf8("로그아웃 요청");

        // 게임 창 숨기기
        if (m_gameWindow) {
            m_gameWindow->hide();
        }

        // 로그인 창 다시 표시
        if (m_loginWindow) {
            m_loginWindow->show();
            m_loginWindow->raise();
            m_loginWindow->activateWindow();
        }
    }

private:
    void setupLoginWindow()
    {
        m_loginWindow = new Blokus::LoginWindow();

        // 로그인 관련 시그널 연결
        connect(m_loginWindow, &Blokus::LoginWindow::loginRequested,
            this, &AppController::onLoginRequested);
        connect(m_loginWindow, &Blokus::LoginWindow::registerRequested,
            this, &AppController::onRegisterRequested);
        connect(m_loginWindow, &Blokus::LoginWindow::passwordResetRequested,
            this, &AppController::onPasswordResetRequested);
        connect(m_loginWindow, &Blokus::LoginWindow::loginSuccessful,
            this, &AppController::onLoginSuccessful);

        // 로그인 창 표시
        m_loginWindow->show();
    }

private:
    Blokus::LoginWindow* m_loginWindow;
    Blokus::MainWindow* m_gameWindow;
};

int main(int argc, char* argv[])
{
    QApplication app(argc, argv);

    // 애플리케이션 정보 설정
    app.setApplicationName(QString::fromUtf8("블로커스 온라인"));
    app.setApplicationVersion("1.0.0");
    app.setOrganizationName("Blokus Online");

    // 한글 폰트 설정 (Windows에서 한글 표시 개선)
    QFont font("맑은 고딕", 9);
    if (!font.exactMatch()) {
        font = QFont("굴림", 9);
    }
    app.setFont(font);

    // 앱 컨트롤러 생성 및 실행
    AppController controller;

    qDebug() << QString::fromUtf8("=== 블로커스 온라인 시작 ===");
    qDebug() << QString::fromUtf8("로그인 화면 표시됨");

    return app.exec();
}

#include "main.moc"