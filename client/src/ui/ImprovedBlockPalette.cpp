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
            QWidget::setEnabled(!used);
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
        int width = boundingRect.width() * m_blockSize + 4; // 여백 최소화
        int height = boundingRect.height() * m_blockSize + 4;

        // 최소 크기 보장
        width = std::max(width, static_cast<int>(m_blockSize + 4));
        height = std::max(height, static_cast<int>(m_blockSize + 4));

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

        // 선택된 블록은 하이라이트
        if (m_isSelected) {
            painter.setPen(QPen(QColor(255, 215, 0), 2)); // 금색 테두리 (더 얇게)
        }
        else if (m_isHovered && !m_isUsed) {
            painter.setPen(QPen(baseColor.lighter(130), 1));
        }
        else {
            painter.setPen(QPen(baseColor.darker(120), 1));
        }

        // 호버 효과
        if (m_isHovered && !m_isUsed) {
            baseColor = baseColor.lighter(110);
        }

        painter.setBrush(QBrush(baseColor));

        // 블록 모양 그리기 (여백 최소화)
        PositionList shape = m_block.getCurrentShape();
        for (const auto& pos : shape) {
            int x = pos.second * m_blockSize + 2;
            int y = pos.first * m_blockSize + 2;

            // 작은 블록의 경우 테두리 없이 그리기
            if (m_blockSize <= 10) {
                painter.setPen(Qt::NoPen);
            }

            painter.drawRect(x, y, m_blockSize, m_blockSize);
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

    void DirectionPalette::removeBlock(BlockType blockType)
    {
        auto it = m_blockButtons.find(blockType);
        if (it != m_blockButtons.end()) {
            // 버튼을 레이아웃에서 제거하고 삭제
            m_blockLayout->removeWidget(it->second);
            it->second->setParent(nullptr);  // 부모 관계 해제
            it->second->deleteLater();       // 안전한 삭제
            m_blockButtons.erase(it);

            // 블록 목록에서도 제거
            m_blocks.erase(
                std::remove_if(m_blocks.begin(), m_blocks.end(),
                    [blockType](const Block& block) {
                        return block.getType() == blockType;
                    }),
                m_blocks.end()
            );

            // 사용된 블록 목록에 추가
            m_usedBlocks.insert(blockType);

            // 레이아웃 즉시 재정렬
            reorganizeLayout();

            qDebug() << QString::fromUtf8("블록 제거됨: %1, 남은 블록 수: %2")
                .arg(BlockFactory::getBlockName(blockType))
                .arg(m_blockButtons.size());
        }
    }

    void DirectionPalette::resetAllBlocks()
    {
        m_usedBlocks.clear();

        // 모든 버튼 삭제
        for (auto& pair : m_blockButtons) {
            delete pair.second;
        }
        m_blockButtons.clear();

        // 블록 목록 재생성
        if (m_player != PlayerColor::None) {
            m_blocks.clear();
            auto allTypes = BlockFactory::getAllBlockTypes();
            for (BlockType type : allTypes) {
                m_blocks.emplace_back(type, m_player);
            }
        }

        // 버튼 재생성
        updateBlockButtons();
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
        mainLayout->setContentsMargins(2, 2, 2, 2);
        mainLayout->setSpacing(1);

        // 방향 라벨 (남쪽은 더 큰 글자)
        QString labelText;
        switch (m_direction) {
        case Direction::North: labelText = QString::fromUtf8("상대"); break;
        case Direction::South: labelText = QString::fromUtf8("나의 블록"); break;
        case Direction::East: labelText = QString::fromUtf8("상대"); break;
        case Direction::West: labelText = QString::fromUtf8("상대"); break;
        }

        QLabel* directionLabel = new QLabel(labelText);
        if (m_direction == Direction::South) {
            directionLabel->setStyleSheet("font-weight: bold; font-size: 14px; color: #2c3e50; padding: 3px;");
            directionLabel->setAlignment(Qt::AlignCenter);
        }
        else {
            directionLabel->setStyleSheet("font-size: 9px; color: #7f8c8d; padding: 1px;");
            directionLabel->setAlignment(Qt::AlignCenter);
        }
        mainLayout->addWidget(directionLabel);

        // 스크롤 영역 제거하고 직접 컨테이너 사용
        m_blockContainer = new QWidget();
        m_blockLayout = new QGridLayout(m_blockContainer);
        m_blockLayout->setContentsMargins(1, 1, 1, 1);
        m_blockLayout->setSpacing(1);

        // 스크롤 영역 없이 직접 추가
        mainLayout->addWidget(m_blockContainer);

        // 크기 제약 설정 (더 작게 조정)
        if (m_direction == Direction::South) {
            setMinimumHeight(100);
            setMaximumHeight(130);
        }
        else {
            // 북/동/서쪽은 더 작게
            setMinimumWidth(60);
            setMaximumWidth(90);
            setMinimumHeight(200);
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
            // 이미 제거된 블록은 버튼 생성 안함
            if (m_usedBlocks.find(block.getType()) != m_usedBlocks.end()) {
                continue;
            }

            BlockButton* button = new BlockButton(block, blockSize);

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

        // 레이아웃 업데이트 강제
        m_blockContainer->updateGeometry();
        updateGeometry();
    }

    void DirectionPalette::reorganizeLayout()
    {
        // 현재 존재하는 버튼들만 다시 배치
        QList<BlockButton*> buttons;
        for (auto& pair : m_blockButtons) {
            buttons.append(pair.second);
        }

        // 레이아웃 클리어 (위젯 삭제 안함)
        QLayoutItem* item;
        while ((item = m_blockLayout->takeAt(0)) != nullptr) {
            delete item; // QLayoutItem만 삭제
        }

        // 버튼들을 다시 배치
        int maxPerRow = getMaxBlocksPerRow();
        int row = 0, col = 0;

        for (BlockButton* button : buttons) {
            if (button) {
                m_blockLayout->addWidget(button, row, col);

                col++;
                if (col >= maxPerRow) {
                    col = 0;
                    row++;
                }
            }
        }

        // 레이아웃 업데이트 강제
        m_blockContainer->updateGeometry();
        updateGeometry();
        update();
    }

    qreal DirectionPalette::getBlockSize() const
    {
        switch (m_direction) {
        case Direction::South: return 20.0; // 자신의 블록 (중간 크기)
        default: return 8.0;                // 상대방 블록 (매우 작게)
        }
    }

    int DirectionPalette::getMaxBlocksPerRow() const
    {
        switch (m_direction) {
        case Direction::South: return 8;    // 남쪽은 가로로 많이
        case Direction::North: return 8;    // 북쪽도 가로로 많이
        case Direction::East:
        case Direction::West: return 3;     // 동서쪽은 3개씩
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
        , m_hasSelection(false)
    {
        setupPalettes();
        updatePlayerAssignments();
    }

    Block ImprovedGamePalette::getSelectedBlock() const
    {
        if (m_hasSelection) {
            return m_selectedBlock;
        }
        // 선택이 없으면 빈 블록 반환
        return Block(BlockType::Single, PlayerColor::None);
    }

    void ImprovedGamePalette::setSelectedBlock(const Block& block)
    {
        m_selectedBlock = block;
        m_hasSelection = true;

        // 자신의 팔레트에서만 선택 상태 업데이트
        if (block.getPlayer() == m_currentPlayer) {
            m_southPalette->highlightBlock(block.getType(), true);
        }
    }

    void ImprovedGamePalette::clearSelection()
    {
        if (m_hasSelection) {
            // 이전 선택 해제
            m_southPalette->highlightBlock(m_selectedBlock.getType(), false);
            m_hasSelection = false;
        }
    }

    void ImprovedGamePalette::setCurrentPlayer(PlayerColor player)
    {
        if (m_currentPlayer != player) {
            m_currentPlayer = player;
            m_selectedBlock.setPlayer(player);
            updatePlayerAssignments();
        }
    }

    void ImprovedGamePalette::removeBlock(PlayerColor player, BlockType blockType)
    {
        m_removedBlocks[player].insert(blockType);

        // 해당 플레이어의 팔레트에서 블록 제거
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
            palette->removeBlock(blockType);
        }

        // 현재 선택된 블록이 제거된 블록이면 선택 해제
        if (m_hasSelection && m_selectedBlock.getType() == blockType &&
            m_selectedBlock.getPlayer() == player) {
            clearSelection();
        }
    }

    void ImprovedGamePalette::resetAllPlayerBlocks()
    {
        m_removedBlocks.clear();
        clearSelection();

        // 모든 팔레트 재생성
        m_northPalette->resetAllBlocks();
        m_southPalette->resetAllBlocks();
        m_eastPalette->resetAllBlocks();
        m_westPalette->resetAllBlocks();

        updatePlayerAssignments();
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
        // 각 팔레트의 제거된 블록 상태 업데이트
        for (const auto& playerBlocks : m_removedBlocks) {
            PlayerColor player = playerBlocks.first;
            const auto& removedBlocks = playerBlocks.second;

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
                for (BlockType blockType : removedBlocks) {
                    palette->removeBlock(blockType);
                }
            }
        }
    }

    void ImprovedGamePalette::onDirectionBlockSelected(const Block& block)
    {
        // 자신의 블록만 선택 가능 (남쪽 팔레트에서만)
        if (block.getPlayer() == m_currentPlayer) {
            // 이전 선택 해제
            clearSelection();

            m_selectedBlock = block;
            m_hasSelection = true;
            emit blockSelected(block);
        }
    }

} // namespace Blokus

#include "ui/ImprovedBlockPalette.moc"