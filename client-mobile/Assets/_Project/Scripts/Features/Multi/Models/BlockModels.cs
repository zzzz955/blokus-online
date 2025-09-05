using UnityEngine;
using System.Collections.Generic;

namespace Features.Multi.Models
{
    /// <summary>
    /// 블록 타입 열거형
    /// 블로커스의 다양한 블록 모양들
    /// </summary>
    public enum BlockType
    {
        // 1칸 블록
        Single = 1,
        
        // 2칸 블록
        Domino = 2,
        
        // 3칸 블록
        TriominoI = 3,  // 일자형
        TriominoL = 4,  // L자형
        
        // 4칸 블록
        TetrominoI = 5, // 일자형
        TetrominoO = 6, // 정사각형
        TetrominoT = 7, // T자형
        TetrominoS = 8, // S자형
        TetrominoZ = 9, // Z자형
        TetrominoJ = 10, // J자형
        TetrominoL = 11, // L자형
        
        // 5칸 블록 (블로커스 고유)
        PentominoF = 12,
        PentominoI = 13,
        PentominoL = 14,
        PentominoN = 15,
        PentominoP = 16,
        PentominoT = 17,
        PentominoU = 18,
        PentominoV = 19,
        PentominoW = 20,
        PentominoX = 21,
        PentominoY = 22,
        PentominoZ = 23
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
        /// BlockPlacement 생성자
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
        /// 점유하는 셀들 계산 (Stub 구현)
        /// </summary>
        private void CalculateOccupiedCells()
        {
            occupiedCells = new List<Vector2Int>();
            
            // Stub: 간단한 블록 모양들만 구현
            switch (blockType)
            {
                case BlockType.Single:
                    occupiedCells.Add(position);
                    break;
                    
                case BlockType.Domino:
                    occupiedCells.Add(position);
                    occupiedCells.Add(position + Vector2Int.right);
                    break;
                    
                case BlockType.TriominoI:
                    for (int i = 0; i < 3; i++)
                    {
                        occupiedCells.Add(position + Vector2Int.right * i);
                    }
                    break;
                    
                case BlockType.TriominoL:
                    occupiedCells.Add(position);
                    occupiedCells.Add(position + Vector2Int.right);
                    occupiedCells.Add(position + Vector2Int.down);
                    break;
                    
                default:
                    // 기본적으로 단일 셀
                    occupiedCells.Add(position);
                    break;
            }
            
            // 회전과 뒤집힘 적용 (Stub에서는 생략)
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
                    
                case BlockType.TriominoI:
                case BlockType.TriominoL:
                    return 3;
                    
                case BlockType.TetrominoI:
                case BlockType.TetrominoO:
                case BlockType.TetrominoT:
                case BlockType.TetrominoS:
                case BlockType.TetrominoZ:
                case BlockType.TetrominoJ:
                case BlockType.TetrominoL:
                    return 4;
                    
                case BlockType.PentominoF:
                case BlockType.PentominoI:
                case BlockType.PentominoL:
                case BlockType.PentominoN:
                case BlockType.PentominoP:
                case BlockType.PentominoT:
                case BlockType.PentominoU:
                case BlockType.PentominoV:
                case BlockType.PentominoW:
                case BlockType.PentominoX:
                case BlockType.PentominoY:
                case BlockType.PentominoZ:
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
                case BlockType.TriominoI: return "I(3)";
                case BlockType.TriominoL: return "L(3)";
                case BlockType.TetrominoI: return "I(4)";
                case BlockType.TetrominoO: return "정사각형";
                case BlockType.TetrominoT: return "T자";
                case BlockType.TetrominoS: return "S자";
                case BlockType.TetrominoZ: return "Z자";
                case BlockType.TetrominoJ: return "J자";
                case BlockType.TetrominoL: return "L자";
                default: return blockType.ToString();
            }
        }
    }
}