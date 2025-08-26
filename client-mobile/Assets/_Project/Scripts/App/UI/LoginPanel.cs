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

        [Header("설정")]
        [SerializeField] private bool autoLoginWithRefreshToken = true;

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
        private bool hasCheckedRefreshToken = false;
        private OidcAuthenticator oidcAuthenticator;

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("LoginPanel 초기화");
        }

        protected override void Start()
        {
            base.Start();
            
            // 버튼 이벤트 설정
            SetupButtons();
            
            // 네트워크 이벤트 구독
            SetupNetworkEvents();
            
            // UI 초기화
            UpdateUI();
        }

        void OnEnable()
        {
            // 🔥 수정: LoginPanel이 활성화될 때마다 refresh token 체크 리셋
            // 로그아웃 후 재로그인을 위해 필요하지만, 무한 루프를 방지하기 위해 조건 추가
            
            // Refresh Token 기반 자동 로그인 시도
            if (autoLoginWithRefreshToken && !hasCheckedRefreshToken)
            {
                // 실제로 저장된 refresh token이 있는지 확인
                string savedRefreshToken = PlayerPrefs.GetString("refresh_token", "");
                if (!string.IsNullOrEmpty(savedRefreshToken))
                {
                    // 🔥 수정: CoroutineRunner 사용하여 안전한 코루틴 실행
                    App.Core.CoroutineRunner.Run(TryAutoLoginWithRefreshToken());
                }
                else
                {
                    // refresh token이 없으면 체크 완료로 표시
                    hasCheckedRefreshToken = true;
                }
            }
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
        }

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
            if (oidcAuthenticator == null)
            {
                oidcAuthenticator = gameObject.AddComponent<OidcAuthenticator>();
                
                // OIDC 이벤트 구독
                OidcAuthenticator.OnAuthenticationComplete += OnOidcAuthenticationComplete;
                OidcAuthenticator.OnAuthenticationError += OnOidcAuthenticationError;
                
                Debug.Log("OIDC Authenticator 설정 완료");
            }
        }

        // ==========================================
        // Refresh Token 기반 자동 로그인
        // ==========================================

        private IEnumerator TryAutoLoginWithRefreshToken()
        {
            hasCheckedRefreshToken = true;
            
            // 저장된 Refresh Token 확인
            string refreshToken = PlayerPrefs.GetString("refresh_token", "");
            if (string.IsNullOrEmpty(refreshToken))
            {
                Debug.Log("저장된 Refresh Token이 없습니다. 수동 로그인 필요");
                yield break;
            }

            SetStatusText("자동 로그인 시도 중...", MessagePriority.Info);
            SetLoadingState(true);
            isAuthenticating = true;

            // Refresh Token으로 자동 로그인 시도 (ValidateToken 사용)
            if (HttpApiClient.Instance != null)
            {
                Debug.Log("저장된 토큰으로 자동 로그인 시도");
                string accessToken = PlayerPrefs.GetString("access_token", "");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    HttpApiClient.Instance.ValidateToken();
                }
            }

            yield return new WaitForSeconds(0.5f);

            // 자동 로그인 실패 시 UI 복구
            if (isAuthenticating)
            {
                Debug.Log("자동 로그인 실패 - 수동 로그인 필요");
                SetStatusText("로그인이 필요합니다", MessagePriority.Warning);
                SetLoadingState(false);
                isAuthenticating = false;
                
                // 개발용 테스트 계정 자동 채우기
                if (IsTestModeEnabled)
                {
                    if (usernameInput != null) usernameInput.text = testUsername;
                    if (passwordInput != null) passwordInput.text = testPassword;
                }
            }
        }

        // ==========================================
        // 버튼 이벤트 핸들러
        // ==========================================

        public void OnLoginButtonClicked()
        {
            if (isAuthenticating) return;

            string username = usernameInput?.text?.Trim();
            string password = passwordInput?.text;

            // 입력 검증
            if (string.IsNullOrEmpty(username))
            {
                SetStatusText("사용자명을 입력해주세요", MessagePriority.Warning);
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                SetStatusText("비밀번호를 입력해주세요", MessagePriority.Warning);
                return;
            }

            // ID/PW 로그인 수행
            PerformHttpLogin(username, password);
        }


        public void OnSocialLoginButtonClicked(int buttonIndex)
        {
            if (isAuthenticating) return;

            // 버튼 인덱스에 따른 소셜 로그인
            string provider = "unknown";
            switch (buttonIndex)
            {
                case 0: provider = "google"; break;
                case 1: provider = "facebook"; break;
                // 추가 소셜 로그인 제공자들...
                default: 
                    SetStatusText("지원하지 않는 로그인 방식입니다", MessagePriority.Warning);
                    return;
            }

            PerformOAuthLogin(provider);
        }

        public void OnRegisterButtonClicked()
        {
            // 웹 애플리케이션으로 리다이렉트
            SetStatusText("웹 브라우저에서 회원가입을 진행해주세요", MessagePriority.Info);
            var registerUrl = $"{EnvironmentConfig.WebServerUrl}/register";
            Application.OpenURL(registerUrl);
            Debug.Log($"회원가입 페이지 열기: {registerUrl}");
        }

        // ==========================================
        // 로그인 수행 메서드
        // ==========================================

        private void PerformHttpLogin(string username, string password)
        {
            isAuthenticating = true;
            SetStatusText($"로그인 중: {username}", MessagePriority.Info);
            SetLoadingState(true);

            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.Login(username, password);
                Debug.Log($"ID/PW 로그인 요청 전송: {username}");
            }
            else
            {
                Debug.LogError("HttpApiClient 인스턴스가 없습니다");
                OnLoginFailed("네트워크 오류가 발생했습니다");
            }
        }

        private void PerformOAuthLogin(string provider)
        {
            if (oidcAuthenticator == null || !oidcAuthenticator.IsReady())
            {
                SetStatusText("OAuth 설정을 확인하는 중입니다...", MessagePriority.Warning);
                return;
            }

            isAuthenticating = true;
            SetStatusText($"{provider.ToUpper()} 계정으로 로그인 중...", MessagePriority.Info);
            SetLoadingState(true);

            // OIDC Authorization Code Flow 시작
            oidcAuthenticator.StartAuthentication((success, message, tokenResponse) =>
            {
                if (success && tokenResponse != null)
                {
                    OnOAuthLoginSuccess(tokenResponse.access_token, tokenResponse.refresh_token);
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
                    SetStatusText("OAuth 인증이 필요합니다. Google 계정으로 로그인합니다...", MessagePriority.Info);
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
                SetStatusText("Google 로그인 버튼이 설정되지 않았습니다", MessagePriority.Error);
            }
        }

        private void OnOidcAuthenticationComplete(bool success, string message, OidcAuthenticator.TokenResponse tokens)
        {
            isAuthenticating = false;
            SetLoadingState(false);

            if (success && tokens != null)
            {
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

        // ==========================================
        // 로그인 결과 처리
        // ==========================================

        private void OnLoginSuccess(string message, string token)
        {
            SetStatusText("로그인 성공!", MessagePriority.Success);
            
            Debug.Log($"로그인 성공 - {message}");

            // 게임 메인 화면으로 전환
            App.Core.CoroutineRunner.Run(NavigateToMainAfterDelay());
        }

        private void OnOAuthLoginSuccess(string accessToken, string refreshToken)
        {
            // OAuth 토큰 저장
            PlayerPrefs.SetString("access_token", accessToken);
            PlayerPrefs.SetString("refresh_token", refreshToken);
            PlayerPrefs.Save();

            SetStatusText("OAuth 로그인 성공!", MessagePriority.Success);
            Debug.Log("OAuth 로그인 성공 - 토큰 저장 완료");

            // HttpApiClient에 토큰 설정 (userId는 임시로 0 사용)
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.SetAuthToken(accessToken, 0);
                // 추가로 사용자 정보 요청할 수 있음
            }

            // 게임 메인 화면으로 전환
            App.Core.CoroutineRunner.Run(NavigateToMainAfterDelay());
        }

        private void OnLoginFailed(string errorMessage)
        {
            SetStatusText($"로그인 실패: {errorMessage}", MessagePriority.Error);
            Debug.LogWarning($"로그인 실패: {errorMessage}");

            // 유효하지 않은 refresh token 삭제
            if (errorMessage.Contains("refresh") || errorMessage.Contains("token"))
            {
                PlayerPrefs.DeleteKey("refresh_token");
                PlayerPrefs.DeleteKey("access_token");
                PlayerPrefs.Save();
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
            if (IsTestModeEnabled && !hasCheckedRefreshToken)
            {
                if (usernameInput != null) usernameInput.text = testUsername;
                if (passwordInput != null) passwordInput.text = testPassword;
            }

            SetLoadingState(false);
            SetStatusText("로그인 정보를 입력하세요", MessagePriority.Info);
        }

        private void SetStatusText(string message, MessagePriority priority)
        {
            if (statusText != null)
            {
                statusText.text = message;
                
                // 우선순위에 따른 색상 변경
                switch (priority)
                {
                    case MessagePriority.Success:
                        statusText.color = Color.green;
                        break;
                    case MessagePriority.Warning:
                        statusText.color = Color.yellow;
                        break;
                    case MessagePriority.Error:
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
        // 유틸리티
        // ==========================================

        public enum MessagePriority
        {
            Info,
            Success,
            Warning,
            Error,
            Debug
        }
    }
}