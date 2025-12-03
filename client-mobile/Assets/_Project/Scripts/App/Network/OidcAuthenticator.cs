using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using App.Config;

namespace App.Network
{
    /// <summary>
    /// Unity 모바일 클라이언트용 OIDC 인증 클라이언트
    /// System Browser + Deep Link (blokus://auth/callback) 기반
    /// PKCE (Proof Key for Code Exchange) 지원
    /// </summary>
    public class OidcAuthenticator : MonoBehaviour
    {
        [Header("OIDC Configuration")]
        [SerializeField] private string clientId = "unity-mobile-client";
        [SerializeField] private string redirectUri = "blokus://auth/callback";
        [SerializeField] private string scope = "openid profile email";
        [SerializeField] private bool useProduction = false;
        
        [Header(" Debugging & Diagnostics")]
        [SerializeField] private bool showDetailedLogs = true;
        [SerializeField] private bool testDeepLinkOnStart = false;
        
        [Header("Development Options")]
        public bool useHttpCallbackForTesting = true; //  Editor에서 테스트용 - 기본 활성화
        [SerializeField] private bool useUnityEditorAPI = true; // Unity Editor API 사용 (배포 서버와 직접 연결)
        [SerializeField] private bool enableManualCodeInput = true; // 에디터에서 수동 코드 입력
        
        [Header("Development Settings")]
        [SerializeField] private bool enableDebugLogs = true;

        // OIDC Discovery Document
        private OidcDiscoveryDocument _discoveryDocument;
        private bool _isDiscoveryLoaded = false;
        
        // PKCE Parameters
        private string _codeVerifier;
        private string _codeChallenge;
        private string _state;
        
        // Deep Link 처리 변수들
        private string _receivedAuthCode;
        private string _receivedError;
        private bool _deepLinkHandlerRegistered = false;
        
        // Authentication State
        private bool _isAuthenticating = false;
        private Action<bool, string, TokenResponse> _authCallback;
        
        // Deep Link Handling
        private bool _isListeningForDeepLink = false;
        private Coroutine _deepLinkTimeoutCoroutine;
        
        // Constants
        private const float DEEP_LINK_TIMEOUT = 300f; // 5 minutes
        private const string UNITY_DEEP_LINK_URL = "blokus://auth/callback";

        #region Events
        public static event Action<bool, string, TokenResponse> OnAuthenticationComplete;
        public static event Action<string> OnAuthenticationError;
        #endregion

        #region Data Classes
        [Serializable]
        public class OidcDiscoveryDocument
        {
            public string authorization_endpoint;
            public string token_endpoint;
            public string jwks_uri;
            public string issuer;
            public string[] response_types_supported;
            public string[] subject_types_supported;
            public string[] id_token_signing_alg_values_supported;
        }

        [Serializable]
        public class TokenResponse
        {
            public string access_token;
            public string refresh_token;
            public string id_token;
            public string token_type;
            public int expires_in;
            public string scope;
        }

        [Serializable]
        public class ErrorResponse
        {
            public string error;
            public string error_description;
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Singleton pattern
            if (FindObjectsOfType<OidcAuthenticator>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            
            DontDestroyOnLoad(gameObject);
            
            LogDebug("OidcAuthenticator initialized");
        }

        private void Start()
        {
            //  원격 로깅 시스템 초기화 (릴리즈 빌드용)
            RemoteLogger.Initialize(this);
            RemoteLogger.LogInfo($" OidcAuthenticator 시작 - Platform: {Application.platform}, BuildType: {(Debug.isDebugBuild ? "Debug" : "Release")}", "OIDC");
            
            //  SecureStorage 초기화 및 토큰 마이그레이션
            InitializeSecureStorage();
            
            //  시스템 진단 정보 출력
            LogDebug($"Unity 버전: {Application.unityVersion}");
            LogDebug($"플랫폼: {Application.platform}");
            LogDebug($"개발 빌드: {Debug.isDebugBuild}");
            LogDebug($"에디터 모드: {Application.isEditor}");
            
            // 배포 빌드 방식: Deep Link 사용
            if (Application.isEditor)
            {
                LogDebug($" Unity Editor: Deep Link URI 사용 ({redirectUri})");
            }
            else
            {
                LogDebug($"📱 모바일 빌드: Deep Link URI 사용 ({redirectUri})");
            }
            
            // 환경별 OIDC 서버 URL 설정
            var oidcServerUrl = EnvironmentConfig.OidcServerUrl;
            LogDebug($"OIDC 서버 URL: {oidcServerUrl}");
            
            if (!EnvironmentConfig.IsDevelopment)
            {
                enableDebugLogs = false;
            }
            
            // Deep Link 스키마 테스트 (개발용)
            if (testDeepLinkOnStart && Application.isEditor)
            {
                LogDebug($" Deep Link 테스트: {redirectUri}");
                StartCoroutine(TestDeepLinkSupport());
            }
            
            //  Editor용 HTTP 콜백 서버 시작 (배포 빌드 방식 사용 시에는 불필요)
            if (Application.isEditor && useHttpCallbackForTesting)
            {
                LogDebug("⚠️ HTTP 콜백 서버 시작 건너뜀 - 배포 빌드 방식 사용 중");
                // StartHttpCallbackServer(); // 배포 빌드 방식에서는 불필요
            }
            
            // Load OIDC Discovery Document on startup
            StartCoroutine(LoadDiscoveryDocument());
            
            // Register for deep link events
            Application.deepLinkActivated += OnDeepLinkActivated;
            LogDebug("Deep Link 이벤트 리스너 등록 완료");
            
            // Check if app was opened with deep link
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                LogDebug($"앱 시작 시 Deep Link 감지: {Application.absoluteURL}");
                OnDeepLinkActivated(Application.absoluteURL);
            }
        }

        private void OnDestroy()
        {
            LogDebug("OidcAuthenticator OnDestroy 호출됨");
            
            Application.deepLinkActivated -= OnDeepLinkActivated;
            
            if (_deepLinkTimeoutCoroutine != null)
            {
                StopCoroutine(_deepLinkTimeoutCoroutine);
            }
            
            // OAuth 인증 중이면 서버를 즉시 종료하지 않음
            if (_isAuthenticating)
            {
                LogDebug("⚠️ OAuth 인증 중이므로 HTTP 서버 종료를 연기합니다");
                // 인증 완료 후에 서버가 종료되도록 함
            }
            else
            {
                LogDebug("🛑 OAuth 인증 중이 아니므로 HTTP 서버를 종료합니다");
                StopHttpCallbackServer();
            }
        }
        
        /// <summary>
        /// HTTP 콜백 서버 정지
        /// </summary>
        private void StopHttpCallbackServer()
        {
            if (_isHttpListening && _httpListener != null)
            {
                try
                {
                    LogDebug("🛑 HTTP 콜백 서버 정지 시작");
                    _isHttpListening = false;
                    
                    // HttpListener 정지
                    _httpListener.Stop();
                    _httpListener.Close();
                    
                    // 백그라운드 스레드 정리
                    if (_httpListenerThread != null && _httpListenerThread.IsAlive)
                    {
                        LogDebug("🧵 백그라운드 스레드 종료 대기 중...");
                        if (!_httpListenerThread.Join(5000)) // 5초 대기
                        {
                            LogDebug("⚠️ 백그라운드 스레드가 5초 내에 종료되지 않음");
                            _httpListenerThread.Abort(); // 강제 종료 (deprecated이지만 안전장치)
                        }
                        _httpListenerThread = null;
                    }
                    
                    LogDebug(" HTTP 콜백 서버 정지 완료");
                }
                catch (System.Exception ex)
                {
                    LogDebug($" HTTP 서버 정지 중 오류: {ex.Message}");
                }
                finally
                {
                    _httpListener = null;
                    _httpListenerThread = null;
                }
            }
        }
        #endregion

        #region Public Authentication Methods
        /// <summary>
        /// Start OIDC authentication flow
        /// </summary>
        /// <param name="callback">Callback with success, message, and token response</param>
        public void StartAuthentication(Action<bool, string, TokenResponse> callback)
        {
            if (_isAuthenticating)
            {
                LogDebug("Authentication already in progress");
                callback?.Invoke(false, "인증이 이미 진행 중입니다.", null);
                return;
            }

            if (!_isDiscoveryLoaded || _discoveryDocument == null)
            {
                LogDebug("Discovery document not loaded");
                callback?.Invoke(false, "OIDC 설정을 불러오는 중입니다. 잠시 후 다시 시도해주세요.", null);
                return;
            }

            _authCallback = callback;
            _isAuthenticating = true;

            //  Unity Editor에서 배포 서버의 기존 Google OAuth 직접 사용
            // 모든 환경에서 동일한 배포 빌드 방식 사용
            LogDebug(" 배포 빌드 방식 Google OAuth 사용");
            StartCoroutine(StartProductionGoogleOAuth(callback));
            return;

            //  HTTP 콜백 서버 상태 확인 (기존 localhost 방식)
            if (Application.isEditor && useHttpCallbackForTesting && !_isHttpListening)
            {
                LogDebug(" HTTP 콜백 서버가 꺼져있어서 다시 시작합니다");
                StartHttpCallbackServer();
                
                // 서버 시작 대기
                StartCoroutine(WaitForServerAndStartAuth(callback));
                return;
            }

            // Generate PKCE parameters
            GeneratePkceParameters();
            
            // Build authorization URL
            string authUrl = BuildAuthorizationUrl();
            LogDebug($"Opening authorization URL: {authUrl}");
            
            //  브라우저 열기 시도 및 에러 처리 강화
            try
            {
                // Open system browser
                Application.OpenURL(authUrl);
                LogDebug(" 브라우저 열기 성공");
                
                // Start listening for deep link
                StartDeepLinkListener();
            }
            catch (System.Exception ex)
            {
                LogDebug($" 브라우저 열기 실패: {ex.Message}");
                CompleteAuthentication(false, $"브라우저를 열 수 없습니다: {ex.Message}", null);
                return;
            }
            
            //  추가 진단: 플랫폼별 브라우저 지원 확인
            #if UNITY_WEBGL
            LogDebug("⚠️ WebGL: 브라우저 새 창 열기가 제한될 수 있음");
            #elif UNITY_ANDROID
            LogDebug("📱 Android: 기본 브라우저로 리디렉트");
            #elif UNITY_IOS  
            LogDebug("📱 iOS: Safari로 리디렉트");
            #else
            LogDebug($"🖥️ 플랫폼 {Application.platform}: 시스템 기본 브라우저 사용");
            #endif
        }
        
        /// <summary>
        /// 서버 시작 후 인증 재시도
        /// </summary>
        private IEnumerator WaitForServerAndStartAuth(Action<bool, string, TokenResponse> callback)
        {
            // 최대 3초 대기
            float timeout = 3f;
            float elapsed = 0f;
            
            while (!_isHttpListening && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (_isHttpListening)
            {
                LogDebug(" HTTP 서버 재시작 완료, 인증 계속 진행");
                
                // 인증 다시 시작 (재귀 호출 방지를 위해 상태 리셋)
                _isAuthenticating = false;
                StartAuthentication(callback);
            }
            else
            {
                LogDebug(" HTTP 서버 시작 실패");
                CompleteAuthentication(false, "HTTP 콜백 서버를 시작할 수 없습니다", null);
            }
        }

        /// <summary>
        /// Check if authentication is in progress
        /// </summary>
        public bool IsAuthenticating()
        {
            return _isAuthenticating;
        }

        /// <summary>
        /// Start listening for callback (for manual auth flows)
        /// </summary>
        public void StartListeningForCallback()
        {
            if (!_isListeningForDeepLink)
            {
                StartDeepLinkListener();
            }
        }

        /// <summary>
        /// Cancel ongoing authentication
        /// </summary>
        public void CancelAuthentication()
        {
            if (!_isAuthenticating)
                return;
                
            LogDebug("Authentication cancelled by user");
            StopDeepLinkListener();
            CompleteAuthentication(false, "인증이 취소되었습니다.", null);
        }
        
        /// <summary>
        /// Check if authenticator is ready
        /// </summary>
        public bool IsReady()
        {
            return _isDiscoveryLoaded && _discoveryDocument != null;
        }
        #endregion

        #region OIDC Discovery
        private IEnumerator LoadDiscoveryDocument()
        {
            var currentOidcUrl = EnvironmentConfig.OidcServerUrl;
            string discoveryUrl = $"{currentOidcUrl}/.well-known/openid-configuration";
            LogDebug($"Loading OIDC discovery document from: {discoveryUrl}");
            RemoteLogger.LogInfo($" OIDC Discovery 요청 시작: {discoveryUrl}", "OIDC");

            //  Unity 2021.3+ 방식으로 변경
            using (UnityWebRequest request = new UnityWebRequest(discoveryUrl, "GET"))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.certificateHandler = new BypassCertificate();
                request.timeout = 10;
                
                // 명시적 헤더 설정
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("User-Agent", "Unity-Mobile-Client/1.0");
                
                LogDebug($" Sending request to: {discoveryUrl}");
                RemoteLogger.LogInfo($" UnityWebRequest 전송: {discoveryUrl} (timeout: 10초)", "OIDC");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMsg = $"OIDC Discovery 실패: {request.error} (Response Code: {request.responseCode})";
                    LogDebug($"Failed to load discovery document: {request.error}");
                    RemoteLogger.LogError($" {errorMsg}", "OIDC");
                    RemoteLogger.LogError($" RequestResult: {request.result}, ResponseCode: {request.responseCode}", "OIDC");
                    _isDiscoveryLoaded = false;
                    yield break;
                }

                try
                {
                    string responseText = request.downloadHandler.text;
                    RemoteLogger.LogInfo($" OIDC Discovery 응답 수신 (길이: {responseText.Length})", "OIDC");
                    
                    _discoveryDocument = JsonConvert.DeserializeObject<OidcDiscoveryDocument>(responseText);
                    _isDiscoveryLoaded = true;
                    LogDebug("OIDC discovery document loaded successfully");
                    RemoteLogger.LogInfo($" OIDC Discovery 문서 파싱 성공", "OIDC");
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to parse discovery document: {ex.Message}");
                    RemoteLogger.LogError($" OIDC Discovery 파싱 실패: {ex.Message}", "OIDC");
                    _isDiscoveryLoaded = false;
                }
            }
        }
        #endregion

        #region PKCE Generation
        private void GeneratePkceParameters()
        {
            // Generate random code verifier
            byte[] codeVerifierBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(codeVerifierBytes);
            }
            _codeVerifier = Base64UrlEncode(codeVerifierBytes);

            // Generate code challenge (SHA256 hash of verifier)
            byte[] challengeBytes;
            using (var sha256 = SHA256.Create())
            {
                challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(_codeVerifier));
            }
            _codeChallenge = Base64UrlEncode(challengeBytes);

            // Generate random state
            byte[] stateBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(stateBytes);
            }
            _state = Base64UrlEncode(stateBytes);

            LogDebug($"Generated PKCE parameters - Challenge: {_codeChallenge.Substring(0, 10)}..., State: {_state}");
        }

        private string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
        #endregion

        #region Authorization URL
        private string BuildAuthorizationUrl()
        {
            var parameters = new Dictionary<string, string>
            {
                ["response_type"] = "code",
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["scope"] = scope,
                ["state"] = _state,
                ["code_challenge"] = _codeChallenge,
                ["code_challenge_method"] = "S256"
            };

            var queryParams = new List<string>();
            foreach (var kvp in parameters)
            {
                queryParams.Add($"{UnityWebRequest.EscapeURL(kvp.Key)}={UnityWebRequest.EscapeURL(kvp.Value)}");
            }
            var queryString = string.Join("&", queryParams.ToArray());

            return $"{_discoveryDocument.authorization_endpoint}?{queryString}";
        }
        #endregion

        #region Deep Link Handling
        private void StartDeepLinkListener()
        {
            _isListeningForDeepLink = true;
            _deepLinkTimeoutCoroutine = StartCoroutine(DeepLinkTimeout());
            LogDebug("Started listening for deep link callback");
        }

        private void StopDeepLinkListener()
        {
            _isListeningForDeepLink = false;
            
            if (_deepLinkTimeoutCoroutine != null)
            {
                StopCoroutine(_deepLinkTimeoutCoroutine);
                _deepLinkTimeoutCoroutine = null;
            }
            
            LogDebug("Stopped listening for deep link callback");
        }

        private IEnumerator DeepLinkTimeout()
        {
            yield return new WaitForSeconds(DEEP_LINK_TIMEOUT);
            
            if (_isListeningForDeepLink)
            {
                LogDebug("Deep link timeout reached");
                CompleteAuthentication(false, "인증 시간이 초과되었습니다. 다시 시도해주세요.", null);
            }
        }

        private void OnDeepLinkActivated(string url)
        {
            LogDebug($"Deep link activated: {url}");

            // HTTP callback에서 변환된 Deep Link도 허용
            string originalDeepLinkUri = "blokus://auth/callback";
            bool isValidUri = url.StartsWith(redirectUri) || url.StartsWith(originalDeepLinkUri);
            
            if (!_isListeningForDeepLink || !isValidUri)
            {
                LogDebug($"Ignoring deep link - Listening: {_isListeningForDeepLink}, Valid URI: {isValidUri}");
                LogDebug($"Expected: {redirectUri} or {originalDeepLinkUri}");
                LogDebug($"Received: {url}");
                return;
            }

            StopDeepLinkListener();

            // Parse callback URL
            if (TryParseCallback(url, out string code, out string state, out string error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    CompleteAuthentication(false, $"인증 오류: {error}", null);
                    return;
                }

                if (state != _state)
                {
                    CompleteAuthentication(false, "인증 상태가 일치하지 않습니다. 보안상의 이유로 인증이 중단됩니다.", null);
                    return;
                }

                if (string.IsNullOrEmpty(code))
                {
                    CompleteAuthentication(false, "인증 코드를 받지 못했습니다.", null);
                    return;
                }

                // Exchange code for tokens
                StartCoroutine(ExchangeCodeForTokens(code));
            }
            else
            {
                CompleteAuthentication(false, "인증 응답을 처리할 수 없습니다.", null);
            }
        }

        private bool TryParseCallback(string url, out string code, out string state, out string error)
        {
            code = null;
            state = null;
            error = null;

            try
            {
                // Parse URL parameters
                var uri = new Uri(url);
                var query = uri.Query;
                
                if (string.IsNullOrEmpty(query))
                    return false;

                var parameters = new Dictionary<string, string>();
                foreach (string param in query.Substring(1).Split('&'))
                {
                    var parts = param.Split('=');
                    if (parts.Length == 2)
                    {
                        parameters[UnityWebRequest.UnEscapeURL(parts[0])] = UnityWebRequest.UnEscapeURL(parts[1]);
                    }
                }

                parameters.TryGetValue("code", out code);
                parameters.TryGetValue("state", out state);
                parameters.TryGetValue("error", out error);

                return true;
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to parse callback URL: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Token Exchange
        private IEnumerator ExchangeCodeForTokens(string code)
        {
            LogDebug($"Exchanging code for tokens: {code.Substring(0, 10)}...");

            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = _codeVerifier
            };

            var form = new WWWForm();
            foreach (var kvp in formData)
            {
                form.AddField(kvp.Key, kvp.Value);
            }

            using (UnityWebRequest request = UnityWebRequest.Post(_discoveryDocument.token_endpoint, form))
            {
                request.certificateHandler = new BypassCertificate();
                request.timeout = 15;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMsg = $"토큰 교환 실패: {request.error}";
                    LogDebug(errorMsg);
                    CompleteAuthentication(false, errorMsg, null);
                    yield break;
                }

                try
                {
                    string responseText = request.downloadHandler.text;
                    LogDebug($"Token response received: {responseText.Length} characters");

                    // Try to parse as error first
                    if (responseText.Contains("error"))
                    {
                        var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(responseText);
                        string errorMsg = $"토큰 교환 오류: {errorResponse.error_description ?? errorResponse.error}";
                        CompleteAuthentication(false, errorMsg, null);
                        yield break;
                    }

                    // Parse successful token response
                    var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseText);
                    
                    if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.access_token))
                    {
                        LogDebug("Token exchange successful");
                        CompleteAuthentication(true, "인증 성공", tokenResponse);
                    }
                    else
                    {
                        CompleteAuthentication(false, "토큰 응답이 올바르지 않습니다.", null);
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to parse token response: {ex.Message}");
                    CompleteAuthentication(false, "토큰 응답을 처리할 수 없습니다.", null);
                }
            }
        }
        #endregion

        #region Authentication Completion
        private void CompleteAuthentication(bool success, string message, TokenResponse tokenResponse)
        {
            _isAuthenticating = false;
            
            LogDebug($"Authentication completed - Success: {success}, Message: {message}");
            
            // Clear PKCE parameters
            _codeVerifier = null;
            _codeChallenge = null;
            _state = null;
            
            // Invoke callbacks
            try
            {
                _authCallback?.Invoke(success, message, tokenResponse);
                OnAuthenticationComplete?.Invoke(success, message, tokenResponse);
                
                if (!success)
                {
                    OnAuthenticationError?.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error in authentication callback: {ex.Message}");
            }
            finally
            {
                _authCallback = null;
                
                //  인증 완료 후 HTTP 서버 정리 (에디터에서만)
                if (Application.isEditor && useHttpCallbackForTesting)
                {
                    LogDebug("🧹 인증 완료, HTTP 콜백 서버 정리 예약 (5초 후)");
                    StartCoroutine(DelayedServerCleanup());
                }
            }
        }
        
        /// <summary>
        /// 지연된 서버 정리 (다른 인증 시도를 방해하지 않도록)
        /// </summary>
        private IEnumerator DelayedServerCleanup()
        {
            yield return new WaitForSeconds(5f);
            
            // 다른 인증이 진행 중이 아닐 때만 서버 정리
            if (!_isAuthenticating)
            {
                LogDebug("🧹 HTTP 콜백 서버 정리 실행");
                StopHttpCallbackServer();
            }
            else
            {
                LogDebug("⚠️ 다른 인증이 진행 중이므로 서버 정리 연기");
            }
        }
        #endregion

        #region Secure Storage (Using SecureStorage with Keychain/Keystore support)
        private const string PREF_ACCESS_TOKEN = App.Security.TokenKeys.Access;
        private const string PREF_REFRESH_TOKEN = App.Security.TokenKeys.Refresh;
        private const string PREF_TOKEN_EXPIRY = App.Security.TokenKeys.Expiry;

        public void SaveTokens(TokenResponse tokenResponse)
        {
            if (tokenResponse == null)
                return;

            try
            {
                // Migrate existing PlayerPrefs data first
                MigratePlayerPrefsToSecureStorage();

                // Use dual storage (primary + backup) for critical tokens
                // This protects against Android Keystore key loss during device reboot, OS updates, etc.
                App.Security.SecureStorage.StoreStringWithBackup(PREF_ACCESS_TOKEN, tokenResponse.access_token ?? "");
                App.Security.SecureStorage.StoreStringWithBackup(PREF_REFRESH_TOKEN, tokenResponse.refresh_token ?? "");

                // Calculate expiry time (AccessToken expiry - not RefreshToken)
                var expiryTime = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 60); // 1 minute buffer
                App.Security.SecureStorage.StoreString(PREF_TOKEN_EXPIRY, expiryTime.ToBinary().ToString());

                LogDebug("Tokens saved to SecureStorage with backup successfully");
                LogDebug($"SecureStorage Platform: {App.Security.SecureStorage.GetPlatformInfo()}");
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to save tokens to SecureStorage: {ex.Message}");
                throw; // Re-throw to indicate save failure
            }
        }

        public string GetAccessToken()
        {
            try
            {
                // Try SecureStorage first
                string token = App.Security.SecureStorage.GetString(PREF_ACCESS_TOKEN, "");
                
                // If SecureStorage is empty, try migrating from PlayerPrefs
                if (string.IsNullOrEmpty(token))
                {
                    MigratePlayerPrefsToSecureStorage();
                    token = App.Security.SecureStorage.GetString(PREF_ACCESS_TOKEN, "");
                }
                
                if (string.IsNullOrEmpty(token))
                    return null;

                // Check if token is expired
                string expiryString = App.Security.SecureStorage.GetString(PREF_TOKEN_EXPIRY, "");
                if (!string.IsNullOrEmpty(expiryString) && long.TryParse(expiryString, out long expiry))
                {
                    var expiryTime = DateTime.FromBinary(expiry);
                    if (DateTime.UtcNow >= expiryTime)
                    {
                        LogDebug("Access token expired");
                        return null;
                    }
                }

                return token;
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to get access token from SecureStorage: {ex.Message}");
                return null;
            }
        }

        public string GetRefreshToken()
        {
            try
            {
                // Try SecureStorage with backup recovery
                string token = App.Security.SecureStorage.GetStringWithBackup(PREF_REFRESH_TOKEN, "");

                // If backup recovery also failed, try migrating from old PlayerPrefs
                if (string.IsNullOrEmpty(token))
                {
                    MigratePlayerPrefsToSecureStorage();
                    token = App.Security.SecureStorage.GetStringWithBackup(PREF_REFRESH_TOKEN, "");
                }

                return token;
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to get refresh token from SecureStorage: {ex.Message}");
                return "";
            }
        }

        public void ClearTokens()
        {
            try
            {
                // Clear from both primary and backup storage
                App.Security.SecureStorage.DeleteKeyWithBackup(PREF_ACCESS_TOKEN);
                App.Security.SecureStorage.DeleteKeyWithBackup(PREF_REFRESH_TOKEN);
                App.Security.SecureStorage.DeleteKey(PREF_TOKEN_EXPIRY);
                LogDebug("Tokens cleared from SecureStorage and backup");
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to clear tokens from SecureStorage: {ex.Message}");
            }

            // Also clear from PlayerPrefs during migration cleanup
            try
            {
                PlayerPrefs.DeleteKey(PREF_ACCESS_TOKEN);
                PlayerPrefs.DeleteKey(PREF_REFRESH_TOKEN);
                PlayerPrefs.DeleteKey(PREF_TOKEN_EXPIRY);
                PlayerPrefs.Save();
                LogDebug("Legacy PlayerPrefs tokens also cleared");
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to clear legacy PlayerPrefs tokens: {ex.Message}");
            }
        }

        public bool HasValidTokens()
        {
            return !string.IsNullOrEmpty(GetAccessToken());
        }

        /// <summary>
        /// Migrates existing PlayerPrefs token data to SecureStorage
        /// This ensures backward compatibility for users upgrading from PlayerPrefs
        /// </summary>
        private void MigratePlayerPrefsToSecureStorage()
        {
            try
            {
                // Check if migration is needed (PlayerPrefs has data but SecureStorage doesn't)
                bool hasPlayerPrefsTokens = PlayerPrefs.HasKey(PREF_REFRESH_TOKEN) || PlayerPrefs.HasKey(PREF_ACCESS_TOKEN);
                bool hasSecureStorageTokens = App.Security.SecureStorage.HasKey(PREF_REFRESH_TOKEN) || App.Security.SecureStorage.HasKey(PREF_ACCESS_TOKEN);
                
                if (!hasPlayerPrefsTokens || hasSecureStorageTokens)
                {
                    // No migration needed
                    return;
                }
                
                LogDebug("Starting token migration from PlayerPrefs to SecureStorage...");
                
                // Migrate refresh token
                string refreshToken = PlayerPrefs.GetString(PREF_REFRESH_TOKEN, "");
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    App.Security.SecureStorage.StoreString(PREF_REFRESH_TOKEN, refreshToken);
                    LogDebug("Refresh token migrated to SecureStorage");
                }
                
                // Migrate access token
                string accessToken = PlayerPrefs.GetString(PREF_ACCESS_TOKEN, "");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    App.Security.SecureStorage.StoreString(PREF_ACCESS_TOKEN, accessToken);
                    LogDebug("Access token migrated to SecureStorage");
                }
                
                // Migrate expiry time
                string expiryTime = PlayerPrefs.GetString(PREF_TOKEN_EXPIRY, "");
                if (!string.IsNullOrEmpty(expiryTime))
                {
                    App.Security.SecureStorage.StoreString(PREF_TOKEN_EXPIRY, expiryTime);
                    LogDebug("Token expiry time migrated to SecureStorage");
                }
                
                // Clear PlayerPrefs after successful migration
                PlayerPrefs.DeleteKey(PREF_REFRESH_TOKEN);
                PlayerPrefs.DeleteKey(PREF_ACCESS_TOKEN);
                PlayerPrefs.DeleteKey(PREF_TOKEN_EXPIRY);
                PlayerPrefs.Save();
                
                LogDebug("Token migration completed successfully. PlayerPrefs cleaned up.");
            }
            catch (Exception ex)
            {
                LogDebug($"Token migration failed: {ex.Message}. Will continue with existing data.");
            }
        }

        /// <summary>
        /// Check SecureStorage availability and log platform info for debugging
        /// </summary>
        public bool IsSecureStorageAvailable()
        {
            try
            {
                bool available = App.Security.SecureStorage.IsAvailable();
                LogDebug($"SecureStorage available: {available}");
                LogDebug($"SecureStorage platform: {App.Security.SecureStorage.GetPlatformInfo()}");
                return available;
            }
            catch (Exception ex)
            {
                LogDebug($"SecureStorage availability check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initialize SecureStorage and perform token migration if needed
        /// Called during Start() to ensure proper setup
        /// </summary>
        private void InitializeSecureStorage()
        {
            try
            {
                LogDebug(" Initializing SecureStorage...");
                
                // Check SecureStorage availability
                if (IsSecureStorageAvailable())
                {
                    LogDebug(" SecureStorage is available");
                    
                    // Perform token migration if needed
                    MigratePlayerPrefsToSecureStorage();
                    
                    // Log current storage status
                    LogTokenStorageStatus();
                }
                else
                {
                    LogDebug("⚠️ SecureStorage is not available, will use fallback mechanisms");
                }
            }
            catch (Exception ex)
            {
                LogDebug($" SecureStorage initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Debug method to check token storage status
        /// </summary>
        public void LogTokenStorageStatus()
        {
            try
            {
                LogDebug("=== Token Storage Status ===");
                LogDebug($"SecureStorage available: {App.Security.SecureStorage.IsAvailable()}");
                LogDebug($"SecureStorage platform: {App.Security.SecureStorage.GetPlatformInfo()}");
                LogDebug($"SecureStorage has refresh token: {App.Security.SecureStorage.HasKey(PREF_REFRESH_TOKEN)}");
                LogDebug($"SecureStorage has access token: {App.Security.SecureStorage.HasKey(PREF_ACCESS_TOKEN)}");
                LogDebug($"SecureStorage has expiry: {App.Security.SecureStorage.HasKey(PREF_TOKEN_EXPIRY)}");
                
                // Legacy PlayerPrefs status (for migration detection only)
                bool hasLegacyTokens = PlayerPrefs.HasKey(PREF_REFRESH_TOKEN) || PlayerPrefs.HasKey(PREF_ACCESS_TOKEN);
                if (hasLegacyTokens)
                {
                    LogDebug("⚠️ Legacy PlayerPrefs tokens detected - will be migrated");
                    LogDebug($"Legacy PlayerPrefs has refresh token: {PlayerPrefs.HasKey(PREF_REFRESH_TOKEN)}");
                    LogDebug($"Legacy PlayerPrefs has access token: {PlayerPrefs.HasKey(PREF_ACCESS_TOKEN)}");
                }
                else
                {
                    LogDebug(" No legacy PlayerPrefs tokens found");
                }
                LogDebug("=== End Token Storage Status ===");
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to log token storage status: {ex.Message}");
            }
        }
        #endregion

        #region Logging
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[OidcAuth] {message}");
            }
        }
        #endregion

        #region  HTTP Callback Server for Editor
        private System.Net.HttpListener _httpListener;
        private bool _isHttpListening = false;
        private System.Threading.Thread _httpListenerThread;
        private readonly System.Collections.Concurrent.ConcurrentQueue<System.Action> _mainThreadActions = new System.Collections.Concurrent.ConcurrentQueue<System.Action>();
        private int _requestCount = 0;
        private string _serverStatus = "Stopped";
        private System.DateTime _lastRequestTime = System.DateTime.MinValue;
        
        /// <summary>
        /// Editor용 HTTP 콜백 서버 시작
        /// </summary>
        private void StartHttpCallbackServer()
        {
            if (!Application.isEditor)
            {
                LogDebug("⏭️ HTTP 콜백 서버: Editor 모드가 아니므로 시작하지 않음");
                return;
            }
                
            if (_isHttpListening)
            {
                LogDebug("⏭️ HTTP 콜백 서버: 이미 실행 중");
                return;
            }
                
            try
            {
                _httpListener = new System.Net.HttpListener();
                _httpListener.Prefixes.Add("http://localhost:7777/");
                _httpListener.Start();
                _isHttpListening = true;
                _serverStatus = "Running";
                _requestCount = 0;
                
                LogDebug(" HTTP 콜백 서버 성공적으로 시작: http://localhost:7777/");
                LogDebug(" OAuth 콜백을 대기 중...");
                LogDebug(" 테스트 URL: http://localhost:7777/health");
                LogDebug(" 콜백 URL: http://localhost:7777/auth/callback");
                
                // 백그라운드 스레드에서 요청 처리
                _httpListenerThread = new System.Threading.Thread(HandleHttpRequestsOnBackgroundThread)
                {
                    IsBackground = true,
                    Name = "OidcHttpListener"
                };
                _httpListenerThread.Start();
                
                // 메인 스레드 액션 처리를 위한 코루틴 시작
                StartCoroutine(ProcessMainThreadActions());
                
                //  서버 상태 모니터링 코루틴 시작
                StartCoroutine(MonitorServerStatus());
            }
            catch (System.Exception ex)
            {
                LogDebug($" HTTP 콜백 서버 시작 실패: {ex.Message}");
                LogDebug(" 포트 7777이 이미 사용 중일 수 있습니다. Unity를 재시작해보세요.");
                _isHttpListening = false;
            }
        }
        
        /// <summary>
        /// 백그라운드 스레드에서 HTTP 요청 처리
        /// </summary>
        private void HandleHttpRequestsOnBackgroundThread()
        {
            LogDebug("🧵 HTTP 콜백 서버 백그라운드 스레드 시작");
            LogDebug($"🧵 스레드 ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            LogDebug($"🧵 스레드 이름: {System.Threading.Thread.CurrentThread.Name}");
            
            int consecutiveErrors = 0;
            const int maxConsecutiveErrors = 5;
            
            while (_isHttpListening && _httpListener != null && consecutiveErrors < maxConsecutiveErrors)
            {
                try
                {
                    _serverStatus = "Listening";
                    LogDebug(" HTTP 요청 대기 중... (GetContext 호출)");
                    
                    // GetContext()는 동기 호출로 요청을 대기 - 여기서 블로킹됨
                    var context = _httpListener.GetContext();
                    
                    _requestCount++;
                    _lastRequestTime = System.DateTime.Now;
                    consecutiveErrors = 0; // 성공하면 에러 카운터 리셋
                    
                    LogDebug($"📨 HTTP 요청 수신됨! (총 {_requestCount}번째)");
                    LogDebug($"📨 요청 URL: {context.Request.Url}");
                    LogDebug($"📨 요청 시각: {_lastRequestTime:HH:mm:ss.fff}");
                    
                    // 메인 스레드에서 콜백 처리하도록 큐에 추가
                    _mainThreadActions.Enqueue(() => ProcessHttpCallback(context));
                }
                catch (System.Net.HttpListenerException ex) when (!_isHttpListening)
                {
                    // 정상 종료 시에는 로그 생략
                    LogDebug("🛑 HTTP 콜백 서버 정상 종료 (HttpListenerException)");
                    break;
                }
                catch (System.ObjectDisposedException ex) when (!_isHttpListening)
                {
                    // 정상 종료 시에는 로그 생략
                    LogDebug("🛑 HTTP 콜백 서버 정상 종료 (ObjectDisposed)");
                    break;
                }
                catch (System.Exception ex)
                {
                    consecutiveErrors++;
                    _serverStatus = $"Error ({consecutiveErrors}/{maxConsecutiveErrors})";
                    
                    if (_isHttpListening)
                    {
                        LogDebug($" HTTP 콜백 처리 오류 ({consecutiveErrors}/{maxConsecutiveErrors}): {ex.Message}");
                        LogDebug($" 오류 타입: {ex.GetType().Name}");
                        LogDebug($" 스택 트레이스: {ex.StackTrace}");
                        
                        if (consecutiveErrors >= maxConsecutiveErrors)
                        {
                            LogDebug($" 연속 오류가 {maxConsecutiveErrors}회 발생하여 서버를 중단합니다");
                            _serverStatus = "Failed";
                            break;
                        }
                    }
                    
                    // 오류 발생 시 잠시 대기 후 재시도
                    System.Threading.Thread.Sleep(1000);
                }
            }
            
            _serverStatus = "Stopped";
            LogDebug("🏁 HTTP 콜백 서버 백그라운드 스레드 종료");
        }
        
        /// <summary>
        /// 메인 스레드에서 액션 처리하는 코루틴
        /// </summary>
        private IEnumerator ProcessMainThreadActions()
        {
            LogDebug("🧵 메인 스레드 액션 처리 코루틴 시작");
            
            while (_isHttpListening)
            {
                int processedActions = 0;
                while (_mainThreadActions.TryDequeue(out var action))
                {
                    try
                    {
                        processedActions++;
                        LogDebug($" 메인 스레드에서 HTTP 액션 처리 중... ({processedActions})");
                        action.Invoke();
                        LogDebug($" HTTP 액션 처리 완료 ({processedActions})");
                    }
                    catch (System.Exception ex)
                    {
                        LogDebug($" 메인 스레드 액션 처리 오류: {ex.Message}");
                        LogDebug($" 스택 트레이스: {ex.StackTrace}");
                    }
                }
                
                yield return null; // 한 프레임 대기
            }
            
            LogDebug("🧵 메인 스레드 액션 처리 코루틴 종료");
        }
        
        /// <summary>
        /// 서버 상태 모니터링 코루틴
        /// </summary>
        private IEnumerator MonitorServerStatus()
        {
            LogDebug(" HTTP 서버 상태 모니터링 시작");
            
            while (_isHttpListening)
            {
                // 5초마다 상태 출력
                yield return new WaitForSeconds(5f);
                
                if (_isHttpListening)
                {
                    LogDebug($" HTTP 서버 상태: {_serverStatus}");
                    LogDebug($" 총 요청 수: {_requestCount}");
                    LogDebug($" 마지막 요청: {(_lastRequestTime == System.DateTime.MinValue ? "없음" : _lastRequestTime.ToString("HH:mm:ss"))}");
                    LogDebug($" 대기 중인 액션: {_mainThreadActions.Count}");
                    
                    // 백그라운드 스레드 상태 확인
                    if (_httpListenerThread != null)
                    {
                        LogDebug($" 백그라운드 스레드 상태: {(_httpListenerThread.IsAlive ? "실행 중" : "중단됨")}");
                    }
                    else
                    {
                        LogDebug($" 백그라운드 스레드: null");
                    }
                }
            }
            
            LogDebug(" HTTP 서버 상태 모니터링 종료");
        }
        
        /// <summary>
        /// HTTP 콜백 처리
        /// </summary>
        private void ProcessHttpCallback(System.Net.HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            
            LogDebug($" HTTP 콜백 수신: {request.Url}");
            LogDebug($" 요청 메소드: {request.HttpMethod}");
            LogDebug($" User-Agent: {request.UserAgent}");
            LogDebug($" 쿼리 스트링: {request.Url.Query}");
            
            try
            {
                // OAuth 콜백 URL 파싱
                string url = request.Url.ToString();
                string path = request.Url.AbsolutePath;
                LogDebug($"📋 전체 URL: {url}");
                LogDebug($"📋 경로: {path}");
                
                //  테스트 엔드포인트: /health
                if (path.Equals("/health", System.StringComparison.OrdinalIgnoreCase))
                {
                    LogDebug($" 헬스체크 요청 처리");
                    
                    var healthResponse = new {
                        status = "OK",
                        server = "Unity OAuth Callback Server",
                        timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        requestCount = _requestCount,
                        serverStatus = _serverStatus,
                        lastRequest = _lastRequestTime == System.DateTime.MinValue ? "None" : _lastRequestTime.ToString("HH:mm:ss"),
                        threadAlive = _httpListenerThread?.IsAlive ?? false
                    };
                    
                    string jsonResponse = JsonConvert.SerializeObject(healthResponse, Formatting.Indented);
                    string htmlResponse = $@"
                    <html><head><meta charset='UTF-8'><title>Unity OAuth Server Health</title></head>
                    <body style='font-family: Arial; padding: 20px; background: #f0f8ff;'>
                        <h1> Unity OAuth Callback Server</h1>
                        <div style='background: white; padding: 15px; border-radius: 8px; margin: 10px 0;'>
                            <h2> 서버 상태: 정상</h2>
                            <p><strong>현재 시각:</strong> {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
                            <p><strong>총 요청 수:</strong> {_requestCount}</p>
                            <p><strong>서버 상태:</strong> {_serverStatus}</p>
                            <p><strong>마지막 요청:</strong> {(_lastRequestTime == System.DateTime.MinValue ? "없음" : _lastRequestTime.ToString("yyyy-MM-dd HH:mm:ss"))}</p>
                            <p><strong>백그라운드 스레드:</strong> {(_httpListenerThread?.IsAlive == true ? "실행 중" : "중단됨")}</p>
                        </div>
                        <div style='background: #f9f9f9; padding: 15px; border-radius: 8px; margin: 10px 0;'>
                            <h3>📱 테스트 URL들</h3>
                            <ul>
                                <li><strong>헬스체크:</strong> <a href='http://localhost:7777/health'>http://localhost:7777/health</a></li>
                                <li><strong>OAuth 콜백:</strong> http://localhost:7777/auth/callback</li>
                            </ul>
                        </div>
                        <div style='background: #e8f5e8; padding: 15px; border-radius: 8px; font-family: monospace; font-size: 12px;'>
                            <h3> JSON 응답</h3>
                            <pre>{jsonResponse}</pre>
                        </div>
                    </body></html>";
                    
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(htmlResponse);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html; charset=utf-8";
                    response.StatusCode = 200;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    
                    LogDebug($" 헬스체크 응답 전송 완료 (크기: {buffer.Length} bytes)");
                }
                //  OAuth 콜백 처리  
                else if (url.Contains("/auth/callback"))
                {
                    LogDebug($" OAuth 콜백 감지됨");
                    
                    // URL에서 파라미터 추출 및 로깅
                    var uri = new System.Uri(url);
                    var queryParams = ParseQueryString(uri.Query);
                    
                    LogDebug($" URL 파라미터:");
                    foreach (var kvp in queryParams)
                    {
                        if (kvp.Key == "code")
                        {
                            LogDebug($"  - {kvp.Key}: {kvp.Value?.Substring(0, System.Math.Min(10, kvp.Value.Length))}...");
                        }
                        else
                        {
                            LogDebug($"  - {kvp.Key}: {kvp.Value}");
                        }
                    }
                    
                    // Deep Link 형태로 변환
                    string deepLinkUrl = url.Replace("http://localhost:7777/auth/callback", "blokus://auth/callback");
                    LogDebug($" HTTP → Deep Link 변환: {deepLinkUrl}");
                    
                    // 성공 페이지 응답
                    string responseString = @"
                    <html><head><meta charset='UTF-8'></head>
                    <body style='font-family: Arial; text-align: center; padding: 50px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white;'>
                        <h2> Google Login Success!</h2>
                        <p>인증이 완료되었습니다. Unity 앱으로 돌아갑니다...</p>
                        <div style='margin: 20px; padding: 15px; background: rgba(255,255,255,0.1); border-radius: 10px;'>
                            <small>이 창은 2초 후 자동으로 닫힙니다.</small>
                        </div>
                        <script>
                            console.log('OAuth callback processed successfully');
                            setTimeout(() => {
                                console.log('Closing callback window');
                                window.close();
                            }, 2000);
                        </script>
                    </body></html>";
                    
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html; charset=utf-8";
                    response.StatusCode = 200;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    
                    LogDebug($"📄 성공 페이지 응답 전송 완료 (크기: {buffer.Length} bytes)");
                    
                    // Deep Link 콜백 처리
                    LogDebug($" Deep Link 콜백 처리 시작: {deepLinkUrl}");
                    OnDeepLinkActivated(deepLinkUrl);
                    LogDebug($" Deep Link 콜백 처리 완료");
                }
                else
                {
                    LogDebug($" 예상하지 못한 경로: {url}");
                    // 404 응답
                    response.StatusCode = 404;
                    string notFoundResponse = "<html><body><h1>404 Not Found</h1><p>OAuth callback path not found</p></body></html>";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(notFoundResponse);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html";
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (System.Exception ex)
            {
                LogDebug($" HTTP 콜백 처리 중 심각한 오류: {ex.Message}");
                LogDebug($" 스택 트레이스: {ex.StackTrace}");
                
                try
                {
                    response.StatusCode = 500;
                    string errorResponse = $"<html><body><h1>500 Server Error</h1><p>Error processing callback: {ex.Message}</p></body></html>";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(errorResponse);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html";
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                catch (System.Exception responseEx)
                {
                    LogDebug($" 에러 응답 전송 실패: {responseEx.Message}");
                }
            }
            finally
            {
                try
                {
                    response.Close();
                    LogDebug($"🔒 HTTP 응답 스트림 닫기 완료");
                }
                catch (System.Exception closeEx)
                {
                    LogDebug($"⚠️ 응답 스트림 닫기 실패: {closeEx.Message}");
                }
            }
        }
        #endregion

        #region  Development & Testing Methods
        /// <summary>
        /// Deep Link 지원 테스트 (개발용)
        /// </summary>
        private IEnumerator TestDeepLinkSupport()
        {
            yield return new WaitForSeconds(1f);
            
            LogDebug("=== Deep Link 지원 테스트 시작 ===");
            LogDebug($"Redirect URI: {redirectUri}");
            LogDebug($"Application.absoluteURL: {Application.absoluteURL ?? "null"}");
            
            // 플랫폼별 Deep Link 지원 상태 확인
            #if UNITY_ANDROID
            LogDebug("Android 플랫폼: Deep Link 지원됨");
            #elif UNITY_IOS
            LogDebug("iOS 플랫폼: URL Scheme 확인 필요");
            #else
            LogDebug("현재 플랫폼에서는 Deep Link가 제한적으로 지원됨");
            #endif
            
            LogDebug("=== Deep Link 테스트 완료 ===");
        }

        /// <summary>
        /// 수동으로 Deep Link 테스트 (Inspector에서 호출 가능)
        /// </summary>
        [ContextMenu("Test Deep Link Callback")]
        public void TestDeepLinkCallback()
        {
            string testUrl = $"{redirectUri}?code=test_code_12345&state=test_state";
            LogDebug($" 수동 Deep Link 테스트: {testUrl}");
            OnDeepLinkActivated(testUrl);
        }
        
        /// <summary>
        /// OAuth URL 미리보기 (Inspector에서 호출 가능) 
        /// </summary>
        [ContextMenu("Preview Auth URL")]
        public void PreviewAuthUrl()
        {
            if (!IsReady())
            {
                LogDebug(" Discovery document가 로드되지 않았습니다");
                return;
            }
            
            GeneratePkceParameters();
            string authUrl = BuildAuthorizationUrl();
            LogDebug($" Authorization URL:\n{authUrl}");
        }
        #endregion

        #region Static Helper Methods
        public static OidcAuthenticator Instance
        {
            get
            {
                return FindObjectOfType<OidcAuthenticator>();
            }
        }

        public static OidcAuthenticator GetOrCreate()
        {
            var instance = Instance;
            if (instance == null)
            {
                var go = new GameObject("OidcAuthenticator");
                instance = go.AddComponent<OidcAuthenticator>();
            }
            return instance;
        }
        
        /// <summary>
        /// Unity용 쿼리 스트링 파싱 (System.Web 의존성 없이)
        /// </summary>
        private Dictionary<string, string> ParseQueryString(string queryString)
        {
            var result = new Dictionary<string, string>();
            
            if (string.IsNullOrEmpty(queryString))
                return result;
                
            // '?' 제거
            if (queryString.StartsWith("?"))
                queryString = queryString.Substring(1);
                
            // '&'로 분할
            var pairs = queryString.Split('&');
            
            foreach (var pair in pairs)
            {
                if (string.IsNullOrEmpty(pair))
                    continue;
                    
                var parts = pair.Split('=');
                if (parts.Length >= 2)
                {
                    string key = UnityWebRequest.UnEscapeURL(parts[0]);
                    string value = UnityWebRequest.UnEscapeURL(parts[1]);
                    result[key] = value;
                }
                else if (parts.Length == 1)
                {
                    string key = UnityWebRequest.UnEscapeURL(parts[0]);
                    result[key] = "";
                }
            }
            
            return result;
        }
        #endregion

        #region Direct Google OAuth (Unity Editor)
        /// <summary>
        /// 배포 빌드 방식 Google OAuth - Deep Link 기반
        /// </summary>
        private IEnumerator StartProductionGoogleOAuth(Action<bool, string, TokenResponse> callback)
        {
            // PKCE 파라미터 생성
            GeneratePkceParameters();
            
            var oidcServerUrl = EnvironmentConfig.OidcServerUrl;
            LogDebug($" 배포 빌드 방식 Google OAuth: {oidcServerUrl}");
            
            // Google OAuth URL 생성 - 서버 콜백 방식 (자동 Deep Link 리다이렉트)
            var queryParams = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = $"{oidcServerUrl}/auth/google/callback", // 기존 등록된 서버 콜백
                ["scope"] = scope,
                ["state"] = _state,
                ["code_challenge"] = _codeChallenge,
                ["code_challenge_method"] = "S256"
            };

            string queryString = string.Join("&", queryParams.Select(kv => $"{kv.Key}={UnityWebRequest.EscapeURL(kv.Value)}"));
            string authUrl = $"{oidcServerUrl}/auth/google?{queryString}";
            
            LogDebug($" Google OAuth URL: {authUrl}");
            LogDebug($" 서버 콜백 URI: {oidcServerUrl}/auth/google/callback");
            
            // 브라우저에서 OAuth 수행
            Application.OpenURL(authUrl);
            LogDebug(" Google OAuth 브라우저 열기 성공");
            
            // Deep Link 콜백 대기
            yield return StartCoroutine(WaitForDeepLinkCallback());
        }

        /// <summary>
        /// Deep Link 콜백 대기 (배포 빌드 방식)
        /// </summary>
        private IEnumerator WaitForDeepLinkCallback()
        {
            LogDebug(" Deep Link 콜백 대기 시작");
            
            float startTime = Time.time;
            const float timeout = 300f; // 5분
            
            // Deep Link 이벤트 등록
            RegisterDeepLinkHandler();
            
            while (Time.time - startTime < timeout)
            {
                // Deep Link에서 authorization code가 수신되었는지 확인
                if (!string.IsNullOrEmpty(_receivedAuthCode))
                {
                    LogDebug($" Deep Link에서 Authorization Code 수신!");
                    
                    string authCode = _receivedAuthCode;
                    _receivedAuthCode = null; // 사용 후 초기화
                    
                    yield return StartCoroutine(ExchangeCodeForTokens(authCode));
                    yield break;
                }
                
                // Deep Link 에러 확인
                if (!string.IsNullOrEmpty(_receivedError))
                {
                    LogDebug($" Deep Link 에러: {_receivedError}");
                    CompleteAuthentication(false, $"OAuth 인증 실패: {_receivedError}", null);
                    yield break;
                }
                
                yield return new WaitForSeconds(0.5f); // 0.5초마다 체크
            }
            
            // 타임아웃
            LogDebug("⏰ Deep Link 콜백 타임아웃");
            CompleteAuthentication(false, "OAuth 인증 시간이 초과되었습니다.", null);
        }

        /// <summary>
        /// Deep Link 이벤트 핸들러 등록
        /// </summary>
        private void RegisterDeepLinkHandler()
        {
            if (_deepLinkHandlerRegistered) return;
            
            // Unity Deep Link 이벤트 등록
            Application.deepLinkActivated += OnDeepLinkReceived;
            _deepLinkHandlerRegistered = true;
            
            LogDebug(" Deep Link 이벤트 리스너 등록 완료");
        }

        /// <summary>
        /// Deep Link 수신 처리
        /// </summary>
        private void OnDeepLinkReceived(string deepLinkUrl)
        {
            LogDebug($" Deep Link 수신: {deepLinkUrl}");
            
            try
            {
                // blokus://auth/callback?code=xxx&state=xxx 파싱
                var uri = new Uri(deepLinkUrl);
                
                if (uri.Scheme == "blokus" && uri.Host == "auth" && uri.AbsolutePath == "/callback")
                {
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    
                    string code = query["code"];
                    string state = query["state"];
                    string error = query["error"];
                    
                    // State 검증
                    if (state != _state)
                    {
                        LogDebug($" State 불일치: 예상={_state}, 수신={state}");
                        _receivedError = "Invalid state parameter";
                        return;
                    }
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        LogDebug($" OAuth 에러: {error}");
                        _receivedError = error;
                        return;
                    }
                    
                    if (!string.IsNullOrEmpty(code))
                    {
                        LogDebug($" Authorization Code 수신: {code.Substring(0, Math.Min(10, code.Length))}...");
                        _receivedAuthCode = code;
                    }
                    else
                    {
                        LogDebug(" Authorization Code가 없음");
                        _receivedError = "Missing authorization code";
                    }
                }
                else
                {
                    LogDebug($"⚠️ 알 수 없는 Deep Link 형식: {deepLinkUrl}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($" Deep Link 파싱 오류: {ex.Message}");
                _receivedError = $"Deep Link parsing error: {ex.Message}";
            }
        }

        /// <summary>
        /// Deep Link 이벤트 핸들러 해제
        /// </summary>
        private void UnregisterDeepLinkHandler()
        {
            if (_deepLinkHandlerRegistered)
            {
                Application.deepLinkActivated -= OnDeepLinkReceived;
                _deepLinkHandlerRegistered = false;
                LogDebug(" Deep Link 이벤트 리스너 해제 완료");
            }
            
            // LogDebug($" Unity Editor 콜백 페이지 폴링 시작");
            
            // while (Time.time - startTime < timeout)
            // {
            //     using (var request = UnityWebRequest.Get(pollUrl))
            //     {
            //         yield return request.SendWebRequest();
                    
            //         if (request.result == UnityWebRequest.Result.Success)
            //         {
            //             string responseText = request.downloadHandler.text;
                        
            //             // HTML에서 authorization code 추출
            //             if (responseText.Contains("authorization_code:"))
            //             {
            //                 string code = ExtractCodeFromHtml(responseText);
            //                 if (!string.IsNullOrEmpty(code))
            //                 {
            //                     LogDebug($" Authorization Code 받음!");
            //                     yield return StartCoroutine(ExchangeCodeForTokens(code));
            //                     yield break;
            //                 }
            //             }
                        
            //             // 에러 체크
            //             if (responseText.Contains("error:"))
            //             {
            //                 string error = ExtractErrorFromHtml(responseText);
            //                 LogDebug($" OAuth 에러: {error}");
            //                 CompleteAuthentication(false, $"OAuth 인증 실패: {error}", null);
            //                 yield break;
            //             }
            //         }
            //     }
                
            //     // 2초마다 폴링
            //     yield return new WaitForSeconds(2f);
            // }
            
            // // 타임아웃
            // LogDebug("⏰ OAuth 폴링 타임아웃");
            // CompleteAuthentication(false, "OAuth 인증 시간이 초과되었습니다.", null);
        }

        /// <summary>
        /// HTML에서 authorization code 추출
        /// </summary>
        private string ExtractCodeFromHtml(string html)
        {
            var match = System.Text.RegularExpressions.Regex.Match(html, @"authorization_code:\s*([A-Za-z0-9\-_]+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// HTML에서 에러 메시지 추출
        /// </summary>
        private string ExtractErrorFromHtml(string html)
        {
            var match = System.Text.RegularExpressions.Regex.Match(html, @"error:\s*([^<\n]+)");
            return match.Success ? match.Groups[1].Value.Trim() : "Unknown error";
        }
        #endregion
    }

    /// <summary>
    /// SSL 인증서 검증 우회 (디버깅용)
    /// 프로덕션에서는 사용하지 마세요!
    /// </summary>
    public class BypassCertificate : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            Debug.Log("[BypassCertificate] SSL 인증서 검증 우회됨 (디버깅 모드)");
            return true; // 모든 인증서 허용
        }
    }
}