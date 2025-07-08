#include "common/GameLogic.h"
#include "common/Block.h"  // Block 클래스 사용
#include "common/Utils.h"
#include <algorithm>

namespace Blokus {
    namespace Common {

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

            // 보드에 블록 배치 (Block 클래스 사용)
            Block block(placement.type, placement.player);
            block.setRotation(placement.rotation);
            block.setFlipState(placement.flip);
            
            PositionList absolutePositions = block.getAbsolutePositions(placement.position);
            
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

            // 기존 Types.h의 모든 블록 타입 사용 (Block.cpp와 동일)
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
            // Block 클래스를 사용하여 충돌 검사
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
            // Block 클래스를 사용하여 첫 블록 검증
            Block block(placement.type, placement.player);
            block.setRotation(placement.rotation);
            block.setFlipState(placement.flip);

            PositionList absolutePositions = block.getAbsolutePositions(placement.position);

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
            Block block(placement.type, placement.player);
            block.setRotation(placement.rotation);
            block.setFlipState(placement.flip);

            PositionList absolutePositions = block.getAbsolutePositions(placement.position);

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
            Block block(placement.type, placement.player);
            block.setRotation(placement.rotation);
            block.setFlipState(placement.flip);

            PositionList absolutePositions = block.getAbsolutePositions(placement.position);

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

        // getBlockShape는 제거 - Block 클래스를 직접 사용
        // applyTransformation은 제거 - Block 클래스를 직접 사용
        // normalizeShape는 제거 - Block 클래스를 직접 사용

        // ========================================
        // GameStateManager 구현
        // ========================================

        GameStateManager::GameStateManager()
            : m_gameState(GameState::Waiting)
            , m_turnState(TurnState::WaitingForMove)  // Types.h의 TurnState 사용
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
            m_turnState = TurnState::WaitingForMove;
            m_gameLogic.setCurrentPlayer(m_playerOrder[0]);
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

    } // namespace Common
} // namespace Blokus