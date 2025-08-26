// Assets/Scripts/UI/ConfirmationModal.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
namespace Features.Single.UI.InGame{
    /// <summary>
    /// 확인/취소 모달 다이얼로그
    /// </summary>
    public class ConfirmationModal : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button rejectButton;
        [SerializeField] private Button backgroundButton; // Optional: close on background click

        [Header("Settings")]
        [SerializeField] private string acceptButtonText = "확인";
        [SerializeField] private string rejectButtonText = "취소";

        // Callbacks
        private Action onAccept;
        private Action onReject;

        private void Awake()
        {
            if (modalPanel != null)
                modalPanel.SetActive(false);

            // Button event setup
            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveAllListeners();
                acceptButton.onClick.AddListener(OnAcceptClicked);
            }

            if (rejectButton != null)
            {
                rejectButton.onClick.RemoveAllListeners();
                rejectButton.onClick.AddListener(OnRejectClicked);
            }

            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveAllListeners();
                backgroundButton.onClick.AddListener(OnRejectClicked); // Background click = cancel
            }
        }

        /// <summary>
        /// 모달을 표시합니다
        /// </summary>
        /// <param name="title">제목</param>
        /// <param name="message">메시지</param>
        /// <param name="onAcceptCallback">확인 버튼 콜백</param>
        /// <param name="onRejectCallback">취소 버튼 콜백</param>
        public void Show(string title, string message, Action onAcceptCallback = null, Action onRejectCallback = null)
        {
            if (titleText != null)
                titleText.text = title;

            if (messageText != null)
                messageText.text = message;

            // Update button texts
            if (acceptButton != null && acceptButton.GetComponentInChildren<TMP_Text>() != null)
                acceptButton.GetComponentInChildren<TMP_Text>().text = acceptButtonText;

            if (rejectButton != null && rejectButton.GetComponentInChildren<TMP_Text>() != null)
                rejectButton.GetComponentInChildren<TMP_Text>().text = rejectButtonText;

            onAccept = onAcceptCallback;
            onReject = onRejectCallback;

            if (modalPanel != null)
                modalPanel.SetActive(true);
        }

        /// <summary>
        /// 모달을 숨깁니다
        /// </summary>
        public void Hide()
        {
            if (modalPanel != null)
                modalPanel.SetActive(false);
            // ❌ 여기서 onAccept/onReject 를 null로 지우지 않습니다.
        }

        private void OnAcceptClicked()
        {
            var accept = onAccept;   // 백업
            var reject = onReject;
            // 한 번 쓴 뒤 재진입 방지
            onAccept = null;
            onReject = null;

            accept?.Invoke();        // TopBarUI가 undoButton 다시 켬 + Undo 실행
            Hide();
        }

        private void OnRejectClicked()
        {
            var accept = onAccept;
            var reject = onReject;
            onAccept = null;
            onReject = null;

            reject?.Invoke();        // TopBarUI가 undoButton 다시 켬
            Hide();
        }

        /// <summary>
        /// 버튼 텍스트 설정
        /// </summary>
        public void SetButtonTexts(string acceptText, string rejectText)
        {
            acceptButtonText = acceptText;
            rejectButtonText = rejectText;
        }

        /// <summary>
        /// Undo 확인 모달을 위한 편의 메서드
        /// </summary>
        public void ShowUndoConfirmation(Action onConfirm, Action onCancel = null)
        {
            Show(
                "실행 취소",
                "마지막 블록 배치를 되돌리시겠습니까?\n블록이 팔레트로 돌아가고 점수가 감소합니다.",
                onConfirm,
                onCancel
            );
        }
        
        /// <summary>
        /// 게임 종료 확인 모달을 위한 편의 메서드
        /// </summary>
        public void ShowExitConfirmation(Action onConfirm, Action onCancel = null)
        {
            Show(
                "게임 종료",
                "게임을 종료하고 스테이지 목록으로 돌아가시겠습니까?\n현재 진행상황이 저장됩니다.",
                onConfirm,
                onCancel
            );
        }
    }
}