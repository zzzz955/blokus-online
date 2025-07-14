#include "ClientBlock.h"
#include <QDebug>
#include <QGraphicsScene>
#include <algorithm>
#include <cmath>

namespace Blokus {

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
        auto blockRect = m_block.getBoundingRect();
        return QRectF(0, 0, blockRect.width * m_cellSize, blockRect.height * m_cellSize);
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

} // namespace Blokus