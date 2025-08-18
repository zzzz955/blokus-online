using UnityEngine;
using TMPro;
using BlokusUnity.Game;

namespace BlokusUnity.UI.Game
{
    /// <summary>
    /// 게임 상단 바 UI 관리
    /// 점수, 시간, 스테이지 정보 등을 표시
    /// </summary>
    public class GameTopBar : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI stageNameText;
        [SerializeField] private TextMeshProUGUI timeText;
        
        [Header("설정")]
        [SerializeField] private string scoreFormat = "점수: {0}";
        [SerializeField] private string timeFormat = "{0:F1}초";
        
        private SingleGameManager gameManager;
        private int currentScore = 0;
        private float gameTime = 0f;
        
        void Start()
        {
            // SingleGameManager 찾기
            gameManager = SingleGameManager.Instance;
            if (gameManager != null)
            {
                // 점수 변경 이벤트 구독
                gameManager.OnScoreChanged += OnScoreChanged;
                
                // 초기 점수 설정
                UpdateScoreDisplay(0);
                
                // 스테이지 정보 설정
                UpdateStageInfo();
            }
            else
            {
                Debug.LogWarning("[GameTopBar] SingleGameManager.Instance를 찾을 수 없습니다.");
            }
        }
        
        void Update()
        {
            // 게임 시간 업데이트
            if (gameManager != null && gameManager.IsInitialized)
            {
                gameTime = gameManager.ElapsedSeconds;
                UpdateTimeDisplay(gameTime);
            }
        }
        
        void OnDestroy()
        {
            // 이벤트 구독 해제
            if (gameManager != null)
            {
                gameManager.OnScoreChanged -= OnScoreChanged;
            }
        }
        
        /// <summary>
        /// 점수 변경 이벤트 핸들러
        /// </summary>
        private void OnScoreChanged(int scoreChange, string reason)
        {
            currentScore += scoreChange;
            UpdateScoreDisplay(currentScore);
            
            Debug.Log($"[GameTopBar] 점수 변경: {scoreChange} ({reason}) - 총 점수: {currentScore}");
        }
        
        /// <summary>
        /// 점수 표시 업데이트
        /// </summary>
        private void UpdateScoreDisplay(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = string.Format(scoreFormat, score);
            }
        }
        
        /// <summary>
        /// 시간 표시 업데이트
        /// </summary>
        private void UpdateTimeDisplay(float seconds)
        {
            if (timeText != null)
            {
                timeText.text = string.Format(timeFormat, seconds);
            }
        }
        
        /// <summary>
        /// 스테이지 정보 업데이트
        /// </summary>
        private void UpdateStageInfo()
        {
            if (stageNameText != null && gameManager != null)
            {
                var stageData = SingleGameManager.StageManager?.GetCurrentStageData();
                if (stageData != null)
                {
                    stageNameText.text = $"스테이지 {stageData.stage_number}";
                }
                else
                {
                    stageNameText.text = $"스테이지 {SingleGameManager.CurrentStage}";
                }
            }
        }
        
        /// <summary>
        /// 점수를 수동으로 설정 (게임 시작 시)
        /// </summary>
        public void SetScore(int score)
        {
            currentScore = score;
            UpdateScoreDisplay(currentScore);
        }
        
        /// <summary>
        /// 현재 점수 반환
        /// </summary>
        public int GetCurrentScore()
        {
            return currentScore;
        }
    }
}