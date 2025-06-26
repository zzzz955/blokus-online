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
        void clearSelectedBlock(); // �߰��� �Լ�

        // UI ���� �Լ���
        QWidget* createGameInfoPanel();
        QWidget* createCompactControlPanel();
        QWidget* createMahjongStyleGameArea(); // �߰��� �Լ�
        void setupMenuBar();
        void setupToolBar();
        void setupStatusBar();

        // UI ������Ʈ��
        GameBoard* m_gameBoard;
        ImprovedGamePalette* m_improvedPalette;

        // ���� �󺧵�
        QLabel* m_coordinateLabel;
        QLabel* m_gameStatusLabel;
        QLabel* m_currentPlayerLabel;

        // ��ư��
        QPushButton* m_resetButton;
        QPushButton* m_readOnlyButton;
        QPushButton* m_newGameButton;
        QPushButton* m_nextTurnButton; // ���ŵ� ���������� ȣȯ���� ���� ����

        // ���� �Ŵ���
        GameStateManager* m_gameManager;
    };

} // namespace Blokus