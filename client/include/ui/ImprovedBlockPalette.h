#pragma once

#include <QWidget>
#include <QHBoxLayout>
#include <QVBoxLayout>
#include <QScrollArea>
#include <QLabel>
#include <QFrame>
#include <vector>
#include <map>

#include "common/Types.h"
#include "game/Block.h"

namespace Blokus {

    /**
     * @brief 개별 폴리오미노를 표시하는 간단한 위젯
     */
    class PolyominoWidget : public QWidget
    {
        Q_OBJECT

    public:
        explicit PolyominoWidget(const Block& block, bool isOwned = true, QWidget* parent = nullptr);

        const Block& getBlock() const { return m_block; }
        bool isSelected() const { return m_isSelected; }
        bool isUsed() const { return m_isUsed; }

        void setSelected(bool selected);
        void setUsed(bool used);

    signals:
        void blockClicked(const Block& block);

    protected:
        void paintEvent(QPaintEvent* event) override;
        void mousePressEvent(QMouseEvent* event) override;
        QSize sizeHint() const override;

    private:
        void calculateSize();

        Block m_block;
        bool m_isOwned;
        bool m_isSelected;
        bool m_isUsed;

        qreal m_cellSize;
        QSize m_widgetSize;
    };

    /**
     * @brief 플레이어별 간소화된 블록 팔레트
     */
    class CompactPlayerPalette : public QWidget
    {
        Q_OBJECT

    public:
        explicit CompactPlayerPalette(PlayerColor player, bool isOwned = true,
            Qt::Orientation orientation = Qt::Horizontal,
            QWidget* parent = nullptr);

        PlayerColor getPlayer() const { return m_player; }
        void setSelectedBlock(BlockType blockType);
        void setBlockUsed(BlockType blockType, bool used = true);
        void resetAllBlocks(); // 모든 블록을 사용 가능 상태로 리셋
        Block getSelectedBlock() const;

    signals:
        void blockSelected(const Block& block);

    private slots:
        void onBlockClicked(const Block& block);

    private:
        void setupUI();
        void createBlockWidgets();
        QString getPlayerColorName(PlayerColor player) const; // 누락된 함수 추가

        PlayerColor m_player;
        bool m_isOwned;
        Qt::Orientation m_orientation;
        BlockType m_selectedBlockType;

        QScrollArea* m_scrollArea;
        QWidget* m_container;
        QBoxLayout* m_layout;
        QLabel* m_playerLabel;

        std::map<BlockType, PolyominoWidget*> m_blockWidgets;
    };

    /**
     * @brief 개선된 게임 블록 팔레트 (4방향 레이아웃)
     */
    class ImprovedGamePalette : public QWidget
    {
        Q_OBJECT

    public:
        explicit ImprovedGamePalette(QWidget* parent = nullptr);

        void setCurrentPlayer(PlayerColor player);
        PlayerColor getCurrentPlayer() const { return m_currentPlayer; }
        Block getSelectedBlock() const;
        void setBlockUsed(PlayerColor player, BlockType blockType);
        void resetAllPlayerBlocks(); // 모든 플레이어의 블록 리셋

        // 각 방향 팔레트 접근자들 (누락된 함수들 추가)
        CompactPlayerPalette* getSouthPalette() const;
        CompactPlayerPalette* getEastPalette() const;
        CompactPlayerPalette* getNorthPalette() const;
        CompactPlayerPalette* getWestPalette() const;

    signals:
        void blockSelected(const Block& block);

    private slots:
        void onPlayerBlockSelected(const Block& block);

    private:
        void setupUI();
        void createPlayerPalettes();
        void updateCurrentPlayerHighlight();

        PlayerColor m_currentPlayer;

        // 4방향 배치
        CompactPlayerPalette* m_southPalette;  // 자신 (하단, 큰 크기)
        CompactPlayerPalette* m_eastPalette;   // 상대방 (우측, 작은 크기)
        CompactPlayerPalette* m_northPalette;  // 상대방 (상단, 작은 크기)
        CompactPlayerPalette* m_westPalette;   // 상대방 (좌측, 작은 크기)

        std::map<PlayerColor, CompactPlayerPalette*> m_playerPalettes;
    };

} // namespace Blokus