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
    // Qt 전용 BlockFactory 유틸리티 함수들 (간소화)
    // ========================================

    namespace QtBlockUtils {
        // Qt 문자열 변환
        inline QString getBlockNameQt(BlockType type) {
            return QString::fromUtf8(Common::BlockFactory::getBlockName(type).c_str());
        }

        inline QString getBlockDescriptionQt(BlockType type) {
            return QString::fromUtf8(Common::BlockFactory::getBlockDescription(type).c_str());
        }

        // Qt 색상 변환
        inline QColor getPlayerColorQt(PlayerColor player) {
            return Utils::getPlayerColor(player);
        }

        // QRect 변환
        inline QRect getBoundingRectQt(const Block& block) {
            auto rect = block.getBoundingRect();
            return QRect(rect.left, rect.top, rect.width, rect.height);
        }
    }

    // ========================================
    // 기존 코드 호환성을 위한 BlockFactory alias (간소화)
    // ========================================

    namespace BlockFactory {
        // Qt 전용 함수들
        inline QString getBlockName(BlockType type) {
            return QtBlockUtils::getBlockNameQt(type);
        }

        inline QString getBlockDescription(BlockType type) {
            return QtBlockUtils::getBlockDescriptionQt(type);
        }

        // Common::BlockFactory 함수들을 명시적으로 forwarding
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