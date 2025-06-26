#pragma once

#include <QGraphicsView>
#include <QGraphicsScene>
#include <QGraphicsRectItem>
#include <QMouseEvent>
#include <QWheelEvent>
#include <QResizeEvent>
#include <QKeyEvent>
#include <QTimer>
#include <QPen>
#include <QBrush>
#include <QColor>
#include <vector>
#include <map>

#include "common/Types.h"
#include "game/Block.h"
#include "game/GameLogic.h"

namespace Blokus {

    /**
     * @brief 블로커스 게임보드를 표시하고 상호작용을 처리하는 QGraphicsView 클래스
     *
     * 주요 기능:
     * - 20x20 격자 보드 렌더링
     * - 마우스 클릭/호버 이벤트 처리
     * - 블록 배치 및 미리보기
     * - 확대/축소 기능
     * - 시작 모서리 하이라이트
     * - 21가지 폴리오미노 블록 렌더링
     */
    class GameBoard : public QGraphicsView
    {
        Q_OBJECT

    public:
        // 기본 셀 크기 (픽셀)
        static constexpr qreal DEFAULT_CELL_SIZE = 25.0;

        explicit GameBoard(QWidget* parent = nullptr);
        ~GameBoard();

        // 보드 상태 조회
        bool isCellValid(int row, int col) const;
        bool isCellOccupied(int row, int col) const;
        PlayerColor getCellOwner(int row, int col) const;

        // 블록 배치 관련
        bool canPlaceBlock(const BlockPlacement& placement) const;
        bool placeBlock(const BlockPlacement& placement);
        void removeBlock(const Position& position);

        // 미리보기 기능
        void showBlockPreview(const BlockPlacement& placement);
        void hideBlockPreview();
        void showCurrentBlockPreview(); // 새로 추가

        // 블록 렌더링 (새로 추가)
        void addBlockToBoard(const Block& block, const Position& position);
        void removeBlockFromBoard(const Position& position);
        void clearAllBlocks();

        // 게임 로직 연동 (새로 추가)
        void setGameLogic(GameLogic* gameLogic);
        bool tryPlaceCurrentBlock(const Position& position);
        void setSelectedBlock(const Block& block);

        // 테스트용 블록 생성 (개발/디버깅용)
        void addTestBlocks();
        void showAllBlockTypes();

        // 하이라이트 기능
        void highlightCell(int row, int col, const QColor& color);
        void clearHighlights();

        // 좌표 변환
        Position screenToBoard(const QPointF& screenPos) const;
        QPointF boardToScreen(const Position& boardPos) const;

        // 플레이어 색상 얻기
        QColor getPlayerColor(PlayerColor player) const;

    public slots:
        // 보드 설정
        void setBoardReadOnly(bool readOnly);
        void resetBoard();

    signals:
        // 사용자 입력 이벤트
        void cellClicked(int row, int col);
        void cellHovered(int row, int col);
        void blockPlaced(const BlockPlacement& placement);
        void blockRemoved(const Position& position);
        void blockRotated(const Block& block);    // 새로 추가
        void blockFlipped(const Block& block);    // 새로 추가

        // 블록 관련 시그널 (새로 추가)
        void blockSelected(const Block& block);
        void blockDeselected();

    protected:
        // 이벤트 핸들러
        void mousePressEvent(QMouseEvent* event) override;
        void mouseMoveEvent(QMouseEvent* event) override;
        void wheelEvent(QWheelEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;
        void leaveEvent(QEvent* event) override;
        void keyPressEvent(QKeyEvent* event) override; // 블록 회전용

    private slots:
        void onSceneChanged();

    private:
        // 초기화 함수들
        void setupScene();
        void setupStyles();
        void initializeBoard();
        void clearBoard();

        // 렌더링 함수들
        void drawGrid();
        void drawStartingCorners();
        void fitBoardToView();

        // 블록 관련 함수들 (새로 추가)
        void updateBlockGraphics();
        BlockGraphicsItem* createBlockGraphicsItem(const Block& block, const Position& position);
        void updateCellOccupancy();

        // 블록 배치 검증
        bool isValidBlockPlacement(const Block& block, const Position& position) const;
        bool checkBlokusRules(const Block& block, const Position& position, PlayerColor player) const;

        // 유틸리티 함수들
        QColor getPlayerBrushColor(PlayerColor player) const;
        QColor getPlayerBorderColor(PlayerColor player) const;

        // 테스트/디버깅 함수들 (private로 이동)
        void onShowAllBlocks();
        void onClearAllBlocks();
        void onAddRandomBlock();

        // 멤버 변수들
        QGraphicsScene* m_scene;                    // 그래픽 씬
        QGraphicsRectItem* m_boardRect;             // 보드 경계 사각형

        // 보드 상태
        PlayerColor m_board[BOARD_SIZE][BOARD_SIZE]; // 보드 상태 배열
        bool m_readOnly;                            // 읽기 전용 모드
        qreal m_cellSize;                           // 셀 크기

        // 마우스 상태
        Position m_hoveredCell;                     // 현재 호버된 셀
        bool m_mousePressed;                        // 마우스 눌림 상태
        QTimer* m_hoverTimer;                       // 호버 지연 타이머

        // 그래픽 요소들
        std::vector<QGraphicsRectItem*> m_gridCells;    // 격자 셀들
        std::vector<QGraphicsRectItem*> m_highlights;   // 하이라이트 요소들
        std::vector<QGraphicsItem*> m_previewItems;     // 미리보기 요소들

        // 블록 관련 (새로 추가)
        std::vector<BlockGraphicsItem*> m_blockItems;   // 배치된 블록들
        std::map<Position, BlockGraphicsItem*> m_blockMap; // 위치별 블록 맵
        BlockGraphicsItem* m_currentPreview;            // 현재 미리보기 블록
        Block m_selectedBlock;                          // 현재 선택된 블록
        int m_testBlockIndex;                           // 테스트 블록 인덱스

        // 게임 로직 (새로 추가)
        GameLogic* m_gameLogic;                         // 게임 로직 참조

        // 스타일 설정
        QPen m_gridPen;                             // 격자 펜
        QPen m_borderPen;                           // 경계 펜
        QBrush m_emptyBrush;                        // 빈 셀 브러시
        QBrush m_highlightBrush;                    // 하이라이트 브러시

        // 플레이어 색상 맵
        std::map<PlayerColor, QColor> m_playerColors;
    };

} // namespace Blokus