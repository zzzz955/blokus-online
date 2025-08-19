// Assets/_Project/Scripts/Features/Single/UI/InGame/GameResultModal.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Features.Single.Gameplay;

namespace Features.Single.UI.InGame
{
    /// <summary>
    /// 게임 종료 결과 모달.
    /// - 형제 패널 직접 제어 금지
    /// - 화면 전환은 SingleGameplayUIScreenController에 위임(의존 역전)
    /// </summary>
    public class GameResultModal : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject modalPanel;          // 모달 루트
        [SerializeField] private Button backgroundButton;        // 배경 (클릭 시 닫기)
        [SerializeField] private Button confirmButton;           // 확인 (닫기)
        [Space(4)]
        [SerializeField] private TMP_Text resultTitleText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text optimalScoreText;

        [Header("Stars")]
        [SerializeField] private Image[] starImages = new Image[3];
        [SerializeField] private Sprite activeStarSprite;
        [SerializeField] private Sprite inactiveStarSprite;

        [Header("Labels")]
        [SerializeField] private string successTitle = "클리어 성공!";
        [SerializeField] private string failureTitle = "클리어 실패";
        [SerializeField] private Color successColor = Color.green;
        [SerializeField] private Color failureColor = Color.red;

        [Header("Star thresholds (ratio)")]
        [SerializeField] private float threeStarThreshold = 0.90f;
        [SerializeField] private float twoStarThreshold   = 0.70f;
        [SerializeField] private float oneStarThreshold   = 0.50f;

        [Header("Router (선택: 미지정 시 자동 탐색)")]
        [SerializeField] private Features.Single.UI.Scene.SingleGameplayUIScreenController uiController;

        private Action _onClosed;

        private void Awake()
        {
            if (modalPanel) modalPanel.SetActive(false);

            if (backgroundButton)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(CloseToSelection);
            }
            if (confirmButton)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(CloseToSelection);
            }
        }

        private void OnDestroy()
        {
            if (backgroundButton) backgroundButton.onClick.RemoveListener(CloseToSelection);
            if (confirmButton)    confirmButton.onClick.RemoveListener(CloseToSelection);
        }

        // ---------------- Public API ----------------

        public void ShowResult(int score, int optimalScore, float elapsedTime, bool isSuccess)
        {
            ShowResult(score, optimalScore, elapsedTime, isSuccess, onClosed: null);
        }

        public void ShowResult(int score, int optimalScore, float elapsedTime, bool isSuccess, Action onClosed)
        {
            _onClosed = onClosed;

            // 🔥 디버깅 코드 추가
            Debug.Log($"[GameResultModal] modalPanel null check: {modalPanel == null}");
            if (modalPanel != null)
            {
                Debug.Log($"[GameResultModal] modalPanel.activeSelf: {modalPanel.activeSelf}");
                Debug.Log($"[GameResultModal] modalPanel.activeInHierarchy: {modalPanel.activeInHierarchy}");
                Debug.Log($"[GameResultModal] modalPanel.transform.parent: {modalPanel.transform.parent?.name}");
                
                var rect = modalPanel.GetComponent<RectTransform>();
                if (rect != null)
                    Debug.Log($"[GameResultModal] modalPanel size: {rect.sizeDelta}, position: {rect.position}");
                    
                var canvas = modalPanel.GetComponent<Canvas>();
                if (canvas != null)
                    Debug.Log($"[GameResultModal] Canvas enabled: {canvas.enabled}, sortingOrder: {canvas.sortingOrder}");
                    
                var cg = modalPanel.GetComponent<CanvasGroup>();
                if (cg != null)
                    Debug.Log($"[GameResultModal] CanvasGroup alpha: {cg.alpha}, interactable: {cg.interactable}");
            }

            // 🔥 부모 GameObject 먼저 활성화 (핵심 수정!)
            if (!this.gameObject.activeSelf)
            {
                this.gameObject.SetActive(true);
                Debug.Log("[GameResultModal] GameResultModal GameObject 활성화됨");
            }

            EnsureModalOnTopAndBlockRaycasts();

            if (modalPanel && !modalPanel.activeSelf)
                modalPanel.SetActive(true);
                
            // 🔥 활성화 후 재확인
            if (modalPanel != null)
                Debug.Log($"[GameResultModal] After SetActive - activeSelf: {modalPanel.activeSelf}, activeInHierarchy: {modalPanel.activeInHierarchy}");

            if (resultTitleText)
            {
                resultTitleText.text  = isSuccess ? successTitle : failureTitle;
                resultTitleText.color = isSuccess ? successColor : failureColor;
            }
            if (scoreText)        scoreText.text        = $"획득 점수: {score:N0}";
            if (optimalScoreText) optimalScoreText.text = $"최대 점수: {optimalScore:N0}";

            if (timeText)
            {
                int m = Mathf.FloorToInt(elapsedTime / 60f);
                int s = Mathf.FloorToInt(elapsedTime % 60f);
                timeText.text = $"소요 시간: {m:00}:{s:00}";
            }

            int stars = CalculateStars(score, optimalScore, isSuccess);
            DisplayStars(stars);
            UpdateStageProgress(score, stars, elapsedTime, isSuccess);

            Debug.Log($"[GameResultModal] 표시: score={score}/{optimalScore}, time={elapsedTime:F1}s, success={isSuccess}, stars={stars}");
        }

        // ---------------- Internals ----------------

        private int CalculateStars(int score, int optimalScore, bool isSuccess)
        {
            if (!isSuccess) return 0;
            if (optimalScore <= 0) return 1;

            float r = (float)score / optimalScore;
            if (r >= threeStarThreshold) return 3;
            if (r >= twoStarThreshold)   return 2;
            if (r >= oneStarThreshold)   return 1;
            return 0;
        }

        private void DisplayStars(int starCount)
        {
            if (starImages == null) return;

            for (int i = 0; i < starImages.Length; i++)
            {
                var img = starImages[i];
                if (!img) continue;

                bool on = (i < starCount);
                img.sprite = on ? activeStarSprite : inactiveStarSprite;
                img.color  = on ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            }

            Debug.Log($"[GameResultModal] 별점 표시 완료 - {starCount}개");
        }

        private void UpdateStageProgress(int score, int starCount, float elapsedTime, bool isSuccess)
        {
            var gm = SingleGameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[GameResultModal] SingleGameManager 없음 → 진행도 전송 생략");
                return;
            }

            int stageNumber = SingleGameManager.CurrentStage;
            bool completed  = isSuccess && starCount > 0;

            Debug.Log($"[GameResultModal] 서버 진행도 업데이트 요청: stage={stageNumber}, done={completed}, stars={starCount}, score={score}, t={elapsedTime:F1}s");
            gm.UpdateStageProgress(stageNumber, completed, starCount, score, elapsedTime);
        }

        private void CloseToSelection()
        {
            if (modalPanel && modalPanel.activeSelf)
                modalPanel.SetActive(false);

            // 컨트롤러에만 위임 (형제 패널 직접 제어 금지)
            if (!uiController)
                uiController = FindObjectOfType<Features.Single.UI.Scene.SingleGameplayUIScreenController>(true);

            if (uiController)
            {
                uiController.ShowSelection(); // GamePanel OFF, StageSelect ON(또는 활성 유지 전략)
            }
            else
            {
                Debug.LogWarning("[GameResultModal] UIController가 없어 화면 복귀를 수행하지 못했습니다.");
            }

            var cb = _onClosed; _onClosed = null;
            cb?.Invoke();

            Debug.Log("[GameResultModal] 닫기 → 스테이지 선택으로 복귀");
        }

        private void EnsureModalOnTopAndBlockRaycasts()
        {
            if (!modalPanel) return;

            // 같은 Canvas 내에서 최상단 배치
            modalPanel.transform.SetAsLastSibling();

            // 입력 차단 보장
            var cg = modalPanel.GetComponent<CanvasGroup>();
            if (!cg) cg = modalPanel.AddComponent<CanvasGroup>();
            cg.interactable = true;
            cg.blocksRaycasts = true;

            if (!FindObjectOfType<EventSystem>())
                Debug.LogWarning("[GameResultModal] EventSystem이 없어 버튼 입력이 안 될 수 있습니다.");
        }
    }
}
