using UnityEngine;
using UnityEngine.UI;
using TMPro;
using App.Core;

namespace Features.Multi.UI
{
    /// <summary>
    /// 연결 끊김 원인 열거형
    /// </summary>
    public enum DisconnectReason
    {
        Unknown,                // 알 수 없는 원인
        AuthenticationFailure,  // 인증 실패
        DuplicateLogin,        // 중복 로그인
        ConnectionTimeout,     // 연결 시간 초과
        ServerError,           // 서버 오류
        NetworkError,          // 네트워크 오류
        ServerMaintenance      // 서버 점검
    }

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

        [Header("에러 원인별 메시지")]
        [SerializeField] private string authenticationFailureMessage = "인증에 실패하여 연결이 끊어졌습니다.\n다시 로그인해 주세요.";
        [SerializeField] private string duplicateLoginMessage = "다른 곳에서 같은 계정으로 로그인하여\n연결이 끊어졌습니다.";
        [SerializeField] private string connectionTimeoutMessage = "네트워크 연결 시간이 초과되어\n연결이 끊어졌습니다.";
        [SerializeField] private string serverErrorMessage = "서버 오류로 인해 연결이 끊어졌습니다.\n잠시 후 다시 시도해 주세요.";

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
        /// 에러 원인별 모달 표시
        /// </summary>
        public static void ShowWithReason(DisconnectReason reason, string customMessage = null, System.Action onConfirm = null)
        {
            if (Instance == null)
            {
                Debug.LogError("[NetworkDisconnectedModal] Instance가 null입니다.");
                return;
            }

            Instance.ShowWithReasonInternal(reason, customMessage, onConfirm);
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

        private void ShowWithReasonInternal(DisconnectReason reason, string customMessage, System.Action onConfirm)
        {
            // 콜백 설정
            onConfirmAction = onConfirm;

            // 원인별 제목과 메시지 설정
            string title = GetReasonTitle(reason);
            string message = string.IsNullOrEmpty(customMessage) ? GetReasonMessage(reason) : customMessage;

            // 텍스트 설정
            if (titleText != null)
            {
                titleText.text = title;
            }

            if (messageText != null)
            {
                messageText.text = message;
            }

            // 모달 표시
            if (modalPanel != null)
            {
                modalPanel.SetActive(true);
            }

            Debug.Log($"[NetworkDisconnectedModal] 연결 끊김 모달 표시됨 - 원인: {reason}, 메시지: {message}");
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

        /// <summary>
        /// 연결 끊김 원인별 제목 반환
        /// </summary>
        private string GetReasonTitle(DisconnectReason reason)
        {
            switch (reason)
            {
                case DisconnectReason.AuthenticationFailure:
                    return "인증 실패";
                case DisconnectReason.DuplicateLogin:
                    return "중복 로그인";
                case DisconnectReason.ConnectionTimeout:
                    return "연결 시간 초과";
                case DisconnectReason.ServerError:
                    return "서버 오류";
                case DisconnectReason.NetworkError:
                    return "네트워크 오류";
                case DisconnectReason.ServerMaintenance:
                    return "서버 점검";
                default:
                    return defaultTitle;
            }
        }

        /// <summary>
        /// 연결 끊김 원인별 메시지 반환
        /// </summary>
        private string GetReasonMessage(DisconnectReason reason)
        {
            switch (reason)
            {
                case DisconnectReason.AuthenticationFailure:
                    return authenticationFailureMessage;
                case DisconnectReason.DuplicateLogin:
                    return duplicateLoginMessage;
                case DisconnectReason.ConnectionTimeout:
                    return connectionTimeoutMessage;
                case DisconnectReason.ServerError:
                    return serverErrorMessage;
                case DisconnectReason.NetworkError:
                    return "네트워크 연결에 문제가 발생했습니다.\n연결 상태를 확인해 주세요.";
                case DisconnectReason.ServerMaintenance:
                    return "서버 점검 중입니다.\n잠시 후 다시 접속해 주세요.";
                default:
                    return defaultMessage;
            }
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