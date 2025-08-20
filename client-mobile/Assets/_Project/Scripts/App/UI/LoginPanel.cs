using UnityEngine;
using UnityEngine.UI;
using TMPro;
using App.Network;
using Features.Single.Core;
using Shared.UI;
using App.Core;
namespace App.UI
{
    public class LoginPanel : Shared.UI.PanelBase
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Button loginButton;
        [SerializeField] private Button registerButton;
        [SerializeField] private Button testLoginButton; // 개발용 테스트 로그인 버튼
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject loadingIndicator;

        [Header("OAuth 설정")]
        [SerializeField] private string oauthRegisterUrl = "https://your-website.com/oauth/register";
        [SerializeField] private bool useOAuthForRegister = true;

        [Header("개발용 설정")]
        [SerializeField] private bool enableTestMode = false;
        [SerializeField] private string testUsername = "testuser";
        [SerializeField] private string testPassword = "testpass";

        // 상태 관리  
        private bool isAuthenticating = false;
        private bool isNetworkEventsSetup = false;

        protected override void Awake()
        {
            base.Awake();
            // LoginPanel은 게임의 첫 진입점이므로 시작시 활성화
            // startActive 제거됨 - PanelBase에서 더 이상 사용하지 않음
            Debug.Log("LoginPanel startActive = true로 설정");
        }

        protected override void Start()
        {
            base.Start();
            Debug.Log("LoginPanel 초기화 완료");

            // 인스펙터 할당 버튼 이벤트 연결
            SetupButtons();

            // 네트워크 이벤트 구독 (모드별)
            SetupNetworkEvents();

            // 상태 UI 초기화
            UpdateUI();

            // 기존 캐시된 사용자 확인
            CheckCachedUser();
        }

        void OnEnable()
        {
            // HTTP 기반이므로 자동 연결 불필요
            Debug.Log("LoginPanel 활성화 - HTTP API 모드");
            UpdateUI();
        }

        void OnDestroy()
        {
            // HTTP API 이벤트 구독 해제
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.OnAuthResponse -= OnHttpAuthResponse;
                HttpApiClient.Instance.OnUserInfoReceived -= OnHttpUserInfoReceived;
                HttpApiClient.Instance.OnUserProfileReceived -= OnHttpUserProfileReceived; // 🔥 추가: 프로필 이벤트 구독 해제
            }
        }

        // ========================================
        // 초기화
        // ========================================

        /// <summary>
        /// 인스펙터에서 할당된 버튼들의 이벤트 연결
        /// </summary>
        private void SetupButtons()
        {
            if (loginButton != null)
            {
                loginButton.onClick.AddListener(OnLoginButtonClicked);
                Debug.Log("로그인 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogWarning("loginButton이 인스펙터에서 할당되지 않았습니다!");
            }

            if (registerButton != null)
            {
                registerButton.onClick.AddListener(OnRegisterButtonClicked);
                Debug.Log("회원가입 버튼 이벤트 연결 완료");
            }

            // 개발용 테스트 로그인 버튼
            if (testLoginButton != null)
            {
                testLoginButton.onClick.AddListener(OnTestLoginButtonClicked);
                testLoginButton.gameObject.SetActive(enableTestMode);
                Debug.Log($"테스트 로그인 버튼 설정: {enableTestMode}");
            }

            Debug.Log("LoginPanel 버튼 설정 완료");
        }

        /// <summary>
        /// HTTP API 이벤트 구독
        /// </summary>
        private void SetupNetworkEvents()
        {
            // 이미 이벤트가 구독되어 있으면 중복 방지
            if (isNetworkEventsSetup)
                return;

            // HttpApiClient 인스턴스 생성 시도
            if (HttpApiClient.Instance == null)
            {
                CreateHttpApiClientIfNeeded();
            }

            // HTTP API 이벤트 구독
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.OnAuthResponse += OnHttpAuthResponse;
                HttpApiClient.Instance.OnUserInfoReceived += OnHttpUserInfoReceived;
                HttpApiClient.Instance.OnUserProfileReceived += OnHttpUserProfileReceived; // 🔥 추가: 프로필 이벤트 구독
                isNetworkEventsSetup = true;
                Debug.Log("HttpApiClient 이벤트 구독 완료");
            }
            else
            {
                Debug.LogWarning("HttpApiClient 생성 실패. 네트워크 기능이 비활성화됩니다.");
            }
        }

        /// <summary>
        /// HttpApiClient가 없으면 동적으로 생성
        /// </summary>
        private void CreateHttpApiClientIfNeeded()
        {
            if (HttpApiClient.Instance == null)
            {
                // 새로운 루트 GameObject 생성
                GameObject httpClientObj = new GameObject("HttpApiClient");
                httpClientObj.AddComponent<HttpApiClient>();

                Debug.Log("HttpApiClient 동적 생성 완료");
            }
        }

        // ========================================
        // 인증 처리 (HTTP API)
        // ========================================

        /// <summary>
        /// 로그인 버튼 클릭
        /// </summary>
        public void OnLoginButtonClicked()
        {
            if (isAuthenticating)
                return;

            string username = usernameInput?.text?.Trim();
            string password = passwordInput?.text;

            // 입력 검증
            if (string.IsNullOrEmpty(username))
            {
                SetStatusText("사용자명을 입력해주세요.", MessagePriority.Warning);
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                SetStatusText("비밀번호를 입력해주세요.", MessagePriority.Warning);
                return;
            }

            // HTTP 로그인 시도
            PerformHttpLogin(username, password);
        }

        /// <summary>
        /// 회원가입 버튼 클릭 (OAuth 웹사이트 리다이렉트)
        /// </summary>
        public void OnRegisterButtonClicked()
        {
            if (useOAuthForRegister)
            {
                // OAuth 웹사이트로 리다이렉트
                SetStatusText("웹 브라우저에서 회원가입을 진행해주세요.", MessagePriority.Info);
                UnityEngine.Application.OpenURL(oauthRegisterUrl);
                Debug.Log($"OAuth 회원가입 페이지 열기: {oauthRegisterUrl}");
            }
            else
            {
                SetStatusText("회원가입 기능이 비활성화되어 있습니다.", MessagePriority.Warning);
            }
        }

        /// <summary>
        /// 개발용 테스트 로그인 버튼 클릭
        /// </summary>
        public void OnTestLoginButtonClicked()
        {
            if (enableTestMode)
            {
                // 테스트 계정으로 입력 필드 자동 채우기
                if (usernameInput != null) usernameInput.text = testUsername;
                if (passwordInput != null) passwordInput.text = testPassword;

                SetStatusText($"테스트 계정으로 로그인 시도: {testUsername}", MessagePriority.Debug);

                // 자동 로그인 실행
                PerformHttpLogin(testUsername, testPassword);
            }
        }

        /// <summary>
        /// HTTP 로그인 수행
        /// </summary>
        private void PerformHttpLogin(string username, string password)
        {
            isAuthenticating = true;
            SetStatusText($"로그인 중: {username}", MessagePriority.Info);
            SetLoadingState(true);

            // HttpApiClient 확인 및 생성
            if (HttpApiClient.Instance == null)
            {
                CreateHttpApiClientIfNeeded();
                SetupNetworkEvents(); // 이벤트 재구독
            }

            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.Login(username, password);
                Debug.Log($"HTTP 로그인 요청 전송: {username}");
            }
            else
            {
                OnHttpAuthResponse(false, "HTTP API 클라이언트 초기화 실패", null);
                Debug.LogError("HttpApiClient 생성 실패");
            }
        }

        // ========================================
        // 네트워크 이벤트 핸들러
        // ========================================


        /// <summary>
        /// HTTP 인증 응답 처리
        /// </summary>
        private void OnHttpAuthResponse(bool success, string message, string token)
        {
            isAuthenticating = false;
            SetLoadingState(false);

            Debug.Log($"LoginPanel - HTTP 인증 응답: {success}, {message}");

            if (success)
            {
                SetStatusText($"로그인 성공: {message}", MessagePriority.Success);
                // HttpApiClient의 Login 메서드에서 UserInfo가 자동으로 전달됨
                // OnUserInfoReceived 이벤트가 발생하면 OnHttpUserInfoReceived에서 처리
            }
            else
            {
                // HTTP 오류 코드별 친화적 메시지 제공
                string friendlyMessage = GetFriendlyErrorMessage(message);
                SetStatusText($"로그인 실패: {friendlyMessage}", MessagePriority.Error);

                // 개발용 테스트 모드 힌트
                if (enableTestMode)
                {
                    SetStatusText($"개발 모드: {testUsername}/{testPassword} 사용 가능", MessagePriority.Debug);
                }
            }
        }

        /// <summary>
        /// HTTP 오류 메시지를 사용자 친화적으로 변환
        /// </summary>
        private string GetFriendlyErrorMessage(string originalMessage)
        {
            if (originalMessage.Contains("401"))
            {
                return "아이디 또는 비밀번호가 올바르지 않습니다.";
            }
            else if (originalMessage.Contains("404"))
            {
                return "서버를 찾을 수 없습니다. 관리자에게 문의하세요.";
            }
            else if (originalMessage.Contains("500"))
            {
                return "서버 내부 오류가 발생했습니다. 나중에 다시 시도해주세요.";
            }
            else if (originalMessage.Contains("Connection"))
            {
                return "서버에 연결할 수 없습니다. 네트워크 상태를 확인해주세요.";
            }
            else
            {
                return originalMessage; // 원본 메시지 반환
            }
        }

        /// <summary>
        /// HTTP 사용자 정보 수신 처리 (로그인 기본 정보만)
        /// </summary>
        private void OnHttpUserInfoReceived(HttpApiClient.AuthUserData authUserData)
        {
            Debug.Log("============= OnHttpUserInfoReceived 호출됨 =============");

            if (authUserData != null)
            {
                Debug.Log($"[LoginPanel] 로그인 성공: {authUserData.user.username}");

                // UserDataCache 상태 확인
                Debug.Log($"[LoginPanel] UserDataCache.Instance: {Features.Single.Core.UserDataCache.Instance}");

                // 🔥 임시 수정: UserDataCache 의존성 제거하고 즉시 화면 전환
                Debug.Log("[LoginPanel] UserDataCache 의존성 건너뛰고 즉시 화면 전환 시도");

                // UserDataCache가 있으면 사용하고, 없어도 진행
                if (Features.Single.Core.UserDataCache.Instance != null)
                {
                    Debug.Log("[LoginPanel] UserDataCache 발견! SetAuthToken 호출");
                    Features.Single.Core.UserDataCache.Instance.SetAuthToken(authUserData.token, authUserData.user.username);
                }
                else
                {
                    Debug.LogWarning("[LoginPanel] UserDataCache 없음 - SingleCore 씬 미로드 상태, 일단 화면 전환 진행");
                }

                SessionManager.Instance?.SeedFromAuth(authUserData);  // ★ 이 한 줄이 핵심

                SetStatusText("로그인 완료!", MessagePriority.Success);
                ProceedToNextScreen();
                return; // 조기 반환
            }
            else
            {
                Debug.LogWarning("[LoginPanel] 사용자 정보 수신 실패");
                SetStatusText("사용자 정보를 가져올 수 없습니다.", MessagePriority.Error);
            }

            Debug.Log("============= OnHttpUserInfoReceived 완료 =============");
        }

        /// <summary>
        /// HTTP 프로필 정보 수신 처리 (상세 프로필)
        /// </summary>
        private void OnHttpUserProfileReceived(HttpApiClient.UserProfile userProfile)
        {
            if (userProfile != null)
            {
                Debug.Log($"LoginPanel - 프로필 데이터 수신: {userProfile.username} (최대 스테이지: {userProfile.max_stage_completed})");

                // 🔥 수정: UserDataCache가 자동으로 OnUserProfileReceived 이벤트를 구독하므로 중복 호출 제거
                // UserDataCache.OnUserProfileReceived에서 자동으로 SetUserProfile() 및 LoadInitialDataFromServer() 호출됨

                SetStatusText($"환영합니다, {userProfile.username}님!", MessagePriority.Success);

                // 즉시 다음 화면으로 전환 (딜레이 제거)
                ProceedToNextScreen();
            }
            else
            {
                Debug.LogWarning("LoginPanel - 프로필 정보 수신 실패");
                SetStatusText("프로필 정보를 가져올 수 없습니다.", MessagePriority.Error);
            }
        }

        /// <summary>
        /// 다음 화면으로 진행
        /// </summary>
        private void ProceedToNextScreen()
        {
            Debug.Log("======== 로그인 완료 - 모드 선택 화면으로 이동 ========");

            // UIManager 상태 확인
            Debug.Log($"[LoginPanel] UIManager.Instance 값: {UIManager.Instance}");
            Debug.Log($"[LoginPanel] FindObjectOfType<UIManager> 결과: {Object.FindObjectOfType<UIManager>()}");

            // UIManager 사용으로 변경
            UIManager uiManager = UIManager.GetInstanceSafe();
            Debug.Log($"[LoginPanel] UIManager.GetInstanceSafe() 결과: {uiManager}");

            if (uiManager != null)
            {
                Debug.Log("[LoginPanel] UIManager 발견! OnLoginSuccess() 호출");
                try
                {
                    uiManager.OnLoginSuccess();
                    Debug.Log("[LoginPanel] UIManager.OnLoginSuccess() 호출 완료");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LoginPanel] OnLoginSuccess() 호출 중 오류: {ex.Message}");
                    SetStatusText("화면 전환 중 오류가 발생했습니다.", MessagePriority.Error);
                }
            }
            else
            {
                Debug.LogError("[LoginPanel] UIManager를 찾을 수 없습니다!");
                SetStatusText("UI 매니저를 찾을 수 없어 화면 전환에 실패했습니다.", MessagePriority.Error);

                // 폴백: MainScene 새로고침 시도
                Debug.Log("[LoginPanel] 폴백: MainScene 새로고침 시도");
                StartCoroutine(RefreshMainSceneAfterDelay());
            }
        }

        /// <summary>
        /// 폴백: MainScene 새로고침 시도
        /// </summary>
        private System.Collections.IEnumerator RefreshMainSceneAfterDelay()
        {
            yield return new WaitForSeconds(1.0f);

            Debug.Log("[LoginPanel] MainScene 새로고침 시도");

            // Try to find UIManager again
            UIManager uiManager = Object.FindObjectOfType<UIManager>();
            if (uiManager != null)
            {
                Debug.Log("[LoginPanel] 새로고침 후 UIManager 발견! OnLoginSuccess() 호출");
                uiManager.OnLoginSuccess();
            }
            else
            {
                Debug.LogError("[LoginPanel] 새로고침 후에도 UIManager를 찾을 수 없습니다.");
                SetStatusText("화면 전환에 실패했습니다. 게임을 재시작해주세요.", MessagePriority.Error);
            }
        }

        // ========================================
        // 캐시된 사용자 처리
        // ========================================

        /// <summary>
        /// 캐시된 사용자 정보 확인
        /// </summary>
        private void CheckCachedUser()
        {
            if (Features.Single.Core.UserDataCache.Instance != null && Features.Single.Core.UserDataCache.Instance.IsLoggedIn())
            {
                var cachedUser = Features.Single.Core.UserDataCache.Instance.GetCurrentUser();
                Debug.Log($"캐시된 사용자 발견: {cachedUser.username}");

                SetStatusText($"이전 로그인: {cachedUser.username}", MessagePriority.Info);

                // 자동 로그인을 위해 사용자명 필드 채우기
                if (usernameInput != null)
                {
                    usernameInput.text = cachedUser.username;
                }

                // 토큰이 있으면 자동 진행 시도 (여기서는 수동 로그인 필요)
                // 실제 구현에서는 토큰 유효성을 서버에 확인해야 함
            }
        }

        // ========================================
        // UI 상태 관리
        // ========================================

        /// <summary>
        /// UI 상태 업데이트
        /// </summary>
        private void UpdateUI()
        {
            // HTTP 기반이므로 연결 상태 확인 불필요
            bool canAuth = !isAuthenticating;

            // 버튼 활성화 상태
            if (loginButton != null) loginButton.interactable = canAuth;
            if (registerButton != null) registerButton.interactable = canAuth;
            if (testLoginButton != null) testLoginButton.interactable = canAuth && enableTestMode;

            // 입력 필드 활성화 상태
            if (usernameInput != null) usernameInput.interactable = canAuth;
            if (passwordInput != null) passwordInput.interactable = canAuth;
        }

        /// <summary>
        /// 상태 텍스트 설정 (SystemMessageManager 사용)
        /// </summary>
        private void SetStatusText(string text, MessagePriority priority = MessagePriority.Info)
        {
            // 로컬 상태 텍스트도 업데이트 (백업용)
            if (statusText != null)
            {
                statusText.text = text;
            }

            // SystemMessageManager로 Toast 표시
            if (SystemMessageManager.Instance != null)
            {
                float duration = GetDurationByPriority(priority);
                SystemMessageManager.ShowToast(text, priority, duration);
            }
            else
            {
                Debug.LogWarning("SystemMessageManager가 초기화되지 않았습니다.");
            }

            Debug.Log($"LoginPanel 상태: [{priority}] {text}");
        }

        /// <summary>
        /// 우선순위에 따른 Toast 지속시간 결정
        /// </summary>
        private float GetDurationByPriority(MessagePriority priority)
        {
            switch (priority)
            {
                case MessagePriority.Debug:
                    return 2f;
                case MessagePriority.Info:
                    return 3f;
                case MessagePriority.Success:
                    return 3f;
                case MessagePriority.Warning:
                    return 4f;
                case MessagePriority.Error:
                    return 5f;
                case MessagePriority.Critical:
                    return 6f;
                default:
                    return 3f;
            }
        }

        /// <summary>
        /// 로딩 상태 설정
        /// </summary>
        private void SetLoadingState(bool isLoading)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(isLoading);
            }
        }
    }
}