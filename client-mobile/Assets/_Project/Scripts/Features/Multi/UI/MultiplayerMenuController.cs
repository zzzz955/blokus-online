using UnityEngine;
using UnityEngine.UI;
using Features.Multi.Net;
using App.UI;
using App.Core;

namespace Features.Multi.UI
{
    /// <summary>
    /// 메인 씬의 멀티플레이어 메뉴 컨트롤러
    /// NetworkManager와 UI 연결을 담당
    /// </summary>
    public class MultiplayerMenuController : MonoBehaviour
    {
        [Header("UI 버튼 참조")]
        [SerializeField] private Button multiplayerButton;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button disconnectButton;
        
        [Header("상태 UI")]
        [SerializeField] private Text connectionStatusText;
        [SerializeField] private Text networkInfoText;
        
        [Header("로그인 UI")]
        [SerializeField] private InputField usernameInput;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button guestLoginButton;
        
        void Start()
        {
            InitializeUI();
            SubscribeToNetworkEvents();
        }
        
        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // 버튼 이벤트 연결
            if (multiplayerButton != null)
                multiplayerButton.onClick.AddListener(OnMultiplayerButtonClicked);
                
            if (connectButton != null)
                connectButton.onClick.AddListener(OnConnectButtonClicked);
                
            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(OnDisconnectButtonClicked);
                
            if (loginButton != null)
                loginButton.onClick.AddListener(OnLoginButtonClicked);
                
            if (guestLoginButton != null)
                guestLoginButton.onClick.AddListener(OnGuestLoginButtonClicked);
            
            UpdateConnectionStatus();
        }
        
        /// <summary>
        /// 네트워크 이벤트 구독
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionChanged += OnConnectionChanged;
                NetworkManager.Instance.OnAuthResponse += OnAuthResponse;
                NetworkManager.Instance.OnErrorReceived += OnErrorReceived;
            }
        }
        
        /// <summary>
        /// 멀티플레이어 버튼 클릭 처리 - UIManager로 위임
        /// </summary>
        private void OnMultiplayerButtonClicked()
        {
            Debug.Log("[MultiplayerMenuController] 멀티플레이어 버튼 클릭 - UIManager로 위임");
            
            // SessionManager 로그인 상태 확인
            if (SessionManager.Instance == null || !SessionManager.Instance.IsLoggedIn)
            {
                ShowError("먼저 로그인해주세요.");
                return;
            }
            
            // refreshToken 유효성 확인 (TCP 소켓 인증에는 refresh token 사용)
            string refreshToken = GetRefreshTokenFromSession();
            if (string.IsNullOrEmpty(refreshToken))
            {
                ShowError("인증 정보가 유효하지 않습니다. 다시 로그인해주세요.");
                return;
            }
            
            // UIManager의 멀티플레이 모드 선택으로 위임 (중복 실행 방지)
            var uiManager = UIManager.GetInstance();
            if (uiManager != null)
            {
                uiManager.OnMultiModeSelected();
            }
            else
            {
                ShowError("UI 관리자를 찾을 수 없습니다.");
            }
        }
        
        /// <summary>
        /// 연결 버튼 클릭 처리
        /// </summary>
        private void OnConnectButtonClicked()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.ConnectToServer();
            }
        }
        
        /// <summary>
        /// 연결 해제 버튼 클릭 처리
        /// </summary>
        private void OnDisconnectButtonClicked()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.DisconnectFromServer();
            }
        }
        
        /// <summary>
        /// 로그인 버튼 클릭 처리
        /// </summary>
        private void OnLoginButtonClicked()
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected())
            {
                ShowError("서버에 연결되지 않았습니다.");
                return;
            }
            
            string username = usernameInput?.text ?? "";
            string password = passwordInput?.text ?? "";
            
            if (string.IsNullOrEmpty(username))
            {
                ShowError("사용자명을 입력하세요.");
                return;
            }
            
            NetworkManager.Instance.Login(username, password);
        }
        
        /// <summary>
        /// 게스트 로그인 버튼 클릭 처리
        /// </summary>
        private void OnGuestLoginButtonClicked()
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected())
            {
                ShowError("서버에 연결되지 않았습니다.");
                return;
            }
            
            NetworkManager.Instance.GuestLogin();
        }
        
        /// <summary>
        /// 연결 상태 변경 이벤트 핸들러
        /// </summary>
        private void OnConnectionChanged(bool isConnected)
        {
            UpdateConnectionStatus();
            
            if (isConnected)
            {
                ShowMessage("서버에 연결되었습니다.");
            }
            else
            {
                ShowMessage("서버 연결이 끊어졌습니다.");
            }
        }
        
        /// <summary>
        /// 인증 응답 이벤트 핸들러
        /// </summary>
        private void OnAuthResponse(bool success, string message)
        {
            if (success)
            {
                ShowMessage($"로그인 성공: {message}");
                // MultiCore에서 인증 후 자동으로 MultiGameplayScene으로 전환됨
                // 중복 씬 로드 방지를 위해 LoadMultiplayerScene() 호출 제거
            }
            else
            {
                ShowError($"로그인 실패: {message}");
            }
        }
        
        /// <summary>
        /// 에러 이벤트 핸들러
        /// </summary>
        private void OnErrorReceived(string error)
        {
            // NetworkClient에서 이미 로그를 출력하므로 UI 토스트만 표시
            if (SystemMessageManager.Instance != null)
            {
                SystemMessageManager.ShowToast($"네트워크 에러: {error}");
            }
        }
        
        /// <summary>
        /// 연결 상태 UI 업데이트
        /// </summary>
        private void UpdateConnectionStatus()
        {
            bool isConnected = NetworkManager.Instance?.IsConnected() ?? false;
            
            if (connectionStatusText != null)
            {
                connectionStatusText.text = isConnected ? "연결됨" : "연결 안됨";
                connectionStatusText.color = isConnected ? Color.green : Color.red;
            }
            
            if (networkInfoText != null && NetworkManager.Instance != null)
            {
                networkInfoText.text = NetworkManager.Instance.GetStatusInfo();
            }
            
            // 버튼 상태 업데이트
            if (connectButton != null)
                connectButton.interactable = !isConnected;
                
            if (disconnectButton != null)
                disconnectButton.interactable = isConnected;
                
            if (loginButton != null)
                loginButton.interactable = isConnected;
                
            if (guestLoginButton != null)
                guestLoginButton.interactable = isConnected;
        }
        
        /// <summary>
        /// MultiCore 씬 로드 (데이터 로딩 전용)
        /// </summary>
        private void LoadMultiCoreScene()
        {
            // SceneFlowController를 통한 씬 전환
            if (App.Core.SceneFlowController.Instance != null)
            {
                App.Core.SceneFlowController.Instance.StartGoMulti();
            }
            else
            {
                // 레거시 방식
                UnityEngine.SceneManagement.SceneManager.LoadScene("MultiCore");
            }
        }
        
        /// <summary>
        /// 멀티플레이어 씬 로드
        /// </summary>
        private void LoadMultiplayerScene()
        {
            Debug.Log("[MultiplayerMenuController] 멀티플레이어 씬 로드");
            
            // SceneFlowController를 통한 씬 전환
            if (App.Core.SceneFlowController.Instance != null)
            {
                App.Core.SceneFlowController.Instance.StartGoMulti();
            }
            else
            {
                // 레거시 방식
                UnityEngine.SceneManagement.SceneManager.LoadScene("MultiGameplay");
            }
        }
        
        /// <summary>
        /// SessionManager에서 refreshToken 가져오기
        /// </summary>
        private string GetRefreshTokenFromSession()
        {
            if (SessionManager.Instance != null && SessionManager.Instance.IsLoggedIn)
            {
                // OidcAuthenticator를 통해 실제 refreshToken 가져오기
                var oidcAuthenticator = FindObjectOfType<App.Network.OidcAuthenticator>();
                if (oidcAuthenticator != null)
                {
                    return oidcAuthenticator.GetRefreshToken();
                }
                
                Debug.LogWarning("[MultiplayerMenuController] OidcAuthenticator를 찾을 수 없습니다.");
            }
            
            return null;
        }
        
        /// <summary>
        /// 메시지 표시
        /// </summary>
        private void ShowMessage(string message)
        {
            Debug.Log($"[MultiplayerMenuController] {message}");
            
            if (SystemMessageManager.Instance != null)
            {
                SystemMessageManager.ShowToast(message);
            }
        }
        
        /// <summary>
        /// 에러 메시지 표시
        /// </summary>
        private void ShowError(string error)
        {
            Debug.LogError($"[MultiplayerMenuController] {error}");
            
            if (SystemMessageManager.Instance != null)
            {
                SystemMessageManager.ShowToast(error);
            }
        }
        
        void OnDestroy()
        {
            // 이벤트 구독 해제
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionChanged -= OnConnectionChanged;
                NetworkManager.Instance.OnAuthResponse -= OnAuthResponse;
                NetworkManager.Instance.OnErrorReceived -= OnErrorReceived;
            }
        }
    }
}