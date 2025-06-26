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
        QString message = QString("Cell clicked: (%1, %2)").arg(row).arg(col);
        statusBar()->showMessage(message, 2000);
    }

    void MainWindow::onCellHovered(int row, int col)
    {
        QString message = QString("Hovering: (%1, %2)").arg(row).arg(col);
        m_coordinateLabel->setText(message);
    }

    void MainWindow::onResetBoard()
    {
        m_gameBoard->resetBoard();
        statusBar()->showMessage("Board reset", 1000);
    }

    void MainWindow::onToggleReadOnly()
    {
        bool readOnly = !m_gameBoard->property("readOnly").toBool();
        m_gameBoard->setBoardReadOnly(readOnly);
        m_gameBoard->setProperty("readOnly", readOnly);

        m_readOnlyButton->setText(readOnly ? "Enable Interaction" : "Disable Interaction");
        statusBar()->showMessage(readOnly ? "Board is read-only" : "Board is interactive", 1000);
    }

    void MainWindow::onAbout()
    {
        QMessageBox::about(this, "Blokus Online",
            "🎲 Blokus Online - Development Build\n\n"
            "✅ GameBoard Implementation Complete!\n\n"
            "Features:\n"
            "• 20x20 interactive grid\n"
            "• Mouse click/hover events\n"
            "• Zoom in/out with mouse wheel\n"
            "• Starting corner highlights\n"
            "• Coordinate tracking\n\n"
            "Next: Block rendering system");
    }

    void MainWindow::setupUI()
    {
        setWindowTitle("Blokus Online - GameBoard Test");
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
        QLabel* titleLabel = new QLabel("🎮 GameBoard Test");
        titleLabel->setStyleSheet("font-size: 16px; font-weight: bold; margin-bottom: 10px;");
        layout->addWidget(titleLabel);

        // 좌표 표시
        m_coordinateLabel = new QLabel("Move mouse over board");
        m_coordinateLabel->setStyleSheet("font-size: 12px; color: #666; margin-bottom: 10px;");
        layout->addWidget(m_coordinateLabel);

        // 버튼들
        m_resetButton = new QPushButton("🔄 Reset Board");
        m_readOnlyButton = new QPushButton("🔒 Disable Interaction");
        QPushButton* aboutButton = new QPushButton("ℹ️ About");

        layout->addWidget(m_resetButton);
        layout->addWidget(m_readOnlyButton);
        layout->addWidget(aboutButton);

        // About 버튼 연결
        connect(aboutButton, &QPushButton::clicked, this, &MainWindow::onAbout);

        // 스페이서
        layout->addStretch();

        // 정보 라벨
        QLabel* infoLabel = new QLabel(
            "Controls:\n"
            "• Click: Select cell\n"
            "• Hover: Show coordinates\n"
            "• Wheel: Zoom in/out\n"
            "• Resize: Auto-fit board");
        infoLabel->setStyleSheet("font-size: 11px; color: #888; margin-top: 10px;");
        infoLabel->setWordWrap(true);
        layout->addWidget(infoLabel);

        return panel;
    }

    void MainWindow::setupMenuBar()
    {
        QMenu* gameMenu = menuBar()->addMenu("&Game");
        gameMenu->addAction("&Reset Board", this, &MainWindow::onResetBoard, QKeySequence("Ctrl+R"));
        gameMenu->addSeparator();
        gameMenu->addAction("E&xit", this, &QWidget::close, QKeySequence("Ctrl+Q"));

        QMenu* viewMenu = menuBar()->addMenu("&View");
        viewMenu->addAction("&Toggle Interaction", this, &MainWindow::onToggleReadOnly, QKeySequence("Ctrl+T"));

        QMenu* helpMenu = menuBar()->addMenu("&Help");
        helpMenu->addAction("&About", this, &MainWindow::onAbout, QKeySequence("F1"));
    }

    void MainWindow::setupToolBar()
    {
        QToolBar* toolBar = addToolBar("Main");
        toolBar->addAction("🔄", this, &MainWindow::onResetBoard)->setToolTip("Reset Board (Ctrl+R)");
        toolBar->addAction("🔒", this, &MainWindow::onToggleReadOnly)->setToolTip("Toggle Interaction (Ctrl+T)");
        toolBar->addSeparator();
        toolBar->addAction("ℹ️", this, &MainWindow::onAbout)->setToolTip("About (F1)");
    }

    void MainWindow::setupStatusBar()
    {
        statusBar()->showMessage("GameBoard initialized - Click on cells to test interaction", 3000);
    }

    void MainWindow::connectSignals()
    {
        connect(m_gameBoard, &GameBoard::cellClicked, this, &MainWindow::onCellClicked);
        connect(m_gameBoard, &GameBoard::cellHovered, this, &MainWindow::onCellHovered);
        connect(m_resetButton, &QPushButton::clicked, this, &MainWindow::onResetBoard);
        connect(m_readOnlyButton, &QPushButton::clicked, this, &MainWindow::onToggleReadOnly);
    }

} // namespace Blokus