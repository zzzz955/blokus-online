#include "ui/MainWindow.h"
#include "ui/ImprovedBlockPalette.h"
#include "game/Block.h"
#include <QApplication>
#include <QGridLayout>

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

    void MainWindow::onCellClicked(int row, int col)
    {
        QString message = QString::fromUtf8("클릭된 셀: (%1, %2)").arg(row).arg(col);
        statusBar()->showMessage(message, 2000);

        // 게임이 진행 중이면 블록 배치 시도
        if (m_gameManager->getGameState() == GameState::Playing) {
            Position clickedPos = { row, col };
            if (m_gameBoard->tryPlaceCurrentBlock(clickedPos)) {
                // 블록 배치 성공 시 팔레트에서 사용됨 표시
                PlayerColor currentPlayer = m_gameManager->getGameLogic().getCurrentPlayer();
                Block selectedBlock = m_improvedPalette->getSelectedBlock();
                m_improvedPalette->setBlockUsed(currentPlayer, selectedBlock.getType());

                // 다음 턴으로 이동
                m_gameManager->nextTurn();
                updateGameUI();

                // 다음 플레이어에게 기본 블록 선택
                PlayerColor nextPlayer = m_gameManager->getGameLogic().getCurrentPlayer();
                Block defaultBlock(BlockType::Single, nextPlayer);
                m_improvedPalette->setSelectedBlock(defaultBlock);
                m_gameBoard->setSelectedBlock(defaultBlock);
            }
        }
    }

    void MainWindow::onCellHovered(int row, int col)
    {
        QString message = QString::fromUtf8("마우스 위치: (%1, %2)").arg(row).arg(col);
        m_coordinateLabel->setText(message);
    }

    void MainWindow::onBlockSelected(const Block& block)
    {
        if (m_gameBoard) {
            // 현재 플레이어 색상으로 블록 설정
            Block playerBlock = block;
            if (m_gameManager) {
                playerBlock.setPlayer(m_gameManager->getGameLogic().getCurrentPlayer());
            }

            m_gameBoard->setSelectedBlock(playerBlock);
            m_improvedPalette->setSelectedBlock(playerBlock);

            QString message = QString::fromUtf8("선택된 블록: %1")
                .arg(BlockFactory::getBlockName(block.getType()));
            statusBar()->showMessage(message, 2000);
        }
    }

    void MainWindow::onNewGame()
    {
        m_gameManager->startNewGame();
        m_gameBoard->setGameLogic(&m_gameManager->getGameLogic());
        m_gameBoard->clearAllBlocks();

        // 모든 블록을 사용 가능 상태로 리셋
        resetAllBlockStates();

        // 기본 블록 선택 (파란 플레이어의 첫 블록)
        Block defaultBlock(BlockType::Single, PlayerColor::Blue);
        m_improvedPalette->setSelectedBlock(defaultBlock);
        m_gameBoard->setSelectedBlock(defaultBlock);

        updateGameUI();

        statusBar()->showMessage(QString::fromUtf8("새 게임이 시작되었습니다! 파란 플레이어부터 시작합니다."), 3000);
    }

    void MainWindow::onNextTurn()
    {
        if (m_gameManager->getGameState() == GameState::Playing) {
            m_gameManager->nextTurn();
            updateGameUI();

            // 다음 플레이어에게 기본 블록 선택
            PlayerColor currentPlayer = m_gameManager->getGameLogic().getCurrentPlayer();
            Block defaultBlock(BlockType::Single, currentPlayer);
            m_improvedPalette->setSelectedBlock(defaultBlock);
            m_gameBoard->setSelectedBlock(defaultBlock);
        }
    }

    void MainWindow::onResetBoard()
    {
        m_gameManager->resetGame();
        m_gameBoard->resetBoard();
        resetAllBlockStates();
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
            QString::fromUtf8("🎲 블로커스 온라인 - 개선된 UI 버전 v3.0\n\n"
                "✅ 완전히 새로운 4방향 블록 팔레트!\n\n"
                "🎮 UI 개선사항:\n"
                "• 자신의 블록: 하단에 크게 표시\n"
                "• 상대방 블록: 동/서/북쪽에 작게 표시\n"
                "• 폴리오미노 모양 그대로 표시\n"
                "• 깔끔한 그리드 레이아웃\n"
                "• 직관적인 블록 선택\n\n"
                "🏆 게임 규칙:\n"
                "• 첫 블록은 아무 모서리에서나 시작 가능\n"
                "• 이후 블록은 같은 색과 모서리로만 접촉\n"
                "• 같은 색끼리 변 접촉 금지\n\n"
                "🎯 플레이 방법:\n"
                "1. '새 게임'으로 시작\n"
                "2. 하단에서 자신의 블록 선택\n"
                "3. R/F키로 회전/뒤집기\n"
                "4. 좌클릭으로 배치\n\n"
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

        // 게임 상태 업데이트
        GameState gameState = m_gameManager->getGameState();
        QString statusText;

        switch (gameState) {
        case GameState::Waiting:
            statusText = QString::fromUtf8("대기 중 - '새 게임' 버튼을 눌러 시작하세요");
            break;
        case GameState::Playing:
            statusText = QString::fromUtf8("게임 진행 중 - 턴 %1")
                .arg(m_gameManager->getTurnNumber());
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
        m_nextTurnButton->setEnabled(gameState == GameState::Playing);

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
        // 모든 플레이어의 모든 블록을 사용 가능 상태로 리셋
        m_improvedPalette->resetAllPlayerBlocks();
    }

    void MainWindow::setupUI()
    {
        setWindowTitle(QString::fromUtf8("블로커스 온라인 - 개선된 4방향 UI"));
        setMinimumSize(1600, 1000); // 더 큰 창 크기로 조정

        // 중앙 위젯 설정
        QWidget* centralWidget = new QWidget(this);
        setCentralWidget(centralWidget);

        // 메인 그리드 레이아웃 (3x3)
        QGridLayout* mainLayout = new QGridLayout(centralWidget);
        mainLayout->setContentsMargins(5, 5, 5, 5);
        mainLayout->setSpacing(3);

        // 상단 게임 정보 패널 (0,0 - 0,2)
        QWidget* gameInfoPanel = createGameInfoPanel();
        mainLayout->addWidget(gameInfoPanel, 0, 0, 1, 3);

        // 팔레트 및 게임보드 생성
        m_improvedPalette = new ImprovedGamePalette(this);
        m_gameBoard = new GameBoard(this);
        m_gameBoard->setGameLogic(&m_gameManager->getGameLogic());

        // 서쪽 팔레트 (1,0) - 크기 조정
        QWidget* westWidget = m_improvedPalette->getWestPalette();
        westWidget->setMaximumWidth(100);
        mainLayout->addWidget(westWidget, 1, 0);

        // 중앙 영역 (1,1)
        QWidget* centerWidget = new QWidget();
        QVBoxLayout* centerLayout = new QVBoxLayout(centerWidget);
        centerLayout->setContentsMargins(0, 0, 0, 0);
        centerLayout->setSpacing(3);

        // 북쪽 팔레트 - 높이 제한
        QWidget* northWidget = m_improvedPalette->getNorthPalette();
        northWidget->setMaximumHeight(100);
        centerLayout->addWidget(northWidget);

        // 게임보드 - 정사각형 유지
        m_gameBoard->setMinimumSize(600, 600);
        centerLayout->addWidget(m_gameBoard, 1);

        // 남쪽 팔레트 (자신의 블록) - 높이 적절히 조정
        QWidget* southWidget = m_improvedPalette->getSouthPalette();
        southWidget->setMaximumHeight(150);
        southWidget->setMinimumHeight(120);
        centerLayout->addWidget(southWidget);

        mainLayout->addWidget(centerWidget, 1, 1);

        // 동쪽 팔레트 (1,2) - 크기 조정
        QWidget* eastWidget = m_improvedPalette->getEastPalette();
        eastWidget->setMaximumWidth(100);
        mainLayout->addWidget(eastWidget, 1, 2);

        // 하단 컨트롤 패널 (2,0 - 2,2)
        QWidget* controlPanel = createCompactControlPanel();
        mainLayout->addWidget(controlPanel, 2, 0, 1, 3);

        // 그리드 비율 설정
        mainLayout->setRowStretch(0, 0);  // 게임 정보 패널 (고정)
        mainLayout->setRowStretch(1, 1);  // 메인 게임 영역 (확장)
        mainLayout->setRowStretch(2, 0);  // 컨트롤 패널 (고정)

        mainLayout->setColumnStretch(0, 0); // 서쪽 팔레트 (고정 100px)
        mainLayout->setColumnStretch(1, 1); // 중앙 게임보드 (확장)
        mainLayout->setColumnStretch(2, 0); // 동쪽 팔레트 (고정 100px)

        // 메뉴 바 설정
        setupMenuBar();

        // 툴바 설정
        setupToolBar();

        // 상태 바 설정
        setupStatusBar();
    }

    QWidget* MainWindow::createGameInfoPanel()
    {
        QWidget* panel = new QWidget();
        panel->setFixedHeight(50);
        panel->setStyleSheet("QWidget { background-color: #2c3e50; color: white; border-radius: 5px; }");

        QHBoxLayout* layout = new QHBoxLayout(panel);
        layout->setContentsMargins(10, 5, 10, 5);

        // 게임 상태 라벨
        m_gameStatusLabel = new QLabel(QString::fromUtf8("대기 중"));
        m_gameStatusLabel->setStyleSheet("font-size: 14px; font-weight: bold;");
        layout->addWidget(m_gameStatusLabel);

        layout->addStretch();

        // 현재 플레이어 라벨
        m_currentPlayerLabel = new QLabel(QString::fromUtf8("현재 플레이어: 없음"));
        m_currentPlayerLabel->setStyleSheet("font-size: 12px;");
        layout->addWidget(m_currentPlayerLabel);

        layout->addStretch();

        // 게임 조작 버튼들
        m_newGameButton = new QPushButton(QString::fromUtf8("🎮 새 게임"));
        m_nextTurnButton = new QPushButton(QString::fromUtf8("⏭️ 다음 턴"));

        m_newGameButton->setStyleSheet("QPushButton { font-size: 11px; padding: 5px 12px; background-color: #27ae60; border: none; border-radius: 3px; } QPushButton:hover { background-color: #2ecc71; }");
        m_nextTurnButton->setStyleSheet("QPushButton { font-size: 11px; padding: 5px 12px; background-color: #3498db; border: none; border-radius: 3px; } QPushButton:hover { background-color: #5dade2; }");

        layout->addWidget(m_newGameButton);
        layout->addWidget(m_nextTurnButton);

        return panel;
    }

    QWidget* MainWindow::createCompactControlPanel()
    {
        QWidget* panel = new QWidget();
        panel->setFixedHeight(60);
        panel->setStyleSheet("QWidget { background-color: #ecf0f1; border-radius: 5px; }");

        QHBoxLayout* layout = new QHBoxLayout(panel);
        layout->setContentsMargins(10, 5, 10, 5);

        // 좌표 표시
        m_coordinateLabel = new QLabel(QString::fromUtf8("보드 위에서 마우스를 움직이세요"));
        m_coordinateLabel->setStyleSheet("font-size: 11px; color: #666;");
        layout->addWidget(m_coordinateLabel);

        layout->addStretch();

        // 조작법 안내
        QLabel* helpLabel = new QLabel(QString::fromUtf8("R키: 회전 | F키: 뒤집기 | 좌클릭: 배치"));
        helpLabel->setStyleSheet("font-size: 10px; color: #888;");
        layout->addWidget(helpLabel);

        layout->addStretch();

        // 컨트롤 버튼들
        m_resetButton = new QPushButton(QString::fromUtf8("🔄 리셋"));
        m_readOnlyButton = new QPushButton(QString::fromUtf8("🔒 잠금"));

        m_resetButton->setFixedSize(60, 30);
        m_readOnlyButton->setFixedSize(60, 30);

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
        toolBar->addAction(QString::fromUtf8("⏭️"), this, &MainWindow::onNextTurn)->setToolTip(QString::fromUtf8("다음 턴"));
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

        // 블록 팔레트 시그널
        connect(m_improvedPalette, &ImprovedGamePalette::blockSelected, this, &MainWindow::onBlockSelected);

        // 버튼 시그널
        connect(m_resetButton, &QPushButton::clicked, this, &MainWindow::onResetBoard);
        connect(m_readOnlyButton, &QPushButton::clicked, this, &MainWindow::onToggleReadOnly);
        connect(m_newGameButton, &QPushButton::clicked, this, &MainWindow::onNewGame);
        connect(m_nextTurnButton, &QPushButton::clicked, this, &MainWindow::onNextTurn);
    }

} // namespace Blokus