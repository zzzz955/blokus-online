// Assets/Scripts/Common/StagePayload.cs
using System.Collections.Generic;
using UnityEngine;
using BlokusUnity.Common;

namespace BlokusUnity.Common
{
    /// <summary>
    /// 싱글 게임 시작에 필요한 페이로드 (네트워크/캐시에서 주입)
    /// API 기반 스테이지 데이터와 호환
    /// </summary>
    public sealed class StagePayload
    {
        public int BoardSize = 20;
        public BlockType[] AvailableBlocks;   // null이면 기본 풀세트 사용
        public string LayoutSeedOrJson;       // 퍼즐 레이아웃/시드 (옵션)
        public string StageName;
        public int ParScore = 0;              // 별 계산 기준 (옵션)
        
        // API 확장 필드들
        public int StageNumber = 0;           // API에서 받은 스테이지 번호
        public int Difficulty = 1;            // 난이도 (1-5)
        public int TimeLimit = 0;             // 제한시간 (0이면 무제한)
        public int MaxUndoCount = 5;          // 최대 언두 횟수
        public InitialBoardData InitialBoard; // 초기 보드 상태 (파싱된 데이터)
        public int[] InitialBoardPositions;   // 원시 initial_board_state 데이터 (GameLogic.SetInitialBoardState용)
        
        /// <summary>
        /// JSONB 초기 보드 상태를 파싱하여 InitialBoardData로 변환
        /// Compact format을 기본으로 사용하여 네트워크 효율성 최적화
        /// </summary>
        public void ParseInitialBoardFromJson(string jsonData)
        {
            if (string.IsNullOrEmpty(jsonData))
            {
                InitialBoard = null;
                return;
            }
            
            try
            {
                // Primary: Compact format (네트워크 효율적)
                var compactData = JsonUtility.FromJson<ApiCompactBoardState>(jsonData);
                if (compactData.obsIdx != null || compactData.pre != null)
                {
                    InitialBoard = new InitialBoardData
                    {
                        obstacles = ParseCompactObstacles(compactData.obsIdx),
                        preplaced = ParseCompactPreplacedBlocks(compactData.pre)
                    };
                    
                    Debug.Log($"[StagePayload] 압축된 보드 상태 파싱 완료: 장애물 {InitialBoard.obstacles?.Count ?? 0}개, 사전배치 블록 {InitialBoard.preplaced?.Count ?? 0}개");
                    return;
                }
                
                // Fallback: Legacy expanded format (호환성)
                try
                {
                    var expandedData = JsonUtility.FromJson<ApiExpandedBoardState>(jsonData);
                    if (expandedData.obstacles != null || expandedData.preplaced != null)
                    {
                        InitialBoard = new InitialBoardData
                        {
                            obstacles = ParseExpandedObstacles(expandedData.obstacles),
                            preplaced = ParseExpandedPreplacedBlocks(expandedData.preplaced)
                        };
                        
                        Debug.Log($"[StagePayload] 확장된 보드 상태 파싱 완료 (레거시): 장애물 {InitialBoard.obstacles?.Count ?? 0}개, 사전배치 블록 {InitialBoard.preplaced?.Count ?? 0}개");
                        return;
                    }
                }
                catch
                {
                    // 확장 형식도 실패
                }
                
                // 빈 상태로 초기화
                InitialBoard = new InitialBoardData
                {
                    obstacles = new List<Position>(),
                    preplaced = new List<BlockPlacement>()
                };
                
                Debug.LogWarning($"[StagePayload] 알 수 없는 보드 상태 형식, 빈 상태로 초기화");
                
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[StagePayload] 초기 보드 상태 파싱 실패: {ex.Message}");
                InitialBoard = null;
            }
        }
        
        /// <summary>
        /// 확장된 형식의 장애물 데이터 파싱
        /// </summary>
        private List<Position> ParseExpandedObstacles(ApiExpandedObstacle[] obstacles)
        {
            if (obstacles == null) return null;
            
            var result = new List<Position>();
            foreach (var obstacle in obstacles)
            {
                result.Add(new Position(obstacle.x, obstacle.y));
            }
            return result;
        }
        
        /// <summary>
        /// 확장된 형식의 사전 배치된 블록 데이터 파싱
        /// </summary>
        private List<BlockPlacement> ParseExpandedPreplacedBlocks(ApiExpandedPreplacedBlock[] preplaced)
        {
            if (preplaced == null) return null;
            
            var result = new List<BlockPlacement>();
            foreach (var block in preplaced)
            {
                var placement = new BlockPlacement(
                    BlockType.Single, // Default block type, will be determined by game logic
                    new Position(block.x, block.y),
                    Rotation.Degree_0, // Default rotation
                    FlipState.Normal, // Default flip state
                    (PlayerColor)block.color
                );
                result.Add(placement);
            }
            return result;
        }
        
        /// <summary>
        /// 압축된 형식의 장애물 데이터 파싱 (선형 인덱스 → 좌표 변환)
        /// </summary>
        private List<Position> ParseCompactObstacles(int[] obsIdx)
        {
            if (obsIdx == null) return null;
            
            const int BOARD_SIZE = 20;
            var result = new List<Position>();
            foreach (var idx in obsIdx)
            {
                int x = idx % BOARD_SIZE;
                int y = idx / BOARD_SIZE;
                result.Add(new Position(x, y));
            }
            return result;
        }
        
        /// <summary>
        /// 압축된 형식의 사전 배치된 블록 데이터 파싱
        /// </summary>
        private List<BlockPlacement> ParseCompactPreplacedBlocks(int[][] pre)
        {
            if (pre == null) return null;
            
            var result = new List<BlockPlacement>();
            foreach (var blockArray in pre)
            {
                if (blockArray.Length >= 3)
                {
                    var placement = new BlockPlacement(
                        BlockType.Single, // Default block type
                        new Position(blockArray[0], blockArray[1]),
                        Rotation.Degree_0, // Default rotation
                        FlipState.Normal, // Default flip state
                        (PlayerColor)blockArray[2]
                    );
                    result.Add(placement);
                }
            }
            return result;
        }
    }
    
    /// <summary>
    /// 파싱된 초기 보드 데이터
    /// </summary>
    public class InitialBoardData
    {
        public List<Position> obstacles;          // 장애물 위치들
        public List<BlockPlacement> preplaced;    // 사전 배치된 블록들
    }
    
    // ========================================
    // API JSON 파싱용 데이터 구조체들
    // ========================================
    
    [System.Serializable]
    public class ApiInitialBoardState
    {
        public ApiObstacle[] obstacles;
        public ApiPreplacedBlock[] preplaced;
    }
    
    [System.Serializable]
    public class ApiObstacle
    {
        public int row;
        public int col;
    }
    
    [System.Serializable]
    public class ApiPreplacedBlock
    {
        public int block_type;
        public int row;
        public int col;
        public int rotation;
        public int flip_state;
        public int color;
    }
    
    // ========================================
    // 새로운 확장된 형식 (API에서 변환된 데이터)
    // ========================================
    
    [System.Serializable]
    public class ApiExpandedBoardState
    {
        public ApiExpandedObstacle[] obstacles;
        public ApiExpandedPreplacedBlock[] preplaced;
    }
    
    [System.Serializable]
    public class ApiExpandedObstacle
    {
        public int x;
        public int y;
    }
    
    [System.Serializable]
    public class ApiExpandedPreplacedBlock
    {
        public int x;
        public int y;
        public int color;
    }
    
    // ========================================
    // 압축된 형식 (데이터베이스 저장 형식)
    // ========================================
    
    [System.Serializable]
    public class ApiCompactBoardState
    {
        public int[] obsIdx;        // 장애물 선형 인덱스 배열
        public int[][] pre;         // 사전배치 블록 [x, y, color] 배열
    }
}
