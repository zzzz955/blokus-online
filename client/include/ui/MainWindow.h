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
        void onNewGame();
        void onResetBoard();
        void onToggleReadOnly();
        void onAbout();

    private:
        void setupUI();
        void connectSignals();
        void updateGameUI();
        void resetAllBlockStates();
        void clearSelectedBlock(); // 추가된 함수

        // UI 생성 함수들
        QWidget* createGameInfoPanel();
        QWidget* createCompactControlPanel();
        QWidget* createMahjongStyleGameArea(); // 추가된 함수
        void setupMenuBar();
        void setupToolBar();
        void setupStatusBar();

        // UI 컴포넌트들
        GameBoard* m_gameBoard;
        ImprovedGamePalette* m_improvedPalette;

        // 상태 라벨들
        QLabel* m_coordinateLabel;
        QLabel* m_gameStatusLabel;
        QLabel* m_currentPlayerLabel;

        // 버튼들
        QPushButton* m_resetButton;
        QPushButton* m_readOnlyButton;
        QPushButton* m_newGameButton;
        QPushButton* m_nextTurnButton; // 제거될 예정이지만 호환성을 위해 유지

        // 게임 매니저
        GameStateManager* m_gameManager;
    };

} // namespace Blokus