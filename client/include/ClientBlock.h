#pragma once

#include "Block.h"
#include "ClientTypes.h"

// Qt 관련 헤더들 (그래픽 아이템용)
#include <QGraphicsItemGroup>
#include <QGraphicsRectItem>
#include <QGraphicsSceneMouseEvent>
#include <QPainter>
#include <QPen>
#include <QBrush>
#include <QColor>
#include <QRect>
#include <QString>

namespace Blokus {

    // ========================================
    // Common 라이브러리 타입들을 기본 네임스페이스로 가져오기
    // ========================================

    using Block = Common::Block;

    // ========================================
    // Qt 전용 블록 그래픽 아이템
    // ========================================

    class BlockGraphicsItem : public QGraphicsItemGroup
    {
    public:
        explicit BlockGraphicsItem(const Block& block, qreal cellSize, QGraphicsItem* parent = nullptr);

        // 업데이트 함수들
        void updateBlock(const Block& block);
        void updatePosition(const Position& boardPos, qreal cellSize);
        void updateColors(const QColor& fillColor, const QColor& borderColor);

        // 표시 모드
        void setPreviewMode(bool preview);
        void setDraggable(bool draggable);

        // 블록 정보 접근
        const Block& getBlock() const { return m_block; }

        // QGraphicsItem 인터페이스
        QRectF boundingRect() const override;
        void paint(QPainter* painter, const QStyleOptionGraphicsItem* option, QWidget* widget = nullptr) override;

    protected:
        void mousePressEvent(QGraphicsSceneMouseEvent* event) override;
        void mouseMoveEvent(QGraphicsSceneMouseEvent* event) override;
        void mouseReleaseEvent(QGraphicsSceneMouseEvent* event) override;

    private:
        void rebuildGraphics();
        void clearGraphics();

    private:
        Block m_block;
        qreal m_cellSize;
        bool m_isPreview;
        bool m_isDraggable;
        QColor m_fillColor;
        QColor m_borderColor;

        std::vector<QGraphicsRectItem*> m_cells;
    };

    // ========================================
    // Qt 전용 BlockFactory 확장
    // ========================================

    class QtBlockFactory : public Common::BlockFactory
    {
    public:
        // Qt 전용 편의 함수들
        static QString getBlockNameQt(BlockType type) {
            return QString::fromUtf8(Common::BlockFactory::getBlockName(type).c_str());
        }

        static QString getBlockDescriptionQt(BlockType type) {
            return QString::fromUtf8(Common::BlockFactory::getBlockDescription(type).c_str());
        }

        // Qt 색상 변환
        static QColor getPlayerColorQt(PlayerColor player) {
            return Utils::getPlayerColor(player);
        }

        // QRect 변환
        static QRect getBoundingRectQt(const Block& block) {
            auto rect = block.getBoundingRect();
            return QRect(rect.left, rect.top, rect.width, rect.height);
        }

        // 기존 함수들과의 호환성을 위한 alias
        static QString getBlockName(BlockType type) {
            return getBlockNameQt(type);
        }

        static QString getBlockDescription(BlockType type) {
            return getBlockDescriptionQt(type);
        }

        static int getBlockScore(BlockType type) {
            return Common::BlockFactory::getBlockScore(type);
        }

        static bool isValidBlockType(BlockType type) {
            return Common::BlockFactory::isValidBlockType(type);
        }

        static std::vector<BlockType> getAllBlockTypes() {
            return Common::BlockFactory::getAllBlockTypes();
        }
    };

    // ========================================
    // 기존 코드 호환성을 위한 alias (클라이언트에서만 사용)
    // ========================================

    // 기존 코드에서 BlockFactory::getBlockName()을 사용하던 곳들을 위해
    namespace BlockFactory {
        inline QString getBlockName(BlockType type) {
            return QtBlockFactory::getBlockNameQt(type);
        }

        inline QString getBlockDescription(BlockType type) {
            return QtBlockFactory::getBlockDescriptionQt(type);
        }

        inline int getBlockScore(BlockType type) {
            return Common::BlockFactory::getBlockScore(type);
        }

        inline bool isValidBlockType(BlockType type) {
            return Common::BlockFactory::isValidBlockType(type);
        }

        inline std::vector<BlockType> getAllBlockTypes() {
            return Common::BlockFactory::getAllBlockTypes();
        }

        inline Block createBlock(BlockType type, PlayerColor player = PlayerColor::None) {
            return Common::BlockFactory::createBlock(type, player);
        }

        inline std::vector<Block> createPlayerSet(PlayerColor player) {
            return Common::BlockFactory::createPlayerSet(player);
        }

        inline std::vector<Block> createAllBlocks() {
            return Common::BlockFactory::createAllBlocks();
        }
    }

} // namespace Blokus