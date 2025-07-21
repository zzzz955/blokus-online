#include "LobbyWindow.h"
#include "ClientTypes.h"  // 🔥 UserInfo 등을 위해 추가
#include <QApplication>
#include <QDesktopWidget>
#include <QHeaderView>
#include <QMessageBox>
#include <QCloseEvent>
#include <QMenuBar>
#include <QStatusBar>
#include <QDateTime>
#include <QInputDialog>
#include <QSplitter>

namespace Blokus {

    // ========================================
    // CreateRoomDialog 구현
    // ========================================

    CreateRoomDialog::CreateRoomDialog(QWidget* parent)
        : QDialog(parent)
        , m_roomNameEdit(nullptr)
        , m_gameModeCombo(nullptr)
        , m_maxPlayersSpinBox(nullptr)
        , m_privateCheckBox(nullptr)
        , m_passwordEdit(nullptr)
        , m_buttonBox(nullptr)
    {
        setWindowTitle(QString::fromUtf8("방 만들기"));
        setModal(true);
        setupUI();
        setupStyles();
        resize(350, 280);
    }

    void CreateRoomDialog::setupUI()
    {
        QVBoxLayout* layout = new QVBoxLayout(this);
        layout->setSpacing(15);
        layout->setContentsMargins(20, 20, 20, 20);

        // 방 이름
        QLabel* nameLabel = new QLabel(QString::fromUtf8("방 이름"));
        nameLabel->setStyleSheet("font-weight: bold;");
        m_roomNameEdit = new QLineEdit();
        m_roomNameEdit->setPlaceholderText(QString::fromUtf8("방 이름을 입력하세요"));
        m_roomNameEdit->setText(QString::fromUtf8("%1님의 방").arg("플레이어"));
        m_roomNameEdit->setMaxLength(30);

        // 게임 모드 (클래식만)
        QLabel* modeLabel = new QLabel(QString::fromUtf8("게임 모드"));
        modeLabel->setStyleSheet("font-weight: bold;");
        m_gameModeCombo = new QComboBox();
        m_gameModeCombo->addItem(QString::fromUtf8("클래식 (4인, 20x20)"), "classic");
        m_gameModeCombo->setCurrentIndex(0);
        m_gameModeCombo->setEnabled(false); // 클래식 모드 고정

        // 최대 인원 (2-4명)
        QLabel* playersLabel = new QLabel(QString::fromUtf8("최대 인원"));
        playersLabel->setStyleSheet("font-weight: bold;");
        m_maxPlayersSpinBox = new QSpinBox();
        m_maxPlayersSpinBox->setRange(2, 4);
        m_maxPlayersSpinBox->setValue(4);
        m_maxPlayersSpinBox->setSuffix(QString::fromUtf8("명"));

        // 비공개 방 설정
        m_privateCheckBox = new QCheckBox(QString::fromUtf8("비공개 방 (패스워드 설정)"));

        QLabel* passwordLabel = new QLabel(QString::fromUtf8("방 패스워드"));
        passwordLabel->setStyleSheet("font-weight: bold;");
        m_passwordEdit = new QLineEdit();
        m_passwordEdit->setPlaceholderText(QString::fromUtf8("패스워드 (선택사항)"));
        m_passwordEdit->setEchoMode(QLineEdit::Password);
        m_passwordEdit->setMaxLength(20);
        m_passwordEdit->setEnabled(false);

        // 버튼
        m_buttonBox = new QDialogButtonBox(QDialogButtonBox::Ok | QDialogButtonBox::Cancel);
        m_buttonBox->button(QDialogButtonBox::Ok)->setText(QString::fromUtf8("방 만들기"));
        m_buttonBox->button(QDialogButtonBox::Cancel)->setText(QString::fromUtf8("취소"));

        // 레이아웃 추가
        layout->addWidget(nameLabel);
        layout->addWidget(m_roomNameEdit);
        layout->addWidget(modeLabel);
        layout->addWidget(m_gameModeCombo);
        layout->addWidget(playersLabel);
        layout->addWidget(m_maxPlayersSpinBox);
        layout->addSpacing(10);
        layout->addWidget(m_privateCheckBox);
        layout->addWidget(passwordLabel);
        layout->addWidget(m_passwordEdit);
        layout->addSpacing(10);
        layout->addWidget(m_buttonBox);

        // 시그널 연결 (onGameModeChanged 제거)
        connect(m_privateCheckBox, &QCheckBox::toggled,
            this, &CreateRoomDialog::onPrivateToggled);
        connect(m_buttonBox, &QDialogButtonBox::accepted, this, &QDialog::accept);
        connect(m_buttonBox, &QDialogButtonBox::rejected, this, &QDialog::reject);
    }

    void CreateRoomDialog::setupStyles()
    {
        setStyleSheet(
            "QDialog { background-color: #f8f9fa; } "
            "QLineEdit, QComboBox, QSpinBox { "
            "padding: 8px; border: 2px solid #ddd; border-radius: 6px; "
            "background-color: white; font-size: 13px; } "
            "QLineEdit:focus, QComboBox:focus, QSpinBox:focus { border-color: #3498db; } "
            "QPushButton { "
            "padding: 8px 15px; border: none; border-radius: 6px; "
            "font-weight: bold; font-size: 13px; min-width: 80px; } "
            "QPushButton[text='방 만들기'] { background-color: #27ae60; color: white; } "
            "QPushButton[text='방 만들기']:hover { background-color: #229954; } "
            "QPushButton[text='취소'] { background-color: #95a5a6; color: white; } "
            "QPushButton[text='취소']:hover { background-color: #7f8c8d; } "
            "QCheckBox { font-weight: bold; color: #2c3e50; }"
        );
    }

    void CreateRoomDialog::onGameModeChanged()
    {
        QString mode = m_gameModeCombo->currentData().toString();

        if (mode == "duo") {
            // 듀오 모드는 2인용 고정
            m_maxPlayersSpinBox->setValue(2);
            m_maxPlayersSpinBox->setRange(2, 2);
            m_maxPlayersSpinBox->setEnabled(false); // 수정 불가
        }
        else {
            // 클래식 모드는 2-4명
            m_maxPlayersSpinBox->setEnabled(true);
            m_maxPlayersSpinBox->setRange(2, 4);
            if (m_maxPlayersSpinBox->value() < 2) {
                m_maxPlayersSpinBox->setValue(4);
            }
        }
    }

    void CreateRoomDialog::onPrivateToggled(bool enabled)
    {
        m_passwordEdit->setEnabled(enabled);
        if (enabled) {
            m_passwordEdit->setFocus();
        }
        else {
            m_passwordEdit->clear();
        }
    }

    RoomInfo CreateRoomDialog::getRoomInfo() const
    {
        RoomInfo room;
        room.roomName = m_roomNameEdit->text().trimmed();
        room.maxPlayers = m_maxPlayersSpinBox->value();
        room.isPrivate = m_privateCheckBox->isChecked();
        room.gameMode = QString::fromUtf8("클래식"); // 고정

        if (room.roomName.isEmpty()) {
            room.roomName = QString::fromUtf8("새로운 방");
        }

        return room;
    }

    // ========================================
    // LobbyWindow 구현
    // ========================================

    LobbyWindow::LobbyWindow(const QString& username, QWidget* parent)
        : QMainWindow(parent)
        , m_myUsername(username)
        , m_centralWidget(nullptr)
        , m_mainSplitter(nullptr)
        , m_leftPanel(nullptr)
        , m_leftTabs(nullptr)
        , m_usersTab(nullptr)
        , m_rankingTab(nullptr)
        , m_userList(nullptr)
        , m_rankingTable(nullptr)
        , m_onlineCountLabel(nullptr)
        , m_centerPanel(nullptr)
        , m_roomTable(nullptr)
        , m_roomControlsWidget(nullptr)
        , m_createRoomButton(nullptr)
        , m_joinRoomButton(nullptr)
        , m_refreshRoomButton(nullptr)
        , m_rightPanel(nullptr)
        , m_chatDisplay(nullptr)
        , m_chatInputWidget(nullptr)
        , m_chatInput(nullptr)
        , m_chatSendButton(nullptr)
        , m_infoPanel(nullptr)
        , m_welcomeLabel(nullptr)
        , m_userStatsLabel(nullptr)
        , m_logoutButton(nullptr)
        , m_refreshTimer(new QTimer(this))
        , m_selectedRoomId(-1)
        , m_buttonCooldownTimer(new QTimer(this))
    {
        qDebug() << QString::fromUtf8("LobbyWindow 생성자 시작: %1").arg(username);

        try {
            qDebug() << QString::fromUtf8("UI 설정 시작...");
            setupUI();
            qDebug() << QString::fromUtf8("UI 설정 완료");

            qDebug() << QString::fromUtf8("메뉴바 설정 시작...");
            setupMenuBar();
            qDebug() << QString::fromUtf8("메뉴바 설정 완료");

            qDebug() << QString::fromUtf8("상태바 설정 시작...");
            setupStatusBar();
            qDebug() << QString::fromUtf8("상태바 설정 완료");

            qDebug() << QString::fromUtf8("스타일 설정 시작...");
            setupStyles();
            qDebug() << QString::fromUtf8("스타일 설정 완료");

            // 서버에서 실제 데이터를 받아올 때까지 빈 상태로 시작
            updateUserListDisplay();
            updateRoomListDisplay();
            updateRankingDisplay();

            // 내 정보 설정
            m_myUserInfo.username = username;
            m_myUserInfo.totalGames = 45;
            m_myUserInfo.wins = 28;
            m_myUserInfo.losses = 17;
            m_myUserInfo.level = m_myUserInfo.calculateLevel();
            m_myUserInfo.averageScore = 52;

            // 타이머 설정 (30초마다 방 목록 갱신)
            if (m_refreshTimer) {
                m_refreshTimer->setInterval(30000);
                connect(m_refreshTimer, &QTimer::timeout, this, &LobbyWindow::onRefreshTimer);
                m_refreshTimer->start();
            }

            // 쿨다운 타이머 설정
            m_buttonCooldownTimer->setSingleShot(false);
            m_buttonCooldownTimer->setInterval(100); // 100ms마다 체크
            connect(m_buttonCooldownTimer, &QTimer::timeout, this, &LobbyWindow::onCooldownTimerTick);

            // 창 설정
            setWindowTitle(QString::fromUtf8("블로커스 온라인 - 로비 (%1님)").arg(username));
            setMinimumSize(1200, 800);
            resize(1400, 900);

            // 화면 중앙에 배치
            QRect screenGeometry = QApplication::desktop()->screenGeometry();
            int x = (screenGeometry.width() - width()) / 2;
            int y = (screenGeometry.height() - height()) / 2;
            move(x, y);

            // 환영 메시지
            qDebug() << QString::fromUtf8("환영 메시지 추가...");
            addSystemMessage(QString::fromUtf8("안녕하세요, %1님! 블로커스 온라인에 오신 것을 환영합니다.").arg(username));

            qDebug() << QString::fromUtf8("LobbyWindow 생성자 완료");

        }
        catch (const std::exception& e) {
            qDebug() << QString::fromUtf8("LobbyWindow 생성 중 예외: %1").arg(e.what());
            throw;
        }
        catch (...) {
            qDebug() << QString::fromUtf8("LobbyWindow 생성 중 알 수 없는 오류");
            throw;
        }
    }

    LobbyWindow::~LobbyWindow()
    {
        if (m_refreshTimer) {
            m_refreshTimer->stop();
        }
    }

    // 버튼 쿨다운 설정
    void LobbyWindow::setButtonCooldown(QPushButton* button)
    {
        if (!button || m_cooldownButtons.contains(button)) {
            return; // 이미 쿨다운 중인 버튼
        }

        // 버튼 비활성화
        button->setEnabled(false);
        m_cooldownButtons.insert(button);

        // 쿨다운 시각적 표시
        QString originalText = button->text();
        button->setProperty("originalText", originalText);
        button->setProperty("cooldownStart", QDateTime::currentMSecsSinceEpoch());

        // 쿨다운 스타일 적용
        QString cooldownStyle = button->styleSheet() +
            "QPushButton:disabled { "
            "background-color: #bdc3c7 !important; "
            "color: #7f8c8d !important; "
            "}";
        button->setStyleSheet(cooldownStyle);

        // 타이머가 실행 중이 아니면 시작
        if (!m_buttonCooldownTimer->isActive()) {
            m_buttonCooldownTimer->start();
        }
    }

    // 쿨다운 타이머 틱
    void LobbyWindow::onCooldownTimerTick()
    {
        qint64 currentTime = QDateTime::currentMSecsSinceEpoch();
        QList<QPushButton*> buttonsToEnable;

        for (QPushButton* button : m_cooldownButtons) {
            qint64 cooldownStart = button->property("cooldownStart").toLongLong();
            qint64 elapsed = currentTime - cooldownStart;

            if (elapsed >= BUTTON_COOLDOWN_MS) {
                buttonsToEnable.append(button);
            }
            else {
                // 남은 시간 표시
                int remainingMs = BUTTON_COOLDOWN_MS - elapsed;
                double remainingSec = remainingMs / 1000.0;
                QString originalText = button->property("originalText").toString();
                button->setText(QString("%1 (%.1f)").arg(originalText).arg(remainingSec));
            }
        }

        // 쿨다운 완료된 버튼들 활성화
        for (QPushButton* button : buttonsToEnable) {
            enableCooldownButton(button);
        }

        // 모든 버튼이 활성화되면 타이머 중지
        if (m_cooldownButtons.isEmpty()) {
            m_buttonCooldownTimer->stop();
        }
    }

    // 버튼 쿨다운 해제
    void LobbyWindow::enableCooldownButton(QPushButton* button)
    {
        if (!button || !m_cooldownButtons.contains(button)) {
            return;
        }

        // 원래 텍스트 복원
        QString originalText = button->property("originalText").toString();
        button->setText(originalText);

        // 버튼 활성화
        button->setEnabled(true);
        m_cooldownButtons.remove(button);

        // 프로퍼티 정리
        button->setProperty("originalText", QVariant());
        button->setProperty("cooldownStart", QVariant());
    }

    void LobbyWindow::setupUI()
    {
        // 중앙 위젯 설정
        m_centralWidget = new QWidget(this);
        setCentralWidget(m_centralWidget);

        setupMainLayout();
    }

    void LobbyWindow::setupMainLayout()
    {
        QVBoxLayout* mainVBoxLayout = new QVBoxLayout(m_centralWidget);
        mainVBoxLayout->setContentsMargins(10, 10, 10, 10);
        mainVBoxLayout->setSpacing(10);

        // 상단 정보 패널
        setupInfoPanel();

        // 메인 스플리터 (3분할)
        m_mainSplitter = new QSplitter(Qt::Horizontal);

        setupLeftPanel();   // 사용자 목록, 랭킹
        setupCenterPanel(); // 방 목록
        setupRightPanel();  // 채팅

        m_mainSplitter->addWidget(m_leftPanel);
        m_mainSplitter->addWidget(m_centerPanel);
        m_mainSplitter->addWidget(m_rightPanel);

        // 스플리터 비율 설정 (2:4:3)
        m_mainSplitter->setStretchFactor(0, 2);
        m_mainSplitter->setStretchFactor(1, 4);
        m_mainSplitter->setStretchFactor(2, 3);

        mainVBoxLayout->addWidget(m_infoPanel);
        mainVBoxLayout->addWidget(m_mainSplitter, 1);
    }

    void LobbyWindow::setupInfoPanel()
    {
        m_infoPanel = new QWidget();
        m_infoPanel->setFixedHeight(60);

        QHBoxLayout* layout = new QHBoxLayout(m_infoPanel);
        layout->setContentsMargins(15, 10, 15, 10);

        // 환영 메시지
        m_welcomeLabel = new QLabel(QString::fromUtf8("🎮 %1님, 환영합니다!").arg(m_myUsername));
        m_welcomeLabel->setStyleSheet("font-size: 16px; font-weight: bold; color: #2c3e50;");

        // 사용자 통계
        m_userStatsLabel = new QLabel();
        updateUserStatsDisplay();

        // 로그아웃 버튼
        m_logoutButton = new QPushButton(QString::fromUtf8("로그아웃"));
        m_logoutButton->setFixedSize(80, 35);

        layout->addWidget(m_welcomeLabel);
        layout->addStretch();
        layout->addWidget(m_userStatsLabel);
        layout->addWidget(m_logoutButton);

        connect(m_logoutButton, &QPushButton::clicked, this, &LobbyWindow::onLogoutClicked);
    }

    void LobbyWindow::setupLeftPanel()
    {
        m_leftPanel = new QWidget();
        m_leftPanel->setMinimumWidth(250);
        m_leftPanel->setMaximumWidth(350);

        QVBoxLayout* layout = new QVBoxLayout(m_leftPanel);
        layout->setContentsMargins(0, 0, 0, 0);
        layout->setSpacing(0);

        // 탭 위젯
        m_leftTabs = new QTabWidget();

        // 접속자 탭
        m_usersTab = new QWidget();
        QVBoxLayout* usersLayout = new QVBoxLayout(m_usersTab);
        usersLayout->setContentsMargins(10, 10, 10, 10);

        m_onlineCountLabel = new QLabel(QString::fromUtf8("접속자 (0명)"));
        m_onlineCountLabel->setStyleSheet("font-weight: bold; color: #27ae60; margin-bottom: 5px;");

        m_userList = new QListWidget();
        m_userList->setAlternatingRowColors(true);

        usersLayout->addWidget(m_onlineCountLabel);
        usersLayout->addWidget(m_userList);

        // 랭킹 탭
        m_rankingTab = new QWidget();
        QVBoxLayout* rankingLayout = new QVBoxLayout(m_rankingTab);
        rankingLayout->setContentsMargins(10, 10, 10, 10);

        QLabel* rankingLabel = new QLabel(QString::fromUtf8("🏆 실시간 랭킹"));
        rankingLabel->setStyleSheet("font-weight: bold; color: #f39c12; margin-bottom: 5px;");

        m_rankingTable = new QTableWidget();
        m_rankingTable->setColumnCount(3);
        m_rankingTable->setHorizontalHeaderLabels({
            QString::fromUtf8("순위"), QString::fromUtf8("플레이어"), QString::fromUtf8("승률")
            });
        m_rankingTable->horizontalHeader()->setStretchLastSection(true);
        m_rankingTable->verticalHeader()->setVisible(false);
        m_rankingTable->setSelectionBehavior(QAbstractItemView::SelectRows);
        m_rankingTable->setAlternatingRowColors(true);

        rankingLayout->addWidget(rankingLabel);
        rankingLayout->addWidget(m_rankingTable);

        m_leftTabs->addTab(m_usersTab, QString::fromUtf8("접속자"));
        m_leftTabs->addTab(m_rankingTab, QString::fromUtf8("랭킹"));

        layout->addWidget(m_leftTabs);

        // 시그널 연결
        connect(m_userList, &QListWidget::itemDoubleClicked, this, &LobbyWindow::onUserDoubleClicked);
        connect(m_leftTabs, &QTabWidget::currentChanged, this, &LobbyWindow::onTabChanged);
    }

    void LobbyWindow::setupCenterPanel()
    {
        m_centerPanel = new QWidget();

        QVBoxLayout* layout = new QVBoxLayout(m_centerPanel);
        layout->setContentsMargins(10, 10, 10, 10);
        layout->setSpacing(10);

        // 제목
        QLabel* titleLabel = new QLabel(QString::fromUtf8("🏠 게임방 목록"));
        titleLabel->setStyleSheet("font-size: 16px; font-weight: bold; color: #2c3e50; margin-bottom: 5px;");

        // 방 목록 테이블
        m_roomTable = new QTableWidget();
        m_roomTable->setColumnCount(6);
        m_roomTable->setHorizontalHeaderLabels({
            QString::fromUtf8("방 번호"), QString::fromUtf8("방 이름"), QString::fromUtf8("호스트"),
            QString::fromUtf8("인원"), QString::fromUtf8("상태"), QString::fromUtf8("모드")
            });

        // 테이블 설정
        m_roomTable->horizontalHeader()->setStretchLastSection(true);
        m_roomTable->verticalHeader()->setVisible(false);
        m_roomTable->setSelectionBehavior(QAbstractItemView::SelectRows);
        m_roomTable->setAlternatingRowColors(true);
        m_roomTable->setSortingEnabled(true);

        // 컨트롤 버튼들
        m_roomControlsWidget = new QWidget();
        QHBoxLayout* controlsLayout = new QHBoxLayout(m_roomControlsWidget);
        controlsLayout->setContentsMargins(0, 0, 0, 0);

        m_createRoomButton = new QPushButton(QString::fromUtf8("🏠 방 만들기"));
        m_joinRoomButton = new QPushButton(QString::fromUtf8("🚪 입장하기"));
        m_refreshRoomButton = new QPushButton(QString::fromUtf8("🔄 새로고침"));

        m_createRoomButton->setMinimumHeight(35);
        m_joinRoomButton->setMinimumHeight(35);
        m_refreshRoomButton->setMinimumHeight(35);

        controlsLayout->addWidget(m_createRoomButton);
        controlsLayout->addWidget(m_joinRoomButton);
        controlsLayout->addStretch();
        controlsLayout->addWidget(m_refreshRoomButton);

        layout->addWidget(titleLabel);
        layout->addWidget(m_roomTable, 1);
        layout->addWidget(m_roomControlsWidget);

        // 시그널 연결
        connect(m_createRoomButton, &QPushButton::clicked, this, &LobbyWindow::onCreateRoomClicked);
        connect(m_joinRoomButton, &QPushButton::clicked, this, &LobbyWindow::onJoinRoomClicked);
        connect(m_refreshRoomButton, &QPushButton::clicked, this, &LobbyWindow::onRefreshRoomListClicked);
        connect(m_roomTable, &QTableWidget::itemDoubleClicked, this, &LobbyWindow::onRoomDoubleClicked);
        connect(m_roomTable, &QTableWidget::itemSelectionChanged, this, [this]() {
            int row = m_roomTable->currentRow();
            if (row >= 0 && row < m_roomList_data.size()) {
                m_selectedRoomId = m_roomList_data[row].roomId;
            }
            });
    }

    void LobbyWindow::setupRightPanel()
    {
        m_rightPanel = new QWidget();
        m_rightPanel->setMinimumWidth(300);
        m_rightPanel->setMaximumWidth(400);

        QVBoxLayout* layout = new QVBoxLayout(m_rightPanel);
        layout->setContentsMargins(10, 10, 10, 10);
        layout->setSpacing(10);

        // 채팅 제목
        QLabel* chatLabel = new QLabel(QString::fromUtf8("💬 로비 채팅"));
        chatLabel->setStyleSheet("font-size: 14px; font-weight: bold; color: #8e44ad; margin-bottom: 5px;");

        // 채팅 디스플레이
        m_chatDisplay = new QTextEdit();
        m_chatDisplay->setReadOnly(true);
        // QTextEdit에는 setMaximumBlockCount가 없으므로 제거
        // 대신 주기적으로 텍스트 길이를 확인하여 정리

        // 채팅 입력
        m_chatInputWidget = new QWidget();
        QHBoxLayout* chatInputLayout = new QHBoxLayout(m_chatInputWidget);
        chatInputLayout->setContentsMargins(0, 0, 0, 0);

        m_chatInput = new QLineEdit();
        m_chatInput->setPlaceholderText(QString::fromUtf8("메시지를 입력하세요..."));
        m_chatInput->setMaxLength(200);

        m_chatSendButton = new QPushButton(QString::fromUtf8("전송"));
        m_chatSendButton->setFixedSize(60, 30);

        chatInputLayout->addWidget(m_chatInput);
        chatInputLayout->addWidget(m_chatSendButton);

        layout->addWidget(chatLabel);
        layout->addWidget(m_chatDisplay, 1);
        layout->addWidget(m_chatInputWidget);

        // 시그널 연결
        connect(m_chatSendButton, &QPushButton::clicked, this, &LobbyWindow::onChatSendClicked);
        connect(m_chatInput, &QLineEdit::returnPressed, this, &LobbyWindow::onChatReturnPressed);
    }

    void LobbyWindow::setupMenuBar()
    {
        QMenuBar* menuBar = this->menuBar();

        // 게임 메뉴
        QMenu* gameMenu = menuBar->addMenu(QString::fromUtf8("게임"));
        QAction* createRoomAction = gameMenu->addAction(QString::fromUtf8("방 만들기"));
        QAction* refreshAction = gameMenu->addAction(QString::fromUtf8("새로고침"));
        gameMenu->addSeparator();
        QAction* logoutAction = gameMenu->addAction(QString::fromUtf8("로그아웃"));

        connect(createRoomAction, &QAction::triggered, this, &LobbyWindow::onCreateRoomClicked);
        connect(refreshAction, &QAction::triggered, this, &LobbyWindow::onRefreshRoomListClicked);
        connect(logoutAction, &QAction::triggered, this, &LobbyWindow::onLogoutClicked);

        // 설정 메뉴
        QMenu* settingsMenu = menuBar->addMenu(QString::fromUtf8("설정"));
        QAction* preferencesAction = settingsMenu->addAction(QString::fromUtf8("환경설정"));

        // 도움말 메뉴
        QMenu* helpMenu = menuBar->addMenu(QString::fromUtf8("도움말"));
        QAction* rulesAction = helpMenu->addAction(QString::fromUtf8("게임 규칙"));
        QAction* aboutAction = helpMenu->addAction(QString::fromUtf8("정보"));

        // TODO: 메뉴 액션들 구현
    }

    void LobbyWindow::setupStatusBar()
    {
        QStatusBar* statusBar = this->statusBar();
        statusBar->showMessage(QString::fromUtf8("로비에 연결되었습니다."));

        // 오른쪽에 추가 정보 표시
        QLabel* statusLabel = new QLabel(QString::fromUtf8("서버 상태: 정상"));
        statusLabel->setStyleSheet("color: #27ae60; font-weight: bold;");
        statusBar->addPermanentWidget(statusLabel);
    }

    void LobbyWindow::setupStyles()
    {
        setStyleSheet(
            "QMainWindow { background-color: #ecf0f1; } "

            // 정보 패널 스타일
            "QWidget#infoPanel { "
            "background: qlineargradient(x1:0, y1:0, x2:0, y2:1, "
            "stop:0 #3498db, stop:1 #2980b9); "
            "border-radius: 8px; } "

            // 패널 스타일
            "QWidget#leftPanel, QWidget#centerPanel, QWidget#rightPanel { "
            "background-color: white; border: 1px solid #bdc3c7; "
            "border-radius: 8px; } "

            // 버튼 스타일
            "QPushButton { "
            "border: none; border-radius: 6px; font-weight: bold; "
            "font-size: 13px; padding: 8px 15px; } "
            "QPushButton[text*='만들기'] { background-color: #27ae60; color: white; } "
            "QPushButton[text*='만들기']:hover { background-color: #229954; } "
            "QPushButton[text*='입장'] { background-color: #3498db; color: white; } "
            "QPushButton[text*='입장']:hover { background-color: #2980b9; } "
            "QPushButton[text*='새로고침'] { background-color: #95a5a6; color: white; } "
            "QPushButton[text*='새로고침']:hover { background-color: #7f8c8d; } "
            "QPushButton[text*='로그아웃'] { background-color: #e74c3c; color: white; } "
            "QPushButton[text*='로그아웃']:hover { background-color: #c0392b; } "
            "QPushButton[text*='전송'] { background-color: #8e44ad; color: white; } "
            "QPushButton[text*='전송']:hover { background-color: #732d91; } "

            // 테이블 스타일
            "QTableWidget { "
            "gridline-color: #ddd; border: 1px solid #ddd; "
            "selection-background-color: #3498db; } "
            "QTableWidget::item { padding: 8px; } "
            "QHeaderView::section { "
            "background-color: #34495e; color: white; "
            "font-weight: bold; padding: 8px; border: none; } "

            // 리스트 스타일
            "QListWidget { "
            "border: 1px solid #ddd; "
            "selection-background-color: #3498db; } "
            "QListWidget::item { padding: 8px; } "

            // 채팅 스타일
            "QTextEdit { "
            "border: 1px solid #ddd; border-radius: 6px; "
            "background-color: #fafafa; font-family: 'Consolas', monospace; } "
            "QLineEdit { "
            "border: 2px solid #ddd; border-radius: 6px; "
            "padding: 6px 10px; font-size: 13px; } "
            "QLineEdit:focus { border-color: #3498db; } "

            // 탭 스타일
            "QTabWidget::pane { border: 1px solid #ddd; } "
            "QTabBar::tab { "
            "padding: 8px 15px; margin-right: 2px; "
            "background-color: #ecf0f1; border: 1px solid #ddd; } "
            "QTabBar::tab:selected { "
            "background-color: white; border-bottom: none; } "
        );

        // 개별 위젯 ID 설정 (CSS 선택자용)
        m_infoPanel->setObjectName("infoPanel");
        m_leftPanel->setObjectName("leftPanel");
        m_centerPanel->setObjectName("centerPanel");
        m_rightPanel->setObjectName("rightPanel");
    }

    // ========================================
    // 이벤트 핸들러들
    // ========================================

    void LobbyWindow::onCreateRoomClicked()
    {
        // 쿨다운 체크
        if (!m_createRoomButton->isEnabled()) {
            qDebug() << QString::fromUtf8("방 만들기 버튼 쿨다운 중");
            return;
        }

        CreateRoomDialog dialog(this);
        if (dialog.exec() == QDialog::Accepted) {
            // 쿨다운 설정 (다이얼로그에서 OK를 눌렀을 때만)
            setButtonCooldown(m_createRoomButton);

            RoomInfo roomInfo = dialog.getRoomInfo();
            roomInfo.roomId = m_roomList_data.size() + 1001; // 임시 ID
            roomInfo.hostName = m_myUsername;
            roomInfo.currentPlayers = 1;

            addSystemMessage(QString::fromUtf8("방 '%1'을(를) 생성했습니다.").arg(roomInfo.roomName));
            emit createRoomRequested(roomInfo);
        }
    }

    void LobbyWindow::onJoinRoomClicked()
    {
        // 쿨다운 체크
        if (!m_joinRoomButton->isEnabled()) {
            qDebug() << QString::fromUtf8("입장하기 버튼 쿨다운 중");
            return;
        }

        if (m_selectedRoomId == -1) {
            QMessageBox::information(this, QString::fromUtf8("알림"),
                QString::fromUtf8("입장할 방을 선택해주세요."));
            return;
        }

        // 쿨다운 설정
        setButtonCooldown(m_joinRoomButton);

        // 선택된 방 정보 찾기
        RoomInfo* selectedRoom = nullptr;
        for (auto& room : m_roomList_data) {
            if (room.roomId == m_selectedRoomId) {
                selectedRoom = &room;
                break;
            }
        }

        if (!selectedRoom) {
            QMessageBox::warning(this, QString::fromUtf8("오류"),
                QString::fromUtf8("방 정보를 찾을 수 없습니다."));
            return;
        }

        // 게임 중인 방 확인
        if (selectedRoom->isPlaying) {
            QMessageBox::information(this, QString::fromUtf8("알림"),
                QString::fromUtf8("이미 게임이 진행 중인 방입니다."));
            return;
        }

        // 인원 확인
        if (selectedRoom->currentPlayers >= selectedRoom->maxPlayers) {
            QMessageBox::information(this, QString::fromUtf8("알림"),
                QString::fromUtf8("방이 가득 찼습니다."));
            return;
        }

        QString password = "";
        if (selectedRoom->isPrivate) {
            bool ok;
            password = QInputDialog::getText(this, QString::fromUtf8("비공개 방"),
                QString::fromUtf8("방 패스워드를 입력하세요:"), QLineEdit::Password, "", &ok);
            if (!ok) return;
        }

        addSystemMessage(QString::fromUtf8("방 '%1'에 입장을 요청했습니다...")
            .arg(selectedRoom->roomName));
        emit joinRoomRequested(m_selectedRoomId, password);
    }

    void LobbyWindow::onRefreshRoomListClicked()
    {
        // 쿨다운 체크
        if (!m_refreshRoomButton->isEnabled()) {
            qDebug() << QString::fromUtf8("새로고침 버튼 쿨다운 중");
            return;
        }

        // 쿨다운 설정
        setButtonCooldown(m_refreshRoomButton);

        addSystemMessage(QString::fromUtf8("방 목록을 새로고침합니다..."));
        emit refreshRoomListRequested();
        
        // 더미 데이터 제거 - 서버에서 실제 데이터로 바뀌음
    }

    void LobbyWindow::onRoomDoubleClicked()
    {
        onJoinRoomClicked();
    }

    void LobbyWindow::onChatSendClicked()
    {
        QString message = m_chatInput->text().trimmed();
        if (message.isEmpty()) return;

        // 서버에만 전송, 로컬에 추가하지 않음 (브로드캐스트로 받음)
        emit sendChatMessageRequested(message);

        m_chatInput->clear();
        m_chatInput->setFocus();
    }

    void LobbyWindow::onChatReturnPressed()
    {
        onChatSendClicked();
    }

    void LobbyWindow::onLogoutClicked()
    {
        int result = QMessageBox::question(this, QString::fromUtf8("로그아웃"),
            QString::fromUtf8("정말 로그아웃하시겠습니까?"),
            QMessageBox::Yes | QMessageBox::No, QMessageBox::No);

        if (result == QMessageBox::Yes) {
            emit logoutRequested();
        }
    }

    void LobbyWindow::onUserDoubleClicked()
    {
        QListWidgetItem* item = m_userList->currentItem();
        if (!item) return;

        QString username = item->text().split(" ")[0]; // 첫 번째 단어가 사용자명
        if (username == m_myUsername) return;

        // 귓속말 기능 (향후 구현)
        QString whisper = QInputDialog::getText(this, QString::fromUtf8("귓속말"),
            QString::fromUtf8("%1님에게 귓속말:").arg(username));

        if (!whisper.isEmpty()) {
            ChatMessage msg;
            msg.username = QString::fromUtf8("귓속말 to %1").arg(username);
            msg.message = whisper;
            msg.timestamp = QDateTime::currentDateTime();
            msg.type = ChatMessage::Whisper;
            addChatMessage(msg);
        }
    }

    void LobbyWindow::onTabChanged(int index)
    {
        // 탭 변경 시 데이터 갱신
        if (index == 1) { // 랭킹 탭
            updateRankingDisplay();
        }
    }

    void LobbyWindow::onRefreshTimer()
    {
        // 자동 새로고침
        emit refreshRoomListRequested();
    }

    void LobbyWindow::closeEvent(QCloseEvent* event)
    {
        int result = QMessageBox::question(this, QString::fromUtf8("종료"),
            QString::fromUtf8("블로커스 온라인을 종료하시겠습니까?"),
            QMessageBox::Yes | QMessageBox::No, QMessageBox::No);

        if (result == QMessageBox::Yes) {
            emit logoutRequested();
            event->accept();
        }
        else {
            event->ignore();
        }
    }

    void LobbyWindow::resizeEvent(QResizeEvent* event)
    {
        QMainWindow::resizeEvent(event);
        // 크기 변경 시 테이블 열 너비 조정
        if (m_roomTable) {
            int totalWidth = m_roomTable->width();
            m_roomTable->setColumnWidth(0, totalWidth * 0.10); // 방 번호
            m_roomTable->setColumnWidth(1, totalWidth * 0.30); // 방 이름
            m_roomTable->setColumnWidth(2, totalWidth * 0.20); // 호스트
            m_roomTable->setColumnWidth(3, totalWidth * 0.15); // 인원
            m_roomTable->setColumnWidth(4, totalWidth * 0.15); // 상태
            // 마지막 열(모드)는 자동으로 남은 공간 차지
        }
    }

    // ========================================
    // 데이터 업데이트 함수들
    // ========================================

    void LobbyWindow::updateUserList(const QList<UserInfo>& users)
    {
        m_userList_data = users;
        updateUserListDisplay();
    }

    void LobbyWindow::updateRoomList(const QList<RoomInfo>& rooms)
    {
        m_roomList_data = rooms;
        updateRoomListDisplay();
    }

    void LobbyWindow::updateRanking(const QList<UserInfo>& ranking)
    {
        m_ranking_data = ranking;
        updateRankingDisplay();
    }

    void LobbyWindow::addChatMessage(const ChatMessage& message)
    {
        m_chatHistory.append(message);

        // 채팅 히스토리가 너무 길어지면 앞부분 제거 (메모리 관리)
        if (m_chatHistory.size() > 500) {
            m_chatHistory.removeFirst();
        }

        QString formattedMsg = formatChatMessage(message);
        m_chatDisplay->append(formattedMsg);

        // 채팅 표시 영역도 너무 길어지면 정리
        QString allText = m_chatDisplay->toPlainText();
        QStringList lines = allText.split('\n');
        if (lines.size() > 500) {
            // 앞의 100줄 제거하고 나머지만 유지
            QStringList trimmedLines = lines.mid(100);
            m_chatDisplay->clear();
            m_chatDisplay->setPlainText(trimmedLines.join('\n'));
        }

        scrollChatToBottom();
    }

    void LobbyWindow::setMyUserInfo(const UserInfo& userInfo)
    {
        m_myUserInfo = userInfo;
        updateUserStatsDisplay();
    }

    // ========================================
    // UI 업데이트 함수들
    // ========================================

    void LobbyWindow::updateRoomListDisplay()
    {
        m_roomTable->setRowCount(m_roomList_data.size());

        for (int i = 0; i < m_roomList_data.size(); ++i) {
            const RoomInfo& room = m_roomList_data[i];

            m_roomTable->setItem(i, 0, new QTableWidgetItem(QString::number(room.roomId)));

            QString roomName = room.roomName;
            if (room.isPrivate) roomName += QString::fromUtf8(" 🔒");
            m_roomTable->setItem(i, 1, new QTableWidgetItem(roomName));

            m_roomTable->setItem(i, 2, new QTableWidgetItem(room.hostName));
            m_roomTable->setItem(i, 3, new QTableWidgetItem(
                QString::fromUtf8("%1/%2").arg(room.currentPlayers).arg(room.maxPlayers)));

            QString status = room.isPlaying ? QString::fromUtf8("게임중") : QString::fromUtf8("대기중");
            QTableWidgetItem* statusItem = new QTableWidgetItem(status);
            if (room.isPlaying) {
                statusItem->setForeground(QBrush(QColor("#e74c3c")));
            }
            else {
                statusItem->setForeground(QBrush(QColor("#27ae60")));
            }
            m_roomTable->setItem(i, 4, statusItem);

            m_roomTable->setItem(i, 5, new QTableWidgetItem(room.gameMode));
        }
    }

    void LobbyWindow::updateUserListDisplay()
    {
        m_userList->clear();

        for (const UserInfo& user : m_userList_data) {
            QString userText = formatUserStatus(user);
            QListWidgetItem* item = new QListWidgetItem(userText);

            if (user.username == m_myUsername) {
                item->setForeground(QBrush(QColor("#3498db")));
                item->setFont(QFont(item->font().family(), item->font().pointSize(), QFont::Bold));
            }

            m_userList->addItem(item);
        }

        m_onlineCountLabel->setText(QString::fromUtf8("접속자 (%1명)").arg(m_userList_data.size()));
    }

    void LobbyWindow::updateRankingDisplay()
    {
        m_rankingTable->setRowCount(m_ranking_data.size());

        for (int i = 0; i < m_ranking_data.size(); ++i) {
            const UserInfo& user = m_ranking_data[i];

            m_rankingTable->setItem(i, 0, new QTableWidgetItem(QString::number(i + 1)));

            QTableWidgetItem* nameItem = new QTableWidgetItem(user.username);
            if (user.username == m_myUsername) {
                nameItem->setForeground(QBrush(QColor("#3498db")));
                nameItem->setFont(QFont(nameItem->font().family(), nameItem->font().pointSize(), QFont::Bold));
            }
            m_rankingTable->setItem(i, 1, nameItem);

            // 승률 표시 (소수점 1자리)
            QString winRateText = QString::number(user.getWinRate(), 'f', 1) + "%";
            m_rankingTable->setItem(i, 2, new QTableWidgetItem(winRateText));
        }
    }

    void LobbyWindow::updateUserStatsDisplay()
    {
        QString statsText = QString::fromUtf8("레벨 %1 | %2승 %3패 | 승률 %4%")
            .arg(m_myUserInfo.level)
            .arg(m_myUserInfo.wins)
            .arg(m_myUserInfo.losses)
            .arg(QString::number(m_myUserInfo.getWinRate(), 'f', 1));

        m_userStatsLabel->setText(statsText);
        m_userStatsLabel->setStyleSheet("color: white; font-size: 13px;");
    }

    // ========================================
    // 유틸리티 함수들
    // ========================================

    void LobbyWindow::addSystemMessage(const QString& message)
    {
        if (!m_chatDisplay) {
            qDebug() << QString::fromUtf8("경고: 채팅 디스플레이가 초기화되지 않음");
            return;
        }

        ChatMessage sysMsg;
        sysMsg.username = QString::fromUtf8("시스템");
        sysMsg.message = message;
        sysMsg.timestamp = QDateTime::currentDateTime();
        sysMsg.type = ChatMessage::System;

        addChatMessage(sysMsg);
    }

    void LobbyWindow::scrollChatToBottom()
    {
        QTextCursor cursor = m_chatDisplay->textCursor();
        cursor.movePosition(QTextCursor::End);
        m_chatDisplay->setTextCursor(cursor);
    }

    QString LobbyWindow::formatChatMessage(const ChatMessage& message)
    {
        QString timeStr = message.timestamp.toString("hh:mm");
        QString colorCode;

        switch (message.type) {
        case ChatMessage::System:
            colorCode = "#8e44ad"; // 보라색
            return QString("<span style='color: %1; font-weight: bold;'>[%2] %3: %4</span>")
                .arg(colorCode, timeStr, message.username, message.message);

        case ChatMessage::Whisper:
            colorCode = "#e67e22"; // 주황색
            return QString("<span style='color: %1; font-style: italic;'>[%2] %3: %4</span>")
                .arg(colorCode, timeStr, message.username, message.message);

        default: // Normal
            if (message.username == m_myUsername) {
                colorCode = "#3498db"; // 파란색 (내 메시지)
            }
            else {
                colorCode = "#2c3e50"; // 검은색 (다른 사람 메시지)
            }
            return QString("<span style='color: %1;'>[%2] <b>%3:</b> %4</span>")
                .arg(colorCode, timeStr, message.username, message.message);
        }
    }

    QString LobbyWindow::formatUserStatus(const UserInfo& user)
    {
        QString statusIcon;
        if (user.status == QString::fromUtf8("게임중")) {
            statusIcon = "🎮";
        }
        else if (user.status == QString::fromUtf8("자리비움")) {
            statusIcon = "💤";
        }
        else {
            statusIcon = "🟢";
        }

        return QString::fromUtf8("%1 %2 (Lv.%3)").arg(statusIcon, user.username).arg(user.level);
    }

    QString LobbyWindow::formatRoomStatus(const RoomInfo& room)
    {
        return QString::fromUtf8("%1/%2명").arg(room.currentPlayers).arg(room.maxPlayers);
    }


} // namespace Blokus

#include "ui/LobbyWindow.moc"