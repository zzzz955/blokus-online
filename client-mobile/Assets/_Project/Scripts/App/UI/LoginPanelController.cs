using UnityEngine;
using UnityEngine.UI;
using TMPro;
using App.Core;
using Shared.UI;
namespace App.UI{
    /// <summary>
    /// Login panel controller for MainScene
    /// Migration Plan: 로그인 로직은 MainScene의 패널에서 동작
    /// </summary>
    public class LoginPanelController : PanelBase
    {
        [Header("Login UI Components")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button registerButton;
        
        [Header("UI Feedback")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject loadingIndicator;

        private bool isProcessingLogin = false;

        protected override void Awake()
        {
            base.Awake();
            
            // Validate required components
            ValidateComponents();
        }

        protected override void Start()
        {
            base.Start();
            
            // Setup UI event handlers
            SetupEventHandlers();
            
            // Initialize UI state
            InitializeUI();
        }

        private void ValidateComponents()
        {
            if (usernameInput == null || passwordInput == null || loginButton == null)
            {
                Debug.LogError("[LoginPanelController] Required UI components not assigned!", this);
            }
            
            // TMP_Text 컴포넌트 검증 및 복구
            if (statusText == null)
            {
                Debug.LogWarning("[LoginPanelController] statusText가 할당되지 않음, 자동으로 찾는 중...");
                var textComponents = GetComponentsInChildren<TMP_Text>();
                foreach (var text in textComponents)
                {
                    if (text.name.ToLower().Contains("status") || text.name.ToLower().Contains("message"))
                    {
                        statusText = text;
                        Debug.Log($"[LoginPanelController] statusText 자동 할당: {text.name}");
                        break;
                    }
                }
                
                if (statusText == null)
                {
                    Debug.LogWarning("[LoginPanelController] statusText를 찾을 수 없음, TextMeshProUGUI로 생성 시도");
                    var statusObj = GameObject.Find("StatusText");
                    if (statusObj != null)
                    {
                        statusText = statusObj.GetComponent<TMP_Text>();
                        if (statusText == null)
                        {
                            statusText = statusObj.AddComponent<TMPro.TextMeshProUGUI>();
                            Debug.Log("[LoginPanelController] statusText를 TextMeshProUGUI로 생성했습니다");
                        }
                    }
                }
            }
        }

        private void SetupEventHandlers()
        {
            if (loginButton != null)
                loginButton.onClick.AddListener(OnLoginButtonClicked);
            
            if (registerButton != null)
                registerButton.onClick.AddListener(OnRegisterButtonClicked);
            
            // Enter key support
            if (usernameInput != null)
                usernameInput.onSubmit.AddListener(OnUsernameSubmit);
            
            if (passwordInput != null)
                passwordInput.onSubmit.AddListener(OnPasswordSubmit);

            // Listen to session manager events
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnLoginStateChanged += OnLoginStateChanged;
            }
        }

        private void InitializeUI()
        {
            SetLoginUIEnabled(true);
            UpdateStatusText("", false);
            
            if (loadingIndicator != null)
                loadingIndicator.SetActive(false);
        }

        // ========================================
        // UI Event Handlers
        // ========================================

        private async void OnLoginButtonClicked()
        {
            if (isProcessingLogin) return;
            
            string username = usernameInput?.text?.Trim() ?? "";
            string password = passwordInput?.text ?? "";
            
            // Validate input
            if (string.IsNullOrEmpty(username))
            {
                UpdateStatusText("사용자명을 입력해주세요.", true);
                return;
            }
            
            if (string.IsNullOrEmpty(password))
            {
                UpdateStatusText("비밀번호를 입력해주세요.", true);
                return;
            }
            
            // Start login process
            await ProcessLogin(username, password);
        }

        private void OnRegisterButtonClicked()
        {
            if (debugMode)
                Debug.Log("[LoginPanelController] Register button clicked");
            
            // TODO: Implement registration flow
            UpdateStatusText("회원가입 기능은 준비 중입니다.", false);
        }

        private void OnUsernameSubmit(string value)
        {
            // Move focus to password field
            if (passwordInput != null)
                passwordInput.Select();
        }

        private void OnPasswordSubmit(string value)
        {
            // Trigger login when Enter is pressed in password field
            OnLoginButtonClicked();
        }

        // ========================================
        // Login Process
        // ========================================

        private async System.Threading.Tasks.Task ProcessLogin(string username, string password)
        {
            if (debugMode)
                Debug.Log($"[LoginPanelController] Processing login for: {username}");

            isProcessingLogin = true;
            SetLoginUIEnabled(false);
            UpdateStatusText("로그인 중...", false);
            
            if (loadingIndicator != null)
                loadingIndicator.SetActive(true);

            try
            {
                // Use SessionManager for login
                bool loginSuccess = await SessionManager.Instance.Login(username, password);
                
                if (loginSuccess)
                {
                    UpdateStatusText("로그인 성공!", false);
                    Debug.Log("[LoginPanelController] 로그인 성공 처리 시작");
                    
                    // 즉시 BlokusUIManager 호출 - 강화된 방어 코드
                    Debug.Log("[LoginPanelController] BlokusUIManager 찾기 시작");
                    Debug.Log($"[LoginPanelController] BlokusUIManager.Instance 값: {BlokusUIManager.Instance}");
                    
                    BlokusUIManager uiManager = BlokusUIManager.GetInstance();
                    Debug.Log($"[LoginPanelController] BlokusUIManager.GetInstance() 결과: {uiManager}");
                    
                    if (uiManager != null)
                    {
                        Debug.Log("[LoginPanelController] BlokusUIManager 발견! OnLoginSuccess() 즉시 호출");
                        try
                        {
                            uiManager.OnLoginSuccess();
                            Debug.Log("[LoginPanelController] BlokusUIManager.OnLoginSuccess() 호출 완료");
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[LoginPanelController] OnLoginSuccess() 호출 중 오류: {ex.Message}");
                            
                            // 폴백: SystemMessageManager로 사용자에게 알림
                            if (SystemMessageManager.Instance != null)
                            {
                                MessagePriority priority = MessagePriority.Warning;
                                SystemMessageManager.ShowToast("로그인 성공했지만 화면 전환에 문제가 있습니다.", priority);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError("[LoginPanelController] BlokusUIManager를 찾을 수 없습니다!");
                        Debug.LogError($"[LoginPanelController] BlokusUIManager.Instance: {BlokusUIManager.Instance}");
                        Debug.LogError($"[LoginPanelController] FindObjectOfType 결과: {Object.FindObjectOfType<BlokusUIManager>()}");
                        
                        // 폴백: SystemMessageManager로 사용자에게 알림
                        if (SystemMessageManager.Instance != null)
                        {
                            MessagePriority priority = MessagePriority.Error;
                            SystemMessageManager.ShowToast("로그인 성공했지만 UI 매니저를 찾을 수 없습니다.", priority);
                        }
                        
                        // 수동으로 씬 새로고침 시도
                        Debug.Log("[LoginPanelController] 수동으로 MainScene 새로고침 시도");
                        StartCoroutine(RefreshMainSceneAfterDelay());
                    }
                }
                else
                {
                    UpdateStatusText("로그인에 실패했습니다.", true);
                    SetLoginUIEnabled(true);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LoginPanelController] Login error: {ex.Message}");
                UpdateStatusText($"로그인 오류: {ex.Message}", true);
                SetLoginUIEnabled(true);
            }
            finally
            {
                isProcessingLogin = false;
                
                if (loadingIndicator != null)
                    loadingIndicator.SetActive(false);
            }
        }

        // ========================================
        // Session Events
        // ========================================

        private void OnLoginStateChanged(bool isLoggedIn)
        {
            if (debugMode)
                Debug.Log($"[LoginPanelController] Login state changed: {isLoggedIn}");

            if (isLoggedIn)
            {
                // User logged in successfully
                SetLoginUIEnabled(false);
                UpdateStatusText("로그인됨", false);
            }
            else
            {
                // User logged out or login failed
                SetLoginUIEnabled(true);
                UpdateStatusText("", false);
                ClearInputFields();
            }
        }

        // ========================================
        // UI Helper Methods
        // ========================================

        private void SetLoginUIEnabled(bool enabled)
        {
            if (usernameInput != null)
                usernameInput.interactable = enabled;
            
            if (passwordInput != null)
                passwordInput.interactable = enabled;
            
            if (loginButton != null)
                loginButton.interactable = enabled;
            
            if (registerButton != null)
                registerButton.interactable = enabled;
        }

        private void UpdateStatusText(string message, bool isError)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = isError ? Color.red : Color.white;
            }
            else
            {
                Debug.LogWarning($"[LoginPanelController] statusText가 null입니다. 메시지: {message}");
                
                // 폴백: SystemMessageManager 사용
                if (SystemMessageManager.Instance != null)
                {
                    MessagePriority priority = isError ? MessagePriority.Error : MessagePriority.Info;
                    SystemMessageManager.ShowToast(message, priority);
                }
            }
        }

        private void ClearInputFields()
        {
            if (usernameInput != null)
                usernameInput.text = "";
            
            if (passwordInput != null)
                passwordInput.text = "";
        }

        // ========================================
        // Public API
        // ========================================

        /// <summary>
        /// Focus on username input field
        /// </summary>
        public void FocusUsernameInput()
        {
            if (usernameInput != null)
                usernameInput.Select();
        }

        /// <summary>
        /// Check if login is currently in progress
        /// </summary>
        public bool IsProcessingLogin => isProcessingLogin;

        /// <summary>
        /// Fallback method to refresh MainScene when UI Manager is not found
        /// </summary>
        private System.Collections.IEnumerator RefreshMainSceneAfterDelay()
        {
            yield return new WaitForSeconds(1.0f);
            
            Debug.Log("[LoginPanelController] MainScene 새로고침 시도");
            
            // Try to find BlokusUIManager again
            BlokusUIManager uiManager = Object.FindObjectOfType<BlokusUIManager>();
            if (uiManager != null)
            {
                Debug.Log("[LoginPanelController] 새로고침 후 BlokusUIManager 발견! OnLoginSuccess() 호출");
                uiManager.OnLoginSuccess();
            }
            else
            {
                Debug.LogError("[LoginPanelController] 새로고침 후에도 BlokusUIManager를 찾을 수 없습니다.");
                
                // Final fallback: reload current scene
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                Debug.Log($"[LoginPanelController] 최종 폴백: {currentScene.name} 씬 재로드");
                UnityEngine.SceneManagement.SceneManager.LoadScene(currentScene.name);
            }
        }

        // ========================================
        // Unity Lifecycle
        // ========================================

        void OnDestroy()
        {
            // Clean up event handlers
            if (loginButton != null)
                loginButton.onClick.RemoveListener(OnLoginButtonClicked);
            
            if (registerButton != null)
                registerButton.onClick.RemoveListener(OnRegisterButtonClicked);
            
            if (usernameInput != null)
                usernameInput.onSubmit.RemoveListener(OnUsernameSubmit);
            
            if (passwordInput != null)
                passwordInput.onSubmit.RemoveListener(OnPasswordSubmit);

            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.OnLoginStateChanged -= OnLoginStateChanged;
            }
        }
    }
}