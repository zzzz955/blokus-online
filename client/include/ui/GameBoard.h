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
     * @brief ���Ŀ�� ���Ӻ��带 ǥ���ϰ� ��ȣ�ۿ��� ó���ϴ� QGraphicsView Ŭ����
     *
     * �ֿ� ���:
     * - 20x20 ���� ���� ������
     * - ���콺 Ŭ��/ȣ�� �̺�Ʈ ó��
     * - ��� ��ġ �� �̸�����
     * - Ȯ��/��� ���
     * - ���� �𼭸� ���̶���Ʈ
     * - 21���� �������̳� ��� ������
     */
    class GameBoard : public QGraphicsView
    {
        Q_OBJECT

    public:
        // �⺻ �� ũ�� (�ȼ�)
        static constexpr qreal DEFAULT_CELL_SIZE = 25.0;

        explicit GameBoard(QWidget* parent = nullptr);
        ~GameBoard();

        // ���� ���� ��ȸ
        bool isCellValid(int row, int col) const;
        bool isCellOccupied(int row, int col) const;
        PlayerColor getCellOwner(int row, int col) const;

        // ��� ��ġ ����
        bool canPlaceBlock(const BlockPlacement& placement) const;
        bool placeBlock(const BlockPlacement& placement);
        void removeBlock(const Position& position);

        // �̸����� ���
        void showBlockPreview(const BlockPlacement& placement);
        void hideBlockPreview();
        void showCurrentBlockPreview(); // ���� �߰�

        // ��� ������ (���� �߰�)
        void addBlockToBoard(const Block& block, const Position& position);
        void removeBlockFromBoard(const Position& position);
        void clearAllBlocks();

        // ���� ���� ���� (���� �߰�)
        void setGameLogic(GameLogic* gameLogic);
        bool tryPlaceCurrentBlock(const Position& position);
        void setSelectedBlock(const Block& block);

        // �׽�Ʈ�� ��� ���� (����/������)
        void addTestBlocks();
        void showAllBlockTypes();

        // ���̶���Ʈ ���
        void highlightCell(int row, int col, const QColor& color);
        void clearHighlights();

        // ��ǥ ��ȯ
        Position screenToBoard(const QPointF& screenPos) const;
        QPointF boardToScreen(const Position& boardPos) const;

        // �÷��̾� ���� ���
        QColor getPlayerColor(PlayerColor player) const;

    public slots:
        // ���� ����
        void setBoardReadOnly(bool readOnly);
        void resetBoard();

    signals:
        // ����� �Է� �̺�Ʈ
        void cellClicked(int row, int col);
        void cellHovered(int row, int col);
        void blockPlaced(const BlockPlacement& placement);
        void blockRemoved(const Position& position);
        void blockRotated(const Block& block);    // ���� �߰�
        void blockFlipped(const Block& block);    // ���� �߰�

        // ��� ���� �ñ׳� (���� �߰�)
        void blockSelected(const Block& block);
        void blockDeselected();

    protected:
        // �̺�Ʈ �ڵ鷯
        void mousePressEvent(QMouseEvent* event) override;
        void mouseMoveEvent(QMouseEvent* event) override;
        void wheelEvent(QWheelEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;
        void leaveEvent(QEvent* event) override;
        void keyPressEvent(QKeyEvent* event) override; // ��� ȸ����

    private slots:
        void onSceneChanged();

    private:
        // �ʱ�ȭ �Լ���
        void setupScene();
        void setupStyles();
        void initializeBoard();
        void clearBoard();

        // ������ �Լ���
        void drawGrid();
        void drawStartingCorners();
        void fitBoardToView();

        // ��� ���� �Լ��� (���� �߰�)
        void updateBlockGraphics();
        BlockGraphicsItem* createBlockGraphicsItem(const Block& block, const Position& position);
        void updateCellOccupancy();

        // ��� ��ġ ����
        bool isValidBlockPlacement(const Block& block, const Position& position) const;
        bool checkBlokusRules(const Block& block, const Position& position, PlayerColor player) const;

        // ��ƿ��Ƽ �Լ���
        QColor getPlayerBrushColor(PlayerColor player) const;
        QColor getPlayerBorderColor(PlayerColor player) const;

        // �׽�Ʈ/����� �Լ��� (private�� �̵�)
        void onShowAllBlocks();
        void onClearAllBlocks();
        void onAddRandomBlock();

        // ��� ������
        QGraphicsScene* m_scene;                    // �׷��� ��
        QGraphicsRectItem* m_boardRect;             // ���� ��� �簢��

        // ���� ����
        PlayerColor m_board[BOARD_SIZE][BOARD_SIZE]; // ���� ���� �迭
        bool m_readOnly;                            // �б� ���� ���
        qreal m_cellSize;                           // �� ũ��

        // ���콺 ����
        Position m_hoveredCell;                     // ���� ȣ���� ��
        bool m_mousePressed;                        // ���콺 ���� ����
        QTimer* m_hoverTimer;                       // ȣ�� ���� Ÿ�̸�

        // �׷��� ��ҵ�
        std::vector<QGraphicsRectItem*> m_gridCells;    // ���� ����
        std::vector<QGraphicsRectItem*> m_highlights;   // ���̶���Ʈ ��ҵ�
        std::vector<QGraphicsItem*> m_previewItems;     // �̸����� ��ҵ�

        // ��� ���� (���� �߰�)
        std::vector<BlockGraphicsItem*> m_blockItems;   // ��ġ�� ��ϵ�
        std::map<Position, BlockGraphicsItem*> m_blockMap; // ��ġ�� ��� ��
        BlockGraphicsItem* m_currentPreview;            // ���� �̸����� ���
        Block m_selectedBlock;                          // ���� ���õ� ���
        int m_testBlockIndex;                           // �׽�Ʈ ��� �ε���

        // ���� ���� (���� �߰�)
        GameLogic* m_gameLogic;                         // ���� ���� ����

        // ��Ÿ�� ����
        QPen m_gridPen;                             // ���� ��
        QPen m_borderPen;                           // ��� ��
        QBrush m_emptyBrush;                        // �� �� �귯��
        QBrush m_highlightBrush;                    // ���̶���Ʈ �귯��

        // �÷��̾� ���� ��
        std::map<PlayerColor, QColor> m_playerColors;
    };

} // namespace Blokus