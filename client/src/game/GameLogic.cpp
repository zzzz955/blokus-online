#include "game/GameLogic.h"
#include <QDebug>
#include <algorithm>

namespace Blokus {

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

    bool GameLogic::canPlaceBlock(const Block& block, const Position& position, PlayerColor player) const
    {
        // 1. 기본 유효성 검사 (보드 경계, 충돌)
        if (!block.isValidPlacement(position, BOARD_SIZE)) {
            return false;
        }

        if (hasCollision(block, position)) {
            return false;
        }

        // 2. 블록이 이미 사용되었는지 확인
        if (isBlockUsed(player, block.getType())) {
            return false;
        }

        // 3. 첫 번째 블록인지 확인
        if (!hasPlayerPlacedFirstBlock(player)) {
            return isFirstBlockValid(block, position, player);
        }

        // 4. 첫 번째 블록이 아닌 경우, 블로커스 규칙 적용
        return isCornerAdjacencyValid(block, position, player) &&
            hasNoEdgeAdjacency(block, position, player);
    }

    bool GameLogic::placeBlock(const Block& block, const Position& position, PlayerColor player)
    {
        if (!canPlaceBlock(block, position, player)) {
            return false;
        }

        // 보드에 블록 배치
        PositionList absolutePositions = block.getAbsolutePositions(position);
        for (const auto& pos : absolutePositions) {
            m_board[pos.first][pos.second] = player;
            m_playerOccupiedCells[player].push_back(pos);
        }

        // 블록 사용 표시
        setPlayerBlockUsed(player, block.getType());

        // 첫 블록 배치 표시
        if (!hasPlayerPlacedFirstBlock(player)) {
            m_hasPlacedFirstBlock[player] = true;
        }

        qDebug() << QString::fromUtf8("블록 배치 성공: %1 플레이어, %2 블록")
            .arg(Utils::playerColorToString(player))
            .arg(BlockFactory::getBlockName(block.getType()));

        return true;
    }

    bool GameLogic::removeBlock(const Position& position)
    {
        // TODO: 블록 제거 로직 구현 (디버깅용)
        if (!isPositionValid(position)) return false;

        PlayerColor owner = getCellOwner(position);
        if (owner == PlayerColor::None) return false;

        // 단순히 해당 셀만 제거 (실제로는 전체 블록을 찾아서 제거해야 함)
        m_board[position.first][position.second] = PlayerColor::None;

        return true;
    }

    bool GameLogic::isFirstBlockValid(const Block& block, const Position& position, PlayerColor player) const
    {
        // 수정된 첫 번째 블록 규칙: 지정된 시작 모서리에서만 시작 가능
        PositionList absolutePositions = block.getAbsolutePositions(position);
        Position startCorner = getPlayerStartCorner(player);

        // 블록의 셀 중 하나가 플레이어의 시작 모서리를 포함해야 함
        for (const auto& blockPos : absolutePositions) {
            if (blockPos == startCorner) {
                return true;
            }
        }

        return false;
    }

    bool GameLogic::isCornerAdjacencyValid(const Block& block, const Position& position, PlayerColor player) const
    {
        // 같은 색 블록과 모서리로 접촉해야 함
        PositionList absolutePositions = block.getAbsolutePositions(position);

        for (const auto& blockPos : absolutePositions) {
            std::vector<Position> diagonals = getDiagonalCells(blockPos);

            for (const auto& diagonal : diagonals) {
                if (isPositionValid(diagonal) && getCellOwner(diagonal) == player) {
                    return true; // 같은 색 블록과 모서리 접촉 발견
                }
            }
        }

        return false;
    }

    bool GameLogic::hasNoEdgeAdjacency(const Block& block, const Position& position, PlayerColor player) const
    {
        // 같은 색 블록과 변으로 접촉하면 안됨
        PositionList absolutePositions = block.getAbsolutePositions(position);

        for (const auto& blockPos : absolutePositions) {
            std::vector<Position> adjacents = getAdjacentCells(blockPos);

            for (const auto& adjacent : adjacents) {
                if (isPositionValid(adjacent) && getCellOwner(adjacent) == player) {
                    return false; // 같은 색 블록과 변 접촉 발견
                }
            }
        }

        return true;
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
        auto allTypes = BlockFactory::getAllBlockTypes();

        for (BlockType blockType : allTypes) {
            if (!isBlockUsed(player, blockType)) {
                available.push_back(blockType);
            }
        }

        return available;
    }

    PlayerColor GameLogic::getNextPlayer() const
    {
        return Utils::getNextPlayer(m_currentPlayer);
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
            Block testBlock(blockType, player);

            // 모든 가능한 위치와 회전/뒤집기 상태를 테스트
            for (int row = 0; row < BOARD_SIZE; ++row) {
                for (int col = 0; col < BOARD_SIZE; ++col) {
                    Position testPos = { row, col };

                    // 4가지 회전 x 4가지 뒤집기 = 16가지 상태 테스트
                    for (int rot = 0; rot < 4; ++rot) {
                        for (int flip = 0; flip < 4; ++flip) {
                            testBlock.setRotation(static_cast<Rotation>(rot));
                            testBlock.setFlipState(static_cast<FlipState>(flip));

                            if (canPlaceBlock(testBlock, testPos, player)) {
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
                score -= BlockFactory::getBlockScore(blockType);
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

    void GameLogic::printBoard() const
    {
        qDebug() << QString::fromUtf8("=== 게임 보드 상태 ===");
        for (int row = 0; row < BOARD_SIZE; ++row) {
            QString rowStr;
            for (int col = 0; col < BOARD_SIZE; ++col) {
                switch (m_board[row][col]) {
                case PlayerColor::Blue: rowStr += "B "; break;
                case PlayerColor::Yellow: rowStr += "Y "; break;
                case PlayerColor::Red: rowStr += "R "; break;
                case PlayerColor::Green: rowStr += "G "; break;
                default: rowStr += ". "; break;
                }
            }
            qDebug() << rowStr;
        }
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
        return pos.first >= 0 && pos.first < BOARD_SIZE &&
            pos.second >= 0 && pos.second < BOARD_SIZE;
    }

    bool GameLogic::hasCollision(const Block& block, const Position& position) const
    {
        PositionList absolutePositions = block.getAbsolutePositions(position);

        for (const auto& pos : absolutePositions) {
            if (!isPositionValid(pos) || isCellOccupied(pos)) {
                return true;
            }
        }

        return false;
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
        case PlayerColor::Blue: return { 0, 0 };                    // 왼쪽 위
        case PlayerColor::Yellow: return { 0, BOARD_SIZE - 1 };     // 오른쪽 위
        case PlayerColor::Red: return { BOARD_SIZE - 1, 0 };        // 왼쪽 아래
        case PlayerColor::Green: return { BOARD_SIZE - 1, BOARD_SIZE - 1 }; // 오른쪽 아래
        default: return { 0, 0 };
        }
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

        qDebug() << QString::fromUtf8("새 게임 시작! 현재 플레이어: %1")
            .arg(Utils::playerColorToString(m_gameLogic.getCurrentPlayer()));
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

        auto scores = getFinalScores();
        qDebug() << QString::fromUtf8("게임 종료! 최종 점수:");
        for (const auto& pair : scores) {
            qDebug() << QString::fromUtf8("%1: %2점")
                .arg(Utils::playerColorToString(pair.first))
                .arg(pair.second);
        }
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
            qDebug() << QString::fromUtf8("턴 %1: %2 플레이어 차례")
                .arg(m_turnNumber)
                .arg(Utils::playerColorToString(newPlayer));
        }
    }

    void GameStateManager::skipTurn()
    {
        qDebug() << QString::fromUtf8("%1 플레이어 턴 스킵")
            .arg(Utils::playerColorToString(m_gameLogic.getCurrentPlayer()));
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

} // namespace Blokus