#include "GameRoomWindow.h"
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
        , m_scoreLabel(nullptr)
        , m_actionButton(nullptr)
        , m_hostIndicator(nullptr)
        , m_readyIndicator(nullptr)
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
        setFixedSize(130, 130);

        m_mainLayout = new QVBoxLayout(this);
        m_mainLayout->setContentsMargins(4, 4, 4, 4);
        m_mainLayout->setSpacing(4);

        // 색상 표시 프레임
        m_colorFrame = new QFrame();
        m_colorFrame->setFixedHeight(25);
        m_colorFrame->setFrameStyle(QFrame::Box | QFrame::Raised);
        m_colorFrame->setLineWidth(2);

        m_colorLabel = new QLabel(getColorName());
        m_colorLabel->setAlignment(Qt::AlignCenter);
        m_colorLabel->setStyleSheet("font-weight: bold; font-size: 12px; color: white;");

        QVBoxLayout* colorLayout = new QVBoxLayout(m_colorFrame);
        colorLayout->setContentsMargins(2, 2, 2, 2);
        colorLayout->addWidget(m_colorLabel);

        // 호스트 표시
        m_hostIndicator = new QWidget();
        m_hostIndicator->setFixedHeight(15);
        QLabel* hostLabel = new QLabel(QString::fromUtf8("👑"));
        hostLabel->setAlignment(Qt::AlignCenter);
        hostLabel->setStyleSheet("font-size: 12px; font-weight: bold; color: #f39c12;");
        QVBoxLayout* hostLayout = new QVBoxLayout(m_hostIndicator);
        hostLayout->setContentsMargins(0, 0, 0, 0);
        hostLayout->addWidget(hostLabel);
        m_hostIndicator->hide();

        // 플레이어 정보
        m_usernameLabel = new QLabel(QString::fromUtf8("빈 슬롯"));
        m_usernameLabel->setAlignment(Qt::AlignCenter);
        m_usernameLabel->setStyleSheet("font-size: 12px; font-weight: bold; color: #ffffff;");

        // 상태 라벨 제거 (준비 상태 인디케이터로 통합)

        m_scoreLabel = new QLabel(QString::fromUtf8("점수: 0"));
        m_scoreLabel->setAlignment(Qt::AlignCenter);
        m_scoreLabel->setStyleSheet("font-size: 12px; color: #35ff72;");

        // 남은 블록 수 표시
        m_remainingBlocksLabel = new QLabel(QString::fromUtf8("블록: 21"));
        m_remainingBlocksLabel->setAlignment(Qt::AlignCenter);
        m_remainingBlocksLabel->setStyleSheet("font-size: 12px; color: #ff4e4e;");

        // 준비 상태 인디케이터
        m_readyIndicator = new QLabel(QString::fromUtf8("대기중"));
        m_readyIndicator->setAlignment(Qt::AlignCenter);
        m_readyIndicator->setFixedHeight(20);
        m_readyIndicator->setStyleSheet("font-size: 12px; font-weight: bold; color: #f39c12;");

        // 액션 버튼 (강퇴 전용)
        m_actionButton = new QPushButton(QString::fromUtf8("강퇴"));
        m_actionButton->setFixedHeight(25);
        m_actionButton->setVisible(false); // 기본적으로 숨김

        // 레이아웃 구성
        m_mainLayout->addWidget(m_colorFrame);
        m_mainLayout->addWidget(m_hostIndicator);
        m_mainLayout->addWidget(m_usernameLabel);
        m_mainLayout->addWidget(m_readyIndicator);
        m_mainLayout->addWidget(m_scoreLabel);
        m_mainLayout->addWidget(m_remainingBlocksLabel);
        m_mainLayout->addStretch();
        m_mainLayout->addWidget(m_actionButton);

        // 시그널 연결
        connect(m_actionButton, &QPushButton::clicked, this, &PlayerSlotWidget::onKickClicked);
    }

    void PlayerSlotWidget::setupStyles()
    {
        // 전체 위젯 스타일
        setStyleSheet(
            "PlayerSlotWidget { "
            "background-color: white; "
            "border: 2px solid #ddd; "
            "border-radius: 8px; "
            "}"
        );

        // 색상 프레임 스타일
        QColor playerColor = getPlayerColor();
        m_colorFrame->setStyleSheet(QString(
            "QFrame { "
            "background-color: %1; "
            "border: 1px solid %2; "
            "border-radius: 2px; "
            "}"
        ).arg(playerColor.name(), playerColor.darker(150).name()));

        // 버튼 스타일
        m_actionButton->setStyleSheet(
            "QPushButton { "
            "background-color: #3498db; "
            "border: none; "
            "border-radius: 4px; "
            "color: white; "
            "font-weight: bold; "
            "font-size: 14px; "
            "padding: 2px; "
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
        // 호스트 권한 확인
        GameRoomWindow* gameRoom = qobject_cast<GameRoomWindow*>(parent());
        bool isHost = gameRoom ? gameRoom->isHost() : false;

        // 버튼 표시 결정 - 멀티플레이어 전용
        if (m_currentSlot.isEmpty()) {
            // 빈 슬롯 - 버튼 숨김 (플레이어가 직접 참여해야 함)
            m_actionButton->setVisible(false);
        }
        else if (m_isMySlot) {
            // 내 슬롯 - 버튼 숨김 (GameRoomWindow에 방 나가기 버튼이 별도로 있음)
            m_actionButton->setVisible(false);
        }
        else {
            // 다른 플레이어 - 호스트라면 강퇴 가능
            m_actionButton->setText(QString::fromUtf8("강퇴"));
            m_actionButton->setVisible(isHost);
            m_actionButton->setEnabled(isHost);
            m_actionButton->setStyleSheet(
                "QPushButton { background-color: #e74c3c; color: white; border: none; "
                "border-radius: 6px; font-weight: bold; font-size: 12px; padding: 4px; } "
                "QPushButton:hover { background-color: #c0392b; }"
            );
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

    void PlayerSlotWidget::updatePlayerSlot(const PlayerSlot& slot)
    {
        qDebug() << QString::fromUtf8("PlayerSlotWidget::updatePlayerSlot - 색상 %1: %2 (빈슬롯: %3)")
            .arg(static_cast<int>(m_color))
            .arg(slot.username)
            .arg(slot.isEmpty() ? "예" : "아니오");
            
        m_currentSlot = slot;

        // 사용자명 업데이트
        QString displayName = slot.getDisplayName();
        if (displayName.length() > 12) {
            displayName = displayName.left(10) + "...";
        }
        m_usernameLabel->setText(displayName);

        // 호스트 표시 업데이트
        m_hostIndicator->setVisible(slot.isHost);

        // 준비 상태 업데이트 (별도 메서드로 처리)
        updateReadyState(slot.isReady);

        // 점수 및 남은 블록 수 업데이트
        m_scoreLabel->setText(QString::fromUtf8("점수: %1").arg(slot.score));
        m_remainingBlocksLabel->setText(QString::fromUtf8("블록: %1").arg(slot.remainingBlocks));

        // 액션 버튼 업데이트
        updateActionButton();
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

    void GameRoomWindow::setMyReadyState(bool ready)
    {
        m_isReady = ready;
        updateGameControlsState();
        
        // 타임아웃 타이머 정지
        m_readyButtonTimeout->stop();
        
        // 서버 응답 후 버튼 다시 활성화
        if (!isHost()) {
            m_gameStartButton->setEnabled(true);
        }
        
        qDebug() << QString::fromUtf8("내 준비 상태 업데이트 완료: %1").arg(ready ? "준비완료" : "대기중");
    }

    void PlayerSlotWidget::updateReadyState(bool isReady)
    {
        if (m_currentSlot.isEmpty()) {
            m_readyIndicator->setVisible(false);
            return;
        }

        // 호스트의 경우 준비 상태 표시하지 않음 (왕관 표시로 충분)
        if (m_currentSlot.isHost) {
            m_readyIndicator->setVisible(false);
            return;
        }

        m_readyIndicator->setVisible(true);
        
        if (isReady) {
            m_readyIndicator->setText(QString::fromUtf8("준비완료"));
            m_readyIndicator->setStyleSheet("font-size: 10px; font-weight: bold; color: #27ae60;");
        } else {
            m_readyIndicator->setText(QString::fromUtf8("대기중"));
            m_readyIndicator->setStyleSheet("font-size: 10px; font-weight: bold; color: #f39c12;");
        }
    }

    void PlayerSlotWidget::setCurrentTurn(bool isCurrentTurn)
    {
        if (m_currentSlot.isEmpty()) {
            return;
        }

        // 현재 턴인 플레이어 슬롯에 시각적 하이라이트 추가
        if (isCurrentTurn) {
            // 턴 하이라이트 스타일 적용
            setStyleSheet("PlayerSlotWidget {"
                "border: 3px solid #f39c12;"
                "border-radius: 8px;"
                "background-color: rgba(243, 156, 18, 0.1);"
                "}");
            
            // 사용자 이름 라벨 강조
            if (m_usernameLabel) {
                m_usernameLabel->setStyleSheet("font-size: 12px; font-weight: bold; color: #f39c12;");
            }
        } else {
            // 기본 스타일로 되돌리기
            setStyleSheet("PlayerSlotWidget {"
                "border: 1px solid #bdc3c7;"
                "border-radius: 8px;"
                "background-color: #ecf0f1;"
                "}");
            
            // 사용자 이름 라벨 기본 스타일
            if (m_usernameLabel) {
                m_usernameLabel->setStyleSheet("font-size: 12px; font-weight: bold; color: #ffffff;");
            }
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
        , m_isReady(false)
        , m_previousTurn(PlayerColor::None)
        , m_turnTimer(new QTimer(this))
        , m_readyButtonTimeout(new QTimer(this))
        , m_timerPanel(nullptr)
        , m_timerLabel(nullptr)
        , m_timerProgressBar(nullptr)
        , m_turnTimeLimit(Common::DEFAULT_TURN_TIME)
        , m_remainingTime(0)
        , m_isTimerActive(false)
        , m_countdownTimer(new QTimer(this))
    {
        // 게임 매니저 생성
        m_gameManager = new GameStateManager();

        // 타이머 설정
        m_readyButtonTimeout->setSingleShot(true);
        connect(m_readyButtonTimeout, &QTimer::timeout, this, [this]() {
            // 서버 응답 타임아웃 시 버튼 다시 활성화
            if (!isHost()) {
                m_gameStartButton->setEnabled(true);
            }
            qDebug() << QString::fromUtf8("준비 상태 변경 타임아웃 - 버튼 재활성화");
        });
        
        // 턴 타이머 설정
        m_countdownTimer->setInterval(1000); // 1초마다 업데이트
        connect(m_countdownTimer, &QTimer::timeout, this, &GameRoomWindow::onCountdownTick);

        setupUI();
        // setupMenuBar();
        setupStatusBar();
        setupStyles();

        // 룸 정보 업데이트
        updateRoomInfo(roomInfo);

        // 초기 상태에서는 게임보드와 팔레트 비활성화
        if (m_gameBoard) {
            m_gameBoard->setBoardReadOnly(true);
            m_gameBoard->clearSelection();
        }

        if (m_myBlockPalette) {
            m_myBlockPalette->setEnabled(false);
            m_myBlockPalette->clearSelection();
        }

        // 창 설정
        setWindowTitle(QString::fromUtf8("블로커스 온라인 - %1 (%2님)").arg(roomInfo.roomName, myUsername));
        setMinimumSize(1280, 800);  // 더 작은 최소 크기 설정
        resize(1280, 800);         // 초기 진입 시 최소 크기로 시작

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
        m_mainLayout->setContentsMargins(5, 5, 5, 5);
        m_mainLayout->setSpacing(8);

        // 상단 룸 정보
        setupRoomInfoPanel();

        // 턴 타이머 패널
        setupTimerPanel();

        // 플레이어 슬롯들 (4개 고정)
        setupPlayerSlotsPanel();

        // 메인 게임 영역 (게임보드 + 내 팔레트 + 채팅)
        QWidget* mainGameArea = new QWidget();
        QHBoxLayout* mainGameLayout = new QHBoxLayout(mainGameArea);
        mainGameLayout->setContentsMargins(0, 0, 0, 0);
        mainGameLayout->setSpacing(6);  // 간격 줄임

        // 게임 영역 (보드 + 내 팔레트만)
        setupGameArea();

        // 채팅 패널
        setupChatPanel();

        // 비율 조정: 게임보드가 훨씬 큰 비중
        mainGameLayout->addWidget(m_gameArea, 3);
        mainGameLayout->addWidget(m_chatPanel, 1);

        // 하단 컨트롤
        setupControlsPanel();

        // 메인 레이아웃에 추가
        m_mainLayout->addWidget(m_roomInfoPanel);
        m_mainLayout->addWidget(m_timerPanel);
        m_mainLayout->addWidget(m_playerSlotsPanel);
        m_mainLayout->addWidget(mainGameArea, 1);
        m_mainLayout->addWidget(m_controlsPanel);
    }

    void GameRoomWindow::setupRoomInfoPanel()
    {
        m_roomInfoPanel = new QWidget();
        m_roomInfoPanel->setFixedHeight(35);  // 높이 줄여서 공간 절약

        QHBoxLayout* layout = new QHBoxLayout(m_roomInfoPanel);
        layout->setContentsMargins(10, 3, 10, 3);  // 여백 줄여서 공간 절약

        m_roomNameLabel = new QLabel();
        m_roomNameLabel->setStyleSheet("font-size: 16px; font-weight: bold; color: #2c3e50;");

        m_roomStatusLabel = new QLabel();
        m_roomStatusLabel->setStyleSheet("font-size: 12px; color: #7f8c8d;");

        m_currentTurnLabel = new QLabel();
        m_currentTurnLabel->setStyleSheet("font-size: 14px; font-weight: bold; color: #34495e;");

        // 방 나가기 버튼을 우측 상단에 배치
        m_leaveRoomButton = new QPushButton(QString::fromUtf8("🚪 방 나가기"));
        m_leaveRoomButton->setFixedSize(120, 25);
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

    void GameRoomWindow::setupTimerPanel()
    {
        m_timerPanel = new QWidget();
        m_timerPanel->setFixedHeight(40);
        // 패널은 항상 표시, 내부 요소들만 조건부 표시

        QHBoxLayout* layout = new QHBoxLayout(m_timerPanel);
        layout->setContentsMargins(10, 5, 10, 5);
        layout->setSpacing(10);

        // 타이머 라벨
        m_timerLabel = new QLabel("턴 시간");
        m_timerLabel->setStyleSheet("font-size: 14px; font-weight: bold; color: #2c3e50;");
        m_timerLabel->hide(); // 초기에는 숨김

        // 진행률 바
        m_timerProgressBar = new QProgressBar();
        m_timerProgressBar->setFixedHeight(20);
        m_timerProgressBar->setMinimum(0);
        m_timerProgressBar->setMaximum(100);
        m_timerProgressBar->setValue(100);
        m_timerProgressBar->setTextVisible(false);
        m_timerProgressBar->setStyleSheet(
            "QProgressBar {"
            "border: 2px solid #bdc3c7; border-radius: 10px; background-color: #ecf0f1;"
            "}"
            "QProgressBar::chunk {"
            "background-color: #27ae60; border-radius: 8px;"
            "}"
        );
        m_timerProgressBar->hide(); // 초기에는 숨김

        layout->addWidget(m_timerLabel);
        layout->addWidget(m_timerProgressBar, 1);
    }

    void GameRoomWindow::setupPlayerSlotsPanel()
    {
        m_playerSlotsPanel = new QWidget();
        m_playerSlotsPanel->setFixedHeight(115);  // 높이 줄여서 공간 절약

        m_slotsLayout = new QHBoxLayout(m_playerSlotsPanel);
        m_slotsLayout->setContentsMargins(3, 3, 3, 3);  // 여백 줄여서 공간 절약
        m_slotsLayout->setSpacing(4);  // 간격 줄임

        // 4개 슬롯 고정 생성 (클래식 모드만)
        for (int i = 0; i < 4; ++i) {
            PlayerColor color = static_cast<PlayerColor>(i + 1);
            PlayerSlotWidget* slotWidget = new PlayerSlotWidget(color, this);

            // 시그널 연결
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

        QHBoxLayout* gameLayout = new QHBoxLayout(m_gameArea);
        gameLayout->setContentsMargins(6, 3, 6, 3);  // 여백 줄여서 공간 절약
        gameLayout->setSpacing(6);  // 간격 줄임

        // 게임보드 생성
        m_gameBoard = new GameBoard();
        m_gameBoard->setGameLogic(&m_gameManager->getGameLogic());
        m_gameBoard->setMinimumSize(320, 320);  // 더 컴팩트하게 조정하여 최소 창 크기에서도 잘 맞도록

        // 내 블록 팔레트 생성
        m_myBlockPalette = new MyBlockPalette();

        // 게임보드가 훨씬 큰 비중
        gameLayout->addWidget(m_gameBoard, 3);        // 게임보드 4
        gameLayout->addWidget(m_myBlockPalette, 1);   // 내 팔레트 1

        // 시그널 연결
        connect(m_gameBoard, &GameBoard::cellClicked, this, &GameRoomWindow::onCellClicked);
        connect(m_gameBoard, &GameBoard::cellHovered, this, &GameRoomWindow::onCellHovered);
        connect(m_gameBoard, &GameBoard::blockPlacedSuccessfully, this, &GameRoomWindow::onBlockPlacedSuccessfully);
        connect(m_gameBoard, &GameBoard::afkUnblockRequested, this, &GameRoomWindow::onAfkUnblockRequested);
        connect(m_gameBoard, &GameBoard::leaveRoomRequested, this, &GameRoomWindow::onLeaveRoomClicked);
        connect(m_myBlockPalette, &MyBlockPalette::blockSelected, this, &GameRoomWindow::onBlockSelected);
    }

    void GameRoomWindow::setupChatPanel()
    {
        m_chatPanel = new QWidget();
        m_chatPanel->setMinimumWidth(180);  // 더 컴팩트하게 조정
        m_chatPanel->setMaximumWidth(300);  // 최대 너비도 줄임

        QVBoxLayout* chatLayout = new QVBoxLayout(m_chatPanel);
        chatLayout->setContentsMargins(6, 5, 6, 5);  // 여백 줄여서 공간 절약
        chatLayout->setSpacing(6);  // 간격 줄임

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
        m_chatSendButton->setFixedSize(50, 26);  // 더 컴팩트한 버튼 크기

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

        // 게임 시작/준비 버튼 (호스트 여부에 따라 다름)
        m_gameStartButton = new QPushButton(QString::fromUtf8("게임 시작"));
        m_gameStartButton->setFixedHeight(30);
        m_gameStartButton->setMinimumWidth(100); // 최소 너비 설정

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

        // 시그널 연결 - 호스트 여부에 따라 다른 슬롯 연결
        // 실제 연결은 updateGameControlsState()에서 수행
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
            "padding: 5px 8px; "
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

    void GameRoomWindow::updatePlayerReadyState(const QString& username, bool ready)
    {
        // 룸 정보에서 해당 플레이어의 준비 상태 업데이트
        for (auto& slot : m_roomInfo.playerSlots) {
            if (slot.username == username) {
                slot.isReady = ready;
                qDebug() << QString::fromUtf8("플레이어 %1의 준비 상태 업데이트: %2").arg(username).arg(ready ? "준비완료" : "대기중");
                break;
            }
        }
        
        // UI 업데이트 (준비 상태만)
        updateReadyStates();
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
        qDebug() << QString::fromUtf8("🔄 플레이어 슬롯 디스플레이 업데이트 시작");
        for (int i = 0; i < m_playerSlotWidgets.size() && i < m_roomInfo.playerSlots.size(); ++i) {
            const auto& slot = m_roomInfo.playerSlots[i];
            qDebug() << QString::fromUtf8("  슬롯 %1 업데이트: %2 (빈슬롯=%3)")
                .arg(i).arg(slot.username).arg(slot.isEmpty());
            
            m_playerSlotWidgets[i]->updatePlayerSlot(m_roomInfo.playerSlots[i]);
            m_playerSlotWidgets[i]->setMySlot(m_roomInfo.playerSlots[i].username == m_myUsername);
        }
        qDebug() << QString::fromUtf8("✅ 플레이어 슬롯 디스플레이 업데이트 완료");
    }

    void GameRoomWindow::updateGameControlsState()
    {
        bool amHost = isHost();
        bool canStart = canStartGame();

        if (!m_isGameStarted) {
            // 연결된 시그널 해제
            disconnect(m_gameStartButton, nullptr, nullptr, nullptr);
            
            if (amHost) {
                // 호스트 - 게임 시작 버튼
                m_gameStartButton->setText(QString::fromUtf8("게임 시작"));
                m_gameStartButton->setEnabled(canStart);
                m_gameStartButton->setMinimumWidth(100);
                connect(m_gameStartButton, &QPushButton::clicked, this, &GameRoomWindow::onGameStartClicked);
            } else {
                // 비호스트 - 준비/준비해제 버튼
                if (m_isReady) {
                    m_gameStartButton->setText(QString::fromUtf8("준비 해제"));
                } else {
                    m_gameStartButton->setText(QString::fromUtf8("준비 완료"));
                }
                m_gameStartButton->setEnabled(true);
                m_gameStartButton->setMinimumWidth(100);
                connect(m_gameStartButton, &QPushButton::clicked, this, &GameRoomWindow::onReadyToggleClicked);
            }
            m_gameStartButton->setVisible(true);
        } else {
            m_gameStartButton->setVisible(false);
        }

        updateReadyStates();
        
        // 게임 상태 라벨 업데이트 및 플레이어 슬롯 하이라이트
        if (m_isGameStarted) {
            PlayerColor currentTurn = m_gameManager->getGameLogic().getCurrentPlayer();
            
            // 턴 변경 감지 및 알림
            if (currentTurn != m_previousTurn && m_previousTurn != PlayerColor::None) {
                QString currentPlayerName = "";
                for (const auto& slot : m_roomInfo.playerSlots) {
                    if (slot.color == currentTurn) {
                        currentPlayerName = slot.getDisplayName();
                        break;
                    }
                }
                
                bool isMyTurn = m_roomInfo.isMyTurn(m_myUsername, currentTurn);
                showTurnChangeNotification(currentPlayerName, isMyTurn);
            }
            m_previousTurn = currentTurn;
            
            // 모든 플레이어 슬롯의 턴 하이라이트 업데이트
            for (int i = 0; i < m_playerSlotWidgets.size(); ++i) {
                PlayerSlotWidget* slotWidget = m_playerSlotWidgets[i];
                bool isCurrentTurn = (slotWidget->getColor() == currentTurn);
                slotWidget->setCurrentTurn(isCurrentTurn);
            }
            
            if (m_roomInfo.isMyTurn(m_myUsername, currentTurn)) {
                m_gameStatusLabel->setText(QString::fromUtf8("내 턴입니다!"));
                m_gameStatusLabel->setStyleSheet("font-size: 14px; font-weight: bold; color: #27ae60;");
                
                // 내 블록 팔레트 활성화
                if (m_myBlockPalette) {
                    m_myBlockPalette->setEnabled(true);
                }
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
                
                // 내 블록 팔레트 비활성화
                if (m_myBlockPalette) {
                    m_myBlockPalette->setEnabled(false);
                }
            }
        }
        else {
            // 게임이 시작되지 않은 경우 모든 슬롯 하이라이트 제거
            for (int i = 0; i < m_playerSlotWidgets.size(); ++i) {
                m_playerSlotWidgets[i]->setCurrentTurn(false);
            }
            
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
        qDebug() << QString::fromUtf8("🎮 게임 시작 - 클래식 모드 초기화 중...");
        qDebug() << QString::fromUtf8("⏰ [TIMER_DEBUG] 게임 시작: m_isGameStarted = true 설정");

        m_isGameStarted = true;
        m_gameManager->startNewGame();

        // 클래식 모드 고정 (20x20 보드)
        qDebug() << QString::fromUtf8("게임 모드: 클래식 (20x20 보드)");

        // 게임보드 설정
        if (m_gameBoard) {
            m_gameBoard->setGameLogic(&m_gameManager->getGameLogic());
            m_gameBoard->clearAllBlocks();
            m_gameBoard->setBoardReadOnly(false);
            m_gameBoard->clearSelection();

            qDebug() << QString::fromUtf8("✅ 게임보드 초기화 완료 - 20x20 크기");
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

        updateGameControlsState();
        updateRoomInfoDisplay();
        
        // 타이머 패널 내부 요소 표시 (게임 시작 시)
        if (m_timerLabel && m_timerProgressBar) {
            m_timerLabel->show();
            m_timerProgressBar->show();
        }

        // 시스템 메시지는 서버에서 오는 SYSTEM: 메시지로만 표시 (중복 방지)

        qDebug() << QString::fromUtf8("🎉 게임 시작 완료!");
    }

    // 블록 배치 성공 시 처리
    void GameRoomWindow::onBlockPlacedSuccessfully(BlockType blockType, PlayerColor player, int row, int col, int rotation, int flip)
    {
        qDebug() << QString::fromUtf8("블록 배치 성공: %1 플레이어의 %2 블록 (위치: %3,%4, 회전: %5, 뒤집기: %6)")
            .arg(Utils::playerColorToString(player))
            .arg(BlockFactory::getBlockName(blockType))
            .arg(row).arg(col).arg(rotation).arg(flip);
            
        // 블록 배치 성공 시 타이머 정지 (내 턴인 경우)
        PlayerColor myPlayerColor = PlayerColor::None;
        for (const auto& slot : m_roomInfo.playerSlots) {
            if (slot.username == m_myUsername) {
                myPlayerColor = slot.color;
                break;
            }
        }
        
        if (player == myPlayerColor) {
            stopTurnTimer();
        }

        // 내 플레이어만 서버에 블록 배치 알림 (중복 방지)
        PlayerColor myColor = m_roomInfo.getMyColor(m_myUsername);
        if (player == myColor) {
            // 서버에 블록 배치 정보 전송 (실제 위치 정보 포함)
            sendBlockPlacementToServer(blockType, player, row, col, rotation, flip);
            
            // 내 블록 팔레트에서 제거
            m_myBlockPalette->removeBlock(blockType);
        }

        // 점수와 남은 블록 수 업데이트는 서버 브로드캐스트에서 처리 (중복 방지)

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

        // 시스템 메시지는 서버에서 오는 SYSTEM: 메시지로만 표시 (중복 방지)

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

    void GameRoomWindow::setPlayerScore(PlayerColor player, int score)
    {
        for (int i = 0; i < m_roomInfo.playerSlots.size(); ++i) {
            if (m_roomInfo.playerSlots[i].color == player) {
                m_roomInfo.playerSlots[i].score = score;

                // 해당 슬롯 위젯 업데이트
                if (i < m_playerSlotWidgets.size()) {
                    m_playerSlotWidgets[i]->updatePlayerSlot(m_roomInfo.playerSlots[i]);
                }
                break;
            }
        }
    }

    void GameRoomWindow::setPlayerRemainingBlocks(PlayerColor player, int remainingBlocks)
    {
        for (int i = 0; i < static_cast<int>(m_roomInfo.playerSlots.size()); ++i) {
            if (m_roomInfo.playerSlots[i].color == player) {
                m_roomInfo.playerSlots[i].remainingBlocks = remainingBlocks;

                // 해당 슬롯 위젯 업데이트
                if (i < static_cast<int>(m_playerSlotWidgets.size())) {
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
        
        // 타이머 정지 (게임 리셋 시)
        stopTurnTimer();

        // 게임 매니저 리셋
        if (m_gameManager) {
            m_gameManager->resetGame();
        }

        // 게임보드 초기화
        if (m_gameBoard) {
            m_gameBoard->clearAllBlocks();
        }

        // 내 팔레트 초기화 (대기 상태에서는 블록 숨김)
        if (m_myBlockPalette) {
            m_myBlockPalette->clearAllBlocks();
        }

        // 플레이어 슬롯 정보는 서버에서 브로드캐스트로 업데이트됨 (로컬 초기화 제거)

        // UI 업데이트
        updatePlayerSlotsDisplay();
        updateGameControlsState();
        updateRoomInfoDisplay();

        qDebug() << QString::fromUtf8("게임 대기 상태로 초기화됨");
    }

    void GameRoomWindow::resetGameState()
    {
        qDebug() << QString::fromUtf8("🔄 게임 상태 완전 리셋 시작");
        
        // 기본 리셋 먼저 수행
        resetGameToWaitingState();
        
        // 추가 리셋 작업
        // MyBlockPalette 완전 초기화
        if (m_myBlockPalette) {
            m_myBlockPalette->resetAllBlocks();
            m_myBlockPalette->setEnabled(false); // 게임 시작 전까지 비활성화
        }
        
        // 모든 플레이어 슬롯 준비 상태 초기화
        for (auto& slot : m_roomInfo.playerSlots) {
            if (!slot.isEmpty()) {
                // 호스트는 준비 완료 상태 유지, 비호스트는 준비 해제
                slot.isReady = slot.isHost;
                slot.score = 0;
            }
        }
        
        // 내 준비 상태도 서버와 동기화 (호스트면 준비 완료, 아니면 준비 해제)
        m_isReady = isHost();
        
        // UI 강제 업데이트
        updatePlayerSlotsDisplay();
        updateGameControlsState();
        
        qDebug() << QString::fromUtf8("✅ 게임 상태 완전 리셋 완료");
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
        
        // 게임 종료 시 타이머 정지
        stopTurnTimer();

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

            // 내 팔레트 활성화/비활성화
            PlayerColor myColor = m_roomInfo.getMyColor(m_myUsername);
            bool isMyTurn = (currentPlayer == myColor);

            if (m_myBlockPalette) {
                m_myBlockPalette->setEnabled(isMyTurn);
            }

            updateGameControlsState();
            updateRoomInfoDisplay();

            qDebug() << QString::fromUtf8("게임 상태 업데이트 - 현재 턴: %1, 내 턴: %2")
                .arg(Utils::playerColorToString(currentPlayer))
                .arg(isMyTurn);
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

        // 모든 플레이어가 준비되었는지 확인
        if (!areAllPlayersReady()) {
            QMessageBox::information(this, QString::fromUtf8("게임 시작 불가"),
                QString::fromUtf8("모든 플레이어가 준비 상태가 아닙니다."));
            return;
        }

        emit gameStartRequested();
    }

    void GameRoomWindow::onReadyToggleClicked()
    {
        // 버튼 비활성화 (서버 응답 대기)
        m_gameStartButton->setEnabled(false);
        
        bool newReadyState = !m_isReady;
        emit playerReadyChanged(newReadyState);
        
        // 타임아웃 타이머 시작 (5초)
        m_readyButtonTimeout->start(5000);
        
        qDebug() << QString::fromUtf8("준비 상태 변경 요청: %1 -> %2").arg(m_isReady ? "준비완료" : "대기중").arg(newReadyState ? "준비완료" : "대기중");
        
        // 서버 응답을 기다려서 상태 업데이트하므로 여기서는 UI 변경하지 않음
        // updateGameControlsState()는 서버 응답 후에 호출됨
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
        try {
            return m_roomInfo.hostUsername == m_myUsername;
        } catch (...) {
            // 초기화 중 오류 발생 시 안전하게 false 반환
            return false;
        }
    }

    bool GameRoomWindow::canStartGame() const
    {
        try {
            int playerCount = m_roomInfo.getCurrentPlayerCount();
            return playerCount >= 2 && !m_isGameStarted;
        } catch (...) {
            // 초기화 중 오류 발생 시 안전하게 false 반환
            return false;
        }
    }

    bool GameRoomWindow::canKickPlayer(PlayerColor color)
    {
        if (!isHost()) return false;

        PlayerSlot* slot = findPlayerSlot(color);
        return slot && !slot->isEmpty() && slot->username != m_myUsername;
    }

    bool GameRoomWindow::areAllPlayersReady() const
    {
        QStringList debugInfo;
        bool allReady = true;
        
        for (const auto& slot : m_roomInfo.playerSlots) {
            if (!slot.isEmpty()) {
                bool playerReady = slot.isHost || slot.isReady;
                QString readyStatus = playerReady ? "준비됨" : "준비안됨";
                QString hostStatus = slot.isHost ? "(호스트)" : "";
                
                debugInfo << QString("%1: %2 %3").arg(slot.username).arg(readyStatus).arg(hostStatus);
                
                if (!playerReady) {
                    allReady = false;
                }
            }
        }
        
        qDebug() << QString::fromUtf8("게임 시작 조건 확인:");
        qDebug() << QString::fromUtf8("  - %1").arg(debugInfo.join(", "));
        qDebug() << QString::fromUtf8("  - 모든 플레이어 준비: %1").arg(allReady ? "예" : "아니오");
        
        return allReady;
    }

    void GameRoomWindow::updateReadyStates()
    {
        qDebug() << QString::fromUtf8("준비 상태 UI 업데이트 시작:");
        
        // 플레이어 슬롯 위젯들의 준비 상태 표시 업데이트
        for (int i = 0; i < m_playerSlotWidgets.size() && i < m_roomInfo.playerSlots.size(); ++i) {
            const auto& slot = m_roomInfo.playerSlots[i];
            qDebug() << QString::fromUtf8("  슬롯 %1: %2 (준비: %3, 호스트: %4, 빈슬롯: %5)")
                .arg(i)
                .arg(slot.username)
                .arg(slot.isReady ? "예" : "아니오")
                .arg(slot.isHost ? "예" : "아니오")
                .arg(slot.isEmpty() ? "예" : "아니오");
            
            m_playerSlotWidgets[i]->updateReadyState(slot.isReady);
        }
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

        // 게임 결과는 main.cpp에서 비모달 다이얼로그로 표시되므로 중복 제거
        // QMessageBox::information(this, QString::fromUtf8("게임 종료"), resultText);
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

    class BlockShapeButton : public QPushButton
    {
        Q_OBJECT

    public:
        BlockShapeButton(const Block& block, qreal cellSize, QWidget* parent = nullptr)
            : QPushButton(parent)
            , m_block(block)
            , m_cellSize(cellSize)
            , m_isCustomSelected(false)
        {
            setupButton();
        }

        void setCustomSelected(bool selected) {
            if (m_isCustomSelected != selected) {
                m_isCustomSelected = selected;
                update();
            }
        }

        const Block& getBlock() const { return m_block; }

    protected:
        void paintEvent(QPaintEvent* event) override {
            Q_UNUSED(event)

                QPainter painter(this);
            painter.setRenderHint(QPainter::Antialiasing);

            QColor playerColor = getPlayerColor();
            QColor borderColor = playerColor.darker(150);

            // 배경 그리기
            QColor bgColor;
            if (isDown()) {
                bgColor = playerColor.lighter(120);
            }
            else if (m_isCustomSelected) {
                bgColor = QColor(255, 215, 0, 100); // 금색 배경
            }
            else if (underMouse()) {
                bgColor = QColor(255, 255, 255, 50); // 하얀 배경
            }
            else {
                bgColor = QColor(0, 0, 0, 0); // 투명
            }

            if (bgColor.alpha() > 0) {
                painter.fillRect(rect(), bgColor);
            }

            // 블록 모양 가져오기
            PositionList shape = m_block.getCurrentShape();
            if (shape.empty()) return;

            // 블록의 바운딩 박스 계산
            int minRow = shape[0].first, maxRow = shape[0].first;
            int minCol = shape[0].second, maxCol = shape[0].second;

            for (const auto& pos : shape) {
                minRow = std::min(minRow, pos.first);
                maxRow = std::max(maxRow, pos.first);
                minCol = std::min(minCol, pos.second);
                maxCol = std::max(maxCol, pos.second);
            }

            int blockWidth = maxCol - minCol + 1;
            int blockHeight = maxRow - minRow + 1;

            // 버튼 중앙에 블록 그리기
            qreal totalBlockWidth = blockWidth * m_cellSize;
            qreal totalBlockHeight = blockHeight * m_cellSize;
            qreal startX = (width() - totalBlockWidth) / 2.0;
            qreal startY = (height() - totalBlockHeight) / 2.0;

            // 각 셀 그리기
            painter.setBrush(QBrush(playerColor));
            painter.setPen(QPen(borderColor, 1.5));

            for (const auto& pos : shape) {
                qreal x = startX + (pos.second - minCol) * m_cellSize;
                qreal y = startY + (pos.first - minRow) * m_cellSize;

                QRectF cellRect(x, y, m_cellSize, m_cellSize);
                painter.drawRect(cellRect);

                // 3D 효과 (작은 하이라이트)
                if (m_cellSize >= 8) {
                    painter.setPen(QPen(playerColor.lighter(150), 1));
                    painter.drawLine(cellRect.topLeft(), cellRect.topRight());
                    painter.drawLine(cellRect.topLeft(), cellRect.bottomLeft());
                    painter.setPen(QPen(borderColor, 1.5)); // 원래 펜으로 복원
                }
            }

            // 선택 테두리
            if (m_isCustomSelected) {
                painter.setPen(QPen(QColor(255, 215, 0), 3));
                painter.setBrush(Qt::NoBrush);
                painter.drawRect(rect().adjusted(1, 1, -1, -1));
            }

            // 블록 크기 텍스트 (우상단)
            if (width() > 40) {
                painter.setPen(QPen(QColor(60, 60, 60), 1));
                painter.setFont(QFont("Arial", 7, QFont::Bold));
                QString sizeText = QString::number(shape.size());
                painter.drawText(rect().adjusted(2, 2, -2, -2), Qt::AlignTop | Qt::AlignRight, sizeText);
            }
        }

    private:
        void setupButton() {
            // 블록 크기에 따라 버튼 크기 결정
            PositionList shape = m_block.getCurrentShape();
            if (shape.empty()) {
                setFixedSize(50, 40);
                return;
            }

            int minRow = shape[0].first, maxRow = shape[0].first;
            int minCol = shape[0].second, maxCol = shape[0].second;

            for (const auto& pos : shape) {
                minRow = std::min(minRow, pos.first);
                maxRow = std::max(maxRow, pos.first);
                minCol = std::min(minCol, pos.second);
                maxCol = std::max(maxCol, pos.second);
            }

            int blockWidth = maxCol - minCol + 1;
            int blockHeight = maxRow - minRow + 1;

            // 패딩 추가
            int padding = 8;
            int buttonWidth = blockWidth * m_cellSize + padding * 2;
            int buttonHeight = blockHeight * m_cellSize + padding * 2;

            // 최소/최대 크기 보장
            buttonWidth = std::max(buttonWidth, 45);
            buttonHeight = std::max(buttonHeight, 35);
            buttonWidth = std::min(buttonWidth, 80);
            buttonHeight = std::min(buttonHeight, 70);

            setFixedSize(buttonWidth, buttonHeight);

            // 툴팁 설정
            setToolTip(QString::fromUtf8("%1 (%2칸)")
                .arg(BlockFactory::getBlockName(m_block.getType()))
                .arg(shape.size()));

            // 기본 버튼 스타일 제거
            setStyleSheet(
                "QPushButton { "
                "border: 1px solid #ddd; "
                "border-radius: 6px; "
                "background-color: transparent; "
                "} "
                "QPushButton:hover { "
                "border-color: #aaa; "
                "} "
                "QPushButton:pressed { "
                "border-color: #888; "
                "}"
            );
        }

        QColor getPlayerColor() const {
            switch (m_block.getPlayer()) {
            case PlayerColor::Blue: return QColor(52, 152, 219);
            case PlayerColor::Yellow: return QColor(241, 196, 15);
            case PlayerColor::Red: return QColor(231, 76, 60);
            case PlayerColor::Green: return QColor(46, 204, 113);
            default: return QColor(149, 165, 166);
            }
        }

    private:
        Block m_block;
        qreal m_cellSize;
        bool m_isCustomSelected;
    };

    MyBlockPalette::MyBlockPalette(QWidget* parent)
        : QWidget(parent)
        , m_player(PlayerColor::Blue)
        , m_mainLayout(nullptr)
        , m_scrollArea(nullptr)
        , m_blockContainer(nullptr)
        , m_blockGrid(nullptr)
        , m_selectedBlock(BlockType::Single, PlayerColor::None)
        , m_hasSelection(false)
        , m_selectedButton(nullptr)
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
            "QScrollArea { border: 1px solid #ddd; border-radius: 6px; background-color: #f5f5dc; }"
            "QScrollBar:vertical { width: 12px; background-color: #f8f9fa; }"
            "QScrollBar::handle:vertical { background-color: #dee2e6; border-radius: 6px; }"
            "QScrollBar::handle:vertical:hover { background-color: #adb5bd; }"
        );

        // 블록 컨테이너 (베이지색 배경)
        m_blockContainer = new QWidget();
        m_blockContainer->setStyleSheet("background-color: #f5f5dc;");
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

        // 전체 스타일 (베이지색 배경)
        setStyleSheet(
            "MyBlockPalette { "
            "background-color: #f5f5dc; "
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
                "background-color: #f5f5dc; "
                "border: 2px solid %1; "
                "border-radius: 8px; "
                "}"
            ).arg(getPlayerColor().name()));
        }
    }

    void MyBlockPalette::updateBlockButtons()
    {
        clearBlockButtons();

        qreal cellSize = 10.0; // 작은 셀 크기

        int row = 0, col = 0;
        const int maxCols = 3; // 3열로 배치

        for (const Block& block : m_availableBlocks) {
            // 🔥 커스텀 블록 모양 버튼 사용
            BlockShapeButton* shapeButton = new BlockShapeButton(block, cellSize, m_blockContainer);
            shapeButton->setProperty("blockType", static_cast<int>(block.getType()));

            connect(shapeButton, &QPushButton::clicked, this, &MyBlockPalette::onBlockButtonClicked);

            m_blockGrid->addWidget(shapeButton, row, col);
            m_blockButtons[block.getType()] = shapeButton; // QPushButton*로 저장

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
            // 즉시 삭제하여 지연 방지
            delete button;
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
            clearSelection();
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
        clearSelection();

        qDebug() << QString::fromUtf8("모든 블록 리셋됨: %1개").arg(m_availableBlocks.size());
    }

    void MyBlockPalette::clearAllBlocks()
    {
        clearBlockButtons();
        m_availableBlocks.clear();
        clearSelection();
        
        qDebug() << QString::fromUtf8("모든 블록 클리어됨 (대기 상태)");
    }

    void MyBlockPalette::clearBlockButtons()
    {
        for (auto& pair : m_blockButtons) {
            if (pair.second) {
                pair.second->setParent(nullptr);
                pair.second->deleteLater();
            }
        }
        m_blockButtons.clear();
        m_selectedButton = nullptr;
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

        qDebug() << QString::fromUtf8("🎯 시각적 블록 클릭: %1").arg(BlockFactory::getBlockName(blockType));

        // 이전 선택 해제
        clearSelection();

        // 새 선택 설정 - BlockShapeButton으로 캐스팅해서 setCustomSelected 호출
        BlockShapeButton* shapeButton = qobject_cast<BlockShapeButton*>(button);
        if (shapeButton) {
            shapeButton->setCustomSelected(true);
            m_selectedButton = button;
        }

        // 블록 찾아서 설정
        for (const Block& block : m_availableBlocks) {
            if (block.getType() == blockType) {
                m_selectedBlock = block;
                m_hasSelection = true;

                qDebug() << QString::fromUtf8("✅ 시각적 블록 선택됨: %1, 시그널 발생")
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
        // 모든 버튼의 선택 상태 제거
        for (auto& pair : m_blockButtons) {
            BlockShapeButton* shapeButton = qobject_cast<BlockShapeButton*>(pair.second);
            if (shapeButton) {
                shapeButton->setCustomSelected(false);
            }
        }

        m_hasSelection = false;
        m_selectedButton = nullptr;
        m_selectedBlock = Block(BlockType::Single, PlayerColor::None);

        qDebug() << QString::fromUtf8("MyBlockPalette 선택 해제됨");
    }

    // ========================================
    // GameRoomWindow 턴 전환 메서드
    // ========================================

    void GameRoomWindow::showTurnChangeNotification(const QString& playerName, bool isMyTurn)
    {
        // 채팅 창에는 메시지 추가하지 않음 (서버에서 오는 SYSTEM 메시지만 사용)
        // 중복 방지를 위해 주석 처리

        // 상태 바에 일시적으로 턴 전환 메시지 표시
        if (statusBar()) {
            if (isMyTurn) {
                statusBar()->showMessage(QString::fromUtf8("🎯 내 턴 - 블록을 배치하세요!"), 3000);
            } else {
                statusBar()->showMessage(QString::fromUtf8("⏳ %1님이 블록을 배치하는 중...").arg(playerName), 3000);
            }
        }

        // 내 턴일 때 효과음 재생
        if (isMyTurn) {
            SoundManager::getInstance().playMyTurnSound();
        }

        // 로그 출력
        qDebug() << QString::fromUtf8("턴 전환: %1 (내턴=%2)")
            .arg(playerName).arg(isMyTurn ? "예" : "아니오");
    }

    // ========================================
    // 게임 상태 동기화 슬롯들
    // ========================================

    void GameRoomWindow::onGameStateUpdated(const QString& gameStateJson)
    {
        qDebug() << QString::fromUtf8("게임 상태 업데이트 수신: %1").arg(gameStateJson);
        
        // JSON 파싱 (간단한 방식)
        QRegExp currentPlayerRegex("\"currentPlayer\":(\\d+)");
        QRegExp turnNumberRegex("\"turnNumber\":(\\d+)");
        
        if (currentPlayerRegex.indexIn(gameStateJson) != -1) {
            int currentPlayerInt = currentPlayerRegex.cap(1).toInt();
            PlayerColor currentPlayer = static_cast<PlayerColor>(currentPlayerInt);
            
            // 현재 턴 플레이어 업데이트
            if (m_gameManager) {
                m_gameManager->getGameLogic().setCurrentPlayer(currentPlayer);
            }
            
            // UI 업데이트
            updateGameControlsState();
            updateRoomInfoDisplay();
        }
        
        // 플레이어 점수 파싱 및 업데이트
        QRegExp scoresRegex("\"scores\":\\{([^}]+)\\}");
        if (scoresRegex.indexIn(gameStateJson) != -1) {
            QString scoresStr = scoresRegex.cap(1);
            qDebug() << QString::fromUtf8("점수 파싱: %1").arg(scoresStr);
            QStringList scorePairs = scoresStr.split(',');
            
            for (const QString& pair : scorePairs) {
                QRegExp scoreRegex("\"(\\d+)\":(\\d+)");
                if (scoreRegex.indexIn(pair) != -1) {
                    int playerColorInt = scoreRegex.cap(1).toInt();
                    int score = scoreRegex.cap(2).toInt();
                    PlayerColor playerColor = static_cast<PlayerColor>(playerColorInt);
                    
                    qDebug() << QString::fromUtf8("플레이어 %1 점수 업데이트: %2").arg(playerColorInt).arg(score);
                    
                    // 플레이어 슬롯의 점수 설정 (절대값)
                    setPlayerScore(playerColor, score);
                }
            }
        } else {
            qDebug() << QString::fromUtf8("점수 정보를 찾을 수 없음");
        }
        
        // 남은 블록 개수 파싱 및 업데이트
        QRegExp remainingRegex("\"remainingBlocks\":\\{([^}]+)\\}");
        if (remainingRegex.indexIn(gameStateJson) != -1) {
            QString remainingStr = remainingRegex.cap(1);
            qDebug() << QString::fromUtf8("남은 블록 파싱: %1").arg(remainingStr);
            QStringList remainingPairs = remainingStr.split(',');
            
            for (const QString& pair : remainingPairs) {
                QRegExp blockRegex("\"(\\d+)\":(\\d+)");
                if (blockRegex.indexIn(pair) != -1) {
                    int playerColorInt = blockRegex.cap(1).toInt();
                    int remainingCount = blockRegex.cap(2).toInt();
                    PlayerColor playerColor = static_cast<PlayerColor>(playerColorInt);
                    
                    qDebug() << QString::fromUtf8("플레이어 %1 남은 블록 업데이트: %2").arg(playerColorInt).arg(remainingCount);
                    
                    // 플레이어 슬롯의 남은 블록 개수 설정 (절대값)
                    setPlayerRemainingBlocks(playerColor, remainingCount);
                }
            }
        } else {
            qDebug() << QString::fromUtf8("남은 블록 정보를 찾을 수 없음");
        }
    }

    void GameRoomWindow::onBlockPlaced(const QString& playerName, int blockType, int row, int col, int rotation, int flip, int playerColor, int scoreGained)
    {
        qDebug() << QString::fromUtf8("블록 배치 알림: %1이 블록 배치 (타입: %2, 위치: %3,%4, 점수: +%5)")
                    .arg(playerName).arg(blockType).arg(row).arg(col).arg(scoreGained);
        
        // 게임 보드에 블록 배치 반영
        if (m_gameBoard && m_gameManager) {
            Common::BlockPlacement placement;
            placement.type = static_cast<Common::BlockType>(blockType);
            placement.position = { row, col };
            placement.rotation = static_cast<Common::Rotation>(rotation);
            placement.flip = static_cast<Common::FlipState>(flip);
            placement.player = static_cast<PlayerColor>(playerColor);
            
            // 게임 보드에 블록 배치
            if (m_gameBoard->canPlaceBlock(placement)) {
                m_gameBoard->placeBlock(placement);
                
                // 게임 로직에도 반영
                m_gameManager->getGameLogic().placeBlock(placement);
            }
        }
        
        // 플레이어 점수와 남은 블록 수 업데이트 (서버에서 받은 정보 기반)
        PlayerColor currentPlayerColor = static_cast<PlayerColor>(playerColor);
        
        // 점수는 증분 업데이트 (서버에서 scoreGained만 보내주므로)
        updatePlayerScore(currentPlayerColor, scoreGained);
        
        // 남은 블록 수는 증분 업데이트 (블록 하나 사용)
        updatePlayerRemainingBlocks(currentPlayerColor, -1);
        
        // 시스템 메시지는 서버에서 오는 SYSTEM: 메시지로만 표시 (중복 방지)
    }

    void GameRoomWindow::onTurnChanged(const QString& newPlayerName, int playerColor, int turnNumber, int turnTimeSeconds, int remainingTimeSeconds, bool previousTurnTimedOut)
    {
        qDebug() << QString::fromUtf8("⏰ [TIMER_DEBUG] onTurnChanged 호출: 플레이어=%1, 색상=%2, 턴=%3, 턴시간=%4초, 남은시간=%5초, 게임시작=%6")
                    .arg(newPlayerName).arg(playerColor).arg(turnNumber).arg(turnTimeSeconds).arg(remainingTimeSeconds).arg(m_isGameStarted);
        
        // 게임 매니저의 현재 플레이어 업데이트
        if (m_gameManager) {
            PlayerColor newPlayer = static_cast<PlayerColor>(playerColor);
            m_gameManager->getGameLogic().setCurrentPlayer(newPlayer);
        }
        
        // 이전 턴 타임아웃 알림
        if (previousTurnTimedOut) {
            addSystemMessage(QString::fromUtf8("이전 플레이어의 시간이 초과되어 턴이 넘어왔습니다."));
            // 타임아웃 효과음 재생
            SoundManager::getInstance().playTimeOutSound();
        }
        
        // 턴 타이머 시작 (게임이 시작된 상태에서만)
        if (m_isGameStarted) {
            qDebug() << QString::fromUtf8("⏰ [TIMER_DEBUG] 타이머 시작 시도: turnTimeSeconds=%1, remainingTimeSeconds=%2")
                        .arg(turnTimeSeconds).arg(remainingTimeSeconds);
            startTurnTimer(turnTimeSeconds, remainingTimeSeconds);
        } else {
            qDebug() << QString::fromUtf8("⏰ [TIMER_DEBUG] 타이머 시작 스킵: 게임이 시작되지 않음");
        }
        
        // 턴 변경 알림 표시
        bool isMyTurn = (newPlayerName == m_myUsername);
        showTurnChangeNotification(newPlayerName, isMyTurn);
        
        // UI 업데이트
        updateGameControlsState();
        updateRoomInfoDisplay();
    }

    void GameRoomWindow::sendBlockPlacementToServer(BlockType blockType, PlayerColor playerColor, int row, int col, int rotation, int flip)
    {
        // 서버 메시지 형식: game:move:블록타입:x좌표:y좌표:회전도:뒤집기
        QString gameMessage = QString("game:move:%1:%2:%3:%4:%5")
            .arg(static_cast<int>(blockType))  // 블록 타입
            .arg(col)                          // x 좌표 (열)
            .arg(row)                          // y 좌표 (행)  
            .arg(rotation)                     // 회전도
            .arg(flip);                        // 뒤집기

        qDebug() << QString::fromUtf8("🚀 서버에 블록 배치 메시지 전송: %1 (위치: %2,%3, 회전: %4, 뒤집기: %5)")
            .arg(gameMessage).arg(row).arg(col).arg(rotation).arg(flip);
        
        // 시그널을 통해 AppController로 전달하여 NetworkClient를 통해 서버에 전송
        emit blockPlacementRequested(gameMessage);
    }

    // ========================================
    // 턴 타이머 관리 메서드
    // ========================================

    void GameRoomWindow::startTurnTimer(int timeLimit, int remainingTime)
    {
        m_turnTimeLimit = timeLimit;
        m_remainingTime = (remainingTime > 0) ? remainingTime : timeLimit;
        m_isTimerActive = true;

        // 타이머 패널 내부 요소 표시
        if (m_timerLabel && m_timerProgressBar) {
            m_timerLabel->show();
            m_timerProgressBar->show();
        }

        // 카운트다운 타이머 시작
        if (m_countdownTimer) {
            m_countdownTimer->start();
        }

        // 초기 디스플레이 업데이트
        updateTimerDisplay(m_remainingTime);

        qDebug() << QString::fromUtf8("⏰ 턴 타이머 시작: %1초").arg(m_remainingTime);
    }

    void GameRoomWindow::stopTurnTimer()
    {
        m_isTimerActive = false;
        m_countdownTimer->stop();

        // 사운드 매니저의 카운트다운도 중지
        SoundManager::getInstance().stopCountdown();

        // 타이머 패널 내부 요소 숨김
        if (m_timerLabel && m_timerProgressBar) {
            m_timerLabel->hide();
            m_timerProgressBar->hide();
        }

        qDebug() << QString::fromUtf8("⏰ 턴 타이머 정지");
    }

    void GameRoomWindow::updateTimerDisplay(int remainingTime)
    {
        if (!m_timerLabel || !m_timerProgressBar) {
            return;
        }

        // 텍스트 업데이트
        m_timerLabel->setText(QString::fromUtf8("남은 시간: %1초").arg(remainingTime));

        // 진행률 바 업데이트 (백분율)
        int percentage = (m_turnTimeLimit > 0) ? (remainingTime * 100 / m_turnTimeLimit) : 0;
        m_timerProgressBar->setValue(percentage);

        // 색상 변경 (시간에 따라)
        QString progressStyle;
        if (remainingTime <= 5) {
            // 5초 이하: 빨간색 (위험)
            progressStyle = "QProgressBar::chunk { background-color: #e74c3c; border-radius: 8px; }";
        } else if (remainingTime <= 10) {
            // 10초 이하: 주황색 (경고)
            progressStyle = "QProgressBar::chunk { background-color: #f39c12; border-radius: 8px; }";
        } else if (remainingTime <= 15) {
            // 15초 이하: 노란색 (주의)
            progressStyle = "QProgressBar::chunk { background-color: #f1c40f; border-radius: 8px; }";
        } else {
            // 그 외: 초록색 (안전)
            progressStyle = "QProgressBar::chunk { background-color: #27ae60; border-radius: 8px; }";
        }

        m_timerProgressBar->setStyleSheet(
            "QProgressBar {"
            "border: 2px solid #bdc3c7; border-radius: 10px; background-color: #ecf0f1;"
            "}" + progressStyle
        );
    }

    void GameRoomWindow::showTimeoutNotification(const QString& playerName)
    {
        addSystemMessage(QString::fromUtf8("%1님의 시간이 초과되어 턴이 넘어갑니다.").arg(playerName));
    }

    void GameRoomWindow::onCountdownTick()
    {
        if (!m_isTimerActive) {
            return;
        }

        m_remainingTime--;

        if (m_remainingTime <= 0) {
            // 시간 초과
            qDebug() << QString::fromUtf8("⏰ 시간 초과 감지, 타임아웃 처리");
            onTimerTimeout();
        } else {
            // 카운트다운 사운드 관리 (5초 이하일 때)
            if (m_remainingTime <= 5 && m_remainingTime > 0) {
                SoundManager::getInstance().startCountdown(m_remainingTime);
            }
            
            // 디스플레이 업데이트
            updateTimerDisplay(m_remainingTime);
        }
    }

    void GameRoomWindow::onTimerTimeout()
    {
        // 타이머 정지
        stopTurnTimer();

        // 타임아웃 효과음 재생
        SoundManager::getInstance().playTimeOutSound();

        qDebug() << QString::fromUtf8("⏰ 턴 타임아웃 발생");
    }

    // ========================================
    // AFK 관련 메서드
    // ========================================

    void GameRoomWindow::onAfkModeActivated(const QString& jsonData)
    {
        qDebug() << QString::fromUtf8("🚨 AFK 모드 활성화 알림 수신: %1").arg(jsonData);
        
        // GameBoard에 AFK 알림 표시 요청
        if (m_gameBoard) {
            m_gameBoard->showAfkNotification(jsonData);
        }
    }

    void GameRoomWindow::onAfkUnblockRequested()
    {
        qDebug() << QString::fromUtf8("🔓 AFK 해제 요청 신호 수신");
        
        // NetworkClient에 AFK 해제 메시지 전송 요청
        // 이는 main.cpp에서 처리될 예정 (GameBoard -> GameRoomWindow -> main -> NetworkClient)
        emit afkUnblockRequested();
    }

    void GameRoomWindow::onGameEndedForAfk()
    {
        qDebug() << QString::fromUtf8("🏁 게임 종료로 인한 AFK 모달 처리");
        
        // GameBoard에 게임 종료 알림
        if (m_gameBoard) {
            m_gameBoard->onGameEnded();
        }
    }

    void GameRoomWindow::onAfkUnblockErrorForAfk(const QString& reason, const QString& message)
    {
        qDebug() << QString::fromUtf8("❌ AFK 해제 에러로 인한 모달 처리: %1 - %2").arg(reason, message);
        
        // GameBoard에 AFK 에러 알림
        if (m_gameBoard) {
            m_gameBoard->onAfkUnblockError(reason, message);
        }
    }

} // namespace Blokus

#include "ui/GameRoomWindow.moc"