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
#include <QTimer>              // 추가된 헤더
#include <QDebug>              // 추가된 헤더
#include <QApplication>        // 추가된 헤더
#include <map>
#include <set>

#include "common/Types.h"
#include "game/Block.h"

namespace Blokus {

    // 블록 버튼 클래스 - 클릭 가능한 블록 표시
    class BlockButton : public QWidget
    {
        Q_OBJECT

    public:
        explicit BlockButton(const Block& block, qreal blockSize = 20.0, QWidget* parent = nullptr);

        void setSelected(bool selected);
        void setUsed(bool used);
        void setEnabled(bool enabled); // override 제거됨

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

    // 방향별 팔레트 패널 클래스
    class DirectionPalette : public QWidget
    {
        Q_OBJECT

    public:
        enum class Direction {
            North,  // 상단 (작은 크기)
            South,  // 하단 (큰 크기, 자신의 블록)
            East,   // 우측 (작은 크기)
            West    // 좌측 (작은 크기)
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

    // 메인 개선된 게임 팔레트 클래스
    class ImprovedGamePalette : public QWidget
    {
        Q_OBJECT

    public:
        explicit ImprovedGamePalette(QWidget* parent = nullptr);

        // 팔레트 접근자
        DirectionPalette* getNorthPalette() const { return m_northPalette; }
        DirectionPalette* getSouthPalette() const { return m_southPalette; }
        DirectionPalette* getEastPalette() const { return m_eastPalette; }
        DirectionPalette* getWestPalette() const { return m_westPalette; }

        // 게임 상태 관리
        void setCurrentPlayer(PlayerColor player);
        void removeBlock(PlayerColor player, BlockType blockType);
        void resetAllPlayerBlocks();

        // 블록 선택 관리
        Block getSelectedBlock() const;
        void setSelectedBlock(const Block& block);
        void clearSelection();

    signals:
        void blockSelected(const Block& block);

    private slots:
        void onDirectionBlockSelected(const Block& block);

    private:
        void setupPalettes();
        void setupFixedPlayerAssignments();  // 새로 추가
        void updatePlayerAssignments();      // 사용 안함 (호환성 유지)
        void updateBlockAvailability();

        PlayerColor m_currentPlayer;
        PlayerColor m_fixedPlayer;           // 새로 추가 (항상 파랑)
        Block m_selectedBlock;
        bool m_hasSelection;

        // 4방향 팔레트
        DirectionPalette* m_northPalette;    // 항상 노랑
        DirectionPalette* m_southPalette;    // 항상 파랑 (나)
        DirectionPalette* m_eastPalette;     // 항상 빨강
        DirectionPalette* m_westPalette;     // 항상 초록

        // 제거된 블록 추적
        std::map<PlayerColor, std::set<BlockType>> m_removedBlocks;
    };

} // namespace Blokus