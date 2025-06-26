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

#include "ui/GameBoard.h"

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
        ~MainWindow() = default;

    private slots:
        // ���Ӻ��� �̺�Ʈ �ڵ鷯
        void onCellClicked(int row, int col);
        void onCellHovered(int row, int col);

        // UI ��Ʈ�� �ڵ鷯
        void onResetBoard();
        void onToggleReadOnly();
        void onAbout();

    private:
        // UI ���� �Լ���
        void setupUI();
        QWidget* createControlPanel();
        void setupMenuBar();
        void setupToolBar();
        void setupStatusBar();
        void connectSignals();

        // ���� �����͵�
        GameBoard* m_gameBoard;              // ���� ���Ӻ���
        QLabel* m_coordinateLabel;           // ��ǥ ǥ�� ��
        QPushButton* m_resetButton;          // �ʱ�ȭ ��ư
        QPushButton* m_readOnlyButton;       // ��ȣ�ۿ� ��� ��ư
    };

} // namespace Blokus