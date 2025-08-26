using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Shared.Models;
namespace Shared.Models{
    /// <summary>
    /// Unity 블로쿠스 블록 클래스 (C++ Block.h/cpp 포팅)
    /// 21가지 블록 모양과 회전/뒤집기 변환 처리
    /// </summary>
    public class Block
    {
        // ========================================
        // 정적 블록 모양 데이터 (C++ 포팅)
        // ========================================
        
        private static readonly Dictionary<BlockType, List<Position>> BlockShapes = new Dictionary<BlockType, List<Position>>
        {
            // 1칸 블록
            { BlockType.Single, new List<Position> { new Position(0, 0) } },

            // 2칸 블록
            { BlockType.Domino, new List<Position> { new Position(0, 0), new Position(0, 1) } },

            // 3칸 블록
            { BlockType.TrioLine, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(0, 2) } },
            { BlockType.TrioAngle, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(1, 1) } },

            // 4칸 블록 (테트로미노)
            { BlockType.Tetro_I, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(0, 2), new Position(0, 3) } },
            { BlockType.Tetro_O, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(1, 0), new Position(1, 1) } },
            { BlockType.Tetro_T, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(0, 2), new Position(1, 1) } },
            { BlockType.Tetro_L, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(0, 2), new Position(1, 0) } },
            { BlockType.Tetro_S, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(1, 1), new Position(1, 2) } },

            // 5칸 블록 (펜토미노)
            { BlockType.Pento_F, new List<Position> { new Position(0, 1), new Position(0, 2), new Position(1, 0), new Position(1, 1), new Position(2, 1) } },
            { BlockType.Pento_I, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(0, 2), new Position(0, 3), new Position(0, 4) } },
            { BlockType.Pento_L, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(0, 2), new Position(0, 3), new Position(1, 0) } },
            { BlockType.Pento_N, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(0, 2), new Position(1, 2), new Position(1, 3) } },
            { BlockType.Pento_P, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(1, 0), new Position(1, 1), new Position(2, 0) } },
            { BlockType.Pento_T, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(0, 2), new Position(1, 1), new Position(2, 1) } },
            { BlockType.Pento_U, new List<Position> { new Position(0, 0), new Position(0, 2), new Position(1, 0), new Position(1, 1), new Position(1, 2) } },
            { BlockType.Pento_V, new List<Position> { new Position(0, 0), new Position(1, 0), new Position(2, 0), new Position(2, 1), new Position(2, 2) } },
            { BlockType.Pento_W, new List<Position> { new Position(0, 0), new Position(1, 0), new Position(1, 1), new Position(2, 1), new Position(2, 2) } },
            { BlockType.Pento_X, new List<Position> { new Position(0, 1), new Position(1, 0), new Position(1, 1), new Position(1, 2), new Position(2, 1) } },
            { BlockType.Pento_Y, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(0, 2), new Position(0, 3), new Position(1, 1) } },
            { BlockType.Pento_Z, new List<Position> { new Position(0, 0), new Position(0, 1), new Position(1, 1), new Position(2, 1), new Position(2, 2) } }
        };

        // ========================================
        // 멤버 변수
        // ========================================
        
        private BlockType blockType;
        private PlayerColor playerColor;
        private Rotation rotation;
        private FlipState flipState;

        // ========================================
        // 생성자
        // ========================================
        
        /// <summary>
        /// 블록 생성자
        /// </summary>
        public Block(BlockType type, PlayerColor player = PlayerColor.None)
        {
            // 유효한 블록 타입인지 확인
            if (!BlockShapes.ContainsKey(type))
            {
                Debug.LogError($"유효하지 않은 블록 타입: {type}. Single로 대체됩니다.");
                blockType = BlockType.Single;
            }
            else
            {
                blockType = type;
            }

            playerColor = player;
            rotation = Rotation.Degree_0;
            flipState = FlipState.Normal;
        }

        // ========================================
        // 프로퍼티
        // ========================================
        
        public BlockType Type => blockType;
        public PlayerColor Player => playerColor;
        public Rotation CurrentRotation => rotation;
        public FlipState CurrentFlipState => flipState;

        // ========================================
        // 변환 함수들
        // ========================================
        
        /// <summary>
        /// 회전 상태 설정
        /// </summary>
        public void SetRotation(Rotation newRotation)
        {
            rotation = newRotation;
        }

        /// <summary>
        /// 뒤집기 상태 설정
        /// </summary>
        public void SetFlipState(FlipState newFlipState)
        {
            flipState = newFlipState;
        }

        /// <summary>
        /// 플레이어 색상 설정
        /// </summary>
        public void SetPlayer(PlayerColor player)
        {
            playerColor = player;
        }

        /// <summary>
        /// 시계 방향 회전
        /// </summary>
        public void RotateClockwise()
        {
            int currentRotation = (int)rotation;
            rotation = (Rotation)((currentRotation + 1) % 4);
        }

        /// <summary>
        /// 반시계 방향 회전
        /// </summary>
        public void RotateCounterClockwise()
        {
            int currentRotation = (int)rotation;
            rotation = (Rotation)((currentRotation + 3) % 4);
        }

        /// <summary>
        /// 수평 뒤집기
        /// </summary>
        public void FlipHorizontal()
        {
            flipState = flipState switch
            {
                FlipState.Normal => FlipState.Horizontal,
                FlipState.Horizontal => FlipState.Normal,
                FlipState.Vertical => FlipState.Both,
                FlipState.Both => FlipState.Vertical,
                _ => FlipState.Normal
            };
        }

        /// <summary>
        /// 수직 뒤집기
        /// </summary>
        public void FlipVertical()
        {
            flipState = flipState switch
            {
                FlipState.Normal => FlipState.Vertical,
                FlipState.Vertical => FlipState.Normal,
                FlipState.Horizontal => FlipState.Both,
                FlipState.Both => FlipState.Horizontal,
                _ => FlipState.Normal
            };
        }

        /// <summary>
        /// 변환 상태 초기화
        /// </summary>
        public void ResetTransform()
        {
            rotation = Rotation.Degree_0;
            flipState = FlipState.Normal;
        }

        // ========================================
        // 형태 관련 함수들
        // ========================================
        
        /// <summary>
        /// 현재 변환이 적용된 블록 모양 반환
        /// </summary>
        public List<Position> GetCurrentShape()
        {
            if (!BlockShapes.TryGetValue(blockType, out List<Position> baseShape))
            {
                return new List<Position> { new Position(0, 0) };
            }

            List<Position> shape = new List<Position>(baseShape);

            // 뒤집기 적용
            shape = ApplyFlip(shape, flipState);

            // 회전 적용
            shape = ApplyRotation(shape, rotation);

            // 정규화 (최소 좌표를 (0,0)으로)
            shape = NormalizeShape(shape);

            return shape;
        }

        /// <summary>
        /// 기준 위치에서의 절대 좌표 반환
        /// </summary>
        public List<Position> GetAbsolutePositions(Position basePos)
        {
            List<Position> currentShape = GetCurrentShape();
            List<Position> absolutePositions = new List<Position>();

            foreach (Position relativePos in currentShape)
            {
                Position absolutePos = new Position(
                    basePos.row + relativePos.row,
                    basePos.col + relativePos.col
                );
                absolutePositions.Add(absolutePos);
            }

            return absolutePositions;
        }

        /// <summary>
        /// 블록 크기 (셀 개수) 반환
        /// </summary>
        public int GetSize()
        {
            return GetCurrentShape().Count;
        }

        /// <summary>
        /// 바운딩 박스 정보
        /// </summary>
        [System.Serializable]
        public struct BoundingRect
        {
            public int left, top, width, height;

            public BoundingRect(int left, int top, int width, int height)
            {
                this.left = left;
                this.top = top;
                this.width = width;
                this.height = height;
            }
        }

        /// <summary>
        /// 바운딩 박스 반환
        /// </summary>
        public BoundingRect GetBoundingRect()
        {
            List<Position> shape = GetCurrentShape();
            if (shape.Count == 0)
            {
                return new BoundingRect(0, 0, 1, 1);
            }

            int minRow = shape[0].row, maxRow = shape[0].row;
            int minCol = shape[0].col, maxCol = shape[0].col;

            foreach (Position pos in shape)
            {
                minRow = Mathf.Min(minRow, pos.row);
                maxRow = Mathf.Max(maxRow, pos.row);
                minCol = Mathf.Min(minCol, pos.col);
                maxCol = Mathf.Max(maxCol, pos.col);
            }

            return new BoundingRect(minCol, minRow, maxCol - minCol + 1, maxRow - minRow + 1);
        }

        // ========================================
        // 충돌 및 유효성 검사
        // ========================================
        
        /// <summary>
        /// 지정된 위치에서 점유된 셀들과 충돌하는지 확인
        /// </summary>
        public bool WouldCollideAt(Position basePos, List<Position> occupiedCells)
        {
            List<Position> absolutePositions = GetAbsolutePositions(basePos);

            foreach (Position blockPos in absolutePositions)
            {
                if (occupiedCells.Contains(blockPos))
                {
                    return true; // 충돌 발생
                }
            }

            return false; // 충돌 없음
        }

        /// <summary>
        /// 보드 범위 내 유효한 배치인지 확인
        /// </summary>
        public bool IsValidPlacement(Position basePos, int boardSize = 20)
        {
            List<Position> absolutePositions = GetAbsolutePositions(basePos);

            foreach (Position pos in absolutePositions)
            {
                if (!ValidationUtility.IsValidPosition(pos))
                {
                    return false;
                }
            }

            return true;
        }

        // ========================================
        // 정적 메서드들
        // ========================================
        
        /// <summary>
        /// 기본 블록 모양 반환 (변환 적용 안됨)
        /// </summary>
        public static List<Position> GetBaseShape(BlockType type)
        {
            if (BlockShapes.TryGetValue(type, out List<Position> shape))
            {
                return new List<Position>(shape);
            }
            return new List<Position> { new Position(0, 0) };
        }

        /// <summary>
        /// 유효한 블록 타입인지 확인
        /// </summary>
        public static bool IsValidBlockType(BlockType type)
        {
            return BlockShapes.ContainsKey(type);
        }

        // ========================================
        // 내부 헬퍼 함수들
        // ========================================
        
        /// <summary>
        /// 회전 변환 적용
        /// </summary>
        private List<Position> ApplyRotation(List<Position> shape, Rotation rotationAngle)
        {
            List<Position> rotatedShape = new List<Position>();

            foreach (Position pos in shape)
            {
                Position newPos = rotationAngle switch
                {
                    Rotation.Degree_0 => pos, // 변화 없음
                    Rotation.Degree_90 => new Position(pos.col, -pos.row), // 90도 시계방향: (r, c) -> (c, -r)
                    Rotation.Degree_180 => new Position(-pos.row, -pos.col), // 180도: (r, c) -> (-r, -c)
                    Rotation.Degree_270 => new Position(-pos.col, pos.row), // 270도 시계방향: (r, c) -> (-c, r)
                    _ => pos
                };

                rotatedShape.Add(newPos);
            }

            return rotatedShape;
        }

        /// <summary>
        /// 뒤집기 변환 적용
        /// </summary>
        private List<Position> ApplyFlip(List<Position> shape, FlipState flip)
        {
            List<Position> flippedShape = new List<Position>();

            foreach (Position pos in shape)
            {
                Position newPos = flip switch
                {
                    FlipState.Normal => pos, // 변화 없음
                    FlipState.Horizontal => new Position(pos.row, -pos.col), // 수평 뒤집기: (r, c) -> (r, -c)
                    FlipState.Vertical => new Position(-pos.row, pos.col), // 수직 뒤집기: (r, c) -> (-r, c)
                    FlipState.Both => new Position(-pos.row, -pos.col), // 양쪽 뒤집기: (r, c) -> (-r, -c)
                    _ => pos
                };

                flippedShape.Add(newPos);
            }

            return flippedShape;
        }

        /// <summary>
        /// 모양 정규화 (최소 좌표를 (0,0)으로)
        /// </summary>
        private List<Position> NormalizeShape(List<Position> shape)
        {
            if (shape.Count == 0)
                return shape;

            // 최소 좌표 찾기
            int minRow = shape[0].row;
            int minCol = shape[0].col;

            foreach (Position pos in shape)
            {
                minRow = Mathf.Min(minRow, pos.row);
                minCol = Mathf.Min(minCol, pos.col);
            }

            // 정규화된 형태로 변환
            List<Position> normalizedShape = new List<Position>();
            foreach (Position pos in shape)
            {
                normalizedShape.Add(new Position(pos.row - minRow, pos.col - minCol));
            }

            return normalizedShape;
        }
    }

    // ========================================
    // BlockFactory 클래스 (C++ BlockFactory 포팅)
    // ========================================
    
    /// <summary>
    /// 블록 팩토리 - 블록 생성 및 관리 유틸리티
    /// </summary>
    public static class BlockFactory
    {
        /// <summary>
        /// 블록 생성
        /// </summary>
        public static Block CreateBlock(BlockType type, PlayerColor player = PlayerColor.None)
        {
            return new Block(type, player);
        }

        /// <summary>
        /// 플레이어용 전체 블록 세트 생성 (21개)
        /// </summary>
        public static List<Block> CreatePlayerSet(PlayerColor player)
        {
            List<Block> playerBlocks = new List<Block>();
            List<BlockType> allTypes = GetAllBlockTypes();

            foreach (BlockType type in allTypes)
            {
                playerBlocks.Add(new Block(type, player));
            }

            return playerBlocks;
        }

        /// <summary>
        /// 모든 플레이어의 블록 생성
        /// </summary>
        public static List<Block> CreateAllBlocks()
        {
            List<Block> allBlocks = new List<Block>();
            List<PlayerColor> players = new List<PlayerColor>
            {
                PlayerColor.Blue, PlayerColor.Yellow,
                PlayerColor.Red, PlayerColor.Green
            };

            foreach (PlayerColor player in players)
            {
                List<Block> playerBlocks = CreatePlayerSet(player);
                allBlocks.AddRange(playerBlocks);
            }

            return allBlocks;
        }

        /// <summary>
        /// 블록 이름 반환
        /// </summary>
        public static string GetBlockName(BlockType type)
        {
            return type switch
            {
                BlockType.Single => "단일",
                BlockType.Domino => "도미노",
                BlockType.TrioLine => "3직선",
                BlockType.TrioAngle => "3꺾임",
                BlockType.Tetro_I => "I자",
                BlockType.Tetro_O => "O자",
                BlockType.Tetro_T => "T자",
                BlockType.Tetro_L => "L자",
                BlockType.Tetro_S => "S자",
                BlockType.Pento_F => "F자",
                BlockType.Pento_I => "5직선",
                BlockType.Pento_L => "5L자",
                BlockType.Pento_N => "N자",
                BlockType.Pento_P => "P자",
                BlockType.Pento_T => "5T자",
                BlockType.Pento_U => "U자",
                BlockType.Pento_V => "V자",
                BlockType.Pento_W => "W자",
                BlockType.Pento_X => "X자",
                BlockType.Pento_Y => "Y자",
                BlockType.Pento_Z => "5Z자",
                _ => "알 수 없음"
            };
        }

        /// <summary>
        /// 블록 설명 반환
        /// </summary>
        public static string GetBlockDescription(BlockType type)
        {
            return $"{GetBlockName(type)} ({GetBlockScore(type)}칸)";
        }

        /// <summary>
        /// 블록 점수 반환 (칸 수)
        /// </summary>
        public static int GetBlockScore(BlockType type)
        {
            return type switch
            {
                BlockType.Single => 1,
                BlockType.Domino => 2,
                BlockType.TrioLine => 3,
                BlockType.TrioAngle => 3,
                BlockType.Tetro_I => 4,
                BlockType.Tetro_O => 4,
                BlockType.Tetro_T => 4,
                BlockType.Tetro_L => 4,
                BlockType.Tetro_S => 4,
                _ => 5 // 펜토미노는 모두 5점
            };
        }

        /// <summary>
        /// 유효한 블록 타입인지 확인
        /// </summary>
        public static bool IsValidBlockType(BlockType type)
        {
            return Block.IsValidBlockType(type);
        }

        /// <summary>
        /// 모든 블록 타입 반환
        /// </summary>
        public static List<BlockType> GetAllBlockTypes()
        {
            return new List<BlockType>
            {
                BlockType.Single,
                BlockType.Domino,
                BlockType.TrioLine, BlockType.TrioAngle,
                BlockType.Tetro_I, BlockType.Tetro_O, BlockType.Tetro_T,
                BlockType.Tetro_L, BlockType.Tetro_S,
                BlockType.Pento_F, BlockType.Pento_I, BlockType.Pento_L,
                BlockType.Pento_N, BlockType.Pento_P, BlockType.Pento_T,
                BlockType.Pento_U, BlockType.Pento_V, BlockType.Pento_W,
                BlockType.Pento_X, BlockType.Pento_Y, BlockType.Pento_Z
            };
        }

        /// <summary>
        /// 블록 카테고리 반환 (크기별 분류)
        /// </summary>
        public static int GetBlockCategory(BlockType type)
        {
            return GetBlockScore(type); // 블록 크기 = 카테고리
        }

        /// <summary>
        /// 지정된 크기의 블록들 반환
        /// </summary>
        public static List<BlockType> GetBlocksBySize(int size)
        {
            List<BlockType> blocks = new List<BlockType>();
            List<BlockType> allTypes = GetAllBlockTypes();

            foreach (BlockType type in allTypes)
            {
                if (GetBlockScore(type) == size)
                {
                    blocks.Add(type);
                }
            }

            return blocks;
        }
    }
}