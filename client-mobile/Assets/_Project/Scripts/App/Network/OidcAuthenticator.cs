using System;
using System.Collections;
using System.Collections.Generic;
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
        
        [Header("🔥 Debugging & Diagnostics")]
        [SerializeField] private bool showDetailedLogs = true;
        [SerializeField] private bool testDeepLinkOnStart = false;
        
        [Header("Development Options")]
        public bool useHttpCallbackForTesting = true; // 🔥 Editor에서 테스트용 - 기본 활성화
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
            // 🔧 원격 로깅 시스템 초기화 (릴리즈 빌드용)
            RemoteLogger.Initialize(this);
            RemoteLogger.LogInfo($"🚀 OidcAuthenticator 시작 - Platform: {Application.platform}, BuildType: {(Debug.isDebugBuild ? "Debug" : "Release")}", "OIDC");
            
            // 🔥 시스템 진단 정보 출력
            LogDebug($"Unity 버전: {Application.unityVersion}");
            LogDebug($"플랫폼: {Application.platform}");
            LogDebug($"개발 빌드: {Debug.isDebugBuild}");
            LogDebug($"에디터 모드: {Application.isEditor}");
            
            // Development testing option (Editor에서만)
            if (Application.isEditor && useHttpCallbackForTesting)
            {
                redirectUri = "http://localhost:7777/auth/callback";
                LogDebug("✅ Editor 테스트 모드: HTTP 콜백 URI 사용");
                LogDebug($"🔄 Redirect URI: {redirectUri}");
            }
            else
            {
                LogDebug($"📱 프로덕션 모드: Deep Link URI 사용 - {redirectUri}");
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
                LogDebug($"🧪 Deep Link 테스트: {redirectUri}");
                StartCoroutine(TestDeepLinkSupport());
            }
            
            // 🔥 Editor용 HTTP 콜백 서버 시작
            if (Application.isEditor && useHttpCallbackForTesting)
            {
                StartHttpCallbackServer();
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
            Application.deepLinkActivated -= OnDeepLinkActivated;
            
            if (_deepLinkTimeoutCoroutine != null)
            {
                StopCoroutine(_deepLinkTimeoutCoroutine);
            }
            
            StopHttpCallbackServer();
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
                    _isHttpListening = false;
                    _httpListener.Stop();
                    _httpListener.Close();
                    LogDebug("🛑 HTTP 콜백 서버 정지됨");
                }
                catch (System.Exception ex)
                {
                    LogDebug($"HTTP 서버 정지 중 오류: {ex.Message}");
                }
                finally
                {
                    _httpListener = null;
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

            // Generate PKCE parameters
            GeneratePkceParameters();
            
            // Build authorization URL
            string authUrl = BuildAuthorizationUrl();
            LogDebug($"Opening authorization URL: {authUrl}");
            
            // 🔥 브라우저 열기 시도 및 에러 처리 강화
            try
            {
                // Open system browser
                Application.OpenURL(authUrl);
                LogDebug("✅ 브라우저 열기 성공");
                
                // Start listening for deep link
                StartDeepLinkListener();
            }
            catch (System.Exception ex)
            {
                LogDebug($"❌ 브라우저 열기 실패: {ex.Message}");
                CompleteAuthentication(false, $"브라우저를 열 수 없습니다: {ex.Message}", null);
                return;
            }
            
            // 🔥 추가 진단: 플랫폼별 브라우저 지원 확인
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
            RemoteLogger.LogInfo($"🌐 OIDC Discovery 요청 시작: {discoveryUrl}", "OIDC");

            // 🔧 Unity 2021.3+ 방식으로 변경
            using (UnityWebRequest request = new UnityWebRequest(discoveryUrl, "GET"))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.certificateHandler = new BypassCertificate();
                request.timeout = 10;
                
                // 명시적 헤더 설정
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("User-Agent", "Unity-Mobile-Client/1.0");
                
                LogDebug($"🌐 Sending request to: {discoveryUrl}");
                RemoteLogger.LogInfo($"📡 UnityWebRequest 전송: {discoveryUrl} (timeout: 10초)", "OIDC");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMsg = $"OIDC Discovery 실패: {request.error} (Response Code: {request.responseCode})";
                    LogDebug($"Failed to load discovery document: {request.error}");
                    RemoteLogger.LogError($"❌ {errorMsg}", "OIDC");
                    RemoteLogger.LogError($"🔧 RequestResult: {request.result}, ResponseCode: {request.responseCode}", "OIDC");
                    _isDiscoveryLoaded = false;
                    yield break;
                }

                try
                {
                    string responseText = request.downloadHandler.text;
                    RemoteLogger.LogInfo($"✅ OIDC Discovery 응답 수신 (길이: {responseText.Length})", "OIDC");
                    
                    _discoveryDocument = JsonConvert.DeserializeObject<OidcDiscoveryDocument>(responseText);
                    _isDiscoveryLoaded = true;
                    LogDebug("OIDC discovery document loaded successfully");
                    RemoteLogger.LogInfo($"✅ OIDC Discovery 문서 파싱 성공", "OIDC");
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to parse discovery document: {ex.Message}");
                    RemoteLogger.LogError($"❌ OIDC Discovery 파싱 실패: {ex.Message}", "OIDC");
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
            }
        }
        #endregion

        #region Secure Storage (PlayerPrefs for now, can be upgraded to Keychain/Keystore)
        private const string PREF_ACCESS_TOKEN = "oidc_access_token";
        private const string PREF_REFRESH_TOKEN = "oidc_refresh_token";
        private const string PREF_TOKEN_EXPIRY = "oidc_token_expiry";

        public void SaveTokens(TokenResponse tokenResponse)
        {
            if (tokenResponse == null)
                return;

            try
            {
                PlayerPrefs.SetString(PREF_ACCESS_TOKEN, tokenResponse.access_token ?? "");
                PlayerPrefs.SetString(PREF_REFRESH_TOKEN, tokenResponse.refresh_token ?? "");
                
                // Calculate expiry time
                var expiryTime = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 60); // 1 minute buffer
                PlayerPrefs.SetString(PREF_TOKEN_EXPIRY, expiryTime.ToBinary().ToString());
                
                PlayerPrefs.Save();
                LogDebug("Tokens saved to secure storage");
            }
            catch (Exception ex)
            {
                LogDebug($"Failed to save tokens: {ex.Message}");
            }
        }

        public string GetAccessToken()
        {
            try
            {
                string token = PlayerPrefs.GetString(PREF_ACCESS_TOKEN, "");
                if (string.IsNullOrEmpty(token))
                    return null;

                // Check if token is expired
                string expiryString = PlayerPrefs.GetString(PREF_TOKEN_EXPIRY, "");
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
                LogDebug($"Failed to get access token: {ex.Message}");
                return null;
            }
        }

        public string GetRefreshToken()
        {
            return PlayerPrefs.GetString(PREF_REFRESH_TOKEN, "");
        }

        public void ClearTokens()
        {
            PlayerPrefs.DeleteKey(PREF_ACCESS_TOKEN);
            PlayerPrefs.DeleteKey(PREF_REFRESH_TOKEN);
            PlayerPrefs.DeleteKey(PREF_TOKEN_EXPIRY);
            PlayerPrefs.Save();
            LogDebug("Tokens cleared from secure storage");
        }

        public bool HasValidTokens()
        {
            return !string.IsNullOrEmpty(GetAccessToken());
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

        #region 🔥 HTTP Callback Server for Editor
        private System.Net.HttpListener _httpListener;
        private bool _isHttpListening = false;
        
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
                
                LogDebug("🌐 HTTP 콜백 서버 성공적으로 시작: http://localhost:7777/");
                LogDebug("🔄 OAuth 콜백을 대기 중...");
                
                // 비동기로 요청 처리
                StartCoroutine(HandleHttpRequests());
            }
            catch (System.Exception ex)
            {
                LogDebug($"❌ HTTP 콜백 서버 시작 실패: {ex.Message}");
                LogDebug("💡 포트 7777이 이미 사용 중일 수 있습니다. Unity를 재시작해보세요.");
            }
        }
        
        /// <summary>
        /// HTTP 요청 처리 코루틴
        /// </summary>
        private IEnumerator HandleHttpRequests()
        {
            while (_isHttpListening && _httpListener != null)
            {
                System.Threading.Tasks.Task<System.Net.HttpListenerContext> contextTask = null;
                bool hasError = false;
                
                try
                {
                    contextTask = _httpListener.GetContextAsync();
                }
                catch (System.Exception ex)
                {
                    if (_isHttpListening) // 정상 종료가 아닌 경우만 로그
                    {
                        LogDebug($"HTTP 콜백 GetContext 오류: {ex.Message}");
                    }
                    yield break;
                }
                
                // 비동기 대기 (try-catch 밖에서)
                while (contextTask != null && !contextTask.IsCompleted)
                {
                    yield return null;
                }
                
                // 결과 처리
                try
                {
                    if (contextTask != null && contextTask.IsCompletedSuccessfully)
                    {
                        var context = contextTask.Result;
                        ProcessHttpCallback(context);
                    }
                }
                catch (System.Exception ex)
                {
                    if (_isHttpListening) // 정상 종료가 아닌 경우만 로그
                    {
                        LogDebug($"HTTP 콜백 처리 오류: {ex.Message}");
                    }
                    // 에러가 발생해도 계속 루프를 돌도록 yield break 대신 continue
                    continue;
                }
                
                yield return null; // 한 프레임 대기
            }
        }
        
        /// <summary>
        /// HTTP 콜백 처리
        /// </summary>
        private void ProcessHttpCallback(System.Net.HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            
            LogDebug($"🌐 HTTP 콜백 수신: {request.Url}");
            LogDebug($"🔍 요청 메소드: {request.HttpMethod}");
            LogDebug($"🔍 User-Agent: {request.UserAgent}");
            
            try
            {
                // OAuth 콜백 URL 파싱
                string url = request.Url.ToString();
                if (url.Contains("/auth/callback"))
                {
                    LogDebug($"✅ OAuth 콜백 감지됨");
                    
                    // Deep Link 형태로 변환
                    string deepLinkUrl = url.Replace("http://localhost:7777/auth/callback", "blokus://auth/callback");
                    LogDebug($"🔄 HTTP → Deep Link 변환: {deepLinkUrl}");
                    
                    // 성공 페이지 응답
                    string responseString = @"
                    <html><head><meta charset='UTF-8'></head>
                    <body style='font-family: Arial; text-align: center; padding: 50px;'>
                        <h2>✅ Login Success!</h2>
                        <p>Returning to Unity app...</p>
                        <script>setTimeout(() => window.close(), 2000);</script>
                    </body></html>";
                    
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html";
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    
                    LogDebug($"📄 성공 페이지 응답 전송 완료");
                    
                    // Deep Link 콜백 처리
                    LogDebug($"🎯 Deep Link 콜백 처리 시작");
                    OnDeepLinkActivated(deepLinkUrl);
                }
                else
                {
                    // 404 응답
                    response.StatusCode = 404;
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes("Not Found");
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            catch (System.Exception ex)
            {
                LogDebug($"HTTP 콜백 처리 중 오류: {ex.Message}");
            }
            finally
            {
                response.Close();
            }
        }
        #endregion

        #region 🔥 Development & Testing Methods
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
            LogDebug($"🧪 수동 Deep Link 테스트: {testUrl}");
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
                LogDebug("❌ Discovery document가 로드되지 않았습니다");
                return;
            }
            
            GeneratePkceParameters();
            string authUrl = BuildAuthorizationUrl();
            LogDebug($"🔗 Authorization URL:\n{authUrl}");
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