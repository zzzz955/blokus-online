using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using App.Config;
#if UNITY_ANDROID
using GooglePlayGames;
#endif

namespace App.Network
{
    /// <summary>
    /// 통합 인증 서비스
    /// Google Play Games (Mobile) 및 Web OAuth를 관리합니다.
    /// </summary>
    public class AuthenticationService : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool useProduction = false;

        private IAuthenticationProvider _authProvider;
        private bool _isAuthenticating = false;

        #region Events
        public static event Action<bool, string, OidcAuthenticator.TokenResponse> OnAuthenticationComplete;
        public static event Action<string> OnAuthenticationError;
        #endregion

        private void Awake()
        {
            // Singleton pattern
            if (FindObjectsOfType<AuthenticationService>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            // 플랫폼별 Provider 선택
            SelectAuthProvider();
        }

        /// <summary>
        /// 플랫폼에 따라 적절한 인증 Provider 선택
        /// </summary>
        private void SelectAuthProvider()
        {
            #if UNITY_EDITOR
            _authProvider = new EditorAuthProvider();
            Debug.Log("[AuthenticationService] Using EditorAuthProvider for testing");
            #elif UNITY_ANDROID
            _authProvider = new GooglePlayGamesAuthProvider();
            Debug.Log("[AuthenticationService] Using GooglePlayGamesAuthProvider");
            #else
            Debug.LogWarning("[AuthenticationService] No auth provider available for this platform");
            #endif
        }

        /// <summary>
        /// Google Play Games 인증 시작 (authCode 파라미터 추가 - Silent sign-in용)
        /// </summary>
        public async void StartGooglePlayGamesAuth(Action<bool, string, OidcAuthenticator.TokenResponse> callback, string authCode = null)
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            App.Logging.AndroidLogger.LogAuth("=== AuthenticationService.StartGooglePlayGamesAuth START ===");
            #endif

            if (_isAuthenticating)
            {
                #if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogAuth("⚠️ Authentication already in progress");
                #else
                Debug.LogWarning("[AuthenticationService] Authentication already in progress");
                #endif
                callback?.Invoke(false, "Authentication already in progress", null);
                return;
            }

            if (_authProvider == null || !_authProvider.IsAvailable())
            {
                string error = "Auth provider not available for this platform";
                #if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogError($"❌ {error}");
                #else
                Debug.LogError($"[AuthenticationService] {error}");
                #endif
                callback?.Invoke(false, error, null);
                OnAuthenticationError?.Invoke(error);
                return;
            }

            _isAuthenticating = true;

            try
            {
                AuthResult authResult;

                // authCode가 제공된 경우 (Silent sign-in에서 이미 획득한 경우)
                if (!string.IsNullOrEmpty(authCode))
                {
                    #if UNITY_ANDROID && !UNITY_EDITOR
                    App.Logging.AndroidLogger.LogAuth($"Using provided auth code (length: {authCode.Length})");
                    #endif

                    authResult = new AuthResult
                    {
                        Success = true,
                        AuthCode = authCode
                    };
                }
                else
                {
                    // 1. Google Play Games 인증 및 Auth Code 획득
                    #if UNITY_ANDROID && !UNITY_EDITOR
                    App.Logging.AndroidLogger.LogAuth($"Starting authentication with {_authProvider.GetProviderName()}");
                    #else
                    Debug.Log($"[AuthenticationService] Starting authentication with {_authProvider.GetProviderName()}");
                    #endif

                    authResult = await _authProvider.AuthenticateAsync();

                    if (!authResult.Success)
                    {
                        #if UNITY_ANDROID && !UNITY_EDITOR
                        App.Logging.AndroidLogger.LogError($"❌ Authentication failed: {authResult.ErrorMessage}");
                        #else
                        Debug.LogError($"[AuthenticationService] Authentication failed: {authResult.ErrorMessage}");
                        #endif
                        callback?.Invoke(false, authResult.ErrorMessage, null);
                        OnAuthenticationError?.Invoke(authResult.ErrorMessage);
                        return;
                    }
                }

                #if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogAuth($"Auth code received (length: {authResult.AuthCode?.Length ?? 0}), exchanging with backend...");
                #else
                Debug.Log("[AuthenticationService] Auth code received, exchanging with backend...");
                #endif

                // 2. Backend로 Auth Code 전송 및 JWT 토큰 교환
                StartCoroutine(ExchangeAuthCodeForTokens(authResult.AuthCode, callback));
            }
            catch (Exception ex)
            {
                #if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogError($"❌ EXCEPTION: {ex.GetType().Name}");
                App.Logging.AndroidLogger.LogError($"Message: {ex.Message}");
                App.Logging.AndroidLogger.LogError($"StackTrace: {ex.StackTrace}");
                #else
                Debug.LogError($"[AuthenticationService] Exception: {ex.Message}");
                #endif
                callback?.Invoke(false, ex.Message, null);
                OnAuthenticationError?.Invoke(ex.Message);
            }
            finally
            {
                _isAuthenticating = false;
            }
        }

        /// <summary>
        /// Backend API로 Player ID를 전송하고 JWT 토큰을 받아옵니다 (OAuth 불필요)
        /// </summary>
        private IEnumerator ExchangeAuthCodeForTokens(string authCode, Action<bool, string, OidcAuthenticator.TokenResponse> callback)
        {
            string backendUrl = EnvironmentConfig.OidcServerUrl;
            string endpoint = $"{backendUrl}/auth/google-play-games-player-id";

            #if UNITY_ANDROID && !UNITY_EDITOR
            App.Logging.AndroidLogger.LogAuth($"Backend URL: {backendUrl}");
            App.Logging.AndroidLogger.LogAuth($"Endpoint: {endpoint}");
            App.Logging.AndroidLogger.LogAuth($"Player ID length: {authCode?.Length ?? 0}");
            App.Logging.AndroidLogger.LogAuth("🎮 Using Play Games Player ID authentication (no OAuth required)");
            #endif

            // Request body - Player ID 사용 (auth_code가 실제로는 player_id임)
            var requestData = new System.Collections.Generic.Dictionary<string, string>
            {
                {"client_id", "unity-mobile-client"},
                {"player_id", authCode}  // authCode 변수에 player_id가 담겨있음
            };

            string jsonData = JsonConvert.SerializeObject(requestData);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

            #if UNITY_ANDROID && !UNITY_EDITOR
            App.Logging.AndroidLogger.LogAuth($"Request JSON: {jsonData}");
            App.Logging.AndroidLogger.LogAuth($"Request body length: {bodyRaw.Length}");
            #endif

            using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30;

                #if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogAuth($"Sending Player ID to backend: {endpoint}");
                #else
                Debug.Log($"[AuthenticationService] Sending Player ID to: {endpoint}");
                #endif

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    #if UNITY_ANDROID && !UNITY_EDITOR
                    App.Logging.AndroidLogger.LogAuth($"✅ Token exchange successful: {request.responseCode}");
                    #else
                    Debug.Log($"[AuthenticationService] Token exchange successful: {request.responseCode}");
                    #endif

                    try
                    {
                        var tokenResponse = JsonConvert.DeserializeObject<OidcAuthenticator.TokenResponse>(request.downloadHandler.text);

                        #if UNITY_ANDROID && !UNITY_EDITOR
                        App.Logging.AndroidLogger.LogAuth($"Token parsed successfully");
                        App.Logging.AndroidLogger.LogAuth($"Access token length: {tokenResponse.access_token?.Length ?? 0}");
                        App.Logging.AndroidLogger.LogAuth($"Refresh token length: {tokenResponse.refresh_token?.Length ?? 0}");
                        #endif

                        // 토큰 저장 (OidcAuthenticator의 저장 로직 활용)
                        var oidcAuth = FindObjectOfType<OidcAuthenticator>();
                        if (oidcAuth != null)
                        {
                            oidcAuth.SaveTokens(tokenResponse);
                            #if UNITY_ANDROID && !UNITY_EDITOR
                            App.Logging.AndroidLogger.LogAuth("Tokens saved via OidcAuthenticator");
                            #endif
                        }
                        else
                        {
                            #if UNITY_ANDROID && !UNITY_EDITOR
                            App.Logging.AndroidLogger.LogAuth("⚠️ OidcAuthenticator not found, tokens not saved");
                            #endif
                        }

                        callback?.Invoke(true, "Authentication successful", tokenResponse);
                        OnAuthenticationComplete?.Invoke(true, "Success", tokenResponse);
                    }
                    catch (Exception ex)
                    {
                        #if UNITY_ANDROID && !UNITY_EDITOR
                        App.Logging.AndroidLogger.LogError($"❌ Failed to parse token response: {ex.Message}");
                        App.Logging.AndroidLogger.LogError($"Response text: {request.downloadHandler.text}");
                        #else
                        Debug.LogError($"[AuthenticationService] Failed to parse token response: {ex.Message}");
                        #endif
                        callback?.Invoke(false, "Failed to parse token response", null);
                        OnAuthenticationError?.Invoke(ex.Message);
                    }
                }
                else
                {
                    string errorMsg = $"Token exchange failed: {request.responseCode} - {request.error}";
                    #if UNITY_ANDROID && !UNITY_EDITOR
                    App.Logging.AndroidLogger.LogError($"❌ {errorMsg}");
                    App.Logging.AndroidLogger.LogError($"Response: {request.downloadHandler.text}");
                    App.Logging.AndroidLogger.LogError($"Request URL: {request.url}");
                    App.Logging.AndroidLogger.LogError($"Request method: {request.method}");
                    #else
                    Debug.LogError($"[AuthenticationService] {errorMsg}");
                    Debug.LogError($"[AuthenticationService] Response: {request.downloadHandler.text}");
                    #endif

                    callback?.Invoke(false, errorMsg, null);
                    OnAuthenticationError?.Invoke(errorMsg);
                }
            }
        }

        /// <summary>
        /// 현재 사용 중인 인증 제공자 이름 반환
        /// </summary>
        public string GetCurrentProviderName()
        {
            return _authProvider?.GetProviderName() ?? "None";
        }
    }
}