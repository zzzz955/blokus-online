#pragma once

#include <vector>
#include <map>
#include <QGraphicsItemGroup>
#include <QGraphicsRectItem>
#include <QPen>
#include <QBrush>

#include "common/Types.h"

namespace Blokus {

    /**
     * @brief ���� ���(�������̳�)�� �����ϰ� �����ϴ� Ŭ����
     *
     * �ֿ� ���:
     * - 21���� ��� Ÿ�Ժ� ��� ����
     * - ȸ�� �� ������ ��ȯ
     * - ��� ������ �� �ð�ȭ
     * - �浹 �˻�� ��ǥ ���
     */
    class Block
    {
    public:
        explicit Block(BlockType type = BlockType::Single, PlayerColor player = PlayerColor::Blue);
        ~Block() = default;

        // ��� ���� ����
        BlockType getType() const { return m_type; }
        PlayerColor getPlayer() const { return m_player; }
        Rotation getRotation() const { return m_rotation; }
        FlipState getFlipState() const { return m_flipState; }

        // ��� ��ȯ
        void setRotation(Rotation rotation);
        void setFlipState(FlipState flip);
        void setPlayer(PlayerColor player) { m_player = player; }

        // ȸ��/������ ����
        void rotateClockwise();
        void rotateCounterclockwise();
        void flipHorizontal();
        void flipVertical();
        void resetTransform();

        // ��ǥ ���
        PositionList getCurrentShape() const;
        PositionList getAbsolutePositions(const Position& basePos) const;
        QRect getBoundingRect() const;
        int getSize() const; // ����� �����ϴ� �� ����

        // �浹 �˻�
        bool wouldCollideAt(const Position& basePos, const PositionList& occupiedCells) const;

        // ��� ����
        bool isValidPlacement(const Position& basePos, int boardSize = BOARD_SIZE) const;

    private:
        // �⺻ ��� ��� ���� (���� ������)
        static const std::map<BlockType, PositionList> s_blockShapes;

        // ��ȯ �Լ���
        PositionList applyRotation(const PositionList& shape, Rotation rotation) const;
        PositionList applyFlip(const PositionList& shape, FlipState flip) const;
        PositionList normalizeShape(const PositionList& shape) const;

        // ��� ����
        BlockType m_type;
        PlayerColor m_player;
        Rotation m_rotation;
        FlipState m_flipState;
    };

    /**
     * @brief �׷��� ��� ������ Ŭ���� (QGraphicsItemGroup ���)
     *
     * GameBoard���� ����� �ð������� ǥ���ϱ� ���� Ŭ����
     */
    class BlockGraphicsItem : public QGraphicsItemGroup
    {
    public:
        explicit BlockGraphicsItem(const Block& block, qreal cellSize, QGraphicsItem* parent = nullptr);
        ~BlockGraphicsItem() = default;

        // ��� ������Ʈ
        void updateBlock(const Block& block);
        void updatePosition(const Position& boardPos, qreal cellSize);
        void updateColors(const QColor& fillColor, const QColor& borderColor);

        // �̸����� ���
        void setPreviewMode(bool preview);
        bool isPreviewMode() const { return m_isPreview; }

        // ��� ����
        const Block& getBlock() const { return m_block; }

        // ���콺 �̺�Ʈ (�巡�� �� ��ӿ�)
        void setDraggable(bool draggable);
        bool isDraggable() const { return m_isDraggable; }

    protected:
        // QGraphicsItem �������̵�
        QRectF boundingRect() const override;
        void paint(QPainter* painter, const QStyleOptionGraphicsItem* option, QWidget* widget) override;

        // ���콺 �̺�Ʈ
        void mousePressEvent(QGraphicsSceneMouseEvent* event) override;
        void mouseMoveEvent(QGraphicsSceneMouseEvent* event) override;
        void mouseReleaseEvent(QGraphicsSceneMouseEvent* event) override;

    private:
        void rebuildGraphics();
        void clearGraphics();

        Block m_block;
        qreal m_cellSize;
        bool m_isPreview;
        bool m_isDraggable;

        // �׷��� ��ҵ�
        std::vector<QGraphicsRectItem*> m_cells;
        QColor m_fillColor;
        QColor m_borderColor;
    };

    /**
     * @brief ��� ���丮 Ŭ����
     *
     * �پ��� ����� �����ϰ� �����ϴ� ��ƿ��Ƽ Ŭ����
     */
    class BlockFactory
    {
    public:
        // ��� ����
        static Block createBlock(BlockType type, PlayerColor player = PlayerColor::Blue);
        static std::vector<Block> createPlayerSet(PlayerColor player);
        static std::vector<Block> createAllBlocks();

        // ��� ����
        static QString getBlockName(BlockType type);
        static QString getBlockDescription(BlockType type);
        static int getBlockScore(BlockType type); // ����� ���� ��

        // ��� ����
        static bool isValidBlockType(BlockType type);
        static std::vector<BlockType> getAllBlockTypes();
    };

} // namespace Blokus