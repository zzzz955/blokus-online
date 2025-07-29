#pragma once

#include <QGraphicsView>
#include <QGraphicsScene>
#include <QGraphicsRectItem>
#include <QTimer>
#include <QMouseEvent>
#include <QKeyEvent>
#include <QWheelEvent>
#include <QPen>
#include <QBrush>
#include <QColor>
#include <vector>
#include <map>

#include "ClientTypes.h"      // 🔥 이 줄 추가 (BOARD_SIZE 포함)
#include "ClientBlock.h"
#include "ClientLogic.h"

// Forward declarations
namespace Blokus {
    class AfkNotificationDialog;
}

namespace Blokus {

    class GameBoard : public QGraphicsView
    {
        Q_OBJECT

    public:
        // 🔥 BOARD_SIZE 상수 명시적 정의 (Types.h에서 가져오지 못하는 경우 대비)
        static constexpr int BOARD_SIZE = 20;  // 또는 Common::BOARD_SIZE 사용
        static constexpr qreal DEFAULT_CELL_SIZE = 25.0;

        explicit GameBoard(QWidget* parent = nullptr);
        ~GameBoard();

        // 게임 로직 연동
        void setGameLogic(GameLogic* gameLogic);
        bool tryPlaceCurrentBlock(const Position& position);
        void setSelectedBlock(const Block& block);

        // 기본 보드 상태
        bool isCellValid(int row, int col) const;
        bool isCellOccupied(int row, int col) const;
        PlayerColor getCellOwner(int row, int col) const;

        // 시각적 효과
        void highlightCell(int row, int col, const QColor& color);
        void clearHighlights();

        // 좌표 변환
        Position screenToBoard(const QPointF& screenPos) const;
        QPointF boardToScreen(const Position& boardPos) const;

        // 색상 관리
        QColor getPlayerColor(PlayerColor player) const;

        // 반응형 게임보드 관련
        qreal calculateResponsiveCellSize() const;
        void updateResponsiveLayout();
        void rebuildBoard();
        void fitBoardToView();
        void redrawAllBlocks();
        void drawCellWithColor(const Position& pos, const QColor& color);
        bool isValidPosition(const Position& pos) const;

        // 보드 관리
        void setBoardReadOnly(bool readOnly);
        void resetBoard();

        // 블록 배치 관련
        bool canPlaceBlock(const BlockPlacement& placement) const;
        bool placeBlock(const BlockPlacement& placement);
        void removeBlock(const Position& position);
        void showBlockPreview(const BlockPlacement& placement);
        void hideBlockPreview();

        // 블록 렌더링 관련
        void addBlockToBoard(const Block& block, const Position& position);
        void removeBlockFromBoard(const Position& position);
        void clearAllBlocks();

        void clearSelection();
        void setBlockSelected(bool selected);

        // AFK 알림 처리
        void showAfkNotification(const QString& jsonData);
        void showAfkNotification(int timeoutCount, int maxCount);

    signals:
        void cellClicked(int row, int col);
        void cellHovered(int row, int col);
        void blockPlaced(const BlockPlacement& placement);
        void blockRemoved(const Position& position);
        void blockRotated(const Block& block);
        void blockFlipped(const Block& block);
        void blockPlacedSuccessfully(BlockType blockType, PlayerColor player, int row, int col, int rotation, int flip);
        
        // AFK 관련 시그널
        void afkUnblockRequested();

    protected:
        void mousePressEvent(QMouseEvent* event) override;
        void mouseMoveEvent(QMouseEvent* event) override;
        void wheelEvent(QWheelEvent* event) override;
        void keyPressEvent(QKeyEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;
        void leaveEvent(QEvent* event) override;
        void focusInEvent(QFocusEvent* event) override;
        void focusOutEvent(QFocusEvent* event) override;

    private slots:
        void onSceneChanged();

    private:
        // 초기화
        void setupScene();
        void setupStyles();
        void initializeBoard();
        void clearBoard();

        // 격자 그리기
        void drawGrid();
        void drawStartingCorners();

        // 블록 그래픽 관련
        BlockGraphicsItem* createBlockGraphicsItem(const Block& block, const Position& position);
        bool isValidBlockPlacement(const Block& block, const Position& position) const;
        bool checkBlokusRules(const Block& block, const Position& position, PlayerColor player) const;
        QColor getPlayerBrushColor(PlayerColor player) const;
        QColor getPlayerBorderColor(PlayerColor player) const;

        // 미리보기 관련
        void showCurrentBlockPreview();

        // 테스트/디버깅 함수들
        void addTestBlocks();
        void onShowAllBlocks();
        void onClearAllBlocks();
        void onAddRandomBlock();

        // 게임 시작 여부 확인
        bool isGameStarted() const;

        // UI 컴포넌트
        QGraphicsScene* m_scene;
        QGraphicsRectItem* m_boardRect;

        // 🔥 보드 상태 - 동적 배열로 변경 (크기 문제 해결)
        std::vector<std::vector<PlayerColor>> m_board;  // 2D vector 사용
        bool m_readOnly;

        // 시각적 요소
        qreal m_cellSize;
        std::vector<QGraphicsRectItem*> m_gridCells;
        std::vector<QGraphicsRectItem*> m_highlights;
        std::vector<QGraphicsItem*> m_previewItems;

        // 마우스/키보드 상태
        Position m_hoveredCell;
        bool m_mousePressed;
        QTimer* m_hoverTimer;

        // 블록 관리
        std::vector<BlockGraphicsItem*> m_blockItems;
        std::map<Position, BlockGraphicsItem*> m_blockMap;
        BlockGraphicsItem* m_currentPreview;

        // 선택된 블록
        Block m_selectedBlock;
        bool m_hasSelectedBlock;
        int m_testBlockIndex;

        // 게임 로직 연동
        GameLogic* m_gameLogic;

        // 스타일
        QPen m_gridPen;
        QPen m_borderPen;
        QBrush m_emptyBrush;
        QBrush m_highlightBrush;
        std::map<PlayerColor, QColor> m_playerColors;

        bool m_blockSelected;
        
        // AFK 알림 대화상자
        Blokus::AfkNotificationDialog* m_afkDialog;
    };

} // namespace Blokus