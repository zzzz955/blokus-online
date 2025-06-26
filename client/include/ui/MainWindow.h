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
     * @brief 블로커스 게임의 메인 윈도우 클래스
     *
     * 주요 기능:
     * - GameBoard 위젯을 포함하는 UI 구성
     * - 메뉴바, 툴바, 상태바 관리
     * - 게임보드 이벤트 처리
     * - 사용자 인터페이스 제어
     */
    class MainWindow : public QMainWindow
    {
        Q_OBJECT

    public:
        explicit MainWindow(QWidget* parent = nullptr);
        ~MainWindow() = default;

    private slots:
        // 게임보드 이벤트 핸들러
        void onCellClicked(int row, int col);
        void onCellHovered(int row, int col);

        // UI 컨트롤 핸들러
        void onResetBoard();
        void onToggleReadOnly();
        void onAbout();

    private:
        // UI 설정 함수들
        void setupUI();
        QWidget* createControlPanel();
        void setupMenuBar();
        void setupToolBar();
        void setupStatusBar();
        void connectSignals();

        // 위젯 포인터들
        GameBoard* m_gameBoard;              // 메인 게임보드
        QLabel* m_coordinateLabel;           // 좌표 표시 라벨
        QPushButton* m_resetButton;          // 초기화 버튼
        QPushButton* m_readOnlyButton;       // 상호작용 토글 버튼
    };

} // namespace Blokus