#include "ui/BlockPalette.h"
#include <QGroupBox>
#include <QApplication>
#include <QPainter>
#include <QStyleOption>
#include <QDebug>

namespace Blokus {

    // ========================================
    // BlockItem 구현
    // ========================================

    BlockItem::BlockItem(const Block& block, bool isOwned, QWidget* parent)
        : QGraphicsView(parent)
        , m_block(block)
        , m_scene(nullptr)
        , m_blockItem(nullptr)
        , m_isOwned(isOwned)
        , m_isSelected(false)
        , m_isUsed(false)
    {
        setupGraphics();

        // 크기 설정
        qreal cellSize = isOwned ? OWNED_CELL_SIZE : OPPONENT_CELL_SIZE;
        QRect blockRect = m_block.getBoundingRect();
        int width = blockRect.width() * cellSize + 10;
        int height = blockRect.height() * cellSize + 10;

        setFixedSize(width, height);
        setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
        setVerticalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
        setFrameStyle(QFrame::Box);

        // 툴팁 설정
        setToolTip(QString::fromUtf8("%1 (%2점)")
            .arg(BlockFactory::getBlockName(m_block.getType()))
            .arg(BlockFactory::getBlockScore(m_block.getType())));
    }

    void BlockItem::setupGraphics()
    {
        m_scene = new QGraphicsScene(this);
        setScene(m_scene);

        qreal cellSize = m_isOwned ? OWNED_CELL_SIZE : OPPONENT_CELL_SIZE;

        // 배경 색상 설정
        QColor fillColor;
        if (m_isUsed) {
            fillColor = QColor(150, 150, 150, 100); // 회색 (사용됨)
        }
        else {
            // 플레이어 색상 가져오기
            switch (m_block.getPlayer()) {
            case PlayerColor::Blue: fillColor = QColor(52, 152, 219); break;
            case PlayerColor::Yellow: fillColor = QColor(241, 196, 15); break;
            case PlayerColor::Red: fillColor = QColor(231, 76, 60); break;
            case PlayerColor::Green: fillColor = QColor(46, 204, 113); break;
            default: fillColor = QColor(200, 200, 200); break;
            }
        }

        QColor borderColor = fillColor.darker(150);

        m_blockItem = new BlockGraphicsItem(m_block, cellSize);
        m_blockItem->updateColors(fillColor, borderColor);
        m_blockItem->setPos(5, 5); // 약간의 여백

        m_scene->addItem(m_blockItem);

        // 씬 크기 설정
        QRect blockRect = m_block.getBoundingRect();
        m_scene->setSceneRect(0, 0,
            blockRect.width() * cellSize + 10,
            blockRect.height() * cellSize + 10);

        fitInView(m_scene->sceneRect(), Qt::KeepAspectRatio);
    }

    void BlockItem::setSelected(bool selected)
    {
        if (m_isSelected != selected) {
            m_isSelected = selected;
            updateSelection();
        }
    }

    void BlockItem::setUsed(bool used)
    {
        if (m_isUsed != used) {
            m_isUsed = used;
            setupGraphics(); // 그래픽 다시 생성
            updateSelection();
        }
    }

    void BlockItem::updateBlock(const Block& block)
    {
        m_block = block;
        setupGraphics();
        updateSelection();
    }

    void BlockItem::updateSelection()
    {
        if (m_isSelected && !m_isUsed) {
            setStyleSheet("QGraphicsView { border: 3px solid #3498db; background-color: #ecf0f1; }");
        }
        else if (m_isUsed) {
            setStyleSheet("QGraphicsView { border: 2px solid #95a5a6; background-color: #bdc3c7; }");
        }
        else {
            setStyleSheet("QGraphicsView { border: 1px solid #bdc3c7; background-color: white; }");
        }
    }

    void BlockItem::mousePressEvent(QMouseEvent* event)
    {
        if (event->button() == Qt::LeftButton && !m_isUsed) {
            emit blockClicked(m_block);
        }
        QGraphicsView::mousePressEvent(event);
    }

    void BlockItem::paintEvent(QPaintEvent* event)
    {
        QGraphicsView::paintEvent(event);

        // 사용된 블록에 X 표시
        if (m_isUsed) {
            QPainter painter(viewport());
            painter.setPen(QPen(Qt::red, 3));
            painter.drawLine(5, 5, width() - 5, height() - 5);
            painter.drawLine(width() - 5, 5, 5, height() - 5);
        }
    }

    void BlockItem::resizeEvent(QResizeEvent* event)
    {
        QGraphicsView::resizeEvent(event);
        if (m_scene) {
            fitInView(m_scene->sceneRect(), Qt::KeepAspectRatio);
        }
    }

    // ========================================
    // PlayerBlockPalette 구현
    // ========================================

    PlayerBlockPalette::PlayerBlockPalette(PlayerColor player, bool isOwned, QWidget* parent)
        : QWidget(parent)
        , m_player(player)
        , m_isOwned(isOwned)
        , m_selectedBlockType(BlockType::Single)
        , m_mainLayout(nullptr)
        , m_playerLabel(nullptr)
        , m_scrollArea(nullptr)
        , m_blocksContainer(nullptr)
        , m_blocksLayout(nullptr)
    {
        setupUI();
        createBlockItems();
        updatePlayerLabel();
    }

    void PlayerBlockPalette::setupUI()
    {
        m_mainLayout = new QVBoxLayout(this);
        m_mainLayout->setContentsMargins(5, 5, 5, 5);
        m_mainLayout->setSpacing(3);

        // 플레이어 레이블
        m_playerLabel = new QLabel();
        m_playerLabel->setAlignment(Qt::AlignCenter);
        m_playerLabel->setStyleSheet("font-weight: bold; padding: 3px;");
        m_mainLayout->addWidget(m_playerLabel);

        // 스크롤 영역
        m_scrollArea = new QScrollArea();
        m_scrollArea->setHorizontalScrollBarPolicy(Qt::ScrollBarAsNeeded);
        m_scrollArea->setVerticalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
        m_scrollArea->setFixedHeight(m_isOwned ? 80 : 50); // 자신/상대방 크기 다르게

        // 블록 컨테이너
        m_blocksContainer = new QWidget();
        m_blocksLayout = new QHBoxLayout(m_blocksContainer);
        m_blocksLayout->setContentsMargins(2, 2, 2, 2);
        m_blocksLayout->setSpacing(3);

        m_scrollArea->setWidget(m_blocksContainer);
        m_scrollArea->setWidgetResizable(true);
        m_mainLayout->addWidget(m_scrollArea);
    }

    void PlayerBlockPalette::createBlockItems()
    {
        auto allBlockTypes = BlockFactory::getAllBlockTypes();

        for (BlockType blockType : allBlockTypes) {
            Block block(blockType, m_player);
            BlockItem* item = new BlockItem(block, m_isOwned, this);

            connect(item, &BlockItem::blockClicked, this, &PlayerBlockPalette::onBlockClicked);

            m_blocksLayout->addWidget(item);
            m_blockItems[blockType] = item;
        }

        // 첫 번째 블록을 기본 선택
        if (!m_blockItems.empty()) {
            m_blockItems[BlockType::Single]->setSelected(true);
        }
    }

    void PlayerBlockPalette::updatePlayerLabel()
    {
        QString playerName = Utils::playerColorToString(m_player);
        QString ownerInfo = m_isOwned ? QString::fromUtf8("(내 블록)") : QString::fromUtf8("(상대 블록)");

        m_playerLabel->setText(QString::fromUtf8("%1 %2").arg(playerName).arg(ownerInfo));

        // 플레이어 색상으로 배경 설정
        QColor playerColor;
        switch (m_player) {
        case PlayerColor::Blue: playerColor = QColor(52, 152, 219); break;
        case PlayerColor::Yellow: playerColor = QColor(241, 196, 15); break;
        case PlayerColor::Red: playerColor = QColor(231, 76, 60); break;
        case PlayerColor::Green: playerColor = QColor(46, 204, 113); break;
        default: playerColor = QColor(200, 200, 200); break;
        }

        QString styleSheet = QString("background-color: %1; color: white; border-radius: 3px;")
            .arg(playerColor.name());
        m_playerLabel->setStyleSheet(styleSheet);
    }

    void PlayerBlockPalette::setSelectedBlock(BlockType blockType)
    {
        // 기존 선택 해제
        if (m_blockItems.find(m_selectedBlockType) != m_blockItems.end()) {
            m_blockItems[m_selectedBlockType]->setSelected(false);
        }

        // 새로운 블록 선택
        m_selectedBlockType = blockType;
        if (m_blockItems.find(blockType) != m_blockItems.end()) {
            m_blockItems[blockType]->setSelected(true);

            // 스크롤하여 선택된 블록이 보이도록 조정
            QWidget* selectedWidget = m_blockItems[blockType];
            m_scrollArea->ensureWidgetVisible(selectedWidget);
        }
    }

    void PlayerBlockPalette::setBlockUsed(BlockType blockType, bool used)
    {
        if (m_blockItems.find(blockType) != m_blockItems.end()) {
            m_blockItems[blockType]->setUsed(used);

            // 사용된 블록이 현재 선택된 블록이면 다른 블록으로 변경
            if (used && blockType == m_selectedBlockType) {
                auto availableBlocks = getAvailableBlocks();
                if (!availableBlocks.empty()) {
                    setSelectedBlock(availableBlocks[0]);
                }
            }
        }

        updatePlayerLabel();
    }

    Block PlayerBlockPalette::getSelectedBlock() const
    {
        return Block(m_selectedBlockType, m_player);
    }

    std::vector<BlockType> PlayerBlockPalette::getAvailableBlocks() const
    {
        std::vector<BlockType> available;

        for (const auto& pair : m_blockItems) {
            if (!pair.second->isUsed()) {
                available.push_back(pair.first);
            }
        }

        return available;
    }

    void PlayerBlockPalette::updateAvailableBlocks(const std::vector<BlockType>& usedBlocks)
    {
        // 모든 블록을 사용 가능으로 리셋
        for (auto& pair : m_blockItems) {
            pair.second->setUsed(false);
        }

        // 사용된 블록들을 표시
        for (BlockType usedBlock : usedBlocks) {
            setBlockUsed(usedBlock, true);
        }
    }

    void PlayerBlockPalette::onBlockClicked(const Block& block)
    {
        if (!m_blockItems[block.getType()]->isUsed()) {
            setSelectedBlock(block.getType());
            emit blockSelected(block);
        }
    }

    // ========================================
    // GameBlockPalette 구현
    // ========================================

    GameBlockPalette::GameBlockPalette(QWidget* parent)
        : QWidget(parent)
        , m_currentPlayer(PlayerColor::Blue)
        , m_mainLayout(nullptr)
        , m_titleLabel(nullptr)
    {
        setupUI();
        createPlayerPalettes();
        updateCurrentPlayerHighlight();
    }

    void GameBlockPalette::setupUI()
    {
        m_mainLayout = new QVBoxLayout(this);
        m_mainLayout->setContentsMargins(5, 5, 5, 5);
        m_mainLayout->setSpacing(5);

        // 제목
        m_titleLabel = new QLabel(QString::fromUtf8("🎲 블록 팔레트"));
        m_titleLabel->setAlignment(Qt::AlignCenter);
        m_titleLabel->setStyleSheet("font-size: 14px; font-weight: bold; padding: 5px;");
        m_mainLayout->addWidget(m_titleLabel);

        setFixedHeight(280); // 고정 높이
    }

    void GameBlockPalette::createPlayerPalettes()
    {
        std::vector<PlayerColor> players = {
            PlayerColor::Blue, PlayerColor::Yellow,
            PlayerColor::Red, PlayerColor::Green
        };

        for (PlayerColor player : players) {
            bool isOwned = (player == m_currentPlayer);
            PlayerBlockPalette* palette = new PlayerBlockPalette(player, isOwned, this);

            connect(palette, &PlayerBlockPalette::blockSelected,
                this, &GameBlockPalette::onPlayerBlockSelected);

            m_mainLayout->addWidget(palette);
            m_playerPalettes[player] = palette;
        }
    }

    void GameBlockPalette::setCurrentPlayer(PlayerColor player)
    {
        if (m_currentPlayer != player) {
            m_currentPlayer = player;
            updateCurrentPlayerHighlight();
            emit playerChanged(player);
        }
    }

    void GameBlockPalette::updateCurrentPlayerHighlight()
    {
        for (auto& pair : m_playerPalettes) {
            PlayerColor player = pair.first;
            PlayerBlockPalette* palette = pair.second;

            if (player == m_currentPlayer) {
                palette->setStyleSheet("QWidget { border: 3px solid #e74c3c; background-color: #ffeaa7; }");
            }
            else {
                palette->setStyleSheet("QWidget { border: 1px solid #bdc3c7; background-color: #f8f9fa; }");
            }
        }
    }

    Block GameBlockPalette::getSelectedBlock() const
    {
        auto it = m_playerPalettes.find(m_currentPlayer);
        if (it != m_playerPalettes.end()) {
            return it->second->getSelectedBlock();
        }

        return Block(BlockType::Single, m_currentPlayer);
    }

    void GameBlockPalette::setBlockUsed(PlayerColor player, BlockType blockType)
    {
        auto it = m_playerPalettes.find(player);
        if (it != m_playerPalettes.end()) {
            it->second->setBlockUsed(blockType, true);
        }
    }

    void GameBlockPalette::updateGameState(const std::map<PlayerColor, std::vector<BlockType>>& usedBlocks)
    {
        for (const auto& pair : usedBlocks) {
            PlayerColor player = pair.first;
            const std::vector<BlockType>& playerUsedBlocks = pair.second;

            auto it = m_playerPalettes.find(player);
            if (it != m_playerPalettes.end()) {
                it->second->updateAvailableBlocks(playerUsedBlocks);
            }
        }
    }

    int GameBlockPalette::getAvailableBlockCount(PlayerColor player) const
    {
        auto it = m_playerPalettes.find(player);
        if (it != m_playerPalettes.end()) {
            return static_cast<int>(it->second->getAvailableBlocks().size());
        }

        return 0;
    }

    void GameBlockPalette::onPlayerBlockSelected(const Block& block)
    {
        // 현재 플레이어의 블록만 선택 가능
        if (block.getPlayer() == m_currentPlayer) {
            emit blockSelected(block);
        }
    }

} // namespace Blokus