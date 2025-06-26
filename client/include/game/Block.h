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
     * @brief 단일 블록(폴리오미노)을 정의하고 관리하는 클래스
     *
     * 주요 기능:
     * - 21가지 블록 타입별 모양 정의
     * - 회전 및 뒤집기 변환
     * - 블록 렌더링 및 시각화
     * - 충돌 검사용 좌표 계산
     */
    class Block
    {
    public:
        explicit Block(BlockType type = BlockType::Single, PlayerColor player = PlayerColor::Blue);
        ~Block() = default;

        // 블록 정보 접근
        BlockType getType() const { return m_type; }
        PlayerColor getPlayer() const { return m_player; }
        Rotation getRotation() const { return m_rotation; }
        FlipState getFlipState() const { return m_flipState; }

        // 블록 변환
        void setRotation(Rotation rotation);
        void setFlipState(FlipState flip);
        void setPlayer(PlayerColor player) { m_player = player; }

        // 회전/뒤집기 동작
        void rotateClockwise();
        void rotateCounterclockwise();
        void flipHorizontal();
        void flipVertical();
        void resetTransform();

        // 좌표 계산
        PositionList getCurrentShape() const;
        PositionList getAbsolutePositions(const Position& basePos) const;
        QRect getBoundingRect() const;
        int getSize() const; // 블록이 차지하는 셀 개수

        // 충돌 검사
        bool wouldCollideAt(const Position& basePos, const PositionList& occupiedCells) const;

        // 블록 검증
        bool isValidPlacement(const Position& basePos, int boardSize = BOARD_SIZE) const;

    private:
        // 기본 블록 모양 정의 (정적 데이터)
        static const std::map<BlockType, PositionList> s_blockShapes;

        // 변환 함수들
        PositionList applyRotation(const PositionList& shape, Rotation rotation) const;
        PositionList applyFlip(const PositionList& shape, FlipState flip) const;
        PositionList normalizeShape(const PositionList& shape) const;

        // 멤버 변수
        BlockType m_type;
        PlayerColor m_player;
        Rotation m_rotation;
        FlipState m_flipState;
    };

    /**
     * @brief 그래픽 블록 아이템 클래스 (QGraphicsItemGroup 기반)
     *
     * GameBoard에서 블록을 시각적으로 표현하기 위한 클래스
     */
    class BlockGraphicsItem : public QGraphicsItemGroup
    {
    public:
        explicit BlockGraphicsItem(const Block& block, qreal cellSize, QGraphicsItem* parent = nullptr);
        ~BlockGraphicsItem() = default;

        // 블록 업데이트
        void updateBlock(const Block& block);
        void updatePosition(const Position& boardPos, qreal cellSize);
        void updateColors(const QColor& fillColor, const QColor& borderColor);

        // 미리보기 모드
        void setPreviewMode(bool preview);
        bool isPreviewMode() const { return m_isPreview; }

        // 블록 정보
        const Block& getBlock() const { return m_block; }

        // 마우스 이벤트 (드래그 앤 드롭용)
        void setDraggable(bool draggable);
        bool isDraggable() const { return m_isDraggable; }

    protected:
        // QGraphicsItem 오버라이드
        QRectF boundingRect() const override;
        void paint(QPainter* painter, const QStyleOptionGraphicsItem* option, QWidget* widget) override;

        // 마우스 이벤트
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

        // 그래픽 요소들
        std::vector<QGraphicsRectItem*> m_cells;
        QColor m_fillColor;
        QColor m_borderColor;
    };

    /**
     * @brief 블록 팩토리 클래스
     *
     * 다양한 블록을 생성하고 관리하는 유틸리티 클래스
     */
    class BlockFactory
    {
    public:
        // 블록 생성
        static Block createBlock(BlockType type, PlayerColor player = PlayerColor::Blue);
        static std::vector<Block> createPlayerSet(PlayerColor player);
        static std::vector<Block> createAllBlocks();

        // 블록 정보
        static QString getBlockName(BlockType type);
        static QString getBlockDescription(BlockType type);
        static int getBlockScore(BlockType type); // 블록의 점수 값

        // 블록 검증
        static bool isValidBlockType(BlockType type);
        static std::vector<BlockType> getAllBlockTypes();
    };

} // namespace Blokus