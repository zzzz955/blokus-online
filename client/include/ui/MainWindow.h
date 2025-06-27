#pragma once

#include <QMainWindow>
#include <QLabel>
#include <QPushButton>
#include <QMessageBox>
#include <QMenuBar>
#include <QToolBar>
#include <QStatusBar>
#include <QGridLayout>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QDebug>

#include "ui/GameBoard.h"
#include "ui/ImprovedBlockPalette.h"
#include "game/GameLogic.h"

namespace Blokus {

    class MainWindow : public QMainWindow
    {
        Q_OBJECT

    public:
        explicit MainWindow(QWidget* parent = nullptr);
        ~MainWindow();

    private slots:
        void onCellClicked(int row, int col);
        void onCellHovered(int row, int col);
        void onBlockSelected(const Block& block);
        void onBlockPlacedSuccessfully(BlockType blockType, PlayerColor player); // 새로 추가
        void onNewGame();

    protected:
        void resizeEvent(QResizeEvent* event) override;  // 새로 추가

    private:
        void setupUI();
        void connectSignals();
        void updateGameUI();
        void resetAllBlockStates();
        void clearSelectedBlock(); // 추가된 함수
        void adjustPalettesToWindowSize();

        // UI 생성 함수들
        QWidget* createSimpleGameInfoPanel();     // 단순화된 상단 패널
        QWidget* createSimpleStatusPanel();       // 단순화된 하단 패널
        QWidget* createMahjongStyleGameArea(); // 추가된 함수
        QWidget* createCornerWidget();
        void setupResponsivePalettes(QWidget* north, QWidget* south, QWidget* east, QWidget* west);
        void setupStatusBar();

        // UI 컴포넌트들
        GameBoard* m_gameBoard;
        ImprovedGamePalette* m_improvedPalette;

        // 상태 라벨들
        QLabel* m_coordinateLabel;
        QLabel* m_gameStatusLabel;
        QLabel* m_currentPlayerLabel;

        // 버튼들
        QPushButton* m_newGameButton;

        // 게임 매니저
        GameStateManager* m_gameManager;
    };

} // namespace Blokus