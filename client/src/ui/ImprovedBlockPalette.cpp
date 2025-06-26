#include "ui/ImprovedBlockPalette.h"
#include <QPainter>
#include <QMouseEvent>
#include <QHBoxLayout>
#include <QVBoxLayout>
#include <QScrollArea>
#include <QDebug>
#include <QGraphicsProxyWidget>
#include <cmath>

namespace Blokus {

    // ========================================
    // BlockButton 구현
    // ========================================

    BlockButton::BlockButton(const Block& block, qreal blockSize, QWidget* parent)
        : QWidget(parent)
        , m_block(block)
        , m_blockSize(blockSize)
        , m_isSelected(false)
        , m_isUsed(false)
        , m_isHovered(false)
        , m_scene(nullptr)
        , m_blockItem(nullptr)
    {
        setupGraphics();

        // 마우스 추적 활성화
        setMouseTracking(true);

        // 툴팁 설정
        setToolTip(QString::fromUtf8("%1 (%2칸)")
            .arg(BlockFactory::getBlockName(block.getType()))
            .arg(block.getSize()));
    }

    void BlockButton::setSelected(bool selected)
    {
        if (m_isSelected != selected) {
            m_isSelected = selected;
            update();
        }
    }

    void BlockButton::setUsed(bool used)
    {
        if (m_isUsed != used) {
            m_isUsed = used;
            QWidget::setEnabled(!used); // QWidget의 setEnabled 호출
            update();
        }
    }

    void BlockButton::setEnabled(bool enabled)
    {
        QWidget::setEnabled(enabled);
        update();
    }

    void BlockButton::updateBlockState(const Block& newBlock)
    {
        m_block = newBlock;
        setupGraphics();
        update();
    }

    void BlockButton::setupGraphics()
    {
        // 블록의 바운딩 박스 계산
        QRect boundingRect = m_block.getBoundingRect();
        int width = boundingRect.width() * m_blockSize + 10; // 여백 추가
        int height = boundingRect.height() * m_blockSize + 10;

        setFixedSize(width, height);
    }

    QColor BlockButton::getPlayerColor() const
    {
        static const std::map<PlayerColor, QColor> colors = {
            { PlayerColor::Blue, QColor(52, 152, 219) },
            { PlayerColor::Yellow, QColor(241, 196, 15) },
            { PlayerColor::Red, QColor(231, 76, 60) },
            { PlayerColor::Green, QColor(46, 204, 113) },
            { PlayerColor::None, QColor(200, 200, 200) }
        };

        auto it = colors.find(m_block.getPlayer());
        return (it != colors.end()) ? it->second : colors.at(PlayerColor::None);
    }

    void BlockButton::paintEvent(QPaintEvent* event)
    {
        Q_UNUSED(event)

            QPainter painter(this);
        painter.setRenderHint(QPainter::Antialiasing);

        QColor baseColor = getPlayerColor();

        // 사용된 블록은 회색으로 표시
        if (m_isUsed) {
            baseColor = QColor(150, 150, 150);
        }

        // 선택된 블록은 하이라이트
        if (m_isSelected) {
            painter.setPen(QPen(QColor(255, 215, 0), 3)); // 금색 테두리
        }
        else if (m_isHovered && !m_isUsed) {
            painter.setPen(QPen(baseColor.lighter(130), 2));
        }
        else {
            painter.setPen(QPen(baseColor.darker(120), 1));
        }

        // 호버 효과
        if (m_isHovered && !m_isUsed) {
            baseColor = baseColor.lighter(110);
        }

        painter.setBrush(QBrush(baseColor));

        // 블록 모양 그리기
        PositionList shape = m_block.getCurrentShape();
        for (const auto& pos : shape) {
            int x = pos.second * m_blockSize + 5;
            int y = pos.first * m_blockSize + 5;
            painter.drawRect(x, y, m_blockSize, m_blockSize);
        }

        // 사용된 블록에는 X 표시
        if (m_isUsed) {
            painter.setPen(QPen(Qt::red, 2));
            painter.drawLine(2, 2, width() - 2, height() - 2);
            painter.drawLine(width() - 2, 2, 2, height() - 2);
        }
    }

    void BlockButton::mousePressEvent(QMouseEvent* event)
    {
        if (event->button() == Qt::LeftButton && !m_isUsed) {
            emit blockClicked(m_block);
        }
        QWidget::mousePressEvent(event);
    }

    void BlockButton::enterEvent(QEvent* event)
    {
        m_isHovered = true;
        update();
        QWidget::enterEvent(event);
    }

    void BlockButton::leaveEvent(QEvent* event)
    {
        m_isHovered = false;
        update();
        QWidget::leaveEvent(event);
    }

    // ========================================
    // DirectionPalette 구현
    // ========================================

    DirectionPalette::DirectionPalette(Direction direction, QWidget* parent)
        : QWidget(parent)
        , m_direction(direction)
        , m_player(PlayerColor::None)
        , m_scrollArea(nullptr)
        , m_blockContainer(nullptr)
        , m_blockLayout(nullptr)
        , m_selectedBlockType(BlockType::Single)
    {
        setupLayout();

        // 스타일시트 설정
        QString directionName = getDirectionName();
        setObjectName(QString("DirectionPalette_%1").arg(directionName));

        if (direction == Direction::South) {
            setStyleSheet("QWidget#DirectionPalette_South { background-color: #f8f9fa; border: 2px solid #3498db; border-radius: 8px; }");
        }
        else {
            setStyleSheet("QWidget#" + objectName() + " { background-color: #ecf0f1; border: 1px solid #bdc3c7; border-radius: 5px; }");
        }
    }

    void DirectionPalette::setPlayer(PlayerColor player)
    {
        if (m_player != player) {
            m_player = player;

            // 해당 플레이어의 블록들로 업데이트
            m_blocks.clear();
            auto allTypes = BlockFactory::getAllBlockTypes();
            for (BlockType type : allTypes) {
                m_blocks.emplace_back(type, player);
            }

            updateBlockButtons();
        }
    }

    void DirectionPalette::setBlocks(const std::vector<Block>& blocks)
    {
        m_blocks = blocks;
        updateBlockButtons();
    }

    void DirectionPalette::setBlockUsed(BlockType blockType, bool used)
    {
        if (used) {
            m_usedBlocks.insert(blockType);
        }
        else {
            m_usedBlocks.erase(blockType);
        }

        // 해당 블록 버튼 업데이트
        auto it = m_blockButtons.find(blockType);
        if (it != m_blockButtons.end()) {
            it->second->setUsed(used);
        }
    }

    void DirectionPalette::resetAllBlocks()
    {
        m_usedBlocks.clear();
        for (auto& pair : m_blockButtons) {
            pair.second->setUsed(false);
        }
    }

    void DirectionPalette::highlightBlock(BlockType blockType, bool highlight)
    {
        auto it = m_blockButtons.find(blockType);
        if (it != m_blockButtons.end()) {
            it->second->setSelected(highlight);
            m_selectedBlockType = highlight ? blockType : BlockType::Single;
        }
    }

    void DirectionPalette::setupLayout()
    {
        QVBoxLayout* mainLayout = new QVBoxLayout(this);
        mainLayout->setContentsMargins(3, 3, 3, 3);
        mainLayout->setSpacing(2);

        // 방향 라벨 (남쪽은 더 큰 글자)
        QString labelText;
        switch (m_direction) {
        case Direction::North: labelText = QString::fromUtf8("상대방"); break;
        case Direction::South: labelText = QString::fromUtf8("나의 블록"); break;
        case Direction::East: labelText = QString::fromUtf8("상대방"); break;
        case Direction::West: labelText = QString::fromUtf8("상대방"); break;
        }

        QLabel* directionLabel = new QLabel(labelText);
        if (m_direction == Direction::South) {
            directionLabel->setStyleSheet("font-weight: bold; font-size: 14px; color: #2c3e50; padding: 5px;");
            directionLabel->setAlignment(Qt::AlignCenter);
        }
        else {
            directionLabel->setStyleSheet("font-size: 10px; color: #7f8c8d; padding: 2px;");
            directionLabel->setAlignment(Qt::AlignCenter);
        }
        mainLayout->addWidget(directionLabel);

        // 스크롤 영역
        m_scrollArea = new QScrollArea();
        m_scrollArea->setWidgetResizable(true);
        m_scrollArea->setHorizontalScrollBarPolicy(Qt::ScrollBarAsNeeded);
        m_scrollArea->setVerticalScrollBarPolicy(Qt::ScrollBarAsNeeded);

        // 블록 컨테이너
        m_blockContainer = new QWidget();
        m_blockLayout = new QGridLayout(m_blockContainer);
        m_blockLayout->setContentsMargins(2, 2, 2, 2);
        m_blockLayout->setSpacing(2);

        m_scrollArea->setWidget(m_blockContainer);
        mainLayout->addWidget(m_scrollArea);

        // 크기 제약 설정
        if (m_direction == Direction::South) {
            setMinimumHeight(120);
            setMaximumHeight(180);
        }
        else {
            setMinimumWidth(80);
            setMaximumWidth(120);
            setMinimumHeight(300);
        }
    }

    void DirectionPalette::updateBlockButtons()
    {
        // 기존 버튼들 제거
        for (auto& pair : m_blockButtons) {
            delete pair.second;
        }
        m_blockButtons.clear();

        // 레이아웃 클리어
        QLayoutItem* item;
        while ((item = m_blockLayout->takeAt(0)) != nullptr) {
            delete item;
        }

        qreal blockSize = getBlockSize();
        int maxPerRow = getMaxBlocksPerRow();
        int row = 0, col = 0;

        for (const Block& block : m_blocks) {
            BlockButton* button = new BlockButton(block, blockSize);

            // 사용 상태 설정
            bool isUsed = m_usedBlocks.find(block.getType()) != m_usedBlocks.end();
            button->setUsed(isUsed);

            // 시그널 연결
            connect(button, &BlockButton::blockClicked, this, &DirectionPalette::onBlockButtonClicked);

            m_blockLayout->addWidget(button, row, col);
            m_blockButtons[block.getType()] = button;

            col++;
            if (col >= maxPerRow) {
                col = 0;
                row++;
            }
        }
    }

    qreal DirectionPalette::getBlockSize() const
    {
        switch (m_direction) {
        case Direction::South: return 25.0; // 자신의 블록은 크게
        default: return 15.0;               // 상대방 블록은 작게
        }
    }

    int DirectionPalette::getMaxBlocksPerRow() const
    {
        switch (m_direction) {
        case Direction::South: return 7;    // 남쪽은 가로로 넓게
        case Direction::North: return 6;    // 북쪽도 가로로
        case Direction::East:
        case Direction::West: return 2;     // 동서쪽은 세로로 좁게
        }
        return 4;
    }

    QString DirectionPalette::getDirectionName() const
    {
        switch (m_direction) {
        case Direction::North: return "North";
        case Direction::South: return "South";
        case Direction::East: return "East";
        case Direction::West: return "West";
        }
        return "Unknown";
    }

    void DirectionPalette::onBlockButtonClicked(const Block& block)
    {
        // 사용된 블록은 선택 불가
        if (m_usedBlocks.find(block.getType()) != m_usedBlocks.end()) {
            return;
        }

        // 이전 선택 해제
        if (m_selectedBlockType != BlockType::Single) {
            auto prevIt = m_blockButtons.find(m_selectedBlockType);
            if (prevIt != m_blockButtons.end()) {
                prevIt->second->setSelected(false);
            }
        }

        // 새 선택 설정
        m_selectedBlockType = block.getType();
        auto it = m_blockButtons.find(m_selectedBlockType);
        if (it != m_blockButtons.end()) {
            it->second->setSelected(true);
        }

        emit blockSelected(block);
    }

    // ========================================
    // ImprovedGamePalette 구현
    // ========================================

    ImprovedGamePalette::ImprovedGamePalette(QWidget* parent)
        : QWidget(parent)
        , m_currentPlayer(PlayerColor::Blue)
        , m_selectedBlock(BlockType::Single, PlayerColor::Blue)
    {
        setupPalettes();
        updatePlayerAssignments();
    }

    void ImprovedGamePalette::setCurrentPlayer(PlayerColor player)
    {
        if (m_currentPlayer != player) {
            m_currentPlayer = player;
            m_selectedBlock.setPlayer(player);
            updatePlayerAssignments();
        }
    }

    void ImprovedGamePalette::setBlockUsed(PlayerColor player, BlockType blockType)
    {
        m_usedBlocks[player].insert(blockType);

        // 해당 플레이어의 팔레트에서 블록 사용 표시
        DirectionPalette* palette = nullptr;
        if (player == m_currentPlayer) {
            palette = m_southPalette;
        }
        else {
            // 다른 플레이어의 팔레트 찾기
            if (m_northPalette->getPlayer() == player) palette = m_northPalette;
            else if (m_eastPalette->getPlayer() == player) palette = m_eastPalette;
            else if (m_westPalette->getPlayer() == player) palette = m_westPalette;
        }

        if (palette) {
            palette->setBlockUsed(blockType, true);
        }
    }

    void ImprovedGamePalette::resetAllPlayerBlocks()
    {
        m_usedBlocks.clear();

        m_northPalette->resetAllBlocks();
        m_southPalette->resetAllBlocks();
        m_eastPalette->resetAllBlocks();
        m_westPalette->resetAllBlocks();
    }

    void ImprovedGamePalette::setSelectedBlock(const Block& block)
    {
        m_selectedBlock = block;

        // 자신의 팔레트에서만 선택 상태 업데이트
        if (block.getPlayer() == m_currentPlayer) {
            m_southPalette->highlightBlock(block.getType(), true);
        }
    }

    void ImprovedGamePalette::setupPalettes()
    {
        // 4방향 팔레트 생성
        m_northPalette = new DirectionPalette(DirectionPalette::Direction::North, this);
        m_southPalette = new DirectionPalette(DirectionPalette::Direction::South, this);
        m_eastPalette = new DirectionPalette(DirectionPalette::Direction::East, this);
        m_westPalette = new DirectionPalette(DirectionPalette::Direction::West, this);

        // 시그널 연결
        connect(m_northPalette, &DirectionPalette::blockSelected, this, &ImprovedGamePalette::onDirectionBlockSelected);
        connect(m_southPalette, &DirectionPalette::blockSelected, this, &ImprovedGamePalette::onDirectionBlockSelected);
        connect(m_eastPalette, &DirectionPalette::blockSelected, this, &ImprovedGamePalette::onDirectionBlockSelected);
        connect(m_westPalette, &DirectionPalette::blockSelected, this, &ImprovedGamePalette::onDirectionBlockSelected);
    }

    void ImprovedGamePalette::updatePlayerAssignments()
    {
        // 현재 플레이어는 항상 남쪽(하단)에 배치
        m_southPalette->setPlayer(m_currentPlayer);

        // 나머지 플레이어들을 다른 방향에 배치
        std::vector<PlayerColor> otherPlayers;
        std::vector<PlayerColor> allPlayers = {
            PlayerColor::Blue, PlayerColor::Yellow,
            PlayerColor::Red, PlayerColor::Green
        };

        for (PlayerColor player : allPlayers) {
            if (player != m_currentPlayer) {
                otherPlayers.push_back(player);
            }
        }

        // 3명의 상대방을 북, 동, 서에 배치
        if (otherPlayers.size() >= 1) m_northPalette->setPlayer(otherPlayers[0]);
        if (otherPlayers.size() >= 2) m_eastPalette->setPlayer(otherPlayers[1]);
        if (otherPlayers.size() >= 3) m_westPalette->setPlayer(otherPlayers[2]);

        updateBlockAvailability();
    }

    void ImprovedGamePalette::updateBlockAvailability()
    {
        // 각 팔레트의 사용된 블록 상태 업데이트
        for (const auto& playerBlocks : m_usedBlocks) {
            PlayerColor player = playerBlocks.first;
            const auto& usedBlocks = playerBlocks.second;

            DirectionPalette* palette = nullptr;
            if (player == m_currentPlayer) {
                palette = m_southPalette;
            }
            else {
                if (m_northPalette->getPlayer() == player) palette = m_northPalette;
                else if (m_eastPalette->getPlayer() == player) palette = m_eastPalette;
                else if (m_westPalette->getPlayer() == player) palette = m_westPalette;
            }

            if (palette) {
                for (BlockType blockType : usedBlocks) {
                    palette->setBlockUsed(blockType, true);
                }
            }
        }
    }

    void ImprovedGamePalette::onDirectionBlockSelected(const Block& block)
    {
        // 자신의 블록만 선택 가능 (남쪽 팔레트에서만)
        if (block.getPlayer() == m_currentPlayer) {
            m_selectedBlock = block;
            emit blockSelected(block);
        }
    }

} // namespace Blokus

#include "ui/ImprovedBlockPalette.moc"