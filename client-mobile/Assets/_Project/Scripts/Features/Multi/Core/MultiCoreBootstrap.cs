using UnityEngine;
using Features.Multi.Net;
using System.Collections;
using App.Core;
using App.UI;
using Shared.UI;

namespace Features.Multi.Core
{
    /// <summary>
    /// MultiCore scene bootstrap manager
    /// 멀티플레이 전용 데이터 로딩 및 초기화 담당
    /// UI 없이 순수 데이터 로딩 전용
    /// </summary>
    public class MultiCoreBootstrap : MonoBehaviour
    {
        [Header("Managers")]
        // MultiUserDataCache 제거됨 - 더 이상 사용되지 않음
        
        // NetworkManager는 DontDestroyOnLoad로 인해 Instance를 통해 접근
        
        [Header("Loading UI")]
        [SerializeField] private LoadingOverlay loadingOverlay;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Singleton for scene-scoped access
        public static MultiCoreBootstrap Instance { get; private set; }

        // 데이터 로딩 상태 관리
        private bool isDataLoaded = false;
        private bool isDataLoading = false;
        private bool isNetworkConnected = false;
        private bool isAuthenticated = false;

        // 이벤트
        public event System.Action OnDataLoadingComplete;
        public event System.Action<string> OnDataLoadingFailed;

        void Awake()
        {
            if (debugMode)
                Debug.Log("[MultiCoreBootstrap] Awake - Initializing MultiCore");

            // Set instance for scene-scoped access
            Instance = this;
            
            FindManagers();
        }

        void Start()
        {
            if (debugMode)
                Debug.Log("[MultiCoreBootstrap] Start - Starting multicore initialization");

            // Audio Listener 중복 문제 해결
            Utilities.AudioListenerManager.FixDuplicateAudioListeners();

            // NetworkManager 에러 이벤트 구독 (인증 실패 감지용)
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnErrorReceived += OnNetworkError;
            }

            ShowLoadingUI();
            InitializeManagers();
            StartCoroutine(InitializeMulticoreDataCoroutine());
        }

        void OnDestroy()
        {
            if (debugMode)
                Debug.Log("[MultiCoreBootstrap] OnDestroy - Cleaning up MultiCore");

            // 모든 코루틴 중지
            StopAllCoroutines();

            // NetworkManager 이벤트 구독 해제 (연결은 끊지 않음)
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnErrorReceived -= OnNetworkError;
            }

            // NetworkManager는 싱글톤이므로 씬 전환 시 연결을 끊지 않음
            CleanupLocalManagers();
            
            // 상태 초기화
            isDataLoading = false;
            isDataLoaded = false;
            isNetworkConnected = false;
            isAuthenticated = false;
            
            Instance = null;
        }

        /// <summary>
        /// NetworkManager 에러 이벤트 핸들러 (인증 실패 감지)
        /// </summary>
        private void OnNetworkError(string errorMessage)
        {
            if (debugMode)
                Debug.Log($"[MultiCoreBootstrap] 네트워크 에러 감지: {errorMessage}");

            // 인증 관련 에러인지 확인
            if (errorMessage.Contains("인증 토큰이 유효하지 않습니다") ||
                errorMessage.Contains("authentication") ||
                errorMessage.Contains("토큰"))
            {
                Debug.LogError($"[MultiCoreBootstrap] 인증 실패 감지: {errorMessage}");
                
                // 인증 실패 상태로 설정
                isAuthenticated = false;
                
                // MainScene으로 복귀 (MultiGameplayScene으로 가지 않음)
                HandleConnectionFailure("인증에 실패하여 메인 화면으로 돌아갑니다.");
            }
        }

        // ========================================
        // Initialization
        // ========================================

        private void FindManagers()
        {
            // MultiUserDataCache 제거됨

            if (loadingOverlay == null)
                loadingOverlay = FindObjectOfType<LoadingOverlay>();

            // NetworkManager는 Instance를 통해 접근하므로 별도 검증
            if (NetworkManager.Instance == null)
            {
                Debug.LogError("[MultiCoreBootstrap] NetworkManager Instance not found!", this);
            }
            else if (debugMode)
            {
                Debug.Log("[MultiCoreBootstrap] NetworkManager Instance found successfully");
            }
        }

        private void ShowLoadingUI()
        {
            LoadingOverlay.Show("멀티플레이 데이터 로딩 중...");
        }

        private void HideLoadingUI()
        {
            LoadingOverlay.Hide();
        }

        private void InitializeManagers()
        {
            if (debugMode)
                Debug.Log("[MultiCoreBootstrap] Initializing managers...");

            // NetworkManager 초기화
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.Initialize();
                if (debugMode)
                    Debug.Log("[MultiCoreBootstrap] NetworkManager initialized");
            }

            // MultiUserDataCache 제거됨
        }

        private void CleanupLocalManagers()
        {
            if (debugMode)
                Debug.Log("[MultiCoreBootstrap] Cleaning up local managers...");

            // NetworkManager는 싱글톤이므로 여기서 정리하지 않음 (연결 유지)
            // if (NetworkManager.Instance != null)
            //     NetworkManager.Instance.Cleanup();

            // MultiUserDataCache 제거됨
        }

        // ========================================
        // Data Loading Process
        // ========================================

        private IEnumerator InitializeMulticoreDataCoroutine()
        {
            if (debugMode)
                Debug.Log("[MultiCoreBootstrap] Starting multicore data loading...");

            isDataLoading = true;
            isDataLoaded = false;

            // Step 1: TCP 서버 연결
            yield return StartCoroutine(ConnectToTcpServerCoroutine());
            
            if (!isNetworkConnected)
            {
                OnDataLoadingFailed?.Invoke("TCP 서버 연결 실패");
                yield break;
            }

            // Step 2: JWT 토큰 기반 인증
            yield return StartCoroutine(AuthenticateWithTokenCoroutine());
            
            if (!isAuthenticated)
            {
                OnDataLoadingFailed?.Invoke("사용자 인증 실패");
                yield break;
            }

            // Step 3: 멀티플레이 데이터 로딩 (사용자 통계, 랭킹 등)
            yield return StartCoroutine(LoadMultiplayerDataCoroutine());

            // 로딩 완료
            CompleteDataLoading();
        }

        private IEnumerator ConnectToTcpServerCoroutine()
        {
            if (NetworkManager.Instance == null)
            {
                Debug.LogError("[MultiCoreBootstrap] NetworkManager Instance가 없습니다.");
                yield break;
            }

            var networkManager = NetworkManager.Instance;

            // 이미 연결되어 있는지 확인
            if (networkManager.IsConnected())
            {
                Debug.Log("[MultiCoreBootstrap] 이미 TCP 서버에 연결되어 있음");
                isNetworkConnected = true;
                yield break;
            }

            // LoadingOverlay 표시 (정적 메소드 사용)
            LoadingOverlay.Show("TCP 서버 연결 중...");
            Debug.Log("[MultiCoreBootstrap] LoadingOverlay 표시됨");

            // 서버 연결 시도
            bool connectionResult = false;
            bool connectionComplete = false;

            networkManager.OnConnectionChanged += (connected) => {
                connectionResult = connected;
                connectionComplete = true;
                isNetworkConnected = connected;
                Debug.Log($"[MultiCoreBootstrap] 연결 상태 변경: {connected}");
            };

            Debug.Log($"[MultiCoreBootstrap] 서버 연결 시도: {networkManager.GetStatusInfo()}");
            
            // NetworkSetup에서 설정된 서버 정보 재확인 및 동기화
            var networkSetup = FindObjectOfType<NetworkSetup>();
            if (networkSetup != null)
            {
                Debug.Log("[MultiCoreBootstrap] NetworkSetup 발견 - 서버 정보 동기화 확인");
                // NetworkSetup이 이미 서버 정보를 설정했을 것임
            }
            else
            {
                Debug.LogWarning("[MultiCoreBootstrap] NetworkSetup이 없음 - 기본 서버 정보 사용");
            }
            
            networkManager.ConnectToServer();

            // 연결 완료까지 대기 (최대 10초)
            float timeout = 10f;
            while (!connectionComplete && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }

            if (!connectionComplete || !connectionResult)
            {
                Debug.LogError("[MultiCoreBootstrap] TCP 서버 연결 실패");
                isNetworkConnected = false;
                
                // 연결 실패 시 MainScene으로 복귀
                HandleConnectionFailure("멀티플레이어 서버 연결에 실패했습니다.");
                yield break;
            }
            else if (debugMode)
            {
                Debug.Log("[MultiCoreBootstrap] TCP 서버 연결 성공");
            }
        }

        private IEnumerator AuthenticateWithTokenCoroutine()
        {
            // NetworkManager 인스턴스 가져오기
            var networkManager = NetworkManager.Instance;
            if (networkManager == null)
            {
                Debug.LogError("[MultiCoreBootstrap] NetworkManager Instance가 없습니다.");
                yield break;
            }
            
            // 이미 인증된 상태인지 확인
            if (networkManager.IsAuthenticated())
            {
                Debug.Log("[MultiCoreBootstrap] 이미 사용자 인증 완료됨");
                isAuthenticated = true;
                yield break;
            }

            if (loadingOverlay != null)
                LoadingOverlay.Show("사용자 인증 중...");

            // SessionManager에서 refreshToken 가져오기
            if (SessionManager.Instance == null || !SessionManager.Instance.IsLoggedIn)
            {
                Debug.LogError("[MultiCoreBootstrap] SessionManager 로그인 상태가 아님");
                yield break;
            }

            // accessToken 가져오기 (만료된 경우 자동 갱신됨)
            string accessToken = GetAccessTokenFromSession();
            string clientId = "unity-mobile-client"; // OIDC 클라이언트 ID

            // 고급 연결/인증 방식 사용 (토큰 자동 갱신 포함)
            bool connectionResult = false;
            bool connectionComplete = false;

            // 비동기 작업을 코루틴에서 처리
            StartCoroutine(ConnectWithTokenRefreshCoroutine(accessToken, clientId, (success) => {
                connectionResult = success;
                connectionComplete = true;
                isAuthenticated = success;
            }));

            // 연결 완료까지 대기 (최대 10초)
            float timeout = 10f;
            while (!connectionComplete && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }

            if (!connectionComplete || !connectionResult)
            {
                Debug.LogError("[MultiCoreBootstrap] 사용자 인증 실패 (토큰 갱신 포함)");
                isAuthenticated = false;
                
                // 인증 실패 시 MainScene으로 복귀
                HandleConnectionFailure("사용자 인증에 실패했습니다.");
                yield break;
            }
            else if (debugMode)
            {
                Debug.Log("[MultiCoreBootstrap] 사용자 인증 성공");
            }
        }

        private IEnumerator LoadMultiplayerDataCoroutine()
        {
            LoadingOverlay.Show("멀티플레이 데이터 로딩 중...");

            // 멀티플레이 전용 데이터 로딩 (사용자 통계, 랭킹 등)
            // TODO: 실제 구현 시 필요한 데이터들 로딩
            
            yield return new WaitForSeconds(1f); // 임시 로딩 시뮬레이션
            
            if (debugMode)
                Debug.Log("[MultiCoreBootstrap] 멀티플레이 데이터 로딩 완료");
        }

        private void CompleteDataLoading()
        {
            isDataLoading = false;
            isDataLoaded = true;

            HideLoadingUI();

            if (debugMode)
                Debug.Log("[MultiCoreBootstrap] 🎉 멀티플레이 데이터 로딩 완료!");

            OnDataLoadingComplete?.Invoke();

            // 서버 연결과 인증이 모두 성공했을 때만 MultiGameplayScene으로 전환
            if (isNetworkConnected && isAuthenticated)
            {
                // 성공 시 멀티플레이 버튼 재활성화 (나중에 다시 사용할 수 있도록)
                EnableMultiplayerButton();
                TransitionToGameplayScene();
            }
            else
            {
                string errorMessage = "데이터 로딩이 완료되었으나 서버 연결 또는 인증에 실패했습니다.";
                Debug.LogError($"[MultiCoreBootstrap] {errorMessage}");
                HandleConnectionFailure(errorMessage);
            }
        }

        private void TransitionToGameplayScene()
        {
            if (SceneFlowController.Instance != null)
            {
                // SceneFlowController를 통한 씬 전환 (실제 메서드명 확인 필요)
                SceneFlowController.Instance.StartGoMultiGameplay();
            }
            else
            {
                // 레거시 방식
                UnityEngine.SceneManagement.SceneManager.LoadScene("MultiGameplayScene");
            }
        }

        // ========================================
        // Utility Methods
        // ========================================

        private string GetAccessTokenFromSession()
        {
            if (SessionManager.Instance != null && SessionManager.Instance.IsLoggedIn)
            {
                // OidcAuthenticator를 통해 accessToken 가져오기 (만료된 토큰도 갱신을 위해 전달)
                var oidcAuthenticator = FindObjectOfType<App.Network.OidcAuthenticator>();
                if (oidcAuthenticator != null)
                {
                    // 만료 여부에 상관없이 저장된 토큰을 가져옴 (ConnectToMultiplayerAsync에서 갱신 처리)
                    string storedToken = App.Security.SecureStorage.GetString("oidc_access_token", "");
                    
                    if (!string.IsNullOrEmpty(storedToken))
                    {
                        if (debugMode)
                            Debug.Log("[MultiCoreBootstrap] AccessToken 획득 (만료 여부는 ConnectToMultiplayerAsync에서 처리)");
                        return storedToken;
                    }
                    else
                    {
                        Debug.LogWarning("[MultiCoreBootstrap] 저장된 AccessToken이 없습니다.");
                    }
                }
                else
                {
                    Debug.LogWarning("[MultiCoreBootstrap] OidcAuthenticator를 찾을 수 없습니다.");
                }
            }
            else
            {
                Debug.LogWarning("[MultiCoreBootstrap] SessionManager가 로그인 상태가 아닙니다.");
            }
            
            return null;
        }

        /// <summary>
        /// 토큰 자동 갱신을 포함한 연결/인증 코루틴
        /// </summary>
        private IEnumerator ConnectWithTokenRefreshCoroutine(string accessToken, string clientId, System.Action<bool> onComplete)
        {
            var networkManager = NetworkManager.Instance;
            
            if (debugMode)
                Debug.Log("[MultiCoreBootstrap] 토큰 갱신 포함 연결 시작...");

            // Unity의 Task를 코루틴으로 변환
            var connectTask = networkManager.ConnectToMultiplayerAsync(accessToken, clientId);
            
            // Task 완료까지 대기
            while (!connectTask.IsCompleted)
            {
                yield return new WaitForSeconds(0.1f);
            }

            // 결과 처리
            bool success = false;
            if (connectTask.IsFaulted)
            {
                Debug.LogError($"[MultiCoreBootstrap] 연결 중 예외 발생: {connectTask.Exception?.GetBaseException()?.Message}");
            }
            else
            {
                success = connectTask.Result;
                if (debugMode)
                    Debug.Log($"[MultiCoreBootstrap] 연결 결과: {success}");
            }

            onComplete?.Invoke(success);
        }

        /// <summary>
        /// 연결 실패 시 MainScene으로 복귀 및 에러 메시지 처리
        /// </summary>
        private void HandleConnectionFailure(string errorMessage)
        {
            Debug.LogError($"[MultiCoreBootstrap] 연결 실패: {errorMessage}");
            
            // 멀티플레이 버튼 재활성화
            EnableMultiplayerButton();
            
            // SystemMessageManager로 토스트 메시지 표시
            if (SystemMessageManager.Instance != null)
            {
                SystemMessageManager.ShowToast(errorMessage, MessagePriority.Error);
            }
            else
            {
                Debug.LogError("[MultiCoreBootstrap] SystemMessageManager를 찾을 수 없습니다.");
            }
            
            // MainScene으로 복귀
            StartCoroutine(ReturnToMainScene());
        }

        /// <summary>
        /// MainScene으로 복귀하는 코루틴
        /// </summary>
        private IEnumerator ReturnToMainScene()
        {
            // 약간의 지연 (사용자가 에러 메시지를 볼 수 있도록)
            yield return new WaitForSeconds(2f);
            
            // SceneFlowController를 통한 MainScene 복귀
            if (App.Core.SceneFlowController.Instance != null)
            {
                App.Core.SceneFlowController.Instance.StartExitMultiToMain();
            }
            else
            {
                // 레거시 방식
                Debug.LogWarning("[MultiCoreBootstrap] SceneFlowController를 찾을 수 없어 레거시 방식으로 복귀");
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
            }
        }

        // ========================================
        // Public API
        // ========================================

        public bool IsDataLoaded()
        {
            return isDataLoaded;
        }

        public bool IsDataLoading()
        {
            return isDataLoading;
        }

        public bool IsNetworkConnected()
        {
            return isNetworkConnected;
        }

        public bool IsAuthenticated()
        {
            return isAuthenticated;
        }

        public NetworkManager GetNetworkManager()
        {
            return NetworkManager.Instance;
        }
        
        /// <summary>
        /// 멀티플레이 버튼 재활성화 (연결 실패 시)
        /// </summary>
        private void EnableMultiplayerButton()
        {
            var uiManager = UIManager.GetInstanceSafe();
            if (uiManager != null)
            {
                uiManager.EnableMultiplayerButton();
                Debug.Log("[MultiCoreBootstrap] 멀티플레이 버튼 재활성화 요청");
            }
            else
            {
                Debug.LogWarning("[MultiCoreBootstrap] UIManager를 찾을 수 없어 버튼 재활성화 실패");
            }
        }
    }
}