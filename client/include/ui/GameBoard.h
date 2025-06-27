#pragma once

#include <QGraphicsView>
#include <QGraphicsScene>
#include <QGraphicsRectItem>
#include <QTimer>
#include <QMouseEvent>
#include <QKeyEvent>
#include <QWheelEvent>
#include <QPen>
#include <QBrush>
#include <QColor>
#include <vector>
#include <map>

#include "common/Types.h"
#include "game/Block.h"
#include "game/GameLogic.h"

namespace Blokus {

    class GameBoard : public QGraphicsView
    {
        Q_OBJECT

    public:
        static constexpr qreal DEFAULT_CELL_SIZE = 25.0;

        explicit GameBoard(QWidget* parent = nullptr);
        ~GameBoard();

        // 게임 로직 연동
        void setGameLogic(GameLogic* gameLogic);
        bool tryPlaceCurrentBlock(const Position& position);
        void setSelectedBlock(const Block& block);

        // 기본 보드 상태
        bool isCellValid(int row, int col) const;
        bool isCellOccupied(int row, int col) const;
        PlayerColor getCellOwner(int row, int col) const;

        // 시각적 효과
        void highlightCell(int row, int col, const QColor& color);
        void clearHighlights();

        // 좌표 변환
        Position screenToBoard(const QPointF& screenPos) const;
        QPointF boardToScreen(const Position& boardPos) const;

        // 색상 관리
        QColor getPlayerColor(PlayerColor player) const;

        // 보드 관리
        void setBoardReadOnly(bool readOnly);
        void resetBoard();

        // 블록 배치 관련 (인터페이스 호환성)
        bool canPlaceBlock(const BlockPlacement& placement) const;
        bool placeBlock(const BlockPlacement& placement);
        void removeBlock(const Position& position);
        void showBlockPreview(const BlockPlacement& placement);
        void hideBlockPreview();

        // 블록 렌더링 관련
        void addBlockToBoard(const Block& block, const Position& position);
        void removeBlockFromBoard(const Position& position);
        void clearAllBlocks();

    signals:
        void cellClicked(int row, int col);
        void cellHovered(int row, int col);
        void blockPlaced(const BlockPlacement& placement);
        void blockRemoved(const Position& position);
        void blockRotated(const Block& block);
        void blockFlipped(const Block& block);
        void blockPlacedSuccessfully(BlockType blockType, PlayerColor player); // 새로 추가

    protected:
        void mousePressEvent(QMouseEvent* event) override;
        void mouseMoveEvent(QMouseEvent* event) override;
        void wheelEvent(QWheelEvent* event) override;
        void keyPressEvent(QKeyEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;
        void leaveEvent(QEvent* event) override;
        void focusInEvent(QFocusEvent* event) override;
        void focusOutEvent(QFocusEvent* event) override;

    private slots:
        void onSceneChanged();

    private:
        // 초기화
        void setupScene();
        void setupStyles();
        void initializeBoard();
        void clearBoard();

        // 격자 그리기
        void drawGrid();
        void drawStartingCorners();

        // 블록 그래픽 관련
        BlockGraphicsItem* createBlockGraphicsItem(const Block& block, const Position& position);
        bool isValidBlockPlacement(const Block& block, const Position& position) const;
        bool checkBlokusRules(const Block& block, const Position& position, PlayerColor player) const;
        QColor getPlayerBrushColor(PlayerColor player) const;
        QColor getPlayerBorderColor(PlayerColor player) const;

        // 미리보기 관련
        void showCurrentBlockPreview();

        // 뷰 관리
        void fitBoardToView();

        // 테스트/디버깅 함수들
        void addTestBlocks();
        void onShowAllBlocks();
        void onClearAllBlocks();
        void onAddRandomBlock();

        // UI 컴포넌트
        QGraphicsScene* m_scene;
        QGraphicsRectItem* m_boardRect;

        // 보드 상태
        PlayerColor m_board[BOARD_SIZE][BOARD_SIZE];
        bool m_readOnly;

        // 시각적 요소
        qreal m_cellSize;
        std::vector<QGraphicsRectItem*> m_gridCells;
        std::vector<QGraphicsRectItem*> m_highlights;
        std::vector<QGraphicsItem*> m_previewItems;

        // 마우스/키보드 상태
        Position m_hoveredCell;
        bool m_mousePressed;
        QTimer* m_hoverTimer;

        // 블록 관리
        std::vector<BlockGraphicsItem*> m_blockItems;
        std::map<Position, BlockGraphicsItem*> m_blockMap;
        BlockGraphicsItem* m_currentPreview;

        // 선택된 블록
        Block m_selectedBlock;
        bool m_hasSelectedBlock; // 추가된 멤버 변수
        int m_testBlockIndex;

        // 게임 로직 연동
        GameLogic* m_gameLogic;

        // 스타일
        QPen m_gridPen;
        QPen m_borderPen;
        QBrush m_emptyBrush;
        QBrush m_highlightBrush;
        std::map<PlayerColor, QColor> m_playerColors;
    };

} // namespace Blokus