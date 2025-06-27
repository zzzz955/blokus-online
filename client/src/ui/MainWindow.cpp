#include "ui/MainWindow.h"
#include "ui/ImprovedBlockPalette.h"
#include "game/Block.h"
#include <QApplication>
#include <QGridLayout>
#include <QHBoxLayout>
#include <QVBoxLayout>

namespace Blokus {

    MainWindow::MainWindow(QWidget* parent)
        : QMainWindow(parent)
        , m_gameBoard(nullptr)
        , m_improvedPalette(nullptr)
        , m_coordinateLabel(nullptr)
        , m_gameStatusLabel(nullptr)
        , m_currentPlayerLabel(nullptr)
        , m_resetButton(nullptr)
        , m_readOnlyButton(nullptr)
        , m_newGameButton(nullptr)
        , m_nextTurnButton(nullptr)
        , m_gameManager(nullptr)
    {
        // 게임 매니저 생성
        m_gameManager = new GameStateManager();

        setupUI();
        connectSignals();
        updateGameUI();
    }

    MainWindow::~MainWindow()
    {
        delete m_gameManager;
    }

    void MainWindow::clearSelectedBlock()
    {
        qDebug() << QString::fromUtf8("선택된 블록 해제");

        // 선택된 블록 해제
        m_improvedPalette->clearSelection();
        if (m_gameBoard) {
            // 기본 블록으로 설정하되 선택 상태는 해제
            Block emptyBlock(BlockType::Single, PlayerColor::None);
            m_gameBoard->setSelectedBlock(emptyBlock);
        }
    }

    void MainWindow::onCellClicked(int row, int col)
    {
        // 이제 실제 블록 배치는 GameBoard에서 처리하고 여기서는 단순히 UI 피드백만
        QString message = QString::fromUtf8("클릭된 셀: (%1, %2)").arg(row).arg(col);
        statusBar()->showMessage(message, 2000);

        qDebug() << QString::fromUtf8("=== MainWindow::onCellClicked ===");
        qDebug() << QString::fromUtf8("클릭 위치: (%1, %2)").arg(row).arg(col);
        qDebug() << QString::fromUtf8("게임 상태: %1").arg((int)m_gameManager->getGameState());
    }

    void MainWindow::onBlockPlacedSuccessfully(BlockType blockType, PlayerColor player)
    {
        qDebug() << QString::fromUtf8("🎉 MainWindow::onBlockPlacedSuccessfully 호출됨!");
        qDebug() << QString::fromUtf8("   블록 타입: %1").arg(BlockFactory::getBlockName(blockType));
        qDebug() << QString::fromUtf8("   배치한 플레이어: %1").arg(Utils::playerColorToString(player));

        // 해당 플레이어의 팔레트에서만 블록 제거
        qDebug() << QString::fromUtf8("🗑️ %1 플레이어 팔레트에서 블록 제거")
            .arg(Utils::playerColorToString(player));
        m_improvedPalette->removeBlock(player, blockType);

        // 강제 화면 업데이트
        m_improvedPalette->update();
        QApplication::processEvents();

        qDebug() << QString::fromUtf8("✅ 팔레트 업데이트 완료");

        // 선택 해제 (내 블록이었다면)
        PlayerColor currentPlayer = m_gameManager->getGameLogic().getCurrentPlayer();
        if (player == PlayerColor::Blue) {  // 내가 배치했다면
            clearSelectedBlock();
            qDebug() << QString::fromUtf8("내 블록 배치 완료 - 선택 해제");
        }

        // 다음 턴으로 이동
        m_gameManager->nextTurn();
        updateGameUI();

        QString successMsg = QString::fromUtf8("%1 플레이어 블록 배치 완료! 다음: %2")
            .arg(Utils::playerColorToString(player))
            .arg(Utils::playerColorToString(m_gameManager->getGameLogic().getCurrentPlayer()));
        statusBar()->showMessage(successMsg, 3000);

        qDebug() << QString::fromUtf8("🎉 블록 배치 처리 완료!");
    }

    void MainWindow::onCellHovered(int row, int col)
    {
        QString message = QString::fromUtf8("마우스 위치: (%1, %2)").arg(row).arg(col);
        m_coordinateLabel->setText(message);
    }

    void MainWindow::onBlockSelected(const Block& block)
    {
        qDebug() << QString::fromUtf8("MainWindow::onBlockSelected 호출됨");
        qDebug() << QString::fromUtf8("   선택된 블록: %1").arg(BlockFactory::getBlockName(block.getType()));
        qDebug() << QString::fromUtf8("   블록 플레이어: %1").arg(Utils::playerColorToString(block.getPlayer()));

        if (m_gameBoard) {
            // 블록을 그대로 게임보드에 설정 (색깔 변경 안함)
            m_gameBoard->setSelectedBlock(block);
            m_improvedPalette->setSelectedBlock(block);

            QString message = QString::fromUtf8("선택된 블록: %1")
                .arg(BlockFactory::getBlockName(block.getType()));
            statusBar()->showMessage(message, 2000);

            // 게임보드에 포커스 설정 (키보드 입력을 위해)
            m_gameBoard->setFocus();
        }
    }

    void MainWindow::onNewGame()
    {
        qDebug() << QString::fromUtf8("=== 새 게임 시작 ===");

        m_gameManager->startNewGame();
        m_gameBoard->setGameLogic(&m_gameManager->getGameLogic());
        m_gameBoard->clearAllBlocks();

        // 모든 블록을 사용 가능 상태로 리셋
        resetAllBlockStates();

        // 선택된 블록 해제
        clearSelectedBlock();

        updateGameUI();

        statusBar()->showMessage(QString::fromUtf8("새 게임이 시작되었습니다! 파란 플레이어부터 시작합니다."), 3000);

        qDebug() << QString::fromUtf8("새 게임 초기화 완료");
    }

    void MainWindow::onResetBoard()
    {
        m_gameManager->resetGame();
        m_gameBoard->resetBoard();
        resetAllBlockStates();
        clearSelectedBlock();
        updateGameUI();
        statusBar()->showMessage(QString::fromUtf8("게임이 초기화되었습니다"), 1000);
    }

    void MainWindow::onToggleReadOnly()
    {
        bool readOnly = !m_gameBoard->property("readOnly").toBool();
        m_gameBoard->setBoardReadOnly(readOnly);
        m_gameBoard->setProperty("readOnly", readOnly);

        m_readOnlyButton->setText(readOnly ? QString::fromUtf8("상호작용 활성화") : QString::fromUtf8("상호작용 비활성화"));
        statusBar()->showMessage(readOnly ? QString::fromUtf8("보드가 읽기 전용입니다") : QString::fromUtf8("보드 상호작용이 활성화되었습니다"), 1000);
    }

    void MainWindow::onAbout()
    {
        QMessageBox::about(this, QString::fromUtf8("블로커스 온라인"),
            QString::fromUtf8("🎲 블로커스 온라인 - 고급 UI 버전 v4.0\n\n"
                "✅ 완전히 새로운 마작 스타일 레이아웃!\n\n"
                "🎮 UI 개선사항:\n"
                "• 중앙 게임보드 중심 설계\n"
                "• 4방향 팔레트가 보드에 인접 배치\n"
                "• 사용된 블록 자동 제거\n"
                "• 반응형 레이아웃\n"
                "• 자동 턴 진행\n\n"
                "🏆 게임 규칙:\n"
                "• 첫 블록은 아무 모서리에서나 시작 가능\n"
                "• 이후 블록은 같은 색과 모서리로만 접촉\n"
                "• 같은 색끼리 변 접촉 금지\n\n"
                "🎯 플레이 방법:\n"
                "1. '새 게임'으로 시작\n"
                "2. 자신의 팔레트에서 블록 선택\n"
                "3. R/F키로 회전/뒤집기 (호버 상태에서)\n"
                "4. 좌클릭으로 배치\n"
                "5. 자동으로 다음 턴 진행\n\n"
                "개발: SSAFY 포트폴리오 프로젝트"));
    }

    void MainWindow::updateGameUI()
    {
        if (!m_gameManager || !m_improvedPalette) return;

        // 현재 플레이어 정보 업데이트
        PlayerColor currentPlayer = m_gameManager->getGameLogic().getCurrentPlayer();
        m_improvedPalette->setCurrentPlayer(currentPlayer);

        QString playerText = QString::fromUtf8("현재 플레이어: %1")
            .arg(Utils::playerColorToString(currentPlayer));
        m_currentPlayerLabel->setText(playerText);

        // 내 턴인지 표시
        if (currentPlayer == PlayerColor::Blue) {
            playerText += QString::fromUtf8(" (내 턴)");
            m_currentPlayerLabel->setStyleSheet("color: #2980b9; font-weight: bold;");
        }
        else {
            playerText += QString::fromUtf8(" (상대 턴)");
            m_currentPlayerLabel->setStyleSheet("color: #7f8c8d;");
        }
        m_currentPlayerLabel->setText(playerText);

        // 게임 상태 업데이트
        GameState gameState = m_gameManager->getGameState();
        QString statusText;

        switch (gameState) {
        case GameState::Waiting:
            statusText = QString::fromUtf8("대기 중 - '새 게임' 버튼을 눌러 시작하세요");
            break;
        case GameState::Playing:
            if (currentPlayer == PlayerColor::Blue) {
                statusText = QString::fromUtf8("게임 진행 중 - 내 턴입니다! (턴 %1)")
                    .arg(m_gameManager->getTurnNumber());
            }
            else {
                statusText = QString::fromUtf8("게임 진행 중 - 상대방 턴입니다 (턴 %1)")
                    .arg(m_gameManager->getTurnNumber());
            }
            break;
        case GameState::Finished:
            statusText = QString::fromUtf8("게임 종료");
            break;
        case GameState::Paused:
            statusText = QString::fromUtf8("게임 일시정지");
            break;
        }

        m_gameStatusLabel->setText(statusText);

        // 버튼 상태 업데이트
        m_newGameButton->setEnabled(true);

        // 게임 종료 시 점수 표시
        if (gameState == GameState::Finished) {
            auto scores = m_gameManager->getFinalScores();
            QString scoreText = QString::fromUtf8("최종 점수: ");
            for (const auto& pair : scores) {
                scoreText += QString::fromUtf8("%1: %2점 ")
                    .arg(Utils::playerColorToString(pair.first))
                    .arg(pair.second);
            }
            statusBar()->showMessage(scoreText, 10000);
        }
    }

    void MainWindow::resetAllBlockStates()
    {
        qDebug() << QString::fromUtf8("=== 모든 블록 상태 리셋 ===");

        // 모든 플레이어의 모든 블록을 사용 가능 상태로 리셋
        m_improvedPalette->resetAllPlayerBlocks();

        // 강제 화면 업데이트
        m_improvedPalette->update();
        qApp->processEvents();

        qDebug() << QString::fromUtf8("블록 상태 리셋 완료");
    }

    void MainWindow::setupUI()
    {
        setWindowTitle(QString::fromUtf8("블로커스 온라인 - 반응형 베이지 테마"));
        setMinimumSize(1000, 800);   // 최소 크기
        resize(1300, 1000);          // 기본 크기

        // 전체 배경색 설정
        setStyleSheet(
            "QMainWindow { "
            "background-color: #faf0e6; "
            "}"
        );

        // 중앙 위젯 설정
        QWidget* centralWidget = new QWidget(this);
        setCentralWidget(centralWidget);

        // 팔레트 및 게임보드 생성
        m_improvedPalette = new ImprovedGamePalette(this);
        m_gameBoard = new GameBoard(this);
        m_gameBoard->setGameLogic(&m_gameManager->getGameLogic());

        // 메인 레이아웃
        QVBoxLayout* mainLayout = new QVBoxLayout(centralWidget);
        mainLayout->setContentsMargins(10, 10, 10, 10);
        mainLayout->setSpacing(8);

        // 상단 게임 정보 패널 (고정 높이)
        QWidget* gameInfoPanel = createGameInfoPanel();
        gameInfoPanel->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Fixed);
        gameInfoPanel->setFixedHeight(50);
        mainLayout->addWidget(gameInfoPanel);

        // 중앙 게임 영역 (확장)
        QWidget* gameArea = createMahjongStyleGameArea();
        gameArea->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Expanding);
        mainLayout->addWidget(gameArea, 1); // stretch factor = 1

        // 하단 컨트롤 패널 (고정 높이)
        QWidget* controlPanel = createCompactControlPanel();
        controlPanel->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Fixed);
        controlPanel->setFixedHeight(40);
        mainLayout->addWidget(controlPanel);

        // 메뉴 바 설정
        setupMenuBar();

        // 툴바 설정
        setupToolBar();

        // 상태 바 설정
        setupStatusBar();
    }

    QWidget* MainWindow::createMahjongStyleGameArea()
    {
        QWidget* gameArea = new QWidget();
        gameArea->setStyleSheet(
            "QWidget { "
            "background-color: #34495e; "
            "border-radius: 12px; "
            "padding: 10px; "
            "}"
        );

        // 3x3 그리드로 마작 스타일 배치
        QGridLayout* gridLayout = new QGridLayout(gameArea);
        gridLayout->setContentsMargins(15, 15, 15, 15);
        gridLayout->setSpacing(12);

        // 팔레트들 가져오기
        QWidget* northPalette = m_improvedPalette->getNorthPalette();
        QWidget* southPalette = m_improvedPalette->getSouthPalette();
        QWidget* eastPalette = m_improvedPalette->getEastPalette();
        QWidget* westPalette = m_improvedPalette->getWestPalette();

        // 반응형 크기 정책 설정
        setupResponsivePalettes(northPalette, southPalette, eastPalette, westPalette);

        // 게임보드 반응형 설정
        m_gameBoard->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Expanding);
        m_gameBoard->setMinimumSize(400, 400);  // 최소 크기만 설정

        // 게임보드 베이지색 스타일
        m_gameBoard->setStyleSheet(
            "QGraphicsView { "
            "background-color: #f5f5dc; "
            "border: 3px solid #8b7355; "
            "border-radius: 8px; "
            "}"
        );

        // 3x3 그리드 배치
        QWidget* corner1 = createCornerWidget();
        QWidget* corner2 = createCornerWidget();
        QWidget* corner3 = createCornerWidget();
        QWidget* corner4 = createCornerWidget();

        gridLayout->addWidget(corner1, 0, 0);
        gridLayout->addWidget(northPalette, 0, 1);
        gridLayout->addWidget(corner2, 0, 2);

        gridLayout->addWidget(westPalette, 1, 0);
        gridLayout->addWidget(m_gameBoard, 1, 1);
        gridLayout->addWidget(eastPalette, 1, 2);

        gridLayout->addWidget(corner3, 2, 0);
        gridLayout->addWidget(southPalette, 2, 1);
        gridLayout->addWidget(corner4, 2, 2);

        // 반응형 비율 설정
        gridLayout->setRowStretch(0, 0);  // 북쪽: 고정 높이
        gridLayout->setRowStretch(1, 1);  // 중앙: 확장 (핵심!)
        gridLayout->setRowStretch(2, 0);  // 남쪽: 고정 높이

        gridLayout->setColumnStretch(0, 0);  // 서쪽: 고정 너비
        gridLayout->setColumnStretch(1, 1);  // 중앙: 확장 (핵심!)
        gridLayout->setColumnStretch(2, 0);  // 동쪽: 고정 너비

        return gameArea;
    }

    void MainWindow::setupResponsivePalettes(QWidget* north, QWidget* south, QWidget* east, QWidget* west)
    {
        // 북쪽 팔레트 - 가로는 확장, 세로는 고정
        north->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Fixed);
        north->setMinimumHeight(100);
        north->setMaximumHeight(140);

        // 남쪽 팔레트 - 가로는 확장, 세로는 고정
        south->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Fixed);
        south->setMinimumHeight(150);
        south->setMaximumHeight(220);

        // 동쪽 팔레트 - 가로는 고정, 세로는 확장
        east->setSizePolicy(QSizePolicy::Fixed, QSizePolicy::Expanding);
        east->setMinimumWidth(120);
        east->setMaximumWidth(180);

        // 서쪽 팔레트 - 가로는 고정, 세로는 확장
        west->setSizePolicy(QSizePolicy::Fixed, QSizePolicy::Expanding);
        west->setMinimumWidth(120);
        west->setMaximumWidth(180);
    }

    QWidget* MainWindow::createCornerWidget()
    {
        QWidget* corner = new QWidget();
        corner->setStyleSheet("background: transparent; border: none;");
        corner->setSizePolicy(QSizePolicy::Fixed, QSizePolicy::Fixed);
        corner->setFixedSize(20, 20); // 작은 고정 크기
        return corner;
    }

    void MainWindow::resizeEvent(QResizeEvent* event)
    {
        QMainWindow::resizeEvent(event);

        // 창 크기 변경 시 팔레트 재조정
        if (m_improvedPalette) {
            adjustPalettesToWindowSize();
        }
    }

    void MainWindow::adjustPalettesToWindowSize()
    {
        QSize windowSize = size();
        qDebug() << QString::fromUtf8("창 크기 변경: %1 x %2").arg(windowSize.width()).arg(windowSize.height());

        // 창 크기에 따른 팔레트 조정
        QWidget* north = m_improvedPalette->getNorthPalette();
        QWidget* south = m_improvedPalette->getSouthPalette();
        QWidget* east = m_improvedPalette->getEastPalette();
        QWidget* west = m_improvedPalette->getWestPalette();

        if (windowSize.width() < 1200) {
            // 작은 화면: 팔레트 축소
            north->setMaximumHeight(120);
            south->setMaximumHeight(180);
            east->setMaximumWidth(140);
            west->setMaximumWidth(140);
        }
        else if (windowSize.width() > 1500) {
            // 큰 화면: 팔레트 확대
            north->setMaximumHeight(160);
            south->setMaximumHeight(250);
            east->setMaximumWidth(200);
            west->setMaximumWidth(200);
        }
        else {
            // 기본 크기
            north->setMaximumHeight(140);
            south->setMaximumHeight(220);
            east->setMaximumWidth(180);
            west->setMaximumWidth(180);
        }
    }

    QWidget* MainWindow::createGameInfoPanel()
    {
        QWidget* panel = new QWidget();
        panel->setFixedHeight(50);
        panel->setStyleSheet("QWidget { background-color: #34495e; color: white; border-radius: 5px; }");

        QHBoxLayout* layout = new QHBoxLayout(panel);
        layout->setContentsMargins(15, 8, 15, 8);

        // 게임 상태 라벨
        m_gameStatusLabel = new QLabel(QString::fromUtf8("대기 중"));
        m_gameStatusLabel->setStyleSheet("font-size: 16px; font-weight: bold;");
        layout->addWidget(m_gameStatusLabel);

        layout->addStretch();

        // 현재 플레이어 라벨
        m_currentPlayerLabel = new QLabel(QString::fromUtf8("현재 플레이어: 없음"));
        m_currentPlayerLabel->setStyleSheet("font-size: 14px;");
        layout->addWidget(m_currentPlayerLabel);

        layout->addStretch();

        // 새 게임 버튼만 유지 (다음턴 버튼 제거)
        m_newGameButton = new QPushButton(QString::fromUtf8("🎮 새 게임"));
        m_newGameButton->setStyleSheet("QPushButton { font-size: 12px; padding: 8px 15px; background-color: #27ae60; border: none; border-radius: 4px; } QPushButton:hover { background-color: #2ecc71; }");

        layout->addWidget(m_newGameButton);

        return panel;
    }

    QWidget* MainWindow::createCompactControlPanel()
    {
        QWidget* panel = new QWidget();
        panel->setFixedHeight(50);
        panel->setStyleSheet("QWidget { background-color: #ecf0f1; border-radius: 5px; }");

        QHBoxLayout* layout = new QHBoxLayout(panel);
        layout->setContentsMargins(15, 8, 15, 8);

        // 좌표 표시
        m_coordinateLabel = new QLabel(QString::fromUtf8("보드 위에서 마우스를 움직이세요"));
        m_coordinateLabel->setStyleSheet("font-size: 12px; color: #666;");
        layout->addWidget(m_coordinateLabel);

        layout->addStretch();

        // 조작법 안내
        QLabel* helpLabel = new QLabel(QString::fromUtf8("R키: 회전 | F키: 뒤집기 | 좌클릭: 배치 | 자동 턴 진행"));
        helpLabel->setStyleSheet("font-size: 11px; color: #888;");
        layout->addWidget(helpLabel);

        layout->addStretch();

        // 컨트롤 버튼들
        m_resetButton = new QPushButton(QString::fromUtf8("🔄"));
        m_readOnlyButton = new QPushButton(QString::fromUtf8("🔒"));

        m_resetButton->setFixedSize(40, 30);
        m_readOnlyButton->setFixedSize(40, 30);
        m_resetButton->setToolTip(QString::fromUtf8("게임 리셋"));
        m_readOnlyButton->setToolTip(QString::fromUtf8("잠금 모드"));

        layout->addWidget(m_resetButton);
        layout->addWidget(m_readOnlyButton);

        return panel;
    }

    void MainWindow::setupMenuBar()
    {
        QMenu* gameMenu = menuBar()->addMenu(QString::fromUtf8("게임(&G)"));
        gameMenu->addAction(QString::fromUtf8("새 게임(&N)"), this, &MainWindow::onNewGame, QKeySequence("Ctrl+N"));
        gameMenu->addAction(QString::fromUtf8("게임 리셋(&R)"), this, &MainWindow::onResetBoard, QKeySequence("Ctrl+R"));
        gameMenu->addSeparator();
        gameMenu->addAction(QString::fromUtf8("종료(&X)"), this, &QWidget::close, QKeySequence("Ctrl+Q"));

        QMenu* helpMenu = menuBar()->addMenu(QString::fromUtf8("도움말(&H)"));
        helpMenu->addAction(QString::fromUtf8("게임 규칙(&R)"), this, &MainWindow::onAbout, QKeySequence("F1"));
    }

    void MainWindow::setupToolBar()
    {
        QToolBar* toolBar = addToolBar(QString::fromUtf8("메인"));

        toolBar->addAction(QString::fromUtf8("🎮"), this, &MainWindow::onNewGame)->setToolTip(QString::fromUtf8("새 게임 (Ctrl+N)"));
        toolBar->addAction(QString::fromUtf8("🔄"), this, &MainWindow::onResetBoard)->setToolTip(QString::fromUtf8("게임 리셋 (Ctrl+R)"));
        toolBar->addSeparator();
        toolBar->addAction(QString::fromUtf8("ℹ️"), this, &MainWindow::onAbout)->setToolTip(QString::fromUtf8("게임 규칙 (F1)"));
    }

    void MainWindow::setupStatusBar()
    {
        statusBar()->showMessage(QString::fromUtf8("블로커스 온라인에 오신 것을 환영합니다! 새 게임 버튼을 눌러 시작하세요."), 5000);
    }

    void MainWindow::connectSignals()
    {
        // 게임보드 시그널
        connect(m_gameBoard, &GameBoard::cellClicked, this, &MainWindow::onCellClicked);
        connect(m_gameBoard, &GameBoard::cellHovered, this, &MainWindow::onCellHovered);
        connect(m_gameBoard, &GameBoard::blockPlacedSuccessfully, this, &MainWindow::onBlockPlacedSuccessfully); // 새로 추가

        // 블록 팔레트 시그널
        connect(m_improvedPalette, &ImprovedGamePalette::blockSelected, this, &MainWindow::onBlockSelected);

        // 버튼 시그널
        connect(m_resetButton, &QPushButton::clicked, this, &MainWindow::onResetBoard);
        connect(m_readOnlyButton, &QPushButton::clicked, this, &MainWindow::onToggleReadOnly);
        connect(m_newGameButton, &QPushButton::clicked, this, &MainWindow::onNewGame);
    }

} // namespace Blokus