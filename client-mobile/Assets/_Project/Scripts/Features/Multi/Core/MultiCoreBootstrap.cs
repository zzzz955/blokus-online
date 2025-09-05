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
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private MultiUserDataCache multiUserDataCache;
        
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

            ShowLoadingUI();
            InitializeManagers();
            StartCoroutine(InitializeMulticoreDataCoroutine());
        }

        void OnDestroy()
        {
            if (debugMode)
                Debug.Log("[MultiCoreBootstrap] OnDestroy - Cleaning up MultiCore");

            CleanupManagers();
            Instance = null;
        }

        // ========================================
        // Initialization
        // ========================================

        private void FindManagers()
        {
            if (networkManager == null)
                networkManager = FindObjectOfType<NetworkManager>();

            if (multiUserDataCache == null)
                multiUserDataCache = FindObjectOfType<MultiUserDataCache>();

            if (loadingOverlay == null)
                loadingOverlay = FindObjectOfType<LoadingOverlay>();

            // Validate required managers
            if (networkManager == null)
            {
                Debug.LogError("[MultiCoreBootstrap] NetworkManager not found in scene!", this);
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
            if (networkManager != null)
            {
                networkManager.Initialize();
                if (debugMode)
                    Debug.Log("[MultiCoreBootstrap] NetworkManager initialized");
            }

            // MultiUserDataCache 초기화 (만약 필요하다면)
            if (multiUserDataCache != null)
            {
                multiUserDataCache.Initialize();
                if (debugMode)
                    Debug.Log("[MultiCoreBootstrap] MultiUserDataCache initialized");
            }
        }

        private void CleanupManagers()
        {
            if (debugMode)
                Debug.Log("[MultiCoreBootstrap] Cleaning up managers...");

            if (networkManager != null)
                networkManager.Cleanup();

            if (multiUserDataCache != null)
                multiUserDataCache.Cleanup();
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
            if (networkManager == null)
            {
                Debug.LogError("[MultiCoreBootstrap] NetworkManager가 없습니다.");
                yield break;
            }

            if (loadingOverlay != null)
                LoadingOverlay.Show("TCP 서버 연결 중...");

            // 서버 연결 시도
            bool connectionResult = false;
            bool connectionComplete = false;

            networkManager.OnConnectionChanged += (connected) => {
                connectionResult = connected;
                connectionComplete = true;
                isNetworkConnected = connected;
            };

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
            if (loadingOverlay != null)
                LoadingOverlay.Show("사용자 인증 중...");

            // SessionManager에서 refreshToken 가져오기
            if (SessionManager.Instance == null || !SessionManager.Instance.IsLoggedIn)
            {
                Debug.LogError("[MultiCoreBootstrap] SessionManager 로그인 상태가 아님");
                yield break;
            }

            // refreshToken 가져오기 (실제 구현은 SessionManager API 확인 필요)
            string refreshToken = GetRefreshTokenFromSession();
            
            if (string.IsNullOrEmpty(refreshToken))
            {
                Debug.LogError("[MultiCoreBootstrap] refreshToken을 가져올 수 없음");
                yield break;
            }

            // JWT 로그인 요청
            bool authResult = false;
            bool authComplete = false;

            networkManager.OnAuthResponse += (success, message) => {
                authResult = success;
                authComplete = true;
                isAuthenticated = success;
                
                if (debugMode)
                    Debug.Log($"[MultiCoreBootstrap] 인증 응답: {success}, {message}");
            };

            networkManager.JwtLogin(refreshToken);

            // 인증 완료까지 대기 (최대 5초)
            float timeout = 5f;
            while (!authComplete && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }

            if (!authComplete || !authResult)
            {
                Debug.LogError("[MultiCoreBootstrap] 사용자 인증 실패");
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
            if (loadingOverlay != null)
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

            // MultiGameplayScene으로 전환
            TransitionToGameplayScene();
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

        private string GetRefreshTokenFromSession()
        {
            if (SessionManager.Instance != null && SessionManager.Instance.IsLoggedIn)
            {
                // OidcAuthenticator를 통해 실제 refreshToken 가져오기
                var oidcAuthenticator = FindObjectOfType<App.Network.OidcAuthenticator>();
                if (oidcAuthenticator != null)
                {
                    return oidcAuthenticator.GetRefreshToken();
                }
                
                Debug.LogWarning("[MultiCoreBootstrap] OidcAuthenticator를 찾을 수 없습니다.");
            }
            
            return null;
        }

        /// <summary>
        /// 연결 실패 시 MainScene으로 복귀 및 에러 메시지 처리
        /// </summary>
        private void HandleConnectionFailure(string errorMessage)
        {
            Debug.LogError($"[MultiCoreBootstrap] 연결 실패: {errorMessage}");
            
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
            return networkManager;
        }
    }
}