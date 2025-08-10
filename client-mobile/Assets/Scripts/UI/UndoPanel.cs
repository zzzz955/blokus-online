// Assets/Scripts/UI/UndoPanel.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.Game;

namespace BlokusUnity.UI
{
    /// <summary>
    /// Undo 버튼 + 남은 횟수 라벨 UI
    /// </summary>
    public sealed class UndoPanel : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Button undoButton;
        [SerializeField] private TMP_Text countText; // Text 사용하면 TMP_Text 대신 Text로 바꿔도 됨

        private void Awake()
        {
            if (undoButton != null)
            {
                undoButton.onClick.RemoveAllListeners();
                undoButton.onClick.AddListener(OnClickUndo);
            }
        }

        private void OnEnable()
        {
            // 싱글톤 매니저 이벤트 구독
            if (SingleGameManager.Instance != null)
            {
                SingleGameManager.Instance.OnUndoCountChanged -= Refresh;
                SingleGameManager.Instance.OnUndoCountChanged += Refresh;
                Refresh(SingleGameManager.Instance.RemainingUndo);
            }
        }

        private void OnDisable()
        {
            if (SingleGameManager.Instance != null)
            {
                SingleGameManager.Instance.OnUndoCountChanged -= Refresh;
            }
        }

        private void OnClickUndo()
        {
            if (SingleGameManager.Instance != null)
            {
                SingleGameManager.Instance.OnUndoMove();
            }
        }

        private void Refresh(int remain)
        {
            if (countText != null) countText.text = $"UNDO {remain}";
            if (undoButton != null) undoButton.interactable = remain > 0;
        }
    }
}
