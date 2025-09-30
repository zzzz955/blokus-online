using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using App.Config;

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
        /// Google Play Games 인증 시작
        /// </summary>
        public async void StartGooglePlayGamesAuth(Action<bool, string, OidcAuthenticator.TokenResponse> callback)
        {
            if (_isAuthenticating)
            {
                Debug.LogWarning("[AuthenticationService] Authentication already in progress");
                callback?.Invoke(false, "Authentication already in progress", null);
                return;
            }

            if (_authProvider == null || !_authProvider.IsAvailable())
            {
                string error = "Auth provider not available for this platform";
                Debug.LogError($"[AuthenticationService] {error}");
                callback?.Invoke(false, error, null);
                OnAuthenticationError?.Invoke(error);
                return;
            }

            _isAuthenticating = true;

            try
            {
                // 1. Google Play Games 인증 및 Auth Code 획득
                Debug.Log($"[AuthenticationService] Starting authentication with {_authProvider.GetProviderName()}");
                AuthResult authResult = await _authProvider.AuthenticateAsync();

                if (!authResult.Success)
                {
                    Debug.LogError($"[AuthenticationService] Authentication failed: {authResult.ErrorMessage}");
                    callback?.Invoke(false, authResult.ErrorMessage, null);
                    OnAuthenticationError?.Invoke(authResult.ErrorMessage);
                    return;
                }

                Debug.Log("[AuthenticationService] Auth code received, exchanging with backend...");

                // 2. Backend로 Auth Code 전송 및 JWT 토큰 교환
                StartCoroutine(ExchangeAuthCodeForTokens(authResult.AuthCode, callback));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthenticationService] Exception: {ex.Message}");
                callback?.Invoke(false, ex.Message, null);
                OnAuthenticationError?.Invoke(ex.Message);
            }
            finally
            {
                _isAuthenticating = false;
            }
        }

        /// <summary>
        /// Backend API로 Auth Code를 전송하고 JWT 토큰을 받아옵니다
        /// </summary>
        private IEnumerator ExchangeAuthCodeForTokens(string authCode, Action<bool, string, OidcAuthenticator.TokenResponse> callback)
        {
            string backendUrl = useProduction
                ? EnvironmentConfig.ProductionOidcServerUrl
                : EnvironmentConfig.DevelopmentOidcServerUrl;

            string endpoint = $"{backendUrl}/auth/google-play-games";

            // Request body
            var requestData = new
            {
                client_id = "unity-mobile-client",
                auth_code = authCode
            };

            string jsonData = JsonConvert.SerializeObject(requestData);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

            using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30;

                Debug.Log($"[AuthenticationService] Sending auth code to: {endpoint}");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[AuthenticationService] Token exchange successful: {request.responseCode}");

                    try
                    {
                        var tokenResponse = JsonConvert.DeserializeObject<OidcAuthenticator.TokenResponse>(request.downloadHandler.text);

                        // 토큰 저장 (OidcAuthenticator의 저장 로직 활용)
                        var oidcAuth = FindObjectOfType<OidcAuthenticator>();
                        if (oidcAuth != null)
                        {
                            oidcAuth.SaveTokens(tokenResponse);
                        }

                        callback?.Invoke(true, "Authentication successful", tokenResponse);
                        OnAuthenticationComplete?.Invoke(true, "Success", tokenResponse);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[AuthenticationService] Failed to parse token response: {ex.Message}");
                        callback?.Invoke(false, "Failed to parse token response", null);
                        OnAuthenticationError?.Invoke(ex.Message);
                    }
                }
                else
                {
                    string errorMsg = $"Token exchange failed: {request.responseCode} - {request.error}";
                    Debug.LogError($"[AuthenticationService] {errorMsg}");
                    Debug.LogError($"[AuthenticationService] Response: {request.downloadHandler.text}");

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