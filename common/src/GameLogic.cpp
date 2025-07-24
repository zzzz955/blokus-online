#include "GameLogic.h"
#include "Block.h"  // Block Ŭ���� ���
#include "Utils.h"
#include <algorithm>

namespace Blokus {
    namespace Common {

        // ========================================
        // GameLogic ����
        // ========================================

        GameLogic::GameLogic()
            : m_currentPlayer(PlayerColor::Blue)
        {
            initializeBoard();

            // ��� �÷��̾��� ù ���� ��ġ ���� �ʱ�ȭ
            m_hasPlacedFirstBlock[PlayerColor::Blue] = false;
            m_hasPlacedFirstBlock[PlayerColor::Yellow] = false;
            m_hasPlacedFirstBlock[PlayerColor::Red] = false;
            m_hasPlacedFirstBlock[PlayerColor::Green] = false;
        }

        void GameLogic::initializeBoard()
        {
            clearBoard();
        }

        void GameLogic::clearBoard()
        {
            for (int row = 0; row < BOARD_SIZE; ++row) {
                for (int col = 0; col < BOARD_SIZE; ++col) {
                    m_board[row][col] = PlayerColor::None;
                }
            }

            // ���� ���ϰ� ��ġ ���� �ʱ�ȭ
            m_usedBlocks.clear();
            m_playerOccupiedCells.clear();

            for (auto& pair : m_hasPlacedFirstBlock) {
                pair.second = false;
            }
        }

        PlayerColor GameLogic::getCellOwner(const Position& pos) const
        {
            if (!isPositionValid(pos)) return PlayerColor::None;
            return m_board[pos.first][pos.second];
        }

        bool GameLogic::isCellOccupied(const Position& pos) const
        {
            return getCellOwner(pos) != PlayerColor::None;
        }

        bool GameLogic::canPlaceBlock(const BlockPlacement& placement) const
        {
            // 1. �⺻ ��ȿ�� �˻� (���� ���, �浹)
            if (hasCollision(placement)) {
                return false;
            }

            // 2. ������ �̹� ���Ǿ����� Ȯ��
            if (isBlockUsed(placement.player, placement.type)) {
                return false;
            }

            // 3. ù ��° �������� Ȯ��
            if (!hasPlayerPlacedFirstBlock(placement.player)) {
                return isFirstBlockValid(placement);
            }

            // 4. ù ��° ������ �ƴ� ���, ����Ŀ�� ��Ģ ����
            return isCornerAdjacencyValid(placement) && hasNoEdgeAdjacency(placement);
        }

        bool GameLogic::placeBlock(const BlockPlacement& placement)
        {
            if (!canPlaceBlock(placement)) {
                return false;
            }

            // ���忡 ���� ��ġ (Block Ŭ���� ���)
            Block block(placement.type, placement.player);
            block.setRotation(placement.rotation);
            block.setFlipState(placement.flip);
            
            PositionList absolutePositions = block.getAbsolutePositions(placement.position);
            
            for (const auto& pos : absolutePositions) {
                m_board[pos.first][pos.second] = placement.player;
                m_playerOccupiedCells[placement.player].push_back(pos);
            }

            // ���� ��� ǥ��
            setPlayerBlockUsed(placement.player, placement.type);

            // ù ���� ��ġ ǥ��
            if (!hasPlayerPlacedFirstBlock(placement.player)) {
                m_hasPlacedFirstBlock[placement.player] = true;
            }

            return true;
        }

        bool GameLogic::removeBlock(const Position& position)
        {
            if (!isPositionValid(position)) return false;

            PlayerColor owner = getCellOwner(position);
            if (owner == PlayerColor::None) return false;

            // �ܼ��� �ش� ���� ���� (�����δ� ��ü ������ ã�Ƽ� �����ؾ� ��)
            m_board[position.first][position.second] = PlayerColor::None;
            return true;
        }

        PlayerColor GameLogic::getNextPlayer() const
        {
            return Utils::getNextPlayer(m_currentPlayer);
        }

        void GameLogic::setPlayerBlockUsed(PlayerColor player, BlockType blockType)
        {
            m_usedBlocks[player].insert(blockType);
        }

        bool GameLogic::isBlockUsed(PlayerColor player, BlockType blockType) const
        {
            auto it = m_usedBlocks.find(player);
            if (it == m_usedBlocks.end()) return false;
            return it->second.find(blockType) != it->second.end();
        }

        std::vector<BlockType> GameLogic::getUsedBlocks(PlayerColor player) const
        {
            std::vector<BlockType> result;
            auto it = m_usedBlocks.find(player);
            if (it != m_usedBlocks.end()) {
                for (BlockType blockType : it->second) {
                    result.push_back(blockType);
                }
            }
            return result;
        }

        std::vector<BlockType> GameLogic::getAvailableBlocks(PlayerColor player) const
        {
            std::vector<BlockType> available;

            // ���� Types.h�� ��� ���� Ÿ�� ��� (Block.cpp�� ����)
            std::vector<BlockType> allTypes = {
                BlockType::Single,
                BlockType::Domino,
                BlockType::TrioLine, BlockType::TrioAngle,
                BlockType::Tetro_I, BlockType::Tetro_O, BlockType::Tetro_T,
                BlockType::Tetro_L, BlockType::Tetro_S,
                BlockType::Pento_F, BlockType::Pento_I, BlockType::Pento_L,
                BlockType::Pento_N, BlockType::Pento_P, BlockType::Pento_T,
                BlockType::Pento_U, BlockType::Pento_V, BlockType::Pento_W,
                BlockType::Pento_X, BlockType::Pento_Y, BlockType::Pento_Z
            };

            for (BlockType blockType : allTypes) {
                if (!isBlockUsed(player, blockType)) {
                    available.push_back(blockType);
                }
            }

            return available;
        }

        bool GameLogic::hasPlayerPlacedFirstBlock(PlayerColor player) const
        {
            auto it = m_hasPlacedFirstBlock.find(player);
            return it != m_hasPlacedFirstBlock.end() && it->second;
        }

        bool GameLogic::canPlayerPlaceAnyBlock(PlayerColor player) const
        {
            auto availableBlocks = getAvailableBlocks(player);

            for (BlockType blockType : availableBlocks) {
                // ��� ������ ��ġ�� ȸ��/������ ���¸� �׽�Ʈ
                for (int row = 0; row < BOARD_SIZE; ++row) {
                    for (int col = 0; col < BOARD_SIZE; ++col) {
                        Position testPos = { row, col };

                        // 4���� ȸ�� x 4���� ������ = 16���� ���� �׽�Ʈ
                        for (int rot = 0; rot < 4; ++rot) {
                            for (int flip = 0; flip < 4; ++flip) {
                                BlockPlacement testPlacement;
                                testPlacement.type = blockType;
                                testPlacement.position = testPos;
                                testPlacement.rotation = static_cast<Rotation>(rot);
                                testPlacement.flip = static_cast<FlipState>(flip);
                                testPlacement.player = player;

                                if (canPlaceBlock(testPlacement)) {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        bool GameLogic::isGameFinished() const
        {
            // ��� �÷��̾ �� �̻� ������ ���� �� ������ ���� ����
            std::vector<PlayerColor> players = {
                PlayerColor::Blue, PlayerColor::Yellow,
                PlayerColor::Red, PlayerColor::Green
            };

            for (PlayerColor player : players) {
                if (canPlayerPlaceAnyBlock(player)) {
                    return false;
                }
            }

            return true;
        }

        std::map<PlayerColor, int> GameLogic::calculateScores() const
        {
            std::map<PlayerColor, int> scores;

            std::vector<PlayerColor> players = {
                PlayerColor::Blue, PlayerColor::Yellow,
                PlayerColor::Red, PlayerColor::Green
            };

            for (PlayerColor player : players) {
                int score = 0;
                auto availableBlocks = getAvailableBlocks(player);

                // ������� ���� ������ ������ ����
                for (BlockType blockType : availableBlocks) {
                    score -= Utils::getBlockScore(blockType);
                }

                // ���ʽ� ���� ���
                if (availableBlocks.empty()) {
                    score += 15; // ��� ���� ��� ���ʽ�

                    // ������ ������ ���� �����̾����� �߰� ���ʽ�
                    if (isBlockUsed(player, BlockType::Single)) {
                        score += 5;
                    }
                }

                scores[player] = score;
            }

            return scores;
        }

        PlayerColor GameLogic::getBoardCell(int row, int col) const
        {
            if (row < 0 || row >= BOARD_SIZE || col < 0 || col >= BOARD_SIZE) {
                return PlayerColor::None;
            }
            return m_board[row][col];
        }

        int GameLogic::getPlacedBlockCount(PlayerColor player) const
        {
            auto it = m_usedBlocks.find(player);
            return it != m_usedBlocks.end() ? static_cast<int>(it->second.size()) : 0;
        }

        // ========================================
        // ���� ���� �Լ���
        // ========================================

        bool GameLogic::isPositionValid(const Position& pos) const
        {
            return Utils::isPositionValid(pos, BOARD_SIZE);
        }

        bool GameLogic::hasCollision(const BlockPlacement& placement) const
        {
            // Block Ŭ������ ����Ͽ� �浹 �˻�
            Block block(placement.type, placement.player);
            block.setRotation(placement.rotation);
            block.setFlipState(placement.flip);

            PositionList absolutePositions = block.getAbsolutePositions(placement.position);

            for (const auto& pos : absolutePositions) {
                if (!isPositionValid(pos) || isCellOccupied(pos)) {
                    return true;
                }
            }

            return false;
        }

        bool GameLogic::isFirstBlockValid(const BlockPlacement& placement) const
        {
            // Block Ŭ������ ����Ͽ� ù ���� ����
            Block block(placement.type, placement.player);
            block.setRotation(placement.rotation);
            block.setFlipState(placement.flip);

            PositionList absolutePositions = block.getAbsolutePositions(placement.position);

            // Ŭ���� ���: 4�� �𼭸� �� �ϳ��� ��ƾ� ��
            std::vector<Position> corners = {
                {0, 0},                    // ���� �� �𼭸�
                {0, BOARD_SIZE - 1},       // ������ �� �𼭸�  
                {BOARD_SIZE - 1, 0},       // ���� �Ʒ� �𼭸�
                {BOARD_SIZE - 1, BOARD_SIZE - 1}  // ������ �Ʒ� �𼭸�
            };

            // ������ �� �� �ϳ��� 4�� �𼭸� �� �ϳ��� ��Ȯ�� ��ġ�ؾ� ��
            for (const auto& blockPos : absolutePositions) {
                for (const auto& corner : corners) {
                    if (blockPos == corner) {
                        return true;
                    }
                }
            }

            return false;
        }

        bool GameLogic::isCornerAdjacencyValid(const BlockPlacement& placement) const
        {
            // ���� �� ���ϰ� �𼭸��� �����ؾ� ��
            Block block(placement.type, placement.player);
            block.setRotation(placement.rotation);
            block.setFlipState(placement.flip);

            PositionList absolutePositions = block.getAbsolutePositions(placement.position);

            for (const auto& blockPos : absolutePositions) {
                std::vector<Position> diagonals = getDiagonalCells(blockPos);

                for (const auto& diagonal : diagonals) {
                    if (isPositionValid(diagonal) && getCellOwner(diagonal) == placement.player) {
                        return true; // ���� �� ���ϰ� �𼭸� ���� �߰�
                    }
                }
            }

            return false;
        }

        bool GameLogic::hasNoEdgeAdjacency(const BlockPlacement& placement) const
        {
            // ���� �� ���ϰ� ������ �����ϸ� �ȵ�
            Block block(placement.type, placement.player);
            block.setRotation(placement.rotation);
            block.setFlipState(placement.flip);

            PositionList absolutePositions = block.getAbsolutePositions(placement.position);

            for (const auto& blockPos : absolutePositions) {
                std::vector<Position> adjacents = getAdjacentCells(blockPos);

                for (const auto& adjacent : adjacents) {
                    if (isPositionValid(adjacent) && getCellOwner(adjacent) == placement.player) {
                        return false; // ���� �� ���ϰ� �� ���� �߰�
                    }
                }
            }

            return true;
        }

        std::vector<Position> GameLogic::getAdjacentCells(const Position& pos) const
        {
            std::vector<Position> adjacents;

            // �����¿� 4����
            std::vector<Position> directions = { {-1, 0}, {1, 0}, {0, -1}, {0, 1} };

            for (const auto& dir : directions) {
                Position adjacent = { pos.first + dir.first, pos.second + dir.second };
                if (isPositionValid(adjacent)) {
                    adjacents.push_back(adjacent);
                }
            }

            return adjacents;
        }

        std::vector<Position> GameLogic::getDiagonalCells(const Position& pos) const
        {
            std::vector<Position> diagonals;

            // �밢�� 4����
            std::vector<Position> directions = { {-1, -1}, {-1, 1}, {1, -1}, {1, 1} };

            for (const auto& dir : directions) {
                Position diagonal = { pos.first + dir.first, pos.second + dir.second };
                if (isPositionValid(diagonal)) {
                    diagonals.push_back(diagonal);
                }
            }

            return diagonals;
        }

        Position GameLogic::getPlayerStartCorner(PlayerColor player) const
        {
            switch (player) {
            case PlayerColor::Blue: return { 0, 0 };                          // ���� ��
            case PlayerColor::Yellow: return { 0, BOARD_SIZE - 1 };          // ������ ��
            case PlayerColor::Red: return { BOARD_SIZE - 1, 0 };             // ���� �Ʒ�
            case PlayerColor::Green: return { BOARD_SIZE - 1, BOARD_SIZE - 1 }; // ������ �Ʒ�
            default: return { 0, 0 };
            }
        }

        // getBlockShape�� ���� - Block Ŭ������ ���� ���
        // applyTransformation�� ���� - Block Ŭ������ ���� ���
        // normalizeShape�� ���� - Block Ŭ������ ���� ���

        // ========================================
        // GameStateManager ����
        // ========================================

        GameStateManager::GameStateManager()
            : m_gameState(GameState::Waiting)
            , m_turnState(TurnState::WaitingForMove)  // Types.h�� TurnState ���
            , m_turnNumber(1)
            , m_currentPlayerIndex(0)
        {
            // �÷��̾� ���� ����
            m_playerOrder = {
                PlayerColor::Blue, PlayerColor::Yellow,
                PlayerColor::Red, PlayerColor::Green
            };
        }

        void GameStateManager::startNewGame()
        {
            resetGame();
            m_gameState = GameState::Playing;
            m_turnState = TurnState::WaitingForMove;
            m_gameLogic.setCurrentPlayer(m_playerOrder[0]);
        }

        void GameStateManager::startNewGame(const std::vector<PlayerColor>& turnOrder)
        {
            setTurnOrder(turnOrder);
            startNewGame();
        }

        void GameStateManager::resetGame()
        {
            m_gameLogic.clearBoard();
            m_gameState = GameState::Waiting;
            m_turnState = TurnState::WaitingForMove;
            m_turnNumber = 1;
            m_currentPlayerIndex = 0;
        }

        void GameStateManager::endGame()
        {
            m_gameState = GameState::Finished;
            m_turnState = TurnState::TurnComplete;
        }

        void GameStateManager::nextTurn()
        {
            if (m_gameState != GameState::Playing) return;

            m_currentPlayerIndex = (m_currentPlayerIndex + 1) % m_playerOrder.size();

            // �� ���� ���� �� ��ȣ ����
            if (m_currentPlayerIndex == 0) {
                m_turnNumber++;
            }

            PlayerColor newPlayer = m_playerOrder[m_currentPlayerIndex];
            m_gameLogic.setCurrentPlayer(newPlayer);

            // ���� ���� ���� Ȯ��
            if (m_gameLogic.isGameFinished()) {
                endGame();
            }
            else {
                m_turnState = TurnState::WaitingForMove;
            }
        }

        void GameStateManager::skipTurn()
        {
            m_turnState = TurnState::Skipped;
            nextTurn();
        }

        bool GameStateManager::canCurrentPlayerMove() const
        {
            return m_gameLogic.canPlayerPlaceAnyBlock(m_gameLogic.getCurrentPlayer());
        }

        std::map<PlayerColor, int> GameStateManager::getFinalScores() const
        {
            return m_gameLogic.calculateScores();
        }

        void GameStateManager::setTurnOrder(const std::vector<PlayerColor>& turnOrder)
        {
            if (!turnOrder.empty()) {
                m_playerOrder = turnOrder;
                m_currentPlayerIndex = 0;
                if (m_gameState == GameState::Playing) {
                    m_gameLogic.setCurrentPlayer(m_playerOrder[0]);
                }
            }
        }

        void GameStateManager::setCurrentPlayerIndex(int index)
        {
            if (index >= 0 && index < static_cast<int>(m_playerOrder.size())) {
                m_currentPlayerIndex = index;
            }
        }

        PlayerColor GameStateManager::getNextPlayer() const
        {
            if (m_playerOrder.empty()) {
                return PlayerColor::None;
            }
            int nextIndex = (m_currentPlayerIndex + 1) % m_playerOrder.size();
            return m_playerOrder[nextIndex];
        }

    } // namespace Common
} // namespace Blokus