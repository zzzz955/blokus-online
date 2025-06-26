#pragma once

#include <QWidget>
#include <QScrollArea>
#include <QGridLayout>
#include <QLabel>
#include <QPushButton>
#include <QGraphicsView>
#include <QGraphicsScene>
#include <QGraphicsItem>
#include <QPropertyAnimation>
#include <QGraphicsOpacityEffect>
#include <QTimer>              // �߰��� ���
#include <QDebug>              // �߰��� ���
#include <QApplication>        // �߰��� ���
#include <map>
#include <set>

#include "common/Types.h"
#include "game/Block.h"

namespace Blokus {

    // ��� ��ư Ŭ���� - Ŭ�� ������ ��� ǥ��
    class BlockButton : public QWidget
    {
        Q_OBJECT

    public:
        explicit BlockButton(const Block& block, qreal blockSize = 20.0, QWidget* parent = nullptr);

        void setSelected(bool selected);
        void setUsed(bool used);
        void setEnabled(bool enabled); // override ���ŵ�

        Block getBlock() const { return m_block; }
        BlockType getBlockType() const { return m_block.getType(); }

        void updateBlockState(const Block& newBlock);

    signals:
        void blockClicked(const Block& block);

    protected:
        void paintEvent(QPaintEvent* event) override;
        void mousePressEvent(QMouseEvent* event) override;
        void enterEvent(QEvent* event) override;
        void leaveEvent(QEvent* event) override;

    private:
        void setupGraphics();
        QColor getPlayerColor() const;

        Block m_block;
        qreal m_blockSize;
        bool m_isSelected;
        bool m_isUsed;
        bool m_isHovered;
        QGraphicsScene* m_scene;
        BlockGraphicsItem* m_blockItem;
    };

    // ���⺰ �ȷ�Ʈ �г� Ŭ����
    class DirectionPalette : public QWidget
    {
        Q_OBJECT

    public:
        enum class Direction {
            North,  // ��� (���� ũ��)
            South,  // �ϴ� (ū ũ��, �ڽ��� ���)
            East,   // ���� (���� ũ��)
            West    // ���� (���� ũ��)
        };

        explicit DirectionPalette(Direction direction, QWidget* parent = nullptr);

        void setPlayer(PlayerColor player);
        void setBlocks(const std::vector<Block>& blocks);
        void setBlockUsed(BlockType blockType, bool used);
        void removeBlock(BlockType blockType);
        void resetAllBlocks();
        void highlightBlock(BlockType blockType, bool highlight);

        Direction getDirection() const { return m_direction; }
        PlayerColor getPlayer() const { return m_player; }

    signals:
        void blockSelected(const Block& block);

    private slots:
        void onBlockButtonClicked(const Block& block);

    private:
        void setupLayout();
        void updateBlockButtons();
        void forceLayoutUpdate();
        void reorganizeLayout();
        qreal getBlockSize() const;
        int getMaxBlocksPerRow() const;
        QString getDirectionName() const;

        Direction m_direction;
        PlayerColor m_player;
        QScrollArea* m_scrollArea;
        QWidget* m_blockContainer;
        QGridLayout* m_blockLayout;
        std::vector<Block> m_blocks;
        std::map<BlockType, BlockButton*> m_blockButtons;
        std::set<BlockType> m_usedBlocks;
        BlockType m_selectedBlockType;
    };

    // ���� ������ ���� �ȷ�Ʈ Ŭ����
    class ImprovedGamePalette : public QWidget
    {
        Q_OBJECT

    public:
        explicit ImprovedGamePalette(QWidget* parent = nullptr);

        // �ȷ�Ʈ ������
        DirectionPalette* getNorthPalette() const { return m_northPalette; }
        DirectionPalette* getSouthPalette() const { return m_southPalette; }
        DirectionPalette* getEastPalette() const { return m_eastPalette; }
        DirectionPalette* getWestPalette() const { return m_westPalette; }

        // ���� ���� ����
        void setCurrentPlayer(PlayerColor player);
        void removeBlock(PlayerColor player, BlockType blockType);
        void resetAllPlayerBlocks();

        // ��� ���� ����
        Block getSelectedBlock() const;
        void setSelectedBlock(const Block& block);
        void clearSelection();

    signals:
        void blockSelected(const Block& block);

    private slots:
        void onDirectionBlockSelected(const Block& block);

    private:
        void setupPalettes();
        void setupFixedPlayerAssignments();  // ���� �߰�
        void updatePlayerAssignments();      // ��� ���� (ȣȯ�� ����)
        void updateBlockAvailability();

        PlayerColor m_currentPlayer;
        PlayerColor m_fixedPlayer;           // ���� �߰� (�׻� �Ķ�)
        Block m_selectedBlock;
        bool m_hasSelection;

        // 4���� �ȷ�Ʈ
        DirectionPalette* m_northPalette;    // �׻� ���
        DirectionPalette* m_southPalette;    // �׻� �Ķ� (��)
        DirectionPalette* m_eastPalette;     // �׻� ����
        DirectionPalette* m_westPalette;     // �׻� �ʷ�

        // ���ŵ� ��� ����
        std::map<PlayerColor, std::set<BlockType>> m_removedBlocks;
    };

} // namespace Blokus