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
     * @brief ���� �������̳븦 ǥ���ϴ� ������ ����
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
     * @brief �÷��̾ ����ȭ�� ��� �ȷ�Ʈ
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
        void resetAllBlocks(); // ��� ����� ��� ���� ���·� ����
        Block getSelectedBlock() const;

    signals:
        void blockSelected(const Block& block);

    private slots:
        void onBlockClicked(const Block& block);

    private:
        void setupUI();
        void createBlockWidgets();
        QString getPlayerColorName(PlayerColor player) const; // ������ �Լ� �߰�

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
     * @brief ������ ���� ��� �ȷ�Ʈ (4���� ���̾ƿ�)
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
        void resetAllPlayerBlocks(); // ��� �÷��̾��� ��� ����

        // �� ���� �ȷ�Ʈ �����ڵ� (������ �Լ��� �߰�)
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

        // 4���� ��ġ
        CompactPlayerPalette* m_southPalette;  // �ڽ� (�ϴ�, ū ũ��)
        CompactPlayerPalette* m_eastPalette;   // ���� (����, ���� ũ��)
        CompactPlayerPalette* m_northPalette;  // ���� (���, ���� ũ��)
        CompactPlayerPalette* m_westPalette;   // ���� (����, ���� ũ��)

        std::map<PlayerColor, CompactPlayerPalette*> m_playerPalettes;
    };

} // namespace Blokus