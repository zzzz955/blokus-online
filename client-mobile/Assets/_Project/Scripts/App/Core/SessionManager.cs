using System.Threading.Tasks;
using UnityEngine;
using App.Network;
using App.UI;
using Shared.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif
namespace App.Core
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
        
        /// <summary>
        /// 릴리즈 빌드에서는 디버그 로그 비활성화
        /// </summary>
        private bool IsDebugEnabled => debugMode && (Application.isEditor || Debug.isDebugBuild);

        // Session state
        private bool isLoggedIn = false;
        private string cachedId = "";
        private string cachedPassword = "";
        private string authToken = "";
        private string refreshToken = "";
        private int userId = 0;
        private string displayName = "";

        // Events
        public event System.Action<bool> OnLoginStateChanged;
        public event System.Action<string, int> OnUserDataReceived; // username, userId
        public event System.Action<string> OnSavedUsernameLoaded; // 저장된 사용자명 로드시

        // 세션 영속성을 위한 상수 (6시간 짧은 세션)
        private const string SAVED_ACCESS_TOKEN_KEY = "blokus_access_token";
        private const string SAVED_REFRESH_TOKEN_KEY = "blokus_refresh_token";
        private const string SAVED_USER_ID_KEY = "blokus_user_id";
        private const string SAVED_USERNAME_KEY = "blokus_username";
        private const string SAVED_DISPLAY_NAME_KEY = "blokus_display_name";
        private const string SAVED_AT_KEY = "blokus_saved_at";
        
        // 세션 유효 시간 (6시간)
        private const int SESSION_VALID_HOURS = 6;

        // Unity 에디터용 EditorPrefs 키 (프리픽스 추가)
        private const string EDITOR_PREFIX = "BlokusEditor_";

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);

                if (IsDebugEnabled)
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
        /// Get current user displayName
        /// </summary>
        public string DisplayName => displayName;

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
                    cachedId = userData.user.username ?? cachedId;      // ★ 놓치지 말기
                    displayName = userData.user.display_name ?? displayName; // ★ 여기!
                    OnUserDataReceived?.Invoke(userData.user.username, userId);

                    if (debugMode)
                        Debug.Log($"[SessionManager] OnUserInfoResponse: username='{cachedId}', displayName='{displayName}'");
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

            // SingleCore 캐시 정리 (UserDataCache, StageProgress 등)
            ClearSingleCoreCache();

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
        /// Set tokens (access and refresh) for session management
        /// </summary>
        public void SetTokens(string accessToken, string refreshTokenValue, int userIdValue)
        {
            authToken = accessToken;
            refreshToken = refreshTokenValue;
            userId = userIdValue;
            isLoggedIn = true; // 🔥 핵심 수정: 로그인 상태 플래그 설정

            if (IsDebugEnabled)
                Debug.Log($"[SessionManager] 토큰 설정 완료: User {userId}, Refresh Token: {(!string.IsNullOrEmpty(refreshToken) ? "있음" : "없음")}");

            // 🔥 추가: UserDataCache가 이미 존재한다면 수동으로 사용자 정보 동기화
            TrySyncUserDataCache();
        }

        /// <summary>
        /// 🔥 추가: UserDataCache와 수동 동기화 (로그인 타이밍 문제 해결)
        /// </summary>
        private void TrySyncUserDataCache()
        {
            // UserDataCache가 존재하고 초기화된 상태인지 확인
            if (Features.Single.Core.UserDataCache.Instance != null && 
                Features.Single.Core.UserDataCache.Instance.IsInitialized)
            {
                // HttpApiClient에서 마지막 로그인 응답 정보를 가져와서 UserDataCache에 설정
                if (App.Network.HttpApiClient.Instance != null)
                {
                    var httpClient = App.Network.HttpApiClient.Instance;
                    
                    // HttpApiClient의 LastLoginResponse 또는 유사한 정보가 있다면 사용
                    // 없다면 UserDataCache의 SyncWithServer를 호출해서 서버에서 다시 가져오기
                    Features.Single.Core.UserDataCache.Instance.SyncWithServer();
                    
                    if (IsDebugEnabled)
                        Debug.Log($"[SessionManager] UserDataCache 수동 동기화 완료 - User {userId}");
                }
            }
            else if (IsDebugEnabled)
            {
                Debug.Log($"[SessionManager] UserDataCache 미초기화 상태 - 나중에 자동 동기화됨");
            }
        }

        /// <summary>
        /// Get refresh token for automatic login
        /// 🔥 수정: SecureStorage에서 refresh token 반환
        /// </summary>
        public string GetRefreshToken()
        {
            return App.Security.SecureStorage.GetString("blokus_refresh_token", "");
        }

        /// <summary>
        /// Check if refresh token is available
        /// 🔥 수정: SecureStorage에서 refresh token 확인
        /// </summary>
        public bool HasRefreshToken()
        {
            return App.Security.SecureStorage.HasKey("blokus_refresh_token");
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

        /// <summary>
        /// 메모리 세션만 초기화 (SecureStorage는 보존)
        /// </summary>
        private void ClearMemorySession()
        {
            isLoggedIn = false;
            cachedId = "";
            cachedPassword = "";
            authToken = "";
            refreshToken = "";
            userId = 0;
            displayName = "";

            // HttpApiClient에게 토큰 삭제 알림 (자동 재로그인 방지)
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.ClearAuthToken();
            }
            
            if (debugMode)
                Debug.Log("[SessionManager] Memory session cleared (SecureStorage 보존)");
        }

        /// <summary>
        /// 완전한 세션 삭제 (SecureStorage 포함) - 로그아웃 시에만 사용
        /// </summary>
        private void ClearSecureSession()
        {
            // 메모리 세션 초기화
            ClearMemorySession();

            // 🔥 SecureStorage에서 refresh token 삭제
            App.Security.SecureStorage.DeleteKey("blokus_refresh_token");
            App.Security.SecureStorage.DeleteKey("blokus_user_id");
            App.Security.SecureStorage.DeleteKey("blokus_username");
            
            if (debugMode)
                Debug.Log("[SessionManager] Secure session cleared (SecureStorage + 메모리 삭제)");
        }

        /// <summary>
        /// 호환성을 위한 기존 메서드 (로그아웃 시 사용)
        /// </summary>
        private void ClearSession()
        {
            ClearSecureSession();
        }

        // ========================================
        // 세션 영속성 (6시간 자동 로그인)
        // ========================================

        /// <summary>
        /// 세션 데이터를 PlayerPrefs에 저장 (6시간 유효)
        /// </summary>
        private void SaveSessionData()
        {
            if (!isLoggedIn || string.IsNullOrEmpty(authToken))
            {
                Debug.LogWarning("[SessionManager] 세션 저장 실패: 로그인되지 않았거나 토큰이 없음");
                return;
            }

            try
            {
#if UNITY_EDITOR
                // Unity 에디터에서는 EditorPrefs 사용 (더 안정적)
                EditorPrefs.SetString(EDITOR_PREFIX + SAVED_ACCESS_TOKEN_KEY, authToken);
                EditorPrefs.SetString(EDITOR_PREFIX + SAVED_REFRESH_TOKEN_KEY, refreshToken ?? "");
                EditorPrefs.SetInt(EDITOR_PREFIX + SAVED_USER_ID_KEY, userId);
                EditorPrefs.SetString(EDITOR_PREFIX + SAVED_USERNAME_KEY, cachedId);
                EditorPrefs.SetString(EDITOR_PREFIX + SAVED_DISPLAY_NAME_KEY, displayName);
                EditorPrefs.SetString(EDITOR_PREFIX + SAVED_AT_KEY, System.DateTime.Now.ToBinary().ToString());

                // 저장 후 즉시 확인
                string savedToken = EditorPrefs.GetString(EDITOR_PREFIX + SAVED_ACCESS_TOKEN_KEY, "");
                string savedUsername = EditorPrefs.GetString(EDITOR_PREFIX + SAVED_USERNAME_KEY, "");
#else
                // 빌드에서는 PlayerPrefs 사용
                PlayerPrefs.SetString(SAVED_ACCESS_TOKEN_KEY, authToken);
                PlayerPrefs.SetString(SAVED_REFRESH_TOKEN_KEY, refreshToken ?? "");
                PlayerPrefs.SetInt(SAVED_USER_ID_KEY, userId);
                PlayerPrefs.SetString(SAVED_USERNAME_KEY, cachedId);
                PlayerPrefs.SetString(SAVED_DISPLAY_NAME_KEY, displayName);
                PlayerPrefs.SetString(SAVED_AT_KEY, System.DateTime.Now.ToBinary().ToString());
                PlayerPrefs.Save();

                // 저장 후 즉시 확인
                string savedToken = PlayerPrefs.GetString(SAVED_ACCESS_TOKEN_KEY, "");
                string savedUsername = PlayerPrefs.GetString(SAVED_USERNAME_KEY, "");
#endif
                
                Debug.Log($"[SessionManager] 세션 데이터 저장 완료: {cachedId}");
                Debug.Log($"[SessionManager] 저장 확인 - Token: {(string.IsNullOrEmpty(savedToken) ? "실패" : "성공")}");
                Debug.Log($"[SessionManager] 저장 확인 - Username: {savedUsername}");
                Debug.Log($"[SessionManager] Unity 에디터: {Application.isEditor}");

#if UNITY_EDITOR
                Debug.Log($"[SessionManager] [EDITOR] EditorPrefs 사용 - 플레이모드 간 데이터 유지됨");
                Debug.Log($"[SessionManager] [EDITOR] 에디터 종료 후에도 데이터 유지됨");
#else
                Debug.Log($"[SessionManager] [BUILD] PlayerPrefs 사용");
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SessionManager] 세션 저장 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 저장된 세션이 유효한지 확인하고 복구
        /// </summary>
        private void CheckAndRestoreSession()
        {
            Debug.Log("[SessionManager] 세션 복구 시도 시작");
            Debug.Log($"[SessionManager] Unity 에디터: {Application.isEditor}");

            try
            {
                // 저장된 데이터 확인
#if UNITY_EDITOR
                string savedToken = EditorPrefs.GetString(EDITOR_PREFIX + SAVED_ACCESS_TOKEN_KEY, "");
                string savedTimeStr = EditorPrefs.GetString(EDITOR_PREFIX + SAVED_AT_KEY, "");
                string savedUsername = EditorPrefs.GetString(EDITOR_PREFIX + SAVED_USERNAME_KEY, "");
#else
                string savedToken = PlayerPrefs.GetString(SAVED_ACCESS_TOKEN_KEY, "");
                string savedTimeStr = PlayerPrefs.GetString(SAVED_AT_KEY, "");
                string savedUsername = PlayerPrefs.GetString(SAVED_USERNAME_KEY, "");
#endif

                Debug.Log($"[SessionManager] 저장소 조회 결과 (에디터: EditorPrefs, 빌드: PlayerPrefs):");
                Debug.Log($"[SessionManager] - Token: {(string.IsNullOrEmpty(savedToken) ? "없음" : "있음")}");
                Debug.Log($"[SessionManager] - Username: {savedUsername}");
                Debug.Log($"[SessionManager] - Time: {savedTimeStr}");

                if (string.IsNullOrEmpty(savedToken) || string.IsNullOrEmpty(savedTimeStr))
                {
                    Debug.Log("[SessionManager] 저장된 세션 없음 - 로그인 패널 표시");

#if UNITY_EDITOR
                    Debug.Log("[SessionManager] [EDITOR] EditorPrefs에 저장된 세션이 없습니다");
                    Debug.Log($"[SessionManager] [EDITOR] 확인할 키: {EDITOR_PREFIX + SAVED_ACCESS_TOKEN_KEY}");
                    
                    // 모든 저장 키 확인
                    Debug.Log("[SessionManager] [EDITOR] 모든 세션 키 확인:");
                    Debug.Log($"  - 토큰 키: '{EDITOR_PREFIX + SAVED_ACCESS_TOKEN_KEY}' = '{EditorPrefs.GetString(EDITOR_PREFIX + SAVED_ACCESS_TOKEN_KEY, "없음")}'");
                    Debug.Log($"  - 사용자명 키: '{EDITOR_PREFIX + SAVED_USERNAME_KEY}' = '{EditorPrefs.GetString(EDITOR_PREFIX + SAVED_USERNAME_KEY, "없음")}'");
                    Debug.Log($"  - 시간 키: '{EDITOR_PREFIX + SAVED_AT_KEY}' = '{EditorPrefs.GetString(EDITOR_PREFIX + SAVED_AT_KEY, "없음")}'");
#endif
                    return;
                }

                // 저장 시간 파싱
                if (long.TryParse(savedTimeStr, out long savedTimeBinary))
                {
                    System.DateTime savedTime = System.DateTime.FromBinary(savedTimeBinary);
                    System.TimeSpan elapsed = System.DateTime.Now - savedTime;

                    // 6시간 이내인지 확인
                    if (elapsed.TotalHours < SESSION_VALID_HOURS)
                    {
                        // 세션 복구
                        authToken = savedToken;
#if UNITY_EDITOR
                        refreshToken = EditorPrefs.GetString(EDITOR_PREFIX + SAVED_REFRESH_TOKEN_KEY, "");
                        userId = EditorPrefs.GetInt(EDITOR_PREFIX + SAVED_USER_ID_KEY, 0);
                        cachedId = EditorPrefs.GetString(EDITOR_PREFIX + SAVED_USERNAME_KEY, "");
                        displayName = EditorPrefs.GetString(EDITOR_PREFIX + SAVED_DISPLAY_NAME_KEY, "");
#else
                        refreshToken = PlayerPrefs.GetString(SAVED_REFRESH_TOKEN_KEY, "");
                        userId = PlayerPrefs.GetInt(SAVED_USER_ID_KEY, 0);
                        cachedId = PlayerPrefs.GetString(SAVED_USERNAME_KEY, "");
                        displayName = PlayerPrefs.GetString(SAVED_DISPLAY_NAME_KEY, "");
#endif
                        isLoggedIn = true;

                        // HttpApiClient에 토큰 설정
                        if (HttpApiClient.Instance != null)
                        {
                            HttpApiClient.Instance.SetAuthToken(authToken, userId);
                        }

                        if (debugMode)
                            Debug.Log($"[SessionManager] 세션 복구 성공: {cachedId} (남은 시간: {SESSION_VALID_HOURS - elapsed.TotalHours:F1}시간)");

                        // 자동 로그인 성공 이벤트 (UIManager가 초기화된 후 상태 확인하므로 알림 불필요)
                        OnLoginStateChanged?.Invoke(true);
                        OnUserDataReceived?.Invoke(cachedId, userId);

                        if (debugMode)
                            Debug.Log("[SessionManager] 자동 로그인 성공 - UIManager가 초기화된 후 상태를 확인할 예정");
                    }
                    else
                    {
                        if (debugMode)
                            Debug.Log($"[SessionManager] 세션 만료 ({elapsed.TotalHours:F1}시간 경과) - 데이터 삭제");
                        ClearSavedSession();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SessionManager] 세션 복구 실패: {ex.Message}");
                ClearSavedSession();
            }
        }

        /// <summary>
        /// 저장된 세션 데이터 삭제
        /// </summary>
        public void ClearSavedSession()
        {
#if UNITY_EDITOR
            EditorPrefs.DeleteKey(EDITOR_PREFIX + SAVED_ACCESS_TOKEN_KEY);
            EditorPrefs.DeleteKey(EDITOR_PREFIX + SAVED_REFRESH_TOKEN_KEY);
            EditorPrefs.DeleteKey(EDITOR_PREFIX + SAVED_USER_ID_KEY);
            EditorPrefs.DeleteKey(EDITOR_PREFIX + SAVED_USERNAME_KEY);
            EditorPrefs.DeleteKey(EDITOR_PREFIX + SAVED_DISPLAY_NAME_KEY);
            EditorPrefs.DeleteKey(EDITOR_PREFIX + SAVED_AT_KEY);
#else
            PlayerPrefs.DeleteKey(SAVED_ACCESS_TOKEN_KEY);
            PlayerPrefs.DeleteKey(SAVED_REFRESH_TOKEN_KEY);
            PlayerPrefs.DeleteKey(SAVED_USER_ID_KEY);
            PlayerPrefs.DeleteKey(SAVED_USERNAME_KEY);
            PlayerPrefs.DeleteKey(SAVED_DISPLAY_NAME_KEY);
            PlayerPrefs.DeleteKey(SAVED_AT_KEY);
            PlayerPrefs.Save();
#endif

            if (debugMode)
                Debug.Log("[SessionManager] 저장된 세션 데이터 삭제 완료");
        }

        /// <summary>
        /// UIManager에 자동 로그인 성공 알림
        /// </summary>
        private System.Collections.IEnumerator NotifyAutoLoginSuccess()
        {
            // UIManager 초기화 대기
            yield return new WaitForSeconds(0.5f);

            UIManager uiManager = UIManager.GetInstanceSafe();
            if (uiManager != null)
            {
                if (debugMode)
                    Debug.Log("[SessionManager] 자동 로그인 성공 - UIManager.OnLoginSuccess() 호출");
                
                uiManager.OnLoginSuccess();
            }
            else
            {
                Debug.LogWarning("[SessionManager] UIManager 없음 - 자동 로그인 UI 전환 실패");
            }
        }

        /// <summary>
        /// 수동 로그아웃 (ModeSelectPanel에서 호출)
        /// </summary>
        public void LogoutAndClearSession()
        {
            if (debugMode)
                Debug.Log("[SessionManager] 수동 로그아웃 시작");

            // HTTP API 로그아웃
            if (HttpApiClient.Instance != null && isLoggedIn)
            {
                HttpApiClient.Instance.Logout();
            }

            // SingleCore 캐시 정리 (UserDataCache, StageProgress 등)
            ClearSingleCoreCache();

            // 메모리 세션 클리어
            ClearSession();

            // 저장된 세션 데이터 삭제
            ClearSavedSession();

            // 로그인 상태 변경 이벤트
            OnLoginStateChanged?.Invoke(false);

            if (debugMode)
                Debug.Log("[SessionManager] 로그아웃 및 세션 삭제 완료");
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

        public void SetDisplayName(string dn)
        {
            displayName = dn ?? "";
            if (debugMode)
                Debug.Log($"[SessionManager] DisplayName set: '{displayName}'");
        }

        /// <summary>
        /// 로그인 HTTP 응답(AuthUserData)로부터 세션을 즉시 채움
        /// </summary>
        public void SeedFromAuth(HttpApiClient.AuthUserData auth)
        {
            if (auth?.user == null) return;

            // 🔥 수정: authToken 설정이 누락되어 있었음!
            authToken = auth.token ?? "";
            cachedId = auth.user.username ?? "";
            displayName = auth.user.display_name ?? "";
            userId = auth.user.user_id;
            isLoggedIn = true;

            if (debugMode)
            {
                Debug.Log($"[SessionManager] SeedFromAuth: username='{cachedId}', displayName='{displayName}', userId={userId}");
                Debug.Log($"[SessionManager] SeedFromAuth: authToken={(string.IsNullOrEmpty(authToken) ? "없음" : "설정됨")}");
            }

            // 성공적인 로그인시 세션 데이터 저장 (자동 로그인용)
            SaveSessionData();

            OnLoginStateChanged?.Invoke(true);
            OnUserDataReceived?.Invoke(cachedId, userId); // 기존 시그니처 유지
        }

        // ========================================
        // Unity Lifecycle
        // ========================================

        void Start()
        {
            // 🔥 수정: 메모리 세션만 초기화 (SecureStorage는 보존)
            ClearMemorySession();
            
            // 🔥 수정: 세션 자동 복구는 SceneFlowController에서 처리
            // CheckAndRestoreSession();
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

        /// <summary>
        /// SingleCore 매니저들의 캐시 데이터 정리
        /// </summary>
        private void ClearSingleCoreCache()
        {
            if (debugMode)
                Debug.Log("[SessionManager] SingleCore 캐시 정리 시작");

            try
            {
                // UserDataCache 정리
                if (Features.Single.Core.UserDataCache.Instance != null)
                {
                    Features.Single.Core.UserDataCache.Instance.LogoutUser();
                    Debug.Log("[SessionManager] UserDataCache.LogoutUser() 호출 완료");
                }

                // StageProgressManager 정리 (있다면)
                var stageProgressManager = FindObjectOfType<Features.Single.Core.StageProgressManager>();
                if (stageProgressManager != null)
                {
                    stageProgressManager.ClearCache();
                    Debug.Log("[SessionManager] StageProgressManager.ClearCache() 호출 완료");
                }

                // StageDataManager 정리 (있다면)
                var stageDataManager = FindObjectOfType<Features.Single.Core.StageDataManager>();
                if (stageDataManager != null)
                {
                    stageDataManager.ClearCache();
                    Debug.Log("[SessionManager] StageDataManager.ClearCache() 호출 완료");
                }

                if (debugMode)
                    Debug.Log("[SessionManager] SingleCore 캐시 정리 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SessionManager] SingleCore 캐시 정리 중 오류 발생: {ex.Message}");
            }
        }
    }
}
