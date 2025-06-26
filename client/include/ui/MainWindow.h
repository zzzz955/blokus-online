#pragma once

#include <QMainWindow>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLabel>
#include <QPushButton>
#include <QMessageBox>
#include <QStatusBar>
#include <QMenuBar>
#include <QToolBar>

#include "ui/GameBoard.h"

namespace Blokus {

    class MainWindow : public QMainWindow
    {
        Q_OBJECT

    public:
        explicit MainWindow(QWidget* parent = nullptr);

    private slots:
        void onCellClicked(int row, int col);
        void onCellHovered(int row, int col);
        void onResetBoard();
        void onToggleReadOnly();
        void onAbout();

    private:
        void setupUI();
        QWidget* createControlPanel();
        void setupMenuBar();
        void setupToolBar();
        void setupStatusBar();
        void connectSignals();

    private:
        GameBoard* m_gameBoard;
        QLabel* m_coordinateLabel;
        QPushButton* m_resetButton;
        QPushButton* m_readOnlyButton;
    };

} // namespace Blokus