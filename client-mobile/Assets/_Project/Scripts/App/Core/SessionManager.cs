using System.Threading.Tasks;
using UnityEngine;
using BlokusUnity.Network;
using BlokusUnity.UI.Messages;

namespace BlokusUnity
{
    /// <summary>
    /// Session manager for user authentication and session persistence
    /// Migration Plan: 로그인은 MainScene 로그인 패널에서 처리. 게스트 없음. 멀티 입장 시 캐싱된 ID/PW를 TCP 서버로 전송
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        // Singleton pattern
        public static SessionManager Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Session state
        private bool isLoggedIn = false;
        private string cachedId = "";
        private string cachedPassword = "";
        private string authToken = "";
        private int userId = 0;

        // Events
        public event System.Action<bool> OnLoginStateChanged;
        public event System.Action<string, int> OnUserDataReceived; // username, userId

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                
                if (debugMode)
                    Debug.Log("SessionManager initialized with DontDestroyOnLoad");
            }
            else
            {
                if (debugMode)
                    Debug.Log("SessionManager duplicate instance destroyed");
                Destroy(gameObject);
            }
        }

        // ========================================
        // Public API
        // ========================================

        /// <summary>
        /// Check if user is currently logged in
        /// Migration Plan: 플레이모드 재시작 없이 씬 전환 반복해도 캐시/상태 일관
        /// </summary>
        public bool IsLoggedIn => isLoggedIn;

        /// <summary>
        /// Get cached user ID for TCP communication
        /// Migration Plan: 멀티 입장 시 캐싱된 ID/PW를 TCP 서버로 전송
        /// </summary>
        public string CachedId => cachedId;

        /// <summary>
        /// Get cached password for TCP communication
        /// Migration Plan: 토큰은 서버에서 관리/갱신
        /// </summary>
        public string CachedPassword => cachedPassword;

        /// <summary>
        /// Get current auth token
        /// </summary>
        public string AuthToken => authToken;

        /// <summary>
        /// Get current user ID
        /// </summary>
        public int UserId => userId;

        /// <summary>
        /// Login with username and password
        /// Migration Plan: 게스트 금지, ID/PW 입력 → REST 로그인 성공 시 메모리 캐시 저장
        /// </summary>
        public async Task<bool> Login(string username, string password)
        {
            if (debugMode)
                Debug.Log($"[SessionManager] Login attempt: {username}");

            try
            {
                // Use HttpApiClient for REST login
                var loginTask = new TaskCompletionSource<bool>();
                
                HttpApiClient.Instance.OnAuthResponse += OnLoginResponse;
                HttpApiClient.Instance.OnUserInfoReceived += OnUserInfoResponse;
                
                HttpApiClient.Instance.Login(username, password);
                
                // Wait for response (timeout after 10 seconds)
                bool loginSuccess = await WaitForLoginResult(loginTask, 10f);
                
                HttpApiClient.Instance.OnAuthResponse -= OnLoginResponse;
                HttpApiClient.Instance.OnUserInfoReceived -= OnUserInfoResponse;

                if (loginSuccess)
                {
                    // Cache credentials for TCP communication
                    cachedId = username;
                    cachedPassword = password;
                    isLoggedIn = true;
                    
                    OnLoginStateChanged?.Invoke(true);
                    
                    if (debugMode)
                        Debug.Log($"[SessionManager] Login successful: {username}");
                    
                    return true;
                }
                else
                {
                    ClearSession();
                    if (debugMode)
                        Debug.Log($"[SessionManager] Login failed: {username}");
                    
                    return false;
                }

                void OnLoginResponse(bool success, string message, string token)
                {
                    if (success)
                    {
                        authToken = token;
                        loginTask.TrySetResult(true);
                    }
                    else
                    {
                        // Migration Plan: 실패 시 SystemMessageManager로 토스트
                        SystemMessageManager.ShowToast($"로그인 실패: {message}", MessagePriority.Error);
                        loginTask.TrySetResult(false);
                    }
                }

                void OnUserInfoResponse(HttpApiClient.AuthUserData userData)
                {
                    userId = userData.user.user_id;
                    OnUserDataReceived?.Invoke(userData.user.username, userId);
                }
            }
            catch (System.Exception ex)
            {
                if (debugMode)
                    Debug.LogError($"[SessionManager] Login exception: {ex.Message}");
                
                // Migration Plan: 로그인 실패/네트워크 예외 시 사용자 피드백 명확
                SystemMessageManager.ShowToast($"로그인 오류: {ex.Message}", MessagePriority.Error);
                
                ClearSession();
                return false;
            }
        }

        /// <summary>
        /// Logout and clear session
        /// </summary>
        public void Logout()
        {
            if (debugMode)
                Debug.Log("[SessionManager] Logout requested");

            if (isLoggedIn)
            {
                HttpApiClient.Instance.Logout();
            }
            
            ClearSession();
            OnLoginStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Get credentials for TCP server communication
        /// Migration Plan: 멀티 입장 시 캐싱된 ID/PW를 TCP 서버로 전송(서버가 토큰 식별/갱신)
        /// </summary>
        public (string id, string password) GetCredentialsForTcp()
        {
            if (!isLoggedIn)
            {
                Debug.LogWarning("[SessionManager] GetCredentialsForTcp called but user not logged in");
                return ("", "");
            }
            
            return (cachedId, cachedPassword);
        }

        /// <summary>
        /// Validate current session
        /// </summary>
        public async Task<bool> ValidateSession()
        {
            if (!isLoggedIn || string.IsNullOrEmpty(authToken))
            {
                return false;
            }

            try
            {
                var validateTask = new TaskCompletionSource<bool>();
                
                HttpApiClient.Instance.OnAuthResponse += OnValidateResponse;
                HttpApiClient.Instance.ValidateToken();
                
                bool isValid = await WaitForLoginResult(validateTask, 5f);
                
                HttpApiClient.Instance.OnAuthResponse -= OnValidateResponse;
                
                if (!isValid)
                {
                    ClearSession();
                    OnLoginStateChanged?.Invoke(false);
                }
                
                return isValid;

                void OnValidateResponse(bool success, string message, string token)
                {
                    validateTask.TrySetResult(success);
                }
            }
            catch (System.Exception ex)
            {
                if (debugMode)
                    Debug.LogError($"[SessionManager] Session validation exception: {ex.Message}");
                
                ClearSession();
                OnLoginStateChanged?.Invoke(false);
                return false;
            }
        }

        // ========================================
        // Private Methods
        // ========================================

        private void ClearSession()
        {
            isLoggedIn = false;
            cachedId = "";
            cachedPassword = "";
            authToken = "";
            userId = 0;
            
            if (debugMode)
                Debug.Log("[SessionManager] Session cleared");
        }

        private async Task<bool> WaitForLoginResult(TaskCompletionSource<bool> taskSource, float timeoutSeconds)
        {
            var timeoutTask = Task.Delay((int)(timeoutSeconds * 1000));
            var completedTask = await Task.WhenAny(taskSource.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                // Timeout
                SystemMessageManager.ShowToast("로그인 요청 시간 초과", MessagePriority.Error);
                return false;
            }
            
            return taskSource.Task.Result;
        }

        // ========================================
        // Unity Lifecycle
        // ========================================

        void Start()
        {
            // Initialize session state
            ClearSession();
        }

        void OnDestroy()
        {
            // Clean up event subscriptions
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.OnAuthResponse -= null;
                HttpApiClient.Instance.OnUserInfoReceived -= null;
            }
        }
    }
}
