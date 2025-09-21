using UnityEngine;
using UnityEngine.UI;
using TMPro;
using App.Core;

namespace Features.Multi.UI
{
    /// <summary>
    /// 네트워크 연결 끊김 알림 모달
    /// 멀티플레이 모드에서 서버 연결이 끊어졌을 때 사용자에게 알림 표시
    /// </summary>
    public class NetworkDisconnectedModal : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backgroundButton;

        [Header("기본 설정")]
        [SerializeField] private string defaultTitle = "연결 끊김";
        [SerializeField] private string defaultMessage = "서버와의 연결이 끊어졌습니다.\n메인 화면으로 이동합니다.";
        [SerializeField] private string confirmButtonText = "확인";

        // 싱글톤 패턴
        public static NetworkDisconnectedModal Instance { get; private set; }

        // 콜백 액션
        private System.Action onConfirmAction;

        void Awake()
        {
            // 싱글톤 설정
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeUI();
                Hide();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeUI()
        {
            // 버튼 이벤트 연결
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (backgroundButton != null)
            {
                backgroundButton.onClick.AddListener(OnConfirmClicked);
            }

            // 기본 텍스트 설정
            if (titleText != null)
            {
                titleText.text = defaultTitle;
            }

            if (messageText != null)
            {
                messageText.text = defaultMessage;
            }

            // 버튼 텍스트 설정
            if (confirmButton != null)
            {
                var buttonText = confirmButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = confirmButtonText;
                }
            }
        }

        /// <summary>
        /// 모달 표시
        /// </summary>
        public static void Show(string title = null, string message = null, System.Action onConfirm = null)
        {
            if (Instance == null)
            {
                Debug.LogError("[NetworkDisconnectedModal] Instance가 null입니다.");
                return;
            }

            Instance.ShowInternal(title, message, onConfirm);
        }

        /// <summary>
        /// 모달 숨김
        /// </summary>
        public static void Hide()
        {
            if (Instance == null) return;

            Instance.HideInternal();
        }

        private void ShowInternal(string title, string message, System.Action onConfirm)
        {
            // 콜백 설정
            onConfirmAction = onConfirm;

            // 텍스트 설정
            if (titleText != null && !string.IsNullOrEmpty(title))
            {
                titleText.text = title;
            }

            if (messageText != null && !string.IsNullOrEmpty(message))
            {
                messageText.text = message;
            }

            // 모달 표시
            if (modalPanel != null)
            {
                modalPanel.SetActive(true);
            }

            Debug.Log("[NetworkDisconnectedModal] 연결 끊김 알림 모달 표시됨");
        }

        private void HideInternal()
        {
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }

            // 콜백 초기화
            onConfirmAction = null;

            Debug.Log("[NetworkDisconnectedModal] 연결 끊김 알림 모달 숨겨짐");
        }

        private void OnConfirmClicked()
        {
            Debug.Log("[NetworkDisconnectedModal] 확인 버튼 클릭");

            HideInternal();

            // 확인 콜백 실행
            onConfirmAction?.Invoke();
        }

        void OnDestroy()
        {
            // 버튼 이벤트 해제
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClicked);
            }

            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveListener(OnConfirmClicked);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}