using UnityEngine;
using System.Collections.Generic;
using Features.Multi.Models;

namespace Features.Multi.Core
{
    /// <summary>
    /// 멀티플레이어 게임 로직
    /// 블록 배치 검증 및 게임 상태 관리
    /// </summary>
    public class GameLogic : MonoBehaviour
    {
        [Header("게임 상태")]
        [SerializeField] private bool isGameStarted = false;
        [SerializeField] private int currentPlayerTurn = 0;
        [SerializeField] private int maxPlayers = 4;
        
        // 이벤트
        public event System.Action<int> OnTurnChanged;
        public event System.Action<int> OnPlayerScoreChanged;
        public event System.Action<GameResult> OnGameEnded;
        
        // 게임 데이터
        private int[] playerScores = new int[4];
        private bool[] playerFinished = new bool[4];
        private bool[] hasPlacedFirstBlock = new bool[4]; // 첫 블록 배치 여부
        private List<BlockPlacement> placedBlocks = new List<BlockPlacement>(); // 배치된 블록들
        private const int BOARD_SIZE = 20;
        
        /// <summary>
        /// 게임 시작
        /// </summary>
        public void StartGame()
        {
            Debug.Log("[GameLogic] 게임 시작");
            isGameStarted = true;
            currentPlayerTurn = 0;
            
            // 게임 상태 초기화
            for (int i = 0; i < playerScores.Length; i++)
            {
                playerScores[i] = 0;
                playerFinished[i] = false;
                hasPlacedFirstBlock[i] = false;
            }
            
            // 블록 상태 초기화
            placedBlocks.Clear();
            
            OnTurnChanged?.Invoke(currentPlayerTurn);
        }
        
        /// <summary>
        /// 게임 종료
        /// </summary>
        public void EndGame()
        {
            Debug.Log("[GameLogic] 게임 종료 (Stub)");
            isGameStarted = false;
            
            // 게임 결과 생성 (Stub)
            var result = new GameResult
            {
                playerScores = (int[])playerScores.Clone(),
                winner = GetWinner()
            };
            
            OnGameEnded?.Invoke(result);
        }
        
        /// <summary>
        /// 다음 턴으로 진행
        /// </summary>
        public void NextTurn()
        {
            if (!isGameStarted) return;
            
            currentPlayerTurn = (currentPlayerTurn + 1) % maxPlayers;
            OnTurnChanged?.Invoke(currentPlayerTurn);
            
            Debug.Log($"[GameLogic] 턴 변경: Player {currentPlayerTurn}");
        }
        
        /// <summary>
        /// 플레이어 점수 설정
        /// </summary>
        public void SetPlayerScore(int playerId, int score)
        {
            if (playerId >= 0 && playerId < playerScores.Length)
            {
                playerScores[playerId] = score;
                OnPlayerScoreChanged?.Invoke(playerId);
            }
        }
        
        /// <summary>
        /// 플레이어 점수 가져오기
        /// </summary>
        public int GetPlayerScore(int playerId)
        {
            if (playerId >= 0 && playerId < playerScores.Length)
            {
                return playerScores[playerId];
            }
            return 0;
        }
        
        /// <summary>
        /// 현재 턴 플레이어 가져오기
        /// </summary>
        public int GetCurrentPlayer()
        {
            return currentPlayerTurn;
        }
        
        /// <summary>
        /// 게임 시작 여부 확인
        /// </summary>
        public bool IsGameStarted()
        {
            return isGameStarted;
        }
        
        /// <summary>
        /// 보드 상태 클리어
        /// </summary>
        public void ClearBoard()
        {
            Debug.Log("[GameLogic] 보드 상태 클리어");
            placedBlocks.Clear();
            for (int i = 0; i < hasPlacedFirstBlock.Length; i++)
            {
                hasPlacedFirstBlock[i] = false;
            }
        }
        
        /// <summary>
        /// 플레이어가 첫 블록을 배치했는지 확인
        /// </summary>
        public bool HasPlayerPlacedFirstBlock(PlayerColor playerColor)
        {
            int playerId = (int)playerColor;
            if (playerId >= 0 && playerId < hasPlacedFirstBlock.Length)
            {
                return hasPlacedFirstBlock[playerId];
            }
            return false;
        }
        
        /// <summary>
        /// 블록 배치 가능 여부 확인
        /// </summary>
        public bool CanPlaceBlock(BlockPlacement placement)
        {
            Debug.Log($"[GameLogic] 블록 배치 가능성 검증: Player={placement.playerId}, Block={placement.blockType}, Position=({placement.position.x},{placement.position.y})");
            
            try
            {
                // 1. 보드 범위 확인
                if (!placement.IsValidPlacement(BOARD_SIZE))
                {
                    Debug.LogWarning("[GameLogic] 블록이 보드 범위를 벗어남");
                    return false;
                }
                
                // 2. 다른 블록과의 겹침 확인
                foreach (var existingBlock in placedBlocks)
                {
                    foreach (var cell in placement.occupiedCells)
                    {
                        if (existingBlock.ContainsCell(cell))
                        {
                            Debug.LogWarning($"[GameLogic] 다른 블록과 겹침: 위치 ({cell.x},{cell.y})");
                            return false;
                        }
                    }
                }
                
                // 3. 첫 블록인지 확인
                bool isFirstBlock = !HasPlayerPlacedFirstBlock(placement.playerColor);
                
                if (isFirstBlock)
                {
                    // 첫 블록은 보드 모서리에 인접해야 함
                    bool touchesCorner = false;
                    foreach (var cell in placement.occupiedCells)
                    {
                        // 모서리 체크: (0,0), (0,19), (19,0), (19,19) 중 하나와 인접
                        if ((cell.x == 0 && cell.y == 0) || (cell.x == 0 && cell.y == BOARD_SIZE - 1) ||
                            (cell.x == BOARD_SIZE - 1 && cell.y == 0) || (cell.x == BOARD_SIZE - 1 && cell.y == BOARD_SIZE - 1))
                        {
                            touchesCorner = true;
                            break;
                        }
                    }
                    
                    if (!touchesCorner)
                    {
                        Debug.LogWarning("[GameLogic] 첫 블록은 보드 모서리에 인접해야 함");
                        return false;
                    }
                }
                else
                {
                    // 후속 블록은 같은 색 블록과 대각선 연결, 변 연결 금지
                    bool hasCornerConnection = false;
                    bool hasSideConnection = false;
                    
                    foreach (var cell in placement.occupiedCells)
                    {
                        foreach (var existingBlock in placedBlocks)
                        {
                            if (existingBlock.playerId == placement.playerId)
                            {
                                foreach (var existingCell in existingBlock.occupiedCells)
                                {
                                    int dx = Mathf.Abs(cell.x - existingCell.x);
                                    int dy = Mathf.Abs(cell.y - existingCell.y);
                                    
                                    // 대각선 연결 (코너 연결)
                                    if (dx == 1 && dy == 1)
                                    {
                                        hasCornerConnection = true;
                                    }
                                    // 변 연결 (금지)
                                    else if ((dx == 1 && dy == 0) || (dx == 0 && dy == 1))
                                    {
                                        hasSideConnection = true;
                                    }
                                }
                            }
                        }
                    }
                    
                    if (hasSideConnection)
                    {
                        Debug.LogWarning("[GameLogic] 같은 색 블록끼리 변으로 연결될 수 없음");
                        return false;
                    }
                    
                    if (!hasCornerConnection)
                    {
                        Debug.LogWarning("[GameLogic] 같은 색 블록과 코너로 연결되어야 함");
                        return false;
                    }
                }
                
                Debug.Log("[GameLogic] 블록 배치 가능");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameLogic] 블록 배치 검증 중 오류: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 블록 배치 실행
        /// </summary>
        public bool PlaceBlock(BlockPlacement placement)
        {
            Debug.Log($"[GameLogic] 블록 배치 실행: Player={placement.playerId}, Block={placement.blockType}");
            
            if (CanPlaceBlock(placement))
            {
                placedBlocks.Add(placement);
                hasPlacedFirstBlock[placement.playerId] = true;
                
                Debug.Log($"[GameLogic] 블록 배치 성공: 총 {placedBlocks.Count}개 블록 배치됨");
                return true;
            }
            
            Debug.LogWarning("[GameLogic] 블록 배치 실패");
            return false;
        }
        
        /// <summary>
        /// 승자 결정 (점수 기준)
        /// </summary>
        private int GetWinner()
        {
            int winner = 0;
            int maxScore = playerScores[0];
            
            for (int i = 1; i < playerScores.Length; i++)
            {
                if (playerScores[i] > maxScore)
                {
                    maxScore = playerScores[i];
                    winner = i;
                }
            }
            
            return winner;
        }
    }
    
    /// <summary>
    /// 게임 결과 구조체
    /// </summary>
    [System.Serializable]
    public struct GameResult
    {
        public int[] playerScores;
        public int winner;
        public string winnerName;  // 멀티플레이어에서 승자의 displayName
        
        public GameResult(int[] scores, int winnerIndex, string winnerDisplayName = null)
        {
            playerScores = scores;
            winner = winnerIndex;
            winnerName = winnerDisplayName ?? $"Player {winnerIndex + 1}";
        }
    }
}