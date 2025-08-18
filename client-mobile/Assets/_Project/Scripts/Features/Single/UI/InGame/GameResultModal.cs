using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using BlokusUnity.Game;
using BlokusUnity.Data;

namespace BlokusUnity.UI.Game
{
    /// <summary>
    /// 게임 종료 시 결과를 표시하는 모달
    /// 점수, 별점, 시간, 클리어 여부 등을 표시
    /// </summary>
    public class GameResultModal : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private TMP_Text resultTitleText;        // 클리어 성공/실패
        [SerializeField] private TMP_Text scoreText;             // 획득 점수
        [SerializeField] private TMP_Text timeText;              // 소요 시간
        [SerializeField] private TMP_Text optimalScoreText;      // 최대 점수 (별점 계산용)
        [SerializeField] private Button confirmButton;          // MainScene 이동
        
        [Header("별점 시스템")]
        [SerializeField] private Image[] starImages = new Image[3];  // Star[3]
        [SerializeField] private Sprite activeStarSprite;            // 활성화된 별
        [SerializeField] private Sprite inactiveStarSprite;          // 비활성화된 별
        
        [Header("설정")]
        [SerializeField] private string mainSceneName = "MainScene";
        [SerializeField] private string successTitle = "클리어 성공!";
        [SerializeField] private string failureTitle = "클리어 실패";
        [SerializeField] private Color successColor = Color.green;
        [SerializeField] private Color failureColor = Color.red;
        
        // 별점 계산 기준 (점수 비율)
        [SerializeField] private float threeStarThreshold = 0.9f;    // 90% 이상: 3개
        [SerializeField] private float twoStarThreshold = 0.7f;      // 70% 이상: 2개
        [SerializeField] private float oneStarThreshold = 0.5f;      // 50% 이상: 1개
        
        private void Awake()
        {
            // 초기에는 모달 숨김
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }
            
            // 확인 버튼 이벤트 연결
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }
            
            // 별 스프라이트 기본값 설정
            if (activeStarSprite == null || inactiveStarSprite == null)
            {
                Debug.LogWarning("[GameResultModal] 별 스프라이트가 설정되지 않았습니다. Inspector에서 설정해주세요.");
            }
        }
        
        private void OnDestroy()
        {
            // 버튼 이벤트 해제
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
            }
        }
        
        /// <summary>
        /// 게임 결과 모달을 표시합니다
        /// </summary>
        /// <param name="score">획득 점수</param>
        /// <param name="optimalScore">최대 가능 점수</param>
        /// <param name="elapsedTime">소요 시간 (초)</param>
        /// <param name="isSuccess">클리어 성공 여부</param>
        public void ShowResult(int score, int optimalScore, float elapsedTime, bool isSuccess)
        {
            if (modalPanel != null)
            {
                modalPanel.SetActive(true);
            }
            
            // 결과 제목 설정
            if (resultTitleText != null)
            {
                resultTitleText.text = isSuccess ? successTitle : failureTitle;
                resultTitleText.color = isSuccess ? successColor : failureColor;
            }
            
            // 점수 표시
            if (scoreText != null)
            {
                scoreText.text = $"획득 점수: {score:N0}";
            }
            
            // 최대 점수 표시
            if (optimalScoreText != null)
            {
                optimalScoreText.text = $"최대 점수: {optimalScore:N0}";
            }
            
            // 시간 표시
            if (timeText != null)
            {
                int minutes = Mathf.FloorToInt(elapsedTime / 60f);
                int seconds = Mathf.FloorToInt(elapsedTime % 60f);
                timeText.text = $"소요 시간: {minutes:00}:{seconds:00}";
            }
            
            // 별점 계산 및 표시
            int starCount = CalculateStars(score, optimalScore, isSuccess);
            DisplayStars(starCount);
            
            // 진행도 업데이트 (서버 전송)
            UpdateStageProgress(score, starCount, elapsedTime, isSuccess);
            
            Debug.Log($"[GameResultModal] 게임 결과 표시 - 점수: {score}/{optimalScore}, 시간: {elapsedTime:F1}초, 성공: {isSuccess}, 별: {starCount}개");
        }
        
        /// <summary>
        /// 점수 비율에 따른 별점 계산
        /// </summary>
        private int CalculateStars(int score, int optimalScore, bool isSuccess)
        {
            // 클리어 실패 시 0개
            if (!isSuccess)
            {
                return 0;
            }
            
            // 최대 점수가 0이면 1개 (안전장치)
            if (optimalScore <= 0)
            {
                return 1;
            }
            
            // 점수 비율 계산
            float ratio = (float)score / optimalScore;
            
            if (ratio >= threeStarThreshold)
            {
                return 3;  // 90% 이상: 3개
            }
            else if (ratio >= twoStarThreshold)
            {
                return 2;  // 70% 이상: 2개
            }
            else if (ratio >= oneStarThreshold)
            {
                return 1;  // 50% 이상: 1개
            }
            else
            {
                return 0;  // 50% 미만: 0개 (클리어 실패 처리)
            }
        }
        
        /// <summary>
        /// 별점 UI 표시
        /// </summary>
        private void DisplayStars(int starCount)
        {
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] != null)
                {
                    bool isActive = i < starCount;
                    starImages[i].sprite = isActive ? activeStarSprite : inactiveStarSprite;
                    
                    // 별 색상도 조정 (선택사항)
                    starImages[i].color = isActive ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
                }
            }
            
            Debug.Log($"[GameResultModal] 별점 표시 완료 - {starCount}개");
        }
        
        /// <summary>
        /// 스테이지 진행도 업데이트 (서버 전송)
        /// </summary>
        private void UpdateStageProgress(int score, int starCount, float elapsedTime, bool isSuccess)
        {
            var gameManager = SingleGameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogWarning("[GameResultModal] SingleGameManager를 찾을 수 없어 진행도 업데이트를 건너뜁니다.");
                return;
            }
            
            int currentStage = SingleGameManager.CurrentStage;
            bool isCompleted = isSuccess && starCount > 0;  // 별 1개 이상이어야 클리어
            
            Debug.Log($"[GameResultModal] 서버에 진행도 업데이트 요청 - 스테이지: {currentStage}, 완료: {isCompleted}, 별: {starCount}, 점수: {score}, 시간: {elapsedTime:F1}초");
            
            // 게임 매니저를 통해 진행도 업데이트
            gameManager.UpdateStageProgress(currentStage, isCompleted, starCount, score, elapsedTime);
        }
        
        /// <summary>
        /// 확인 버튼 클릭 - MainScene으로 이동
        /// </summary>
        private void OnConfirmClicked()
        {
            Debug.Log("[GameResultModal] 확인 버튼 클릭 - MainScene으로 이동");
            
            // 게임 정리
            var gameManager = SingleGameManager.Instance;
            if (gameManager != null)
            {
                gameManager.OnGameCompleted();
            }
            
            // MainScene으로 복귀했다는 플래그 설정
            PlayerPrefs.SetInt("ReturnedFromGame", 1);
            PlayerPrefs.Save();
            
            // MainScene으로 이동
            SceneManager.LoadScene(mainSceneName, LoadSceneMode.Single);
        }
        
        /// <summary>
        /// 모달 숨김 (필요시 사용)
        /// </summary>
        public void HideModal()
        {
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }
        }
        
        /// <summary>
        /// 별점 기준 설정 (Inspector에서 조정 가능)
        /// </summary>
        public void SetStarThresholds(float three, float two, float one)
        {
            threeStarThreshold = three;
            twoStarThreshold = two;
            oneStarThreshold = one;
        }
    }
}