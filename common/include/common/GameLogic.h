#pragma once

#include "Types.h"
#include <map>
#include <set>
#include <vector>

namespace Blokus {
    namespace Common {

        // ========================================
        // GameLogic 클래스 (서버와 클라이언트 공유)
        // ========================================

        class GameLogic
        {
        public:
            GameLogic();

            // 보드 관리
            void initializeBoard();
            void clearBoard();

            // 셀 상태 확인
            PlayerColor getCellOwner(const Position& pos) const;
            bool isCellOccupied(const Position& pos) const;

            // 블록 배치 관련
            bool canPlaceBlock(const BlockPlacement& placement) const;
            bool placeBlock(const BlockPlacement& placement);
            bool removeBlock(const Position& position);

            // 게임 상태 관리
            PlayerColor getCurrentPlayer() const { return m_currentPlayer; }
            void setCurrentPlayer(PlayerColor player) { m_currentPlayer = player; }
            PlayerColor getNextPlayer() const;

            // 블록 사용 관리
            void setPlayerBlockUsed(PlayerColor player, BlockType blockType);
            bool isBlockUsed(PlayerColor player, BlockType blockType) const;
            std::vector<BlockType> getUsedBlocks(PlayerColor player) const;
            std::vector<BlockType> getAvailableBlocks(PlayerColor player) const;

            // 첫 블록 관리
            bool hasPlayerPlacedFirstBlock(PlayerColor player) const;

            // 게임 진행 상태
            bool canPlayerPlaceAnyBlock(PlayerColor player) const;
            bool isGameFinished() const;
            std::map<PlayerColor, int> calculateScores() const;

            // 보드 상태 접근
            PlayerColor getBoardCell(int row, int col) const;

            // 디버깅
            int getPlacedBlockCount(PlayerColor player) const;

        private:
            PlayerColor m_currentPlayer;
            PlayerColor m_board[BOARD_SIZE][BOARD_SIZE];

            std::map<PlayerColor, std::set<BlockType>> m_usedBlocks;
            std::map<PlayerColor, std::vector<Position>> m_playerOccupiedCells;
            std::map<PlayerColor, bool> m_hasPlacedFirstBlock;

            // 내부 헬퍼 함수들
            bool isPositionValid(const Position& pos) const;
            bool hasCollision(const BlockPlacement& placement) const;
            bool isFirstBlockValid(const BlockPlacement& placement) const;
            bool isCornerAdjacencyValid(const BlockPlacement& placement) const;
            bool hasNoEdgeAdjacency(const BlockPlacement& placement) const;

            std::vector<Position> getAdjacentCells(const Position& pos) const;
            std::vector<Position> getDiagonalCells(const Position& pos) const;
            Position getPlayerStartCorner(PlayerColor player) const;

            // 블록 변환 헬퍼
            PositionList getBlockShape(const BlockPlacement& placement) const;
            PositionList applyTransformation(const PositionList& shape, Rotation rotation, FlipState flip) const;
            PositionList normalizeShape(const PositionList& shape) const;
        };

        // ========================================
        // GameStateManager 클래스 (서버와 클라이언트 공유)
        // ========================================

        class GameStateManager
        {
        public:
            GameStateManager();

            // 게임 상태 관리
            void startNewGame();
            void resetGame();
            void endGame();

            // 턴 관리
            void nextTurn();
            void skipTurn();

            // 상태 확인
            GameState getGameState() const { return m_gameState; }
            TurnState getTurnState() const { return m_turnState; }
            bool canCurrentPlayerMove() const;

            // 게임 로직 접근
            GameLogic& getGameLogic() { return m_gameLogic; }
            const GameLogic& getGameLogic() const { return m_gameLogic; }

            // 최종 점수
            std::map<PlayerColor, int> getFinalScores() const;

            // 턴 정보
            int getTurnNumber() const { return m_turnNumber; }
            PlayerColor getCurrentPlayer() const { return m_gameLogic.getCurrentPlayer(); }

        private:
            GameLogic m_gameLogic;
            GameState m_gameState;
            TurnState m_turnState;

            int m_turnNumber;
            int m_currentPlayerIndex;
            std::vector<PlayerColor> m_playerOrder;
        };

    } // namespace Common
} // namespace Blokus