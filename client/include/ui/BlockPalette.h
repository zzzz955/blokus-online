#pragma once

#include <QWidget>
#include <QHBoxLayout>
#include <QVBoxLayout>
#include <QScrollArea>
#include <QLabel>
#include <QGraphicsView>
#include <QGraphicsScene>
#include <QGraphicsItem>
#include <QMouseEvent>
#include <QFrame>
#include <QPaintEvent>
#include <QResizeEvent>
#include <vector>
#include <map>

#include "common/Types.h"
#include "game/Block.h"

namespace Blokus {

    /**
     * @brief ���� ����� ǥ���ϰ� ���� ������ ����
     */
    class BlockItem : public QGraphicsView
    {
        Q_OBJECT

    public:
        explicit BlockItem(const Block& block, bool isOwned = true, QWidget* parent = nullptr);
        ~BlockItem() = default;

        const Block& getBlock() const { return m_block; }
        bool isOwned() const { return m_isOwned; }
        bool isSelected() const { return m_isSelected; }
        bool isUsed() const { return m_isUsed; }

        void setSelected(bool selected);
        void setUsed(bool used);
        void updateBlock(const Block& block);

    signals:
        void blockClicked(const Block& block);

    protected:
        void mousePressEvent(QMouseEvent* event) override;
        void paintEvent(QPaintEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;

    private:
        void setupGraphics();
        void updateSelection();

        Block m_block;
        QGraphicsScene* m_scene;
        BlockGraphicsItem* m_blockItem;
        bool m_isOwned;      // �ڽ��� ������� ���� �������
        bool m_isSelected;   // ���� ���õ� �������
        bool m_isUsed;       // �̹� ���� �������

        static constexpr qreal OWNED_CELL_SIZE = 15.0;     // �ڽ��� ��� ũ��
        static constexpr qreal OPPONENT_CELL_SIZE = 8.0;   // ���� ��� ũ��
    };

    /**
     * @brief �÷��̾ ��� �ȷ�Ʈ (�� �÷��̾��� ��� ��� ǥ��)
     */
    class PlayerBlockPalette : public QWidget
    {
        Q_OBJECT

    public:
        explicit PlayerBlockPalette(PlayerColor player, bool isOwned = true, QWidget* parent = nullptr);
        ~PlayerBlockPalette() = default;

        PlayerColor getPlayer() const { return m_player; }
        bool isOwned() const { return m_isOwned; }

        void setSelectedBlock(BlockType blockType);
        void setBlockUsed(BlockType blockType, bool used = true);
        BlockType getSelectedBlockType() const { return m_selectedBlockType; }
        Block getSelectedBlock() const;

        // ��� ������ ��� ��� ��������
        std::vector<BlockType> getAvailableBlocks() const;

        void updateAvailableBlocks(const std::vector<BlockType>& usedBlocks);

    signals:
        void blockSelected(const Block& block);

    private slots:
        void onBlockClicked(const Block& block);

    private:
        void setupUI();
        void createBlockItems();
        void updatePlayerLabel();

        PlayerColor m_player;
        bool m_isOwned;
        BlockType m_selectedBlockType;

        QVBoxLayout* m_mainLayout;
        QLabel* m_playerLabel;
        QScrollArea* m_scrollArea;
        QWidget* m_blocksContainer;
        QHBoxLayout* m_blocksLayout;

        std::map<BlockType, BlockItem*> m_blockItems;
    };

    /**
     * @brief ��ü ������ ��� �ȷ�Ʈ (��� �÷��̾��� ��� ǥ��)
     */
    class GameBlockPalette : public QWidget
    {
        Q_OBJECT

    public:
        explicit GameBlockPalette(QWidget* parent = nullptr);
        ~GameBlockPalette() = default;

        void setCurrentPlayer(PlayerColor player);
        PlayerColor getCurrentPlayer() const { return m_currentPlayer; }

        Block getSelectedBlock() const;
        void setBlockUsed(PlayerColor player, BlockType blockType);

        // ���� ���� ������Ʈ
        void updateGameState(const std::map<PlayerColor, std::vector<BlockType>>& usedBlocks);

        // �÷��̾ ��� ������ ��� �� ��ȯ
        int getAvailableBlockCount(PlayerColor player) const;

    signals:
        void blockSelected(const Block& block);
        void playerChanged(PlayerColor newPlayer);

    private slots:
        void onPlayerBlockSelected(const Block& block);

    private:
        void setupUI();
        void createPlayerPalettes();
        void updateCurrentPlayerHighlight();

        PlayerColor m_currentPlayer;

        QVBoxLayout* m_mainLayout;
        QLabel* m_titleLabel;

        std::map<PlayerColor, PlayerBlockPalette*> m_playerPalettes;
    };

} // namespace Blokus