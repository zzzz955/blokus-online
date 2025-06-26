#include "ui/MainWindow.h"
#include "game/Block.h"
#include <QApplication>
#include <QSplitter>

namespace Blokus {

    MainWindow::MainWindow(QWidget* parent)
        : QMainWindow(parent)
        , m_gameBoard(nullptr)
        , m_blockPalette(nullptr)
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
                // 블록 배치 성공 시 다음 턴으로 이동
                m_gameManager->nextTurn();
                updateGameUI();
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
            m_gameBoard->setSelectedBlock(block);
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
        updateGameUI();

        statusBar()->showMessage(QString::fromUtf8("새 게임이 시작되었습니다!"), 3000);
    }

    void MainWindow::onNextTurn()
    {
        if (m_gameManager->getGameState() == GameState::Playing) {
            m_gameManager->nextTurn();
            updateGameUI();
        }
    }

    void MainWindow::onResetBoard()
    {
        m_gameManager->resetGame();
        m_gameBoard->resetBoard();
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
            QString::fromUtf8("🎲 블로커스 온라인 - 완성 버전 v2.0\n\n"
                "✅ 완전한 블로커스 게임 구현!\n\n"
                "🎮 게임 특징:\n"
                "• 정식 블로커스 규칙 완벽 구현\n"
                "• 직관적인 블록 팔레트 UI\n"
                "• 실시간 배치 가능성 미리보기\n"
                "• 자동 턴 관리 시스템\n"
                "• 게임 종료 및 점수 계산\n\n"
                "🏆 블로커스 규칙:\n"
                "• 각 플레이어는 21개의 폴리오미노 블록 보유\n"
                "• 첫 블록은 자신의 모서리에서 시작\n"
                "• 이후 블록은 같은 색과 모서리로만 접촉\n"
                "• 같은 색끼리 변 접촉 금지\n"
                "• 모든 플레이어가 블록을 놓을 수 없으면 게임 종료\n"
                "• 사용하지 못한 블록 점수만큼 감점\n\n"
                "🎯 플레이 방법:\n"
                "1. '새 게임'으로 시작\n"
                "2. 하단에서 블록 선택\n"
                "3. R/F키로 회전/뒤집기\n"
                "4. 좌클릭으로 배치\n\n"
                "개발: SSAFY 포트폴리오 프로젝트\n"
                "목표: 게임 서버 프로그래머 취업"));
    }

    void MainWindow::updateGameUI()
    {
        if (!m_gameManager || !m_blockPalette) return;

        // 현재 플레이어 정보 업데이트
        PlayerColor currentPlayer = m_gameManager->getGameLogic().getCurrentPlayer();
        m_blockPalette->setCurrentPlayer(currentPlayer);

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

    void MainWindow::setupUI()
    {
        setWindowTitle(QString::fromUtf8("블로커스 온라인 - 완전한 게임 시스템"));
        setMinimumSize(1200, 800); // 더 큰 창 크기

        // 중앙 위젯 설정
        QWidget* centralWidget = new QWidget(this);
        setCentralWidget(centralWidget);

        // 메인 레이아웃 (수직)
        QVBoxLayout* mainLayout = new QVBoxLayout(centralWidget);
        mainLayout->setContentsMargins(5, 5, 5, 5);
        mainLayout->setSpacing(5);

        // 상단 게임 정보 패널
        QWidget* gameInfoPanel = createGameInfoPanel();
        mainLayout->addWidget(gameInfoPanel);

        // 중앙 영역 (게임보드 + 사이드 패널)
        QSplitter* centerSplitter = new QSplitter(Qt::Horizontal);

        // 게임 보드
        m_gameBoard = new GameBoard(this);
        m_gameBoard->setGameLogic(&m_gameManager->getGameLogic());
        centerSplitter->addWidget(m_gameBoard);

        // 우측 컨트롤 패널
        QWidget* controlPanel = createControlPanel();
        centerSplitter->addWidget(controlPanel);

        // 스플리터 비율 설정 (게임보드 4 : 컨트롤 패널 1)
        centerSplitter->setStretchFactor(0, 4);
        centerSplitter->setStretchFactor(1, 1);

        mainLayout->addWidget(centerSplitter, 1);

        // 하단 블록 팔레트
        m_blockPalette = new GameBlockPalette(this);
        mainLayout->addWidget(m_blockPalette);

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
        panel->setFixedHeight(60);
        panel->setStyleSheet("QWidget { background-color: #34495e; color: white; border-radius: 5px; }");

        QHBoxLayout* layout = new QHBoxLayout(panel);
        layout->setContentsMargins(10, 5, 10, 5);

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

        // 게임 조작 버튼들
        m_newGameButton = new QPushButton(QString::fromUtf8("🎮 새 게임"));
        m_nextTurnButton = new QPushButton(QString::fromUtf8("⏭️ 다음 턴"));

        m_newGameButton->setStyleSheet("QPushButton { font-size: 12px; padding: 8px 15px; background-color: #27ae60; border: none; border-radius: 3px; } QPushButton:hover { background-color: #2ecc71; }");
        m_nextTurnButton->setStyleSheet("QPushButton { font-size: 12px; padding: 8px 15px; background-color: #3498db; border: none; border-radius: 3px; } QPushButton:hover { background-color: #5dade2; }");

        layout->addWidget(m_newGameButton);
        layout->addWidget(m_nextTurnButton);

        return panel;
    }

    QWidget* MainWindow::createControlPanel()
    {
        QWidget* panel = new QWidget();
        panel->setFixedWidth(280);
        panel->setStyleSheet("QWidget { background-color: #f8f9fa; padding: 10px; }");

        QVBoxLayout* layout = new QVBoxLayout(panel);

        // 제목
        QLabel* titleLabel = new QLabel(QString::fromUtf8("🎯 게임 컨트롤"));
        titleLabel->setStyleSheet("font-size: 16px; font-weight: bold; margin-bottom: 10px;");
        layout->addWidget(titleLabel);

        // 좌표 표시
        m_coordinateLabel = new QLabel(QString::fromUtf8("보드 위에서 마우스를 움직이세요"));
        m_coordinateLabel->setStyleSheet("font-size: 12px; color: #666; margin-bottom: 15px;");
        m_coordinateLabel->setWordWrap(true);
        layout->addWidget(m_coordinateLabel);

        // 보드 제어 그룹
        QGroupBox* boardGroup = new QGroupBox(QString::fromUtf8("보드 제어"));
        QVBoxLayout* boardLayout = new QVBoxLayout(boardGroup);

        m_resetButton = new QPushButton(QString::fromUtf8("🔄 게임 리셋"));
        m_readOnlyButton = new QPushButton(QString::fromUtf8("🔒 상호작용 비활성화"));

        boardLayout->addWidget(m_resetButton);
        boardLayout->addWidget(m_readOnlyButton);
        layout->addWidget(boardGroup);

        // 스페이서
        layout->addStretch();

        // 게임 가이드 라벨
        QLabel* gameGuideLabel = new QLabel(
            QString::fromUtf8("🎯 게임 방법:\n"
                "1. 하단 팔레트에서 블록 선택\n"
                "2. 마우스 호버로 배치 위치 확인\n"
                "3. R키: 회전 / F키: 뒤집기\n"
                "4. 좌클릭으로 블록 배치\n\n"
                "🎨 미리보기:\n"
                "• 플레이어 색상: 배치 가능\n"
                "• 빨간색: 규칙 위반으로 배치 불가\n\n"
                "🏆 블로커스 규칙:\n"
                "• 첫 블록: 자신의 모서리에서 시작\n"
                "• 이후 블록: 같은 색과 모서리 접촉\n"
                "• 같은 색끼리 변 접촉 금지\n"
                "• 다른 색과는 자유롭게 접촉")
        );
        gameGuideLabel->setStyleSheet("font-size: 10px; color: #666; margin-top: 10px;");
        gameGuideLabel->setWordWrap(true);
        layout->addWidget(gameGuideLabel);

        return panel;
    }

    void MainWindow::setupMenuBar()
    {
        QMenu* gameMenu = menuBar()->addMenu(QString::fromUtf8("게임(&G)"));
        gameMenu->addAction(QString::fromUtf8("새 게임(&N)"), this, &MainWindow::onNewGame, QKeySequence("Ctrl+N"));
        gameMenu->addAction(QString::fromUtf8("게임 리셋(&R)"), this, &MainWindow::onResetBoard, QKeySequence("Ctrl+R"));
        gameMenu->addSeparator();
        gameMenu->addAction(QString::fromUtf8("종료(&X)"), this, &QWidget::close, QKeySequence("Ctrl+Q"));

        QMenu* viewMenu = menuBar()->addMenu(QString::fromUtf8("보기(&V)"));
        viewMenu->addAction(QString::fromUtf8("상호작용 토글(&T)"), this, &MainWindow::onToggleReadOnly, QKeySequence("Ctrl+T"));

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
        toolBar->addAction(QString::fromUtf8("🔒"), this, &MainWindow::onToggleReadOnly)->setToolTip(QString::fromUtf8("상호작용 토글 (Ctrl+T)"));
        toolBar->addSeparator();
        toolBar->addAction(QString::fromUtf8("ℹ️"), this, &MainWindow::onAbout)->setToolTip(QString::fromUtf8("정보 (F1)"));
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
        connect(m_blockPalette, &GameBlockPalette::blockSelected, this, &MainWindow::onBlockSelected);

        // 버튼 시그널
        connect(m_resetButton, &QPushButton::clicked, this, &MainWindow::onResetBoard);
        connect(m_readOnlyButton, &QPushButton::clicked, this, &MainWindow::onToggleReadOnly);
        connect(m_newGameButton, &QPushButton::clicked, this, &MainWindow::onNewGame);
        connect(m_nextTurnButton, &QPushButton::clicked, this, &MainWindow::onNextTurn);

        // 게임 로직 시그널
        connect(m_blockPalette, &GameBlockPalette::playerChanged, this, [this](PlayerColor player) {
            Q_UNUSED(player)
                updateGameUI();
            });
    }

} // namespace Blokus