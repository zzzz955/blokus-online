#include "ui/GameBoard.h"

#include <QGraphicsRectItem>
#include <QGraphicsTextItem>
#include <QApplication>
#include <QDebug>
#include <cmath>

namespace Blokus {

    GameBoard::GameBoard(QWidget* parent)
        : QGraphicsView(parent)
        , m_scene(nullptr)
        , m_boardRect(nullptr)
        , m_readOnly(false)
        , m_cellSize(DEFAULT_CELL_SIZE)
        , m_hoveredCell({ -1, -1 })
        , m_mousePressed(false)
        , m_hoverTimer(new QTimer(this))
    {
        setupScene();
        setupStyles();
        initializeBoard();

        // ȣ�� Ÿ�̸� ���� (100ms ����)
        m_hoverTimer->setSingleShot(true);
        m_hoverTimer->setInterval(100);

        // ���콺 ���� Ȱ��ȭ
        setMouseTracking(true);
    }

    GameBoard::~GameBoard()
    {
        clearBoard();
    }

    void GameBoard::setupScene()
    {
        m_scene = new QGraphicsScene(this);
        setScene(m_scene);

        // QGraphicsView ����
        setDragMode(QGraphicsView::NoDrag);
        setHorizontalScrollBarPolicy(Qt::ScrollBarAsNeeded);
        setVerticalScrollBarPolicy(Qt::ScrollBarAsNeeded);
        setRenderHint(QPainter::Antialiasing, true);
        setViewportUpdateMode(QGraphicsView::FullViewportUpdate);

        // �� ���� �ñ׳� ����
        connect(m_scene, &QGraphicsScene::changed, this, &GameBoard::onSceneChanged);
    }

    void GameBoard::setupStyles()
    {
        // ���� �� ����
        m_gridPen = QPen(QColor(200, 200, 200), 1, Qt::SolidLine);
        m_borderPen = QPen(QColor(100, 100, 100), 2, Qt::SolidLine);

        // �귯�� ����
        m_emptyBrush = QBrush(QColor(245, 245, 245));
        m_highlightBrush = QBrush(QColor(255, 255, 0, 100));

        // �÷��̾� ���� ����
        m_playerColors[PlayerColor::Blue] = QColor(52, 152, 219);     // �Ķ�
        m_playerColors[PlayerColor::Yellow] = QColor(241, 196, 15);   // ���  
        m_playerColors[PlayerColor::Red] = QColor(231, 76, 60);       // ����
        m_playerColors[PlayerColor::Green] = QColor(46, 204, 113);    // �ʷ�
        m_playerColors[PlayerColor::None] = QColor(245, 245, 245);    // �� ĭ
    }

    void GameBoard::initializeBoard()
    {
        clearBoard();

        // ���� ���� �ʱ�ȭ
        for (int row = 0; row < BOARD_SIZE; ++row) {
            for (int col = 0; col < BOARD_SIZE; ++col) {
                m_board[row][col] = PlayerColor::None;
            }
        }

        // �ð��� ��� ����
        drawGrid();
        drawStartingCorners();

        // �� ����
        fitBoardToView();
    }

    void GameBoard::clearBoard()
    {
        if (!m_scene) return;

        // ���� �����۵� ����
        m_scene->clear();
        m_gridCells.clear();
        m_highlights.clear();
        m_previewItems.clear();
        m_boardRect = nullptr;
    }

    void GameBoard::drawGrid()
    {
        if (!m_scene) return;

        const qreal totalSize = BOARD_SIZE * m_cellSize;

        // ���� ��� �簢��
        m_boardRect = m_scene->addRect(0, 0, totalSize, totalSize, m_borderPen, m_emptyBrush);

        // ���� ���� ����
        m_gridCells.reserve(BOARD_SIZE * BOARD_SIZE);

        for (int row = 0; row < BOARD_SIZE; ++row) {
            for (int col = 0; col < BOARD_SIZE; ++col) {
                const qreal x = col * m_cellSize;
                const qreal y = row * m_cellSize;

                QGraphicsRectItem* cell = m_scene->addRect(
                    x, y, m_cellSize, m_cellSize, m_gridPen, m_emptyBrush);

                // ���� ��ǥ ���� ����
                cell->setData(0, row);  // row
                cell->setData(1, col);  // col

                m_gridCells.push_back(cell);
            }
        }

        // �� ũ�� ����
        m_scene->setSceneRect(0, 0, totalSize, totalSize);
    }

    void GameBoard::drawStartingCorners()
    {
        // �� �÷��̾��� ���� �𼭸� ǥ��
        const std::vector<Position> corners = {
            {0, 0},                    // �Ķ� (���� ��)
            {0, BOARD_SIZE - 1},       // ��� (������ ��)
            {BOARD_SIZE - 1, 0},       // ���� (���� �Ʒ�)
            {BOARD_SIZE - 1, BOARD_SIZE - 1}  // �ʷ� (������ �Ʒ�)
        };

        const std::vector<PlayerColor> players = {
            PlayerColor::Blue, PlayerColor::Yellow,
            PlayerColor::Red, PlayerColor::Green
        };

        for (size_t i = 0; i < corners.size(); ++i) {
            const auto& corner = corners[i];
            const auto& color = players[i];

            highlightCell(corner.first, corner.second, getPlayerColor(color));
        }
    }

    bool GameBoard::isCellValid(int row, int col) const
    {
        return row >= 0 && row < BOARD_SIZE && col >= 0 && col < BOARD_SIZE;
    }

    bool GameBoard::isCellOccupied(int row, int col) const
    {
        if (!isCellValid(row, col)) return true;
        return m_board[row][col] != PlayerColor::None;
    }

    PlayerColor GameBoard::getCellOwner(int row, int col) const
    {
        if (!isCellValid(row, col)) return PlayerColor::None;
        return m_board[row][col];
    }

    void GameBoard::highlightCell(int row, int col, const QColor& color)
    {
        if (!isCellValid(row, col) || !m_scene) return;

        const qreal x = col * m_cellSize;
        const qreal y = row * m_cellSize;

        QBrush highlightBrush(color);
        highlightBrush.setStyle(Qt::SolidPattern);

        QGraphicsRectItem* highlight = m_scene->addRect(
            x, y, m_cellSize, m_cellSize, QPen(color.darker(), 2), highlightBrush);

        highlight->setZValue(1);  // ���� ���� ǥ��
        m_highlights.push_back(highlight);
    }

    void GameBoard::clearHighlights()
    {
        for (auto* highlight : m_highlights) {
            if (m_scene && highlight) {
                m_scene->removeItem(highlight);
                delete highlight;
            }
        }
        m_highlights.clear();
    }

    Position GameBoard::screenToBoard(const QPointF& screenPos) const
    {
        QPointF scenePos = mapToScene(screenPos.toPoint());

        int col = static_cast<int>(scenePos.x() / m_cellSize);
        int row = static_cast<int>(scenePos.y() / m_cellSize);

        // ��� Ȯ��
        if (col < 0 || col >= BOARD_SIZE || row < 0 || row >= BOARD_SIZE) {
            return { -1, -1 };
        }

        return { row, col };
    }

    QPointF GameBoard::boardToScreen(const Position& boardPos) const
    {
        if (!isCellValid(boardPos.first, boardPos.second)) {
            return QPointF(-1, -1);
        }

        qreal sceneX = boardPos.second * m_cellSize + m_cellSize / 2;
        qreal sceneY = boardPos.first * m_cellSize + m_cellSize / 2;

        return mapFromScene(sceneX, sceneY);
    }

    void GameBoard::mousePressEvent(QMouseEvent* event)
    {
        if (m_readOnly) {
            QGraphicsView::mousePressEvent(event);
            return;
        }

        m_mousePressed = true;

        if (event->button() == Qt::LeftButton) {
            Position boardPos = screenToBoard(event->pos());

            if (isCellValid(boardPos.first, boardPos.second)) {
                emit cellClicked(boardPos.first, boardPos.second);
            }
        }

        QGraphicsView::mousePressEvent(event);
    }

    void GameBoard::mouseMoveEvent(QMouseEvent* event)
    {
        if (!m_readOnly) {
            Position newHover = screenToBoard(event->pos());

            if (newHover != m_hoveredCell) {
                m_hoveredCell = newHover;

                if (isCellValid(m_hoveredCell.first, m_hoveredCell.second)) {
                    // ȣ�� Ÿ�̸� �����
                    m_hoverTimer->stop();
                    m_hoverTimer->start();

                    // ��� ȣ�� �̺�Ʈ �߻�
                    emit cellHovered(m_hoveredCell.first, m_hoveredCell.second);
                }
            }
        }

        QGraphicsView::mouseMoveEvent(event);
    }

    void GameBoard::wheelEvent(QWheelEvent* event)
    {
        // Ȯ��/��� ���
        const qreal scaleFactor = 1.15;
        const qreal currentScale = transform().m11();

        if (event->angleDelta().y() > 0) {
            // Ȯ��
            if (currentScale < 3.0) {
                scale(scaleFactor, scaleFactor);
            }
        }
        else {
            // ���
            if (currentScale > 0.3) {
                scale(1.0 / scaleFactor, 1.0 / scaleFactor);
            }
        }

        event->accept();
    }

    void GameBoard::resizeEvent(QResizeEvent* event)
    {
        QGraphicsView::resizeEvent(event);
        fitBoardToView();
    }

    void GameBoard::leaveEvent(QEvent* event)
    {
        m_hoveredCell = { -1, -1 };
        m_hoverTimer->stop();
        QGraphicsView::leaveEvent(event);
    }

    void GameBoard::fitBoardToView()
    {
        if (!m_scene || !m_boardRect) return;

        // �信 ���� �� ����
        fitInView(m_boardRect, Qt::KeepAspectRatio);

        // �ּ�/�ִ� ���� ����
        const qreal currentScale = transform().m11();
        if (currentScale < 0.5) {
            resetTransform();
            scale(0.5, 0.5);
        }
        else if (currentScale > 2.0) {
            resetTransform();
            scale(2.0, 2.0);
        }
    }

    QColor GameBoard::getPlayerColor(PlayerColor player) const
    {
        auto it = m_playerColors.find(player);
        if (it != m_playerColors.end()) {
            return it->second;
        }
        return m_playerColors.at(PlayerColor::None);
    }

    void GameBoard::setBoardReadOnly(bool readOnly)
    {
        m_readOnly = readOnly;

        if (readOnly) {
            setCursor(Qt::ArrowCursor);
        }
        else {
            setCursor(Qt::CrossCursor);
        }
    }

    void GameBoard::resetBoard()
    {
        initializeBoard();
    }

    void GameBoard::onSceneChanged()
    {
        // �� ���� �� �ʿ��� ������Ʈ ����
        update();
    }

    // ��� ��ġ ���� �Լ��� (�⺻ ����)
    bool GameBoard::canPlaceBlock(const BlockPlacement& placement) const
    {
        // TODO: ���� ��� ��ġ ��Ģ ���� ����
        Q_UNUSED(placement)
            return true;
    }

    bool GameBoard::placeBlock(const BlockPlacement& placement)
    {
        // TODO: ���� ��� ��ġ ����
        Q_UNUSED(placement)
            return false;
    }

    void GameBoard::removeBlock(const Position& position)
    {
        // TODO: ��� ���� ����
        Q_UNUSED(position)
    }

    void GameBoard::showBlockPreview(const BlockPlacement& placement)
    {
        // TODO: ��� ��ġ �̸����� ����
        Q_UNUSED(placement)
    }

    void GameBoard::hideBlockPreview()
    {
        // TODO: �̸����� ����� ����
        for (auto* item : m_previewItems) {
            if (m_scene && item) {
                m_scene->removeItem(item);
                delete item;
            }
        }
        m_previewItems.clear();
    }

} // namespace Blokus