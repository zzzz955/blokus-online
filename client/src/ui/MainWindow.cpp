#include "ui/MainWindow.h"
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
        QMessageBox::about(this, "블로커스 온라인",
            "블로커스 온라인 - 개발 빌드\n\n"
            "게임보드 구현 완료!\n\n"
            "기능:\n"
            "• 20x20 상호작용 격자\n"
            "• 마우스 클릭/호버 이벤트\n"
            "• 마우스 휠로 확대/축소\n"
            "• 시작 모서리 하이라이트\n"
            "• 좌표 추적\n\n"
            "다음 단계: 블록 렌더링 시스템");
    }

    void MainWindow::setupUI()
    {
        setWindowTitle("블로커스 온라인 - 게임보드 테스트");
        setMinimumSize(800, 600);

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
        panel->setFixedWidth(200);
        panel->setStyleSheet("QWidget { background-color: #f5f5f5; padding: 10px; }");

        QVBoxLayout* layout = new QVBoxLayout(panel);

        // 제목
        QLabel* titleLabel = new QLabel("게임보드 테스트");
        titleLabel->setStyleSheet("font-size: 16px; font-weight: bold; margin-bottom: 10px;");
        layout->addWidget(titleLabel);

        // 좌표 표시
        m_coordinateLabel = new QLabel("보드 위에서 마우스를 움직이세요");
        m_coordinateLabel->setStyleSheet("font-size: 12px; color: #666; margin-bottom: 10px;");
        layout->addWidget(m_coordinateLabel);

        // 버튼들
        m_resetButton = new QPushButton("보드 초기화");
        m_readOnlyButton = new QPushButton("상호작용 비활성화");
        QPushButton* aboutButton = new QPushButton("정보");

        layout->addWidget(m_resetButton);
        layout->addWidget(m_readOnlyButton);
        layout->addWidget(aboutButton);

        // About 버튼 연결
        connect(aboutButton, &QPushButton::clicked, this, &MainWindow::onAbout);

        // 스페이서
        layout->addStretch();

        // 정보 라벨
        QLabel* infoLabel = new QLabel(
            "조작법:\n"
            "• 클릭: 셀 선택\n"
            "• 마우스 오버: 좌표 표시\n"
            "• 휠: 확대/축소\n"
            "• 크기 조정: 자동 맞춤");
        infoLabel->setStyleSheet("font-size: 11px; color: #888; margin-top: 10px;");
        infoLabel->setWordWrap(true);
        layout->addWidget(infoLabel);

        return panel;
    }

    void MainWindow::setupMenuBar()
    {
        QMenu* gameMenu = menuBar()->addMenu("게임(&G)");
        gameMenu->addAction("보드 초기화(&R)", this, &MainWindow::onResetBoard, QKeySequence("Ctrl+R"));
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
        toolBar->addAction("초기화", this, &MainWindow::onResetBoard)->setToolTip("보드 초기화 (Ctrl+R)");
        toolBar->addAction("잠금", this, &MainWindow::onToggleReadOnly)->setToolTip("상호작용 토글 (Ctrl+T)");
        toolBar->addSeparator();
        toolBar->addAction("정보", this, &MainWindow::onAbout)->setToolTip("정보 (F1)");
    }

    void MainWindow::setupStatusBar()
    {
        statusBar()->showMessage("게임보드가 초기화되었습니다 - 셀을 클릭하여 상호작용을 테스트하세요", 3000);
    }

    void MainWindow::connectSignals()
    {
        connect(m_gameBoard, &GameBoard::cellClicked, this, &MainWindow::onCellClicked);
        connect(m_gameBoard, &GameBoard::cellHovered, this, &MainWindow::onCellHovered);
        connect(m_resetButton, &QPushButton::clicked, this, &MainWindow::onResetBoard);
        connect(m_readOnlyButton, &QPushButton::clicked, this, &MainWindow::onToggleReadOnly);
    }

} // namespace Blokus