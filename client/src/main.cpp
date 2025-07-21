#include <QApplication>
#include <QDebug>
#include <QTimer>
#include <QFont>

#include "LoginWindow.h"
#include "LobbyWindow.h"
#include "GameRoomWindow.h"
#include "ClientTypes.h"
#include "NetworkClient.h"

using namespace Blokus;

class AppController : public QObject
{
    Q_OBJECT

public:
    AppController()
        : m_loginWindow(nullptr)
        , m_lobbyWindow(nullptr)
        , m_gameRoomWindow(nullptr)
        , m_networkClient(new NetworkClient(this))
        , m_currentUsername("")
        , m_currentRoomInfo()
    {
        initializeApplication();
        setupNetworkClient();
    }

    ~AppController()
    {
        cleanupWindows();
    }

    void start()
    {
        // 서버 연결 시도
        m_networkClient->connectToServer();
        createLoginWindow();
    }

private slots:
    void handleLoginRequest(const QString& username, const QString& password)
    {
        qDebug() << QString::fromUtf8("로그인 시도: %1").arg(username);
        
        if (!m_networkClient->isConnected()) {
            m_loginWindow->setLoginResult(false, QString::fromUtf8("서버에 연결되지 않았습니다."));
            return;
        }
        
        m_networkClient->login(username, password);
    }

    void handleRegisterRequest(const QString& username, const QString& password, const QString& email)
    {
        qDebug() << QString::fromUtf8("회원가입 시도: %1").arg(username);
        
        if (!m_networkClient->isConnected()) {
            m_loginWindow->setRegisterResult(false, QString::fromUtf8("서버에 연결되지 않았습니다."));
            return;
        }
        
        m_networkClient->registerUser(username, password);
    }

    void handlePasswordResetRequest(const QString& email)
    {
        qDebug() << QString::fromUtf8("비밀번호 재설정 요청: %1").arg(email);

        // 비밀번호 재설정은 아직 미구현
        m_loginWindow->setPasswordResetResult(false, QString::fromUtf8("비밀번호 재설정 기능은 준비 중입니다."));
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

        if (m_gameRoomWindow) {
            m_gameRoomWindow->hide();
            m_gameRoomWindow->deleteLater();
            m_gameRoomWindow = nullptr;
        }

        if (m_loginWindow) {
            m_loginWindow->show();
            m_loginWindow->raise();
            m_loginWindow->activateWindow();
        }

        m_currentUsername.clear();
        m_currentRoomInfo = GameRoomInfo();
    }

    void handleCreateRoomRequest(const RoomInfo& roomInfo)
    {
        qDebug() << QString::fromUtf8("방 생성 요청: %1").arg(roomInfo.roomName);

        // 더미 방 생성 로직
        QTimer::singleShot(500, this, [this, roomInfo]() {
            // RoomInfo를 GameRoomInfo로 변환
            GameRoomInfo gameRoomInfo;
            gameRoomInfo.roomId = roomInfo.roomId;
            gameRoomInfo.roomName = roomInfo.roomName;
            gameRoomInfo.hostUsername = m_currentUsername;
            gameRoomInfo.hostColor = PlayerColor::Blue;  // 방장은 항상 파란색
            gameRoomInfo.maxPlayers = 4;  // 클래식 모드만 지원
            gameRoomInfo.gameMode = QString::fromUtf8("클래식");
            gameRoomInfo.isPlaying = false;

            // 첫 번째 슬롯(파란색)에 호스트 배치 - std::array 접근 방식
            gameRoomInfo.playerSlots[0].username = m_currentUsername;
            gameRoomInfo.playerSlots[0].isHost = true;
            gameRoomInfo.playerSlots[0].isReady = true;

            qDebug() << QString::fromUtf8("방 생성 성공! 게임 룸으로 이동");
            createGameRoomWindow(gameRoomInfo, true); // 호스트로 입장
            });
    }

    void handleJoinRoomRequest(int roomId, const QString& password)
    {
        qDebug() << QString::fromUtf8("방 입장 요청: 방번호 %1").arg(roomId);

        // 더미 방 입장 로직
        QTimer::singleShot(500, this, [this, roomId]() {
            // 더미 방 정보 생성
            GameRoomInfo gameRoomInfo;
            gameRoomInfo.roomId = roomId;
            gameRoomInfo.roomName = QString::fromUtf8("테스트 방 #%1").arg(roomId);
            gameRoomInfo.hostUsername = QString::fromUtf8("방장");
            gameRoomInfo.hostColor = PlayerColor::Blue;
            gameRoomInfo.maxPlayers = 4;
            gameRoomInfo.gameMode = QString::fromUtf8("클래식");
            gameRoomInfo.isPlaying = false;

            // 기존 플레이어들 배치 (더미) - std::array 접근 방식
            gameRoomInfo.playerSlots[0].username = QString::fromUtf8("방장");
            gameRoomInfo.playerSlots[0].isHost = true;
            gameRoomInfo.playerSlots[0].isReady = true;

            // 내가 들어갈 다음 빈 슬롯 찾기
            PlayerColor myColor = PlayerColor::Yellow; // 기본적으로 노란색
            for (int i = 1; i < 4; ++i) {
                if (gameRoomInfo.playerSlots[i].isEmpty()) {
                    PlayerColor slotColor = static_cast<PlayerColor>(i + 1);
                    gameRoomInfo.playerSlots[i].username = m_currentUsername;
                    gameRoomInfo.playerSlots[i].isHost = false;
                    gameRoomInfo.playerSlots[i].isReady = false;
                    myColor = slotColor;
                    break;
                }
            }

            qDebug() << QString::fromUtf8("방 입장 성공! 게임 룸으로 이동 (색상: %1)")
                .arg(Utils::playerColorToString(myColor));
            createGameRoomWindow(gameRoomInfo, false); // 일반 참가자로 입장
            });
    }

    void handleLeaveRoomRequest()
    {
        qDebug() << QString::fromUtf8("방 나가기 요청");

        // 게임 룸 창 닫고 로비로 돌아가기
        if (m_gameRoomWindow) {
            m_gameRoomWindow->hide();
            m_gameRoomWindow->deleteLater();
            m_gameRoomWindow = nullptr;
        }

        // 로비 창 다시 표시
        if (m_lobbyWindow) {
            m_lobbyWindow->show();
            m_lobbyWindow->raise();
            m_lobbyWindow->activateWindow();
        }
        else {
            createLobbyWindow(); // 로비 창이 없으면 새로 생성
        }

        m_currentRoomInfo = GameRoomInfo();
    }

    void handleGameStartRequest()
    {
        qDebug() << QString::fromUtf8("게임 시작 요청");

        if (m_gameRoomWindow) {
            m_gameRoomWindow->startGame();
        }
    }

    void handleAddAIPlayerRequest(PlayerColor color, int difficulty)
    {
        qDebug() << QString::fromUtf8("AI 플레이어 추가 요청: %1 색상, 난이도 %2")
            .arg(Utils::playerColorToString(color)).arg(difficulty);

        // 더미 AI 추가 로직
        QTimer::singleShot(200, this, [this, color, difficulty]() {
            if (m_gameRoomWindow) {
                // AI 슬롯 생성
                PlayerSlot aiSlot;
                aiSlot.color = color;
                aiSlot.username = "";
                aiSlot.isAI = true;
                aiSlot.aiDifficulty = difficulty;
                aiSlot.isHost = false;
                aiSlot.isReady = true;

                m_gameRoomWindow->updatePlayerSlot(color, aiSlot);
                m_gameRoomWindow->addSystemMessage(
                    QString::fromUtf8("AI 플레이어가 추가되었습니다. (난이도: %1)")
                    .arg(difficulty == 1 ? "쉬움" : difficulty == 2 ? "보통" : "어려움"));
            }
            });
    }

    void handleRemovePlayerRequest(PlayerColor color)
    {
        qDebug() << QString::fromUtf8("플레이어 제거 요청: %1").arg(Utils::playerColorToString(color));

        // 더미 플레이어 제거 로직
        if (m_gameRoomWindow) {
            PlayerSlot emptySlot;
            emptySlot.color = color;
            m_gameRoomWindow->updatePlayerSlot(color, emptySlot);
            m_gameRoomWindow->addSystemMessage(QString::fromUtf8("플레이어가 방을 나갔습니다."));
        }
    }

    void handleKickPlayerRequest(PlayerColor color)
    {
        qDebug() << QString::fromUtf8("플레이어 강퇴 요청: %1").arg(Utils::playerColorToString(color));

        // 더미 강퇴 로직
        handleRemovePlayerRequest(color); // 제거와 동일하게 처리
    }

    void handleGameRoomChatMessage(const QString& message)
    {
        qDebug() << QString::fromUtf8("게임 룸 채팅: %1").arg(message);

        // 더미 채팅 처리 - 내 메시지를 방에 추가
        if (m_gameRoomWindow) {
            m_gameRoomWindow->addChatMessage(m_currentUsername, message, false);
        }
    }

    // 네트워크 시그널 핸들러들
    void onNetworkConnected()
    {
        qDebug() << QString::fromUtf8("서버 연결 성공");
    }

    void onNetworkDisconnected()
    {
        qDebug() << QString::fromUtf8("서버 연결 해제");
    }

    void onNetworkError(const QString& error)
    {
        qDebug() << QString::fromUtf8("네트워크 오류: %1").arg(error);
        
        if (m_loginWindow) {
            m_loginWindow->setLoginResult(false, QString::fromUtf8("네트워크 오류: %1").arg(error));
        }
    }

    void onLoginResult(bool success, const QString& message, const QString& sessionToken)
    {
        if (m_loginWindow) {
            m_loginWindow->setLoginResult(success, message);
        }
    }

    void onRegisterResult(bool success, const QString& message)
    {
        if (m_loginWindow) {
            m_loginWindow->setRegisterResult(success, message);
        }
    }

private:
    void initializeApplication()
    {
        qDebug() << QString::fromUtf8("=== 블로커스 온라인 초기화 ===");
    }

    void setupNetworkClient()
    {
        // 네트워크 연결 상태 시그널
        connect(m_networkClient, &NetworkClient::connected, 
                this, &AppController::onNetworkConnected);
        connect(m_networkClient, &NetworkClient::disconnected, 
                this, &AppController::onNetworkDisconnected);
        connect(m_networkClient, &NetworkClient::connectionError, 
                this, &AppController::onNetworkError);

        // 인증 관련 시그널
        connect(m_networkClient, &NetworkClient::loginResult, 
                this, &AppController::onLoginResult);
        connect(m_networkClient, &NetworkClient::registerResult, 
                this, &AppController::onRegisterResult);
        
        qDebug() << QString::fromUtf8("네트워크 클라이언트 설정 완료");
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

        // 로그인 창이 닫히면 애플리케이션 종료
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

    void createGameRoomWindow(const GameRoomInfo& roomInfo, bool isHost)
    {
        qDebug() << QString::fromUtf8("게임 룸 창 생성: 방 %1, 호스트: %2")
            .arg(roomInfo.roomId).arg(isHost);

        try {
            // 로비 창 숨기기
            if (m_lobbyWindow) {
                m_lobbyWindow->hide();
            }

            // 기존 게임 룸 창이 있으면 제거
            if (m_gameRoomWindow) {
                m_gameRoomWindow->deleteLater();
            }

            // 새 게임 룸 창 생성
            m_gameRoomWindow = new Blokus::GameRoomWindow(roomInfo, m_currentUsername);
            m_currentRoomInfo = roomInfo;

            // 게임 룸 시그널 연결
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::leaveRoomRequested,
                this, &AppController::handleLeaveRoomRequest);
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::gameStartRequested,
                this, &AppController::handleGameStartRequest);
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::addAIPlayerRequested,
                this, &AppController::handleAddAIPlayerRequest);
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::removePlayerRequested,
                this, &AppController::handleRemovePlayerRequest);
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::kickPlayerRequested,
                this, &AppController::handleKickPlayerRequest);
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::chatMessageSent,
                this, &AppController::handleGameRoomChatMessage);

            m_gameRoomWindow->show();
            m_gameRoomWindow->raise();
            m_gameRoomWindow->activateWindow();

            qDebug() << QString::fromUtf8("게임 룸 창 생성 완료");

        }
        catch (const std::exception& e) {
            qDebug() << QString::fromUtf8("게임 룸 창 생성 실패: %1").arg(e.what());
        }
        catch (...) {
            qDebug() << QString::fromUtf8("게임 룸 창 생성 중 알 수 없는 오류");
        }
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

        if (m_gameRoomWindow) {
            delete m_gameRoomWindow;
            m_gameRoomWindow = nullptr;
        }
    }

private:
    Blokus::LoginWindow* m_loginWindow;
    Blokus::LobbyWindow* m_lobbyWindow;
    Blokus::GameRoomWindow* m_gameRoomWindow;
    Blokus::NetworkClient* m_networkClient;
    QString m_currentUsername;
    Blokus::GameRoomInfo m_currentRoomInfo;
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

    qDebug() << QString::fromUtf8("블로커스 온라인 시작됨 - 클래식 모드 전용");

    return app.exec();
}

#include "main.moc"