#pragma once

#include <QMainWindow>
#include <QWidget>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLabel>
#include <QPushButton>
#include <QMenuBar>
#include <QToolBar>
#include <QStatusBar>
#include <QMessageBox>
#include <QGroupBox>
#include <QSplitter>

#include "ui/GameBoard.h"
#include "game/GameLogic.h"

namespace Blokus {

    /**
     * @brief ���Ŀ�� ������ ���� ������ Ŭ����
     *
     * �ֿ� ���:
     * - GameBoard ������ �����ϴ� UI ����
     * - �޴���, ����, ���¹� ����
     * - ���Ӻ��� �̺�Ʈ ó��
     * - ����� �������̽� ����
     */
    class MainWindow : public QMainWindow
    {
        Q_OBJECT

    public:
        explicit MainWindow(QWidget* parent = nullptr);
        ~MainWindow(); // = default ����

    private slots:
        // ���Ӻ��� �̺�Ʈ �ڵ鷯
        void onCellClicked(int row, int col);
        void onCellHovered(int row, int col);

        // ��� �ȷ�Ʈ �̺�Ʈ �ڵ鷯 (���� �߰�)
        void onBlockSelected(const Block& block);

        // UI ��Ʈ�� �ڵ鷯
        void onResetBoard();
        void onToggleReadOnly();
        void onAbout();

        // ���� ��Ʈ�� �ڵ鷯 (���� �߰�)
        void onNewGame();
        void onNextTurn();

    private:
        // UI ���� �Լ���
        void setupUI();
        QWidget* createCompactControlPanel(); // ����ȭ�� ��Ʈ�� �г�
        QWidget* createGameInfoPanel();      // ���� ���� �г�
        void setupMenuBar();
        void setupToolBar();
        void setupStatusBar();
        void connectSignals();

        // ���� UI ������Ʈ
        void updateGameUI();
        void resetAllBlockStates();         // ��� ��� ���� ����

        // ���� �����͵�
        GameBoard* m_gameBoard;              // ���� ���Ӻ���
        class ImprovedGamePalette* m_improvedPalette; // ������ 4���� ��� �ȷ�Ʈ
        QLabel* m_coordinateLabel;           // ��ǥ ǥ�� ��
        QLabel* m_gameStatusLabel;           // ���� ���� ��
        QLabel* m_currentPlayerLabel;        // ���� �÷��̾� ��
        QPushButton* m_resetButton;          // �ʱ�ȭ ��ư
        QPushButton* m_readOnlyButton;       // ��ȣ�ۿ� ��� ��ư
        QPushButton* m_newGameButton;        // �� ���� ��ư
        QPushButton* m_nextTurnButton;       // ���� �� ��ư

        // ���� ����
        GameStateManager* m_gameManager;     // ���� ���� ������
    };

} // namespace Blokus