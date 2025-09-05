using UnityEngine;
using System.Collections.Generic;

namespace Features.Multi.Core
{
    /// <summary>
    /// 멀티플레이어 게임 로직 (Stub 구현)
    /// 실제 게임 로직은 추후 구현
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
        
        /// <summary>
        /// 게임 시작
        /// </summary>
        public void StartGame()
        {
            Debug.Log("[GameLogic] 게임 시작 (Stub)");
            isGameStarted = true;
            currentPlayerTurn = 0;
            
            // 점수 초기화
            for (int i = 0; i < playerScores.Length; i++)
            {
                playerScores[i] = 0;
                playerFinished[i] = false;
            }
            
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