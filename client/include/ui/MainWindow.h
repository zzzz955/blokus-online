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
        void onBlockPlacedSuccessfully(BlockType blockType, PlayerColor player); // ���� �߰�
        void onNewGame();

    protected:
        void resizeEvent(QResizeEvent* event) override;  // ���� �߰�

    private:
        void setupUI();
        void connectSignals();
        void updateGameUI();
        void resetAllBlockStates();
        void clearSelectedBlock(); // �߰��� �Լ�
        void adjustPalettesToWindowSize();

        // UI ���� �Լ���
        QWidget* createSimpleGameInfoPanel();     // �ܼ�ȭ�� ��� �г�
        QWidget* createSimpleStatusPanel();       // �ܼ�ȭ�� �ϴ� �г�
        QWidget* createMahjongStyleGameArea(); // �߰��� �Լ�
        QWidget* createCornerWidget();
        void setupResponsivePalettes(QWidget* north, QWidget* south, QWidget* east, QWidget* west);
        void setupStatusBar();

        // UI ������Ʈ��
        GameBoard* m_gameBoard;
        ImprovedGamePalette* m_improvedPalette;

        // ���� �󺧵�
        QLabel* m_coordinateLabel;
        QLabel* m_gameStatusLabel;
        QLabel* m_currentPlayerLabel;

        // ��ư��
        QPushButton* m_newGameButton;

        // ���� �Ŵ���
        GameStateManager* m_gameManager;
    };

} // namespace Blokus