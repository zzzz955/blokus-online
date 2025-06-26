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

        // ���� ���� ����
        void setGameLogic(GameLogic* gameLogic);
        bool tryPlaceCurrentBlock(const Position& position);
        void setSelectedBlock(const Block& block);

        // �⺻ ���� ����
        bool isCellValid(int row, int col) const;
        bool isCellOccupied(int row, int col) const;
        PlayerColor getCellOwner(int row, int col) const;

        // �ð��� ȿ��
        void highlightCell(int row, int col, const QColor& color);
        void clearHighlights();

        // ��ǥ ��ȯ
        Position screenToBoard(const QPointF& screenPos) const;
        QPointF boardToScreen(const Position& boardPos) const;

        // ���� ����
        QColor getPlayerColor(PlayerColor player) const;

        // ���� ����
        void setBoardReadOnly(bool readOnly);
        void resetBoard();

        // ��� ��ġ ���� (�������̽� ȣȯ��)
        bool canPlaceBlock(const BlockPlacement& placement) const;
        bool placeBlock(const BlockPlacement& placement);
        void removeBlock(const Position& position);
        void showBlockPreview(const BlockPlacement& placement);
        void hideBlockPreview();

        // ��� ������ ����
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
        void blockPlacedSuccessfully(BlockType blockType, PlayerColor player); // ���� �߰�

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
        // �ʱ�ȭ
        void setupScene();
        void setupStyles();
        void initializeBoard();
        void clearBoard();

        // ���� �׸���
        void drawGrid();
        void drawStartingCorners();

        // ��� �׷��� ����
        BlockGraphicsItem* createBlockGraphicsItem(const Block& block, const Position& position);
        bool isValidBlockPlacement(const Block& block, const Position& position) const;
        bool checkBlokusRules(const Block& block, const Position& position, PlayerColor player) const;
        QColor getPlayerBrushColor(PlayerColor player) const;
        QColor getPlayerBorderColor(PlayerColor player) const;

        // �̸����� ����
        void showCurrentBlockPreview();

        // �� ����
        void fitBoardToView();

        // �׽�Ʈ/����� �Լ���
        void addTestBlocks();
        void onShowAllBlocks();
        void onClearAllBlocks();
        void onAddRandomBlock();

        // UI ������Ʈ
        QGraphicsScene* m_scene;
        QGraphicsRectItem* m_boardRect;

        // ���� ����
        PlayerColor m_board[BOARD_SIZE][BOARD_SIZE];
        bool m_readOnly;

        // �ð��� ���
        qreal m_cellSize;
        std::vector<QGraphicsRectItem*> m_gridCells;
        std::vector<QGraphicsRectItem*> m_highlights;
        std::vector<QGraphicsItem*> m_previewItems;

        // ���콺/Ű���� ����
        Position m_hoveredCell;
        bool m_mousePressed;
        QTimer* m_hoverTimer;

        // ��� ����
        std::vector<BlockGraphicsItem*> m_blockItems;
        std::map<Position, BlockGraphicsItem*> m_blockMap;
        BlockGraphicsItem* m_currentPreview;

        // ���õ� ���
        Block m_selectedBlock;
        bool m_hasSelectedBlock; // �߰��� ��� ����
        int m_testBlockIndex;

        // ���� ���� ����
        GameLogic* m_gameLogic;

        // ��Ÿ��
        QPen m_gridPen;
        QPen m_borderPen;
        QBrush m_emptyBrush;
        QBrush m_highlightBrush;
        std::map<PlayerColor, QColor> m_playerColors;
    };

} // namespace Blokus