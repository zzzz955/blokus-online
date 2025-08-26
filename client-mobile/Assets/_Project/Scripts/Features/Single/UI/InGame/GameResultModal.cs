// Assets/_Project/Scripts/Features/Single/UI/InGame/GameResultModal.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Features.Single.Gameplay;
using Shared.Models;

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

        /// <summary>
        /// 🔥 GameEndResult 기반 결과 표시 (단일 진실원천)
        /// </summary>
        public void ShowResult(GameEndResult gameResult, Action onClosed = null)
        {
            if (gameResult == null)
            {
                Debug.LogError("[GameResultModal] GameEndResult가 null입니다!");
                onClosed?.Invoke();
                return;
            }

            _onClosed = onClosed;

            // 🚨 규칙 위반 검사
            if (gameResult.stars == 0 && gameResult.isCleared)
            {
                Debug.LogError($"[GameResultModal] 🚨 규칙 위반: GameEndResult가 0별인데 isCleared=true - Stage {gameResult.stageNumber}");
            }

            Debug.Log($"[GameResultModal] 결과 표시 요청: {gameResult}");

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

            // 🔥 GameEndResult 기반 UI 업데이트 (하드코딩 제거)
            UpdateUI(gameResult);
            
            Debug.Log($"[GameResultModal] 표시 완료: {gameResult}");
        }

        /// <summary>
        /// 레거시 호환성 (deprecated - GameEndResult 사용 권장)
        /// </summary>
        [System.Obsolete("Use ShowResult(GameEndResult) instead")]
        public void ShowResult(int score, int optimalScore, float elapsedTime, bool isSuccess)
        {
            ShowResult(score, optimalScore, elapsedTime, isSuccess, onClosed: null);
        }

        /// <summary>
        /// 레거시 호환성 (deprecated - GameEndResult 사용 권장)
        /// </summary>
        [System.Obsolete("Use ShowResult(GameEndResult) instead")]
        public void ShowResult(int score, int optimalScore, float elapsedTime, bool isSuccess, Action onClosed)
        {
            Debug.LogWarning("[GameResultModal] 레거시 ShowResult 호출 - GameEndResult 사용을 권장합니다.");
            
            // 임시로 GameEndResult 생성하여 새로운 메서드 호출
            int stars = CalculateStars(score, optimalScore, isSuccess);
            var tempResult = new GameEndResult(
                stageNumber: SingleGameManager.CurrentStage,
                stageName: $"Stage {SingleGameManager.CurrentStage}",
                finalScore: score,
                optimalScore: optimalScore,
                elapsedTime: elapsedTime,
                stars: stars,
                isNewBest: false,
                endReason: "Legacy call"
            );
            
            ShowResult(tempResult, onClosed);
        }

        // ---------------- Internals ----------------

        /// <summary>
        /// 🔥 GameEndResult 기반 UI 업데이트 (단일 진실원천)
        /// </summary>
        private void UpdateUI(GameEndResult gameResult)
        {
            // 타이틀 및 색상 (GameEndResult 기반)
            if (resultTitleText)
            {
                resultTitleText.text = gameResult.GetResultTitle();
                resultTitleText.color = gameResult.GetResultColor();
            }

            // 점수 정보
            if (scoreText) scoreText.text = $"획득 점수: {gameResult.finalScore:N0}";
            if (optimalScoreText) optimalScoreText.text = $"최대 점수: {gameResult.optimalScore:N0}";

            // 시간 정보
            if (timeText)
            {
                int m = Mathf.FloorToInt(gameResult.elapsedTime / 60f);
                int s = Mathf.FloorToInt(gameResult.elapsedTime % 60f);
                timeText.text = $"소요 시간: {m:00}:{s:00}";
            }

            // 🔥 별점 표시: GameEndResult의 정확한 stars 값 사용 (하드코딩 제거)
            DisplayStars(gameResult.stars);

            // 🔥 서버 진행도 업데이트: GameEndResult 기반
            UpdateStageProgress(gameResult);

            Debug.Log($"[GameResultModal] UI 업데이트 완료: {gameResult}");
        }

        /// <summary>
        /// 레거시 별점 계산 (GameEndResult에서는 사용 안 함)
        /// </summary>
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
            if (starImages == null) 
            {
                Debug.LogWarning("[GameResultModal] starImages 배열이 null입니다!");
                return;
            }

            Debug.Log($"[GameResultModal] 별점 표시 시작 - 요청: {starCount}개, 배열 크기: {starImages.Length}");

            for (int i = 0; i < starImages.Length; i++)
            {
                var img = starImages[i];
                if (!img) 
                {
                    Debug.LogWarning($"[GameResultModal] starImages[{i}]가 null입니다!");
                    continue;
                }

                bool on = (i < starCount);
                Sprite targetSprite = on ? activeStarSprite : inactiveStarSprite;
                Color targetColor = on ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
                
                img.sprite = targetSprite;
                img.color = targetColor;

                Debug.Log($"[GameResultModal] 별 {i}: on={on}, sprite={targetSprite?.name}, color={targetColor}");
            }

            // 🔥 추가 검증: 스프라이트가 올바르게 설정되었는지 확인
            if (activeStarSprite == null)
                Debug.LogError("[GameResultModal] activeStarSprite가 Inspector에서 할당되지 않았습니다!");
            if (inactiveStarSprite == null)
                Debug.LogError("[GameResultModal] inactiveStarSprite가 Inspector에서 할당되지 않았습니다!");

            Debug.Log($"[GameResultModal] 별점 표시 완료 - {starCount}개");
        }

        /// <summary>
        /// 🔥 GameEndResult 기반 진행도 업데이트 (단일 진실원천)
        /// </summary>
        private void UpdateStageProgress(GameEndResult gameResult)
        {
            var gm = SingleGameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning("[GameResultModal] SingleGameManager 없음 → 진행도 전송 생략");
                return;
            }

            // 🔥 GameEndResult의 isCleared 값 사용 (stars >= 1 규칙 준수)
            bool completed = gameResult.isCleared;

            Debug.Log($"[GameResultModal] 서버 진행도 업데이트 요청: stage={gameResult.stageNumber}, " +
                     $"done={completed}, stars={gameResult.stars}, score={gameResult.finalScore}, " +
                     $"t={gameResult.elapsedTime:F1}s");

            // 🚨 규칙 위반 재검증
            if (gameResult.stars == 0 && completed)
            {
                Debug.LogError($"[GameResultModal] 🚨 규칙 위반: 0별인데 completed=true로 전송 시도 - Stage {gameResult.stageNumber}");
            }

            gm.UpdateStageProgress(gameResult.stageNumber, completed, gameResult.stars, 
                                 gameResult.finalScore, gameResult.elapsedTime);
        }

        /// <summary>
        /// 레거시 진행도 업데이트 (deprecated)
        /// </summary>
        [System.Obsolete("Use UpdateStageProgress(GameEndResult) instead")]
        private void UpdateStageProgress(int score, int starCount, float elapsedTime, bool isSuccess)
        {
            // 임시로 GameEndResult 생성하여 새로운 메서드 호출
            var tempResult = new GameEndResult(
                stageNumber: SingleGameManager.CurrentStage,
                stageName: $"Stage {SingleGameManager.CurrentStage}",
                finalScore: score,
                optimalScore: 0, // 불명
                elapsedTime: elapsedTime,
                stars: starCount,
                isNewBest: false,
                endReason: "Legacy progress update"
            );
            
            UpdateStageProgress(tempResult);
        }

        private void CloseToSelection()
        {
            if (modalPanel && modalPanel.activeSelf)
                modalPanel.SetActive(false);

            // 🔥 수정: UI 안정화를 위한 지연된 처리
            StartCoroutine(DelayedCloseToSelection());
        }

        /// <summary>
        /// 🔥 수정: UI 안정화를 위한 지연된 화면 전환 (StageSelectPanel 강제 활성화 보장)
        /// </summary>
        private System.Collections.IEnumerator DelayedCloseToSelection()
        {
            // 🔥 수정: 2프레임 대기로 UI 상태 완전 안정화
            yield return null;
            yield return null;

            // 🔥 수정: StageSelectPanel 먼저 강제 활성화 (우선순위 최고)
            var stageSelectPanel = GameObject.Find("StageSelectPanel");
            if (stageSelectPanel != null)
            {
                if (!stageSelectPanel.activeSelf)
                {
                    Debug.Log("[GameResultModal] StageSelectPanel 강제 활성화 - 최우선");
                    stageSelectPanel.SetActive(true);
                }
                
                // 🔥 추가: StageSelectPanel의 CandyCrushStageMapView 강제 리프레시 (throttling 무시)
                var stageMapView = stageSelectPanel.GetComponent<Features.Single.UI.StageSelect.CandyCrushStageMapView>();
                if (stageMapView != null)
                {
                    Debug.Log("[GameResultModal] CandyCrushStageMapView 강제 리프레시 요청");
                    // ForceRefreshStageButtons 메서드 호출 (throttling 무시)
                    stageMapView.SendMessage("ForceRefreshStageButtons", SendMessageOptions.DontRequireReceiver);
                }
            }
            else
            {
                Debug.LogError("[GameResultModal] StageSelectPanel GameObject를 찾을 수 없음!");
            }

            // 컨트롤러에만 위임 (형제 패널 직접 제어 금지)
            if (!uiController)
                uiController = FindObjectOfType<Features.Single.UI.Scene.SingleGameplayUIScreenController>(true);

            if (uiController)
            {
                Debug.Log("[GameResultModal] UIController 발견 - ShowSelection 호출");
                uiController.ShowSelection(); // GamePanel OFF, StageSelect ON
                
                // 🔥 수정: ShowSelection 후 더 긴 대기 시간으로 UI 업데이트 완료 보장
                yield return new WaitForSeconds(0.2f);
                
                // 🔥 추가: 최종 검증 - StageSelectPanel 활성화 재확인
                if (stageSelectPanel != null && !stageSelectPanel.activeSelf)
                {
                    Debug.LogWarning("[GameResultModal] 최종 검증 실패 - StageSelectPanel 재활성화");
                    stageSelectPanel.SetActive(true);
                }
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
