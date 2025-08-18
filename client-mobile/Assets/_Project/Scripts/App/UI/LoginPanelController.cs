using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.UI;

namespace BlokusUnity.UI
{
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
                    
                    // Hide login panel after successful login
                    await System.Threading.Tasks.Task.Delay(1000); // Brief delay to show success message
                    Hide();
                    
                    // TODO: Transition to next UI state (mode selection)
                    if (debugMode)
                        Debug.Log("[LoginPanelController] Login successful, should transition to mode selection");
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