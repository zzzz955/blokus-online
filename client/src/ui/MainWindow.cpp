#include "ui/MainWindow.h"
#include "game/Block.h"
#include <QApplication>

namespace Blokus {

    MainWindow::MainWindow(QWidget* parent)
        : QMainWindow(parent)
        , m_gameBoard(nullptr)
        , m_coordinateLabel(nullptr)
        , m_resetButton(nullptr)
        , m_readOnlyButton(nullptr)
    {
        setupUI();
        connectSignals();
    }

    void MainWindow::onCellClicked(int row, int col)
    {
        QString message = QString("클릭된 셀: (%1, %2)").arg(row).arg(col);
        statusBar()->showMessage(message, 2000);
    }

    void MainWindow::onCellHovered(int row, int col)
    {
        QString message = QString("마우스 위치: (%1, %2)").arg(row).arg(col);
        m_coordinateLabel->setText(message);
    }

    void MainWindow::onResetBoard()
    {
        m_gameBoard->resetBoard();
        statusBar()->showMessage("보드가 초기화되었습니다", 1000);
    }

    void MainWindow::onToggleReadOnly()
    {
        bool readOnly = !m_gameBoard->property("readOnly").toBool();
        m_gameBoard->setBoardReadOnly(readOnly);
        m_gameBoard->setProperty("readOnly", readOnly);

        m_readOnlyButton->setText(readOnly ? "상호작용 활성화" : "상호작용 비활성화");
        statusBar()->showMessage(readOnly ? "보드가 읽기 전용입니다" : "보드 상호작용이 활성화되었습니다", 1000);
    }

    void MainWindow::onAbout()
    {
        QMessageBox::about(this, QString::fromUtf8("블로커스 온라인"),
            QString::fromUtf8("🎲 블로커스 온라인 - 개발 빌드 v1.3\n\n"
                "✅ 직관적 UX 블록 시스템 완성!\n\n"
                "🎮 핵심 개선사항:\n"
                "• 좌클릭으로 미리보기 블록 배치\n"
                "• 키 조작 시 즉시 미리보기 반영\n"
                "• 배치 불가능 시 빨간색 경고 표시\n"
                "• N키로 즉시 블록 타입 변경\n"
                "• 실시간 호버 미리보기\n\n"
                "🎯 완벽한 상호작용 플로우:\n"
                "1. 마우스 오버로 미리보기 확인\n"
                "2. R/F키로 회전/뒤집기 (즉시 반영)\n"
                "3. N키로 블록 타입 변경 (즉시 반영)\n"
                "4. 빨간색이면 배치 불가, 다른 색이면 가능\n"
                "5. 좌클릭으로 배치 완료\n"
                "6. Delete키로 제거\n\n"
                "🎨 색상 가이드:\n"
                "• 파랑/노랑/빨강/초록: 배치 가능\n"
                "• 빨간색: 충돌로 배치 불가\n\n"
                "다음 단계: 블로커스 게임 규칙 구현"));
    }

    void MainWindow::setupUI()
    {
        setWindowTitle("블로커스 온라인 - 블록 렌더링 테스트");
        setMinimumSize(1000, 700); // 더 큰 창 크기

        // 중앙 위젯 설정
        QWidget* centralWidget = new QWidget(this);
        setCentralWidget(centralWidget);

        // 메인 레이아웃
        QHBoxLayout* mainLayout = new QHBoxLayout(centralWidget);

        // 게임 보드
        m_gameBoard = new GameBoard(this);
        mainLayout->addWidget(m_gameBoard, 1);

        // 우측 컨트롤 패널
        QWidget* controlPanel = createControlPanel();
        mainLayout->addWidget(controlPanel);

        // 메뉴 바 설정
        setupMenuBar();

        // 툴바 설정
        setupToolBar();

        // 상태 바 설정
        setupStatusBar();
    }

    QWidget* MainWindow::createControlPanel()
    {
        QWidget* panel = new QWidget();
        panel->setFixedWidth(250); // 더 넓은 패널
        panel->setStyleSheet("QWidget { background-color: #f5f5f5; padding: 10px; }");

        QVBoxLayout* layout = new QVBoxLayout(panel);

        // 제목
        QLabel* titleLabel = new QLabel("🎮 블록 렌더링 테스트");
        titleLabel->setStyleSheet("font-size: 16px; font-weight: bold; margin-bottom: 10px;");
        layout->addWidget(titleLabel);

        // 좌표 표시
        m_coordinateLabel = new QLabel("보드 위에서 마우스를 움직이세요");
        m_coordinateLabel->setStyleSheet("font-size: 12px; color: #666; margin-bottom: 15px;");
        m_coordinateLabel->setWordWrap(true);
        layout->addWidget(m_coordinateLabel);

        // 보드 제어 그룹
        QGroupBox* boardGroup = new QGroupBox("보드 제어");
        QVBoxLayout* boardLayout = new QVBoxLayout(boardGroup);

        m_resetButton = new QPushButton("🔄 보드 초기화");
        m_readOnlyButton = new QPushButton("🔒 상호작용 비활성화");

        boardLayout->addWidget(m_resetButton);
        boardLayout->addWidget(m_readOnlyButton);
        layout->addWidget(boardGroup);

        // 블록 테스트 그룹 (새로 추가)
        QGroupBox* blockGroup = new QGroupBox("블록 테스트");
        QVBoxLayout* blockLayout = new QVBoxLayout(blockGroup);

        QPushButton* showAllBlocksBtn = new QPushButton("📚 모든 블록 보기");
        QPushButton* clearBlocksBtn = new QPushButton("🗑️ 모든 블록 지우기");
        QPushButton* addRandomBlockBtn = new QPushButton("🎲 랜덤 블록 추가");
        QPushButton* addTestBlocksBtn = new QPushButton("🧪 테스트 블록들");

        blockLayout->addWidget(showAllBlocksBtn);
        blockLayout->addWidget(clearBlocksBtn);
        blockLayout->addWidget(addRandomBlockBtn);
        blockLayout->addWidget(addTestBlocksBtn);
        layout->addWidget(blockGroup);

        // 정보 그룹
        QGroupBox* infoGroup = new QGroupBox("정보");
        QVBoxLayout* infoLayout = new QVBoxLayout(infoGroup);

        QPushButton* aboutButton = new QPushButton("ℹ️ 정보");
        infoLayout->addWidget(aboutButton);
        layout->addWidget(infoGroup);

        // 스페이서
        layout->addStretch();

        // 조작법 라벨
        QLabel* controlsLabel = new QLabel(
            QString::fromUtf8("🎯 조작법:\n"
                "• 좌클릭: 미리보기 블록 배치\n"
                "• 우클릭: 랜덤 블록 추가\n"
                "• 마우스 오버: 실시간 미리보기\n"
                "• 휠: 확대/축소\n\n"
                "⌨️ 키보드 조작:\n"
                "• R 키: 블록 회전 (즉시 반영)\n"
                "• F 키: 블록 뒤집기 (즉시 반영)\n"
                "• N 키: 다음 블록 타입 (즉시 변경)\n"
                "• C 키: 플레이어 색상 변경\n"
                "• Delete: 호버 위치 블록 제거\n\n"
                "🎨 미리보기:\n"
                "• 초록/파랑/빨강/노랑: 배치 가능\n"
                "• 빨간색: 배치 불가능\n\n"
                "🔍 블록 종류:\n"
                "• 1칸: 단일 • 2칸: 도미노\n"
                "• 3칸: 트리오미노 (2개)\n"
                "• 4칸: 테트로미노 (5개)\n"
                "• 5칸: 펜토미노 (12개)\n"
                "총 21가지 블록")
        );
        controlsLabel->setStyleSheet("font-size: 10px; color: #666; margin-top: 10px;");
        controlsLabel->setWordWrap(true);
        layout->addWidget(controlsLabel);

        // 시그널 연결
        connect(aboutButton, &QPushButton::clicked, this, &MainWindow::onAbout);
        connect(showAllBlocksBtn, &QPushButton::clicked, m_gameBoard, &GameBoard::onShowAllBlocks);
        connect(clearBlocksBtn, &QPushButton::clicked, m_gameBoard, &GameBoard::onClearAllBlocks);
        connect(addRandomBlockBtn, &QPushButton::clicked, m_gameBoard, &GameBoard::onAddRandomBlock);
        connect(addTestBlocksBtn, &QPushButton::clicked, m_gameBoard, &GameBoard::addTestBlocks);

        return panel;
    }

    void MainWindow::setupMenuBar()
    {
        QMenu* gameMenu = menuBar()->addMenu("게임(&G)");
        gameMenu->addAction("보드 초기화(&R)", this, &MainWindow::onResetBoard, QKeySequence("Ctrl+R"));
        gameMenu->addSeparator();
        gameMenu->addAction("모든 블록 보기(&A)", m_gameBoard, &GameBoard::onShowAllBlocks, QKeySequence("Ctrl+A"));
        gameMenu->addAction("모든 블록 지우기(&C)", m_gameBoard, &GameBoard::onClearAllBlocks, QKeySequence("Ctrl+C"));
        gameMenu->addAction("랜덤 블록 추가(&D)", m_gameBoard, &GameBoard::onAddRandomBlock, QKeySequence("Ctrl+D"));
        gameMenu->addSeparator();
        gameMenu->addAction("종료(&X)", this, &QWidget::close, QKeySequence("Ctrl+Q"));

        QMenu* viewMenu = menuBar()->addMenu("보기(&V)");
        viewMenu->addAction("상호작용 토글(&T)", this, &MainWindow::onToggleReadOnly, QKeySequence("Ctrl+T"));

        QMenu* helpMenu = menuBar()->addMenu("도움말(&H)");
        helpMenu->addAction("정보(&A)", this, &MainWindow::onAbout, QKeySequence("F1"));
    }

    void MainWindow::setupToolBar()
    {
        QToolBar* toolBar = addToolBar("메인");

        // 기본 도구들
        toolBar->addAction("🔄", this, &MainWindow::onResetBoard)->setToolTip("보드 초기화 (Ctrl+R)");
        toolBar->addAction("🔒", this, &MainWindow::onToggleReadOnly)->setToolTip("상호작용 토글 (Ctrl+T)");
        toolBar->addSeparator();

        // 블록 관련 도구들
        toolBar->addAction("📚", m_gameBoard, &GameBoard::onShowAllBlocks)->setToolTip("모든 블록 보기 (Ctrl+A)");
        toolBar->addAction("🗑️", m_gameBoard, &GameBoard::onClearAllBlocks)->setToolTip("모든 블록 지우기 (Ctrl+C)");
        toolBar->addAction("🎲", m_gameBoard, &GameBoard::onAddRandomBlock)->setToolTip("랜덤 블록 추가 (Ctrl+D)");
        toolBar->addSeparator();

        toolBar->addAction("ℹ️", this, &MainWindow::onAbout)->setToolTip("정보 (F1)");
    }

    void MainWindow::setupStatusBar()
    {
        statusBar()->showMessage("블록 렌더링 시스템이 초기화되었습니다 - 우클릭으로 랜덤 블록을 추가하거나 테스트 버튼을 사용하세요", 5000);
    }

    void MainWindow::connectSignals()
    {
        connect(m_gameBoard, &GameBoard::cellClicked, this, &MainWindow::onCellClicked);
        connect(m_gameBoard, &GameBoard::cellHovered, this, &MainWindow::onCellHovered);
        connect(m_resetButton, &QPushButton::clicked, this, &MainWindow::onResetBoard);
        connect(m_readOnlyButton, &QPushButton::clicked, this, &MainWindow::onToggleReadOnly);

        // 블록 관련 시그널들 (새로 추가)
        connect(m_gameBoard, &GameBoard::blockPlaced, this, [this](const BlockPlacement& placement) {
            QString message = QString("블록 배치됨: %1 (%2, %3)")
                .arg(BlockFactory::getBlockName(placement.type))
                .arg(placement.position.first)
                .arg(placement.position.second);
            statusBar()->showMessage(message, 3000);
            });

        connect(m_gameBoard, &GameBoard::blockRemoved, this, [this](const Position& position) {
            QString message = QString("블록 제거됨: (%1, %2)")
                .arg(position.first)
                .arg(position.second);
            statusBar()->showMessage(message, 2000);
            });
    }

} // namespace Blokus