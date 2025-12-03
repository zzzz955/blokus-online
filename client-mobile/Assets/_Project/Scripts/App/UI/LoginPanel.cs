using UnityEngine;
using UnityEngine.UI;
using TMPro;
using App.Network;
using Features.Single.Core;
using Shared.UI;
using App.Core;
using App.Config;
using System;
using System.Collections;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace App.UI
{
    public class LoginPanel : Shared.UI.PanelBase
    {
        [Header("기본 UI 컴포넌트")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject loadingIndicator;

        [Header("소셜 로그인 버튼들")]
        [SerializeField] private Button[] socialLoginButtons; // socialLoginButtons[0] = Google, [1] = Facebook 등

        [Header("기타 버튼")]
        [SerializeField] private Button registerButton;

        [Header("게임 종료 모달")]
        [SerializeField] private GameExitModal gameExitModal;

        [Header("개발용 설정")]
        [SerializeField] private bool enableTestMode = false;
        [SerializeField] private string testUsername = "testuser";
        [SerializeField] private string testPassword = "testpass123";

        /// <summary>
        /// 릴리즈 빌드에서는 테스트 모드 강제 비활성화
        /// </summary>
        private bool IsTestModeEnabled => enableTestMode && (Application.isEditor || Debug.isDebugBuild);

        // 상태 관리
        private bool isAuthenticating = false;
        private OidcAuthenticator oidcAuthenticator;
        private AuthenticationService authenticationService;

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("LoginPanel 초기화");
            
            //  디버깅: GameExitModal 참조 상태 확인
            if (gameExitModal == null)
            {
                Debug.LogWarning("[LoginPanel] GameExitModal이 Inspector에서 할당되지 않음 - 자동으로 찾기 시도");
                gameExitModal = FindObjectOfType<GameExitModal>();
                
                if (gameExitModal != null)
                {
                    Debug.Log($"[LoginPanel] GameExitModal 자동으로 발견: {gameExitModal.name}");
                }
                else
                {
                    Debug.LogError("[LoginPanel] GameExitModal을 찾을 수 없습니다! Scene에 GameExitModal이 있는지 확인하세요.");
                }
            }
            else
            {
                Debug.Log($"[LoginPanel] GameExitModal 참조 확인됨: {gameExitModal.name}");
            }
        }

        protected override void Start()
        {
            base.Start();
            
            //  릴리즈 빌드 디버깅을 위한 토스트 메시지
            ShowSystemDiagnostics();
            
            // 버튼 이벤트 설정
            SetupButtons();
            
            // 네트워크 이벤트 구독
            SetupNetworkEvents();
            
            // UI 초기화
            UpdateUI();
        }

        void OnEnable()
        {
            //  제거: RefreshToken 자동 로그인은 SceneFlowController.CheckAutoLogin에서 처리
            // LoginPanel이 표시되는 시점은 이미 CheckAutoLogin이 실패한 경우이므로
            // 여기서 다시 시도할 필요가 없음
        }

        void OnDestroy()
        {
            // HTTP API 이벤트 구독 해제
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.OnAuthResponse -= OnHttpAuthResponse;
            }

            // OIDC 이벤트 구독 해제
            if (oidcAuthenticator != null)
            {
                OidcAuthenticator.OnAuthenticationComplete -= OnOidcAuthenticationComplete;
                OidcAuthenticator.OnAuthenticationError -= OnOidcAuthenticationError;
            }

            // AuthenticationService 이벤트 구독 해제
            if (authenticationService != null)
            {
                AuthenticationService.OnAuthenticationComplete -= OnGooglePlayGamesAuthComplete;
                AuthenticationService.OnAuthenticationError -= OnGooglePlayGamesAuthError;
            }
        }

        // Android 뒤로가기 처리는 BackButtonManager에서 전역 관리

        // ==========================================
        // 초기화 및 설정
        // ==========================================

        private void SetupButtons()
        {
            // ID/PW 로그인 버튼
            if (loginButton != null)
            {
                loginButton.onClick.AddListener(OnLoginButtonClicked);
                Debug.Log("로그인 버튼 연결 완료");
            }

            // 회원가입 버튼
            if (registerButton != null)
            {
                registerButton.onClick.AddListener(OnRegisterButtonClicked);
                Debug.Log("회원가입 버튼 연결 완료");
            }

            // 소셜 로그인 버튼 배열 처리 (Google, Facebook 등)
            if (socialLoginButtons != null && socialLoginButtons.Length > 0)
            {
                for (int i = 0; i < socialLoginButtons.Length; i++)
                {
                    int index = i; // 클로저 문제 방지
                    if (socialLoginButtons[i] != null)
                    {
                        socialLoginButtons[i].onClick.AddListener(() => OnSocialLoginButtonClicked(index));
                    }
                }
                Debug.Log($"소셜 로그인 버튼 {socialLoginButtons.Length}개 연결 완료");
            }
        }

        private void SetupNetworkEvents()
        {
            // HttpApiClient 인스턴스 생성
            if (HttpApiClient.Instance == null)
            {
                GameObject httpClientObj = new GameObject("HttpApiClient");
                httpClientObj.AddComponent<HttpApiClient>();
                Debug.Log("HttpApiClient 생성 완료");
            }

            // HTTP API 이벤트 구독
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.OnAuthResponse += OnHttpAuthResponse;
                Debug.Log("HttpApiClient 이벤트 구독 완료");
            }

            // OIDC Authenticator 설정
            SetupOidcAuthenticator();
        }

        private void SetupOidcAuthenticator()
        {
            //  글로벌 OIDC Authenticator 사용
            oidcAuthenticator = App.Core.AppBootstrap.GetGlobalOidcAuthenticator();

            if (oidcAuthenticator == null)
            {
                // Fallback: 글로벌 인스턴스가 없으면 직접 찾기
                oidcAuthenticator = FindObjectOfType<OidcAuthenticator>();
                Debug.LogWarning("글로벌 OIDC Authenticator를 찾을 수 없음 - 기존 인스턴스 사용");
            }

            if (oidcAuthenticator != null)
            {
                // OIDC 이벤트 구독
                OidcAuthenticator.OnAuthenticationComplete += OnOidcAuthenticationComplete;
                OidcAuthenticator.OnAuthenticationError += OnOidcAuthenticationError;

                Debug.Log($"OIDC Authenticator 연결 완료 - Ready: {oidcAuthenticator.IsReady()}");
            }
            else
            {
                Debug.LogError("OIDC Authenticator를 찾을 수 없습니다!");
                SystemMessageManager.ShowToast("OAuth 서비스를 찾을 수 없음", Shared.UI.MessagePriority.Error);
            }

            // AuthenticationService 설정 (Google Play Games용)
            authenticationService = FindObjectOfType<AuthenticationService>();
            if (authenticationService != null)
            {
                AuthenticationService.OnAuthenticationComplete += OnGooglePlayGamesAuthComplete;
                AuthenticationService.OnAuthenticationError += OnGooglePlayGamesAuthError;
                Debug.Log($"AuthenticationService 연결 완료 - Provider: {authenticationService.GetCurrentProviderName()}");
            }
            else
            {
                Debug.LogWarning("AuthenticationService를 찾을 수 없습니다 - Google Play Games 로그인 사용 불가");
            }
        }

        //  제거: TryAutoLoginWithRefreshToken 메서드
        // RefreshToken 자동 로그인은 SceneFlowController.CheckAutoLogin에서만 처리

        // ==========================================
        // 버튼 이벤트 핸들러
        // ==========================================

        public void OnLoginButtonClicked()
        {
            if (isAuthenticating)
            {
                SystemMessageManager.ShowToast("이미 인증 진행 중입니다", Shared.UI.MessagePriority.Warning);
                return;
            }

            string username = usernameInput?.text?.Trim();
            string password = passwordInput?.text;

            // 입력 검증
            if (string.IsNullOrEmpty(username))
            {
                SetStatusText("사용자명을 입력해주세요",Shared.UI.MessagePriority.Warning);
                SystemMessageManager.ShowToast("사용자명을 입력해주세요", Shared.UI.MessagePriority.Warning);
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                SetStatusText("비밀번호를 입력해주세요", Shared.UI.MessagePriority.Warning);
                SystemMessageManager.ShowToast("비밀번호를 입력해주세요", Shared.UI.MessagePriority.Warning);
                return;
            }

            // ID/PW 로그인 수행
            PerformHttpLogin(username, password);
        }


        public void OnSocialLoginButtonClicked(int buttonIndex)
        {
            if (isAuthenticating)
            {
                SystemMessageManager.ShowToast("이미 인증 진행 중입니다", Shared.UI.MessagePriority.Warning);
                return;
            }

            // 버튼 인덱스에 따른 소셜 로그인
            string provider = "unknown";
            switch (buttonIndex)
            {
                case 0:
                    provider = "google";
                    // Android 빌드 또는 Editor: Google Play Games/Editor Auth 사용
                    #if UNITY_ANDROID || UNITY_EDITOR
                    PerformGooglePlayGamesToggle();
                    return;
                    #endif
                    break;
                case 1: provider = "facebook"; break;
                // 추가 소셜 로그인 제공자들...
                default:
                    SetStatusText("지원하지 않는 로그인 방식입니다", Shared.UI.MessagePriority.Warning);
                    SystemMessageManager.ShowToast("지원하지 않는 로그인 방식입니다", Shared.UI.MessagePriority.Warning);
                    return;
            }

            // Web OAuth (에디터 또는 iOS)
            PerformOAuthLogin(provider);
        }

        public void OnRegisterButtonClicked()
        {
            // 웹 애플리케이션으로 리다이렉트
            SetStatusText("웹 브라우저에서 회원가입을 진행해주세요", Shared.UI.MessagePriority.Info);
            var registerUrl = $"{EnvironmentConfig.WebServerUrl}/auth/signin";
            
            Application.OpenURL(registerUrl);
            Debug.Log($"회원가입 페이지 열기: {registerUrl}");
        }

        // ==========================================
        // 로그인 수행 메서드
        // ==========================================

        private void PerformHttpLogin(string username, string password)
        {
            // OIDC manual-auth POST 요청으로 직접 로그인
            if (oidcAuthenticator == null || !oidcAuthenticator.IsReady())
            {
                SetStatusText("인증 서버 연결 확인 중...", Shared.UI.MessagePriority.Warning);
                SystemMessageManager.ShowToast("인증 서버 연결 확인 중...", Shared.UI.MessagePriority.Warning);
                return;
            }

            isAuthenticating = true;
            SetStatusText($"ID/PW 로그인 중: {username}", Shared.UI.MessagePriority.Info);
            SetLoadingState(true);

            // OIDC manual-auth POST 요청 수행
            StartCoroutine(PerformOidcManualLoginPost(username, password));
            Debug.Log($"OIDC manual-auth POST 로그인 요청 전송: {username}");
        }
        
        /// <summary>
        /// OIDC 직접 API(/api/auth/login)를 통한 로그인
        /// </summary>
        private IEnumerator PerformOidcManualLoginPost(string username, string password)
        {
            var oidcServerUrl = EnvironmentConfig.OidcServerUrl;
            var clientId = "unity-mobile-client";
            var scope = "openid profile email";
            
            // 직접 API 요청 데이터 (JSON)
            var loginRequest = new DirectLoginRequest
            {
                username = username,
                password = password,
                client_id = clientId,
                scope = scope
            };
            
            string jsonData = JsonUtility.ToJson(loginRequest);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            
            var directAuthUrl = $"{oidcServerUrl}/api/auth/login";
            Debug.Log($"🔐 OIDC 직접 API 로그인: {directAuthUrl}");
            
            using (UnityWebRequest request = new UnityWebRequest(directAuthUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 15;
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"직접 API 로그인 성공: {responseText.Substring(0, Math.Min(100, responseText.Length))}...");
                    
                    try
                    {
                        // JSON 응답 파싱
                        var loginResponse = JsonUtility.FromJson<DirectLoginResponse>(responseText);

                        if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.access_token))
                        {
                            // 토큰을 SecurityStorage에 이중 저장 (Keystore + 암호화 백업)
                            // ⚠️ StoreStringWithBackup을 사용해야 Keystore 손실 시 백업에서 복구 가능
                            App.Security.SecureStorage.StoreStringWithBackup(App.Security.TokenKeys.Access, loginResponse.access_token);
                            App.Security.SecureStorage.StoreStringWithBackup(App.Security.TokenKeys.Refresh, loginResponse.refresh_token);
                            App.Security.SecureStorage.StoreStringWithBackup("blokus_id_token", loginResponse.id_token);

                            // 사용자 정보 저장
                            if (loginResponse.user != null)
                            {
                                App.Security.SecureStorage.StoreStringWithBackup("blokus_user_id", loginResponse.user.user_id.ToString());
                                App.Security.SecureStorage.StoreStringWithBackup("blokus_username", loginResponse.user.username);
                                App.Security.SecureStorage.StoreStringWithBackup("blokus_email", loginResponse.user.email);
                            }

                            Debug.Log($"토큰 저장 완료: access_token 길이={loginResponse.access_token.Length}");

                            // HttpApiClient에 토큰 설정
                            if (HttpApiClient.Instance != null && loginResponse.user != null)
                            {
                                HttpApiClient.Instance.SetAuthToken(loginResponse.access_token, loginResponse.user.user_id);
                            }

                            // SessionManager에 access token만 저장 (refresh_token은 SecureStorage에서 관리)
                            if (App.Core.SessionManager.Instance != null)
                            {
                                App.Core.SessionManager.Instance.SetTokens(
                                    loginResponse.access_token,
                                    "", // refresh_token은 SecureStorage에서 관리
                                    loginResponse.user.user_id
                                );
                            }

                            // 로그인 성공 처리
                            OnLoginSuccess("직접 API 로그인 성공", loginResponse.access_token);
                            isAuthenticating = false;
                            SetLoadingState(false);
                        }
                        else
                        {
                            OnLoginFailed("서버 응답에서 토큰을 찾을 수 없습니다");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"직접 API 응답 파싱 오류: {ex.Message}");
                        OnLoginFailed("서버 응답을 처리할 수 없습니다");
                    }
                }
                else
                {
                    Debug.LogError($"직접 API 로그인 오류: {request.error} (코드: {request.responseCode})");
                    string errorResponse = request.downloadHandler.text;
                    
                    string errorMessage = "로그인 실패";
                    if (!string.IsNullOrEmpty(errorResponse))
                    {
                        try
                        {
                            // JSON 오류 응답 파싱
                            var errorJson = JsonUtility.FromJson<DirectAuthErrorResponse>(errorResponse);
                            if (errorJson != null && !string.IsNullOrEmpty(errorJson.error_description))
                            {
                                errorMessage = errorJson.error_description;
                            }
                        }
                        catch
                        {
                            if (errorResponse.Contains("invalid_credentials"))
                            {
                                errorMessage = "아이디 또는 비밀번호가 올바르지 않습니다";
                            }
                            else if (errorResponse.Contains("invalid_request"))
                            {
                                errorMessage = "요청이 올바르지 않습니다";
                            }
                            else if (errorResponse.Contains("server_error"))
                            {
                                errorMessage = "서버에서 일시적인 오류가 발생했습니다";
                            }
                        }
                    }
                    
                    OnLoginFailed(errorMessage);
                }
            }
        }
        
        /// <summary>
        /// OIDC 서버를 통한 ID/PW 로그인 (사용 안함 - OAuth 전용)
        /// </summary>
        /*
        private IEnumerator PerformOidcManualLogin(string username, string password)
        {
            // OIDC 서버의 manual-auth 엔드포인트로 브라우저 열기
            var oidcServerUrl = EnvironmentConfig.OidcServerUrl;
            var clientId = "unity-mobile-client";
            var redirectUri = Application.isEditor && oidcAuthenticator.useHttpCallbackForTesting 
                ? "http://localhost:7777/auth/callback" 
                : "blokus://auth/callback";
            
            // Manual auth URL 구성
            var authParams = new System.Collections.Generic.Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["scope"] = "openid profile email",
                ["state"] = System.Guid.NewGuid().ToString("N"),
                ["response_type"] = "code"
            };
            
            var paramString = string.Join("&", 
                System.Linq.Enumerable.Select(authParams, 
                    kv => $"{UnityEngine.Networking.UnityWebRequest.EscapeURL(kv.Key)}={UnityEngine.Networking.UnityWebRequest.EscapeURL(kv.Value)}"));
            
            var manualAuthUrl = $"{oidcServerUrl}/manual-auth?{paramString}";
            
            Debug.Log($"🔐 Opening OIDC manual auth: {manualAuthUrl}");

            bool openSuccess = false;
            try
            {
                // 브라우저에서 manual auth 페이지 열기
                Application.OpenURL(manualAuthUrl);
                
                // OIDC 콜백 리스너 시작
                oidcAuthenticator.StartListeningForCallback();
                openSuccess = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"OIDC Manual 로그인 오류: {ex.Message}");
                SetStatusText("로그인 처리 중 오류가 발생했습니다", Shared.UI.MessagePriority.Error);
                OnLoginFailed("인증 서버 연결 실패");
                yield break;
            }

            if (!openSuccess)
            {
                yield break;
            }
            
            // 인증 완료까지 대기 (기존 OIDC 플로우 활용)
            yield return new WaitUntil(() => !isAuthenticating || !oidcAuthenticator.IsAuthenticating());
        }
        */

        /// <summary>
        /// Google Play Games 인증 상태 토글 (로그인 <-> 로그아웃)
        /// </summary>
        private void PerformGooglePlayGamesToggle()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            // CRITICAL: PlayGamesPlatform.IsAuthenticated()가 아닌 서버 인증 상태로 판단
            // Silent sign-in 성공 != 서버 인증 성공 (OPEN_ID 미동의 시 서버 인증 실패)
            bool hasServerAuth = HttpApiClient.Instance != null &&
                                 !string.IsNullOrEmpty(HttpApiClient.Instance.GetAuthToken());

            Debug.Log($"[LoginPanel] Google 버튼 클릭 - 서버 인증 상태: {hasServerAuth}");
            Debug.Log($"[LoginPanel] PlayGamesPlatform.IsAuthenticated: {GooglePlayGames.PlayGamesPlatform.Instance.IsAuthenticated()}");

            if (hasServerAuth)
            {
                // 서버 인증 완료 상태 → 통합 로그아웃 (SessionManager 사용)
                Debug.Log("[LoginPanel] Google Play Games 로그아웃 시도 (SessionManager 사용)");
                SetStatusText("로그아웃 중...", Shared.UI.MessagePriority.Info);

                // SessionManager의 통합 로그아웃 사용
                if (App.Core.SessionManager.Instance != null)
                {
                    App.Core.SessionManager.Instance.LogoutAndClearSession();
                }

                SetStatusText("로그아웃 완료", Shared.UI.MessagePriority.Success);
                SystemMessageManager.ShowToast("로그아웃 완료", Shared.UI.MessagePriority.Success);
            }
            else
            {
                // 서버 인증되지 않은 상태 → 로그인 (OPEN_ID 동의 UI 포함)
                Debug.Log("[LoginPanel] 서버 인증 없음 → Interactive 로그인 시작");
                PerformGooglePlayGamesLogin();
            }
            #else
            // Editor: 항상 로그인 시도
            PerformGooglePlayGamesLogin();
            #endif
        }

        private void PerformGooglePlayGamesLogin()
        {
            if (authenticationService == null)
            {
                SetStatusText("Google Play Games 서비스를 찾을 수 없습니다", Shared.UI.MessagePriority.Error);
                SystemMessageManager.ShowToast("Google Play Games 서비스를 찾을 수 없습니다", Shared.UI.MessagePriority.Error);
                return;
            }

            isAuthenticating = true;
            SetStatusText("Google Play Games로 로그인 중...", Shared.UI.MessagePriority.Info);
            SetLoadingState(true);

            // Google Play Games 인증 시작
            authenticationService.StartGooglePlayGamesAuth((success, message, tokenResponse) =>
            {
                if (success && tokenResponse != null)
                {
                    OnOAuthLoginSuccess(tokenResponse.access_token, tokenResponse.refresh_token);
                    isAuthenticating = false;
                    SetLoadingState(false);
                }
                else
                {
                    OnLoginFailed(message ?? "Google Play Games 로그인 실패");
                }
            });

            Debug.Log("Google Play Games 로그인 시작");
        }

        private void PerformOAuthLogin(string provider)
        {
            if (oidcAuthenticator == null || !oidcAuthenticator.IsReady())
            {
                SetStatusText("OAuth 설정을 확인하는 중입니다...", Shared.UI.MessagePriority.Warning);
                SystemMessageManager.ShowToast("OAuth 설정을 확인하는 중입니다...", Shared.UI.MessagePriority.Warning);
                return;
            }

            isAuthenticating = true;
            SetStatusText($"{provider.ToUpper()} 계정으로 로그인 중...", Shared.UI.MessagePriority.Info);
            SetLoadingState(true);

            // OIDC Authorization Code Flow 시작
            oidcAuthenticator.StartAuthentication((success, message, tokenResponse) =>
            {
                if (success && tokenResponse != null)
                {
                    OnOAuthLoginSuccess(tokenResponse.access_token, tokenResponse.refresh_token);
                    isAuthenticating = false;
                    SetLoadingState(false);
                }
                else
                {
                    OnLoginFailed(message ?? "OAuth 로그인 실패");
                }
            });

            Debug.Log($"OAuth 로그인 시작: {provider}");
        }

        // ==========================================
        // 네트워크 응답 이벤트 핸들러
        // ==========================================

        private void OnHttpAuthResponse(bool success, string message, string token)
        {
            isAuthenticating = false;
            SetLoadingState(false);

            //  토스트로 서버 응답 확인 (에러일 때만)
            if (!success)
            {
                SystemMessageManager.ShowToast($"서버 응답 실패: {message?.Substring(0, Math.Min(30, message?.Length ?? 0))}...", 
                                            Shared.UI.MessagePriority.Error);
            }

            if (success)
            {
                // 토큰 저장
                PlayerPrefs.SetString("access_token", token);
                PlayerPrefs.Save();
                
                OnLoginSuccess(message, token);
            }
            else
            {
                // OIDC_REDIRECT_REQUIRED인 경우 자동으로 OAuth 로그인으로 전환
                if (message.Contains("OIDC_REDIRECT_REQUIRED") || message.Contains("OIDC flow"))
                {
                    SetStatusText("OAuth 인증이 필요합니다. Google 계정으로 로그인합니다...", Shared.UI.MessagePriority.Info);
                    SystemMessageManager.ShowToast("OIDC 인증 요구됨 - Google OAuth 자동 전환", Shared.UI.MessagePriority.Warning);
                    Debug.Log("서버에서 OIDC 인증 요구 - Google OAuth로 자동 전환");
                    
                    // 1초 후 자동으로 Google OAuth 시작
                    App.Core.CoroutineRunner.Run(AutoStartGoogleOAuth());
                }
                else
                {
                    OnLoginFailed(message);
                }
            }
        }

        private System.Collections.IEnumerator AutoStartGoogleOAuth()
        {
            yield return new WaitForSeconds(1.0f);
            
            // Google OAuth 로그인 시작 (배열의 첫 번째 버튼이 Google이라고 가정)
            if (socialLoginButtons != null && socialLoginButtons.Length > 0)
            {
                OnSocialLoginButtonClicked(0); // Google = 인덱스 0
            }
            else
            {
                SetStatusText("Google 로그인 버튼이 설정되지 않았습니다", Shared.UI.MessagePriority.Error);
            }
        }

        private void OnOidcAuthenticationComplete(bool success, string message, OidcAuthenticator.TokenResponse tokens)
        {
            isAuthenticating = false;
            SetLoadingState(false);

            if (success && tokens != null)
            {
                // OIDC 토큰을 OidcAuthenticator에 저장
                var oidcAuthenticator = App.Core.AppBootstrap.GetGlobalOidcAuthenticator();
                if (oidcAuthenticator != null)
                {
                    oidcAuthenticator.SaveTokens(tokens);
                }
                
                OnOAuthLoginSuccess(tokens.access_token, tokens.refresh_token);
            }
            else
            {
                OnLoginFailed(message ?? "OIDC 인증 실패");
            }
        }

        private void OnOidcAuthenticationError(string error)
        {
            isAuthenticating = false;
            SetLoadingState(false);
            OnLoginFailed($"OAuth 오류: {error}");
        }

        private void OnGooglePlayGamesAuthComplete(bool success, string message, OidcAuthenticator.TokenResponse tokens)
        {
            isAuthenticating = false;
            SetLoadingState(false);

            if (success && tokens != null)
            {
                Debug.Log("Google Play Games 인증 성공");
                OnOAuthLoginSuccess(tokens.access_token, tokens.refresh_token);
            }
            else
            {
                OnLoginFailed(message ?? "Google Play Games 인증 실패");
            }
        }

        private void OnGooglePlayGamesAuthError(string error)
        {
            isAuthenticating = false;
            SetLoadingState(false);
            OnLoginFailed($"Google Play Games 오류: {error}");
        }

        // ==========================================
        // 로그인 결과 처리
        // ==========================================

        private void OnLoginSuccess(string message, string token)
        {
            SetStatusText("로그인 성공!", Shared.UI.MessagePriority.Success);
            
            Debug.Log($"로그인 성공 - {message}");

            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.SetAuthToken(token, 0);
                Debug.Log("HttpApiClient에 OAuth 토큰 설정 완료");
            }

            // 게임 메인 화면으로 전환
            App.Core.CoroutineRunner.Run(NavigateToMainAfterDelay());
        }

        private void OnOAuthLoginSuccess(string accessToken, string refreshToken)
        {
            SetStatusText("OAuth 로그인 성공!", Shared.UI.MessagePriority.Success);
            Debug.Log("OAuth 로그인 성공 - 토큰 저장 시작");

            // OIDC 토큰은 OidcAuthenticator에서 관리하므로 별도 저장 불필요
            // HttpApiClient에 토큰 설정 (userId는 임시로 0 사용)
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.SetAuthToken(accessToken, 0);
                Debug.Log("HttpApiClient에 OAuth 토큰 설정 완료");
            }

            //  수정: SessionManager에도 OAuth 로그인 상태 동기화
            if (App.Core.SessionManager.Instance != null)
            {
                // JWT에서 사용자 정보 추출하여 SessionManager 업데이트
                App.Core.SessionManager.Instance.SetTokens(accessToken, refreshToken, 0); // userId는 JWT에서 파싱됨
                Debug.Log("SessionManager에 OAuth 로그인 상태 동기화 완료");
            }

            // 게임 메인 화면으로 전환
            App.Core.CoroutineRunner.Run(NavigateToMainAfterDelay());
        }

        private void OnLoginFailed(string errorMessage)
        {
            SetStatusText($"로그인 실패: {errorMessage}", Shared.UI.MessagePriority.Error);
            SystemMessageManager.ShowToast($"로그인 실패: {errorMessage}", Shared.UI.MessagePriority.Error);
            Debug.LogWarning($"로그인 실패: {errorMessage}");

            // 유효하지 않은 refresh token 삭제
            if (errorMessage.Contains("refresh") || errorMessage.Contains("token"))
            {
                // SecureStorage에서 토큰 삭제
                App.Security.SecureStorage.DeleteKey(App.Security.TokenKeys.Refresh);
                App.Security.SecureStorage.DeleteKey("blokus_user_id");
                App.Security.SecureStorage.DeleteKey("blokus_username");
                
                // PlayerPrefs에서도 삭제 (하위 호환성)
                PlayerPrefs.DeleteKey("refresh_token");
                PlayerPrefs.DeleteKey("access_token");
                PlayerPrefs.Save();
                
                // OIDC 토큰도 삭제
                var oidcAuthenticator = App.Core.AppBootstrap.GetGlobalOidcAuthenticator();
                if (oidcAuthenticator != null)
                {
                    oidcAuthenticator.ClearTokens();
                }
                
                SystemMessageManager.ShowToast("유효하지 않은 토큰 삭제됨", Shared.UI.MessagePriority.Warning);
                Debug.Log("유효하지 않은 토큰 삭제 완료");
            }
        }

        private IEnumerator NavigateToMainAfterDelay()
        {
            yield return new WaitForSeconds(1.0f);

            // UIManager를 통해 ModeSelectionPanel로 전환
            var uiManager = UIManager.Instance;
            if (uiManager != null)
            {
                Hide(); // 현재 로그인 패널 숨김
                uiManager.ShowPanel(App.UI.UIState.ModeSelection); // 모드 선택 패널 표시
                Debug.Log("ModeSelectionPanel로 이동");
            }
            else
            {
                Debug.LogWarning("UIManager를 찾을 수 없습니다");
            }
        }

        // ==========================================
        // UI 업데이트
        // ==========================================

        private void UpdateUI()
        {
            // 개발 모드에서 테스트 계정 미리 채우기
            if (IsTestModeEnabled)
            {
                if (usernameInput != null) usernameInput.text = testUsername;
                if (passwordInput != null) passwordInput.text = testPassword;
            }
            isAuthenticating = false;
            SetLoadingState(false);
            SetStatusText("로그인 정보를 입력하세요", Shared.UI.MessagePriority.Info);
        }

        private void SetStatusText(string message, Shared.UI.MessagePriority priority)
        {
            if (statusText != null)
            {
                statusText.text = message;
                
                // 우선순위에 따른 색상 변경
                switch (priority)
                {
                    case Shared.UI.MessagePriority.Success:
                        statusText.color = Color.green;
                        break;
                    case Shared.UI.MessagePriority.Warning:
                        statusText.color = Color.yellow;
                        break;
                    case Shared.UI.MessagePriority.Error:
                        statusText.color = Color.red;
                        break;
                    default:
                        statusText.color = Color.white;
                        break;
                }
            }

            Debug.Log($"[{priority}] {message}");
        }

        private void SetLoadingState(bool isLoading)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(isLoading);
            }

            // 로딩 중일 때 버튼 비활성화
            if (loginButton != null) loginButton.interactable = !isLoading;
            if (registerButton != null) registerButton.interactable = !isLoading;

            if (socialLoginButtons != null)
            {
                foreach (var button in socialLoginButtons)
                {
                    if (button != null) button.interactable = !isLoading;
                }
            }
        }

        // ==========================================
        //  디버깅 및 진단
        // ==========================================
        
        /// <summary>
        /// 릴리즈 빌드에서 시스템 상태를 토스트로 표시하여 디버깅 (Warning 이상만)
        /// </summary>
        private void ShowSystemDiagnostics()
        {
            try
            {
                // HttpApiClient 상태 확인 (없으면 에러)
                bool hasHttpClient = HttpApiClient.Instance != null;
                if (!hasHttpClient)
                {
                    SystemMessageManager.ShowToast("HttpApiClient 없음!", Shared.UI.MessagePriority.Error);
                }
                
                // 버튼 상태 확인 (문제가 있으면 경고)
                bool loginBtnOk = loginButton != null && loginButton.interactable;
                bool socialBtnOk = socialLoginButtons != null && socialLoginButtons.Length > 0 && socialLoginButtons[0] != null && socialLoginButtons[0].interactable;
                bool registerBtnOk = registerButton != null && registerButton.interactable;
                
                if (!loginBtnOk || !socialBtnOk || !registerBtnOk)
                {
                    SystemMessageManager.ShowToast($"버튼상태 문제 - Login:{loginBtnOk}, Social:{socialBtnOk}, Register:{registerBtnOk}", 
                        Shared.UI.MessagePriority.Warning);
                }
                    
                // Canvas/UI 시스템 상태 (문제가 있으면 에러)
                var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
                var canvas = GetComponentInParent<Canvas>();
                var raycaster = GetComponentInParent<GraphicRaycaster>();
                
                if (eventSystem == null || canvas == null || raycaster == null)
                {
                    SystemMessageManager.ShowToast($"UI시스템 문제 - EventSystem:{eventSystem != null}, Canvas:{canvas != null}, Raycaster:{raycaster != null}", 
                        Shared.UI.MessagePriority.Error);
                }
            }
            catch (System.Exception ex)
            {
                SystemMessageManager.ShowToast($"진단 오류: {ex.Message}", Shared.UI.MessagePriority.Error);
            }
        }

        // ==========================================
        // 유틸리티
        // ==========================================
    }

    // ==========================================
    // 직접 API 호출을 위한 데이터 구조
    // ==========================================
    
    [System.Serializable]
    public class DirectLoginRequest
    {
        public string username;
        public string password;
        public string client_id;
        public string scope;
    }
    
    [System.Serializable]
    public class DirectLoginResponse
    {
        public string access_token;
        public string id_token;
        public string refresh_token;
        public string token_type;
        public int expires_in;
        public string scope;
        public DirectLoginUser user;
    }
    
    [System.Serializable]
    public class DirectLoginUser
    {
        public int user_id;
        public string username;
        public string email;
        public string display_name;
        public int level;
        public int experience_points;
    }
    
    [System.Serializable]
    public class DirectAuthErrorResponse
    {
        public string error;
        public string error_description;
    }
    
}