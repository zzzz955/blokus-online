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

    void PlayerSlotWidget::setupStyles()
    {
        // 전체 위젯 스타일
        setStyleSheet(
            "PlayerSlotWidget { "
            "background-color: white; "
            "border: 2px solid #ddd; "
            "border-radius: 8px; "  // 모서리 반경 축소
            "}"
        );

        // 색상 프레임 스타일
        QColor playerColor = getPlayerColor();
        m_colorFrame->setStyleSheet(QString(
            "QFrame { "
            "background-color: %1; "
            "border: 2px solid %2; "  // 테두리 두께 축소
            "border-radius: 6px; "    // 모서리 반경 축소
            "}"
        ).arg(playerColor.name(), playerColor.darker(150).name()));

        // 버튼 스타일 - 크기 축소
        m_actionButton->setStyleSheet(
            "QPushButton { "
            "background-color: #3498db; "
            "border: none; "
            "border-radius: 4px; "    // 모서리 반경 축소
            "color: white; "
            "font-weight: bold; "
            "font-size: 10px; "       // 폰트 크기 축소
            "padding: 4px; "          // 패딩 축소
            "} "
            "QPushButton:hover { "
            "background-color: #2980b9; "
            "} "
            "QPushButton:pressed { "
            "background-color: #21618c; "
            "}"
        );
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

    // client/src/ui/GameRoomWindow.cpp - PlayerSlotWidget 크기 축소 및 개선

    void PlayerSlotWidget::setupUI()
    {
        setFixedSize(160, 130);  // 200x250 → 160x130 대폭 축소

        m_mainLayout = new QVBoxLayout(this);
        m_mainLayout->setContentsMargins(8, 6, 8, 6);  // 여백 줄임
        m_mainLayout->setSpacing(4);  // 간격 줄임

        // 색상 표시 프레임 - 높이 축소
        m_colorFrame = new QFrame();
        m_colorFrame->setFixedHeight(25);  // 40px → 25px 축소
        m_colorFrame->setFrameStyle(QFrame::Box | QFrame::Raised);
        m_colorFrame->setLineWidth(2);

        m_colorLabel = new QLabel(getColorName());
        m_colorLabel->setAlignment(Qt::AlignCenter);
        m_colorLabel->setStyleSheet("font-weight: bold; font-size: 11px; color: white;");  // 폰트 크기 축소

        QVBoxLayout* colorLayout = new QVBoxLayout(m_colorFrame);
        colorLayout->setContentsMargins(2, 2, 2, 2);
        colorLayout->addWidget(m_colorLabel);

        // 호스트 표시 - 높이 축소
        m_hostIndicator = new QWidget();
        m_hostIndicator->setFixedHeight(15);  // 20px → 15px
        QLabel* hostLabel = new QLabel(QString::fromUtf8("👑"));
        hostLabel->setAlignment(Qt::AlignCenter);
        hostLabel->setStyleSheet("font-size: 10px; font-weight: bold; color: #f39c12;");
        QVBoxLayout* hostLayout = new QVBoxLayout(m_hostIndicator);
        hostLayout->setContentsMargins(0, 0, 0, 0);
        hostLayout->addWidget(hostLabel);
        m_hostIndicator->hide();

        // 플레이어 정보 - 폰트 크기 축소
        m_usernameLabel = new QLabel(QString::fromUtf8("빈 슬롯"));
        m_usernameLabel->setAlignment(Qt::AlignCenter);
        m_usernameLabel->setStyleSheet("font-size: 11px; font-weight: bold;");

        m_statusLabel = new QLabel(QString::fromUtf8("대기 중"));
        m_statusLabel->setAlignment(Qt::AlignCenter);
        m_statusLabel->setStyleSheet("font-size: 10px; color: #7f8c8d;");

        m_scoreLabel = new QLabel(QString::fromUtf8("점수: 0"));
        m_scoreLabel->setAlignment(Qt::AlignCenter);
        m_scoreLabel->setStyleSheet("font-size: 10px; color: #34495e;");

        // 남은 블록 수 표시 추가
        m_remainingBlocksLabel = new QLabel(QString::fromUtf8("블록: 21"));
        m_remainingBlocksLabel->setAlignment(Qt::AlignCenter);
        m_remainingBlocksLabel->setStyleSheet("font-size: 9px; color: #95a5a6;");

        // 액션 버튼 - 높이 축소
        m_actionButton = new QPushButton(QString::fromUtf8("AI 추가"));
        m_actionButton->setFixedHeight(25);  // 35px → 25px

        // 레이아웃 구성
        m_mainLayout->addWidget(m_colorFrame);
        m_mainLayout->addWidget(m_hostIndicator);
        m_mainLayout->addWidget(m_usernameLabel);
        m_mainLayout->addWidget(m_statusLabel);
        m_mainLayout->addWidget(m_scoreLabel);
        m_mainLayout->addWidget(m_remainingBlocksLabel);
        m_mainLayout->addStretch();
        m_mainLayout->addWidget(m_actionButton);

        // 시그널 연결
        connect(m_actionButton, &QPushButton::clicked, this, &PlayerSlotWidget::onAddAIClicked);
    }

    void PlayerSlotWidget::updatePlayerSlot(const PlayerSlot& slot)
    {
        m_currentSlot = slot;

        // 사용자명 업데이트
        QString displayName = slot.getDisplayName();
        if (displayName.length() > 12) {  // 길이 제한
            displayName = displayName.left(10) + "...";
        }
        m_usernameLabel->setText(displayName);

        // 호스트 표시 업데이트
        m_hostIndicator->setVisible(slot.isHost);

        // 상태 업데이트
        QString statusText;
        if (slot.isEmpty()) {
            statusText = QString::fromUtf8("빈 슬롯");
            m_statusLabel->setStyleSheet("font-size: 10px; color: #95a5a6;");
        }
        else if (slot.isAI) {
            QString difficultyText = (slot.aiDifficulty == 1) ? "쉬움" :
                (slot.aiDifficulty == 2) ? "보통" : "어려움";
            statusText = QString::fromUtf8("AI %1").arg(difficultyText);
            m_statusLabel->setStyleSheet("font-size: 10px; color: #9b59b6;");
        }
        else {
            statusText = slot.isReady ? QString::fromUtf8("준비됨") : QString::fromUtf8("대기 중");
            m_statusLabel->setStyleSheet(QString("font-size: 10px; color: %1;")
                .arg(slot.isReady ? "#27ae60" : "#e74c3c"));
        }
        m_statusLabel->setText(statusText);

        // 점수 및 남은 블록 수 업데이트
        m_scoreLabel->setText(QString::fromUtf8("점수: %1").arg(slot.score));
        m_remainingBlocksLabel->setText(QString::fromUtf8("블록: %1").arg(slot.remainingBlocks));

        // 액션 버튼 업데이트
        updateActionButton();
    }

    // AI 추가 다이얼로그 개선
    void PlayerSlotWidget::onAddAIClicked()
    {
        // 커스텀 다이얼로그 생성
        QDialog dialog(this);
        dialog.setWindowTitle(QString::fromUtf8("AI 플레이어 추가"));
        dialog.setFixedSize(300, 200);  // 적절한 크기

        // 베이지색 배경 스타일
        dialog.setStyleSheet(
            "QDialog { "
            "background-color: #f5f5dc; "  // 베이지색 배경
            "border-radius: 10px; "
            "} "
            "QLabel { "
            "color: #2c3e50; "
            "font-size: 13px; "
            "} "
            "QPushButton { "
            "background-color: #3498db; "
            "border: none; "
            "border-radius: 6px; "
            "color: white; "
            "font-weight: bold; "
            "font-size: 12px; "
            "padding: 8px 15px; "
            "min-width: 80px; "
            "} "
            "QPushButton:hover { "
            "background-color: #2980b9; "
            "} "
            "QPushButton:pressed { "
            "background-color: #21618c; "
            "}"
        );

        QVBoxLayout* layout = new QVBoxLayout(&dialog);
        layout->setContentsMargins(20, 20, 20, 20);
        layout->setSpacing(15);

        // 설명 라벨
        QLabel* titleLabel = new QLabel(QString::fromUtf8("%1 위치에 AI 플레이어를 추가합니다.")
            .arg(getColorName()));
        titleLabel->setAlignment(Qt::AlignCenter);
        titleLabel->setStyleSheet("font-weight: bold; font-size: 14px; color: #2c3e50;");

        QLabel* descLabel = new QLabel(QString::fromUtf8("AI 난이도를 선택해주세요:"));
        descLabel->setAlignment(Qt::AlignCenter);
        descLabel->setStyleSheet("font-size: 12px; color: #34495e; margin-bottom: 10px;");

        // 난이도 버튼들
        QWidget* buttonWidget = new QWidget();
        QHBoxLayout* buttonLayout = new QHBoxLayout(buttonWidget);
        buttonLayout->setSpacing(10);

        QPushButton* easyButton = new QPushButton(QString::fromUtf8("🟢 쉬움"));
        QPushButton* normalButton = new QPushButton(QString::fromUtf8("🟡 보통"));
        QPushButton* hardButton = new QPushButton(QString::fromUtf8("🔴 어려움"));

        easyButton->setStyleSheet(easyButton->styleSheet() + "QPushButton { background-color: #27ae60; }");
        normalButton->setStyleSheet(normalButton->styleSheet() + "QPushButton { background-color: #f39c12; }");
        hardButton->setStyleSheet(hardButton->styleSheet() + "QPushButton { background-color: #e74c3c; }");

        buttonLayout->addWidget(easyButton);
        buttonLayout->addWidget(normalButton);
        buttonLayout->addWidget(hardButton);

        // 취소 버튼
        QPushButton* cancelButton = new QPushButton(QString::fromUtf8("취소"));
        cancelButton->setStyleSheet(cancelButton->styleSheet() +
            "QPushButton { background-color: #95a5a6; } "
            "QPushButton:hover { background-color: #7f8c8d; }");

        layout->addWidget(titleLabel);
        layout->addWidget(descLabel);
        layout->addWidget(buttonWidget);
        layout->addStretch();
        layout->addWidget(cancelButton);

        // 시그널 연결
        connect(easyButton, &QPushButton::clicked, [&dialog]() { dialog.done(1); });
        connect(normalButton, &QPushButton::clicked, [&dialog]() { dialog.done(2); });
        connect(hardButton, &QPushButton::clicked, [&dialog]() { dialog.done(3); });
        connect(cancelButton, &QPushButton::clicked, [&dialog]() { dialog.reject(); });

        int result = dialog.exec();
        if (result >= 1 && result <= 3) {
            emit addAIRequested(m_color, result);
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
        , m_gameBoard(nullptr)
        , m_chatPanel(nullptr)
        , m_chatDisplay(nullptr)
        , m_chatInput(nullptr)
        , m_chatSendButton(nullptr)
        , m_controlsPanel(nullptr)
        , m_leaveRoomButton(nullptr)
        , m_gameStartButton(nullptr)
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

        // 🔥 초기 상태에서는 게임보드와 팔레트 비활성화
        if (m_gameBoard) {
            m_gameBoard->setBoardReadOnly(true);  // 읽기 전용으로 설정
            m_gameBoard->clearSelection();
        }

        if (m_myBlockPalette) {
            m_myBlockPalette->setEnabled(false);  // 비활성화
            m_myBlockPalette->clearSelection();
        }

        // 창 설정 - 크기 축소
        setWindowTitle(QString::fromUtf8("블로커스 온라인 - %1 (%2님)").arg(roomInfo.roomName, myUsername));
        setMinimumSize(1200, 800);   // 기존 1400x1000 → 1200x800
        resize(1400, 900);           // 기존 1600x1200 → 1400x900

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
        m_mainLayout->setContentsMargins(10, 10, 10, 10);  // 여백 줄임
        m_mainLayout->setSpacing(8);  // 간격 줄임

        // 상단 룸 정보 - 높이 줄임
        setupRoomInfoPanel();

        // 플레이어 슬롯들 - 높이 대폭 줄임
        setupPlayerSlotsPanel();

        // 메인 게임 영역 (게임보드 + 내 팔레트 + 채팅)
        QWidget* mainGameArea = new QWidget();
        QHBoxLayout* mainGameLayout = new QHBoxLayout(mainGameArea);
        mainGameLayout->setContentsMargins(0, 0, 0, 0);
        mainGameLayout->setSpacing(8);

        // 게임 영역 (보드 + 내 팔레트만)
        setupGameArea();

        // 채팅 패널
        setupChatPanel();

        // 비율 조정: 게임보드가 훨씬 큰 비중
        mainGameLayout->addWidget(m_gameArea, 4);      // 게임 영역 비중 증가
        mainGameLayout->addWidget(m_chatPanel, 1);     // 채팅은 최소 비중

        // 하단 컨트롤 - 높이 줄임
        setupControlsPanel();

        // 메인 레이아웃에 추가 - 비율 조정
        m_mainLayout->addWidget(m_roomInfoPanel);          // 40px 고정
        m_mainLayout->addWidget(m_playerSlotsPanel);       // 150px 고정 (기존 270px에서 축소)
        m_mainLayout->addWidget(mainGameArea, 1);          // 나머지 모든 공간
        m_mainLayout->addWidget(m_controlsPanel);          // 40px 고정
    }

    void GameRoomWindow::setupRoomInfoPanel()
    {
        m_roomInfoPanel = new QWidget();
        m_roomInfoPanel->setFixedHeight(40);

        QHBoxLayout* layout = new QHBoxLayout(m_roomInfoPanel);
        layout->setContentsMargins(15, 5, 15, 5);

        m_roomNameLabel = new QLabel();
        m_roomNameLabel->setStyleSheet("font-size: 16px; font-weight: bold; color: #2c3e50;");

        m_roomStatusLabel = new QLabel();
        m_roomStatusLabel->setStyleSheet("font-size: 12px; color: #7f8c8d;");

        m_currentTurnLabel = new QLabel();
        m_currentTurnLabel->setStyleSheet("font-size: 12px; font-weight: bold; color: #3498db;");

        // 방 나가기 버튼을 우측 상단에 배치
        m_leaveRoomButton = new QPushButton(QString::fromUtf8("🚪 방 나가기"));
        m_leaveRoomButton->setFixedSize(100, 30);
        m_leaveRoomButton->setStyleSheet(
            "QPushButton { "
            "background-color: #e74c3c; color: white; border: none; "
            "border-radius: 6px; font-weight: bold; font-size: 11px; "
            "} "
            "QPushButton:hover { background-color: #c0392b; }"
        );

        layout->addWidget(m_roomNameLabel);
        layout->addStretch();
        layout->addWidget(m_roomStatusLabel);
        layout->addStretch();
        layout->addWidget(m_currentTurnLabel);
        layout->addWidget(m_leaveRoomButton);

        // 시그널 연결
        connect(m_leaveRoomButton, &QPushButton::clicked, this, &GameRoomWindow::onLeaveRoomClicked);
    }

    void GameRoomWindow::setupPlayerSlotsPanel()
    {
        m_playerSlotsPanel = new QWidget();
        m_playerSlotsPanel->setFixedHeight(150);

        m_slotsLayout = new QHBoxLayout(m_playerSlotsPanel);
        m_slotsLayout->setContentsMargins(8, 8, 8, 8);
        m_slotsLayout->setSpacing(10);

        // 슬롯 수 결정 (듀오 모드면 2개, 클래식 모드면 4개)
        int slotCount = m_roomInfo.isDuoMode() ? 2 : 4;

        for (int i = 0; i < slotCount; ++i) {
            PlayerColor color = static_cast<PlayerColor>(i + 1);
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

        // 듀오 모드 표시
        if (m_roomInfo.isDuoMode()) {
            QLabel* duoLabel = new QLabel(QString::fromUtf8("🎯 듀오 모드 (2인전)"));
            duoLabel->setStyleSheet(
                "font-size: 12px; font-weight: bold; color: #e67e22; "
                "background-color: rgba(230, 126, 34, 0.1); "
                "border: 1px solid #e67e22; border-radius: 5px; "
                "padding: 5px 10px; margin: 5px;"
            );
            duoLabel->setAlignment(Qt::AlignCenter);
            m_slotsLayout->addWidget(duoLabel);
        }

        m_slotsLayout->addStretch();
    }

    void GameRoomWindow::setupGameArea()
    {
        m_gameArea = new QWidget();

        QHBoxLayout* gameLayout = new QHBoxLayout(m_gameArea);
        gameLayout->setContentsMargins(8, 8, 8, 8);
        gameLayout->setSpacing(8);

        // 게임보드 생성
        m_gameBoard = new GameBoard();
        m_gameBoard->setGameLogic(&m_gameManager->getGameLogic());
        m_gameBoard->setMinimumSize(400, 400);

        // 내 블록 팔레트 생성
        m_myBlockPalette = new MyBlockPalette();

        // 게임보드가 훨씬 큰 비중
        gameLayout->addWidget(m_gameBoard, 4);        // 게임보드 4
        gameLayout->addWidget(m_myBlockPalette, 1);   // 내 팔레트 1

        // 시그널 연결
        connect(m_gameBoard, &GameBoard::cellClicked, this, &GameRoomWindow::onCellClicked);
        connect(m_gameBoard, &GameBoard::cellHovered, this, &GameRoomWindow::onCellHovered);
        connect(m_gameBoard, &GameBoard::blockPlacedSuccessfully, this, &GameRoomWindow::onBlockPlacedSuccessfully);
        connect(m_myBlockPalette, &MyBlockPalette::blockSelected, this, &GameRoomWindow::onBlockSelected);
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
        m_controlsPanel->setFixedHeight(40);

        QHBoxLayout* layout = new QHBoxLayout(m_controlsPanel);
        layout->setContentsMargins(15, 5, 15, 5);
        layout->setSpacing(10);

        // 게임 시작 버튼만 표시 (호스트일 때만)
        m_gameStartButton = new QPushButton(QString::fromUtf8("🎮 게임 시작"));
        m_gameStartButton->setFixedHeight(30);

        // 중앙 게임 상태
        m_gameStatusLabel = new QLabel(QString::fromUtf8("게임 대기 중"));
        m_gameStatusLabel->setStyleSheet("font-size: 12px; font-weight: bold; color: #34495e;");

        // 오른쪽 좌표 표시
        m_coordinateLabel = new QLabel(QString::fromUtf8("보드 위에서 마우스를 움직이세요"));
        m_coordinateLabel->setStyleSheet("font-size: 10px; color: #7f8c8d;");

        layout->addWidget(m_gameStartButton);
        layout->addStretch();
        layout->addWidget(m_gameStatusLabel);
        layout->addStretch();
        layout->addWidget(m_coordinateLabel);

        // 시그널 연결
        connect(m_gameStartButton, &QPushButton::clicked, this, &GameRoomWindow::onGameStartClicked);
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
        qDebug() << QString::fromUtf8("🎮 게임 시작 - 초기화 중...");

        m_isGameStarted = true;
        m_gameManager->startNewGame();

        // 게임보드 설정
        if (m_gameBoard) {
            m_gameBoard->setGameLogic(&m_gameManager->getGameLogic());
            m_gameBoard->clearAllBlocks();
            m_gameBoard->setBoardReadOnly(false);  // 읽기 전용 해제
            m_gameBoard->clearSelection();  // 선택 상태 초기화

            qDebug() << QString::fromUtf8("✅ 게임보드 초기화 완료");
        }

        // 내 팔레트 설정
        PlayerColor myColor = m_roomInfo.getMyColor(m_myUsername);
        if (myColor != PlayerColor::None && m_myBlockPalette) {
            m_myBlockPalette->setPlayer(myColor);
            m_myBlockPalette->resetAllBlocks();
            m_myBlockPalette->clearSelection();

            // 첫 번째 턴인지 확인
            PlayerColor currentPlayer = m_gameManager->getGameLogic().getCurrentPlayer();
            bool isMyTurn = (currentPlayer == myColor);
            m_myBlockPalette->setEnabled(isMyTurn);

            qDebug() << QString::fromUtf8("✅ 내 팔레트 설정 완료 - 색상: %1, 현재턴: %2, 활성화: %3")
                .arg(Utils::playerColorToString(myColor))
                .arg(Utils::playerColorToString(currentPlayer))
                .arg(isMyTurn);
        }

        // 듀오 모드 처리
        if (m_roomInfo.isDuoMode()) {
            for (int i = 2; i < 4; ++i) {  // Red, Green 비활성화
                if (i < m_roomInfo.playerSlots.size()) {
                    PlayerSlot emptySlot;
                    emptySlot.color = static_cast<PlayerColor>(i + 1);
                    m_roomInfo.playerSlots[i] = emptySlot;

                    if (i < m_playerSlotWidgets.size()) {
                        m_playerSlotWidgets[i]->updatePlayerSlot(emptySlot);
                        m_playerSlotWidgets[i]->setVisible(false);
                    }
                }
            }
            qDebug() << QString::fromUtf8("✅ 듀오 모드 설정 완료");
        }

        updateGameControlsState();
        updateRoomInfoDisplay();

        addSystemMessage(QString::fromUtf8("🎮 게임이 시작되었습니다!"));

        qDebug() << QString::fromUtf8("🎉 게임 시작 완료!");
    }

    // 블록 배치 성공 시 처리
    void GameRoomWindow::onBlockPlacedSuccessfully(BlockType blockType, PlayerColor player)
    {
        qDebug() << QString::fromUtf8("블록 배치 성공: %1 플레이어의 %2 블록")
            .arg(Utils::playerColorToString(player))
            .arg(BlockFactory::getBlockName(blockType));

        // 내 블록이 배치되었으면 팔레트에서 제거
        PlayerColor myColor = m_roomInfo.getMyColor(m_myUsername);
        if (player == myColor) {
            m_myBlockPalette->removeBlock(blockType);
        }

        // 플레이어 슬롯의 점수와 남은 블록 수 업데이트
        updatePlayerScore(player, BlockFactory::getBlockScore(blockType));
        updatePlayerRemainingBlocks(player, -1);

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

        addSystemMessage(QString::fromUtf8("%1이(가) %2 블록을 배치했습니다. (+%3점)")
            .arg(playerName)
            .arg(BlockFactory::getBlockName(blockType))
            .arg(BlockFactory::getBlockScore(blockType)));

        // 게임 종료 조건 체크
        checkGameEndConditions();
    }

    // 플레이어 점수 업데이트
    void GameRoomWindow::updatePlayerScore(PlayerColor player, int scoreToAdd)
    {
        for (int i = 0; i < m_roomInfo.playerSlots.size(); ++i) {
            if (m_roomInfo.playerSlots[i].color == player) {
                m_roomInfo.playerSlots[i].score += scoreToAdd;

                // 해당 슬롯 위젯 업데이트
                if (i < m_playerSlotWidgets.size()) {
                    m_playerSlotWidgets[i]->updatePlayerSlot(m_roomInfo.playerSlots[i]);
                }
                break;
            }
        }
    }

    // 플레이어 남은 블록 수 업데이트
    void GameRoomWindow::updatePlayerRemainingBlocks(PlayerColor player, int change)
    {
        for (int i = 0; i < m_roomInfo.playerSlots.size(); ++i) {
            if (m_roomInfo.playerSlots[i].color == player) {
                m_roomInfo.playerSlots[i].remainingBlocks += change;

                // 해당 슬롯 위젯 업데이트
                if (i < m_playerSlotWidgets.size()) {
                    m_playerSlotWidgets[i]->updatePlayerSlot(m_roomInfo.playerSlots[i]);
                }
                break;
            }
        }
    }

    // 게임 종료 조건 체크
    void GameRoomWindow::checkGameEndConditions()
    {
        if (!m_gameManager || !m_isGameStarted) return;

        // 모든 플레이어가 더 이상 놓을 수 없는 블록이 있는지 확인
        bool gameEnded = false;

        // 1. 모든 플레이어의 블록이 소진되었는지 확인
        bool allBlocksUsed = true;
        for (const auto& slot : m_roomInfo.playerSlots) {
            if (!slot.isEmpty() && slot.remainingBlocks > 0) {
                allBlocksUsed = false;
                break;
            }
        }

        if (allBlocksUsed) {
            gameEnded = true;
            addSystemMessage(QString::fromUtf8("🎉 모든 블록이 소진되었습니다!"));
        }

        // 2. 게임 로직에서 종료 조건 확인
        if (!gameEnded && m_gameManager->getGameLogic().isGameFinished()) {
            gameEnded = true;
            addSystemMessage(QString::fromUtf8("🎉 더 이상 배치할 수 있는 블록이 없습니다!"));
        }

        if (gameEnded) {
            // 3초 후 게임 결과 표시 및 초기화
            QTimer::singleShot(3000, this, [this]() {
                showFinalResults();
                resetGameToWaitingState();
                });
        }
    }

    // 최종 결과 표시
    void GameRoomWindow::showFinalResults()
    {
        std::map<PlayerColor, int> finalScores;

        // 플레이어 슬롯에서 점수 수집
        for (const auto& slot : m_roomInfo.playerSlots) {
            if (!slot.isEmpty()) {
                finalScores[slot.color] = slot.score;
            }
        }

        // 결과 다이얼로그 표시
        showGameResults(finalScores);

        addSystemMessage(QString::fromUtf8("🏆 게임이 종료되었습니다!"));
    }

    // 게임을 대기 상태로 초기화
    void GameRoomWindow::resetGameToWaitingState()
    {
        m_isGameStarted = false;

        // 게임 매니저 리셋
        if (m_gameManager) {
            m_gameManager->resetGame();
        }

        // 게임보드 초기화
        if (m_gameBoard) {
            m_gameBoard->clearAllBlocks();
        }

        // 내 팔레트 초기화
        if (m_myBlockPalette) {
            m_myBlockPalette->resetAllBlocks();
        }

        // 플레이어 슬롯 점수 및 블록 수 초기화
        for (auto& slot : m_roomInfo.playerSlots) {
            if (!slot.isEmpty()) {
                slot.score = 0;
                slot.remainingBlocks = BLOCKS_PER_PLAYER;
            }
        }

        // UI 업데이트
        updatePlayerSlotsDisplay();
        updateGameControlsState();
        updateRoomInfoDisplay();

        addSystemMessage(QString::fromUtf8("🔄 게임이 초기화되었습니다. 새 게임을 시작할 수 있습니다."));

        qDebug() << QString::fromUtf8("게임 대기 상태로 초기화됨");
    }

    // 턴 스킵 처리 (블록이 없는 플레이어)
    void GameRoomWindow::checkAndSkipPlayerTurn()
    {
        if (!m_gameManager || !m_isGameStarted) return;

        PlayerColor currentPlayer = m_gameManager->getGameLogic().getCurrentPlayer();

        // 현재 플레이어가 놓을 수 있는 블록이 있는지 확인
        if (!m_gameManager->canCurrentPlayerMove()) {
            // 해당 플레이어 이름 찾기
            QString playerName = "";
            for (const auto& slot : m_roomInfo.playerSlots) {
                if (slot.color == currentPlayer) {
                    playerName = slot.getDisplayName();
                    break;
                }
            }

            addSystemMessage(QString::fromUtf8("%1 플레이어는 놓을 수 있는 블록이 없어 턴을 스킵합니다.")
                .arg(playerName));

            // 다음 턴으로 이동
            m_gameManager->nextTurn();
            updateGameState(*m_gameManager);

            // 재귀적으로 다음 플레이어도 체크
            QTimer::singleShot(1000, this, &GameRoomWindow::checkAndSkipPlayerTurn);
        }
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
        if (m_gameManager) {
            PlayerColor currentPlayer = gameManager.getGameLogic().getCurrentPlayer();

            // 🔥 듀오 모드에서는 Blue, Yellow만 순환
            if (m_roomInfo.isDuoMode()) {
                // Red나 Green 턴이 오면 다음 플레이어로 넘김
                if (currentPlayer == PlayerColor::Red || currentPlayer == PlayerColor::Green) {
                    qDebug() << QString::fromUtf8("듀오 모드에서 %1 턴 스킵")
                        .arg(Utils::playerColorToString(currentPlayer));

                    // 다음 유효한 플레이어로 이동
                    PlayerColor nextPlayer = (currentPlayer == PlayerColor::Red) ?
                        PlayerColor::Blue : PlayerColor::Yellow;

                    m_gameManager->getGameLogic().setCurrentPlayer(nextPlayer);
                    currentPlayer = nextPlayer;
                }
            }

            // 내 팔레트 활성화/비활성화
            PlayerColor myColor = m_roomInfo.getMyColor(m_myUsername);
            bool isMyTurn = (currentPlayer == myColor);
            m_myBlockPalette->setEnabled(isMyTurn);

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

    void GameRoomWindow::onBlockSelected(const Block& block)
    {
        qDebug() << QString::fromUtf8("🎯 GameRoomWindow::onBlockSelected 호출: %1")
            .arg(BlockFactory::getBlockName(block.getType()));

        // 게임이 시작되지 않았으면 선택 불가
        if (!m_isGameStarted) {
            qDebug() << QString::fromUtf8("❌ 게임이 시작되지 않아 블록 선택 불가");
            return;
        }

        // 내 턴이 아니면 선택 불가
        PlayerColor currentPlayer = m_gameManager->getGameLogic().getCurrentPlayer();
        PlayerColor myColor = m_roomInfo.getMyColor(m_myUsername);

        if (currentPlayer != myColor) {
            qDebug() << QString::fromUtf8("❌ 내 턴이 아님 - 현재: %1, 내 색상: %2")
                .arg(Utils::playerColorToString(currentPlayer))
                .arg(Utils::playerColorToString(myColor));
            return;
        }

        // 🔥 현재 플레이어 색상으로 블록 설정
        Block selectedBlock = block;
        selectedBlock.setPlayer(currentPlayer);

        qDebug() << QString::fromUtf8("🎯 게임보드에 블록 선택 전달: %1 (%2 플레이어)")
            .arg(BlockFactory::getBlockName(selectedBlock.getType()))
            .arg(Utils::playerColorToString(selectedBlock.getPlayer()));

        if (m_gameBoard) {
            m_gameBoard->setSelectedBlock(selectedBlock);
            m_gameBoard->setFocus();  // 포커스 설정으로 키보드 입력 가능하게
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
    }

    MyBlockPalette::MyBlockPalette(QWidget* parent)
        : QWidget(parent)
        , m_player(PlayerColor::Blue)
        , m_mainLayout(nullptr)
        , m_scrollArea(nullptr)
        , m_blockContainer(nullptr)
        , m_blockGrid(nullptr)
        , m_selectedBlock(BlockType::Single, PlayerColor::None)  // 🔥 None으로 초기화
        , m_hasSelection(false)  // 🔥 false로 초기화
        , m_selectedButton(nullptr)  // 🔥 nullptr로 초기화
    {
        setupUI();
    }

    void MyBlockPalette::setupUI()
    {
        setFixedWidth(250);
        setMinimumHeight(400);

        m_mainLayout = new QVBoxLayout(this);
        m_mainLayout->setContentsMargins(8, 8, 8, 8);
        m_mainLayout->setSpacing(8);

        // 제목
        QLabel* titleLabel = new QLabel(QString::fromUtf8("🎯 내 블록"));
        titleLabel->setStyleSheet(
            "font-size: 14px; font-weight: bold; color: #2c3e50; "
            "background-color: #ecf0f1; padding: 8px; border-radius: 6px;"
        );
        titleLabel->setAlignment(Qt::AlignCenter);

        // 스크롤 영역
        m_scrollArea = new QScrollArea();
        m_scrollArea->setWidgetResizable(true);
        m_scrollArea->setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
        m_scrollArea->setVerticalScrollBarPolicy(Qt::ScrollBarAsNeeded);
        m_scrollArea->setStyleSheet(
            "QScrollArea { border: 1px solid #ddd; border-radius: 6px; }"
            "QScrollBar:vertical { width: 12px; background-color: #f8f9fa; }"
            "QScrollBar::handle:vertical { background-color: #dee2e6; border-radius: 6px; }"
            "QScrollBar::handle:vertical:hover { background-color: #adb5bd; }"
        );

        // 블록 컨테이너
        m_blockContainer = new QWidget();
        m_blockGrid = new QGridLayout(m_blockContainer);
        m_blockGrid->setContentsMargins(8, 8, 8, 8);
        m_blockGrid->setSpacing(6);

        m_scrollArea->setWidget(m_blockContainer);

        // 사용법 안내
        QLabel* helpLabel = new QLabel(QString::fromUtf8("💡 블록을 클릭하여 선택\n🔄 R키: 회전, F키: 뒤집기"));
        helpLabel->setStyleSheet(
            "font-size: 10px; color: #6c757d; "
            "background-color: #f8f9fa; padding: 6px; border-radius: 4px;"
        );
        helpLabel->setAlignment(Qt::AlignCenter);
        helpLabel->setWordWrap(true);

        m_mainLayout->addWidget(titleLabel);
        m_mainLayout->addWidget(m_scrollArea, 1);
        m_mainLayout->addWidget(helpLabel);

        // 전체 스타일
        setStyleSheet(
            "MyBlockPalette { "
            "background-color: white; "
            "border: 2px solid #3498db; "
            "border-radius: 8px; "
            "}"
        );
    }

    void MyBlockPalette::setPlayer(PlayerColor player)
    {
        if (m_player != player) {
            m_player = player;

            // 해당 플레이어의 모든 블록 생성
            m_availableBlocks.clear();
            auto allTypes = BlockFactory::getAllBlockTypes();
            for (BlockType type : allTypes) {
                m_availableBlocks.emplace_back(type, player);
            }

            updateBlockButtons();

            // 제목 업데이트
            QLabel* titleLabel = qobject_cast<QLabel*>(m_mainLayout->itemAt(0)->widget());
            if (titleLabel) {
                titleLabel->setText(QString::fromUtf8("🎯 내 블록 (%1)")
                    .arg(Utils::playerColorToString(player)));

                QColor playerColor = getPlayerColor();
                titleLabel->setStyleSheet(QString(
                    "font-size: 14px; font-weight: bold; color: white; "
                    "background-color: %1; padding: 8px; border-radius: 6px;"
                ).arg(playerColor.name()));
            }

            // 팔레트 테두리 색상도 변경
            setStyleSheet(QString(
                "MyBlockPalette { "
                "background-color: white; "
                "border: 2px solid %1; "
                "border-radius: 8px; "
                "}"
            ).arg(getPlayerColor().name()));
        }
    }

    void MyBlockPalette::updateBlockButtons()
    {
        clearBlockButtons();

        QColor playerColor = getPlayerColor();
        QColor borderColor = playerColor.darker(150);

        int row = 0, col = 0;
        const int maxCols = 3; // 3열로 배치

        for (const Block& block : m_availableBlocks) {
            QPushButton* button = new QPushButton();
            button->setFixedSize(70, 60);  // 적당한 크기
            button->setProperty("blockType", static_cast<int>(block.getType()));

            // 블록 이름과 크기 표시
            QString buttonText = QString::fromUtf8("%1\n(%2칸)")
                .arg(BlockFactory::getBlockName(block.getType()))
                .arg(block.getSize());
            button->setText(buttonText);

            // 스타일 설정
            button->setStyleSheet(QString(
                "QPushButton { "
                "background-color: %1; "
                "border: 2px solid %2; "
                "border-radius: 6px; "
                "color: white; "
                "font-size: 9px; "
                "font-weight: bold; "
                "text-align: center; "
                "} "
                "QPushButton:hover { "
                "background-color: %3; "
                "border-color: %4; "
                "} "
                "QPushButton:pressed { "
                "background-color: %5; "
                "}"
            ).arg(playerColor.name())
                .arg(borderColor.name())
                .arg(playerColor.lighter(110).name())
                .arg(borderColor.lighter(110).name())
                .arg(playerColor.darker(110).name()));

            connect(button, &QPushButton::clicked, this, &MyBlockPalette::onBlockButtonClicked);

            m_blockGrid->addWidget(button, row, col);
            m_blockButtons[block.getType()] = button;

            col++;
            if (col >= maxCols) {
                col = 0;
                row++;
            }
        }
    }

    void MyBlockPalette::removeBlock(BlockType blockType)
    {
        // 버튼 찾아서 제거
        auto it = m_blockButtons.find(blockType);
        if (it != m_blockButtons.end()) {
            QPushButton* button = it->second;
            m_blockGrid->removeWidget(button);
            button->deleteLater();
            m_blockButtons.erase(it);
        }

        // 블록 리스트에서도 제거
        auto blockIt = std::find_if(m_availableBlocks.begin(), m_availableBlocks.end(),
            [blockType](const Block& block) {
                return block.getType() == blockType;
            });

        if (blockIt != m_availableBlocks.end()) {
            m_availableBlocks.erase(blockIt);
        }

        // 현재 선택된 블록이 제거된 블록이면 선택 해제
        if (m_hasSelection && m_selectedBlock.getType() == blockType) {
            m_hasSelection = false;
        }

        qDebug() << QString::fromUtf8("블록 제거됨: %1 (남은 블록: %2개)")
            .arg(BlockFactory::getBlockName(blockType))
            .arg(m_availableBlocks.size());
    }

    void MyBlockPalette::resetAllBlocks()
    {
        clearBlockButtons();

        // 모든 블록 다시 생성
        m_availableBlocks.clear();
        auto allTypes = BlockFactory::getAllBlockTypes();
        for (BlockType type : allTypes) {
            m_availableBlocks.emplace_back(type, m_player);
        }

        updateBlockButtons();
        m_hasSelection = false;

        qDebug() << QString::fromUtf8("모든 블록 리셋됨: %1개").arg(m_availableBlocks.size());
    }

    void MyBlockPalette::clearBlockButtons()
    {
        for (auto& pair : m_blockButtons) {
            if (pair.second) {
                m_blockGrid->removeWidget(pair.second);
                pair.second->deleteLater();
            }
        }
        m_blockButtons.clear();
    }

    void MyBlockPalette::onBlockButtonClicked()
    {
        QPushButton* button = qobject_cast<QPushButton*>(sender());
        if (!button) return;

        if (!isEnabled()) {
            qDebug() << QString::fromUtf8("팔레트가 비활성화 상태 - 선택 불가");
            return;
        }

        BlockType blockType = static_cast<BlockType>(button->property("blockType").toInt());

        qDebug() << QString::fromUtf8("🎯 팔레트에서 블록 클릭: %1").arg(BlockFactory::getBlockName(blockType));

        // 이전 선택 해제
        clearSelection();

        // 새 선택 설정
        QString selectedStyle = button->styleSheet();
        selectedStyle.replace("border: 2px solid", "border: 3px solid #f1c40f");
        button->setStyleSheet(selectedStyle);

        m_selectedButton = button;

        // 블록 찾아서 설정
        for (const Block& block : m_availableBlocks) {
            if (block.getType() == blockType) {
                m_selectedBlock = block;
                m_hasSelection = true;

                qDebug() << QString::fromUtf8("✅ 블록 선택됨: %1, 시그널 발생")
                    .arg(BlockFactory::getBlockName(blockType));

                // 🔥 중요: 시그널 발생
                emit blockSelected(block);
                break;
            }
        }
    }

    QColor MyBlockPalette::getPlayerColor() const
    {
        switch (m_player) {
        case PlayerColor::Blue: return QColor(52, 152, 219);
        case PlayerColor::Yellow: return QColor(241, 196, 15);
        case PlayerColor::Red: return QColor(231, 76, 60);
        case PlayerColor::Green: return QColor(46, 204, 113);
        default: return QColor(149, 165, 166);
        }
    }

    void MyBlockPalette::setEnabled(bool enabled)
    {
        // ✅ QWidget::setEnabled() 호출 (override가 아님)
        QWidget::setEnabled(enabled);

        // 모든 블록 버튼들도 함께 활성화/비활성화
        for (auto& pair : m_blockButtons) {
            if (pair.second) {
                pair.second->setEnabled(enabled);
            }
        }

        // 시각적 피드백 제공
        if (enabled) {
            // 활성화 시: 정상 색상
            setStyleSheet(QString(
                "MyBlockPalette { "
                "background-color: white; "
                "border: 2px solid %1; "
                "border-radius: 8px; "
                "}"
            ).arg(getPlayerColor().name()));

            // 제목 업데이트
            if (m_mainLayout && m_mainLayout->count() > 0) {
                QLabel* titleLabel = qobject_cast<QLabel*>(m_mainLayout->itemAt(0)->widget());
                if (titleLabel) {
                    titleLabel->setText(QString::fromUtf8("🎯 내 블록 (%1) - 내 턴!")
                        .arg(Utils::playerColorToString(m_player)));

                    QColor playerColor = getPlayerColor();
                    titleLabel->setStyleSheet(QString(
                        "font-size: 14px; font-weight: bold; color: white; "
                        "background-color: %1; padding: 8px; border-radius: 6px;"
                    ).arg(playerColor.name()));
                }
            }
        }
        else {
            // 비활성화 시: 회색조
            setStyleSheet(
                "MyBlockPalette { "
                "background-color: #f8f9fa; "
                "border: 2px solid #dee2e6; "
                "border-radius: 8px; "
                "}"
            );

            // 제목 업데이트
            if (m_mainLayout && m_mainLayout->count() > 0) {
                QLabel* titleLabel = qobject_cast<QLabel*>(m_mainLayout->itemAt(0)->widget());
                if (titleLabel) {
                    titleLabel->setText(QString::fromUtf8("🎯 내 블록 (%1) - 대기 중...")
                        .arg(Utils::playerColorToString(m_player)));
                    titleLabel->setStyleSheet(
                        "font-size: 14px; font-weight: bold; color: #6c757d; "
                        "background-color: #e9ecef; padding: 8px; border-radius: 6px;"
                    );
                }
            }
        }

        qDebug() << QString::fromUtf8("MyBlockPalette %1됨")
            .arg(enabled ? "활성화" : "비활성화");
    }

    void MyBlockPalette::clearSelection()
    {
        // 모든 버튼의 선택 스타일 제거
        for (auto& pair : m_blockButtons) {
            if (pair.second) {
                QString originalStyle = pair.second->styleSheet();
                // 선택 스타일 제거
                originalStyle.replace("border: 3px solid #f1c40f;", "border: 2px solid");
                pair.second->setStyleSheet(originalStyle);
            }
        }

        m_hasSelection = false;
        m_selectedButton = nullptr;
        m_selectedBlock = Block(BlockType::Single, PlayerColor::None);

        qDebug() << QString::fromUtf8("MyBlockPalette 선택 해제됨");
    }

} // namespace Blokus

#include "ui/GameRoomWindow.moc"