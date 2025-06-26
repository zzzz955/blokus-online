#pragma once

#include <vector>
#include <map>
#include <set>
#include "common/Types.h"
#include "game/Block.h"

namespace Blokus {

    /**
     * @brief ���Ŀ�� ���� ��Ģ�� ������ �����ϴ� Ŭ����
     */
    class GameLogic
    {
    public:
        explicit GameLogic();
        ~GameLogic() = default;

        // ���� ���� ���� ����
        void initializeBoard();
        void clearBoard();
        PlayerColor getCellOwner(const Position& pos) const;
        bool isCellOccupied(const Position& pos) const;

        // ��� ��ġ ��Ģ ����
        bool canPlaceBlock(const Block& block, const Position& position, PlayerColor player) const;
        bool placeBlock(const Block& block, const Position& position, PlayerColor player);
        bool removeBlock(const Position& position);

        // ���Ŀ�� �ٽ� ��Ģ ����
        bool isFirstBlockValid(const Block& block, const Position& position, PlayerColor player) const;
        bool isCornerAdjacencyValid(const Block& block, const Position& position, PlayerColor player) const;
        bool hasNoEdgeAdjacency(const Block& block, const Position& position, PlayerColor player) const;

        // ���� ���� ����
        void setPlayerBlockUsed(PlayerColor player, BlockType blockType);
        bool isBlockUsed(PlayerColor player, BlockType blockType) const;
        std::vector<BlockType> getUsedBlocks(PlayerColor player) const;
        std::vector<BlockType> getAvailableBlocks(PlayerColor player) const;

        // �� ����
        void setCurrentPlayer(PlayerColor player) { m_currentPlayer = player; }
        PlayerColor getCurrentPlayer() const { return m_currentPlayer; }
        PlayerColor getNextPlayer() const;
        bool hasPlayerPlacedFirstBlock(PlayerColor player) const;

        // ���� ���� ����
        bool canPlayerPlaceAnyBlock(PlayerColor player) const;
        bool isGameFinished() const;
        std::map<PlayerColor, int> calculateScores() const;

        // ���� ���� ����
        const PlayerColor(&getBoard() const)[BOARD_SIZE][BOARD_SIZE]{ return m_board; }

            // ����� �� ��ƿ��Ƽ
        void printBoard() const;
        int getPlacedBlockCount(PlayerColor player) const;

    private:
        // ���� ���� �Լ���
        bool isPositionValid(const Position& pos) const;
        bool hasCollision(const Block& block, const Position& position) const;
        std::vector<Position> getAdjacentCells(const Position& pos) const;
        std::vector<Position> getDiagonalCells(const Position& pos) const;
        Position getPlayerStartCorner(PlayerColor player) const;
        bool isAdjacentToSameColorCorner(const Block& block, const Position& position, PlayerColor player) const;

        // ��� ������
        PlayerColor m_board[BOARD_SIZE][BOARD_SIZE];    // ���� ���� ����
        PlayerColor m_currentPlayer;                     // ���� �� �÷��̾�

        // �÷��̾ ���� ��� ����
        std::map<PlayerColor, std::set<BlockType>> m_usedBlocks;

        // �÷��̾ ù ��� ��ġ ����
        std::map<PlayerColor, bool> m_hasPlacedFirstBlock;

        // �÷��̾ ��ġ�� ��� ��ġ ���� (��Ģ ������)
        std::map<PlayerColor, std::vector<Position>> m_playerOccupiedCells;
    };

    /**
     * @brief ���� ���¸� �����ϴ� �Ŵ��� Ŭ����
     */
    class GameStateManager
    {
    public:
        explicit GameStateManager();
        ~GameStateManager() = default;

        // ���� ����������Ŭ
        void startNewGame();
        void resetGame();
        void endGame();

        // �� ����
        void nextTurn();
        void skipTurn();
        bool canCurrentPlayerMove() const;

        // ���� ���� ����
        GameLogic& getGameLogic() { return m_gameLogic; }
        const GameLogic& getGameLogic() const { return m_gameLogic; }

        // ���� ����
        GameState getGameState() const { return m_gameState; }
        TurnState getTurnState() const { return m_turnState; }

        void setGameState(GameState state) { m_gameState = state; }
        void setTurnState(TurnState state) { m_turnState = state; }

        // ���
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