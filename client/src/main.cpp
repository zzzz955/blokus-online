#include <QApplication>
#include <QDebug>
#include <QTimer>
#include <QFont>

#include "ui/LoginWindow.h"
#include "ui/LobbyWindow.h"
#include "ui/MainWindow.h"

class AppController : public QObject
{
    Q_OBJECT

public:
    AppController()
        : m_loginWindow(nullptr)
        , m_lobbyWindow(nullptr)
        , m_gameWindow(nullptr)
        , m_currentUsername("")
    {
        initializeApplication();
    }

    ~AppController()
    {
        cleanupWindows();
    }

    void start()
    {
        createLoginWindow();
    }

private slots:
    void handleLoginRequest(const QString& username, const QString& password)
    {
        qDebug() << QString::fromUtf8("로그인 시도: %1").arg(username);

        // 더미 인증 로직 (서버 연동 전까지)
        QTimer::singleShot(1500, this, [this, username]() {
            if (username.length() >= 4) {
                m_loginWindow->setLoginResult(true, QString::fromUtf8("환영합니다, %1님!").arg(username));
            }
            else {
                m_loginWindow->setLoginResult(false, QString::fromUtf8("아이디가 너무 짧습니다."));
            }
            });
    }

    void handleRegisterRequest(const QString& username, const QString& password, const QString& email)
    {
        qDebug() << QString::fromUtf8("회원가입 시도: %1, %2").arg(username, email);

        // 더미 회원가입 로직
        QTimer::singleShot(2000, this, [this]() {
            m_loginWindow->setRegisterResult(true, QString::fromUtf8("회원가입이 완료되었습니다!"));
            });
    }

    void handlePasswordResetRequest(const QString& email)
    {
        qDebug() << QString::fromUtf8("비밀번호 재설정 요청: %1").arg(email);

        // 더미 비밀번호 재설정 로직
        QTimer::singleShot(1000, this, [this]() {
            m_loginWindow->setPasswordResetResult(true, QString::fromUtf8("이메일을 확인해주세요."));
            });
    }

    void handleLoginSuccess(const QString& username)
    {
        qDebug() << QString::fromUtf8("로그인 성공! 로비로 이동: %1").arg(username);

        m_currentUsername = username;

        // 로그인 창 숨기고 로비 창 생성
        if (m_loginWindow) {
            m_loginWindow->hide();
        }

        createLobbyWindow();
    }

    void handleLogoutRequest()
    {
        qDebug() << QString::fromUtf8("로그아웃 요청");

        // 모든 창 정리하고 로그인 창으로 돌아가기
        if (m_lobbyWindow) {
            m_lobbyWindow->hide();
            m_lobbyWindow->deleteLater();
            m_lobbyWindow = nullptr;
        }

        if (m_gameWindow) {
            m_gameWindow->hide();
            m_gameWindow->deleteLater();
            m_gameWindow = nullptr;
        }

        if (m_loginWindow) {
            m_loginWindow->show();
            m_loginWindow->raise();
            m_loginWindow->activateWindow();
        }

        m_currentUsername.clear();
    }

    void handleCreateRoomRequest(const Blokus::RoomInfo& roomInfo)
    {
        qDebug() << QString::fromUtf8("방 생성 요청: %1").arg(roomInfo.roomName);

        // 더미 방 생성 로직
        QTimer::singleShot(500, this, [this, roomInfo]() {
            qDebug() << QString::fromUtf8("방 생성 성공! 게임 시작");
            createGameWindow(roomInfo.roomId, true); // 호스트로 입장
            });
    }

    void handleJoinRoomRequest(int roomId, const QString& password)
    {
        qDebug() << QString::fromUtf8("방 입장 요청: 방번호 %1").arg(roomId);

        // 더미 방 입장 로직
        QTimer::singleShot(500, this, [this, roomId]() {
            qDebug() << QString::fromUtf8("방 입장 성공! 게임 시작");
            createGameWindow(roomId, false); // 일반 참가자로 입장
            });
    }

    void handleGameStartRequest()
    {
        qDebug() << QString::fromUtf8("게임 시작 요청");
        // 게임 시작 로직 (향후 구현)
    }

private:
    void initializeApplication()
    {
        qDebug() << QString::fromUtf8("=== 블로커스 온라인 초기화 ===");
    }

    void createLoginWindow()
    {
        qDebug() << QString::fromUtf8("로그인 창 생성");

        m_loginWindow = new Blokus::LoginWindow();

        // 로그인 시그널 연결
        connect(m_loginWindow, &Blokus::LoginWindow::loginRequested,
            this, &AppController::handleLoginRequest);
        connect(m_loginWindow, &Blokus::LoginWindow::registerRequested,
            this, &AppController::handleRegisterRequest);
        connect(m_loginWindow, &Blokus::LoginWindow::passwordResetRequested,
            this, &AppController::handlePasswordResetRequest);
        connect(m_loginWindow, &Blokus::LoginWindow::loginSuccessful,
            this, &AppController::handleLoginSuccess);

        // 🔥 로그인 창이 닫히면 애플리케이션 종료
        connect(m_loginWindow, &QMainWindow::destroyed,
            qApp, &QApplication::quit);

        m_loginWindow->show();
    }

    void createLobbyWindow()
    {
        qDebug() << QString::fromUtf8("로비 창 생성 시작");

        try {
            m_lobbyWindow = new Blokus::LobbyWindow(m_currentUsername);

            // 로비 시그널 연결
            connect(m_lobbyWindow, &Blokus::LobbyWindow::logoutRequested,
                this, &AppController::handleLogoutRequest);
            connect(m_lobbyWindow, &Blokus::LobbyWindow::createRoomRequested,
                this, &AppController::handleCreateRoomRequest);
            connect(m_lobbyWindow, &Blokus::LobbyWindow::joinRoomRequested,
                this, &AppController::handleJoinRoomRequest);
            connect(m_lobbyWindow, &Blokus::LobbyWindow::gameStartRequested,
                this, &AppController::handleGameStartRequest);

            // 🔥 주의: 로비 창이 닫힐 때 애플리케이션 종료하지 않도록 제거
            // connect(m_lobbyWindow, &QMainWindow::destroyed, qApp, &QApplication::quit);

            m_lobbyWindow->show();
            m_lobbyWindow->raise();
            m_lobbyWindow->activateWindow();

            qDebug() << QString::fromUtf8("로비 창 생성 완료");

        }
        catch (const std::exception& e) {
            qDebug() << QString::fromUtf8("로비 창 생성 실패: %1").arg(e.what());
        }
        catch (...) {
            qDebug() << QString::fromUtf8("로비 창 생성 중 알 수 없는 오류");
        }
    }

    void createGameWindow(int roomId, bool isHost)
    {
        qDebug() << QString::fromUtf8("게임 창 생성: 방 %1, 호스트: %2").arg(roomId).arg(isHost);

        // 로비 창 숨기기
        if (m_lobbyWindow) {
            m_lobbyWindow->hide();
        }

        // 게임 창 생성
        if (!m_gameWindow) {
            m_gameWindow = new Blokus::MainWindow();
        }

        // 창 제목 설정
        QString title = QString::fromUtf8("블로커스 온라인 - %1님 (방 #%2)")
            .arg(m_currentUsername).arg(roomId);
        if (isHost) {
            title += QString::fromUtf8(" [방장]");
        }
        m_gameWindow->setWindowTitle(title);

        m_gameWindow->show();
        m_gameWindow->raise();
        m_gameWindow->activateWindow();
    }

    void cleanupWindows()
    {
        if (m_loginWindow) {
            delete m_loginWindow;
            m_loginWindow = nullptr;
        }

        if (m_lobbyWindow) {
            delete m_lobbyWindow;
            m_lobbyWindow = nullptr;
        }

        if (m_gameWindow) {
            delete m_gameWindow;
            m_gameWindow = nullptr;
        }
    }

private:
    Blokus::LoginWindow* m_loginWindow;
    Blokus::LobbyWindow* m_lobbyWindow;
    Blokus::MainWindow* m_gameWindow;
    QString m_currentUsername;
};

int main(int argc, char* argv[])
{
    QApplication app(argc, argv);

    // 애플리케이션 설정
    app.setApplicationName(QString::fromUtf8("블로커스 온라인"));
    app.setApplicationVersion("1.0.0");
    app.setOrganizationName("Blokus Online");

    // 한글 폰트 설정
    QFont defaultFont("맑은 고딕", 9);
    if (!defaultFont.exactMatch()) {
        defaultFont = QFont("굴림", 9);
    }
    app.setFont(defaultFont);

    // 앱 컨트롤러 생성 및 시작
    AppController controller;
    controller.start();

    qDebug() << QString::fromUtf8("블로커스 온라인 시작됨");

    return app.exec();
}

#include "main.moc"