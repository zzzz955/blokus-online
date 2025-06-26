#pragma once

#include <QGraphicsView>
#include <QGraphicsScene>
#include <QGraphicsRectItem>
#include <QMouseEvent>
#include <QWheelEvent>
#include <QResizeEvent>
#include <QPen>
#include <QBrush>
#include <QTimer>

#include "common/Types.h"

namespace Blokus {

    class GameBoard : public QGraphicsView
    {
        Q_OBJECT

    public:
        explicit GameBoard(QWidget* parent = nullptr);
        ~GameBoard() override;

        // 게임 보드 초기화
        void initializeBoard();
        void clearBoard();

        // 셀 관련 기능
        bool isCellValid(int row, int col) const;
        bool isCellOccupied(int row, int col) const;
        PlayerColor getCellOwner(int row, int col) const;

        // 블록 배치 관련
        bool canPlaceBlock(const BlockPlacement& placement) const;
        bool placeBlock(const BlockPlacement& placement);
        void removeBlock(const Position& position);

        // 시각적 피드백
        void highlightCell(int row, int col, const QColor& color);
        void clearHighlights();
        void showBlockPreview(const BlockPlacement& placement);
        void hideBlockPreview();

        // 보드 상태
        void resetBoard();
        void setBoardReadOnly(bool readOnly);

        // 좌표 변환
        Position screenToBoard(const QPointF& screenPos) const;
        QPointF boardToScreen(const Position& boardPos) const;

    signals:
        void cellClicked(int row, int col);
        void cellHovered(int row, int col);
        void blockPlaced(const BlockPlacement& placement);
        void blockRemoved(const Position& position);

    protected:
        // 이벤트 처리
        void mousePressEvent(QMouseEvent* event) override;
        void mouseMoveEvent(QMouseEvent* event) override;
        void wheelEvent(QWheelEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;
        void leaveEvent(QEvent* event) override;

    private slots:
        void onSceneChanged();

    private:
        // 초기화 함수들
        void setupScene();
        void setupGrid();
        void setupStyles();

        // 그리기 함수들
        void drawGrid();
        void drawCell(int row, int col, const QColor& color);
        void drawStartingCorners();

        // 유틸리티 함수들
        void updateCellSize();
        void fitBoardToView();
        QColor getPlayerColor(PlayerColor player) const;

        // 상수들
        static constexpr int BOARD_SIZE = 20;
        static constexpr qreal MIN_CELL_SIZE = 15.0;
        static constexpr qreal MAX_CELL_SIZE = 50.0;
        static constexpr qreal DEFAULT_CELL_SIZE = 25.0;

    private:
        // Qt Graphics 관련
        QGraphicsScene* m_scene;
        QGraphicsRectItem* m_boardRect;

        // 게임 상태
        std::array<std::array<PlayerColor, BOARD_SIZE>, BOARD_SIZE> m_board;
        bool m_readOnly;

        // 시각적 요소들
        std::vector<QGraphicsRectItem*> m_gridCells;
        std::vector<QGraphicsRectItem*> m_highlights;
        std::vector<QGraphicsRectItem*> m_previewItems;

        // 스타일 설정
        qreal m_cellSize;
        QPen m_gridPen;
        QPen m_borderPen;
        QBrush m_emptyBrush;
        QBrush m_highlightBrush;

        // 플레이어 색상
        std::map<PlayerColor, QColor> m_playerColors;

        // 마우스 상태
        Position m_hoveredCell;
        bool m_mousePressed;
        QTimer* m_hoverTimer;
    };

} // namespace Blokus