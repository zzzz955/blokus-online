#pragma once

#include <QMainWindow>
#include <QWidget>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QGridLayout>
#include <QSplitter>
#include <QLabel>
#include <QLineEdit>
#include <QPushButton>
#include <QListWidget>
#include <QTableWidget>
#include <QTextEdit>
#include <QGroupBox>
#include <QFrame>
#include <QComboBox>
#include <QSpinBox>
#include <QCheckBox>
#include <QProgressBar>
#include <QTimer>
#include <QScrollArea>
#include <QTabWidget>
#include <QDialog>
#include <QDialogButtonBox>
#include <QDateTime>
#include <QDebug>

#include "ClientTypes.h"  // 🔥 Types.h에서 UserInfo, RoomInfo 등을 가져옴

namespace Blokus {
    // 전방 선언
    class UserInfoDialog;
    // 방 생성 다이얼로그
    class CreateRoomDialog : public QDialog
    {
        Q_OBJECT

    public:
        explicit CreateRoomDialog(QWidget* parent = nullptr);

        RoomInfo getRoomInfo() const;

    private slots:
        void onGameModeChanged();
        void onPrivateToggled(bool enabled);

    private:
        void setupUI();
        void setupStyles();

    private:
        QLineEdit* m_roomNameEdit;
        QComboBox* m_gameModeCombo;
        QSpinBox* m_maxPlayersSpinBox;
        QCheckBox* m_privateCheckBox;
        QLineEdit* m_passwordEdit;
        QDialogButtonBox* m_buttonBox;
    };

    // 메인 로비 윈도우
    class LobbyWindow : public QMainWindow
    {
        Q_OBJECT

    public:
        explicit LobbyWindow(const QString& username, QWidget* parent = nullptr);
        ~LobbyWindow();

        // 데이터 업데이트 함수들
        void updateUserList(const QList<UserInfo>& users);
        void updateRoomList(const QList<RoomInfo>& rooms);
        void updateRanking(const QList<UserInfo>& ranking);
        void addChatMessage(const ChatMessage& message);
        void setMyUserInfo(const UserInfo& userInfo);
        void showUserInfoDialog(const UserInfo& userInfo); // 서버 응답 후 모달 표시

    signals:
        // 서버 통신 시그널들
        void createRoomRequested(const RoomInfo& roomInfo);
        void joinRoomRequested(int roomId, const QString& password = "");
        void refreshRoomListRequested();
        void sendChatMessageRequested(const QString& message);
        void logoutRequested();
        void gameStartRequested(); // 게임 시작 (방에 입장한 상태에서)
        void getUserStatsRequested(const QString& username); // 사용자 상세 정보 요청
        void addFriendRequested(const QString& username); // 친구 추가 요청
        void sendWhisperRequested(const QString& username); // 귓속말 요청
        
        // 설정 관련 시그널
        void settingsRequested(); // 설정 창 열기 요청

    private slots:
        // UI 이벤트 핸들러
        void onCreateRoomClicked();
        void onJoinRoomClicked();
        void onRefreshRoomListClicked();
        void onRoomDoubleClicked();
        void onChatSendClicked();
        void onChatReturnPressed();
        void onLogoutClicked();
        void onUserDoubleClicked();
        void onTabChanged(int index);
        void onCooldownTimerTick();
        
        // 설정 관련 슬롯
        void onSettingsClicked();
        
        // UserInfoDialog 관련 슬롯
        void onUserInfoDialogRequested(const QString& username);
        void onUserInfoDialogClosed();

        // 타이머 이벤트
        void onRefreshTimer();

    protected:
        // 이벤트 핸들러
        void closeEvent(QCloseEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;

    private:
        // UI 구성 함수들
        void setupUI();
        void setupMenuBar();
        void setupStatusBar();
        void setupMainLayout();
        void setupInfoPanel();        // 상단 정보 패널
        void setupLeftPanel();        // 사용자 목록, 랭킹
        void setupCenterPanel();      // 방 목록
        void setupRightPanel();       // 채팅
        void setupStyles();

        // UI 업데이트 함수들
        void updateRoomListDisplay();
        void updateUserListDisplay();
        void updateRankingDisplay();
        void updateUserStatsDisplay(); // 사용자 통계 표시

        // 유틸리티 함수들
        void scrollChatToBottom();
        QString formatChatMessage(const ChatMessage& message);
        QString formatUserStatus(const UserInfo& user);
        QString formatRoomStatus(const RoomInfo& room);

    public:
        void addSystemMessage(const QString& message);
        const QString& getMyUsername() const { return m_myUsername; }

        // 버튼 쿨다운 관리
        QTimer* m_buttonCooldownTimer;
        QSet<QPushButton*> m_cooldownButtons;
        static constexpr int BUTTON_COOLDOWN_MS = 500; // 0.5초 쿨다운

        void setButtonCooldown(QPushButton* button);
        void enableCooldownButton(QPushButton* button);

    private:
        // 사용자 정보
        QString m_myUsername;
        UserInfo m_myUserInfo;

        // 메인 레이아웃
        QWidget* m_centralWidget;
        QSplitter* m_mainSplitter;

        // 왼쪽 패널 (사용자 목록, 랭킹)
        QWidget* m_leftPanel;
        QTabWidget* m_leftTabs;
        QWidget* m_usersTab;
        QWidget* m_rankingTab;
        QListWidget* m_userList;
        QTableWidget* m_rankingTable;
        QLabel* m_onlineCountLabel;

        // 중앙 패널 (방 목록)
        QWidget* m_centerPanel;
        QTableWidget* m_roomTable;
        QWidget* m_roomControlsWidget;
        QPushButton* m_createRoomButton;
        QPushButton* m_joinRoomButton;
        QPushButton* m_refreshRoomButton;

        // 오른쪽 패널 (채팅)
        QWidget* m_rightPanel;
        QTextEdit* m_chatDisplay;
        QWidget* m_chatInputWidget;
        QLineEdit* m_chatInput;
        QPushButton* m_chatSendButton;

        // 상단 정보 패널
        QWidget* m_infoPanel;
        QLabel* m_welcomeLabel;
        QLabel* m_userStatsLabel;
        QProgressBar* m_expProgressBar;
        QLabel* m_expLabel;
        QPushButton* m_settingsButton;  // 설정 버튼
        QPushButton* m_logoutButton;

        // 데이터 저장소
        QList<UserInfo> m_userList_data;
        QList<RoomInfo> m_roomList_data;
        QList<UserInfo> m_ranking_data;
        QList<ChatMessage> m_chatHistory;

        // 타이머
        QTimer* m_refreshTimer;

        // 현재 선택된 방
        int m_selectedRoomId;
        
        // 사용자 정보 다이얼로그
        UserInfoDialog* m_currentUserInfoDialog;
    };

} // namespace Blokus