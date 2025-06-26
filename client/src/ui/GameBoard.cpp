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

        // 호버 타이머 설정 (100ms 지연)
        m_hoverTimer->setSingleShot(true);
        m_hoverTimer->setInterval(100);

        // 마우스 추적 활성화
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

        // QGraphicsView 설정
        setDragMode(QGraphicsView::NoDrag);
        setHorizontalScrollBarPolicy(Qt::ScrollBarAsNeeded);
        setVerticalScrollBarPolicy(Qt::ScrollBarAsNeeded);
        setRenderHint(QPainter::Antialiasing, true);
        setViewportUpdateMode(QGraphicsView::FullViewportUpdate);

        // 씬 변경 시그널 연결
        connect(m_scene, &QGraphicsScene::changed, this, &GameBoard::onSceneChanged);
    }

    void GameBoard::setupStyles()
    {
        // 격자 펜 설정
        m_gridPen = QPen(QColor(200, 200, 200), 1, Qt::SolidLine);
        m_borderPen = QPen(QColor(100, 100, 100), 2, Qt::SolidLine);

        // 브러시 설정
        m_emptyBrush = QBrush(QColor(245, 245, 245));
        m_highlightBrush = QBrush(QColor(255, 255, 0, 100));

        // 플레이어 색상 설정
        m_playerColors[PlayerColor::Blue] = QColor(52, 152, 219);     // 파랑
        m_playerColors[PlayerColor::Yellow] = QColor(241, 196, 15);   // 노랑  
        m_playerColors[PlayerColor::Red] = QColor(231, 76, 60);       // 빨강
        m_playerColors[PlayerColor::Green] = QColor(46, 204, 113);    // 초록
        m_playerColors[PlayerColor::None] = QColor(245, 245, 245);    // 빈 칸
    }

    void GameBoard::initializeBoard()
    {
        clearBoard();

        // 보드 상태 초기화
        for (int row = 0; row < BOARD_SIZE; ++row) {
            for (int col = 0; col < BOARD_SIZE; ++col) {
                m_board[row][col] = PlayerColor::None;
            }
        }

        // 시각적 요소 생성
        drawGrid();
        drawStartingCorners();

        // 뷰 맞춤
        fitBoardToView();
    }

    void GameBoard::clearBoard()
    {
        if (!m_scene) return;

        // 기존 아이템들 제거
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

        // 보드 배경 사각형
        m_boardRect = m_scene->addRect(0, 0, totalSize, totalSize, m_borderPen, m_emptyBrush);

        // 격자 셀들 생성
        m_gridCells.reserve(BOARD_SIZE * BOARD_SIZE);

        for (int row = 0; row < BOARD_SIZE; ++row) {
            for (int col = 0; col < BOARD_SIZE; ++col) {
                const qreal x = col * m_cellSize;
                const qreal y = row * m_cellSize;

                QGraphicsRectItem* cell = m_scene->addRect(
                    x, y, m_cellSize, m_cellSize, m_gridPen, m_emptyBrush);

                // 셀에 좌표 정보 저장
                cell->setData(0, row);  // row
                cell->setData(1, col);  // col

                m_gridCells.push_back(cell);
            }
        }

        // 씬 크기 설정
        m_scene->setSceneRect(0, 0, totalSize, totalSize);
    }

    void GameBoard::drawStartingCorners()
    {
        // 각 플레이어의 시작 모서리 표시
        const std::vector<Position> corners = {
            {0, 0},                    // 파랑 (왼쪽 위)
            {0, BOARD_SIZE - 1},       // 노랑 (오른쪽 위)
            {BOARD_SIZE - 1, 0},       // 빨강 (왼쪽 아래)
            {BOARD_SIZE - 1, BOARD_SIZE - 1}  // 초록 (오른쪽 아래)
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

        highlight->setZValue(1);  // 격자 위에 표시
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

        // 경계 확인
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
                    // 호버 타이머 재시작
                    m_hoverTimer->stop();
                    m_hoverTimer->start();

                    // 즉시 호버 이벤트 발생
                    emit cellHovered(m_hoveredCell.first, m_hoveredCell.second);
                }
            }
        }

        QGraphicsView::mouseMoveEvent(event);
    }

    void GameBoard::wheelEvent(QWheelEvent* event)
    {
        // 확대/축소 기능
        const qreal scaleFactor = 1.15;
        const qreal currentScale = transform().m11();

        if (event->angleDelta().y() > 0) {
            // 확대
            if (currentScale < 3.0) {
                scale(scaleFactor, scaleFactor);
            }
        }
        else {
            // 축소
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

        // 뷰에 맞춰 씬 조정
        fitInView(m_boardRect, Qt::KeepAspectRatio);

        // 최소/최대 배율 제한
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
        // 씬 변경 시 필요한 업데이트 수행
        update();
    }

    // 블록 배치 관련 함수들 (기본 구현)
    bool GameBoard::canPlaceBlock(const BlockPlacement& placement) const
    {
        // TODO: 실제 블록 배치 규칙 검증 구현
        Q_UNUSED(placement)
            return true;
    }

    bool GameBoard::placeBlock(const BlockPlacement& placement)
    {
        // TODO: 실제 블록 배치 구현
        Q_UNUSED(placement)
            return false;
    }

    void GameBoard::removeBlock(const Position& position)
    {
        // TODO: 블록 제거 구현
        Q_UNUSED(position)
    }

    void GameBoard::showBlockPreview(const BlockPlacement& placement)
    {
        // TODO: 블록 배치 미리보기 구현
        Q_UNUSED(placement)
    }

    void GameBoard::hideBlockPreview()
    {
        // TODO: 미리보기 숨기기 구현
        for (auto* item : m_previewItems) {
            if (m_scene && item) {
                m_scene->removeItem(item);
                delete item;
            }
        }
        m_previewItems.clear();
    }

} // namespace Blokus