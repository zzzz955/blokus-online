#include <QApplication>
#include <QDebug>
#include <QTimer>
#include <QFont>
#include <QMessageBox>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QPushButton>
#include <QThread>

#ifdef _WIN32
#include <Windows.h>
#include <io.h>
#include <fcntl.h>
#include <iostream>
#endif

#include "LoginWindow.h"
#include "LobbyWindow.h"
#include "GameRoomWindow.h"
#include "ClientTypes.h"
#include "NetworkClient.h"
#include "ClientConfigManager.h"
#include "BGMManager.h"
#include "UserSettingsDialog.h"

using namespace Blokus;

// Forward declaration
class AppController;

class AppController : public QObject
{
    Q_OBJECT

public:
    AppController()
        : m_loginWindow(nullptr), m_lobbyWindow(nullptr), m_gameRoomWindow(nullptr), m_networkClient(nullptr), m_currentUsername(""), m_currentUserInfo(), m_currentRoomInfo(), m_isLoadingInitialSettings(false), m_cachedUserSettings(UserSettings::getDefaults())
    {
        initializeApplication();
        initializeConfiguration();
        setupNetworkClient();
    }

    ~AppController()
    {
        cleanupWindows();
    }

    void start()
    {
        // 서버 연결 시도 (설정에서 읽은 호스트와 포트 사용)
        auto& config = ClientConfigManager::instance();
        m_networkClient->connectToServer(config.getServerHost(), config.getServerPort());
        createLoginWindow();
    }

private slots:
    void handleLoginRequest(const QString &username, const QString &password)
    {
        qDebug() << QString::fromUtf8("로그인 시도: %1").arg(username);

        if (!m_networkClient->isConnected())
        {
            m_loginWindow->setLoginResult(false, QString::fromUtf8("서버에 연결되지 않았습니다."));
            return;
        }

        m_networkClient->login(username, password);
    }

    void handleJwtLoginRequest(const QString &jwtToken)
    {
        qDebug() << QString::fromUtf8("JWT 로그인 시도");

        if (!m_networkClient->isConnected())
        {
            m_loginWindow->setLoginResult(false, QString::fromUtf8("서버에 연결되지 않았습니다."));
            return;
        }

        // JWT 토큰을 TCP 서버에 전송하여 인증 요청
        m_networkClient->loginWithJwt(jwtToken);
    }

    void handleLoginSuccess(const QString &username)
    {
        qDebug() << QString::fromUtf8("로그인 성공! 사용자 설정 로딩 중: %1").arg(username);

        m_currentUsername = username;

        // UI 변경을 메인 스레드에서 실행
        QMetaObject::invokeMethod(this, [this]() {
            // 로그인 창 숨기기
            if (m_loginWindow)
            {
                m_loginWindow->hide();
            }
        }, Qt::QueuedConnection);

        // 초기 설정 로딩 플래그 설정 및 사용자 설정 요청
        m_isLoadingInitialSettings = true;
        if (m_networkClient && m_networkClient->isConnected())
        {
            qDebug() << QString::fromUtf8("사용자 설정 자동 조회 시작");
            m_networkClient->requestUserSettings();
        }
        else
        {
            qWarning() << QString::fromUtf8("네트워크 연결이 없어 기본 설정으로 로비 생성");
            m_isLoadingInitialSettings = false;
            // 로비 창 생성도 메인 스레드에서 실행
            QMetaObject::invokeMethod(this, [this]() {
                createLobbyWindow();
            }, Qt::QueuedConnection);
        }

        // 로그인 성공 시 서버에서 자동으로 로비 진입 및 정보 전송하므로 별도 요청 불필요
    }

    void handleLogoutRequest()
    {
        qDebug() << QString::fromUtf8("로그아웃 요청");

        // 로비 나가기 요청
        if (m_networkClient && m_networkClient->isConnected())
        {
            m_networkClient->leaveLobby();
            m_networkClient->logout();
        }

        // 모든 창 정리하고 로그인 창으로 돌아가기
        if (m_lobbyWindow)
        {
            m_lobbyWindow->hide();
            m_lobbyWindow->deleteLater();
            m_lobbyWindow = nullptr;
        }

        if (m_gameRoomWindow)
        {
            m_gameRoomWindow->hide();
            m_gameRoomWindow->deleteLater();
            m_gameRoomWindow = nullptr;
        }

        if (m_loginWindow)
        {
            m_loginWindow->show();
            m_loginWindow->raise();
            m_loginWindow->activateWindow();
        }

        m_currentUsername.clear();
        m_currentDisplayname.clear();
        m_currentRoomInfo = GameRoomInfo();
    }

    void handleCreateRoomRequest(const RoomInfo &roomInfo)
    {
        qDebug() << QString::fromUtf8("방 생성 요청: %1").arg(roomInfo.roomName);

        // 실제 서버에 방 생성 요청 전송
        if (m_networkClient && m_networkClient->isConnected())
        {
            m_networkClient->createRoom(roomInfo.roomName, roomInfo.isPrivate, "");
        }

        // 서버 응답을 기다리고 onRoomCreated에서 처리
        // 더이상 더미 데이터 사용하지 않음
    }

    void handleJoinRoomRequest(int roomId, const QString &password)
    {
        qDebug() << QString::fromUtf8("방 입장 요청: 방번호 %1").arg(roomId);

        // 실제 서버에 방 입장 요청 전송
        if (m_networkClient && m_networkClient->isConnected())
        {
            m_networkClient->joinRoom(roomId, password);
        }

        // 서버 응답을 기다리고 onRoomJoined에서 처리
        // 더이상 더미 데이터 사용하지 않음
    }

    void handleLeaveRoomRequest()
    {
        qDebug() << QString::fromUtf8("방 나가기 요청");

        if (!(m_networkClient && m_networkClient->isConnected()))
        {
            qWarning() << "서버에 연결되어 있지 않아 방 나가기 실패";
            return;
        }

        // 게임 룸 창 닫고 로비로 돌아가기
        if (m_gameRoomWindow)
        {
            m_gameRoomWindow->hide();
            m_gameRoomWindow->deleteLater();
            m_gameRoomWindow = nullptr;
        }

        // 로비 창 다시 표시
        if (m_lobbyWindow)
        {
            m_lobbyWindow->show();
            m_lobbyWindow->raise();
            m_lobbyWindow->activateWindow();
        }
        else
        {
            createLobbyWindow(); // 로비 창이 없으면 새로 생성
        }
        m_currentRoomInfo = GameRoomInfo();

        // 서버에 방 나가기 요청
        m_networkClient->leaveRoom();
    }

    void handleGameStartRequest()
    {
        qDebug() << QString::fromUtf8("게임 시작 요청");

        if (m_networkClient && m_networkClient->isConnected())
        {
            m_networkClient->startGame();
        }
    }

    void handlePlayerReadyChanged(bool ready)
    {
        qDebug() << QString::fromUtf8("플레이어 준비 상태 변경 요청: %1").arg(ready ? "준비완료" : "준비해제");

        if (m_networkClient && m_networkClient->isConnected())
        {
            m_networkClient->setPlayerReady(ready);
        }
    }

    void handleGameRoomChatMessage(const QString &message)
    {
        qDebug() << QString::fromUtf8("게임 룸 채팅: %1").arg(message);

        // 서버에만 전송, 내 채팅은 브로드캐스트로 받음
        if (m_networkClient && m_networkClient->isConnected())
        {
            m_networkClient->sendChatMessage(message);
        }
    }

    void handleBlockPlacementRequest(const QString &gameMessage)
    {
        qDebug() << QString::fromUtf8("🎮 블록 배치 요청: %1").arg(gameMessage);

        // 서버에 게임 이동 메시지 전송
        if (m_networkClient && m_networkClient->isConnected())
        {
            m_networkClient->sendMessage(gameMessage);
            qDebug() << QString::fromUtf8("✅ 서버에 블록 배치 메시지 전송 완료");
        }
        else
        {
            qWarning() << QString::fromUtf8("❌ 서버 연결이 없어 블록 배치 메시지를 보낼 수 없습니다");
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

    void onNetworkError(const QString &error)
    {
        qDebug() << QString::fromUtf8("네트워크 오류: %1").arg(error);

        if (m_loginWindow)
        {
            m_loginWindow->setLoginResult(false, QString::fromUtf8("네트워크 오류: %1").arg(error));
        }
    }

    void onGeneralError(const QString &error)
    {
        qDebug() << QString::fromUtf8("서버 에러: %1").arg(error);

        // 특정 에러만 메시지 박스로 표시 (중요한 사용자 액션 관련 에러만)
        if (!error.isEmpty())
        {
            // 사용자가 직접 수행한 액션 관련 에러만 표시
            if (error.contains(QString::fromUtf8("방")) ||
                error.contains(QString::fromUtf8("게임")) ||
                error.contains(QString::fromUtf8("플레이어")) ||
                error.contains(QString::fromUtf8("호스트")) ||
                error.contains(QString::fromUtf8("준비")) ||
                error.contains(QString::fromUtf8("시작")))
            {
                QMessageBox::warning(nullptr, QString::fromUtf8("오류"), error);
            }
            // 시스템 메시지나 파싱 오류는 콘솔 로그만
        }
    }

    void onLoginResult(bool success, const QString &message, const QString &sessionToken)
    {
        if (m_loginWindow)
        {
            m_loginWindow->setLoginResult(success, message);
        }
    }
    
    void onUserProfileReceived(const QString &username, const QString &userInfoJson)
    {
        qDebug() << QString::fromUtf8("사용자 프로필 정보 수신: %1").arg(username);
        
        // JSON 파싱하여 UserInfo 객체 생성
        QJsonParseError error;
        QJsonDocument doc = QJsonDocument::fromJson(userInfoJson.toUtf8(), &error);
        
        if (error.error != QJsonParseError::NoError) {
            qDebug() << QString::fromUtf8("❌ 사용자 프로필 JSON 파싱 오류: %1").arg(error.errorString());
            return;
        }
        
        QJsonObject json = doc.object();
        UserInfo userInfo;
        userInfo.username = QString::fromStdString(json["username"].toString().toStdString());
        userInfo.displayName = QString::fromStdString(json["displayName"].toString().toStdString());
        userInfo.level = json["level"].toInt();
        userInfo.totalGames = json["totalGames"].toInt();
        userInfo.wins = json["wins"].toInt();
        userInfo.losses = json["losses"].toInt();
        userInfo.totalScore = json["totalScore"].toInt();
        userInfo.bestScore = json["bestScore"].toInt();
        userInfo.experience = json["experiencePoints"].toInt();
        userInfo.isOnline = true;
        userInfo.status = QString::fromUtf8("로비");
        
        // 전역 사용자 정보 캐싱
        m_currentUserInfo = userInfo;
        m_currentUsername = username;
        m_currentDisplayname = userInfo.displayName;
        
        // LobbyWindow에 사용자 정보 설정
        if (m_lobbyWindow) {
            m_lobbyWindow->setMyUserInfo(userInfo);
        }
        
        qDebug() << QString::fromUtf8("✅ 사용자 프로필 캐싱 완료: %1 (표시명: %2)")
                    .arg(userInfo.username).arg(userInfo.displayName);
    }

    void onLobbyEntered()
    {
        qDebug() << QString::fromUtf8("로비 입장 성공");
        // 서버에서 로그인 시 자동으로 로비 정보를 전송하므로 별도 요청 불필요
    }

    void onLobbyUserListReceived(const QList<UserInfo> &users)
    {
        qDebug() << QString::fromUtf8("로비 사용자 목록 업데이트: %1명").arg(users.size());
        if (m_lobbyWindow)
        {
            m_lobbyWindow->updateUserList(users);
        }
    }

    void onLobbyUserJoined(const QString &displayname)
    {
        qDebug() << QString::fromUtf8("사용자 로비 입장: %1").arg(displayname);
        if (m_lobbyWindow)
        {
            // 자신의 로그인 브로드캐스트는 시스템 메시지 및 중복 요청 제외
            if (displayname != m_currentDisplayname) {
                if (m_networkClient && m_networkClient->isConnected())
                {
                    m_networkClient->requestLobbyList();
                }
            }
        }
    }

    void onLobbyUserLeft(const QString &username)
    {
        qDebug() << QString::fromUtf8("사용자 로비 퇴장: %1").arg(username);
        if (m_lobbyWindow)
        {
            if (m_networkClient && m_networkClient->isConnected())
            {
                m_networkClient->requestLobbyList();
            }
        }
    }

    void onRoomListReceived(const QStringList &rooms)
    {
        qDebug() << QString::fromUtf8("방 목록 업데이트: %1개").arg(rooms.size());
        if (m_lobbyWindow)
        {
            QList<RoomInfo> roomList;

            // 서버에서 ROOM_LIST:count:room1Data:room2Data... 형식으로 전송됨
            for (const QString &roomData : rooms)
            {
                if (roomData.isEmpty())
                    continue;

                // roomData 파싱: "roomId,roomName,hostName,currentPlayers,maxPlayers,isPrivate,isPlaying,gameMode"
                QStringList parts = roomData.split(',');
                if (parts.size() >= 8)
                {
                    RoomInfo room;
                    room.roomId = parts[0].toInt(); // 서버의 실제 roomId 사용
                    room.roomName = parts[1];       // 서버의 실제 방 이름
                    room.hostName = parts[2];       // 서버의 실제 호스트 이름
                    room.currentPlayers = parts[3].toInt();
                    room.maxPlayers = parts[4].toInt();
                    room.isPrivate = (parts[5] == "1");
                    room.isPlaying = (parts[6] == "1");
                    room.gameMode = parts[7];

                    roomList.append(room);
                }
            }

            m_lobbyWindow->updateRoomList(roomList);
        }
    }

    void onUserStatsReceived(const QString &statsJson)
    {
        qDebug() << QString::fromUtf8("사용자 통계 정보 수신: %1").arg(statsJson);
        
        // JSON 파싱하여 UserInfo 객체 생성
        QJsonParseError error;
        QJsonDocument doc = QJsonDocument::fromJson(statsJson.toUtf8(), &error);
        
        if (error.error != QJsonParseError::NoError) {
            qDebug() << QString::fromUtf8("❌ 사용자 통계 JSON 파싱 오류: %1").arg(error.errorString());
            return;
        }
        
        if (m_lobbyWindow)
        {
            QJsonObject json = doc.object();
            UserInfo userInfo;

            // JSON에서 정보 추출
            userInfo.username = json["username"].toString();
            userInfo.displayName = json["displayName"].toString();
            if (userInfo.displayName.isEmpty()) {
                userInfo.displayName = userInfo.username; // Fallback to username if displayName not found
            }
            userInfo.level = json["level"].toInt();
            userInfo.experience = json["currentExp"].toInt();
            userInfo.requiredExp = json["requiredExp"].toInt();
            userInfo.totalGames = json["totalGames"].toInt();
            userInfo.gamesPlayed = userInfo.totalGames;
            userInfo.wins = json["wins"].toInt();
            userInfo.losses = json["losses"].toInt();
            userInfo.draws = json["draws"].toInt();
            userInfo.winRate = json["winRate"].toDouble();
            userInfo.status = json["status"].toString();
            userInfo.isOnline = true;
            userInfo.averageScore = json["averageScore"].toInt();
            userInfo.totalScore = json["totalScore"].toInt();
            userInfo.bestScore = json["bestScore"].toInt();

            // 자신의 정보인지 다른 사용자의 정보인지 확인
            qDebug() << QString::fromUtf8("사용자 정보 비교: 응답='%1', 현재='%2'").arg(userInfo.username).arg(m_currentUsername);
            
            if (userInfo.username == m_currentUsername) {
                // 자신의 정보면 전역 정보 업데이트 + UI 업데이트 + 모달 표시
                m_currentUserInfo = userInfo;
                qDebug() << QString::fromUtf8("자신의 정보로 판단하여 setMyUserInfo + showUserInfoDialog 호출");
                m_lobbyWindow->setMyUserInfo(userInfo);
                m_lobbyWindow->showUserInfoDialog(userInfo);
            } else {
                // 다른 사용자의 정보면 UserInfoDialog 표시
                qDebug() << QString::fromUtf8("다른 사용자 정보로 판단하여 showUserInfoDialog 호출");
                m_lobbyWindow->showUserInfoDialog(userInfo);
            }
        }
    }

    void onMyStatsUpdated(const QString &statsJson)
    {
        qDebug() << QString::fromUtf8("내 통계 정보 자동 업데이트: %1").arg(statsJson);
        
        // JSON 파싱하여 UserInfo 객체 생성
        QJsonParseError error;
        QJsonDocument doc = QJsonDocument::fromJson(statsJson.toUtf8(), &error);
        
        if (error.error != QJsonParseError::NoError) {
            qDebug() << QString::fromUtf8("❌ 내 통계 JSON 파싱 오류: %1").arg(error.errorString());
            return;
        }
        
        if (m_lobbyWindow)
        {
            QJsonObject json = doc.object();
            UserInfo userInfo;

            // JSON에서 정보 추출
            userInfo.username = json["username"].toString();
            userInfo.displayName = json["displayName"].toString();
            if (userInfo.displayName.isEmpty()) {
                userInfo.displayName = userInfo.username; // Fallback to username if displayName not found
            }
            userInfo.level = json["level"].toInt();
            userInfo.experience = json["currentExp"].toInt();
            userInfo.requiredExp = json["requiredExp"].toInt();
            userInfo.totalGames = json["totalGames"].toInt();
            userInfo.gamesPlayed = userInfo.totalGames;
            userInfo.wins = json["wins"].toInt();
            userInfo.losses = json["losses"].toInt();
            userInfo.draws = json["draws"].toInt();
            userInfo.winRate = json["winRate"].toDouble();
            userInfo.status = json["status"].toString();
            userInfo.isOnline = true;
            userInfo.averageScore = json["averageScore"].toInt();
            userInfo.totalScore = json["totalScore"].toInt();
            userInfo.bestScore = json["bestScore"].toInt();

            // 전역 사용자 정보 업데이트 (자동 업데이트는 모달 표시 없이 UI만 업데이트)
            m_currentUserInfo = userInfo;
            m_lobbyWindow->setMyUserInfo(userInfo);
        }
    }

    void onUserSettingsReceived(const QString &settingsData)
    {
        qDebug() << QString::fromUtf8("사용자 설정 데이터 수신: %1").arg(settingsData);
        
        try {
            // 설정 데이터 파싱
            UserSettings settings = UserSettings::fromServerString(settingsData.split(":"));
            
            // 설정 캐싱
            m_cachedUserSettings = settings;
            
            if (m_isLoadingInitialSettings) {
                // 초기 로딩인 경우: 설정 적용 후 로비 생성
                qDebug() << QString::fromUtf8("초기 설정 로딩 완료 - 설정 적용 중");
                
                applyUserSettings(settings);
                m_isLoadingInitialSettings = false;
                
                // 로비 창 생성
                createLobbyWindow();
                
                // BGM 상태 전환
                transitionToLobbyBGM();
                
                qDebug() << QString::fromUtf8("로비 초기화 완료");
            } else {
                // 설정 버튼 클릭인 경우: 기존 로직 (다이얼로그 표시)
                QWidget *parentWindow = nullptr;
                if (m_gameRoomWindow && m_gameRoomWindow->isVisible()) {
                    parentWindow = m_gameRoomWindow;
                } else if (m_lobbyWindow && m_lobbyWindow->isVisible()) {
                    parentWindow = m_lobbyWindow;
                }
                
                UserSettingsDialog *dialog = new UserSettingsDialog(parentWindow);
                dialog->setCurrentSettings(settings);
                dialog->setAttribute(Qt::WA_DeleteOnClose);
                
                // 실시간 미리보기 시그널 연결 (서버 업데이트 없음)
                connect(dialog, &UserSettingsDialog::settingsChanged, [this](const UserSettings& previewSettings) {
                    // BGM/SFX 미리보기 적용 (서버 업데이트는 하지 않음)
                    qDebug() << QString::fromUtf8("설정 미리보기 적용");
                    applyAudioSettings(previewSettings);
                });
                
                // 최종 설정 저장 시그널 연결 (확인 버튼 클릭 시)
                connect(dialog, &UserSettingsDialog::settingsUpdateRequested, [this](const UserSettings& newSettings) {
                    qDebug() << QString::fromUtf8("설정 업데이트 요청됨");
                    
                    // 캐시된 설정과 비교하여 변경점이 있는지 확인
                    if (hasSettingsChanged(m_cachedUserSettings, newSettings)) {
                        qDebug() << QString::fromUtf8("설정 변경점 발견 - 서버 업데이트 진행");
                        
                        // 설정 적용
                        applyUserSettings(newSettings);
                        
                        // 설정 캐싱 업데이트
                        m_cachedUserSettings = newSettings;
                        
                        // 서버 업데이트
                        QString settingsString = newSettings.toServerString();
                        if (m_networkClient && m_networkClient->isConnected()) {
                            m_networkClient->updateUserSettings(settingsString);
                        } else {
                            QMessageBox::warning(nullptr, QString::fromUtf8("오류"), 
                                               QString::fromUtf8("서버에 연결되지 않았습니다."));
                        }
                    } else {
                        qDebug() << QString::fromUtf8("설정 변경점 없음 - 서버 요청 생략");
                    }
                });
                
                dialog->show();
            }
            
        } catch (const std::exception &e) {
            qDebug() << QString::fromUtf8("설정 데이터 처리 중 오류: %1").arg(e.what());
            
            if (m_isLoadingInitialSettings) {
                // 초기 로딩 실패 시 기본 설정으로 로비 생성
                qWarning() << QString::fromUtf8("초기 설정 로딩 실패 - 기본 설정으로 진행");
                m_isLoadingInitialSettings = false;
                m_cachedUserSettings = UserSettings::getDefaults();
                applyUserSettings(m_cachedUserSettings);
                createLobbyWindow();
                transitionToLobbyBGM();
            } else {
                QMessageBox::warning(nullptr, QString::fromUtf8("오류"), 
                                   QString::fromUtf8("설정을 불러오는 중 오류가 발생했습니다."));
            }
        }
    }

    void onUserSettingsUpdateResult(bool success, const QString &message)
    {
        qDebug() << QString::fromUtf8("설정 업데이트 결과: %1, 메시지: %2")
                        .arg(success ? "성공" : "실패").arg(message);
        
        if (!success) {
            QMessageBox::warning(nullptr, QString::fromUtf8("오류"), 
                               QString::fromUtf8("설정 저장 실패: %1").arg(message));
        }
    }

    // 방 관련 시그널 핸들러들
    void onRoomCreated(int roomId, const QString &roomName)
    {
        qDebug() << QString::fromUtf8("방 생성 성공: %1 (ID: %2)").arg(roomName).arg(roomId);

        // 실제 방 생성 성공 시 GameRoomWindow 생성
        GameRoomInfo gameRoomInfo;
        gameRoomInfo.roomId = roomId;
        gameRoomInfo.roomName = roomName;
        gameRoomInfo.hostUsername = m_currentDisplayname;
        gameRoomInfo.hostColor = PlayerColor::Blue;
        gameRoomInfo.maxPlayers = 4;
        gameRoomInfo.gameMode = QString::fromUtf8("클래식");
        gameRoomInfo.isPlaying = false;

        // 호스트로 설정 (0번 인덱스 = Blue 색상)
        gameRoomInfo.playerSlots[0].username = m_currentDisplayname;
        gameRoomInfo.playerSlots[0].isHost = true;
        gameRoomInfo.playerSlots[0].isReady = true;
        gameRoomInfo.playerSlots[0].color = PlayerColor::Blue;

        createGameRoomWindow(gameRoomInfo, true);

        // 로비에서 방 목록 업데이트
        if (m_networkClient && m_networkClient->isConnected())
        {
            m_networkClient->requestRoomList();
        }
    }

    void onRoomJoined(int roomId, const QString &roomName)
    {
        qDebug() << QString::fromUtf8("방 입장 성공: %1 (ID: %2)").arg(roomName).arg(roomId);

        // 실제 방 입장 성공 시 GameRoomWindow 생성
        GameRoomInfo gameRoomInfo;
        gameRoomInfo.roomId = roomId;
        gameRoomInfo.roomName = roomName;
        gameRoomInfo.hostUsername = QString::fromUtf8("호스트"); // 서버에서 실제 정보 받아야 함
        gameRoomInfo.hostColor = PlayerColor::Blue;
        gameRoomInfo.maxPlayers = 4;
        gameRoomInfo.gameMode = QString::fromUtf8("클래식");
        gameRoomInfo.isPlaying = false;

        // 기본 설정 (서버에서 실제 플레이어 정보를 받아야 함)
        createGameRoomWindow(gameRoomInfo, false);
    }

    void onRoomLeft()
    {
        qDebug() << QString::fromUtf8("방 나가기 성공");

        // 게임 룸 창 닫고 로비로 돌아가기
        if (m_gameRoomWindow)
        {
            m_gameRoomWindow->hide();
            m_gameRoomWindow->deleteLater();
            m_gameRoomWindow = nullptr;
        }

        // 로비 창 다시 표시
        if (m_lobbyWindow)
        {
            m_lobbyWindow->show();
            m_lobbyWindow->raise();
            m_lobbyWindow->activateWindow();
        }

        // 로비에서 방 목록 업데이트
        if (m_networkClient && m_networkClient->isConnected())
        {
            m_networkClient->requestRoomList();
        }

        m_currentRoomInfo = GameRoomInfo();
    }

    void onRoomError(const QString &error)
    {
        qDebug() << QString::fromUtf8("방 오류: %1").arg(error);
        if (m_lobbyWindow)
        {
            m_lobbyWindow->addSystemMessage(QString::fromUtf8("오류: %1").arg(error));
        }
    }

    // 채팅 관련 시그널 핸들러들
    void onChatMessageReceived(const QString &username, const QString &message)
    {
        qDebug() << QString::fromUtf8("채팅 메시지 수신: [%1] %2").arg(username).arg(message);

        // 로비 채팅이면 로비에만 표시 (게임룸과 중복 방지)
        if (m_lobbyWindow && m_lobbyWindow->isVisible() && (!m_gameRoomWindow || !m_gameRoomWindow->isVisible()))
        {
            ChatMessage chatMsg;
            chatMsg.username = username;
            chatMsg.message = message;
            chatMsg.timestamp = QDateTime::currentDateTime();
            chatMsg.type = ChatMessage::Normal;
            m_lobbyWindow->addChatMessage(chatMsg);
        }

        // 게임룸 채팅이면 게임룸에만 표시 (로비와 중복 방지)
        else if (m_gameRoomWindow && m_gameRoomWindow->isVisible())
        {
            bool isSystem = (username == QString::fromUtf8("시스템"));
            m_gameRoomWindow->addChatMessage(username, message, isSystem);
        }
    }

    // displayName 포함 채팅 메시지 처리
    void onChatMessageReceivedWithDisplayName(const QString &username, const QString &displayName, const QString &message)
    {
        qDebug() << QString::fromUtf8("채팅 메시지 수신 (displayName 포함): [%1] (%2) %3").arg(displayName).arg(username).arg(message);

        // GameRoomWindow에 displayName 캐시 업데이트
        if (m_gameRoomWindow) {
            m_gameRoomWindow->updateDisplayNameCache(username, displayName);
        }
        
        // 기존 처리 로직과 동일하게 처리
        onChatMessageReceived(username, message);
    }

    // 로비 채팅 핸들러 추가
    void handleLobbyChatMessage(const QString &message)
    {
        qDebug() << QString::fromUtf8("로비 채팅: %1").arg(message);

        // 서버에만 전송, 내 채팅은 브로드캐스트로 받음
        if (m_networkClient && m_networkClient->isConnected())
        {
            m_networkClient->sendChatMessage(message);
        }
    }

    // 방 목록 새로고침 핸들러 추가
    void handleRefreshRoomListRequest()
    {
        qDebug() << QString::fromUtf8("방 목록 새로고침 요청");

        if (m_networkClient && m_networkClient->isConnected())
        {
            m_networkClient->requestRoomList();
        }
    }

    void handleGetUserStatsRequest(const QString &username)
    {
        qDebug() << QString::fromUtf8("사용자 정보 요청: %1").arg(username);

        if (m_networkClient && m_networkClient->isConnected())
        {
            // USER_STATS:사용자명 형식으로 서버에 요청
            QString message = QString("user:stats:%1").arg(username);
            m_networkClient->sendMessage(message);
        }
    }

    void handleSettingsRequest()
    {
        qDebug() << QString::fromUtf8("사용자 설정 창 열기 요청");

        if (m_networkClient && m_networkClient->isConnected())
        {
            // 서버에서 현재 설정을 가져와서 다이얼로그 표시
            m_networkClient->requestUserSettings();
        }
        else
        {
            QMessageBox::warning(nullptr, QString::fromUtf8("오류"), 
                               QString::fromUtf8("서버에 연결되지 않았습니다."));
        }
    }

    // 방 정보 동기화 핸들러
    void onRoomInfoReceived(const QStringList &roomInfo)
    {
        qDebug() << QString::fromUtf8("ROOM_INFO 메시지 수신: 파라미터 수=%1").arg(roomInfo.size());
        if (roomInfo.size() >= 2)
        {
            qDebug() << QString::fromUtf8("ROOM_INFO 전체 내용: %1").arg(roomInfo.join(":"));
            qDebug() << QString::fromUtf8("ROOM_INFO 메시지 길이: %1").arg(roomInfo.join(":").length());
            for (int i = 0; i < roomInfo.size() && i < 15; ++i)
            {
                qDebug() << QString::fromUtf8("  [%1]: %2").arg(i).arg(roomInfo[i]);
            }
        }

        if (roomInfo.size() < 8)
            return;

        int roomId = roomInfo[1].toInt();
        QString roomName = roomInfo[2];
        QString hostName = roomInfo[3];
        int currentPlayers = roomInfo[4].toInt();
        int maxPlayers = roomInfo[5].toInt();
        bool isPrivate = (roomInfo[6] == "1");
        bool isPlaying = (roomInfo[7] == "1");
        QString gameMode = roomInfo[8];

        // GameRoomInfo 생성
        GameRoomInfo gameRoomInfo;
        gameRoomInfo.roomId = roomId;
        gameRoomInfo.roomName = roomName;
        gameRoomInfo.hostUsername = hostName;
        gameRoomInfo.hostColor = PlayerColor::Blue;
        gameRoomInfo.maxPlayers = maxPlayers;
        gameRoomInfo.gameMode = gameMode;
        gameRoomInfo.isPlaying = isPlaying;

        // 플레이어 정보 파싱 (9번 인덱스부터) - 형식: userId,username,displayName,isHost,isReady,colorIndex
        qDebug() << QString::fromUtf8("플레이어 데이터 파싱 시작: %1개 항목").arg(roomInfo.size() - 9);
        for (int i = 9; i < roomInfo.size(); ++i)
        {
            QStringList playerData = roomInfo[i].split(',');
            qDebug() << QString::fromUtf8("플레이어 %1: %2 (필드 수: %3)").arg(i - 8).arg(roomInfo[i]).arg(playerData.size());

            if (playerData.size() >= 6)
            {
                // 새로운 형식: userId,username,displayName,isHost,isReady,colorIndex
                QString userId = playerData[0];
                QString username = playerData[1];
                QString displayName = playerData[2];
                bool isHost = (playerData[3] == "1");
                bool isReady = (playerData[4] == "1");
                int colorIndex = playerData[5].toInt();

                qDebug() << QString::fromUtf8("  - 사용자: %1 [%2], 색상: %3").arg(displayName).arg(username).arg(colorIndex);

                // 색상 인덱스를 기반으로 정확한 슬롯에 배치 (PlayerColor 1-4를 배열 인덱스 0-3으로 변환)
                // 잘못된 색상 값(11 등)을 1-4 범위로 정규화
                int normalizedColorIndex = ((colorIndex - 1) % 4) + 1;
                if (normalizedColorIndex >= 1 && normalizedColorIndex <= 4)
                {
                    PlayerColor playerColor = static_cast<PlayerColor>(normalizedColorIndex);
                    int slotIndex = normalizedColorIndex - 1; // PlayerColor 1-4를 배열 인덱스 0-3으로 변환

                    qDebug() << QString::fromUtf8("🔧 슬롯 %1에 플레이어 배치: %2 [%3] (색상=%4)")
                                    .arg(slotIndex)
                                    .arg(displayName)
                                    .arg(username)
                                    .arg(colorIndex);

                    gameRoomInfo.playerSlots[slotIndex].username = username;
                    gameRoomInfo.playerSlots[slotIndex].displayName = displayName;
                    gameRoomInfo.playerSlots[slotIndex].isHost = isHost;
                    gameRoomInfo.playerSlots[slotIndex].isReady = isReady;
                    gameRoomInfo.playerSlots[slotIndex].color = playerColor;
                }
            }
            else if (playerData.size() >= 5)
            {
                // 구버전 호환성을 위한 기존 형식 지원: userId,username,isHost,isReady,colorIndex
                QString userId = playerData[0];
                QString username = playerData[1];
                bool isHost = (playerData[2] == "1");
                bool isReady = (playerData[3] == "1");
                int colorIndex = playerData[4].toInt();

                qDebug() << QString::fromUtf8("  - 사용자 (구버전): %1, 색상: %2").arg(username).arg(colorIndex);

                // 색상 인덱스를 기반으로 정확한 슬롯에 배치 (PlayerColor 1-4를 배열 인덱스 0-3으로 변환)
                // 잘못된 색상 값(11 등)을 1-4 범위로 정규화
                int normalizedColorIndex = ((colorIndex - 1) % 4) + 1;
                if (normalizedColorIndex >= 1 && normalizedColorIndex <= 4)
                {
                    PlayerColor playerColor = static_cast<PlayerColor>(normalizedColorIndex);
                    int slotIndex = normalizedColorIndex - 1; // PlayerColor 1-4를 배열 인덱스 0-3으로 변환

                    qDebug() << QString::fromUtf8("🔧 슬롯 %1에 플레이어 배치: %2 (색상=%3)")
                                    .arg(slotIndex)
                                    .arg(username)
                                    .arg(colorIndex);

                    gameRoomInfo.playerSlots[slotIndex].username = username;
                    gameRoomInfo.playerSlots[slotIndex].displayName = username; // Fallback to username
                    gameRoomInfo.playerSlots[slotIndex].isHost = isHost;
                    gameRoomInfo.playerSlots[slotIndex].isReady = isReady;
                    gameRoomInfo.playerSlots[slotIndex].color = playerColor;
                }
            }
            else if (playerData.size() >= 4)
            {
                // 최구버전 호환성을 위한 기존 형식 지원
                QString userId = playerData[0];
                QString username = playerData[1];
                bool isHost = (playerData[2] == "1");
                bool isReady = (playerData[3] == "1");

                // 빈 슬롯 찾아서 플레이어 배치
                for (int slot = 0; slot < 4; ++slot)
                {
                    if (gameRoomInfo.playerSlots[slot].isEmpty())
                    {
                        gameRoomInfo.playerSlots[slot].username = username;
                        gameRoomInfo.playerSlots[slot].displayName = username; // Fallback to username
                        gameRoomInfo.playerSlots[slot].isHost = isHost;
                        gameRoomInfo.playerSlots[slot].isReady = isReady;
                        gameRoomInfo.playerSlots[slot].color = static_cast<PlayerColor>(slot + 1);
                        break;
                    }
                }
            }
        }

        qDebug() << QString::fromUtf8("방 정보 수신: %1 (ID: %2, 플레이어: %3명)")
                        .arg(roomName)
                        .arg(roomId)
                        .arg(currentPlayers);

        // 게임룸 창이 있으면 업데이트
        if (m_gameRoomWindow)
        {
            qDebug() << QString::fromUtf8("GameRoomInfo 업데이트 - 슬롯 상태:");
            for (int i = 0; i < 4; ++i)
            {
                const auto &slot = gameRoomInfo.playerSlots[i];
                qDebug() << QString::fromUtf8("  슬롯 %1: %2, 준비=%3, 호스트=%4")
                                .arg(i)
                                .arg(slot.username)
                                .arg(slot.isReady)
                                .arg(slot.isHost);
            }

            m_gameRoomWindow->updateRoomInfo(gameRoomInfo);
        }
    }

    // 게임룸 상호작용 핸들러들
    void onPlayerJoined(const QString &username)
    {
        qDebug() << QString::fromUtf8("플레이어 방 입장: %1").arg(username);
        if (m_gameRoomWindow)
        {
            QString displayName = m_gameRoomWindow->getDisplayNameFromUsername(username);
            m_gameRoomWindow->addSystemMessage(QString::fromUtf8("%1님이 방에 입장했습니다.").arg(displayName));
        }
    }

    void onPlayerLeft(const QString &username)
    {
        qDebug() << QString::fromUtf8("플레이어 방 퇴장: %1").arg(username);
        // 서버에서 이미 시스템 메시지를 보내므로 클라이언트에서 중복 메시지 제거
        // if (m_gameRoomWindow) {
        //     m_gameRoomWindow->addSystemMessage(QString::fromUtf8("%1님이 방을 나갔습니다.").arg(username));
        // }
    }

    void onPlayerReady(const QString &username, bool ready)
    {
        QString status = ready ? QString::fromUtf8("준비 완료") : QString::fromUtf8("대기 중");
        qDebug() << QString::fromUtf8("플레이어 준비 상태 변경: %1 -> %2").arg(username).arg(status);

        if (m_gameRoomWindow)
        {
            // 내 준비 상태 업데이트 (서버 응답 기준)
            if (username == m_currentUsername)
            {
                m_gameRoomWindow->setMyReadyState(ready);
            }

            // 개별 플레이어의 준비 상태만 업데이트 (전체 룸 정보는 건드리지 않음)
            m_gameRoomWindow->updatePlayerReadyState(username, ready);
            QString displayName = m_gameRoomWindow->getDisplayNameFromUsername(username);
            m_gameRoomWindow->addSystemMessage(QString::fromUtf8("%1님이 %2했습니다.").arg(displayName).arg(status));
        }
    }

    void onHostChanged(const QString &newHost)
    {
        qDebug() << QString::fromUtf8("방장 변경: %1").arg(newHost);
        if (m_gameRoomWindow)
        {
            // display_name으로 변환하여 시스템 메시지 표시
            QString displayName = m_gameRoomWindow->getDisplayNameFromUsername(newHost);
            m_gameRoomWindow->addSystemMessage(QString::fromUtf8("%1님이 새로운 방장이 되었습니다.").arg(displayName));
        }
    }

    // displayName 지원 메서드들
    void onPlayerJoinedWithDisplayName(const QString &username, const QString &displayName)
    {
        qDebug() << QString::fromUtf8("플레이어 방 입장 (displayName 포함): %1 (%2)").arg(username).arg(displayName);
        if (m_gameRoomWindow)
        {
            // displayName 캐시 업데이트
            m_gameRoomWindow->onPlayerJoinedWithDisplayName(username, displayName);
            // 시스템 메시지는 displayName으로 표시
            m_gameRoomWindow->addSystemMessage(QString::fromUtf8("%1님이 방에 입장했습니다.").arg(displayName));
        }
    }

    void onPlayerLeftWithDisplayName(const QString &username, const QString &displayName)
    {
        qDebug() << QString::fromUtf8("플레이어 방 퇴장 (displayName 포함): %1 (%2)").arg(username).arg(displayName);
        if (m_gameRoomWindow)
        {
            // displayName 캐시에서 제거
            m_gameRoomWindow->onPlayerLeftWithDisplayName(username, displayName);
            // 서버에서 이미 시스템 메시지를 보내므로 중복 메시지 제거
            // m_gameRoomWindow->addSystemMessage(QString::fromUtf8("%1님이 방을 나갔습니다.").arg(displayName));
        }
    }

    void onHostChangedWithDisplayName(const QString &username, const QString &displayName)
    {
        qDebug() << QString::fromUtf8("방장 변경 (displayName 포함): %1 (%2)").arg(username).arg(displayName);
        if (m_gameRoomWindow)
        {
            // displayName 캐시 업데이트 및 시스템 메시지는 GameRoomWindow에서 처리
            m_gameRoomWindow->onHostChangedWithDisplayName(username, displayName);
            // 중복 메시지 제거: onHostChangedWithDisplayName() 내부에서 이미 시스템 메시지 처리함
        }
    }

    void onGameStarted()
    {
        qDebug() << QString::fromUtf8("게임 시작!");
        if (m_gameRoomWindow)
        {
            m_gameRoomWindow->startGame();
            // 시스템 메시지는 SYSTEM: 메시지에서 처리하므로 여기서는 제거
        }
    }

    void onGameEnded()
    {
        qDebug() << QString::fromUtf8("게임 종료!");
        if (m_gameRoomWindow)
        {
            // 게임 종료 처리: UI를 대기 상태로 리셋
            m_gameRoomWindow->resetGameToWaitingState();
            // 시스템 메시지는 SYSTEM: 메시지에서 처리하므로 여기서는 제거
        }
    }

    void onGameResult(const QString &resultJson)
    {
        qDebug() << QString::fromUtf8("🎯 게임 결과 수신됨");
        qDebug() << QString::fromUtf8("📦 데이터 크기: %1 바이트").arg(resultJson.size());
        qDebug() << QString::fromUtf8("📄 게임룸창 상태: %1").arg(m_gameRoomWindow ? "활성" : "비활성");

        if (m_gameRoomWindow)
        {
            qDebug() << QString::fromUtf8("✅ 게임 결과 다이얼로그 표시 진행");
            // JSON 파싱 및 게임 결과 다이얼로그 표시
            showGameResultDialog(resultJson);
        }
        else
        {
            qDebug() << QString::fromUtf8("❌ 게임룸창이 없어서 다이얼로그를 표시할 수 없음");
        }
    }

    void onGameReset()
    {
        qDebug() << QString::fromUtf8("🔄 게임 리셋 신호 수신됨");

        if (m_gameRoomWindow)
        {
            qDebug() << QString::fromUtf8("✅ 게임룸 UI 리셋 진행");
            // 게임룸 창의 모든 게임 관련 UI를 리셋
            m_gameRoomWindow->resetGameState();
        }
        else
        {
            qDebug() << QString::fromUtf8("❌ 게임룸창이 없어서 리셋할 수 없음");
        }
    }

private:
    // 설정 비교 함수
    bool hasSettingsChanged(const UserSettings& oldSettings, const UserSettings& newSettings) const
    {
        return (oldSettings.theme != newSettings.theme ||
                oldSettings.language != newSettings.language ||
                oldSettings.bgmMute != newSettings.bgmMute ||
                oldSettings.bgmVolume != newSettings.bgmVolume ||
                oldSettings.effectMute != newSettings.effectMute ||
                oldSettings.effectVolume != newSettings.effectVolume ||
                oldSettings.gameInviteNotifications != newSettings.gameInviteNotifications);
    }

    // 오디오 설정만 적용 (미리보기용)
    void applyAudioSettings(const UserSettings& settings)
    {
        BGMManager& bgm = BGMManager::getInstance();
        bgm.setBGMVolume(settings.bgmVolume / 100.0f);
        bgm.setBGMMuted(settings.bgmMute);
        bgm.setSFXVolume(settings.effectVolume / 100.0f);
        bgm.setSFXMuted(settings.effectMute);
    }

    // 설정 적용 함수
    void applyUserSettings(const UserSettings& settings)
    {
        qDebug() << QString::fromUtf8("사용자 설정 적용 시작: theme=%1, bgmMute=%2, bgmVolume=%3, sfxMute=%4, sfxVolume=%5")
                        .arg(settings.theme == ThemeType::Dark ? "dark" : "light").arg(settings.bgmMute ? "true" : "false")
                        .arg(settings.bgmVolume).arg(settings.effectMute ? "true" : "false")
                        .arg(settings.effectVolume);
        
        // BGM 설정 적용
        BGMManager& bgm = BGMManager::getInstance();
        bgm.setBGMVolume(settings.bgmVolume / 100.0f);  // 0-100 → 0.0-1.0
        bgm.setBGMMuted(settings.bgmMute);
        
        // SFX 설정 적용
        bgm.setSFXVolume(settings.effectVolume / 100.0f);  // 0-100 → 0.0-1.0
        bgm.setSFXMuted(settings.effectMute);
        
        qDebug() << QString::fromUtf8("BGM/SFX 설정 적용 완료");
        qDebug() << QString::fromUtf8("사용자 설정 적용 완료");
    }

    // 🎵 BGM 상태 전환 헬퍼 함수들
    void transitionToLobbyBGM()
    {
        qDebug() << "🎵 Transitioning to Lobby BGM";
        BGMManager::getInstance().onLobbyEntered();
    }
    
    void transitionToGameRoomBGM()
    {
        qDebug() << "🎵 Transitioning to Game Room BGM";
        BGMManager::getInstance().onGameRoomEntered();
    }

    void showGameResultDialog(const QString &resultJson)
    {
        try
        {
            qDebug() << QString::fromUtf8("📨 게임 결과 다이얼로그 표시 시작");
            qDebug() << QString::fromUtf8("📋 수신된 JSON: %1").arg(resultJson);

            // JSON 파싱
            QJsonParseError error;
            QJsonDocument doc = QJsonDocument::fromJson(resultJson.toUtf8(), &error);

            if (error.error != QJsonParseError::NoError)
            {
                qDebug() << QString::fromUtf8("❌ JSON 파싱 오류: %1").arg(error.errorString());
                qDebug() << QString::fromUtf8("❌ 오류 위치: offset %1").arg(error.offset);

                // 파싱 오류가 있어도 기본 메시지 표시
                showFallbackGameResult();
                return;
            }

            qDebug() << QString::fromUtf8("✅ JSON 파싱 성공");

            QJsonObject result = doc.object();
            QJsonObject scores = result["scores"].toObject();
            QJsonArray winners = result["winners"].toArray();

            qDebug() << QString::fromUtf8("📊 점수 데이터: %1개").arg(scores.size());
            qDebug() << QString::fromUtf8("🏆 승자 데이터: %1명").arg(winners.size());

            // 결과 메시지 생성
            QString resultMessage = QString::fromUtf8("🎉 게임이 종료되었습니다!\n\n");

            // 점수 표시
            resultMessage += QString::fromUtf8("📊 최종 점수:\n");
            for (auto it = scores.begin(); it != scores.end(); ++it)
            {
                QString playerName = it.key();
                int score = it.value().toInt();
                QString displayName = m_gameRoomWindow->getDisplayNameFromUsername(playerName);
                resultMessage += QString::fromUtf8("  %1: %2점\n").arg(displayName).arg(score);
            }

            // 승자 표시
            resultMessage += QString::fromUtf8("\n🏆 승리자: ");
            if (winners.size() == 1)
            {
                QString winnerDisplayName = m_gameRoomWindow->getDisplayNameFromUsername(winners[0].toString());
                resultMessage += winnerDisplayName + QString::fromUtf8("님!");
            }
            else if (winners.size() > 1)
            {
                QStringList winnerNames;
                for (int i = 0; i < winners.size(); ++i)
                {
                    QString winnerDisplayName = m_gameRoomWindow->getDisplayNameFromUsername(winners[i].toString());
                    winnerNames << winnerDisplayName;
                }
                resultMessage += winnerNames.join(", ") + QString::fromUtf8("님들! (동점)");
            }
            else
            {
                resultMessage += QString::fromUtf8("없음");
            }

            // 비모달 다이얼로그 생성
            QMessageBox *msgBox = new QMessageBox(m_gameRoomWindow);
            msgBox->setWindowTitle(QString::fromUtf8("게임 결과"));
            msgBox->setText(resultMessage);
            msgBox->setIcon(QMessageBox::Information);
            msgBox->setWindowModality(Qt::NonModal);    // 비모달로 설정
            msgBox->setAttribute(Qt::WA_DeleteOnClose); // 닫힐 때 자동 삭제

            // 닫기 버튼만 추가 (기존의 계속하기/방나가기 버튼 제거)
            msgBox->setStandardButtons(QMessageBox::Close);
            msgBox->setButtonText(QMessageBox::Close, QString::fromUtf8("닫기"));

            // 10초 후 자동 닫기 타이머 설정
            QTimer *autoCloseTimer = new QTimer(msgBox);
            autoCloseTimer->setSingleShot(true);
            autoCloseTimer->setInterval(10000); // 10초

            connect(autoCloseTimer, &QTimer::timeout, msgBox, &QMessageBox::close);
            connect(msgBox, &QMessageBox::finished, autoCloseTimer, &QTimer::deleteLater);

            // 다이얼로그 표시
            msgBox->show();
            autoCloseTimer->start();

            qDebug() << QString::fromUtf8("✅ 비모달 게임 결과 다이얼로그 표시됨 (10초 후 자동 닫기)");
        }
        catch (const std::exception &e)
        {
            qDebug() << QString::fromUtf8("❌ 게임 결과 처리 중 예외 발생: %1").arg(e.what());
            showFallbackGameResult();
        }
        catch (...)
        {
            qDebug() << QString::fromUtf8("❌ 게임 결과 처리 중 알 수 없는 예외 발생");
            showFallbackGameResult();
        }
    }

    void showFallbackGameResult()
    {
        qDebug() << QString::fromUtf8("🔄 기본 게임 결과 다이얼로그 표시");

        try
        {
            QMessageBox msgBox;
            msgBox.setWindowTitle(QString::fromUtf8("게임 종료"));
            msgBox.setText(QString::fromUtf8("🎉 게임이 종료되었습니다!\n\n결과 정보를 표시할 수 없습니다."));
            msgBox.setIcon(QMessageBox::Information);

            // 버튼 추가
            QPushButton *continueBtn = msgBox.addButton(QString::fromUtf8("계속하기"), QMessageBox::AcceptRole);
            QPushButton *leaveBtn = msgBox.addButton(QString::fromUtf8("방 나가기"), QMessageBox::RejectRole);

            msgBox.setDefaultButton(continueBtn);

            // 다이얼로그 표시 및 결과 처리
            msgBox.exec();

            if (msgBox.clickedButton() == continueBtn)
            {
                qDebug() << QString::fromUtf8("플레이어가 계속하기를 선택 (기본 다이얼로그)");
                if (m_networkClient)
                {
                    m_networkClient->sendMessage("game:result:CONTINUE");
                    QThread::msleep(100);
                }
            }
            else if (msgBox.clickedButton() == leaveBtn)
            {
                qDebug() << QString::fromUtf8("플레이어가 방 나가기를 선택 (기본 다이얼로그)");
                if (m_networkClient)
                {
                    m_networkClient->sendMessage("game:result:LEAVE");
                    QThread::msleep(100);
                }
            }
        }
        catch (...)
        {
            qDebug() << QString::fromUtf8("❌ 기본 다이얼로그 표시 중에도 예외 발생");
        }
    }

    void initializeApplication()
    {
        qDebug() << QString::fromUtf8("=== 블로커스 온라인 초기화 ===");
    }

    void initializeConfiguration()
    {
        qDebug() << QString::fromUtf8("=== AppController 설정 로드 ===");
        
        // 설정 시스템은 main에서 이미 초기화됨
        auto& config = ClientConfigManager::instance();
        
        qDebug() << QString::fromUtf8("설정 상태 확인:");
        qDebug() << QString::fromUtf8("  서버: %1:%2").arg(config.getServerHost()).arg(config.getServerPort());
        qDebug() << QString::fromUtf8("  디버그 모드: %1").arg(config.isDebugMode() ? "활성" : "비활성");
        qDebug() << QString::fromUtf8("  로그 레벨: %1").arg(config.getLogLevel());
    }

    void setupNetworkClient()
    {
        // 설정을 사용하여 NetworkClient 생성
        m_networkClient = new NetworkClient(this);
        
        // 네트워크 연결 상태 시그널 - Qt::QueuedConnection으로 스레드 안전 보장
        connect(m_networkClient, &NetworkClient::connected,
                this, &AppController::onNetworkConnected, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::disconnected,
                this, &AppController::onNetworkDisconnected, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::connectionError,
                this, &AppController::onNetworkError, Qt::QueuedConnection);

        // 인증 관련 시그널 - Qt::QueuedConnection으로 스레드 안전 보장
        connect(m_networkClient, &NetworkClient::loginResult,
                this, &AppController::onLoginResult, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::userProfileReceived,
                this, &AppController::onUserProfileReceived, Qt::QueuedConnection);

        // 일반 에러 시그널 추가 - Qt::QueuedConnection으로 스레드 안전 보장
        connect(m_networkClient, &NetworkClient::errorReceived,
                this, &AppController::onGeneralError, Qt::QueuedConnection);

        // 로비 관련 시그널 - Qt::QueuedConnection으로 스레드 안전 보장
        connect(m_networkClient, &NetworkClient::lobbyEntered,
                this, &AppController::onLobbyEntered, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::lobbyUserListReceived,
                this, &AppController::onLobbyUserListReceived, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::lobbyUserJoined,
                this, &AppController::onLobbyUserJoined, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::lobbyUserLeft,
                this, &AppController::onLobbyUserLeft, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::roomListReceived,
                this, &AppController::onRoomListReceived, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::userStatsReceived,
                this, &AppController::onUserStatsReceived, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::myStatsUpdated,
                this, &AppController::onMyStatsUpdated, Qt::QueuedConnection);

        // 설정 관련 시그널 - Qt::QueuedConnection으로 스레드 안전 보장
        connect(m_networkClient, &NetworkClient::userSettingsReceived,
                this, &AppController::onUserSettingsReceived, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::userSettingsUpdateResult,
                this, &AppController::onUserSettingsUpdateResult, Qt::QueuedConnection);

        // 방 관련 시그널 추가
        connect(m_networkClient, &NetworkClient::roomCreated, this, [this](int roomId, const QString& roomName) {
            onRoomCreated(roomId, roomName);
            transitionToGameRoomBGM();  // 🎵 방 생성 성공 → 게임룸 BGM
        }, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::roomJoined, this, [this](int roomId, const QString& roomName) {
            onRoomJoined(roomId, roomName);
            transitionToGameRoomBGM();  // 🎵 방 참여 성공 → 게임룸 BGM
        }, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::roomLeft, this, [this]() {
            onRoomLeft();
            transitionToLobbyBGM();  // 🎵 방 나가기 성공 → 로비 BGM
        }, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::lobbyLeft,
                this, &AppController::onRoomLeft, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::roomError,
                this, &AppController::onRoomError, Qt::QueuedConnection);

        // 채팅 관련 시그널 - Qt::QueuedConnection으로 스레드 안전 보장
        connect(m_networkClient, &NetworkClient::chatMessageReceived,
                this, &AppController::onChatMessageReceived, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::chatMessageReceivedWithDisplayName,
                this, &AppController::onChatMessageReceivedWithDisplayName, Qt::QueuedConnection);

        // 방 정보 동기화 시그널 - Qt::QueuedConnection으로 스레드 안전 보장
        connect(m_networkClient, &NetworkClient::roomInfoReceived,
                this, &AppController::onRoomInfoReceived, Qt::QueuedConnection);

        // 게임룸 상호작용 시그널 - Qt::QueuedConnection으로 스레드 안전 보장
        connect(m_networkClient, &NetworkClient::playerJoined,
                this, &AppController::onPlayerJoined, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::playerLeft,
                this, &AppController::onPlayerLeft, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::playerReady,
                this, &AppController::onPlayerReady, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::hostChanged,
                this, &AppController::onHostChanged, Qt::QueuedConnection);
        
        // displayName 지원 시그널들 - Qt::QueuedConnection으로 스레드 안전 보장
        connect(m_networkClient, &NetworkClient::playerJoinedWithDisplayName,
                this, &AppController::onPlayerJoinedWithDisplayName, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::playerLeftWithDisplayName,
                this, &AppController::onPlayerLeftWithDisplayName, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::hostChangedWithDisplayName,
                this, &AppController::onHostChangedWithDisplayName, Qt::QueuedConnection);
        
        connect(m_networkClient, &NetworkClient::gameStarted,
                this, &AppController::onGameStarted, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::gameEnded,
                this, &AppController::onGameEnded, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::gameResult,
                this, &AppController::onGameResult, Qt::QueuedConnection);
        connect(m_networkClient, &NetworkClient::gameReset,
                this, &AppController::onGameReset, Qt::QueuedConnection);

        qDebug() << QString::fromUtf8("네트워크 클라이언트 설정 완료");
    }

    void createLoginWindow()
    {
        qDebug() << QString::fromUtf8("로그인 창 생성");

        m_loginWindow = new Blokus::LoginWindow();
        
        // 설정에서 창 크기 적용
        // auto& config = ClientConfigManager::instance();
        // const auto& windowConfig = config.getClientConfig().window;
        // m_loginWindow->resize(windowConfig.width, windowConfig.height);
        // m_loginWindow->setMinimumSize(windowConfig.min_width, windowConfig.min_height);

        // 로그인 시그널 연결 (Qt::QueuedConnection으로 스레드 안전성 보장)
        connect(m_loginWindow, &Blokus::LoginWindow::loginRequested,
                this, &AppController::handleLoginRequest, Qt::QueuedConnection);
        connect(m_loginWindow, &Blokus::LoginWindow::jwtLoginRequested,
                this, &AppController::handleJwtLoginRequest, Qt::QueuedConnection);
        connect(m_loginWindow, &Blokus::LoginWindow::loginSuccessful, this, [this](const QString& username) {
            handleLoginSuccess(username);
            transitionToLobbyBGM();  // 🎵 로그인 성공 → 로비 BGM
        }, Qt::QueuedConnection);

        // 로그인 창이 닫히면 애플리케이션 종료
        connect(m_loginWindow, &QMainWindow::destroyed,
                qApp, &QApplication::quit);

        m_loginWindow->show();
    }

    void createLobbyWindow()
    {
        qDebug() << QString::fromUtf8("로비 창 생성 시작");

        try
        {
            m_lobbyWindow = new Blokus::LobbyWindow(m_currentUsername, m_currentDisplayname);
            
            // 설정에서 창 크기 적용
            auto& config = ClientConfigManager::instance();
            const auto& windowConfig = config.getClientConfig().window;
            m_lobbyWindow->resize(windowConfig.width, windowConfig.height);
            m_lobbyWindow->setMinimumSize(windowConfig.min_width, windowConfig.min_height);

            // 로비 시그널 연결
            connect(m_lobbyWindow, &Blokus::LobbyWindow::logoutRequested,
                    this, &AppController::handleLogoutRequest);
            connect(m_lobbyWindow, &Blokus::LobbyWindow::createRoomRequested,
                    this, &AppController::handleCreateRoomRequest);
            connect(m_lobbyWindow, &Blokus::LobbyWindow::joinRoomRequested,
                    this, &AppController::handleJoinRoomRequest);
            connect(m_lobbyWindow, &Blokus::LobbyWindow::sendChatMessageRequested,
                    this, &AppController::handleLobbyChatMessage);
            connect(m_lobbyWindow, &Blokus::LobbyWindow::refreshRoomListRequested,
                    this, &AppController::handleRefreshRoomListRequest);
            connect(m_lobbyWindow, &Blokus::LobbyWindow::getUserStatsRequested,
                    this, &AppController::handleGetUserStatsRequest);
            connect(m_lobbyWindow, &Blokus::LobbyWindow::settingsRequested,
                    this, &AppController::handleSettingsRequest);

            m_lobbyWindow->show();
            m_lobbyWindow->raise();
            m_lobbyWindow->activateWindow();

            // 로비 진입 시 자동으로 사용자 목록과 방 목록 갱신 요청
            if (m_networkClient && m_networkClient->isConnected()) {
                qDebug() << QString::fromUtf8("로비 진입: 사용자 목록 및 방 목록 자동 갱신 요청");
                QTimer::singleShot(100, [this]() {
                    m_networkClient->requestLobbyList();  // 사용자 목록 갱신
                    m_networkClient->requestRoomList();   // 방 목록 갱신
                });
            }

            qDebug() << QString::fromUtf8("로비 창 생성 완료");
        }
        catch (const std::exception &e)
        {
            qDebug() << QString::fromUtf8("로비 창 생성 실패: %1").arg(e.what());
        }
        catch (...)
        {
            qDebug() << QString::fromUtf8("로비 창 생성 중 알 수 없는 오류");
        }
    }

    void createGameRoomWindow(const GameRoomInfo &roomInfo, bool isHost)
    {
        qDebug() << QString::fromUtf8("게임 룸 창 생성: 방 %1, 호스트: %2")
                        .arg(roomInfo.roomId)
                        .arg(isHost);

        try
        {
            // 로비 창 숨기기
            if (m_lobbyWindow)
            {
                m_lobbyWindow->hide();
            }

            // 기존 게임 룸 창이 있으면 제거
            if (m_gameRoomWindow)
            {
                m_gameRoomWindow->deleteLater();
            }

            // 새 게임 룸 창 생성
            m_gameRoomWindow = new Blokus::GameRoomWindow(roomInfo, m_currentUsername, m_currentDisplayname);
            m_currentRoomInfo = roomInfo;
            
            // 설정에서 창 크기 적용
            auto& config = ClientConfigManager::instance();
            const auto& windowConfig = config.getClientConfig().window;
            m_gameRoomWindow->resize(windowConfig.width, windowConfig.height);
            m_gameRoomWindow->setMinimumSize(windowConfig.min_width, windowConfig.min_height);

            // 게임 룸 시그널 연결
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::leaveRoomRequested,
                    this, &AppController::handleLeaveRoomRequest);
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::gameStartRequested,
                    this, &AppController::handleGameStartRequest);
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::playerReadyChanged,
                    this, &AppController::handlePlayerReadyChanged);
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::chatMessageSent,
                    this, &AppController::handleGameRoomChatMessage);
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::blockPlacementRequested,
                    this, &AppController::handleBlockPlacementRequest);
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::settingsRequested,
                    this, &AppController::handleSettingsRequest);

            // 게임 상태 동기화 시그널 연결 - Qt::QueuedConnection으로 스레드 안전 보장
            connect(m_networkClient, &Blokus::NetworkClient::gameStateUpdated,
                    m_gameRoomWindow, &Blokus::GameRoomWindow::onGameStateUpdated, Qt::QueuedConnection);
            connect(m_networkClient, &Blokus::NetworkClient::blockPlaced,
                    m_gameRoomWindow, &Blokus::GameRoomWindow::onBlockPlaced, Qt::QueuedConnection);
            connect(m_networkClient, &Blokus::NetworkClient::turnChanged,
                    m_gameRoomWindow, &Blokus::GameRoomWindow::onTurnChanged, Qt::QueuedConnection);
            qDebug() << QString::fromUtf8("⏰ [TIMER_DEBUG] turnChanged 시그널 연결 완료");

            // AFK 관련 시그널 연결 - Qt::QueuedConnection으로 스레드 안전 보장
            connect(m_networkClient, &Blokus::NetworkClient::afkModeActivated,
                    m_gameRoomWindow, &Blokus::GameRoomWindow::onAfkModeActivated, Qt::QueuedConnection);
            connect(m_gameRoomWindow, &Blokus::GameRoomWindow::afkUnblockRequested,
                    m_networkClient, &Blokus::NetworkClient::sendAfkUnblock, Qt::QueuedConnection);
            
            // 🔥 FIX: 게임 종료 시 AFK 모달 처리 - Qt::QueuedConnection으로 스레드 안전 보장
            connect(m_networkClient, &Blokus::NetworkClient::gameEnded,
                    m_gameRoomWindow, &Blokus::GameRoomWindow::onGameEndedForAfk, Qt::QueuedConnection);
            
            // 🔥 FIX: AFK 해제 에러 처리 - Qt::QueuedConnection으로 스레드 안전 보장
            connect(m_networkClient, &Blokus::NetworkClient::afkUnblockError,
                    m_gameRoomWindow, &Blokus::GameRoomWindow::onAfkUnblockErrorForAfk, Qt::QueuedConnection);
            
            qDebug() << QString::fromUtf8("🚨 AFK 관련 시그널 연결 완료 (게임 종료 & 에러 처리 포함)");

            // 게임룸 채팅은 이미 전역적으로 연결되어 있음 (중복 연결 제거)

            m_gameRoomWindow->show();
            m_gameRoomWindow->raise();
            m_gameRoomWindow->activateWindow();

            qDebug() << QString::fromUtf8("게임 룸 창 생성 완료");
        }
        catch (const std::exception &e)
        {
            qDebug() << QString::fromUtf8("게임 룸 창 생성 실패: %1").arg(e.what());
        }
        catch (...)
        {
            qDebug() << QString::fromUtf8("게임 룸 창 생성 중 알 수 없는 오류");
        }
    }

    void cleanupWindows()
    {
        if (m_loginWindow)
        {
            delete m_loginWindow;
            m_loginWindow = nullptr;
        }

        if (m_lobbyWindow)
        {
            delete m_lobbyWindow;
            m_lobbyWindow = nullptr;
        }

        if (m_gameRoomWindow)
        {
            delete m_gameRoomWindow;
            m_gameRoomWindow = nullptr;
        }
    }

private:
    Blokus::LoginWindow *m_loginWindow;
    Blokus::LobbyWindow *m_lobbyWindow;
    Blokus::GameRoomWindow *m_gameRoomWindow;
    Blokus::NetworkClient *m_networkClient;
    QString m_currentUsername;
    QString m_currentDisplayname;
    UserInfo m_currentUserInfo;
    Blokus::GameRoomInfo m_currentRoomInfo;
    
    // 설정 캐싱 관련
    bool m_isLoadingInitialSettings;
    UserSettings m_cachedUserSettings;
};

#ifdef _WIN32
// Windows에서 WinMain을 직접 구현
int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    Q_UNUSED(hInstance);
    Q_UNUSED(hPrevInstance); 
    Q_UNUSED(nCmdShow);
    
    int argc = 0;
    char** argv = nullptr;
    
    // 명령줄 인수 파싱
    LPWSTR* szArglist = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (szArglist != nullptr) {
        argv = new char*[argc];
        for (int i = 0; i < argc; i++) {
            int len = WideCharToMultiByte(CP_UTF8, 0, szArglist[i], -1, nullptr, 0, nullptr, nullptr);
            argv[i] = new char[len];
            WideCharToMultiByte(CP_UTF8, 0, szArglist[i], -1, argv[i], len, nullptr, nullptr);
        }
        LocalFree(szArglist);
    }
    
    QApplication app(argc, argv);
    
    // 메모리 정리
    if (argv) {
        for (int i = 0; i < argc; i++) {
            delete[] argv[i];
        }
        delete[] argv;
    }
#else
int main(int argc, char *argv[])
{
    QApplication app(argc, argv);
#endif

#ifdef _WIN32
    // Windows에서 디버그 모드일 때 콘솔 창 강제 할당
    #if defined(_DEBUG) || defined(DEBUG)
        // 콘솔이 이미 할당되어 있지 않다면 새로 할당
        if (GetConsoleWindow() == NULL) {
            if (AllocConsole()) {
                FILE* pCout;
                FILE* pCerr;
                FILE* pCin;
                
                freopen_s(&pCout, "CONOUT$", "w", stdout);
                freopen_s(&pCerr, "CONOUT$", "w", stderr);
                freopen_s(&pCin, "CONIN$", "r", stdin);
                
                // 콘솔 창 제목 설정
                SetConsoleTitleA("Blokus Client Debug Console");
                
                // C++ iostream도 콘솔로 리다이렉트
                std::ios::sync_with_stdio(true);
                std::wcout.clear();
                std::cout.clear();
                std::wcerr.clear();
                std::cerr.clear();
                std::wcin.clear();
                std::cin.clear();
            }
        }
        qDebug() << "=== 디버그 콘솔 활성화됨 ===";
    #endif
#endif

    // 애플리케이션 설정
    app.setApplicationName(QString::fromUtf8("블로커스 온라인"));
    app.setApplicationVersion("1.2.0");
    app.setOrganizationName("Blokus Online");

    // Qt Meta Type 등록 (Signal-Slot에서 사용자 정의 타입 전달을 위해 필요)
    qRegisterMetaType<UserInfo>("UserInfo");
    qRegisterMetaType<QList<UserInfo>>("QList<UserInfo>");
    qRegisterMetaType<RoomInfo>("RoomInfo");
    qRegisterMetaType<QList<RoomInfo>>("QList<RoomInfo>");
    qRegisterMetaType<ChatMessage>("ChatMessage");

    // 설정 시스템 초기화
    auto& config = ClientConfigManager::instance();
    if (!config.initialize()) {
        qWarning() << QString::fromUtf8("설정 초기화 실패 - 기본값 사용");
    }

    // 설정에서 폰트 크기 읽어서 적용
    int fontSize = config.getClientConfig().ui.font_size;
    QFont defaultFont("맑은 고딕", fontSize);
    if (!defaultFont.exactMatch())
    {
        defaultFont = QFont("굴림", fontSize);
    }
    app.setFont(defaultFont);

    // 앱 컨트롤러 생성 및 시작
    AppController controller;
    controller.start();

    qDebug() << QString::fromUtf8("블로커스 온라인 시작됨 - 클래식 모드 전용");

    return app.exec();
}

#include "main.moc"