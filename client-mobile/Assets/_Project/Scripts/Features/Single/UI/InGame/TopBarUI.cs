using UnityEngine;
using UnityEngine.UI;
using TMPro; // Text를 쓰면 UnityEngine.UI.Text로 바꿔도 됩니다.
using UnityEngine.SceneManagement;
using BlokusUnity.Game;

namespace BlokusUnity.UI
{
    /// <summary>
    /// 상단 바: Timer + Undo(버튼/카운트) + Exit
    /// </summary>
    public sealed class TopBarUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TMP_Text timerText;       // 또는 Text
        [SerializeField] private TMP_Text scoreText;       // 현재 점수 표시
        [SerializeField] private TMP_Text stageNameText;   // 스테이지 이름 표시
        [SerializeField] private Button undoButton;
        [SerializeField] private TMP_Text undoCountText;   // 또는 Text
        [SerializeField] private Button exitButton;
        [SerializeField] private ConfirmationModal confirmationModal;

        [Header("Config")]
        [SerializeField] private string mainSceneName = "MainScene";
        [SerializeField] private string scoreFormat = "점수: {0}";
        [SerializeField] private bool showExitConfirm = true; // Exit 확인 모달 표시 여부
        
        // 점수 관련 상태
        private int currentScore = 0;

        private void Awake()
        {
            if (undoButton != null)
            {
                // 🔒 중복 방지: Inspector 이벤트도 비워두고, 여기서만 연결
                undoButton.onClick.RemoveAllListeners();
                undoButton.onClick.AddListener(OnClickUndo);
            }
            
            if (exitButton != null)
            {
                // Exit 버튼 이벤트 연결
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(OnClickExit);
            }

            // 모달 자동 찾기(활성 오브젝트 우선)
            if (confirmationModal == null)
            {
                var active = FindObjectOfType<ConfirmationModal>();
                if (active != null) confirmationModal = active;
                else
                {
                    // 비활성까지 검색(비활성은 Show가 안 먹으니, 찾으면 루트 활성화 필요)
                    var all = Resources.FindObjectsOfTypeAll<ConfirmationModal>();
                    if (all != null && all.Length > 0)
                    {
                        confirmationModal = all[0];
                        // 모달이 비활성 GameObject라면 활성화
                        if (!confirmationModal.gameObject.activeInHierarchy)
                            confirmationModal.gameObject.SetActive(true);
                    }
                }
            }
        }

        private void OnEnable()
        {
            var gm = BlokusUnity.Game.SingleGameManager.Instance;
            if (gm != null)
            {
                // Undo 이벤트 구독
                gm.OnUndoCountChanged -= RefreshUndo;
                gm.OnUndoCountChanged += RefreshUndo;
                RefreshUndo(gm.RemainingUndo);
                
                // 점수 이벤트 구독
                gm.OnScoreChanged -= OnScoreChanged;
                gm.OnScoreChanged += OnScoreChanged;
                
                // 초기 UI 설정
                UpdateScoreDisplay(0);
                UpdateStageDisplay();
            }
        }

        private void OnDisable()
        {
            var gm = BlokusUnity.Game.SingleGameManager.Instance;
            if (gm != null)
            {
                gm.OnUndoCountChanged -= RefreshUndo;
                gm.OnScoreChanged -= OnScoreChanged;
            }
        }

        private void Update()
        {
            // 타이머 갱신(매 프레임, 아주 가벼운 연산)
            var gm = SingleGameManager.Instance;
            if (gm == null || timerText == null) return;

            int sec = gm.ElapsedSeconds;
            int h = sec / 3600;
            int m = (sec % 3600) / 60;
            int s = sec % 60;
            timerText.text = (h > 0) ? $"{h:00}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
        }

        private void RefreshUndo(int remain)
        {
            if (undoCountText != null) undoCountText.text = $"UNDO {remain}";
            if (undoButton != null) undoButton.interactable = remain > 0;
        }
private void OnClickUndo()
{
    var gm = BlokusUnity.Game.SingleGameManager.Instance;
    if (gm == null) return;

    if (gm.RemainingUndo <= 0) { Debug.Log("[TopBarUI] Undo 불가 - 남은 횟수 없음"); return; }
    if (!gm.CanUndo())         { Debug.Log("[TopBarUI] Undo 불가 - 되돌릴 배치 없음"); return; }

    if (undoButton != null) undoButton.interactable = false;

    if (confirmationModal != null)
    {
        confirmationModal.ShowUndoConfirmation(
            onConfirm: () =>
            {
                gm.OnUndoMove();
                if (undoButton != null) undoButton.interactable = true;
            },
            onCancel: () =>
            {
                if (undoButton != null) undoButton.interactable = true;
            }
        );
    }
    else
    {
        Debug.LogWarning("[TopBarUI] ConfirmationModal이 없습니다. 바로 Undo 실행");
        gm.OnUndoMove();
        if (undoButton != null) undoButton.interactable = true;
    }
}


        /// <summary>
        /// 점수 변경 이벤트 핸들러
        /// </summary>
        private void OnScoreChanged(int scoreChange, string reason)
        {
            currentScore += scoreChange;
            UpdateScoreDisplay(currentScore);
            
            Debug.Log($"[TopBarUI] 점수 변경: {scoreChange} ({reason}) - 총 점수: {currentScore}");
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
        /// 스테이지 정보 표시 업데이트
        /// </summary>
        private void UpdateStageDisplay()
        {
            if (stageNameText != null)
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

        private void OnClickExit()
        {
            if (showExitConfirm && confirmationModal != null)
            {
                // 확인 모달 표시
                confirmationModal.ShowExitConfirmation(
                    onConfirm: () =>
                    {
                        ExitToMainScene();
                    },
                    onCancel: () =>
                    {
                        Debug.Log("[TopBarUI] 게임 종료 취소");
                    }
                );
            }
            else
            {
                // 바로 종료
                ExitToMainScene();
            }
        }
        
        /// <summary>
        /// MainScene으로 이동
        /// </summary>
        private void ExitToMainScene()
        {
            var gm = SingleGameManager.Instance;
            if (gm != null) gm.OnExitRequested();

            // Exit으로 돌아온다는 플래그 설정
            PlayerPrefs.SetInt("ReturnedFromGame", 1);
            PlayerPrefs.Save();

            SceneManager.LoadScene(mainSceneName, LoadSceneMode.Single);
        }
    }
}
