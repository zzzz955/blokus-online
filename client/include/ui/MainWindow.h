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
        ~MainWindow(); // = default 제거

    private slots:
        // 게임보드 이벤트 핸들러
        void onCellClicked(int row, int col);
        void onCellHovered(int row, int col);

        // 블록 팔레트 이벤트 핸들러 (새로 추가)
        void onBlockSelected(const Block& block);

        // UI 컨트롤 핸들러
        void onResetBoard();
        void onToggleReadOnly();
        void onAbout();

        // 게임 컨트롤 핸들러 (새로 추가)
        void onNewGame();
        void onNextTurn();

    private:
        // UI 설정 함수들
        void setupUI();
        QWidget* createCompactControlPanel(); // 간소화된 컨트롤 패널
        QWidget* createGameInfoPanel();      // 게임 정보 패널
        void setupMenuBar();
        void setupToolBar();
        void setupStatusBar();
        void connectSignals();

        // 게임 UI 업데이트
        void updateGameUI();
        void resetAllBlockStates();         // 모든 블록 상태 리셋

        // 위젯 포인터들
        GameBoard* m_gameBoard;              // 메인 게임보드
        class ImprovedGamePalette* m_improvedPalette; // 개선된 4방향 블록 팔레트
        QLabel* m_coordinateLabel;           // 좌표 표시 라벨
        QLabel* m_gameStatusLabel;           // 게임 상태 라벨
        QLabel* m_currentPlayerLabel;        // 현재 플레이어 라벨
        QPushButton* m_resetButton;          // 초기화 버튼
        QPushButton* m_readOnlyButton;       // 상호작용 토글 버튼
        QPushButton* m_newGameButton;        // 새 게임 버튼
        QPushButton* m_nextTurnButton;       // 다음 턴 버튼

        // 게임 로직
        GameStateManager* m_gameManager;     // 게임 상태 관리자
    };

} // namespace Blokus