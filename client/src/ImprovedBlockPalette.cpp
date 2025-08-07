#include "ImprovedBlockPalette.h"
#include "QtAdapter.h"
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

    void BlockButton::updateBlockSize(qreal newSize)
    {
        if (m_blockSize != newSize) {
            m_blockSize = newSize;
            setupGraphics();
            update();
        }
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
        QRect boundingRect = QtAdapter::boundingRectToQRect(m_block.getBoundingRect());

        // 동적 크기 계산 (블록 크기에 비례, 더 작게)
        int padding;
        if (m_blockSize <= 10) {
            padding = 4; // 매우 작은 블록은 최소 여백
        }
        else if (m_blockSize <= 14) {
            padding = 6; // 작은 블록은 적은 여백
        }
        else {
            padding = 8; // 큰 블록은 기본 여백
        }

        int width = boundingRect.width() * m_blockSize + padding * 2;
        int height = boundingRect.height() * m_blockSize + padding * 2;

        // 최소 크기 보장 (더 작게)
        width = std::max(width, static_cast<int>(m_blockSize * 1.2));
        height = std::max(height, static_cast<int>(m_blockSize * 1.2));

        setFixedSize(width, height);

        // 블록 버튼 개별 스타일 설정 (작은 블록용)
        setStyleSheet(
            "BlockButton { "
            "background-color: transparent; "
            "border: none; "
            "border-radius: 3px; "
            "margin: 1px; "
            "} "
            "BlockButton:hover { "
            "background-color: rgba(255, 255, 255, 20); "
            "border: 1px solid #ccc; "
            "}"
        );
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

        // 블록 모양 그리기 (중앙 정렬)
        PositionList shape = m_block.getCurrentShape();
        QRect boundingRect = QtAdapter::boundingRectToQRect(m_block.getBoundingRect());

        // 중앙 정렬을 위한 오프셋 계산
        int offsetX = (width() - boundingRect.width() * m_blockSize) / 2;
        int offsetY = (height() - boundingRect.height() * m_blockSize) / 2;

        // 호버 효과
        if (m_isHovered) {
            baseColor = baseColor.lighter(115);
        }

        // 일반 블록 테두리
        QPen normalPen(baseColor.darker(130), 1);
        painter.setPen(normalPen);
        painter.setBrush(QBrush(baseColor));

        for (const auto& pos : shape) {
            int x = offsetX + pos.second * m_blockSize;
            int y = offsetY + pos.first * m_blockSize;

            // 블록 셀 그리기
            QRect cellRect(x, y, m_blockSize, m_blockSize);
            painter.drawRect(cellRect);

            // 작은 하이라이트 효과 (3D 느낌)
            if (m_blockSize >= 10) {
                painter.setPen(QPen(baseColor.lighter(150), 1));
                painter.drawLine(cellRect.topLeft(), cellRect.topRight());
                painter.drawLine(cellRect.topLeft(), cellRect.bottomLeft());
                painter.setPen(normalPen); // 원래 펜으로 복원
            }
        }

        // 선택된 블록은 전체를 둘러싸는 금색 테두리
        if (m_isSelected) {
            // 전체 블록 영역 계산
            int minX = offsetX + boundingRect.left() * m_blockSize;
            int minY = offsetY + boundingRect.top() * m_blockSize;
            int maxX = offsetX + (boundingRect.right() + 1) * m_blockSize;
            int maxY = offsetY + (boundingRect.bottom() + 1) * m_blockSize;

            // 금색 테두리로 전체 블록 영역 둘러싸기
            QPen selectedPen(QColor(255, 215, 0), 3);
            painter.setPen(selectedPen);
            painter.setBrush(Qt::NoBrush);

            QRect selectionRect(minX - 2, minY - 2, maxX - minX + 4, maxY - minY + 4);
            painter.drawRect(selectionRect);
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

    void DirectionPalette::setupLayout()
    {
        QVBoxLayout* mainLayout = new QVBoxLayout(this);
        mainLayout->setContentsMargins(6, 6, 6, 6);
        mainLayout->setSpacing(3);

        // 블록 컨테이너 (스크롤 없이 직접 배치)
        m_blockContainer = new QWidget();
        m_blockLayout = new QGridLayout(m_blockContainer);
        m_blockLayout->setContentsMargins(8, 8, 8, 8);
        m_blockLayout->setSpacing(6); // 적당한 간격

        // 스크롤 영역 없이 직접 추가
        mainLayout->addWidget(m_blockContainer);

        // 반응형 크기 정책 설정
        setupResponsiveSizing();

        // 베이지색 배경 설정
        setStyleSheet(
            "QWidget { "
            "background-color: #f5f5dc; "
            "border: 2px solid #d4c5a0; "
            "border-radius: 8px; "
            "}"
        );
    }

    void DirectionPalette::setupResponsiveSizing()
    {
        switch (m_direction) {
        case Direction::South:
            // 남쪽: 가로 확장, 세로 고정
            setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Fixed);
            setMinimumHeight(150);
            setMaximumHeight(220);
            setMinimumWidth(600);  // 최소 너비 보장
            break;

        case Direction::North:
            // 북쪽: 가로 확장, 세로 고정
            setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Fixed);
            setMinimumHeight(100);
            setMaximumHeight(140);
            setMinimumWidth(500);  // 최소 너비 보장
            break;

        case Direction::East:
        case Direction::West:
            // 동서쪽: 가로 고정, 세로 확장
            setSizePolicy(QSizePolicy::Fixed, QSizePolicy::Expanding);
            setMinimumWidth(120);
            setMaximumWidth(180);
            setMinimumHeight(300);  // 최소 높이 보장
            break;
        }
    }

    void DirectionPalette::removeBlock(BlockType blockType)
    {
        qDebug() << QString::fromUtf8("DirectionPalette::removeBlock 호출됨: %1")
            .arg(BlockFactory::getBlockName(blockType));

        auto it = m_blockButtons.find(blockType);
        if (it != m_blockButtons.end()) {
            qDebug() << QString::fromUtf8("블록 버튼 찾음, 제거 시작");

            // 버튼을 레이아웃에서 제거
            m_blockLayout->removeWidget(it->second);

            // 버튼 완전히 삭제
            it->second->setParent(nullptr);
            it->second->deleteLater();

            // 맵에서 제거
            m_blockButtons.erase(it);

            // 블록 목록에서도 제거
            auto blockIt = std::find_if(m_blocks.begin(), m_blocks.end(),
                [blockType](const Block& block) {
                    return block.getType() == blockType;
                });

            if (blockIt != m_blocks.end()) {
                m_blocks.erase(blockIt);
                qDebug() << QString::fromUtf8("블록 목록에서도 제거됨");
            }

            // 사용된 블록 목록에 추가
            m_usedBlocks.insert(blockType);

            // 즉시 레이아웃 재정렬
            QTimer::singleShot(0, this, [this]() {
                reorganizeLayout();
                update();
                qDebug() << QString::fromUtf8("레이아웃 재정렬 완료, 남은 블록 수: %1")
                    .arg(m_blockButtons.size());
                });

        }
        else {
            qDebug() << QString::fromUtf8("제거할 블록 버튼을 찾을 수 없음: %1")
                .arg(BlockFactory::getBlockName(blockType));
        }
    }

    void DirectionPalette::resetAllBlocks()
    {
        qDebug() << QString::fromUtf8("🔄 DirectionPalette::resetAllBlocks 시작 (%1)")
            .arg(getDirectionName());

        // 1단계: 모든 버튼 안전하게 삭제
        for (auto& pair : m_blockButtons) {
            if (pair.second) {
                if (m_blockLayout) {
                    m_blockLayout->removeWidget(pair.second);
                }
                pair.second->setParent(nullptr);
                pair.second->deleteLater();
            }
        }
        m_blockButtons.clear();
        qDebug() << QString::fromUtf8("✅ 모든 버튼 삭제됨");

        // 2단계: 레이아웃 완전히 클리어 (안전하게)
        if (m_blockLayout) {
            QLayoutItem* item;
            while ((item = m_blockLayout->takeAt(0)) != nullptr) {
                delete item;
            }
        }
        qDebug() << QString::fromUtf8("✅ 레이아웃 클리어됨");

        // 3단계: 상태 초기화
        m_usedBlocks.clear();
        m_selectedBlockType = BlockType::Single;

        // 4단계: 블록 목록 재생성 (안전하게)
        if (m_player != PlayerColor::None) {
            m_blocks.clear();
            auto allTypes = BlockFactory::getAllBlockTypes();
            for (BlockType type : allTypes) {
                m_blocks.emplace_back(type, m_player);
            }
            qDebug() << QString::fromUtf8("✅ 블록 목록 재생성됨: %1개").arg(m_blocks.size());

            // 5단계: 버튼 재생성 (지연 실행으로 안전하게)
            QTimer::singleShot(100, this, [this]() {
                updateBlockButtons();
                });
        }

        qDebug() << QString::fromUtf8("🎉 팔레트 리셋 완료!");
    }


    void DirectionPalette::highlightBlock(BlockType blockType, bool highlight)
    {
        auto it = m_blockButtons.find(blockType);
        if (it != m_blockButtons.end()) {
            it->second->setSelected(highlight);
            m_selectedBlockType = highlight ? blockType : BlockType::Single;
        }
    }

    void DirectionPalette::updateBlockButtons()
    {
        if (!m_blockLayout || !m_blockContainer) {
            qDebug() << QString::fromUtf8("❌ 레이아웃이 초기화되지 않음");
            return;
        }

        qDebug() << QString::fromUtf8("🎨 DirectionPalette::updateBlockButtons 시작 (%1)")
            .arg(getDirectionName());

        // 기존 버튼들 안전하게 제거
        for (auto& pair : m_blockButtons) {
            if (pair.second) {
                m_blockLayout->removeWidget(pair.second);
                pair.second->setParent(nullptr);
                pair.second->deleteLater();
            }
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
        int createdButtons = 0;

        // 사용되지 않은 블록들만 버튼 생성
        for (const Block& block : m_blocks) {
            // 사용된 블록은 완전히 스킵
            if (m_usedBlocks.find(block.getType()) != m_usedBlocks.end()) {
                continue;
            }

            try {
                BlockButton* button = new BlockButton(block, blockSize, m_blockContainer);
                connect(button, &BlockButton::blockClicked, this, &DirectionPalette::onBlockButtonClicked);

                m_blockLayout->addWidget(button, row, col);
                m_blockButtons[block.getType()] = button;

                createdButtons++;
                col++;
                if (col >= maxPerRow) {
                    col = 0;
                    row++;
                }
            }
            catch (...) {
                qDebug() << QString::fromUtf8("❌ 버튼 생성 실패: %1")
                    .arg(BlockFactory::getBlockName(block.getType()));
            }
        }

        qDebug() << QString::fromUtf8("✅ %1 방향 팔레트: %2개 버튼 생성됨")
            .arg(getDirectionName()).arg(createdButtons);

        // 안전한 업데이트
        if (m_blockContainer) {
            m_blockContainer->updateGeometry();
            m_blockContainer->update();
        }
        updateGeometry();
        update();
    }

    void DirectionPalette::forceLayoutUpdate()
    {
        // 즉시 레이아웃 업데이트
        if (m_blockContainer) {
            m_blockContainer->updateGeometry();
            m_blockContainer->update();
        }
        updateGeometry();
        update();

        // 부모 위젯도 업데이트
        if (parentWidget()) {
            parentWidget()->updateGeometry();
            parentWidget()->update();
        }

        // 🔧 FIX: Replace processEvents with update() to avoid UI blocking
        // processEvents() can freeze the main thread - use update() for async refresh
        update();

        qDebug() << QString::fromUtf8("✅ %1 방향 팔레트 레이아웃 업데이트 완료")
            .arg(getDirectionName());
    }

    void DirectionPalette::reorganizeLayout()
    {
        qDebug() << QString::fromUtf8("🔄 %1 방향 팔레트 재배치 (크기: %2x%3)")
            .arg(getDirectionName()).arg(width()).arg(height());

        // 현재 존재하는 버튼들 수집
        QList<BlockButton*> buttons;
        for (auto& pair : m_blockButtons) {
            if (pair.second) {
                buttons.append(pair.second);
            }
        }

        // 레이아웃에서 모든 아이템 제거
        QLayoutItem* item;
        while ((item = m_blockLayout->takeAt(0)) != nullptr) {
            delete item;
        }

        // 새로운 크기에 맞춰 재배치
        qreal newBlockSize = getBlockSize();
        int maxPerRow = getMaxBlocksPerRow();
        int row = 0, col = 0;

        for (BlockButton* button : buttons) {
            if (button && button->parent() == m_blockContainer) {
                // 버튼 크기도 동적으로 조정
                button->updateBlockSize(newBlockSize);

                m_blockLayout->addWidget(button, row, col);

                col++;
                if (col >= maxPerRow) {
                    col = 0;
                    row++;
                }
            }
        }

        qDebug() << QString::fromUtf8("✅ 재배치 완료: %1개 버튼, %2열")
            .arg(buttons.size()).arg(maxPerRow);

        forceLayoutUpdate();
    }

    qreal DirectionPalette::getBlockSize() const
    {
        // 고정된 블록 크기로 변경 (일관성 확보)
        switch (m_direction) {
        case Direction::South:
            // 남쪽 (나): 고정 크기
            return 12.0;

        case Direction::North:
            // 북쪽: 고정 크기 (더 작게)
            return 10.0;

        case Direction::East:
        case Direction::West:
            // 동서쪽: 고정 크기 유지
            return 10.0;
        }
        return 12.0;
    }

    int DirectionPalette::getMaxBlocksPerRow() const
    {
        // 고정된 열 수로 변경 (일관성 확보)
        switch (m_direction) {
        case Direction::South:
            // 남쪽: 고정 열 수
            return 12;

        case Direction::North:
            // 북쪽: 고정 열 수 (더 많이)
            return 15;

        case Direction::East:
        case Direction::West:
            // 동서쪽: 고정 유지
            return 3;
        }
        return 8;
    }

    void DirectionPalette::resizeEvent(QResizeEvent* event)
    {
        QWidget::resizeEvent(event);

        // 크래시 방지: 초기화가 완료된 후에만 재배치
        if (m_blockButtons.size() > 0 && m_blockContainer && m_blockLayout) {
            // 즉시 재배치하지 말고 안전한 지연 실행
            static bool isReorganizing = false;
            if (!isReorganizing) {
                isReorganizing = true;
                QTimer::singleShot(200, this, [this]() {
                    static bool inProgress = false;
                    if (!inProgress) {
                        inProgress = true;
                        safeReorganizeLayout();
                        inProgress = false;
                    }
                    isReorganizing = false;
                    });
            }
        }
    }

    void DirectionPalette::safeReorganizeLayout()
    {
        // 안전한 재배치 함수
        if (!m_blockLayout || !m_blockContainer) {
            qDebug() << QString::fromUtf8("❌ 레이아웃이 초기화되지 않음");
            return;
        }

        qDebug() << QString::fromUtf8("🔄 %1 방향 팔레트 안전 재배치 시작")
            .arg(getDirectionName());

        // 현재 존재하는 버튼들 수집
        QList<BlockButton*> validButtons;
        for (auto it = m_blockButtons.begin(); it != m_blockButtons.end(); ) {
            if (it->second && it->second->parent() == m_blockContainer) {
                validButtons.append(it->second);
                ++it;
            }
            else {
                // 유효하지 않은 버튼은 맵에서 제거
                if (it->second) {
                    it->second->deleteLater();
                }
                it = m_blockButtons.erase(it);
            }
        }

        if (validButtons.isEmpty()) {
            qDebug() << QString::fromUtf8("⚠️ 유효한 버튼이 없음");
            return;
        }

        // 레이아웃에서 모든 아이템 안전하게 제거
        QLayoutItem* item;
        while ((item = m_blockLayout->takeAt(0)) != nullptr) {
            delete item; // QLayoutItem만 삭제
        }

        // 새로운 크기에 맞춰 재배치
        int maxPerRow = getMaxBlocksPerRow();
        int row = 0, col = 0;

        for (BlockButton* button : validButtons) {
            if (button && button->parent() == m_blockContainer) {
                m_blockLayout->addWidget(button, row, col);

                col++;
                if (col >= maxPerRow) {
                    col = 0;
                    row++;
                }
            }
        }

        qDebug() << QString::fromUtf8("✅ 안전 재배치 완료: %1개 버튼, %2열")
            .arg(validButtons.size()).arg(maxPerRow);

        // 강제 업데이트 (안전하게)
        if (m_blockContainer) {
            m_blockContainer->updateGeometry();
            m_blockContainer->update();
        }
        updateGeometry();
        update();
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
        qDebug() << QString::fromUtf8("블록 클릭됨: %1 (플레이어: %2, 방향: %3)")
            .arg(BlockFactory::getBlockName(block.getType()))
            .arg(Utils::playerColorToString(block.getPlayer()))
            .arg(getDirectionName());

        // 상대방 블록은 클릭 불가능
        if (m_direction != Direction::South) {
            qDebug() << QString::fromUtf8("❌ 상대방 블록 - 클릭 무시");
            return;
        }

        // 사용된 블록은 선택 불가
        if (m_usedBlocks.find(block.getType()) != m_usedBlocks.end()) {
            qDebug() << QString::fromUtf8("❌ 사용된 블록 - 선택 불가");
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

        qDebug() << QString::fromUtf8("✅ 블록 선택 성공: %1")
            .arg(BlockFactory::getBlockName(block.getType()));

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
        , m_fixedPlayer(PlayerColor::Blue)  // 고정된 플레이어 (항상 파랑)
    {
        setupPalettes();
        setupFixedPlayerAssignments();  // 고정 할당
    }

    void ImprovedGamePalette::setupFixedPlayerAssignments()
    {
        qDebug() << QString::fromUtf8("고정 플레이어 할당 설정");

        // 고정 플레이어 할당 (변경되지 않음)
        m_fixedPlayer = PlayerColor::Blue;  // 항상 파랑 플레이어

        // 남쪽(하단)은 항상 파랑 플레이어 (나)
        if (m_southPalette) {
            m_southPalette->setPlayer(PlayerColor::Blue);
            qDebug() << QString::fromUtf8("남쪽 팔레트: 파랑 (나의 블록)");
        }

        // 다른 방향은 다른 플레이어들
        if (m_northPalette) {
            m_northPalette->setPlayer(PlayerColor::Yellow);
            qDebug() << QString::fromUtf8("북쪽 팔레트: 노랑");
        }
        if (m_eastPalette) {
            m_eastPalette->setPlayer(PlayerColor::Red);
            qDebug() << QString::fromUtf8("동쪽 팔레트: 빨강");
        }
        if (m_westPalette) {
            m_westPalette->setPlayer(PlayerColor::Green);
            qDebug() << QString::fromUtf8("서쪽 팔레트: 초록");
        }
    }

    Block ImprovedGamePalette::getSelectedBlock() const
    {
        // 내 턴이고 선택이 있을 때만 반환
        if (m_hasSelection && m_currentPlayer == m_fixedPlayer) {
            return m_selectedBlock;
        }
        // 내 턴이 아니거나 선택이 없으면 빈 블록 반환
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
            qDebug() << QString::fromUtf8("현재 플레이어 변경: %1")
                .arg(Utils::playerColorToString(player));

            // 중요: 팔레트 재할당하지 않음! 고정 유지
            // updatePlayerAssignments(); // 이 줄 제거

            // 내 턴이 아니면 선택 해제
            if (player != m_fixedPlayer) {
                clearSelection();
                qDebug() << QString::fromUtf8("내 턴이 아님 - 선택 해제");
            }
        }
    }

    void ImprovedGamePalette::resetAllPlayerBlocks()
    {
        qDebug() << QString::fromUtf8("=== 모든 플레이어 블록 리셋 ===");

        // 제거된 블록 목록 초기화
        m_removedBlocks.clear();
        clearSelection();

        // 모든 팔레트 재생성
        if (m_northPalette) m_northPalette->resetAllBlocks();
        if (m_southPalette) m_southPalette->resetAllBlocks();
        if (m_eastPalette) m_eastPalette->resetAllBlocks();
        if (m_westPalette) m_westPalette->resetAllBlocks();

        // 고정 플레이어 재할당 (변경되지 않는 할당)
        setupFixedPlayerAssignments();

        qDebug() << QString::fromUtf8("모든 팔레트 리셋 완료");
    }

    void ImprovedGamePalette::removeBlock(PlayerColor player, BlockType blockType)
    {
        qDebug() << QString::fromUtf8("ImprovedGamePalette::removeBlock 호출됨: %1 플레이어의 %2 블록")
            .arg(Utils::playerColorToString(player))
            .arg(BlockFactory::getBlockName(blockType));

        // 제거된 블록 목록에 추가
        m_removedBlocks[player].insert(blockType);

        // 해당 플레이어의 팔레트 찾기
        DirectionPalette* palette = nullptr;
        QString paletteDirection;

        if (player == m_currentPlayer) {
            palette = m_southPalette;
            paletteDirection = "South (나의 블록)";
        }
        else {
            if (m_northPalette && m_northPalette->getPlayer() == player) {
                palette = m_northPalette;
                paletteDirection = "North";
            }
            else if (m_eastPalette && m_eastPalette->getPlayer() == player) {
                palette = m_eastPalette;
                paletteDirection = "East";
            }
            else if (m_westPalette && m_westPalette->getPlayer() == player) {
                palette = m_westPalette;
                paletteDirection = "West";
            }
        }

        if (palette) {
            qDebug() << QString::fromUtf8("팔레트 찾음: %1, 블록 제거 요청")
                .arg(paletteDirection);

            // 해당 팔레트에서 블록 제거
            palette->removeBlock(blockType);

            qDebug() << QString::fromUtf8("팔레트에서 블록 제거 완료");
        }
        else {
            qDebug() << QString::fromUtf8("경고: 해당 플레이어의 팔레트를 찾을 수 없음: %1")
                .arg(Utils::playerColorToString(player));
        }

        // 현재 선택된 블록이 제거된 블록이면 선택 해제
        if (m_hasSelection && m_selectedBlock.getType() == blockType &&
            m_selectedBlock.getPlayer() == player) {
            qDebug() << QString::fromUtf8("현재 선택된 블록이 제거됨, 선택 해제");
            clearSelection();
        }

        qDebug() << QString::fromUtf8("블록 제거 완료: %1개 블록이 제거됨")
            .arg(m_removedBlocks[player].size());
    }

    void ImprovedGamePalette::setupPalettes()
    {
        qDebug() << QString::fromUtf8("ImprovedGamePalette::setupPalettes 호출됨");

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

        qDebug() << QString::fromUtf8("4방향 팔레트 생성 및 시그널 연결 완료");
    }

    void ImprovedGamePalette::updatePlayerAssignments()
    {
        qDebug() << QString::fromUtf8("ImprovedGamePalette::updatePlayerAssignments 호출됨");

        // 현재 플레이어는 항상 남쪽(하단)에 배치
        if (m_southPalette) {
            m_southPalette->setPlayer(m_currentPlayer);
            qDebug() << QString::fromUtf8("남쪽 팔레트에 %1 플레이어 할당")
                .arg(Utils::playerColorToString(m_currentPlayer));
        }

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
        if (otherPlayers.size() >= 1 && m_northPalette) {
            m_northPalette->setPlayer(otherPlayers[0]);
            qDebug() << QString::fromUtf8("북쪽 팔레트에 %1 플레이어 할당")
                .arg(Utils::playerColorToString(otherPlayers[0]));
        }
        if (otherPlayers.size() >= 2 && m_eastPalette) {
            m_eastPalette->setPlayer(otherPlayers[1]);
            qDebug() << QString::fromUtf8("동쪽 팔레트에 %1 플레이어 할당")
                .arg(Utils::playerColorToString(otherPlayers[1]));
        }
        if (otherPlayers.size() >= 3 && m_westPalette) {
            m_westPalette->setPlayer(otherPlayers[2]);
            qDebug() << QString::fromUtf8("서쪽 팔레트에 %1 플레이어 할당")
                .arg(Utils::playerColorToString(otherPlayers[2]));
        }

        updateBlockAvailability();

        qDebug() << QString::fromUtf8("플레이어 할당 완료");
    }

    void ImprovedGamePalette::updateBlockAvailability()
    {
        qDebug() << QString::fromUtf8("ImprovedGamePalette::updateBlockAvailability 호출됨");

        // 각 팔레트의 제거된 블록 상태 업데이트
        for (const auto& playerBlocks : m_removedBlocks) {
            PlayerColor player = playerBlocks.first;
            const auto& removedBlocks = playerBlocks.second;

            DirectionPalette* palette = nullptr;
            if (player == m_currentPlayer) {
                palette = m_southPalette;
            }
            else {
                if (m_northPalette && m_northPalette->getPlayer() == player) palette = m_northPalette;
                else if (m_eastPalette && m_eastPalette->getPlayer() == player) palette = m_eastPalette;
                else if (m_westPalette && m_westPalette->getPlayer() == player) palette = m_westPalette;
            }

            if (palette) {
                for (BlockType blockType : removedBlocks) {
                    palette->removeBlock(blockType);
                }
                qDebug() << QString::fromUtf8("%1 플레이어의 %2개 블록 제거됨")
                    .arg(Utils::playerColorToString(player))
                    .arg(removedBlocks.size());
            }
        }

        qDebug() << QString::fromUtf8("블록 가용성 업데이트 완료");
    }

    void ImprovedGamePalette::onDirectionBlockSelected(const Block& block)
    {
        qDebug() << QString::fromUtf8("블록 선택 시도: %1 (플레이어: %2)")
            .arg(BlockFactory::getBlockName(block.getType()))
            .arg(Utils::playerColorToString(block.getPlayer()));

        // 오직 내 턴이고, 내 블록(파랑)일 때만 선택 가능
        if (m_currentPlayer != m_fixedPlayer) {
            qDebug() << QString::fromUtf8("❌ 내 턴이 아님 - 선택 불가");
            return;
        }

        if (block.getPlayer() != m_fixedPlayer) {
            qDebug() << QString::fromUtf8("❌ 내 블록이 아님 - 선택 불가");
            return;
        }

        // 남쪽 팔레트(내 블록)에서만 선택 가능
        QObject* sender = QObject::sender();
        if (sender != m_southPalette) {
            qDebug() << QString::fromUtf8("❌ 내 팔레트가 아님 - 선택 불가");
            return;
        }

        // 이전 선택 해제
        clearSelection();

        m_selectedBlock = block;
        m_hasSelection = true;

        qDebug() << QString::fromUtf8("✅ 블록 선택 성공: %1")
            .arg(BlockFactory::getBlockName(block.getType()));

        emit blockSelected(block);
    }

} // namespace Blokus

#include "ui/ImprovedBlockPalette.moc"