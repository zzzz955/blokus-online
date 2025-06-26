#pragma once

#include <vector>
#include <map>
#include <set>
#include "common/Types.h"
#include "game/Block.h"

namespace Blokus {

    /**
     * @brief 블로커스 게임 규칙과 로직을 관리하는 클래스
     */
    class GameLogic
    {
    public:
        explicit GameLogic();
        ~GameLogic() = default;

        // 게임 보드 상태 관리
        void initializeBoard();
        void clearBoard();
        PlayerColor getCellOwner(const Position& pos) const;
        bool isCellOccupied(const Position& pos) const;

        // 블록 배치 규칙 검증
        bool canPlaceBlock(const Block& block, const Position& position, PlayerColor player) const;
        bool placeBlock(const Block& block, const Position& position, PlayerColor player);
        bool removeBlock(const Position& position);

        // 블로커스 핵심 규칙 검증
        bool isFirstBlockValid(const Block& block, const Position& position, PlayerColor player) const;
        bool isCornerAdjacencyValid(const Block& block, const Position& position, PlayerColor player) const;
        bool hasNoEdgeAdjacency(const Block& block, const Position& position, PlayerColor player) const;

        // 게임 상태 관리
        void setPlayerBlockUsed(PlayerColor player, BlockType blockType);
        bool isBlockUsed(PlayerColor player, BlockType blockType) const;
        std::vector<BlockType> getUsedBlocks(PlayerColor player) const;
        std::vector<BlockType> getAvailableBlocks(PlayerColor player) const;

        // 턴 관리
        void setCurrentPlayer(PlayerColor player) { m_currentPlayer = player; }
        PlayerColor getCurrentPlayer() const { return m_currentPlayer; }
        PlayerColor getNextPlayer() const;
        bool hasPlayerPlacedFirstBlock(PlayerColor player) const;

        // 게임 종료 조건
        bool canPlayerPlaceAnyBlock(PlayerColor player) const;
        bool isGameFinished() const;
        std::map<PlayerColor, int> calculateScores() const;

        // 보드 상태 접근
        const PlayerColor(&getBoard() const)[BOARD_SIZE][BOARD_SIZE]{ return m_board; }

            // 디버그 및 유틸리티
        void printBoard() const;
        int getPlacedBlockCount(PlayerColor player) const;

    private:
        // 내부 헬퍼 함수들
        bool isPositionValid(const Position& pos) const;
        bool hasCollision(const Block& block, const Position& position) const;
        std::vector<Position> getAdjacentCells(const Position& pos) const;
        std::vector<Position> getDiagonalCells(const Position& pos) const;
        Position getPlayerStartCorner(PlayerColor player) const;
        bool isAdjacentToSameColorCorner(const Block& block, const Position& position, PlayerColor player) const;

        // 멤버 변수들
        PlayerColor m_board[BOARD_SIZE][BOARD_SIZE];    // 게임 보드 상태
        PlayerColor m_currentPlayer;                     // 현재 턴 플레이어

        // 플레이어별 사용된 블록 추적
        std::map<PlayerColor, std::set<BlockType>> m_usedBlocks;

        // 플레이어별 첫 블록 배치 여부
        std::map<PlayerColor, bool> m_hasPlacedFirstBlock;

        // 플레이어별 배치된 블록 위치 추적 (규칙 검증용)
        std::map<PlayerColor, std::vector<Position>> m_playerOccupiedCells;
    };

    /**
     * @brief 게임 상태를 관리하는 매니저 클래스
     */
    class GameStateManager
    {
    public:
        explicit GameStateManager();
        ~GameStateManager() = default;

        // 게임 라이프사이클
        void startNewGame();
        void resetGame();
        void endGame();

        // 턴 관리
        void nextTurn();
        void skipTurn();
        bool canCurrentPlayerMove() const;

        // 게임 로직 접근
        GameLogic& getGameLogic() { return m_gameLogic; }
        const GameLogic& getGameLogic() const { return m_gameLogic; }

        // 게임 상태
        GameState getGameState() const { return m_gameState; }
        TurnState getTurnState() const { return m_turnState; }

        void setGameState(GameState state) { m_gameState = state; }
        void setTurnState(TurnState state) { m_turnState = state; }

        // 통계
        int getTurnNumber() const { return m_turnNumber; }
        std::map<PlayerColor, int> getFinalScores() const;

    private:
        GameLogic m_gameLogic;
        GameState m_gameState;
        TurnState m_turnState;
        int m_turnNumber;

        std::vector<PlayerColor> m_playerOrder;
        size_t m_currentPlayerIndex;
    };

} // namespace Blokus