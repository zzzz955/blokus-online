using UnityEngine;
using System.Collections.Generic;

namespace Features.Multi.Models
{
    /// <summary>
    /// 블록 타입 열거형 (서버와 동일한 매핑)
    /// common/include/Types.h와 일치
    /// </summary>
    public enum BlockType
    {
        // 1칸 블록
        Single = 1,
        
        // 2칸 블록
        Domino = 2,
        
        // 3칸 블록
        TrioLine = 3,       // 3일자 (구 TriominoI)
        TrioAngle = 4,      // 3꺾임 (구 TriominoL)
        
        // 4칸 블록 (테트로미노)
        Tetro_I = 5,        // 4일자 (구 TetrominoI)
        Tetro_O = 6,        // 정사각형 (구 TetrominoO)
        Tetro_T = 7,        // T자 (구 TetrominoT)
        Tetro_L = 8,        // L자 (구 TetrominoL)
        Tetro_S = 9,        // S자 (구 TetrominoS, TetrominoZ)
        
        // 5칸 블록 (펜토미노) - 서버와 정확히 일치
        Pento_F = 10,       // F자
        Pento_I = 11,       // 5일자 (이전에 TetrominoL=11이었던 부분!)
        Pento_L = 12,       // 5L자
        Pento_N = 13,       // N자
        Pento_P = 14,       // P자
        Pento_T = 15,       // 5T자
        Pento_U = 16,       // U자
        Pento_V = 17,       // V자
        Pento_W = 18,       // W자
        Pento_X = 19,       // X자
        Pento_Y = 20,       // Y자
        Pento_Z = 21        // 5Z자
    }
    
    /// <summary>
    /// 블록 배치 정보
    /// 게임 보드에 블록을 배치할 때 사용하는 데이터
    /// </summary>
    [System.Serializable]
    public struct BlockPlacement
    {
        public int playerId;                    // 플레이어 ID (0-3)
        public PlayerColor playerColor;         // 플레이어 색상
        public BlockType blockType;             // 블록 타입
        public Vector2Int position;             // 보드에서의 위치 (좌상단 기준)
        public int rotation;                    // 회전 (0, 90, 180, 270도)
        public bool isFlipped;                  // 뒤집힘 여부
        public List<Vector2Int> occupiedCells;  // 실제로 차지하는 셀들의 좌표
        
        /// <summary>
        /// BlockPlacement 생성자 (기본 - 점유셀 자동 계산)
        /// </summary>
        public BlockPlacement(int playerId, BlockType blockType, Vector2Int position, int rotation = 0, bool isFlipped = false)
        {
            this.playerId = playerId;
            this.playerColor = PlayerColorExtensions.FromPlayerId(playerId);
            this.blockType = blockType;
            this.position = position;
            this.rotation = rotation;
            this.isFlipped = isFlipped;
            this.occupiedCells = new List<Vector2Int>();
            
            // 점유 셀 계산 (실제 구현에서는 블록 모양에 따라 계산)
            CalculateOccupiedCells();
        }
        
        /// <summary>
        /// BlockPlacement 생성자 (서버 좌표 직접 사용 - 개선된 동기화)
        /// </summary>
        public BlockPlacement(int playerId, BlockType blockType, Vector2Int position, int rotation, bool isFlipped, List<Vector2Int> occupiedCells)
        {
            this.playerId = playerId;
            this.playerColor = PlayerColorExtensions.FromPlayerId(playerId);
            this.blockType = blockType;
            this.position = position;
            this.rotation = rotation;
            this.isFlipped = isFlipped;
            this.occupiedCells = occupiedCells ?? new List<Vector2Int>();
        }
        
        /// <summary>
        /// 점유하는 셀들 계산
        /// </summary>
        private void CalculateOccupiedCells()
        {
            occupiedCells = new List<Vector2Int>();
            
            // 기본 블록 모양 정의 (회전과 뒤집힘 적용 전)
            List<Vector2Int> baseShape = GetBaseBlockShape(blockType);
            
            // 회전 적용
            if (rotation != 0)
            {
                baseShape = ApplyRotation(baseShape, rotation);
            }
            
            // 뒤집힘 적용
            if (isFlipped)
            {
                baseShape = ApplyFlip(baseShape);
            }
            
            // 위치 오프셋 적용
            foreach (Vector2Int cell in baseShape)
            {
                occupiedCells.Add(position + cell);
            }
        }
        
        /// <summary>
        /// 블록 타입에 따른 기본 모양 반환 (0,0 기준)
        /// </summary>
        private List<Vector2Int> GetBaseBlockShape(BlockType blockType)
        {
            List<Vector2Int> shape = new List<Vector2Int>();
            
            switch (blockType)
            {
                case BlockType.Single:
                    shape.Add(Vector2Int.zero);
                    break;
                    
                case BlockType.Domino:
                    shape.Add(Vector2Int.zero);
                    shape.Add(Vector2Int.right);
                    break;
                    
                // 3칸 블록 - 서버와 일치하는 형태
                case BlockType.TrioLine:
                    shape.Add(Vector2Int.zero);
                    shape.Add(Vector2Int.right);
                    shape.Add(Vector2Int.right * 2);
                    break;
                    
                case BlockType.TrioAngle:
                    shape.Add(Vector2Int.zero);
                    shape.Add(Vector2Int.right);
                    shape.Add(Vector2Int.down + Vector2Int.right);
                    break;
                    
                // 4칸 블록 (테트로미노) - 서버와 일치하는 형태
                case BlockType.Tetro_I:
                    shape.Add(Vector2Int.zero);
                    shape.Add(Vector2Int.right);
                    shape.Add(Vector2Int.right * 2);
                    shape.Add(Vector2Int.right * 3);
                    break;
                    
                case BlockType.Tetro_O:
                    shape.Add(Vector2Int.zero);
                    shape.Add(Vector2Int.right);
                    shape.Add(Vector2Int.down);
                    shape.Add(Vector2Int.down + Vector2Int.right);
                    break;
                    
                case BlockType.Tetro_T:
                    shape.Add(Vector2Int.zero);
                    shape.Add(Vector2Int.right);
                    shape.Add(Vector2Int.right * 2);
                    shape.Add(Vector2Int.down + Vector2Int.right);
                    break;
                    
                case BlockType.Tetro_L:
                    shape.Add(Vector2Int.zero);
                    shape.Add(Vector2Int.right);
                    shape.Add(Vector2Int.right * 2);
                    shape.Add(Vector2Int.down);
                    break;
                    
                case BlockType.Tetro_S:
                    shape.Add(Vector2Int.zero);
                    shape.Add(Vector2Int.right);
                    shape.Add(Vector2Int.down + Vector2Int.right);
                    shape.Add(Vector2Int.down + Vector2Int.right * 2);
                    break;
                    
                // 5칸 펜토미노 블록들 - 서버 (row, col) → Unity Vector2Int(col, row)
                case BlockType.Pento_F:
                    // 서버: {0, 1}, {0, 2}, {1, 0}, {1, 1}, {2, 1}
                    shape.Add(new Vector2Int(1, 0)); // {0,1} → (1,0)
                    shape.Add(new Vector2Int(2, 0)); // {0,2} → (2,0)
                    shape.Add(new Vector2Int(0, 1)); // {1,0} → (0,1)
                    shape.Add(new Vector2Int(1, 1)); // {1,1} → (1,1)
                    shape.Add(new Vector2Int(1, 2)); // {2,1} → (1,2)
                    break;
                    
                case BlockType.Pento_I:
                    // 서버: {0, 0}, {0, 1}, {0, 2}, {0, 3}, {0, 4}
                    shape.Add(new Vector2Int(0, 0)); // {0,0} → (0,0)
                    shape.Add(new Vector2Int(1, 0)); // {0,1} → (1,0)
                    shape.Add(new Vector2Int(2, 0)); // {0,2} → (2,0)
                    shape.Add(new Vector2Int(3, 0)); // {0,3} → (3,0)
                    shape.Add(new Vector2Int(4, 0)); // {0,4} → (4,0)
                    break;
                    
                case BlockType.Pento_L:
                    // 서버: {0, 0}, {0, 1}, {0, 2}, {0, 3}, {1, 0}
                    shape.Add(new Vector2Int(0, 0)); // {0,0} → (0,0)
                    shape.Add(new Vector2Int(1, 0)); // {0,1} → (1,0)
                    shape.Add(new Vector2Int(2, 0)); // {0,2} → (2,0)
                    shape.Add(new Vector2Int(3, 0)); // {0,3} → (3,0)
                    shape.Add(new Vector2Int(0, 1)); // {1,0} → (0,1)
                    break;
                    
                case BlockType.Pento_N:
                    // 서버: {0, 0}, {0, 1}, {0, 2}, {1, 2}, {1, 3}
                    shape.Add(new Vector2Int(0, 0)); // {0,0} → (0,0)
                    shape.Add(new Vector2Int(1, 0)); // {0,1} → (1,0)
                    shape.Add(new Vector2Int(2, 0)); // {0,2} → (2,0)
                    shape.Add(new Vector2Int(2, 1)); // {1,2} → (2,1)
                    shape.Add(new Vector2Int(3, 1)); // {1,3} → (3,1)
                    break;
                    
                case BlockType.Pento_P:
                    // 서버: {0, 0}, {0, 1}, {1, 0}, {1, 1}, {2, 0}
                    shape.Add(new Vector2Int(0, 0)); // {0,0} → (0,0)
                    shape.Add(new Vector2Int(1, 0)); // {0,1} → (1,0)
                    shape.Add(new Vector2Int(0, 1)); // {1,0} → (0,1)
                    shape.Add(new Vector2Int(1, 1)); // {1,1} → (1,1)
                    shape.Add(new Vector2Int(0, 2)); // {2,0} → (0,2)
                    break;
                    
                case BlockType.Pento_T:
                    // 서버: {0, 0}, {0, 1}, {0, 2}, {1, 1}, {2, 1}
                    shape.Add(new Vector2Int(0, 0)); // {0,0} → (0,0)
                    shape.Add(new Vector2Int(1, 0)); // {0,1} → (1,0)
                    shape.Add(new Vector2Int(2, 0)); // {0,2} → (2,0)
                    shape.Add(new Vector2Int(1, 1)); // {1,1} → (1,1)
                    shape.Add(new Vector2Int(1, 2)); // {2,1} → (1,2)
                    break;
                    
                case BlockType.Pento_U:
                    // 서버: {0, 0}, {0, 2}, {1, 0}, {1, 1}, {1, 2} (row, col)
                    // Unity: Vector2Int(col, row)
                    shape.Add(new Vector2Int(0, 0)); // {0,0} → (0,0)
                    shape.Add(new Vector2Int(2, 0)); // {0,2} → (2,0)
                    shape.Add(new Vector2Int(0, 1)); // {1,0} → (0,1)
                    shape.Add(new Vector2Int(1, 1)); // {1,1} → (1,1)
                    shape.Add(new Vector2Int(2, 1)); // {1,2} → (2,1)
                    break;
                    
                case BlockType.Pento_V:
                    // 서버: {0, 0}, {1, 0}, {2, 0}, {2, 1}, {2, 2}
                    shape.Add(new Vector2Int(0, 0)); // {0,0} → (0,0)
                    shape.Add(new Vector2Int(0, 1)); // {1,0} → (0,1)
                    shape.Add(new Vector2Int(0, 2)); // {2,0} → (0,2)
                    shape.Add(new Vector2Int(1, 2)); // {2,1} → (1,2)
                    shape.Add(new Vector2Int(2, 2)); // {2,2} → (2,2)
                    break;
                    
                case BlockType.Pento_W:
                    // 서버: {0, 0}, {1, 0}, {1, 1}, {2, 1}, {2, 2}
                    shape.Add(new Vector2Int(0, 0)); // {0,0} → (0,0)
                    shape.Add(new Vector2Int(0, 1)); // {1,0} → (0,1)
                    shape.Add(new Vector2Int(1, 1)); // {1,1} → (1,1)
                    shape.Add(new Vector2Int(1, 2)); // {2,1} → (1,2)
                    shape.Add(new Vector2Int(2, 2)); // {2,2} → (2,2)
                    break;
                    
                case BlockType.Pento_X:
                    // 서버: {0, 1}, {1, 0}, {1, 1}, {1, 2}, {2, 1}
                    shape.Add(new Vector2Int(1, 0)); // {0,1} → (1,0)
                    shape.Add(new Vector2Int(0, 1)); // {1,0} → (0,1)
                    shape.Add(new Vector2Int(1, 1)); // {1,1} → (1,1)
                    shape.Add(new Vector2Int(2, 1)); // {1,2} → (2,1)
                    shape.Add(new Vector2Int(1, 2)); // {2,1} → (1,2)
                    break;
                    
                case BlockType.Pento_Y:
                    // 서버: {0, 0}, {0, 1}, {0, 2}, {0, 3}, {1, 1}
                    shape.Add(new Vector2Int(0, 0)); // {0,0} → (0,0)
                    shape.Add(new Vector2Int(1, 0)); // {0,1} → (1,0)
                    shape.Add(new Vector2Int(2, 0)); // {0,2} → (2,0)
                    shape.Add(new Vector2Int(3, 0)); // {0,3} → (3,0)
                    shape.Add(new Vector2Int(1, 1)); // {1,1} → (1,1)
                    break;
                    
                case BlockType.Pento_Z:
                    // 서버: {0, 0}, {0, 1}, {1, 1}, {2, 1}, {2, 2}
                    shape.Add(new Vector2Int(0, 0)); // {0,0} → (0,0)
                    shape.Add(new Vector2Int(1, 0)); // {0,1} → (1,0)
                    shape.Add(new Vector2Int(1, 1)); // {1,1} → (1,1)
                    shape.Add(new Vector2Int(1, 2)); // {2,1} → (1,2)
                    shape.Add(new Vector2Int(2, 2)); // {2,2} → (2,2)
                    break;
                    
                default:
                    // 알 수 없는 블록 타입은 단일 셀로 처리
                    shape.Add(Vector2Int.zero);
                    break;
            }
            
            return shape;
        }
        
        /// <summary>
        /// 회전 적용 (90도씩 시계 방향)
        /// </summary>
        private List<Vector2Int> ApplyRotation(List<Vector2Int> shape, int rotation)
        {
            List<Vector2Int> rotatedShape = new List<Vector2Int>();
            
            int rotationSteps = (rotation / 90) % 4;
            
            foreach (Vector2Int cell in shape)
            {
                Vector2Int rotatedCell = cell;
                
                for (int i = 0; i < rotationSteps; i++)
                {
                    // 90도 시계방향 회전: (x, y) -> (y, -x)
                    rotatedCell = new Vector2Int(rotatedCell.y, -rotatedCell.x);
                }
                
                rotatedShape.Add(rotatedCell);
            }
            
            return rotatedShape;
        }
        
        /// <summary>
        /// 뒤집힘 적용 (세로축 기준)
        /// </summary>
        private List<Vector2Int> ApplyFlip(List<Vector2Int> shape)
        {
            List<Vector2Int> flippedShape = new List<Vector2Int>();
            
            foreach (Vector2Int cell in shape)
            {
                // 세로축 기준 뒤집기: (x, y) -> (-x, y)
                flippedShape.Add(new Vector2Int(-cell.x, cell.y));
            }
            
            return flippedShape;
        }
        
        /// <summary>
        /// 블록이 유효한 위치에 있는지 확인
        /// </summary>
        public bool IsValidPlacement(int boardSize = 20)
        {
            foreach (var cell in occupiedCells)
            {
                if (cell.x < 0 || cell.x >= boardSize || cell.y < 0 || cell.y >= boardSize)
                {
                    return false;
                }
            }
            return true;
        }
        
        /// <summary>
        /// 특정 셀을 포함하는지 확인
        /// </summary>
        public bool ContainsCell(Vector2Int cell)
        {
            return occupiedCells.Contains(cell);
        }
        
        /// <summary>
        /// 블록이 차지하는 셀 수 반환
        /// </summary>
        public int GetCellCount()
        {
            return occupiedCells.Count;
        }
    }
    
    /// <summary>
    /// BlockType 유틸리티 확장 메서드들
    /// </summary>
    public static class BlockTypeExtensions
    {
        /// <summary>
        /// 블록 타입의 기본 셀 수 가져오기
        /// </summary>
        public static int GetCellCount(this BlockType blockType)
        {
            switch (blockType)
            {
                case BlockType.Single:
                    return 1;
                    
                case BlockType.Domino:
                    return 2;
                    
                case BlockType.TrioLine:
                case BlockType.TrioAngle:
                    return 3;
                    
                case BlockType.Tetro_I:
                case BlockType.Tetro_O:
                case BlockType.Tetro_T:
                case BlockType.Tetro_L:
                case BlockType.Tetro_S:
                    return 4;
                    
                case BlockType.Pento_F:
                case BlockType.Pento_I:
                case BlockType.Pento_L:
                case BlockType.Pento_N:
                case BlockType.Pento_P:
                case BlockType.Pento_T:
                case BlockType.Pento_U:
                case BlockType.Pento_V:
                case BlockType.Pento_W:
                case BlockType.Pento_X:
                case BlockType.Pento_Y:
                case BlockType.Pento_Z:
                    return 5;
                    
                default:
                    return 1;
            }
        }
        
        /// <summary>
        /// 블록 타입의 표시 이름 가져오기
        /// </summary>
        public static string GetDisplayName(this BlockType blockType)
        {
            switch (blockType)
            {
                case BlockType.Single: return "점";
                case BlockType.Domino: return "막대";
                case BlockType.TrioLine: return "3일자";
                case BlockType.TrioAngle: return "3꺾임";
                case BlockType.Tetro_I: return "4일자";
                case BlockType.Tetro_O: return "정사각형";
                case BlockType.Tetro_T: return "T자";
                case BlockType.Tetro_L: return "L자";
                case BlockType.Tetro_S: return "S자";
                case BlockType.Pento_F: return "F자";
                case BlockType.Pento_I: return "5일자";
                case BlockType.Pento_L: return "5L자";
                case BlockType.Pento_N: return "N자";
                case BlockType.Pento_P: return "P자";
                case BlockType.Pento_T: return "5T자";
                case BlockType.Pento_U: return "U자";
                case BlockType.Pento_V: return "V자";
                case BlockType.Pento_W: return "W자";
                case BlockType.Pento_X: return "X자";
                case BlockType.Pento_Y: return "Y자";
                case BlockType.Pento_Z: return "5Z자";
                default: return blockType.ToString();
            }
        }
    }
}