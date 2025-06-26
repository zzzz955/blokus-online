#include "ui/GameBoard.h"

#include <QGraphicsRectItem>
#include <QGraphicsTextItem>
#include <QApplication>
#include <QDebug>
#include <QKeyEvent>
#include <cmath>
#include <random>

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
        , m_currentPreview(nullptr)
        , m_selectedBlock(BlockType::Single, PlayerColor::Blue)
        , m_testBlockIndex(0)
        , m_gameLogic(nullptr)
        , m_hasSelectedBlock(false)  // 선택 상태 추가
    {
        setupScene();
        setupStyles();
        initializeBoard();

        // 호버 타이머 설정 (100ms 지연)
        m_hoverTimer->setSingleShot(true);
        m_hoverTimer->setInterval(100);

        // 마우스 추적 활성화
        setMouseTracking(true);

        // 키보드 포커스 활성화 (블록 회전용)
        setFocusPolicy(Qt::StrongFocus);

        qDebug() << QString::fromUtf8("GameBoard 초기화 완료 - 블록 렌더링 시스템 활성화");
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
        // drawStartingCorners(); // 시작 모서리 하이라이트 제거

        // 뷰 맞춤
        fitBoardToView();

        qDebug() << QString::fromUtf8("보드 초기화 완료");
    }

    void GameBoard::clearBoard()
    {
        if (!m_scene) return;

        // 기존 아이템들 제거
        clearAllBlocks();
        m_scene->clear();
        m_gridCells.clear();
        m_highlights.clear();
        m_previewItems.clear();
        m_boardRect = nullptr;
        m_currentPreview = nullptr;
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
                cell->setData(0, row);  // 행
                cell->setData(1, col);  // 열

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

    // ========================================
    // 블록 렌더링 관련 함수들
    // ========================================

    void GameBoard::addBlockToBoard(const Block& block, const Position& position)
    {
        if (!isValidBlockPlacement(block, position)) {
            qWarning() << QString::fromUtf8("잘못된 블록 배치 위치: (%1, %2)")
                .arg(position.first).arg(position.second);
            return;
        }

        // 그래픽 아이템 생성
        BlockGraphicsItem* blockItem = createBlockGraphicsItem(block, position);
        if (!blockItem) return;

        // 보드 상태 업데이트
        PositionList absolutePositions = block.getAbsolutePositions(position);
        for (const auto& pos : absolutePositions) {
            if (isCellValid(pos.first, pos.second)) {
                m_board[pos.first][pos.second] = block.getPlayer();
            }
        }

        // 블록 리스트에 추가
        m_blockItems.push_back(blockItem);
        m_blockMap[position] = blockItem;

        QString debugMsg = QString::fromUtf8("블록 추가됨: %1 위치: (%2, %3)")
            .arg(BlockFactory::getBlockName(block.getType()))
            .arg(position.first)
            .arg(position.second);
        qDebug() << debugMsg;
    }

    void GameBoard::removeBlockFromBoard(const Position& position)
    {
        auto it = m_blockMap.find(position);
        if (it == m_blockMap.end()) {
            qWarning() << QString::fromUtf8("제거할 블록을 찾을 수 없음: (%1, %2)")
                .arg(position.first).arg(position.second);
            return;
        }

        BlockGraphicsItem* blockItem = it->second;
        Block block = blockItem->getBlock();

        // 보드 상태에서 제거
        PositionList absolutePositions = block.getAbsolutePositions(position);
        for (const auto& pos : absolutePositions) {
            if (isCellValid(pos.first, pos.second)) {
                m_board[pos.first][pos.second] = PlayerColor::None;
            }
        }

        // 그래픽 아이템 제거
        m_scene->removeItem(blockItem);

        // 리스트에서 제거
        m_blockItems.erase(
            std::remove(m_blockItems.begin(), m_blockItems.end(), blockItem),
            m_blockItems.end()
        );
        m_blockMap.erase(it);

        delete blockItem;

        qDebug() << QString::fromUtf8("블록 제거됨: (%1, %2)")
            .arg(position.first).arg(position.second);
    }

    void GameBoard::clearAllBlocks()
    {
        // 모든 블록 그래픽 아이템 제거
        for (auto* blockItem : m_blockItems) {
            if (blockItem && m_scene) {
                m_scene->removeItem(blockItem);
            }
            delete blockItem;
        }

        m_blockItems.clear();
        m_blockMap.clear();

        // 보드 상태 초기화
        for (int row = 0; row < BOARD_SIZE; ++row) {
            for (int col = 0; col < BOARD_SIZE; ++col) {
                m_board[row][col] = PlayerColor::None;
            }
        }

        // 미리보기도 제거
        hideBlockPreview();

        qDebug() << QString::fromUtf8("모든 블록 제거됨");
    }

    BlockGraphicsItem* GameBoard::createBlockGraphicsItem(const Block& block, const Position& position)
    {
        QColor fillColor = getPlayerBrushColor(block.getPlayer());
        QColor borderColor = getPlayerBorderColor(block.getPlayer());

        BlockGraphicsItem* blockItem = new BlockGraphicsItem(block, m_cellSize);
        blockItem->updateColors(fillColor, borderColor);
        blockItem->updatePosition(position, m_cellSize);
        blockItem->setZValue(2); // 격자 위에 표시

        m_scene->addItem(blockItem);

        return blockItem;
    }

    bool GameBoard::isValidBlockPlacement(const Block& block, const Position& position) const
    {
        // 보드 경계 확인
        if (!block.isValidPlacement(position, BOARD_SIZE)) {
            return false;
        }

        // 다른 블록과의 충돌 확인
        PositionList absolutePositions = block.getAbsolutePositions(position);
        for (const auto& pos : absolutePositions) {
            if (isCellOccupied(pos.first, pos.second)) {
                return false;
            }
        }

        return true;
    }

    bool GameBoard::checkBlokusRules(const Block& block, const Position& position, PlayerColor player) const
    {
        // 게임 로직이 있으면 그쪽에서 처리, 없으면 기본 허용
        if (m_gameLogic) {
            return m_gameLogic->canPlaceBlock(block, position, player);
        }

        return true; // 게임 로직이 없으면 기본적으로 허용
    }

    QColor GameBoard::getPlayerBrushColor(PlayerColor player) const
    {
        auto it = m_playerColors.find(player);
        if (it != m_playerColors.end()) {
            return it->second;
        }
        return m_playerColors.at(PlayerColor::None);
    }

    QColor GameBoard::getPlayerBorderColor(PlayerColor player) const
    {
        QColor brushColor = getPlayerBrushColor(player);
        return brushColor.darker(150); // 더 어두운 테두리
    }

    // ========================================
    // 게임 로직 연동 함수들
    // ========================================

    void GameBoard::setGameLogic(GameLogic* gameLogic)
    {
        m_gameLogic = gameLogic;
        qDebug() << QString::fromUtf8("GameBoard에 게임 로직 연결됨");
    }

    bool GameBoard::tryPlaceCurrentBlock(const Position& position)
    {
        if (!m_gameLogic) {
            qWarning() << QString::fromUtf8("게임 로직이 설정되지 않음");
            return false;
        }

        // 현재 선택된 블록으로 배치 시도
        PlayerColor currentPlayer = m_gameLogic->getCurrentPlayer();
        Block blockToPlace = m_selectedBlock;
        blockToPlace.setPlayer(currentPlayer);

        if (m_gameLogic->canPlaceBlock(blockToPlace, position, currentPlayer)) {
            // 게임 로직에서 블록 배치
            if (m_gameLogic->placeBlock(blockToPlace, position, currentPlayer)) {
                // 시각적으로도 블록 추가
                addBlockToBoard(blockToPlace, position);

                // 블록 배치 시그널 발생
                emit blockPlaced(BlockPlacement(
                    blockToPlace.getType(),
                    position,
                    blockToPlace.getRotation(),
                    blockToPlace.getFlipState(),
                    currentPlayer
                ));

                return true;
            }
        }

        return false;
    }

    void GameBoard::setSelectedBlock(const Block& block)
    {
        m_selectedBlock = block;
        m_hasSelectedBlock = (block.getPlayer() != PlayerColor::None);

        // 현재 호버 위치에서 미리보기 업데이트
        if (m_hasSelectedBlock) {
            showCurrentBlockPreview();
        }
        else {
            hideBlockPreview();
        }

        qDebug() << QString::fromUtf8("선택된 블록 변경: %1 (선택상태: %2)")
            .arg(BlockFactory::getBlockName(block.getType()))
            .arg(m_hasSelectedBlock ? "있음" : "없음");
    }

    // ========================================
    // 기본 보드 상태 함수들
    // ========================================

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

        highlight->setZValue(1);  // 격자 위에, 블록 아래에 표시
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

    QColor GameBoard::getPlayerColor(PlayerColor player) const
    {
        auto it = m_playerColors.find(player);
        if (it != m_playerColors.end()) {
            return it->second;
        }
        return m_playerColors.at(PlayerColor::None);
    }

    // ========================================
    // 이벤트 핸들러들
    // ========================================

    void GameBoard::mousePressEvent(QMouseEvent* event)
    {
        if (m_readOnly) {
            QGraphicsView::mousePressEvent(event);
            return;
        }

        m_mousePressed = true;
        Position boardPos = screenToBoard(event->pos());

        if (event->button() == Qt::LeftButton) {
            if (isCellValid(boardPos.first, boardPos.second)) {
                // 좌클릭: 현재 선택된 블록을 해당 위치에 배치 시도
                if (tryPlaceCurrentBlock(boardPos)) {
                    QString placeMsg = QString::fromUtf8("블록 배치: %1 (위치: %2, %3)")
                        .arg(BlockFactory::getBlockName(m_selectedBlock.getType()))
                        .arg(boardPos.first)
                        .arg(boardPos.second);
                    qDebug() << placeMsg;
                }
                else {
                    qDebug() << QString::fromUtf8("블록 배치 불가 - 규칙 위반 (위치: %1, %2)")
                        .arg(boardPos.first).arg(boardPos.second);
                }

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

                    // 블록이 선택된 상태에서만 미리보기 표시
                    if (m_hasSelectedBlock) {
                        showCurrentBlockPreview();
                    }
                }
                else {
                    // 보드 밖으로 나가면 미리보기 숨김
                    hideBlockPreview();
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

    void GameBoard::keyPressEvent(QKeyEvent* event)
    {
        // 읽기 전용 모드가 아니고 블록이 선택된 상태에서만 키보드 입력 처리
        if (!m_readOnly && m_hasSelectedBlock) {
            if (event->key() == Qt::Key_R) {
                // R키: 블록 회전
                m_selectedBlock.rotateClockwise();

                QString rotationMsg = QString::fromUtf8("블록 회전: %1도")
                    .arg(static_cast<int>(m_selectedBlock.getRotation()) * 90);
                qDebug() << rotationMsg;

                // 즉시 미리보기 업데이트
                showCurrentBlockPreview();

                // 팔레트에도 회전 상태 반영
                emit blockRotated(m_selectedBlock);

                event->accept();
                return;
            }
            else if (event->key() == Qt::Key_F) {
                // F키: 블록 뒤집기
                m_selectedBlock.flipHorizontal();

                qDebug() << QString::fromUtf8("블록 뒤집기 적용");

                // 즉시 미리보기 업데이트
                showCurrentBlockPreview();

                // 팔레트에도 뒤집기 상태 반영
                emit blockFlipped(m_selectedBlock);

                event->accept();
                return;
            }
            else if (event->key() == Qt::Key_Delete || event->key() == Qt::Key_Backspace) {
                // Delete/Backspace키: 현재 호버 위치의 블록 제거 (디버깅용)
                Position currentPos = m_hoveredCell;
                if (isCellValid(currentPos.first, currentPos.second) &&
                    m_blockMap.find(currentPos) != m_blockMap.end()) {
                    removeBlockFromBoard(currentPos);
                    qDebug() << QString::fromUtf8("블록 제거: (%1, %2)")
                        .arg(currentPos.first).arg(currentPos.second);

                    // 미리보기 다시 표시
                    showCurrentBlockPreview();
                }

                event->accept();
                return;
            }
        }

        QGraphicsView::keyPressEvent(event);
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
        hideBlockPreview(); // 마우스가 보드를 벗어나면 미리보기 숨김
        QGraphicsView::leaveEvent(event);
    }

    // 포커스 이벤트 처리 추가
    void GameBoard::focusInEvent(QFocusEvent* event)
    {
        // 포커스를 받으면 키보드 입력 준비
        QGraphicsView::focusInEvent(event);
        qDebug() << QString::fromUtf8("GameBoard 포커스 획득 - 키보드 입력 활성화");
    }

    void GameBoard::focusOutEvent(QFocusEvent* event)
    {
        QGraphicsView::focusOutEvent(event);
        qDebug() << QString::fromUtf8("GameBoard 포커스 상실");
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

    // ========================================
    // 미리보기 관련 함수들
    // ========================================

    void GameBoard::showCurrentBlockPreview()
    {
        // 선택된 블록이 없거나 읽기 전용이면 미리보기 안함
        if (!m_hasSelectedBlock || m_readOnly || !isCellValid(m_hoveredCell.first, m_hoveredCell.second)) {
            hideBlockPreview();
            return;
        }

        hideBlockPreview(); // 기존 미리보기 제거

        // 현재 플레이어 색상으로 블록 설정
        Block previewBlock = m_selectedBlock;
        if (m_gameLogic) {
            previewBlock.setPlayer(m_gameLogic->getCurrentPlayer());
        }

        // 배치 가능 여부 확인 (게임 로직 사용)
        bool canPlace = false;
        if (m_gameLogic) {
            canPlace = m_gameLogic->canPlaceBlock(previewBlock, m_hoveredCell, previewBlock.getPlayer());
        }
        else {
            // 게임 로직이 없으면 기본 충돌 검사만
            canPlace = isValidBlockPlacement(previewBlock, m_hoveredCell);
        }

        // 미리보기 색상 결정
        QColor previewColor;
        QColor borderColor;

        if (canPlace) {
            // 배치 가능하면 기존 색상에 투명도 적용
            previewColor = getPlayerBrushColor(previewBlock.getPlayer());
            previewColor.setAlpha(150); // 반투명
            borderColor = previewColor.darker(150);
        }
        else {
            // 배치 불가능하면 빨간색으로 표시
            previewColor = QColor(255, 100, 100, 150); // 반투명 빨간색
            borderColor = QColor(200, 50, 50, 200);    // 진한 빨간색 테두리
        }

        // 미리보기 블록 생성
        m_currentPreview = new BlockGraphicsItem(previewBlock, m_cellSize);
        if (m_currentPreview) {
            m_currentPreview->setPreviewMode(true);
            m_currentPreview->updateColors(previewColor, borderColor);
            m_currentPreview->updatePosition(m_hoveredCell, m_cellSize);
            m_currentPreview->setZValue(3); // 다른 블록들 위에 표시

            m_scene->addItem(m_currentPreview);
        }
    }

    void GameBoard::hideBlockPreview()
    {
        if (m_currentPreview) {
            m_scene->removeItem(m_currentPreview);
            delete m_currentPreview;
            m_currentPreview = nullptr;
        }

        // 기존 미리보기 아이템들도 제거
        for (auto* item : m_previewItems) {
            if (m_scene && item) {
                m_scene->removeItem(item);
                delete item;
            }
        }
        m_previewItems.clear();
    }

    // ========================================
    // 블록 배치 관련 함수들 (인터페이스 호환성)
    // ========================================

    bool GameBoard::canPlaceBlock(const BlockPlacement& placement) const
    {
        Block block(placement.type, placement.player);
        block.setRotation(placement.rotation);
        block.setFlipState(placement.flip);

        if (m_gameLogic) {
            return m_gameLogic->canPlaceBlock(block, placement.position, placement.player);
        }
        else {
            return isValidBlockPlacement(block, placement.position) &&
                checkBlokusRules(block, placement.position, placement.player);
        }
    }

    bool GameBoard::placeBlock(const BlockPlacement& placement)
    {
        if (!canPlaceBlock(placement)) {
            return false;
        }

        Block block(placement.type, placement.player);
        block.setRotation(placement.rotation);
        block.setFlipState(placement.flip);

        if (m_gameLogic) {
            if (m_gameLogic->placeBlock(block, placement.position, placement.player)) {
                addBlockToBoard(block, placement.position);
                emit blockPlaced(placement);
                return true;
            }
        }
        else {
            addBlockToBoard(block, placement.position);
            emit blockPlaced(placement);
            return true;
        }

        return false;
    }

    void GameBoard::removeBlock(const Position& position)
    {
        removeBlockFromBoard(position);
        emit blockRemoved(position);
    }

    void GameBoard::showBlockPreview(const BlockPlacement& placement)
    {
        hideBlockPreview(); // 기존 미리보기 제거

        if (!canPlaceBlock(placement)) {
            return; // 배치할 수 없는 위치면 미리보기 안함
        }

        Block previewBlock(placement.type, placement.player);
        previewBlock.setRotation(placement.rotation);
        previewBlock.setFlipState(placement.flip);

        QColor previewColor = getPlayerBrushColor(placement.player);
        previewColor.setAlpha(120); // 반투명

        m_currentPreview = createBlockGraphicsItem(previewBlock, placement.position);
        if (m_currentPreview) {
            m_currentPreview->setPreviewMode(true);
            m_currentPreview->updateColors(previewColor, previewColor.darker());
            m_currentPreview->setZValue(3); // 다른 블록들 위에 표시
        }
    }

    // ========================================
    // 테스트/디버깅 함수들 (private)
    // ========================================

    void GameBoard::addTestBlocks()
    {
        qDebug() << QString::fromUtf8("테스트 블록들 추가 중...");

        // 각 플레이어별로 몇 개의 블록을 랜덤 위치에 배치
        std::vector<PlayerColor> players = {
            PlayerColor::Blue, PlayerColor::Yellow,
            PlayerColor::Red, PlayerColor::Green
        };

        std::vector<BlockType> testBlocks = {
            BlockType::Single, BlockType::Domino, BlockType::TrioLine,
            BlockType::Tetro_T, BlockType::Pento_F
        };

        std::random_device rd;
        std::mt19937 gen(rd());
        std::uniform_int_distribution<> posDist(2, BOARD_SIZE - 8);

        for (size_t i = 0; i < players.size() && i < testBlocks.size(); ++i) {
            Block testBlock(testBlocks[i], players[i]);
            Position randomPos = { posDist(gen), posDist(gen) };

            addBlockToBoard(testBlock, randomPos);
        }
    }

    void GameBoard::onShowAllBlocks()
    {
        clearAllBlocks();

        qDebug() << QString::fromUtf8("모든 블록 타입 표시 중...");

        auto allBlockTypes = BlockFactory::getAllBlockTypes();
        PlayerColor currentPlayer = PlayerColor::Blue;

        int row = 1, col = 1;
        int maxColsPerRow = 8;
        int currentCol = 0;

        for (BlockType blockType : allBlockTypes) {
            Block block(blockType, currentPlayer);
            Position pos = { row, col };

            if (isValidBlockPlacement(block, pos)) {
                addBlockToBoard(block, pos);

                // 다음 위치 계산
                QRect blockRect = block.getBoundingRect();
                col += blockRect.width() + 1;
                currentCol++;

                // 줄 바꿈
                if (currentCol >= maxColsPerRow) {
                    row += 6; // 충분한 간격
                    col = 1;
                    currentCol = 0;

                    // 플레이어 색상 변경
                    currentPlayer = Utils::getNextPlayer(currentPlayer);
                }
            }
        }
    }

    void GameBoard::onClearAllBlocks()
    {
        clearAllBlocks();
        drawStartingCorners(); // 시작 모서리 다시 그리기
    }

    void GameBoard::onAddRandomBlock()
    {
        std::random_device rd;
        std::mt19937 gen(rd());

        // 랜덤 블록 타입 선택
        auto allTypes = BlockFactory::getAllBlockTypes();
        std::uniform_int_distribution<> typeDist(0, allTypes.size() - 1);
        BlockType randomType = allTypes[typeDist(gen)];

        // 랜덤 플레이어 색상
        std::vector<PlayerColor> players = {
            PlayerColor::Blue, PlayerColor::Yellow,
            PlayerColor::Red, PlayerColor::Green
        };
        std::uniform_int_distribution<> playerDist(0, players.size() - 1);
        PlayerColor randomPlayer = players[playerDist(gen)];

        // 랜덤 위치
        std::uniform_int_distribution<> posDist(1, BOARD_SIZE - 5);
        Position randomPos = { posDist(gen), posDist(gen) };

        Block randomBlock(randomType, randomPlayer);

        if (isValidBlockPlacement(randomBlock, randomPos)) {
            addBlockToBoard(randomBlock, randomPos);
            QString successMsg = QString::fromUtf8("랜덤 블록 추가: %1")
                .arg(BlockFactory::getBlockName(randomType));
            qDebug() << successMsg;
        }
        else {
            qDebug() << QString::fromUtf8("랜덤 블록 배치 실패 - 위치 충돌");
        }
    }

} // namespace Blokus