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

        // ���� ���� �ʱ�ȭ
        void initializeBoard();
        void clearBoard();

        // �� ���� ���
        bool isCellValid(int row, int col) const;
        bool isCellOccupied(int row, int col) const;
        PlayerColor getCellOwner(int row, int col) const;

        // ��� ��ġ ����
        bool canPlaceBlock(const BlockPlacement& placement) const;
        bool placeBlock(const BlockPlacement& placement);
        void removeBlock(const Position& position);

        // �ð��� �ǵ��
        void highlightCell(int row, int col, const QColor& color);
        void clearHighlights();
        void showBlockPreview(const BlockPlacement& placement);
        void hideBlockPreview();

        // ���� ����
        void resetBoard();
        void setBoardReadOnly(bool readOnly);

        // ��ǥ ��ȯ
        Position screenToBoard(const QPointF& screenPos) const;
        QPointF boardToScreen(const Position& boardPos) const;

    signals:
        void cellClicked(int row, int col);
        void cellHovered(int row, int col);
        void blockPlaced(const BlockPlacement& placement);
        void blockRemoved(const Position& position);

    protected:
        // �̺�Ʈ ó��
        void mousePressEvent(QMouseEvent* event) override;
        void mouseMoveEvent(QMouseEvent* event) override;
        void wheelEvent(QWheelEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;
        void leaveEvent(QEvent* event) override;

    private slots:
        void onSceneChanged();

    private:
        // �ʱ�ȭ �Լ���
        void setupScene();
        void setupGrid();
        void setupStyles();

        // �׸��� �Լ���
        void drawGrid();
        void drawCell(int row, int col, const QColor& color);
        void drawStartingCorners();

        // ��ƿ��Ƽ �Լ���
        void updateCellSize();
        void fitBoardToView();
        QColor getPlayerColor(PlayerColor player) const;

        // �����
        static constexpr int BOARD_SIZE = 20;
        static constexpr qreal MIN_CELL_SIZE = 15.0;
        static constexpr qreal MAX_CELL_SIZE = 50.0;
        static constexpr qreal DEFAULT_CELL_SIZE = 25.0;

    private:
        // Qt Graphics ����
        QGraphicsScene* m_scene;
        QGraphicsRectItem* m_boardRect;

        // ���� ����
        std::array<std::array<PlayerColor, BOARD_SIZE>, BOARD_SIZE> m_board;
        bool m_readOnly;

        // �ð��� ��ҵ�
        std::vector<QGraphicsRectItem*> m_gridCells;
        std::vector<QGraphicsRectItem*> m_highlights;
        std::vector<QGraphicsRectItem*> m_previewItems;

        // ��Ÿ�� ����
        qreal m_cellSize;
        QPen m_gridPen;
        QPen m_borderPen;
        QBrush m_emptyBrush;
        QBrush m_highlightBrush;

        // �÷��̾� ����
        std::map<PlayerColor, QColor> m_playerColors;

        // ���콺 ����
        Position m_hoveredCell;
        bool m_mousePressed;
        QTimer* m_hoverTimer;
    };

} // namespace Blokus