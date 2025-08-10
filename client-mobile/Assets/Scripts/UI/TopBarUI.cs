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
        [SerializeField] private Button undoButton;
        [SerializeField] private TMP_Text undoCountText;   // 또는 Text
        [SerializeField] private Button exitButton;

        [Header("Config")]
        [SerializeField] private string mainSceneName = "MainScene";
        [SerializeField] private bool showExitConfirm = false; // 팝업 붙일 계획이면 true로

        private void Awake()
        {
            if (undoButton != null)
            {
                undoButton.onClick.RemoveAllListeners();
                undoButton.onClick.AddListener(OnClickUndo);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(OnClickExit);
            }
        }

        private void OnEnable()
        {
            var gm = SingleGameManager.Instance;
            if (gm != null)
            {
                gm.OnUndoCountChanged -= RefreshUndo;
                gm.OnUndoCountChanged += RefreshUndo;
                RefreshUndo(gm.RemainingUndo);
            }
        }

        private void OnDisable()
        {
            var gm = SingleGameManager.Instance;
            if (gm != null) gm.OnUndoCountChanged -= RefreshUndo;
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

        private void RefreshUndo(int remaining)
        {
            if (undoCountText != null) undoCountText.text = $"x{remaining}";
            if (undoButton != null) undoButton.interactable = remaining > 0;
        }

        private void OnClickUndo()
        {
            var gm = SingleGameManager.Instance;
            if (gm != null) gm.OnUndoMove();
        }

        private void OnClickExit()
        {
            // TODO: showExitConfirm == true일 때 확인 팝업 연결
            var gm = SingleGameManager.Instance;
            if (gm != null) gm.OnExitRequested();

            SceneManager.LoadScene(mainSceneName, LoadSceneMode.Single);
        }
    }
}
