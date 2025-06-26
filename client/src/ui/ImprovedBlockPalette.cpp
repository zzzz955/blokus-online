#include "ui/ImprovedBlockPalette.h"
#include <QPainter>
#include <QMouseEvent>
#include <QDebug>

namespace Blokus {

    // ========================================
    // PolyominoWidget 구현
    // ========================================

    PolyominoWidget::PolyominoWidget(const Block& block, bool isOwned, QWidget* parent)
        : QWidget(parent)
        , m_block(block)
        , m_isOwned(isOwned)
        , m_isSelected(false)
        , m_isUsed(false)
        , m_cellSize(isOwned ? 12.0 : 8.0)  // 자신: 12px, 상대방: 8px
    {
        calculateSize();
        setToolTip(QString::fromUtf8("%1 (%2점)")
            .arg(BlockFactory::getBlockName(m_block.getType()))
            .arg(BlockFactory::getBlockScore(m_block.getType())));
    }

    void PolyominoWidget::calculateSize()
    {
        QRect blockRect = m_block.getBoundingRect();
        int width = blockRect.width() * m_cellSize + 4;  // 2px 여백
        int height = blockRect.height() * m_cellSize + 4; // 2px 여백

        m_widgetSize = QSize(width, height);
        setFixedSize(m_widgetSize);
    }

    void PolyominoWidget::setSelected(bool selected)
    {
        if (m_isSelected != selected) {
            m_isSelected = selected;
            update();
        }
    }

    void PolyominoWidget::setUsed(bool used)
    {
        if (m_isUsed != used) {
            m_isUsed = used;
            update();
        }
    }

    QSize PolyominoWidget::sizeHint() const
    {
        return m_widgetSize;
    }

    void PolyominoWidget::paintEvent(QPaintEvent* event)
    {
        Q_UNUSED(event)

            QPainter painter(this);
        painter.setRenderHint(QPainter::Antialiasing);

        // 배경 그리기
        if (m_isSelected && !m_isUsed) {
            painter.fillRect(rect(), QColor(52, 152, 219, 50)); // 파란 선택 배경
            painter.setPen(QPen(QColor(52, 152, 219), 2));
            painter.drawRect(rect().adjusted(1, 1, -1, -1));
        }

        // 폴리오미노 그리기
        PositionList shape = m_block.getCurrentShape();

        QColor blockColor;
        if (m_isUsed) {
            blockColor = QColor(150, 150, 150, 100); // 회색 (사용됨)
        }
        else {
            // 플레이어 색상
            switch (m_block.getPlayer()) {
            case PlayerColor::Blue: blockColor = QColor(52, 152, 219); break;
            case PlayerColor::Yellow: blockColor = QColor(241, 196, 15); break;
            case PlayerColor::Red: blockColor = QColor(231, 76, 60); break;
            case PlayerColor::Green: blockColor = QColor(46, 204, 113); break;
            default: blockColor = QColor(200, 200, 200); break;
            }
        }

        painter.setBrush(QBrush(blockColor));
        painter.setPen(QPen(blockColor.darker(150), 1));

        // 각 셀 그리기
        for (const auto& pos : shape) {
            QRect cellRect(
                2 + pos.second * m_cellSize,
                2 + pos.first * m_cellSize,
                m_cellSize,
                m_cellSize
            );
            painter.drawRect(cellRect);
        }

        // 사용된 블록에 X 표시
        if (m_isUsed) {
            painter.setPen(QPen(Qt::red, 2));
            painter.drawLine(2, 2, width() - 2, height() - 2);
            painter.drawLine(width() - 2, 2, 2, height() - 2);
        }
    }

    void PolyominoWidget::mousePressEvent(QMouseEvent* event)
    {
        if (event->button() == Qt::LeftButton && !m_isUsed) {
            emit blockClicked(m_block);
        }
        QWidget::mousePressEvent(event);
    }

    // ========================================
    // CompactPlayerPalette 구현
    // ========================================

    CompactPlayerPalette::CompactPlayerPalette(PlayerColor player, bool isOwned,
        Qt::Orientation orientation, QWidget* parent)
        : QWidget(parent)
        , m_player(player)
        , m_isOwned(isOwned)
        , m_orientation(orientation)
        , m_selectedBlockType(BlockType::Single)
    {
        setupUI();
        createBlockWidgets();
    }

    void CompactPlayerPalette::setupUI()
    {
        QVBoxLayout* mainLayout = new QVBoxLayout(this);
        mainLayout->setContentsMargins(2, 2, 2, 2);
        mainLayout->setSpacing(2);

        // 플레이어 라벨 (자신의 블록만 표시)
        if (m_isOwned) {
            m_playerLabel = new QLabel(QString::fromUtf8("내 블록 (%1)")
                .arg(Utils::playerColorToString(m_player)));
            m_playerLabel->setAlignment(Qt::AlignCenter);
            m_playerLabel->setStyleSheet(QString("font-weight: bold; color: %1; padding: 3px;")
                .arg(getPlayerColorName(m_player)));
            mainLayout->addWidget(m_playerLabel);
        }
        else {
            m_playerLabel = nullptr;
        }

        // 스크롤 영역
        m_scrollArea = new QScrollArea();
        m_scrollArea->setHorizontalScrollBarPolicy(
            m_orientation == Qt::Horizontal ? Qt::ScrollBarAsNeeded : Qt::ScrollBarAlwaysOff);
        m_scrollArea->setVerticalScrollBarPolicy(
            m_orientation == Qt::Vertical ? Qt::ScrollBarAsNeeded : Qt::ScrollBarAlwaysOff);
        m_scrollArea->setFrameStyle(QFrame::NoFrame);

        // 컨테이너
        m_container = new QWidget();

        if (m_orientation == Qt::Horizontal) {
            m_layout = new QHBoxLayout(m_container);
            m_scrollArea->setFixedHeight(m_isOwned ? 80 : 50);
        }
        else {
            m_layout = new QVBoxLayout(m_container);
            m_scrollArea->setFixedWidth(m_isOwned ? 80 : 50);
        }

        m_layout->setContentsMargins(2, 2, 2, 2);
        m_layout->setSpacing(2);

        m_scrollArea->setWidget(m_container);
        m_scrollArea->setWidgetResizable(true);
        mainLayout->addWidget(m_scrollArea);
    }

    void CompactPlayerPalette::createBlockWidgets()
    {
        auto allBlockTypes = BlockFactory::getAllBlockTypes();

        for (BlockType blockType : allBlockTypes) {
            Block block(blockType, m_player);
            PolyominoWidget* widget = new PolyominoWidget(block, m_isOwned, this);

            connect(widget, &PolyominoWidget::blockClicked,
                this, &CompactPlayerPalette::onBlockClicked);

            m_layout->addWidget(widget);
            m_blockWidgets[blockType] = widget;
        }

        // 첫 번째 블록을 기본 선택 (자신의 블록만)
        if (m_isOwned && !m_blockWidgets.empty()) {
            m_blockWidgets[BlockType::Single]->setSelected(true);
        }
    }

    void CompactPlayerPalette::setSelectedBlock(BlockType blockType)
    {
        // 기존 선택 해제
        if (m_blockWidgets.find(m_selectedBlockType) != m_blockWidgets.end()) {
            m_blockWidgets[m_selectedBlockType]->setSelected(false);
        }

        // 새로운 블록 선택
        m_selectedBlockType = blockType;
        if (m_blockWidgets.find(blockType) != m_blockWidgets.end()) {
            m_blockWidgets[blockType]->setSelected(true);
        }
    }

    void CompactPlayerPalette::setBlockUsed(BlockType blockType, bool used)
    {
        if (m_blockWidgets.find(blockType) != m_blockWidgets.end()) {
            m_blockWidgets[blockType]->setUsed(used);

            // 사용된 블록이 현재 선택된 블록이면 다른 블록으로 변경
            if (used && blockType == m_selectedBlockType && m_isOwned) {
                auto availableBlocks = BlockFactory::getAllBlockTypes();
                for (BlockType availableType : availableBlocks) {
                    if (!m_blockWidgets[availableType]->isUsed()) {
                        setSelectedBlock(availableType);
                        break;
                    }
                }
            }
        }
    }

    void CompactPlayerPalette::resetAllBlocks()
    {
        for (auto& pair : m_blockWidgets) {
            pair.second->setUsed(false);
        }

        // 첫 번째 블록을 다시 선택 (자신의 블록만)
        if (m_isOwned) {
            setSelectedBlock(BlockType::Single);
        }
    }

    Block CompactPlayerPalette::getSelectedBlock() const
    {
        return Block(m_selectedBlockType, m_player);
    }

    void CompactPlayerPalette::onBlockClicked(const Block& block)
    {
        if (m_isOwned && !m_blockWidgets[block.getType()]->isUsed()) {
            setSelectedBlock(block.getType());
            emit blockSelected(block);
        }
    }

    QString CompactPlayerPalette::getPlayerColorName(PlayerColor player) const
    {
        switch (player) {
        case PlayerColor::Blue: return "#3498db";
        case PlayerColor::Yellow: return "#f1c40f";
        case PlayerColor::Red: return "#e74c3c";
        case PlayerColor::Green: return "#2ecc71";
        default: return "#7f8c8d";
        }
    }

    // ========================================
    // ImprovedGamePalette 구현
    // ========================================

    ImprovedGamePalette::ImprovedGamePalette(QWidget* parent)
        : QWidget(parent)
        , m_currentPlayer(PlayerColor::Blue)
    {
        setupUI();
        createPlayerPalettes();
        updateCurrentPlayerHighlight();
    }

    void ImprovedGamePalette::setupUI()
    {
        // 전체 레이아웃은 MainWindow에서 관리
        // 이 위젯은 각 방향별 팔레트만 포함
    }

    void ImprovedGamePalette::createPlayerPalettes()
    {
        // 남쪽: 자신의 블록 (파란색, 크게)
        m_southPalette = new CompactPlayerPalette(PlayerColor::Blue, true, Qt::Horizontal, this);

        // 동쪽: 상대방 블록 (노란색, 작게)
        m_eastPalette = new CompactPlayerPalette(PlayerColor::Yellow, false, Qt::Vertical, this);

        // 북쪽: 상대방 블록 (빨간색, 작게)
        m_northPalette = new CompactPlayerPalette(PlayerColor::Red, false, Qt::Horizontal, this);

        // 서쪽: 상대방 블록 (초록색, 작게)
        m_westPalette = new CompactPlayerPalette(PlayerColor::Green, false, Qt::Vertical, this);

        // 맵에 저장
        m_playerPalettes[PlayerColor::Blue] = m_southPalette;
        m_playerPalettes[PlayerColor::Yellow] = m_eastPalette;
        m_playerPalettes[PlayerColor::Red] = m_northPalette;
        m_playerPalettes[PlayerColor::Green] = m_westPalette;

        // 자신의 블록 선택 시그널 연결
        connect(m_southPalette, &CompactPlayerPalette::blockSelected,
            this, &ImprovedGamePalette::onPlayerBlockSelected);
    }

    void ImprovedGamePalette::setCurrentPlayer(PlayerColor player)
    {
        m_currentPlayer = player;
        updateCurrentPlayerHighlight();
    }

    void ImprovedGamePalette::updateCurrentPlayerHighlight()
    {
        for (auto& pair : m_playerPalettes) {
            PlayerColor player = pair.first;
            CompactPlayerPalette* palette = pair.second;

            if (player == m_currentPlayer) {
                palette->setStyleSheet("QWidget { border: 2px solid #e74c3c; }");
            }
            else {
                palette->setStyleSheet("QWidget { border: 1px solid #bdc3c7; }");
            }
        }
    }

    Block ImprovedGamePalette::getSelectedBlock() const
    {
        auto it = m_playerPalettes.find(m_currentPlayer);
        if (it != m_playerPalettes.end()) {
            return it->second->getSelectedBlock();
        }

        return Block(BlockType::Single, m_currentPlayer);
    }

    void ImprovedGamePalette::setBlockUsed(PlayerColor player, BlockType blockType)
    {
        auto it = m_playerPalettes.find(player);
        if (it != m_playerPalettes.end()) {
            it->second->setBlockUsed(blockType, true);
        }
    }

    void ImprovedGamePalette::resetAllPlayerBlocks()
    {
        for (auto& pair : m_playerPalettes) {
            pair.second->resetAllBlocks();
        }
    }

    void ImprovedGamePalette::onPlayerBlockSelected(const Block& block)
    {
        // 현재 플레이어의 블록만 선택 가능
        if (block.getPlayer() == m_currentPlayer) {
            emit blockSelected(block);
        }
    }

    // 각 방향 팔레트 접근자들 구현
    CompactPlayerPalette* ImprovedGamePalette::getSouthPalette() const
    {
        return m_southPalette;
    }

    CompactPlayerPalette* ImprovedGamePalette::getEastPalette() const
    {
        return m_eastPalette;
    }

    CompactPlayerPalette* ImprovedGamePalette::getNorthPalette() const
    {
        return m_northPalette;
    }

    CompactPlayerPalette* ImprovedGamePalette::getWestPalette() const
    {
        return m_westPalette;
    }

} // namespace Blokus