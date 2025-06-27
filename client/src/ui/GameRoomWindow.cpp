#include "ui/GameRoomWindow.h"
#include <QApplication>
#include <QMessageBox>
#include <QInputDialog>
#include <QSplitter>
#include <QDesktopWidget>
#include <QMenuBar>
#include <QStatusBar>
#include <QDateTime>

namespace Blokus {

    // ========================================
    // PlayerSlotWidget 구현
    // ========================================

    PlayerSlotWidget::PlayerSlotWidget(PlayerColor color, QWidget* parent)
        : QWidget(parent)
        , m_color(color)
        , m_isMySlot(false)
        , m_mainLayout(nullptr)
        , m_colorFrame(nullptr)
        , m_colorLabel(nullptr)
        , m_usernameLabel(nullptr)
        , m_statusLabel(nullptr)
        , m_scoreLabel(nullptr)
        , m_actionButton(nullptr)
        , m_hostIndicator(nullptr)
    {
        setupUI();
        setupStyles();

        // 빈 슬롯으로 초기화
        PlayerSlot emptySlot;
        emptySlot.color = color;
        updatePlayerSlot(emptySlot);
    }

    void PlayerSlotWidget::setupUI()
    {
        setFixedSize(200, 250);

        m_mainLayout = new QVBoxLayout(this);
        m_mainLayout->setContentsMargins(10, 10, 10, 10);
        m_mainLayout->setSpacing(8);

        // 색상 표시 프레임
        m_colorFrame = new QFrame();
        m_colorFrame->setFixedHeight(40);
        m_colorFrame->setFrameStyle(QFrame::Box | QFrame::Raised);
        m_colorFrame->setLineWidth(3);

        m_colorLabel = new QLabel(getColorName());
        m_colorLabel->setAlignment(Qt::AlignCenter);
        m_colorLabel->setStyleSheet("font-weight: bold; font-size: 14px; color: white;");

        QVBoxLayout* colorLayout = new QVBoxLayout(m_colorFrame);
        colorLayout->setContentsMargins(5, 5, 5, 5);
        colorLayout->addWidget(m_colorLabel);

        // 호스트 표시
        m_hostIndicator = new QWidget();
        m_hostIndicator->setFixedHeight(20);
        QLabel* hostLabel = new QLabel(QString::fromUtf8("👑 호스트"));
        hostLabel->setAlignment(Qt::AlignCenter);
        hostLabel->setStyleSheet("font-size: 12px; font-weight: bold; color: #f39c12;");
        QVBoxLayout* hostLayout = new QVBoxLayout(m_hostIndicator);
        hostLayout->setContentsMargins(0, 0, 0, 0);
        hostLayout->addWidget(hostLabel);
        m_hostIndicator->hide(); // 기본적으로 숨김

        // 플레이어 정보
        m_usernameLabel = new QLabel(QString::fromUtf8("빈 슬롯"));
        m_usernameLabel->setAlignment(Qt::AlignCenter);
        m_usernameLabel->setStyleSheet("font-size: 14px; font-weight: bold;");

        m_statusLabel = new QLabel(QString::fromUtf8("대기 중"));
        m_statusLabel->setAlignment(Qt::AlignCenter);
        m_statusLabel->setStyleSheet("font-size: 12px; color: #7f8c8d;");

        m_scoreLabel = new QLabel(QString::fromUtf8("점수: 0"));
        m_scoreLabel->setAlignment(Qt::AlignCenter);
        m_scoreLabel->setStyleSheet("font-size: 12px; color: #34495e;");

        // 액션 버튼
        m_actionButton = new QPushButton(QString::fromUtf8("AI 추가"));
        m_actionButton->setFixedHeight(35);

        // 레이아웃 구성
        m_mainLayout->addWidget(m_colorFrame);
        m_mainLayout->addWidget(m_hostIndicator);
        m_mainLayout->addWidget(m_usernameLabel);
        m_mainLayout->addWidget(m_statusLabel);
        m_mainLayout->addWidget(m_scoreLabel);
        m_mainLayout->addStretch();
        m_mainLayout->addWidget(m_actionButton);

        // 시그널 연결
        connect(m_actionButton, &QPushButton::clicked, this, &PlayerSlotWidget::onAddAIClicked);
    }

    void PlayerSlotWidget::setupStyles()
    {
        // 전체 위젯 스타일
        setStyleSheet(
            "PlayerSlotWidget { "
            "background-color: white; "
            "border: 2px solid #ddd; "
            "border-radius: 10px; "
            "}"
        );

        // 색상 프레임 스타일
        QColor playerColor = getPlayerColor();
        m_colorFrame->setStyleSheet(QString(
            "QFrame { "
            "background-color: %1; "
            "border: 3px solid %2; "
            "border-radius: 8px; "
            "}"
        ).arg(playerColor.name(), playerColor.darker(150).name()));

        // 버튼 스타일
        m_actionButton->setStyleSheet(
            "QPushButton { "
            "background-color: #3498db; "
            "border: none; "
            "border-radius: 6px; "
            "color: white; "
            "font-weight: bold; "
            "font-size: 12px; "
            "padding: 6px; "
            "} "
            "QPushButton:hover { "
            "background-color: #2980b9; "
            "} "
            "QPushButton:pressed { "
            "background-color: #21618c; "
            "}"
        );
    }

    void PlayerSlotWidget::updatePlayerSlot(const PlayerSlot& slot)
    {
        m_currentSlot = slot;

        // 사용자명 업데이트
        m_usernameLabel->setText(slot.getDisplayName());

        // 호스트 표시 업데이트
        m_hostIndicator->setVisible(slot.isHost);

        // 상태 업데이트
        QString statusText;
        if (slot.isEmpty()) {
            statusText = QString::fromUtf8("빈 슬롯");
            m_statusLabel->setStyleSheet("font-size: 12px; color: #95a5a6;");
        }
        else if (slot.isAI) {
            statusText = QString::fromUtf8("AI 준비됨");
            m_statusLabel->setStyleSheet("font-size: 12px; color: #9b59b6;");
        }
        else {
            statusText = slot.isReady ? QString::fromUtf8("준비됨") : QString::fromUtf8("대기 중");
            m_statusLabel->setStyleSheet(QString("font-size: 12px; color: %1;")
                .arg(slot.isReady ? "#27ae60" : "#e74c3c"));
        }
        m_statusLabel->setText(statusText);

        // 점수 업데이트
        m_scoreLabel->setText(QString::fromUtf8("점수: %1").arg(slot.score));

        // 액션 버튼 업데이트
        updateActionButton();
    }

    void PlayerSlotWidget::setMySlot(bool isMySlot)
    {
        m_isMySlot = isMySlot;

        // 내 슬롯이면 테두리 강조
        if (isMySlot) {
            setStyleSheet(
                "PlayerSlotWidget { "
                "background-color: #ebf3fd; "
                "border: 3px solid #3498db; "
                "border-radius: 10px; "
                "}"
            );
        }
        else {
            setStyleSheet(
                "PlayerSlotWidget { "
                "background-color: white; "
                "border: 2px solid #ddd; "
                "border-radius: 10px; "
                "}"
            );
        }

        updateActionButton();
    }

    void PlayerSlotWidget::updateActionButton()
    {
        // 버튼 텍스트와 기능 결정
        if (m_currentSlot.isEmpty()) {
            m_actionButton->setText(QString::fromUtf8("AI 추가"));
            m_actionButton->setVisible(true);
            disconnect(m_actionButton, nullptr, nullptr, nullptr);
            connect(m_actionButton, &QPushButton::clicked, this, &PlayerSlotWidget::onAddAIClicked);
        }
        else if (m_currentSlot.isAI) {
            m_actionButton->setText(QString::fromUtf8("AI 제거"));
            m_actionButton->setVisible(true);
            disconnect(m_actionButton, nullptr, nullptr, nullptr);
            connect(m_actionButton, &QPushButton::clicked, this, &PlayerSlotWidget::onRemoveClicked);
        }
        else if (m_isMySlot) {
            m_actionButton->setText(QString::fromUtf8("방 나가기"));
            m_actionButton->setVisible(true);
            m_actionButton->setStyleSheet(
                "QPushButton { background-color: #e74c3c; color: white; border: none; "
                "border-radius: 6px; font-weight: bold; font-size: 12px; padding: 6px; } "
                "QPushButton:hover { background-color: #c0392b; }"
            );
            disconnect(m_actionButton, nullptr, nullptr, nullptr);
            connect(m_actionButton, &QPushButton::clicked, this, &PlayerSlotWidget::onRemoveClicked);
        }
        else {
            // 다른 플레이어 - 호스트라면 강퇴 가능
            m_actionButton->setText(QString::fromUtf8("강퇴"));
            m_actionButton->setVisible(false); // 일단 숨김 (호스트 권한 체크 필요)
            disconnect(m_actionButton, nullptr, nullptr, nullptr);
            connect(m_actionButton, &QPushButton::clicked, this, &PlayerSlotWidget::onKickClicked);
        }
    }

    QString PlayerSlotWidget::getColorName() const
    {
        switch (m_color) {
        case PlayerColor::Blue: return QString::fromUtf8("파랑");
        case PlayerColor::Yellow: return QString::fromUtf8("노랑");
        case PlayerColor::Red: return QString::fromUtf8("빨강");
        case PlayerColor::Green: return QString::fromUtf8("초록");
        default: return QString::fromUtf8("없음");
        }
    }

    QColor PlayerSlotWidget::getPlayerColor() const
    {
        switch (m_color) {
        case PlayerColor::Blue: return QColor(52, 152, 219);
        case PlayerColor::Yellow: return QColor(241, 196, 15);
        case PlayerColor::Red: return QColor(231, 76, 60);
        case PlayerColor::Green: return QColor(46, 204, 113);
        default: return QColor(149, 165, 166);
        }
    }

    void PlayerSlotWidget::onAddAIClicked()
    {
        bool ok;
        QStringList difficulties = { QString::fromUtf8("쉬움"), QString::fromUtf8("보통"), QString::fromUtf8("어려움") };
        QString selectedDifficulty = QInputDialog::getItem(this, QString::fromUtf8("AI 난이도 선택"),
            QString::fromUtf8("AI 난이도를 선택하세요:"), difficulties, 1, false, &ok);

        if (ok) {
            int difficulty = difficulties.indexOf(selectedDifficulty) + 1;
            emit addAIRequested(m_color, difficulty);
        }
    }

    void PlayerSlotWidget::onRemoveClicked()
    {
        emit removePlayerRequested(m_color);
    }

    void PlayerSlotWidget::onKickClicked()
    {
        int ret = QMessageBox::question(this, QString::fromUtf8("플레이어 강퇴"),
            QString::fromUtf8("%1 플레이어를 강퇴하시겠습니까?").arg(m_currentSlot.username),
            QMessageBox::Yes | QMessageBox::No);

        if (ret == QMessageBox::Yes) {
            emit kickPlayerRequested(m_color);
        }
    }

    // ========================================
    // GameRoomWindow 구현
    // ========================================

    GameRoomWindow::GameRoomWindow(const GameRoomInfo& roomInfo, const QString& myUsername, QWidget* parent)
        : QMainWindow(parent)
        , m_myUsername(myUsername)
        , m_roomInfo(roomInfo)
        , m_gameManager(nullptr)
        , m_centralWidget(nullptr)
        , m_mainLayout(nullptr)
        , m_roomInfoPanel(nullptr)
        , m_roomNameLabel(nullptr)
        , m_roomStatusLabel(nullptr)
        , m_currentTurnLabel(nullptr)
        , m_playerSlotsPanel(nullptr)
        , m_slotsLayout(nullptr)
        , m_gameArea(nullptr)
        , m_gameSplitter(nullptr)
        , m_gameBoard(nullptr)
        , m_blockPalette(nullptr)
        , m_chatPanel(nullptr)
        , m_chatDisplay(nullptr)
        , m_chatInput(nullptr)
        , m_chatSendButton(nullptr)
        , m_controlsPanel(nullptr)
        , m_leaveRoomButton(nullptr)
        , m_gameStartButton(nullptr)
        , m_gameResetButton(nullptr)
        , m_gameStatusLabel(nullptr)
        , m_coordinateLabel(nullptr)
        , m_isGameStarted(false)
        , m_turnTimer(new QTimer(this))
    {
        // 게임 매니저 생성
        m_gameManager = new GameStateManager();

        setupUI();
        setupMenuBar();
        setupStatusBar();
        setupStyles();

        // 룸 정보 업데이트
        updateRoomInfo(roomInfo);

        // 창 설정
        setWindowTitle(QString::fromUtf8("블로커스 온라인 - %1 (%2님)").arg(roomInfo.roomName, myUsername));
        setMinimumSize(1400, 1000);
        resize(1600, 1200);

        // 화면 중앙에 배치
        QRect screenGeometry = QApplication::desktop()->screenGeometry();
        int x = (screenGeometry.width() - width()) / 2;
        int y = (screenGeometry.height() - height()) / 2;
        move(x, y);

        // 환영 메시지
        addSystemMessage(QString::fromUtf8("%1님이 '%2' 방에 입장했습니다.").arg(myUsername, roomInfo.roomName));

        qDebug() << QString::fromUtf8("GameRoomWindow 생성 완료: 방 %1").arg(roomInfo.roomId);
    }

    GameRoomWindow::~GameRoomWindow()
    {
        if (m_gameManager) {
            delete m_gameManager;
        }
    }

    void GameRoomWindow::setupUI()
    {
        m_centralWidget = new QWidget(this);
        setCentralWidget(m_centralWidget);

        setupMainLayout();
    }

    void GameRoomWindow::setupMainLayout()
    {
        m_mainLayout = new QVBoxLayout(m_centralWidget);
        m_mainLayout->setContentsMargins(15, 15, 15, 15);
        m_mainLayout->setSpacing(10);

        // 상단 룸 정보
        setupRoomInfoPanel();

        // 플레이어 슬롯들
        setupPlayerSlotsPanel();

        // 메인 게임 영역 (게임보드 + 채팅)
        QWidget* mainGameArea = new QWidget();
        QHBoxLayout* mainGameLayout = new QHBoxLayout(mainGameArea);
        mainGameLayout->setContentsMargins(0, 0, 0, 0);
        mainGameLayout->setSpacing(10);

        // 게임 영역 (보드 + 팔레트)
        setupGameArea();

        // 채팅 패널
        setupChatPanel();

        mainGameLayout->addWidget(m_gameArea, 3);    // 게임 영역이 더 큰 비중
        mainGameLayout->addWidget(m_chatPanel, 1);   // 채팅은 작은 비중

        // 하단 컨트롤
        setupControlsPanel();

        // 메인 레이아웃에 추가
        m_mainLayout->addWidget(m_roomInfoPanel);      // 고정 높이
        m_mainLayout->addWidget(m_playerSlotsPanel);   // 고정 높이
        m_mainLayout->addWidget(mainGameArea, 1);      // 확장 가능
        m_mainLayout->addWidget(m_controlsPanel);      // 고정 높이
    }

    void GameRoomWindow::setupRoomInfoPanel()
    {
        m_roomInfoPanel = new QWidget();
        m_roomInfoPanel->setFixedHeight(60);

        QHBoxLayout* layout = new QHBoxLayout(m_roomInfoPanel);
        layout->setContentsMargins(20, 10, 20, 10);

        m_roomNameLabel = new QLabel();
        m_roomNameLabel->setStyleSheet("font-size: 18px; font-weight: bold; color: #2c3e50;");

        m_roomStatusLabel = new QLabel();
        m_roomStatusLabel->setStyleSheet("font-size: 14px; color: #7f8c8d;");

        m_currentTurnLabel = new QLabel();
        m_currentTurnLabel->setStyleSheet("font-size: 14px; font-weight: bold; color: #3498db;");

        layout->addWidget(m_roomNameLabel);
        layout->addStretch();
        layout->addWidget(m_roomStatusLabel);
        layout->addStretch();
        layout->addWidget(m_currentTurnLabel);
    }

    void GameRoomWindow::setupPlayerSlotsPanel()
    {
        m_playerSlotsPanel = new QWidget();
        m_playerSlotsPanel->setFixedHeight(270);

        m_slotsLayout = new QHBoxLayout(m_playerSlotsPanel);
        m_slotsLayout->setContentsMargins(10, 10, 10, 10);
        m_slotsLayout->setSpacing(15);

        // 4개 플레이어 슬롯 생성
        for (int i = 0; i < 4; ++i) {
            PlayerColor color = static_cast<PlayerColor>(i + 1); // Blue=1, Yellow=2, Red=3, Green=4
            PlayerSlotWidget* slotWidget = new PlayerSlotWidget(color, this);

            // 시그널 연결
            connect(slotWidget, &PlayerSlotWidget::addAIRequested,
                this, &GameRoomWindow::onAddAIRequested);
            connect(slotWidget, &PlayerSlotWidget::removePlayerRequested,
                this, &GameRoomWindow::onRemovePlayerRequested);
            connect(slotWidget, &PlayerSlotWidget::kickPlayerRequested,
                this, &GameRoomWindow::onKickPlayerRequested);

            m_playerSlotWidgets.append(slotWidget);
            m_slotsLayout->addWidget(slotWidget);
        }

        m_slotsLayout->addStretch();
    }

    void GameRoomWindow::setupGameArea()
    {
        m_gameArea = new QWidget();

        QVBoxLayout* gameLayout = new QVBoxLayout(m_gameArea);
        gameLayout->setContentsMargins(10, 10, 10, 10);
        gameLayout->setSpacing(10);

        // 게임보드와 팔레트를 위한 스플리터
        m_gameSplitter = new QSplitter(Qt::Horizontal);

        // 게임보드 생성
        m_gameBoard = new GameBoard();
        m_gameBoard->setGameLogic(&m_gameManager->getGameLogic());
        m_gameBoard->setMinimumSize(300, 300);

        // 블록 팔레트 생성
        m_blockPalette = new ImprovedGamePalette();
        m_blockPalette->setMinimumWidth(300);
        m_blockPalette->setMaximumWidth(400);

        m_gameSplitter->addWidget(m_gameBoard);
        m_gameSplitter->addWidget(m_blockPalette);
        m_gameSplitter->setStretchFactor(0, 3); // 게임보드가 더 큰 비중
        m_gameSplitter->setStretchFactor(1, 1); // 팔레트는 작은 비중

        gameLayout->addWidget(m_gameSplitter);

        // 시그널 연결
        connect(m_gameBoard, &GameBoard::cellClicked, this, &GameRoomWindow::onCellClicked);
        connect(m_gameBoard, &GameBoard::cellHovered, this, &GameRoomWindow::onCellHovered);
        connect(m_gameBoard, &GameBoard::blockPlacedSuccessfully, this, &GameRoomWindow::onBlockPlacedSuccessfully);
        connect(m_blockPalette, &ImprovedGamePalette::blockSelected, this, &GameRoomWindow::onBlockSelected);
    }

    void GameRoomWindow::setupChatPanel()
    {
        m_chatPanel = new QWidget();
        m_chatPanel->setMinimumWidth(300);
        m_chatPanel->setMaximumWidth(400);

        QVBoxLayout* chatLayout = new QVBoxLayout(m_chatPanel);
        chatLayout->setContentsMargins(10, 10, 10, 10);
        chatLayout->setSpacing(10);

        // 채팅 제목
        QLabel* chatTitle = new QLabel(QString::fromUtf8("💬 방 채팅"));
        chatTitle->setStyleSheet("font-size: 14px; font-weight: bold; color: #8e44ad;");

        // 채팅 디스플레이
        m_chatDisplay = new QTextEdit();
        m_chatDisplay->setReadOnly(true);
        m_chatDisplay->setMinimumHeight(400);

        // 채팅 입력
        QWidget* chatInputWidget = new QWidget();
        QHBoxLayout* chatInputLayout = new QHBoxLayout(chatInputWidget);
        chatInputLayout->setContentsMargins(0, 0, 0, 0);
        chatInputLayout->setSpacing(5);

        m_chatInput = new QLineEdit();
        m_chatInput->setPlaceholderText(QString::fromUtf8("메시지를 입력하세요..."));
        m_chatInput->setMaxLength(200);

        m_chatSendButton = new QPushButton(QString::fromUtf8("전송"));
        m_chatSendButton->setFixedSize(60, 30);

        chatInputLayout->addWidget(m_chatInput);
        chatInputLayout->addWidget(m_chatSendButton);

        chatLayout->addWidget(chatTitle);
        chatLayout->addWidget(m_chatDisplay, 1);
        chatLayout->addWidget(chatInputWidget);

        // 시그널 연결
        connect(m_chatSendButton, &QPushButton::clicked, this, &GameRoomWindow::onChatSendClicked);
        connect(m_chatInput, &QLineEdit::returnPressed, this, &GameRoomWindow::onChatReturnPressed);
    }

    void GameRoomWindow::setupControlsPanel()
    {
        m_controlsPanel = new QWidget();
        m_controlsPanel->setFixedHeight(50);

        QHBoxLayout* layout = new QHBoxLayout(m_controlsPanel);
        layout->setContentsMargins(20, 10, 20, 10);
        layout->setSpacing(15);

        // 왼쪽 버튼들
        m_leaveRoomButton = new QPushButton(QString::fromUtf8("🚪 방 나가기"));
        m_leaveRoomButton->setFixedHeight(35);

        m_gameStartButton = new QPushButton(QString::fromUtf8("🎮 게임 시작"));
        m_gameStartButton->setFixedHeight(35);

        m_gameResetButton = new QPushButton(QString::fromUtf8("🔄 게임 초기화"));
        m_gameResetButton->setFixedHeight(35);
        m_gameResetButton->setVisible(false); // 게임 중일 때만 표시

        // 중앙 게임 상태
        m_gameStatusLabel = new QLabel(QString::fromUtf8("게임 대기 중"));
        m_gameStatusLabel->setStyleSheet("font-size: 14px; font-weight: bold; color: #34495e;");

        // 오른쪽 좌표 표시
        m_coordinateLabel = new QLabel(QString::fromUtf8("보드 위에서 마우스를 움직이세요"));
        m_coordinateLabel->setStyleSheet("font-size: 12px; color: #7f8c8d;");

        layout->addWidget(m_leaveRoomButton);
        layout->addWidget(m_gameStartButton);
        layout->addWidget(m_gameResetButton);
        layout->addStretch();
        layout->addWidget(m_gameStatusLabel);
        layout->addStretch();
        layout->addWidget(m_coordinateLabel);

        // 시그널 연결
        connect(m_leaveRoomButton, &QPushButton::clicked, this, &GameRoomWindow::onLeaveRoomClicked);
        connect(m_gameStartButton, &QPushButton::clicked, this, &GameRoomWindow::onGameStartClicked);
        connect(m_gameResetButton, &QPushButton::clicked, this, &GameRoomWindow::onGameResetClicked);
    }

    void GameRoomWindow::setupMenuBar()
    {
        QMenuBar* menuBar = this->menuBar();

        // 게임 메뉴
        QMenu* gameMenu = menuBar->addMenu(QString::fromUtf8("게임"));
        QAction* startGameAction = gameMenu->addAction(QString::fromUtf8("게임 시작"));
        QAction* resetGameAction = gameMenu->addAction(QString::fromUtf8("게임 초기화"));
        gameMenu->addSeparator();
        QAction* leaveRoomAction = gameMenu->addAction(QString::fromUtf8("방 나가기"));

        connect(startGameAction, &QAction::triggered, this, &GameRoomWindow::onGameStartClicked);
        connect(resetGameAction, &QAction::triggered, this, &GameRoomWindow::onGameResetClicked);
        connect(leaveRoomAction, &QAction::triggered, this, &GameRoomWindow::onLeaveRoomClicked);

        // 설정 메뉴
        QMenu* settingsMenu = menuBar->addMenu(QString::fromUtf8("설정"));
        QAction* preferencesAction = settingsMenu->addAction(QString::fromUtf8("환경설정"));

        // 도움말 메뉴
        QMenu* helpMenu = menuBar->addMenu(QString::fromUtf8("도움말"));
        QAction* rulesAction = helpMenu->addAction(QString::fromUtf8("게임 규칙"));
        QAction* aboutAction = helpMenu->addAction(QString::fromUtf8("정보"));
    }

    void GameRoomWindow::setupStatusBar()
    {
        QStatusBar* statusBar = this->statusBar();
        statusBar->showMessage(QString::fromUtf8("방에 연결되었습니다."));

        // 오른쪽에 추가 정보 표시
        QLabel* connectionLabel = new QLabel(QString::fromUtf8("서버 연결: 정상"));
        connectionLabel->setStyleSheet("color: #27ae60; font-weight: bold;");
        statusBar->addPermanentWidget(connectionLabel);
    }

    void GameRoomWindow::setupStyles()
    {
        // 메인 윈도우 배경
        setStyleSheet(
            "QMainWindow { background-color: #ecf0f1; }"
        );

        // 룸 정보 패널
        m_roomInfoPanel->setStyleSheet(
            "QWidget { "
            "background: qlineargradient(x1:0, y1:0, x2:0, y2:1, "
            "stop:0 #3498db, stop:1 #2980b9); "
            "border-radius: 8px; "
            "}"
        );

        // 플레이어 슬롯 패널
        m_playerSlotsPanel->setStyleSheet(
            "QWidget { "
            "background-color: #34495e; "
            "border-radius: 10px; "
            "}"
        );

        // 게임 영역
        m_gameArea->setStyleSheet(
            "QWidget { "
            "background-color: white; "
            "border: 1px solid #bdc3c7; "
            "border-radius: 8px; "
            "}"
        );

        // 채팅 패널
        m_chatPanel->setStyleSheet(
            "QWidget { "
            "background-color: white; "
            "border: 1px solid #bdc3c7; "
            "border-radius: 8px; "
            "}"
        );

        // 컨트롤 패널
        m_controlsPanel->setStyleSheet(
            "QWidget { "
            "background-color: #f8f9fa; "
            "border: 1px solid #dee2e6; "
            "border-radius: 8px; "
            "}"
        );

        // 버튼 스타일
        QString buttonStyle =
            "QPushButton { "
            "border: none; "
            "border-radius: 6px; "
            "font-weight: bold; "
            "font-size: 13px; "
            "padding: 8px 15px; "
            "} ";

        m_leaveRoomButton->setStyleSheet(buttonStyle +
            "QPushButton { background-color: #e74c3c; color: white; } "
            "QPushButton:hover { background-color: #c0392b; }");

        m_gameStartButton->setStyleSheet(buttonStyle +
            "QPushButton { background-color: #27ae60; color: white; } "
            "QPushButton:hover { background-color: #229954; }");

        m_gameResetButton->setStyleSheet(buttonStyle +
            "QPushButton { background-color: #f39c12; color: white; } "
            "QPushButton:hover { background-color: #e67e22; }");

        m_chatSendButton->setStyleSheet(buttonStyle +
            "QPushButton { background-color: #8e44ad; color: white; } "
            "QPushButton:hover { background-color: #732d91; }");

        // 채팅 스타일
        m_chatDisplay->setStyleSheet(
            "QTextEdit { "
            "border: 1px solid #ddd; "
            "border-radius: 6px; "
            "background-color: #fafafa; "
            "font-family: 'Consolas', monospace; "
            "font-size: 12px; "
            "}"
        );

        m_chatInput->setStyleSheet(
            "QLineEdit { "
            "border: 2px solid #ddd; "
            "border-radius: 6px; "
            "padding: 6px 10px; "
            "font-size: 13px; "
            "} "
            "QLineEdit:focus { border-color: #3498db; }"
        );
    }

    // ========================================
    // 업데이트 함수들
    // ========================================

    void GameRoomWindow::updateRoomInfo(const GameRoomInfo& roomInfo)
    {
        m_roomInfo = roomInfo;
        updateRoomInfoDisplay();
        updatePlayerSlotsDisplay();
        updateGameControlsState();
    }

    void GameRoomWindow::updatePlayerSlot(PlayerColor color, const PlayerSlot& slot)
    {
        // 해당 색상의 슬롯 찾아서 업데이트
        for (int i = 0; i < m_playerSlotWidgets.size(); ++i) {
            if (m_playerSlotWidgets[i]->getColor() == color) {
                m_playerSlotWidgets[i]->updatePlayerSlot(slot);
                m_playerSlotWidgets[i]->setMySlot(slot.username == m_myUsername);

                // 룸 정보도 업데이트
                m_roomInfo.playerSlots[i] = slot;
                break;
            }
        }

        updateGameControlsState();
    }

    void GameRoomWindow::updateRoomInfoDisplay()
    {
        m_roomNameLabel->setText(QString::fromUtf8("🏠 %1").arg(m_roomInfo.roomName));

        QString statusText = QString::fromUtf8("방장: %1 | %2/%3명")
            .arg(m_roomInfo.hostUsername)
            .arg(m_roomInfo.getCurrentPlayerCount())
            .arg(m_roomInfo.maxPlayers);
        m_roomStatusLabel->setText(statusText);

        if (m_isGameStarted) {
            PlayerColor currentTurn = m_gameManager->getGameLogic().getCurrentPlayer();
            QString turnText = QString::fromUtf8("현재 턴: %1")
                .arg(Utils::playerColorToString(currentTurn));
            m_currentTurnLabel->setText(turnText);
        }
        else {
            m_currentTurnLabel->setText(QString::fromUtf8("게임 대기 중"));
        }
    }

    void GameRoomWindow::updatePlayerSlotsDisplay()
    {
        for (int i = 0; i < m_playerSlotWidgets.size() && i < m_roomInfo.playerSlots.size(); ++i) {
            m_playerSlotWidgets[i]->updatePlayerSlot(m_roomInfo.playerSlots[i]);
            m_playerSlotWidgets[i]->setMySlot(m_roomInfo.playerSlots[i].username == m_myUsername);
        }
    }

    void GameRoomWindow::updateGameControlsState()
    {
        bool amHost = isHost();
        bool canStart = canStartGame();

        m_gameStartButton->setEnabled(amHost && canStart && !m_isGameStarted);
        m_gameStartButton->setVisible(!m_isGameStarted);
        m_gameResetButton->setVisible(m_isGameStarted && amHost);

        // 게임 상태 라벨 업데이트
        if (m_isGameStarted) {
            PlayerColor currentTurn = m_gameManager->getGameLogic().getCurrentPlayer();
            if (m_roomInfo.isMyTurn(m_myUsername, currentTurn)) {
                m_gameStatusLabel->setText(QString::fromUtf8("내 턴입니다!"));
                m_gameStatusLabel->setStyleSheet("font-size: 14px; font-weight: bold; color: #27ae60;");
            }
            else {
                QString turnPlayerName = "";
                for (const auto& slot : m_roomInfo.playerSlots) {
                    if (slot.color == currentTurn) {
                        turnPlayerName = slot.getDisplayName();
                        break;
                    }
                }
                m_gameStatusLabel->setText(QString::fromUtf8("%1 턴").arg(turnPlayerName));
                m_gameStatusLabel->setStyleSheet("font-size: 14px; font-weight: bold; color: #34495e;");
            }
        }
        else {
            if (canStart) {
                m_gameStatusLabel->setText(QString::fromUtf8("게임 시작 준비됨"));
                m_gameStatusLabel->setStyleSheet("font-size: 14px; font-weight: bold; color: #27ae60;");
            }
            else {
                m_gameStatusLabel->setText(QString::fromUtf8("플레이어 대기 중"));
                m_gameStatusLabel->setStyleSheet("font-size: 14px; font-weight: bold; color: #e74c3c;");
            }
        }
    }

    void GameRoomWindow::startGame()
    {
        m_isGameStarted = true;
        m_gameManager->startNewGame();

        // 게임보드에 게임 로직 연결
        m_gameBoard->setGameLogic(&m_gameManager->getGameLogic());
        m_gameBoard->clearAllBlocks();

        // 팔레트 초기화
        m_blockPalette->resetAllPlayerBlocks();
        m_blockPalette->setCurrentPlayer(PlayerColor::Blue); // 파란색부터 시작

        updateGameControlsState();
        updateRoomInfoDisplay();

        addSystemMessage(QString::fromUtf8("🎮 게임이 시작되었습니다!"));

        qDebug() << QString::fromUtf8("게임 시작됨");
    }

    void GameRoomWindow::endGame(const std::map<PlayerColor, int>& finalScores)
    {
        m_isGameStarted = false;

        showGameResults(finalScores);
        updateGameControlsState();
        updateRoomInfoDisplay();

        addSystemMessage(QString::fromUtf8("🏆 게임이 종료되었습니다!"));

        qDebug() << QString::fromUtf8("게임 종료됨");
    }

    void GameRoomWindow::updateGameState(const GameStateManager& gameManager)
    {
        // 게임 상태 동기화
        if (m_gameManager) {
            // 현재 플레이어 업데이트
            PlayerColor currentPlayer = gameManager.getGameLogic().getCurrentPlayer();
            m_blockPalette->setCurrentPlayer(currentPlayer);

            updateGameControlsState();
            updateRoomInfoDisplay();
        }
    }

    // ========================================
    // 이벤트 핸들러들
    // ========================================

    void GameRoomWindow::onLeaveRoomClicked()
    {
        int ret = QMessageBox::question(this, QString::fromUtf8("방 나가기"),
            QString::fromUtf8("정말 방을 나가시겠습니까?"),
            QMessageBox::Yes | QMessageBox::No);

        if (ret == QMessageBox::Yes) {
            emit leaveRoomRequested();
        }
    }

    void GameRoomWindow::onGameStartClicked()
    {
        if (!canStartGame()) {
            QMessageBox::information(this, QString::fromUtf8("게임 시작 불가"),
                QString::fromUtf8("게임을 시작하려면 최소 2명의 플레이어가 필요합니다."));
            return;
        }

        emit gameStartRequested();
    }

    void GameRoomWindow::onGameResetClicked()
    {
        int ret = QMessageBox::question(this, QString::fromUtf8("게임 초기화"),
            QString::fromUtf8("게임을 초기화하시겠습니까?\n모든 진행 상황이 사라집니다."),
            QMessageBox::Yes | QMessageBox::No);

        if (ret == QMessageBox::Yes) {
            emit gameResetRequested();
        }
    }

    void GameRoomWindow::onChatSendClicked()
    {
        QString message = m_chatInput->text().trimmed();
        if (message.isEmpty()) return;

        emit chatMessageSent(message);
        m_chatInput->clear();
        m_chatInput->setFocus();
    }

    void GameRoomWindow::onChatReturnPressed()
    {
        onChatSendClicked();
    }

    void GameRoomWindow::onAddAIRequested(PlayerColor color, int difficulty)
    {
        if (!isHost()) {
            QMessageBox::information(this, QString::fromUtf8("권한 없음"),
                QString::fromUtf8("방장만 AI를 추가할 수 있습니다."));
            return;
        }

        emit addAIPlayerRequested(color, difficulty);
    }

    void GameRoomWindow::onRemovePlayerRequested(PlayerColor color)
    {
        // 내 슬롯이면 방 나가기, 아니면 제거 요청
        PlayerSlot* slot = findPlayerSlot(color);
        if (slot && slot->username == m_myUsername) {
            onLeaveRoomClicked();
        }
        else {
            emit removePlayerRequested(color);
        }
    }

    void GameRoomWindow::onKickPlayerRequested(PlayerColor color)
    {
        if (!isHost()) {
            QMessageBox::information(this, QString::fromUtf8("권한 없음"),
                QString::fromUtf8("방장만 플레이어를 강퇴할 수 있습니다."));
            return;
        }

        emit kickPlayerRequested(color);
    }

    void GameRoomWindow::onCellClicked(int row, int col)
    {
        QString message = QString::fromUtf8("클릭된 셀: (%1, %2)").arg(row).arg(col);
        statusBar()->showMessage(message, 2000);

        qDebug() << QString::fromUtf8("셀 클릭: (%1, %2)").arg(row).arg(col);
    }

    void GameRoomWindow::onCellHovered(int row, int col)
    {
        QString message = QString::fromUtf8("마우스 위치: (%1, %2)").arg(row).arg(col);
        m_coordinateLabel->setText(message);
    }

    void GameRoomWindow::onBlockPlacedSuccessfully(BlockType blockType, PlayerColor player)
    {
        qDebug() << QString::fromUtf8("블록 배치 성공: %1 플레이어의 %2 블록")
            .arg(Utils::playerColorToString(player))
            .arg(BlockFactory::getBlockName(blockType));

        // 팔레트에서 블록 제거
        m_blockPalette->removeBlock(player, blockType);

        // 다음 턴으로 이동
        m_gameManager->nextTurn();
        updateGameState(*m_gameManager);

        // 채팅 메시지 추가
        QString playerName = "";
        for (const auto& slot : m_roomInfo.playerSlots) {
            if (slot.color == player) {
                playerName = slot.getDisplayName();
                break;
            }
        }

        addSystemMessage(QString::fromUtf8("%1이(가) %2 블록을 배치했습니다.")
            .arg(playerName)
            .arg(BlockFactory::getBlockName(blockType)));
    }

    void GameRoomWindow::onBlockSelected(const Block& block)
    {
        qDebug() << QString::fromUtf8("블록 선택됨: %1")
            .arg(BlockFactory::getBlockName(block.getType()));

        if (m_gameBoard) {
            m_gameBoard->setSelectedBlock(block);
            m_gameBoard->setFocus();
        }
    }

    // ========================================
    // 유틸리티 함수들
    // ========================================

    void GameRoomWindow::addChatMessage(const QString& username, const QString& message, bool isSystem)
    {
        QString formattedMsg = formatChatMessage(username, message, isSystem);
        m_chatDisplay->append(formattedMsg);

        m_chatHistory.append(formattedMsg);

        // 채팅 히스토리가 너무 길어지면 앞부분 제거
        if (m_chatHistory.size() > 500) {
            m_chatHistory.removeFirst();
        }

        scrollChatToBottom();
    }

    void GameRoomWindow::addSystemMessage(const QString& message)
    {
        addChatMessage(QString::fromUtf8("시스템"), message, true);
    }

    void GameRoomWindow::scrollChatToBottom()
    {
        QTextCursor cursor = m_chatDisplay->textCursor();
        cursor.movePosition(QTextCursor::End);
        m_chatDisplay->setTextCursor(cursor);
    }

    QString GameRoomWindow::formatChatMessage(const QString& username, const QString& message, bool isSystem)
    {
        QString timeStr = QDateTime::currentDateTime().toString("hh:mm");

        if (isSystem) {
            return QString("<span style='color: #8e44ad; font-weight: bold;'>[%1] %2: %3</span>")
                .arg(timeStr, username, message);
        }
        else {
            QString colorCode = (username == m_myUsername) ? "#3498db" : "#2c3e50";
            return QString("<span style='color: %1;'>[%2] <b>%3:</b> %4</span>")
                .arg(colorCode, timeStr, username, message);
        }
    }

    bool GameRoomWindow::isHost() const
    {
        return m_roomInfo.hostUsername == m_myUsername;
    }

    bool GameRoomWindow::canStartGame() const
    {
        int playerCount = m_roomInfo.getCurrentPlayerCount();
        return playerCount >= 2 && !m_isGameStarted;
    }

    bool GameRoomWindow::canAddAI() const
    {
        return isHost() && m_roomInfo.getCurrentPlayerCount() < m_roomInfo.maxPlayers;
    }

    bool GameRoomWindow::canKickPlayer(PlayerColor color)
    {
        if (!isHost()) return false;

        PlayerSlot* slot = findPlayerSlot(color);
        return slot && !slot->isEmpty() && slot->username != m_myUsername;
    }

    PlayerSlot* GameRoomWindow::findPlayerSlot(PlayerColor color)
    {
        for (auto& slot : m_roomInfo.playerSlots) {
            if (slot.color == color) {
                return &slot;
            }
        }
        return nullptr;
    }

    PlayerSlot* GameRoomWindow::findPlayerSlot(const QString& username)
    {
        for (auto& slot : m_roomInfo.playerSlots) {
            if (slot.username == username) {
                return &slot;
            }
        }
        return nullptr;
    }

    PlayerColor GameRoomWindow::getNextAvailableColor() const
    {
        std::vector<PlayerColor> colors = {
            PlayerColor::Blue, PlayerColor::Yellow,
            PlayerColor::Red, PlayerColor::Green
        };

        for (PlayerColor color : colors) {
            bool found = false;
            for (const auto& slot : m_roomInfo.playerSlots) {
                if (slot.color == color && !slot.isEmpty()) {
                    found = true;
                    break;
                }
            }
            if (!found) {
                return color;
            }
        }

        return PlayerColor::None;
    }

    void GameRoomWindow::showGameResults(const std::map<PlayerColor, int>& scores)
    {
        QString resultText = QString::fromUtf8("🏆 게임 결과\n\n");

        // 점수 순으로 정렬하기 위한 벡터
        std::vector<std::pair<PlayerColor, int>> sortedScores(scores.begin(), scores.end());
        std::sort(sortedScores.begin(), sortedScores.end(),
            [](const auto& a, const auto& b) { return a.second > b.second; });

        int rank = 1;
        for (const auto& pair : sortedScores) {
            QString playerName = "";
            for (const auto& slot : m_roomInfo.playerSlots) {
                if (slot.color == pair.first) {
                    playerName = slot.getDisplayName();
                    break;
                }
            }

            QString rankIcon = (rank == 1) ? "🥇" : (rank == 2) ? "🥈" : (rank == 3) ? "🥉" : "🏅";
            resultText += QString("%1 %2등: %3 (%4점)\n")
                .arg(rankIcon).arg(rank).arg(playerName).arg(pair.second);
            rank++;
        }

        QMessageBox::information(this, QString::fromUtf8("게임 종료"), resultText);
    }

    void GameRoomWindow::enableGameControls(bool enabled)
    {
        if (m_gameBoard) {
            m_gameBoard->setBoardReadOnly(!enabled);
        }

        if (m_blockPalette) {
            m_blockPalette->setEnabled(enabled);
        }
    }

    void GameRoomWindow::closeEvent(QCloseEvent* event)
    {
        int ret = QMessageBox::question(this, QString::fromUtf8("방 나가기"),
            QString::fromUtf8("정말 방을 나가시겠습니까?"),
            QMessageBox::Yes | QMessageBox::No);

        if (ret == QMessageBox::Yes) {
            emit leaveRoomRequested();
            event->accept();
        }
        else {
            event->ignore();
        }
    }

    void GameRoomWindow::resizeEvent(QResizeEvent* event)
    {
        QMainWindow::resizeEvent(event);

        // 창 크기에 따라 스플리터 비율 조정
        if (m_gameSplitter) {
            QList<int> sizes = m_gameSplitter->sizes();
            if (sizes.size() == 2) {
                int total = sizes[0] + sizes[1];
                m_gameSplitter->setSizes({ total * 3 / 4, total / 4 });
            }
        }
    }

} // namespace Blokus

#include "ui/GameRoomWindow.moc"