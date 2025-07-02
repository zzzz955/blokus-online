#include "common/GameLogic.h"
#include "common/Utils.h"
#include <algorithm>

namespace Blokus {
    namespace Common {

        // ========================================
        // 블록 모양 정의 (정적 데이터)
        // ========================================

        static const std::map<BlockType, PositionList> s_blockShapes = {
            // 1칸 블록
            { BlockType::Single, { {0, 0} } },

            // 2칸 블록
            { BlockType::Domino, { {0, 0}, {0, 1} } },

            // 3칸 블록
            { BlockType::TrioLine, { {0, 0}, {0, 1}, {0, 2} } },
            { BlockType::TrioAngle, { {0, 0}, {0, 1}, {1, 1} } },

            // 4칸 블록 (테트로미노)
            { BlockType::Tetro_I, { {0, 0}, {0, 1}, {0, 2}, {0, 3} } },
            { BlockType::Tetro_O, { {0, 0}, {0, 1}, {1, 0}, {1, 1} } },
            { BlockType::Tetro_T, { {0, 0}, {0, 1}, {0, 2}, {1, 1} } },
            { BlockType::Tetro_L, { {0, 0}, {0, 1}, {0, 2}, {1, 0} } },
            { BlockType::Tetro_S, { {0, 0}, {0, 1}, {1, 1}, {1, 2} } },

            // 5칸 블록 (펜토미노)
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
        // GameLogic 구현
        // ========================================

        GameLogic::GameLogic()
            : m_currentPlayer(PlayerColor::Blue)
        {
            initializeBoard();

            // 모든 플레이어의 첫 블록 배치 상태 초기화
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

            // 사용된 블록과 배치 상태 초기화
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
            // 1. 기본 유효성 검사 (보드 경계, 충돌)
            if (hasCollision(placement)) {
                return false;
            }

            // 2. 블록이 이미 사용되었는지 확인
            if (isBlockUsed(placement.player, placement.type)) {
                return false;
            }

            // 3. 첫 번째 블록인지 확인
            if (!hasPlayerPlacedFirstBlock(placement.player)) {
                return isFirstBlockValid(placement);
            }

            // 4. 첫 번째 블록이 아닌 경우, 블로커스 규칙 적용
            return isCornerAdjacencyValid(placement) && hasNoEdgeAdjacency(placement);
        }

        bool GameLogic::placeBlock(const BlockPlacement& placement)
        {
            if (!canPlaceBlock(placement)) {
                return false;
            }

            // 보드에 블록 배치
            PositionList absolutePositions = getBlockShape(placement);
            for (const auto& pos : absolutePositions) {
                m_board[pos.first][pos.second] = placement.player;
                m_playerOccupiedCells[placement.player].push_back(pos);
            }

            // 블록 사용 표시
            setPlayerBlockUsed(placement.player, placement.type);

            // 첫 블록 배치 표시
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

            // 단순히 해당 셀만 제거 (실제로는 전체 블록을 찾아서 제거해야 함)
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

            // 모든 블록 타입 순회
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
                // 모든 가능한 위치와 회전/뒤집기 상태를 테스트
                for (int row = 0; row < BOARD_SIZE; ++row) {
                    for (int col = 0; col < BOARD_SIZE; ++col) {
                        Position testPos = { row, col };

                        // 4가지 회전 x 4가지 뒤집기 = 16가지 상태 테스트
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
            // 모든 플레이어가 더 이상 블록을 놓을 수 없으면 게임 종료
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

                // 사용하지 못한 블록의 점수를 차감
                for (BlockType blockType : availableBlocks) {
                    score -= Utils::getBlockScore(blockType);
                }

                // 보너스 점수 계산
                if (availableBlocks.empty()) {
                    score += 15; // 모든 블록 사용 보너스

                    // 마지막 블록이 단일 블록이었으면 추가 보너스
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
        // 내부 헬퍼 함수들
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

            // 클래식 모드: 4개 모서리 중 하나에 닿아야 함
            std::vector<Position> corners = {
                {0, 0},                    // 왼쪽 위 모서리
                {0, BOARD_SIZE - 1},       // 오른쪽 위 모서리  
                {BOARD_SIZE - 1, 0},       // 왼쪽 아래 모서리
                {BOARD_SIZE - 1, BOARD_SIZE - 1}  // 오른쪽 아래 모서리
            };

            // 블록의 셀 중 하나가 4개 모서리 중 하나에 정확히 위치해야 함
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
            // 같은 색 블록과 모서리로 접촉해야 함
            PositionList absolutePositions = getBlockShape(placement);

            for (const auto& blockPos : absolutePositions) {
                std::vector<Position> diagonals = getDiagonalCells(blockPos);

                for (const auto& diagonal : diagonals) {
                    if (isPositionValid(diagonal) && getCellOwner(diagonal) == placement.player) {
                        return true; // 같은 색 블록과 모서리 접촉 발견
                    }
                }
            }

            return false;
        }

        bool GameLogic::hasNoEdgeAdjacency(const BlockPlacement& placement) const
        {
            // 같은 색 블록과 변으로 접촉하면 안됨
            PositionList absolutePositions = getBlockShape(placement);

            for (const auto& blockPos : absolutePositions) {
                std::vector<Position> adjacents = getAdjacentCells(blockPos);

                for (const auto& adjacent : adjacents) {
                    if (isPositionValid(adjacent) && getCellOwner(adjacent) == placement.player) {
                        return false; // 같은 색 블록과 변 접촉 발견
                    }
                }
            }

            return true;
        }

        std::vector<Position> GameLogic::getAdjacentCells(const Position& pos) const
        {
            std::vector<Position> adjacents;

            // 상하좌우 4방향
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

            // 대각선 4방향
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
            case PlayerColor::Blue: return { 0, 0 };                          // 왼쪽 위
            case PlayerColor::Yellow: return { 0, BOARD_SIZE - 1 };          // 오른쪽 위
            case PlayerColor::Red: return { BOARD_SIZE - 1, 0 };             // 왼쪽 아래
            case PlayerColor::Green: return { BOARD_SIZE - 1, BOARD_SIZE - 1 }; // 오른쪽 아래
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

            // 변환 적용
            shape = applyTransformation(shape, placement.rotation, placement.flip);

            // 정규화
            shape = normalizeShape(shape);

            // 절대 위치로 변환
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

            // 뒤집기 적용
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

            // 회전 적용
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

            // 최소 좌표 찾기
            int minRow = shape[0].first;
            int minCol = shape[0].second;

            for (const auto& pos : shape) {
                minRow = std::min(minRow, pos.first);
                minCol = std::min(minCol, pos.second);
            }

            // 정규화된 형태로 변환
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
        // GameStateManager 구현
        // ========================================

        GameStateManager::GameStateManager()
            : m_gameState(GameState::Waiting)
            , m_turnState(TurnState::Waiting)
            , m_turnNumber(1)
            , m_currentPlayerIndex(0)
        {
            // 플레이어 순서 설정
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

            // 한 바퀴 돌면 턴 번호 증가
            if (m_currentPlayerIndex == 0) {
                m_turnNumber++;
            }

            PlayerColor newPlayer = m_playerOrder[m_currentPlayerIndex];
            m_gameLogic.setCurrentPlayer(newPlayer);

            // 게임 종료 조건 확인
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