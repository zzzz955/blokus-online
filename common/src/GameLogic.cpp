#include "common/GameLogic.h"
#include "common/Utils.h"
#include <algorithm>

namespace Blokus {
    namespace Common {

        // ========================================
        // ��� ��� ���� (���� ������)
        // ========================================

        static const std::map<BlockType, PositionList> s_blockShapes = {
            // 1ĭ ���
            { BlockType::Single, { {0, 0} } },

            // 2ĭ ���
            { BlockType::Domino, { {0, 0}, {0, 1} } },

            // 3ĭ ���
            { BlockType::TrioLine, { {0, 0}, {0, 1}, {0, 2} } },
            { BlockType::TrioAngle, { {0, 0}, {0, 1}, {1, 1} } },

            // 4ĭ ��� (��Ʈ�ι̳�)
            { BlockType::Tetro_I, { {0, 0}, {0, 1}, {0, 2}, {0, 3} } },
            { BlockType::Tetro_O, { {0, 0}, {0, 1}, {1, 0}, {1, 1} } },
            { BlockType::Tetro_T, { {0, 0}, {0, 1}, {0, 2}, {1, 1} } },
            { BlockType::Tetro_L, { {0, 0}, {0, 1}, {0, 2}, {1, 0} } },
            { BlockType::Tetro_S, { {0, 0}, {0, 1}, {1, 1}, {1, 2} } },

            // 5ĭ ��� (����̳�)
            { BlockType::Pento_F, { {0, 1}, {0, 2}, {1, 0}, {1, 1}, {2, 1} } },
            { BlockType::Pento_I, { {0, 0}, {0, 1}, {0, 2}, {0, 3}, {0, 4} } },
            { BlockType::Pento_L, { {0, 0}, {0, 1}, {0, 2}, {0, 3}, {1, 0} } },
            { BlockType::Pento_N, { {0, 0}, {0, 1}, {0, 2}, {1, 2}, {1, 3} } },
            { BlockType::Pento_P, { {0, 0}, {0, 1}, {1, 0}, {1, 1}, {2, 0} } },
            { BlockType::Pento_T, { {0, 0}, {0, 1}, {0, 2}, {1, 1}, {2, 1} } },
            { BlockType::Pento_U, { {0, 0}, {0, 2}, {1, 0}, {1, 1}, {1, 2} } },
            { BlockType::Pento_V, { {0, 0}, {1, 0}, {2, 0}, {2, 1}, {2, 2} } },
            { BlockType::Pento_W, { {0, 0}, {1, 0}, {1, 1}, {2, 1}, {2, 2} } },
            { BlockType::Pento_X, { {0, 1}, {1, 0}, {1, 1}, {1, 2}, {2, 1} } },
            { BlockType::Pento_Y, { {0, 0}, {0, 1}, {0, 2}, {0, 3}, {1, 1} } },
            { BlockType::Pento_Z, { {0, 0}, {0, 1}, {1, 1}, {2, 1}, {2, 2} } }
        };

        // ========================================
        // GameLogic ����
        // ========================================

        GameLogic::GameLogic()
            : m_currentPlayer(PlayerColor::Blue)
        {
            initializeBoard();

            // ��� �÷��̾��� ù ��� ��ġ ���� �ʱ�ȭ
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

            // ���� ��ϰ� ��ġ ���� �ʱ�ȭ
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

            // 2. ����� �̹� ���Ǿ����� Ȯ��
            if (isBlockUsed(placement.player, placement.type)) {
                return false;
            }

            // 3. ù ��° ������� Ȯ��
            if (!hasPlayerPlacedFirstBlock(placement.player)) {
                return isFirstBlockValid(placement);
            }

            // 4. ù ��° ����� �ƴ� ���, ���Ŀ�� ��Ģ ����
            return isCornerAdjacencyValid(placement) && hasNoEdgeAdjacency(placement);
        }

        bool GameLogic::placeBlock(const BlockPlacement& placement)
        {
            if (!canPlaceBlock(placement)) {
                return false;
            }

            // ���忡 ��� ��ġ
            PositionList absolutePositions = getBlockShape(placement);
            for (const auto& pos : absolutePositions) {
                m_board[pos.first][pos.second] = placement.player;
                m_playerOccupiedCells[placement.player].push_back(pos);
            }

            // ��� ��� ǥ��
            setPlayerBlockUsed(placement.player, placement.type);

            // ù ��� ��ġ ǥ��
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

            // �ܼ��� �ش� ���� ���� (�����δ� ��ü ����� ã�Ƽ� �����ؾ� ��)
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

            // ��� ��� Ÿ�� ��ȸ
            for (int i = 0; i <= static_cast<int>(BlockType::Pento_Z); ++i) {
                BlockType blockType = static_cast<BlockType>(i);
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
            // ��� �÷��̾ �� �̻� ����� ���� �� ������ ���� ����
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

                // ������� ���� ����� ������ ����
                for (BlockType blockType : availableBlocks) {
                    score -= Utils::getBlockScore(blockType);
                }

                // ���ʽ� ���� ���
                if (availableBlocks.empty()) {
                    score += 15; // ��� ��� ��� ���ʽ�

                    // ������ ����� ���� ����̾����� �߰� ���ʽ�
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
            PositionList absolutePositions = getBlockShape(placement);

            for (const auto& pos : absolutePositions) {
                if (!isPositionValid(pos) || isCellOccupied(pos)) {
                    return true;
                }
            }

            return false;
        }

        bool GameLogic::isFirstBlockValid(const BlockPlacement& placement) const
        {
            PositionList absolutePositions = getBlockShape(placement);

            // Ŭ���� ���: 4�� �𼭸� �� �ϳ��� ��ƾ� ��
            std::vector<Position> corners = {
                {0, 0},                    // ���� �� �𼭸�
                {0, BOARD_SIZE - 1},       // ������ �� �𼭸�  
                {BOARD_SIZE - 1, 0},       // ���� �Ʒ� �𼭸�
                {BOARD_SIZE - 1, BOARD_SIZE - 1}  // ������ �Ʒ� �𼭸�
            };

            // ����� �� �� �ϳ��� 4�� �𼭸� �� �ϳ��� ��Ȯ�� ��ġ�ؾ� ��
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
            // ���� �� ��ϰ� �𼭸��� �����ؾ� ��
            PositionList absolutePositions = getBlockShape(placement);

            for (const auto& blockPos : absolutePositions) {
                std::vector<Position> diagonals = getDiagonalCells(blockPos);

                for (const auto& diagonal : diagonals) {
                    if (isPositionValid(diagonal) && getCellOwner(diagonal) == placement.player) {
                        return true; // ���� �� ��ϰ� �𼭸� ���� �߰�
                    }
                }
            }

            return false;
        }

        bool GameLogic::hasNoEdgeAdjacency(const BlockPlacement& placement) const
        {
            // ���� �� ��ϰ� ������ �����ϸ� �ȵ�
            PositionList absolutePositions = getBlockShape(placement);

            for (const auto& blockPos : absolutePositions) {
                std::vector<Position> adjacents = getAdjacentCells(blockPos);

                for (const auto& adjacent : adjacents) {
                    if (isPositionValid(adjacent) && getCellOwner(adjacent) == placement.player) {
                        return false; // ���� �� ��ϰ� �� ���� �߰�
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

        PositionList GameLogic::getBlockShape(const BlockPlacement& placement) const
        {
            auto it = s_blockShapes.find(placement.type);
            if (it == s_blockShapes.end()) {
                return { {0, 0} };
            }

            PositionList shape = it->second;

            // ��ȯ ����
            shape = applyTransformation(shape, placement.rotation, placement.flip);

            // ����ȭ
            shape = normalizeShape(shape);

            // ���� ��ġ�� ��ȯ
            PositionList absolutePositions;
            for (const auto& relativePos : shape) {
                Position absolutePos = {
                    placement.position.first + relativePos.first,
                    placement.position.second + relativePos.second
                };
                absolutePositions.push_back(absolutePos);
            }

            return absolutePositions;
        }

        PositionList GameLogic::applyTransformation(const PositionList& shape, Rotation rotation, FlipState flip) const
        {
            PositionList transformedShape = shape;

            // ������ ����
            for (auto& pos : transformedShape) {
                switch (flip) {
                case FlipState::Horizontal:
                    pos = { pos.first, -pos.second };
                    break;
                case FlipState::Vertical:
                    pos = { -pos.first, pos.second };
                    break;
                case FlipState::Both:
                    pos = { -pos.first, -pos.second };
                    break;
                default:
                    break;
                }
            }

            // ȸ�� ����
            for (auto& pos : transformedShape) {
                switch (rotation) {
                case Rotation::Degree_90:
                    pos = { pos.second, -pos.first };
                    break;
                case Rotation::Degree_180:
                    pos = { -pos.first, -pos.second };
                    break;
                case Rotation::Degree_270:
                    pos = { -pos.second, pos.first };
                    break;
                default:
                    break;
                }
            }

            return transformedShape;
        }

        PositionList GameLogic::normalizeShape(const PositionList& shape) const
        {
            if (shape.empty()) {
                return shape;
            }

            // �ּ� ��ǥ ã��
            int minRow = shape[0].first;
            int minCol = shape[0].second;

            for (const auto& pos : shape) {
                minRow = std::min(minRow, pos.first);
                minCol = std::min(minCol, pos.second);
            }

            // ����ȭ�� ���·� ��ȯ
            PositionList normalizedShape;
            for (const auto& pos : shape) {
                normalizedShape.push_back({
                    pos.first - minRow,
                    pos.second - minCol
                    });
            }

            return normalizedShape;
        }

        // ========================================
        // GameStateManager ����
        // ========================================

        GameStateManager::GameStateManager()
            : m_gameState(GameState::Waiting)
            , m_turnState(TurnState::Waiting)
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
            m_turnState = TurnState::Thinking;
            m_gameLogic.setCurrentPlayer(m_playerOrder[0]);
        }

        void GameStateManager::resetGame()
        {
            m_gameLogic.clearBoard();
            m_gameState = GameState::Waiting;
            m_turnState = TurnState::Waiting;
            m_turnNumber = 1;
            m_currentPlayerIndex = 0;
        }

        void GameStateManager::endGame()
        {
            m_gameState = GameState::Finished;
            m_turnState = TurnState::Finished;
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
                m_turnState = TurnState::Thinking;
            }
        }

        void GameStateManager::skipTurn()
        {
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

    } // namespace Common
} // namespace Blokus