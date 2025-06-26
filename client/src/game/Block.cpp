#include "game/Block.h"
#include <QDebug>
#include <QGraphicsScene>
#include <QGraphicsSceneMouseEvent>
#include <QPainter>
#include <algorithm>
#include <cmath>

namespace Blokus {

    // 정적 블록 모양 정의 (기준점 (0,0)에서 상대 좌표)
    const std::map<BlockType, PositionList> Block::s_blockShapes = {
        // 1칸 블록
        { BlockType::Single, { {0, 0} } },

        // 2칸 블록
        { BlockType::Domino, { {0, 0}, {0, 1} } },

        // 3칸 블록
        { BlockType::TrioLine, { {0, 0}, {0, 1}, {0, 2} } },
        { BlockType::TrioAngle, { {0, 0}, {0, 1}, {1, 1} } },

        // 4칸 블록 (테트로미노)
        { BlockType::Tetro_I, { {0, 0}, {0, 1}, {0, 2}, {0, 3} } },
        { BlockType::Tetro_O, { {0, 0}, {0, 1}, {1, 0}, {1, 1} } },
        { BlockType::Tetro_T, { {0, 0}, {0, 1}, {0, 2}, {1, 1} } },
        { BlockType::Tetro_L, { {0, 0}, {0, 1}, {0, 2}, {1, 0} } },
        { BlockType::Tetro_S, { {0, 0}, {0, 1}, {1, 1}, {1, 2} } },

        // 5칸 블록 (펜토미노)
        { BlockType::Pento_F, { {0, 1}, {0, 2}, {1, 0}, {1, 1}, {2, 1} } },
        { BlockType::Pento_I, { {0, 0}, {0, 1}, {0, 2}, {0, 3}, {0, 4} } },
        { BlockType::Pento_L, { {0, 0}, {0, 1}, {0, 2}, {0, 3}, {1, 0} } },
        { BlockType::Pento_N, { {0, 0}, {0, 1}, {0, 2}, {1, 2}, {1, 3} } },
        { BlockType::Pento_P, { {0, 0}, {0, 1}, {1, 0}, {1, 1}, {2, 0} } },
        { BlockType::Pento_T, { {0, 0}, {0, 1}, {0, 2}, {1, 1}, {2, 1} } },
        { BlockType::Pento_U, { {0, 0}, {0, 2}, {1, 0}, {1, 1}, {1, 2} } },
        { BlockType::Pento_V, { {0, 0}, {1, 0}, {2, 0}, {2, 1}, {2, 2} } },
        { BlockType::Pento_W, { {0, 0}, {1, 0}, {1, 1}, {2, 1}, {2, 2} } },
        { BlockType::Pento_X, { {0, 1}, {1, 0}, {1, 1}, {1, 2}, {2, 1} } },
        { BlockType::Pento_Y, { {0, 0}, {0, 1}, {0, 2}, {0, 3}, {1, 1} } },
        { BlockType::Pento_Z, { {0, 0}, {0, 1}, {1, 1}, {2, 1}, {2, 2} } }
    };

    Block::Block(BlockType type, PlayerColor player)
        : m_type(type)
        , m_player(player)
        , m_rotation(Rotation::Degree_0)
        , m_flipState(FlipState::Normal)
    {
        if (s_blockShapes.find(type) == s_blockShapes.end()) {
            qWarning() << "잘못된 블록 타입:" << static_cast<int>(type);
            m_type = BlockType::Single;
        }
    }

    void Block::setRotation(Rotation rotation)
    {
        m_rotation = rotation;
    }

    void Block::setFlipState(FlipState flip)
    {
        m_flipState = flip;
    }

    void Block::rotateClockwise()
    {
        int currentRotation = static_cast<int>(m_rotation);
        m_rotation = static_cast<Rotation>((currentRotation + 1) % 4);
    }

    void Block::rotateCounterclockwise()
    {
        int currentRotation = static_cast<int>(m_rotation);
        m_rotation = static_cast<Rotation>((currentRotation + 3) % 4);
    }

    void Block::flipHorizontal()
    {
        switch (m_flipState) {
        case FlipState::Normal:
            m_flipState = FlipState::Horizontal;
            break;
        case FlipState::Horizontal:
            m_flipState = FlipState::Normal;
            break;
        case FlipState::Vertical:
            m_flipState = FlipState::Both;
            break;
        case FlipState::Both:
            m_flipState = FlipState::Vertical;
            break;
        }
    }

    void Block::flipVertical()
    {
        switch (m_flipState) {
        case FlipState::Normal:
            m_flipState = FlipState::Vertical;
            break;
        case FlipState::Vertical:
            m_flipState = FlipState::Normal;
            break;
        case FlipState::Horizontal:
            m_flipState = FlipState::Both;
            break;
        case FlipState::Both:
            m_flipState = FlipState::Horizontal;
            break;
        }
    }

    void Block::resetTransform()
    {
        m_rotation = Rotation::Degree_0;
        m_flipState = FlipState::Normal;
    }

    PositionList Block::getCurrentShape() const
    {
        auto it = s_blockShapes.find(m_type);
        if (it == s_blockShapes.end()) {
            return { {0, 0} };
        }

        PositionList shape = it->second;

        // 뒤집기 적용
        shape = applyFlip(shape, m_flipState);

        // 회전 적용
        shape = applyRotation(shape, m_rotation);

        // 정규화 (최소 좌표를 (0,0)으로)
        shape = normalizeShape(shape);

        return shape;
    }

    PositionList Block::getAbsolutePositions(const Position& basePos) const
    {
        PositionList currentShape = getCurrentShape();
        PositionList absolutePositions;

        for (const auto& relativePos : currentShape) {
            Position absolutePos = {
                basePos.first + relativePos.first,
                basePos.second + relativePos.second
            };
            absolutePositions.push_back(absolutePos);
        }

        return absolutePositions;
    }

    QRect Block::getBoundingRect() const
    {
        PositionList shape = getCurrentShape();
        if (shape.empty()) {
            return QRect(0, 0, 1, 1);
        }

        int minRow = shape[0].first, maxRow = shape[0].first;
        int minCol = shape[0].second, maxCol = shape[0].second;

        for (const auto& pos : shape) {
            minRow = std::min(minRow, pos.first);
            maxRow = std::max(maxRow, pos.first);
            minCol = std::min(minCol, pos.second);
            maxCol = std::max(maxCol, pos.second);
        }

        return QRect(minCol, minRow, maxCol - minCol + 1, maxRow - minRow + 1);
    }

    int Block::getSize() const
    {
        return static_cast<int>(getCurrentShape().size());
    }

    bool Block::wouldCollideAt(const Position& basePos, const PositionList& occupiedCells) const
    {
        PositionList absolutePositions = getAbsolutePositions(basePos);

        for (const auto& blockPos : absolutePositions) {
            for (const auto& occupiedPos : occupiedCells) {
                if (blockPos == occupiedPos) {
                    return true;
                }
            }
        }

        return false;
    }

    bool Block::isValidPlacement(const Position& basePos, int boardSize) const
    {
        PositionList absolutePositions = getAbsolutePositions(basePos);

        for (const auto& pos : absolutePositions) {
            if (pos.first < 0 || pos.first >= boardSize ||
                pos.second < 0 || pos.second >= boardSize) {
                return false;
            }
        }

        return true;
    }

    PositionList Block::applyRotation(const PositionList& shape, Rotation rotation) const
    {
        PositionList rotatedShape;

        for (const auto& pos : shape) {
            Position newPos = pos;

            switch (rotation) {
            case Rotation::Degree_0:
                // 변화 없음
                break;
            case Rotation::Degree_90:
                // 90도 시계방향: (r, c) -> (c, -r)
                newPos = { pos.second, -pos.first };
                break;
            case Rotation::Degree_180:
                // 180도: (r, c) -> (-r, -c)
                newPos = { -pos.first, -pos.second };
                break;
            case Rotation::Degree_270:
                // 270도 시계방향: (r, c) -> (-c, r)
                newPos = { -pos.second, pos.first };
                break;
            }

            rotatedShape.push_back(newPos);
        }

        return rotatedShape;
    }

    PositionList Block::applyFlip(const PositionList& shape, FlipState flip) const
    {
        PositionList flippedShape;

        for (const auto& pos : shape) {
            Position newPos = pos;

            switch (flip) {
            case FlipState::Normal:
                // 변화 없음
                break;
            case FlipState::Horizontal:
                // 수평 뒤집기: (r, c) -> (r, -c)
                newPos = { pos.first, -pos.second };
                break;
            case FlipState::Vertical:
                // 수직 뒤집기: (r, c) -> (-r, c)
                newPos = { -pos.first, pos.second };
                break;
            case FlipState::Both:
                // 양쪽 뒤집기: (r, c) -> (-r, -c)
                newPos = { -pos.first, -pos.second };
                break;
            }

            flippedShape.push_back(newPos);
        }

        return flippedShape;
    }

    PositionList Block::normalizeShape(const PositionList& shape) const
    {
        if (shape.empty()) {
            return shape;
        }

        // 최소 좌표 찾기
        int minRow = shape[0].first;
        int minCol = shape[0].second;

        for (const auto& pos : shape) {
            minRow = std::min(minRow, pos.first);
            minCol = std::min(minCol, pos.second);
        }

        // 정규화된 형태로 변환
        PositionList normalizedShape;
        for (const auto& pos : shape) {
            normalizedShape.push_back({
                pos.first - minRow,
                pos.second - minCol
                });
        }

        return normalizedShape;
    }

    // ========================================
    // BlockGraphicsItem 구현
    // ========================================

    BlockGraphicsItem::BlockGraphicsItem(const Block& block, qreal cellSize, QGraphicsItem* parent)
        : QGraphicsItemGroup(parent)
        , m_block(block)
        , m_cellSize(cellSize)
        , m_isPreview(false)
        , m_isDraggable(false)
        , m_fillColor(Qt::blue)
        , m_borderColor(Qt::darkBlue)
    {
        rebuildGraphics();
    }

    void BlockGraphicsItem::updateBlock(const Block& block)
    {
        m_block = block;
        rebuildGraphics();
    }

    void BlockGraphicsItem::updatePosition(const Position& boardPos, qreal cellSize)
    {
        m_cellSize = cellSize;
        setPos(boardPos.second * cellSize, boardPos.first * cellSize);
    }

    void BlockGraphicsItem::updateColors(const QColor& fillColor, const QColor& borderColor)
    {
        m_fillColor = fillColor;
        m_borderColor = borderColor;
        rebuildGraphics();
    }

    void BlockGraphicsItem::setPreviewMode(bool preview)
    {
        m_isPreview = preview;
        setOpacity(preview ? 0.6 : 1.0);
        rebuildGraphics();
    }

    void BlockGraphicsItem::setDraggable(bool draggable)
    {
        m_isDraggable = draggable;
        setFlag(QGraphicsItem::ItemIsMovable, draggable);
        setFlag(QGraphicsItem::ItemIsSelectable, draggable);
    }

    QRectF BlockGraphicsItem::boundingRect() const
    {
        QRect blockRect = m_block.getBoundingRect();
        return QRectF(0, 0, blockRect.width() * m_cellSize, blockRect.height() * m_cellSize);
    }

    void BlockGraphicsItem::paint(QPainter* painter, const QStyleOptionGraphicsItem* option, QWidget* widget)
    {
        Q_UNUSED(painter)
            Q_UNUSED(option)
            Q_UNUSED(widget)
            // 자식 아이템들이 그리므로 여기서는 아무것도 하지 않음
    }

    void BlockGraphicsItem::mousePressEvent(QGraphicsSceneMouseEvent* event)
    {
        if (m_isDraggable) {
            QGraphicsItemGroup::mousePressEvent(event);
        }
    }

    void BlockGraphicsItem::mouseMoveEvent(QGraphicsSceneMouseEvent* event)
    {
        if (m_isDraggable) {
            QGraphicsItemGroup::mouseMoveEvent(event);
        }
    }

    void BlockGraphicsItem::mouseReleaseEvent(QGraphicsSceneMouseEvent* event)
    {
        if (m_isDraggable) {
            QGraphicsItemGroup::mouseReleaseEvent(event);
        }
    }

    void BlockGraphicsItem::rebuildGraphics()
    {
        clearGraphics();

        PositionList shape = m_block.getCurrentShape();
        QBrush fillBrush = m_isPreview ? QBrush(m_fillColor, Qt::Dense4Pattern) : QBrush(m_fillColor);
        QPen borderPen(m_borderColor, 2);

        for (const auto& pos : shape) {
            QGraphicsRectItem* cell = new QGraphicsRectItem(
                pos.second * m_cellSize,
                pos.first * m_cellSize,
                m_cellSize,
                m_cellSize
            );

            cell->setPen(borderPen);
            cell->setBrush(fillBrush);
            cell->setParentItem(this);

            m_cells.push_back(cell);
        }
    }

    void BlockGraphicsItem::clearGraphics()
    {
        for (auto* cell : m_cells) {
            removeFromGroup(cell);
            delete cell;
        }
        m_cells.clear();
    }

    // ========================================
    // BlockFactory 구현
    // ========================================

    Block BlockFactory::createBlock(BlockType type, PlayerColor player)
    {
        return Block(type, player);
    }

    std::vector<Block> BlockFactory::createPlayerSet(PlayerColor player)
    {
        std::vector<Block> playerBlocks;

        for (int i = 0; i <= static_cast<int>(BlockType::Pento_Z); ++i) {
            BlockType type = static_cast<BlockType>(i);
            playerBlocks.emplace_back(type, player);
        }

        return playerBlocks;
    }

    std::vector<Block> BlockFactory::createAllBlocks()
    {
        std::vector<Block> allBlocks;

        std::vector<PlayerColor> players = {
            PlayerColor::Blue, PlayerColor::Yellow,
            PlayerColor::Red, PlayerColor::Green
        };

        for (PlayerColor player : players) {
            auto playerBlocks = createPlayerSet(player);
            allBlocks.insert(allBlocks.end(), playerBlocks.begin(), playerBlocks.end());
        }

        return allBlocks;
    }

    QString BlockFactory::getBlockName(BlockType type)
    {
        static const std::map<BlockType, QString> blockNames = {
            { BlockType::Single, "단일" },
            { BlockType::Domino, "도미노" },
            { BlockType::TrioLine, "3일자" },
            { BlockType::TrioAngle, "3꺾임" },
            { BlockType::Tetro_I, "테트로 I" },
            { BlockType::Tetro_O, "테트로 O" },
            { BlockType::Tetro_T, "테트로 T" },
            { BlockType::Tetro_L, "테트로 L" },
            { BlockType::Tetro_S, "테트로 S" },
            { BlockType::Pento_F, "펜토 F" },
            { BlockType::Pento_I, "펜토 I" },
            { BlockType::Pento_L, "펜토 L" },
            { BlockType::Pento_N, "펜토 N" },
            { BlockType::Pento_P, "펜토 P" },
            { BlockType::Pento_T, "펜토 T" },
            { BlockType::Pento_U, "펜토 U" },
            { BlockType::Pento_V, "펜토 V" },
            { BlockType::Pento_W, "펜토 W" },
            { BlockType::Pento_X, "펜토 X" },
            { BlockType::Pento_Y, "펜토 Y" },
            { BlockType::Pento_Z, "펜토 Z" }
        };

        auto it = blockNames.find(type);
        return (it != blockNames.end()) ? it->second : "알 수 없음";
    }

    QString BlockFactory::getBlockDescription(BlockType type)
    {
        return QString("%1 (%2칸)").arg(getBlockName(type)).arg(getBlockScore(type));
    }

    int BlockFactory::getBlockScore(BlockType type)
    {
        // 블록의 점수는 차지하는 칸 수와 동일
        Block tempBlock(type);
        return tempBlock.getSize();
    }

    bool BlockFactory::isValidBlockType(BlockType type)
    {
        return static_cast<int>(type) >= 0 && static_cast<int>(type) <= static_cast<int>(BlockType::Pento_Z);
    }

    std::vector<BlockType> BlockFactory::getAllBlockTypes()
    {
        std::vector<BlockType> types;
        for (int i = 0; i <= static_cast<int>(BlockType::Pento_Z); ++i) {
            types.push_back(static_cast<BlockType>(i));
        }
        return types;
    }

} // namespace Blokus