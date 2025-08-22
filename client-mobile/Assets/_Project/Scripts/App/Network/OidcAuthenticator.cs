using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

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
        [SerializeField] private string oidcServerUrl = "http://localhost:9000";
        [SerializeField] private string clientId = "unity-mobile-client";
        [SerializeField] private string redirectUri = "blokus://auth/callback";
        [SerializeField] private string scope = "openid profile email";
        [SerializeField] private bool useProduction = false;
        
        [Header("Development Options")]
        [SerializeField] private bool useHttpCallbackForTesting = false; // Editor에서 테스트용
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
            // Development testing option (Editor에서만)
            if (Application.isEditor && useHttpCallbackForTesting)
            {
                redirectUri = "http://localhost:7777/auth/callback";
                LogDebug("Editor 테스트 모드: HTTP 콜백 URI 사용");
            }
            
            // Production URL override
            if (useProduction)
            {
                oidcServerUrl = "https://blokus-online.mooo.com";
                enableDebugLogs = false;
            }
            
            // Load OIDC Discovery Document on startup
            StartCoroutine(LoadDiscoveryDocument());
            
            // Register for deep link events
            Application.deepLinkActivated += OnDeepLinkActivated;
            
            // Check if app was opened with deep link
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
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
            
            // Open system browser
            Application.OpenURL(authUrl);
            
            // Start listening for deep link
            StartDeepLinkListener();
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
            string discoveryUrl = $"{oidcServerUrl}/.well-known/openid-configuration";
            LogDebug($"Loading OIDC discovery document from: {discoveryUrl}");

            using (UnityWebRequest request = UnityWebRequest.Get(discoveryUrl))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogDebug($"Failed to load discovery document: {request.error}");
                    _isDiscoveryLoaded = false;
                    yield break;
                }

                try
                {
                    _discoveryDocument = JsonConvert.DeserializeObject<OidcDiscoveryDocument>(request.downloadHandler.text);
                    _isDiscoveryLoaded = true;
                    LogDebug("OIDC discovery document loaded successfully");
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to parse discovery document: {ex.Message}");
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

            if (!_isListeningForDeepLink || !url.StartsWith(redirectUri))
            {
                LogDebug("Ignoring deep link - not listening or wrong URI");
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
}