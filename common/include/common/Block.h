#pragma once

#include "Types.h"
#include <vector>
#include <map>
#include <string>

namespace Blokus {
    namespace Common {

        // ========================================
        // Block 클래스 (서버와 클라이언트 공유)
        // ========================================

        class Block {
        public:
            // 생성자
            Block(BlockType type, PlayerColor player = PlayerColor::None);

            // 변환 함수들
            void setRotation(Rotation rotation);
            void setFlipState(FlipState flip);
            void setPlayer(PlayerColor player) { m_player = player; }

            void rotateClockwise();
            void rotateCounterclockwise();
            void flipHorizontal();
            void flipVertical();
            void resetTransform();

            // 형태 관련 함수들
            PositionList getCurrentShape() const;
            PositionList getAbsolutePositions(const Position& basePos) const;

            // 크기와 바운딩 정보
            int getSize() const;
            struct BoundingRect {
                int left, top, width, height;
                BoundingRect() : left(0), top(0), width(1), height(1) {}
                BoundingRect(int l, int t, int w, int h) : left(l), top(t), width(w), height(h) {}
            };
            BoundingRect getBoundingRect() const;

            // 충돌 및 유효성 검사
            bool wouldCollideAt(const Position& basePos, const PositionList& occupiedCells) const;
            bool isValidPlacement(const Position& basePos, int boardSize) const;

            // Getters
            BlockType getType() const { return m_type; }
            PlayerColor getPlayer() const { return m_player; }
            Rotation getRotation() const { return m_rotation; }
            FlipState getFlipState() const { return m_flipState; }

            // 블록 모양 데이터 접근
            static PositionList getBaseShape(BlockType type);
            static bool isValidBlockType(BlockType type);

        private:
            // 멤버 변수
            BlockType m_type;
            PlayerColor m_player;
            Rotation m_rotation;
            FlipState m_flipState;

            // 정적 블록 모양 데이터
            static const std::map<BlockType, PositionList> s_blockShapes;

            // 내부 헬퍼 함수들
            PositionList applyRotation(const PositionList& shape, Rotation rotation) const;
            PositionList applyFlip(const PositionList& shape, FlipState flip) const;
            PositionList normalizeShape(const PositionList& shape) const;
        };

        // ========================================
        // BlockFactory 클래스 (서버와 클라이언트 공유)
        // ========================================

        class BlockFactory {
        public:
            // 블록 생성
            static Block createBlock(BlockType type, PlayerColor player = PlayerColor::None);
            static std::vector<Block> createPlayerSet(PlayerColor player);
            static std::vector<Block> createAllBlocks();

            // 블록 정보
            static std::string getBlockName(BlockType type);
            static std::string getBlockDescription(BlockType type);
            static int getBlockScore(BlockType type);

            // 블록 타입 관련
            static bool isValidBlockType(BlockType type);
            static std::vector<BlockType> getAllBlockTypes();

            // 블록 분류
            static int getBlockCategory(BlockType type); // 1, 2, 3, 4, 5칸 블록 구분
            static std::vector<BlockType> getBlocksBySize(int size);
        };

    } // namespace Common
} // namespace Blokus