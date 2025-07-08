#include "common/Block.h"
#include "common/Utils.h"
#include <algorithm>

namespace Blokus {
    namespace Common {

        // ========================================
        // ���� ��� ��� ���� (Types.h�� ��ġ)
        // ========================================

        const std::map<BlockType, PositionList> Block::s_blockShapes = {
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
        // Block ����
        // ========================================

        Block::Block(BlockType type, PlayerColor player)
            : m_type(type)
            , m_player(player)
            , m_rotation(Rotation::Degree_0)
            , m_flipState(FlipState::Normal)
        {
            if (s_blockShapes.find(type) == s_blockShapes.end()) {
                // �߸��� ��� Ÿ���� ��� �⺻������ ����
                m_type = BlockType::Single;
            }
        }

        void Block::setRotation(Rotation rotation)
        {
            m_rotation = rotation;
        }

        void Block::setFlipState(FlipState flip)
        {
            m_flipState = flip;
        }

        void Block::rotateClockwise()
        {
            int currentRotation = static_cast<int>(m_rotation);
            m_rotation = static_cast<Rotation>((currentRotation + 1) % 4);
        }

        void Block::rotateCounterclockwise()
        {
            int currentRotation = static_cast<int>(m_rotation);
            m_rotation = static_cast<Rotation>((currentRotation + 3) % 4);
        }

        void Block::flipHorizontal()
        {
            switch (m_flipState) {
            case FlipState::Normal:
                m_flipState = FlipState::Horizontal;
                break;
            case FlipState::Horizontal:
                m_flipState = FlipState::Normal;
                break;
            case FlipState::Vertical:
                m_flipState = FlipState::Both;
                break;
            case FlipState::Both:
                m_flipState = FlipState::Vertical;
                break;
            }
        }

        void Block::flipVertical()
        {
            switch (m_flipState) {
            case FlipState::Normal:
                m_flipState = FlipState::Vertical;
                break;
            case FlipState::Vertical:
                m_flipState = FlipState::Normal;
                break;
            case FlipState::Horizontal:
                m_flipState = FlipState::Both;
                break;
            case FlipState::Both:
                m_flipState = FlipState::Horizontal;
                break;
            }
        }

        void Block::resetTransform()
        {
            m_rotation = Rotation::Degree_0;
            m_flipState = FlipState::Normal;
        }

        PositionList Block::getCurrentShape() const
        {
            auto it = s_blockShapes.find(m_type);
            if (it == s_blockShapes.end()) {
                return { {0, 0} };
            }

            PositionList shape = it->second;

            // ������ ����
            shape = applyFlip(shape, m_flipState);

            // ȸ�� ����
            shape = applyRotation(shape, m_rotation);

            // ����ȭ (�ּ� ��ǥ�� (0,0)����)
            shape = normalizeShape(shape);

            return shape;
        }

        PositionList Block::getAbsolutePositions(const Position& basePos) const
        {
            PositionList currentShape = getCurrentShape();
            PositionList absolutePositions;

            for (const auto& relativePos : currentShape) {
                Position absolutePos = {
                    basePos.first + relativePos.first,
                    basePos.second + relativePos.second
                };
                absolutePositions.push_back(absolutePos);
            }

            return absolutePositions;
        }

        int Block::getSize() const
        {
            return static_cast<int>(getCurrentShape().size());
        }

        Block::BoundingRect Block::getBoundingRect() const
        {
            PositionList shape = getCurrentShape();
            if (shape.empty()) {
                return BoundingRect(0, 0, 1, 1);
            }

            int minRow = shape[0].first, maxRow = shape[0].first;
            int minCol = shape[0].second, maxCol = shape[0].second;

            for (const auto& pos : shape) {
                minRow = std::min(minRow, pos.first);
                maxRow = std::max(maxRow, pos.first);
                minCol = std::min(minCol, pos.second);
                maxCol = std::max(maxCol, pos.second);
            }

            return BoundingRect(minCol, minRow, maxCol - minCol + 1, maxRow - minRow + 1);
        }

        bool Block::wouldCollideAt(const Position& basePos, const PositionList& occupiedCells) const
        {
            PositionList absolutePositions = getAbsolutePositions(basePos);

            for (const auto& blockPos : absolutePositions) {
                for (const auto& occupiedPos : occupiedCells) {
                    if (blockPos == occupiedPos) {
                        return true;
                    }
                }
            }

            return false;
        }

        bool Block::isValidPlacement(const Position& basePos, int boardSize) const
        {
            PositionList absolutePositions = getAbsolutePositions(basePos);

            for (const auto& pos : absolutePositions) {
                if (!Utils::isPositionValid(pos, boardSize)) {
                    return false;
                }
            }

            return true;
        }

        PositionList Block::getBaseShape(BlockType type)
        {
            auto it = s_blockShapes.find(type);
            if (it != s_blockShapes.end()) {
                return it->second;
            }
            return { {0, 0} };
        }

        bool Block::isValidBlockType(BlockType type)
        {
            return s_blockShapes.find(type) != s_blockShapes.end();
        }

        // ========================================
        // ���� ���� �Լ���
        // ========================================

        PositionList Block::applyRotation(const PositionList& shape, Rotation rotation) const
        {
            PositionList rotatedShape;

            for (const auto& pos : shape) {
                Position newPos = pos;

                switch (rotation) {
                case Rotation::Degree_0:
                    // ��ȭ ����
                    break;
                case Rotation::Degree_90:
                    // 90�� �ð����: (r, c) -> (c, -r)
                    newPos = { pos.second, -pos.first };
                    break;
                case Rotation::Degree_180:
                    // 180��: (r, c) -> (-r, -c)
                    newPos = { -pos.first, -pos.second };
                    break;
                case Rotation::Degree_270:
                    // 270�� �ð����: (r, c) -> (-c, r)
                    newPos = { -pos.second, pos.first };
                    break;
                }

                rotatedShape.push_back(newPos);
            }

            return rotatedShape;
        }

        PositionList Block::applyFlip(const PositionList& shape, FlipState flip) const
        {
            PositionList flippedShape;

            for (const auto& pos : shape) {
                Position newPos = pos;

                switch (flip) {
                case FlipState::Normal:
                    // ��ȭ ����
                    break;
                case FlipState::Horizontal:
                    // ���� ������: (r, c) -> (r, -c)
                    newPos = { pos.first, -pos.second };
                    break;
                case FlipState::Vertical:
                    // ���� ������: (r, c) -> (-r, c)
                    newPos = { -pos.first, pos.second };
                    break;
                case FlipState::Both:
                    // ���� ������: (r, c) -> (-r, -c)
                    newPos = { -pos.first, -pos.second };
                    break;
                }

                flippedShape.push_back(newPos);
            }

            return flippedShape;
        }

        PositionList Block::normalizeShape(const PositionList& shape) const
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
        // BlockFactory ����
        // ========================================

        Block BlockFactory::createBlock(BlockType type, PlayerColor player)
        {
            return Block(type, player);
        }

        std::vector<Block> BlockFactory::createPlayerSet(PlayerColor player)
        {
            std::vector<Block> playerBlocks;

            // ��� ��� Ÿ�� ��ȸ
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

            for (BlockType type : allTypes) {
                playerBlocks.emplace_back(type, player);
            }

            return playerBlocks;
        }

        std::vector<Block> BlockFactory::createAllBlocks()
        {
            std::vector<Block> allBlocks;

            std::vector<PlayerColor> players = {
                PlayerColor::Blue, PlayerColor::Yellow,
                PlayerColor::Red, PlayerColor::Green
            };

            for (PlayerColor player : players) {
                auto playerBlocks = createPlayerSet(player);
                allBlocks.insert(allBlocks.end(), playerBlocks.begin(), playerBlocks.end());
            }

            return allBlocks;
        }

        std::string BlockFactory::getBlockName(BlockType type)
        {
            return Utils::getBlockName(type);
        }

        std::string BlockFactory::getBlockDescription(BlockType type)
        {
            return getBlockName(type) + " (" + std::to_string(getBlockScore(type)) + "ĭ)";
        }

        int BlockFactory::getBlockScore(BlockType type)
        {
            return Utils::getBlockScore(type);
        }

        bool BlockFactory::isValidBlockType(BlockType type)
        {
            return Block::isValidBlockType(type);
        }

        std::vector<BlockType> BlockFactory::getAllBlockTypes()
        {
            return {
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
        }

        int BlockFactory::getBlockCategory(BlockType type)
        {
            return getBlockScore(type); // ��� ũ�� = ī�װ�
        }

        std::vector<BlockType> BlockFactory::getBlocksBySize(int size)
        {
            std::vector<BlockType> blocks;
            auto allTypes = getAllBlockTypes();

            for (BlockType type : allTypes) {
                if (getBlockScore(type) == size) {
                    blocks.push_back(type);
                }
            }

            return blocks;
        }

    } // namespace Common
} // namespace Blokus