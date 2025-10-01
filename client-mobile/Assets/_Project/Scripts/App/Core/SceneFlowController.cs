using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using App.UI;
using Shared.UI;
using Features.Single.Gameplay;   // SingleGameManager
using Features.Single.Core;       // SingleCoreBootstrap, StageDataManager
using Features.Single.UI.InGame;

namespace App.Core
{
    /// <summary>
    /// Scene flow controller for additive scene loading and transition management
    /// Migration Plan: 씬 로딩/언로딩/활성 관리 + 로딩 중 입력 잠금 + 인디케이터 표시 + 자동 로그인 체크
    /// </summary>
    public class SceneFlowController : MonoBehaviour
    {
        // Scene name constants
        private const string AppPersistentScene = "AppPersistent";
        private const string MainScene = "MainScene";
        private const string SingleCoreScene = "SingleCore";
        private const string SingleGameplayScene = "SingleGameplayScene";
        private const string MultiCoreScene = "MultiCore";
        private const string MultiGameplayScene = "MultiGameplayScene";

        // Singleton pattern
        public static SceneFlowController Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        //  추가: 자동 로그인 상태
        public enum AutoLoginState
        {
            NotChecked,      // 아직 체크 안함
            InProgress,      // 체크 진행 중
            Success,         // 자동 로그인 성공
            Failed          // 자동 로그인 실패 (로그인 필요)
        }

        public static AutoLoginState CurrentAutoLoginState { get; private set; } = AutoLoginState.NotChecked;
        
        // 중복 실행 방지 플래그
        private static bool isGoMultiInProgress = false;
        private static bool isGoMultiGameplayInProgress = false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);

                if (debugMode)
                    Debug.Log("SceneFlowController initialized with DontDestroyOnLoad");
            }
            else
            {
                if (debugMode)
                    Debug.Log("SceneFlowController duplicate instance destroyed");
                Destroy(gameObject);
            }
        }

        void Start()
        {
            // Auto-boot to MainScene if we're starting from AppPersistent
            if (SceneManager.GetActiveScene().name == AppPersistentScene)
            {
                if (debugMode)
                    Debug.Log("[SceneFlowController] Starting from AppPersistent - auto-booting to MainScene");

                StartCoroutine(BootToMainScene());
            }
        }

        /// <summary>
        /// Boot sequence from AppPersistent to MainScene with auto-login check
        /// </summary>
        private IEnumerator BootToMainScene()
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] Boot sequence started - waiting for systems");

            // Wait for all systems to initialize completely
            yield return new WaitForSeconds(1f);

            // Wait for LoadingOverlay to be ready
            while (LoadingOverlay.Instance == null)
            {
                yield return new WaitForEndOfFrame();
            }

            LoadingOverlay.Show("게임 초기화 중...");
            InputLocker.Enable();

            if (debugMode)
                Debug.Log("[SceneFlowController] Systems ready, continuing boot sequence");

            //  추가: 자동 로그인 체크
            yield return CheckAutoLogin();

            // Wait a moment more for loading overlay to show
            yield return new WaitForSeconds(0.5f);

            // Check if MainScene is already loaded
            Scene mainScene = SceneManager.GetSceneByName(MainScene);
            if (!mainScene.IsValid() || !mainScene.isLoaded)
            {
                if (debugMode)
                    Debug.Log("[SceneFlowController] Loading MainScene additively");

                LoadingOverlay.Show("메인 화면 로드 중...");
                yield return LoadAdditive(MainScene, setActive: true);
            }
            else
            {
                if (debugMode)
                    Debug.Log("[SceneFlowController] MainScene already loaded, setting active");

                SetActive(MainScene);
            }

            // Wait before hiding loading overlay
            yield return new WaitForSeconds(0.3f);

            LoadingOverlay.Hide();
            InputLocker.Disable();

            if (debugMode)
                Debug.Log("[SceneFlowController] Boot sequence completed");
        }

        // ========================================
        // Scene Transition Coroutines
        // ========================================

        /// <summary>
        /// GoSingle: SingleCore(없으면 로드) → SingleGameplayScene 로드 → SingleGameplayScene 활성
        /// 이후 SingleGameManager에 선택된 스테이지를 '명시적'으로 전달 (강제 토글 금지)
        /// </summary>
        public IEnumerator GoSingle(int? stageNumber = null)
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] GoSingle() started");

            LoadingOverlay.Show("싱글플레이 로딩 중...");
            InputLocker.Enable();

            bool success = false;
            string errorMsg = "";

            yield return StartCoroutine(CoGoSingle(stageNumber, (result, error) =>
            {
                success = result;
                errorMsg = error;
            }));

            if (!success && !string.IsNullOrEmpty(errorMsg))
            {
                SystemMessageManager.ShowToast(errorMsg, MessagePriority.Error);
                Debug.LogError($"[SceneFlowController] {errorMsg}");
            }
            else if (debugMode)
            {
                Debug.Log("[SceneFlowController] GoSingle() completed successfully");
            }

            LoadingOverlay.Hide();
            InputLocker.Disable();
        }

        private IEnumerator CoGoSingle(int? stageNumber, System.Action<bool, string> callback)
        {
            // 1) SingleCore 확보 - 사용자 변경 확인
            bool wasAlreadyLoaded = IsSceneLoaded(SingleCoreScene);
            yield return EnsureLoaded(SingleCoreScene);

            // 1.5) SingleCore가 이미 로드되어 있었다면 사용자 변경 확인 후 강제 재로딩
            if (wasAlreadyLoaded)
            {
                yield return CheckAndReloadForUserChange();
            }

            // 2) SingleCore 데이터 로딩 완료 대기
            yield return WaitForSingleCoreDataLoading();

            // 3) SingleGameplayScene 확보 + Active 전환
            yield return EnsureLoaded(SingleGameplayScene);
            SetActive(SingleGameplayScene);

            // 4) SingleGameManager 인스턴스 대기 (토글/재생성 금지)
            SingleGameManager gm = null;
            float timeout = 3f;
            while (timeout > 0f)
            {
                gm = SingleGameManager.Instance ?? FindObjectOfType<SingleGameManager>(true);
                if (gm != null) break;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            if (gm == null)
            {
                callback(false, "SingleGameManager를 찾지 못했습니다.");
                yield break;
            }

            // 5) StageData 전달 (선택적으로 번호 명시)
            var sdm = StageDataManager.Instance ?? FindObjectOfType<StageDataManager>(true);

            if (stageNumber.HasValue && stageNumber.Value > 0)
            {
                if (debugMode) Debug.Log($"[SceneFlowController] 명시적 스테이지 번호 전달: {stageNumber.Value}");
                gm.RequestStartByNumber(stageNumber.Value);
            }
            else if (sdm != null && sdm.GetCurrentStageNumber() > 0)
            {
                int cur = sdm.GetCurrentStageNumber();
                if (debugMode) Debug.Log($"[SceneFlowController] StageDataManager의 현재 스테이지 전달: {cur}");
                gm.RequestStartByNumber(cur);
            }
            else
            {
                // 아직 선택되지 않았다면 StageSelectPanel을 유지하고, 시작 버튼으로 GamePanel → SingleGameManager 호출
                if (debugMode) Debug.Log("[SceneFlowController] 아직 선택된 스테이지 없음. StageSelectPanel 유지.");
            }

            callback(true, "");
        }

        /// <summary>
        ///  사용자 변경 확인 및 강제 데이터 재로딩
        /// </summary>
        private IEnumerator CheckAndReloadForUserChange()
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] 사용자 변경 확인 중...");

            SingleCoreBootstrap bootstrap = null;
            float timeout = 3f;
            float elapsed = 0f;

            // SingleCoreBootstrap 인스턴스 대기
            while (bootstrap == null && elapsed < timeout)
            {
                bootstrap = SingleCoreBootstrap.Instance;
                if (bootstrap == null)
                {
                    elapsed += 0.1f;
                    yield return new WaitForSeconds(0.1f);
                }
            }

            if (bootstrap == null)
            {
                Debug.LogWarning("[SceneFlowController] SingleCoreBootstrap 인스턴스를 찾지 못했습니다.");
                yield break;
            }

            // 사용자 변경 확인 및 강제 재로딩
            bool userChanged = bootstrap.CheckUserChangedAndReload();
            
            if (userChanged)
            {
                LoadingOverlay.Show("사용자 변경 감지 - 데이터 재로딩 중...");
                
                if (debugMode)
                    Debug.Log("[SceneFlowController] 사용자 변경으로 인한 데이터 강제 재로딩 완료");
                
                // 잠시 대기 후 다음 단계 진행
                yield return new WaitForSeconds(0.5f);
            }
            else if (debugMode)
            {
                Debug.Log("[SceneFlowController] 사용자 변경 없음 - 기존 데이터 유지");
            }
        }

        /// <summary>
        ///  SingleCore 데이터 로딩 완료까지 대기
        /// </summary>
        private IEnumerator WaitForSingleCoreDataLoading()
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] SingleCore 데이터 로딩 대기 중...");

            SingleCoreBootstrap bootstrap = null;

            // 인스턴스가 생길 때까지 대기
            float t = 0f;
            while (bootstrap == null && t < 5f)
            {
                bootstrap = SingleCoreBootstrap.Instance;
                if (bootstrap == null)
                {
                    t += 0.1f;
                    yield return new WaitForSeconds(0.1f);
                }
            }

            if (bootstrap == null)
            {
                Debug.LogWarning("[SceneFlowController] SingleCoreBootstrap 인스턴스를 찾지 못했습니다. 계속 진행합니다.");
                yield break;
            }

            if (debugMode)
                Debug.Log("[SceneFlowController] SingleCoreBootstrap 발견. 데이터 로딩 상태 확인 중...");

            // 데이터 로딩 완료까지 대기
            float timeout = 15f; // 15초 타임아웃
            float elapsed = 0f;

            bool loadingCompleted = false;
            System.Action onCompleted = () => { loadingCompleted = true; };
            System.Action<string> onFailed = (error) =>
            {
                loadingCompleted = true;
                Debug.LogError($"[SceneFlowController] 데이터 로딩 실패: {error}");
            };

            // 이벤트 구독
            bootstrap.OnDataLoadingComplete += onCompleted;
            bootstrap.OnDataLoadingFailed += onFailed;

            try
            {
                if (bootstrap.IsDataLoaded())
                {
                    if (debugMode) Debug.Log("[SceneFlowController] 완전한 동기화 이미 완료됨");
                    yield break;
                }

                if (!bootstrap.IsDataLoading())
                {
                    Debug.LogWarning("[SceneFlowController] 데이터 로딩이 시작되지 않음. 수동 동기화 시도...");
                    bootstrap.SyncWithServer();
                    yield return new WaitForSeconds(1f);
                }

                while (!loadingCompleted && elapsed < timeout)
                {
                    if (bootstrap.IsDataLoading())
                    {
                        LoadingOverlay.Show("게임 데이터 로딩 중...");
                    }

                    yield return new WaitForSeconds(0.1f);
                    elapsed += 0.1f;
                }

                if (elapsed >= timeout && !bootstrap.IsDataLoaded())
                {
                    Debug.LogWarning("[SceneFlowController] 완전한 동기화 타임아웃. 게임플레이는 계속 진행됩니다.");
                    
                    //  추가: 동기화 상태 디버그 로그
                    if (bootstrap.GetUserDataCache() != null)
                    {
                        bool syncCompleted = bootstrap.GetUserDataCache().IsInitialSyncCompleted;
                        Debug.LogWarning($"[SceneFlowController] UserDataCache 동기화 상태: {syncCompleted}");
                    }
                }
                else if (debugMode)
                {
                    Debug.Log("[SceneFlowController]  완전한 동기화 완료!");
                }
            }
            finally
            {
                bootstrap.OnDataLoadingComplete -= onCompleted;
                bootstrap.OnDataLoadingFailed -= onFailed;
            }
        }

        /// <summary>
        /// ExitSingleToMain: SingleGameplayScene 언로드(코어 유지) → MainScene 활성
        /// </summary>
        public IEnumerator ExitSingleToMain()
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] ExitSingleToMain() started");

            LoadingOverlay.Show("메인 화면으로 이동 중...");
            InputLocker.Enable();

            bool success = false;
            string errorMsg = "";

            yield return StartCoroutine(CoExitSingleToMain((result, error) =>
            {
                success = result;
                errorMsg = error;
            }));

            if (!success && !string.IsNullOrEmpty(errorMsg))
            {
                SystemMessageManager.ShowToast(errorMsg, MessagePriority.Error);
                Debug.LogError($"[SceneFlowController] {errorMsg}");
            }
            else if (debugMode)
            {
                Debug.Log("[SceneFlowController] ExitSingleToMain() completed successfully");
            }

            LoadingOverlay.Hide();
            InputLocker.Disable();
        }

        private IEnumerator CoExitSingleToMain(System.Action<bool, string> callback)
        {
            // (선택) SingleGameManager 싱글톤 정리
            if (SingleGameManager.Instance != null)
            {
                if (debugMode) Debug.Log("[SceneFlowController] SingleGameManager 싱글톤 참조 정리");
                SingleGameManager.ClearInstance();
            }

            // SingleGameplayScene 언로드
            yield return UnloadIfLoaded(SingleGameplayScene);
            
            //  수정: SingleCore도 언로드 (메인 복귀 시 완전 정리)
            yield return UnloadIfLoaded(SingleCoreScene);

            // MainScene 활성
            SetActive(MainScene);

            callback(true, "");
        }

        /// <summary>
        /// GoMulti: SingleGameplayScene 언로드(있다면) → SingleCore 언로드(있다면) → MultiGameplayScene 로드/활성
        /// </summary>
        public IEnumerator GoMulti()
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] GoMulti() started");

            LoadingOverlay.Show("멀티플레이 로딩 중...");
            InputLocker.Enable();

            bool success = false;
            string errorMsg = "";

            yield return StartCoroutine(CoGoMulti((result, error) =>
            {
                success = result;
                errorMsg = error;
            }));

            if (!success && !string.IsNullOrEmpty(errorMsg))
            {
                SystemMessageManager.ShowToast(errorMsg, MessagePriority.Error);
                Debug.LogError($"[SceneFlowController] {errorMsg}");
            }
            else if (debugMode)
            {
                Debug.Log("[SceneFlowController] GoMulti() completed successfully");
            }

            LoadingOverlay.Hide();
            InputLocker.Disable();
        }

        private IEnumerator CoGoMulti(System.Action<bool, string> callback)
        {
            // SingleGameplayScene 언로드
            yield return UnloadIfLoaded(SingleGameplayScene);

            // SingleCore 언로드 (멀티에서는 필요 없음)
            yield return UnloadIfLoaded(SingleCoreScene);

            // MultiCore 로드 및 활성 (데이터 로딩 전용)
            yield return LoadAdditive(MultiCoreScene, setActive: true);
            
            // MultiCore에서 데이터 로딩 완료까지 대기
            // MultiCoreBootstrap에서 데이터 로딩 완료 후 MultiGameplayScene으로 전환할 것임
            
            callback(true, "");
        }

        /// <summary>
        /// GoMultiGameplay: MultiCore 언로드 → MultiGameplayScene 로드/활성
        /// </summary>
        public IEnumerator GoMultiGameplay()
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] GoMultiGameplay() started");

            LoadingOverlay.Show("게임 로딩 중...");
            
            // MultiCore 언로드 (데이터 로딩 완료)
            yield return UnloadIfLoaded(MultiCoreScene);

            // MultiGameplayScene 로드 및 활성
            yield return LoadAdditive(MultiGameplayScene, setActive: true);

            LoadingOverlay.Hide();
            
            if (debugMode)
                Debug.Log("[SceneFlowController] GoMultiGameplay() completed successfully");
        }

        /// <summary>
        /// ExitMultiToMain: MultiGameplayScene 언로드 → MainScene 활성
        /// </summary>
        public IEnumerator ExitMultiToMain()
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] ExitMultiToMain() started");

            LoadingOverlay.Show("메인 화면으로 이동 중...");
            InputLocker.Enable();

            bool success = false;
            string errorMsg = "";

            yield return StartCoroutine(CoExitMultiToMain((result, error) =>
            {
                success = result;
                errorMsg = error;
            }));

            if (!success && !string.IsNullOrEmpty(errorMsg))
            {
                SystemMessageManager.ShowToast(errorMsg, MessagePriority.Error);
                Debug.LogError($"[SceneFlowController] {errorMsg}");
            }
            else if (debugMode)
            {
                Debug.Log("[SceneFlowController] ExitMultiToMain() completed successfully");
            }

            LoadingOverlay.Hide();
            InputLocker.Disable();
        }

        private IEnumerator CoExitMultiToMain(System.Action<bool, string> callback)
        {
            // MultiGameplayScene 언로드
            yield return UnloadIfLoaded(MultiGameplayScene);

            // DontDestroyOnLoad 객체 정리는 NetworkManager.HandleDisconnection()에서 처리됨
            // (로그아웃 시 autoReconnect = false로 설정되어 자동 정리)

            // MultiCore 씬도 언로드 (연결 실패 시를 위해)
            yield return UnloadIfLoaded(MultiCoreScene);

            // MainScene 활성
            SetActive(MainScene);

            callback(true, "");
        }

        // ========================================
        // Helper Methods
        // ========================================

        /// <summary>
        /// Ensure scene is loaded, load if not present
        /// </summary>
        private IEnumerator EnsureLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (debugMode)
                    Debug.Log($"[SceneFlowController] EnsureLoaded: Loading {sceneName}");

                yield return LoadAdditive(sceneName, setActive: false);
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[SceneFlowController] EnsureLoaded: {sceneName} already loaded");
            }
        }

        /// <summary>
        /// Load scene additively
        /// </summary>
        private IEnumerator LoadAdditive(string sceneName, bool setActive = false)
        {
            if (debugMode)
                Debug.Log($"[SceneFlowController] LoadAdditive: {sceneName}, setActive: {setActive}");

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (asyncLoad == null)
            {
                throw new System.Exception($"Failed to start loading scene: {sceneName}");
            }

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            if (setActive)
            {
                SetActive(sceneName);
            }

            if (debugMode)
                Debug.Log($"[SceneFlowController] LoadAdditive completed: {sceneName}");
        }

        /// <summary>
        /// Unload scene if it's loaded
        /// </summary>
        private IEnumerator UnloadIfLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (debugMode)
                    Debug.Log($"[SceneFlowController] UnloadIfLoaded: Unloading {sceneName}");

                AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(sceneName);
                if (asyncUnload == null)
                {
                    Debug.LogWarning($"[SceneFlowController] Failed to start unloading scene: {sceneName}");
                    yield break;
                }

                while (!asyncUnload.isDone)
                {
                    yield return null;
                }

                if (debugMode)
                    Debug.Log($"[SceneFlowController] UnloadIfLoaded completed: {sceneName}");
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[SceneFlowController] UnloadIfLoaded: {sceneName} not loaded, skipping");
            }
        }

        /// <summary>
        /// Set scene as active scene
        /// </summary>
        private void SetActive(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);

                if (debugMode)
                    Debug.Log($"[SceneFlowController] SetActive: {sceneName} is now active scene");
            }
            else
            {
                Debug.LogError($"[SceneFlowController] SetActive failed: {sceneName} is not loaded or invalid");
            }
        }

        // ========================================
        // 자동 로그인 관련 메서드
        // ========================================

        /// <summary>
        /// 자동 로그인 체크 - 우선순위: 1. Google Play Games, 2. RefreshToken
        /// </summary>
        private IEnumerator CheckAutoLogin()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            App.Logging.AndroidLogger.LogAuth("=== SceneFlowController.CheckAutoLogin START ===");
            #else
            if (debugMode)
                Debug.Log("[SceneFlowController] 자동 로그인 체크 시작");
            #endif

            CurrentAutoLoginState = AutoLoginState.InProgress;
            LoadingOverlay.Show("로그인 상태 확인 중...");

            // Priority 1: Google Play Games Silent Sign-In
            #if UNITY_ANDROID && !UNITY_EDITOR
            App.Logging.AndroidLogger.LogAuth("Priority 1: Google Play Games silent sign-in 시도");

            var googleAuthProvider = new App.Network.GooglePlayGamesAuthProvider();
            if (googleAuthProvider.IsAvailable())
            {
                LoadingOverlay.Show("Google Play Games 로그인 확인 중...");

                var googleAuthTask = googleAuthProvider.AuthenticateSilentAsync();
                while (!googleAuthTask.IsCompleted)
                {
                    yield return new WaitForEndOfFrame();
                }

                var googleAuthResult = googleAuthTask.Result;
                if (googleAuthResult.Success && !string.IsNullOrEmpty(googleAuthResult.AuthCode))
                {
                    App.Logging.AndroidLogger.LogAuth("✅ Google Play Games silent sign-in 성공, 서버 인증 시도");

                    // HttpApiClient가 초기화될 때까지 대기
                    while (App.Network.HttpApiClient.Instance == null)
                    {
                        yield return new WaitForEndOfFrame();
                    }

                    LoadingOverlay.Show("서버 인증 중...");

                    bool serverAuthCompleted = false;
                    bool serverAuthSuccess = false;
                    string serverAuthMessage = "";

                    // 서버 인증 완료 이벤트 구독
                    System.Action<bool, string> onServerAuthComplete = (success, message) =>
                    {
                        serverAuthCompleted = true;
                        serverAuthSuccess = success;
                        serverAuthMessage = message;
                    };

                    App.Network.HttpApiClient.Instance.OnAutoLoginComplete += onServerAuthComplete;

                    // 서버 인증 시도 (Google Auth Code 사용)
                    // AuthenticationService 사용 (토큰 저장은 AuthenticationService에서 처리)
                    var authService = FindObjectOfType<App.Network.AuthenticationService>();
                    if (authService != null)
                    {
                        authService.StartGooglePlayGamesAuth((success, message, tokenResponse) =>
                        {
                            if (success && tokenResponse != null)
                            {
                                App.Logging.AndroidLogger.LogAuth($"✅ 서버 인증 성공: {message}");
                                onServerAuthComplete(true, message);
                            }
                            else
                            {
                                App.Logging.AndroidLogger.LogError($"❌ 서버 인증 실패: {message}");
                                onServerAuthComplete(false, message);
                            }
                        }, googleAuthResult.AuthCode);
                    }
                    else
                    {
                        App.Logging.AndroidLogger.LogError("❌ AuthenticationService를 찾을 수 없음");
                        onServerAuthComplete(false, "AuthenticationService not found");
                    }

                    // 서버 인증 완료까지 대기 (최대 10초)
                    float timeout = 10f;
                    while (!serverAuthCompleted && timeout > 0)
                    {
                        timeout -= Time.deltaTime;
                        yield return new WaitForEndOfFrame();
                    }

                    // 이벤트 구독 해제
                    App.Network.HttpApiClient.Instance.OnAutoLoginComplete -= onServerAuthComplete;

                    if (serverAuthCompleted && serverAuthSuccess)
                    {
                        CurrentAutoLoginState = AutoLoginState.Success;
                        App.Logging.AndroidLogger.LogAuth($"✅ Google Play Games 자동 로그인 성공: {serverAuthMessage}");
                        yield break; // 성공 시 여기서 종료
                    }
                    else
                    {
                        App.Logging.AndroidLogger.LogAuth($"❌ Google Play Games 서버 인증 실패: {serverAuthMessage}");
                    }
                }
                else
                {
                    App.Logging.AndroidLogger.LogAuth($"Silent sign-in 실패 (expected if no previous login): {googleAuthResult.ErrorMessage}");
                }
            }
            else
            {
                App.Logging.AndroidLogger.LogAuth("Google Play Games not available on this device");
            }
            #endif

            // Priority 2: RefreshToken-based Auto-Login
            #if UNITY_ANDROID && !UNITY_EDITOR
            App.Logging.AndroidLogger.LogAuth("Priority 2: RefreshToken 자동 로그인 시도");
            #else
            if (debugMode)
                Debug.Log("[SceneFlowController] Priority 2: RefreshToken 자동 로그인 시도");
            #endif

            LoadingOverlay.Show("저장된 세션 확인 중...");

            // HttpApiClient가 초기화될 때까지 대기
            while (App.Network.HttpApiClient.Instance == null)
            {
                yield return new WaitForEndOfFrame();
            }

            bool autoLoginCompleted = false;
            bool autoLoginSuccess = false;
            string autoLoginMessage = "";

            // 자동 로그인 완료 이벤트 구독
            System.Action<bool, string> onAutoLoginComplete = (success, message) =>
            {
                autoLoginCompleted = true;
                autoLoginSuccess = success;
                autoLoginMessage = message;
            };

            App.Network.HttpApiClient.Instance.OnAutoLoginComplete += onAutoLoginComplete;

            // RefreshToken 기반 자동 로그인 시도
            App.Network.HttpApiClient.Instance.ValidateRefreshTokenFromStorage();

            // 자동 로그인 완료까지 대기 (최대 10초)
            float timeout2 = 10f;
            while (!autoLoginCompleted && timeout2 > 0)
            {
                timeout2 -= Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            // 이벤트 구독 해제
            App.Network.HttpApiClient.Instance.OnAutoLoginComplete -= onAutoLoginComplete;

            // 결과 처리
            if (autoLoginCompleted && autoLoginSuccess)
            {
                CurrentAutoLoginState = AutoLoginState.Success;
                #if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogAuth($"✅ RefreshToken 자동 로그인 성공: {autoLoginMessage}");
                #else
                if (debugMode)
                    Debug.Log($"[SceneFlowController] RefreshToken 자동 로그인 성공: {autoLoginMessage}");
                #endif
            }
            else
            {
                CurrentAutoLoginState = AutoLoginState.Failed;
                #if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogAuth("모든 자동 로그인 실패 - LoginPanel로 이동");
                #else
                if (debugMode)
                    Debug.Log($"[SceneFlowController] 모든 자동 로그인 실패 - LoginPanel로 이동");
                #endif
            }
        }

        /// <summary>
        /// 현재 자동 로그인 상태 반환 (UIManager에서 사용)
        /// </summary>
        public static AutoLoginState GetAutoLoginState()
        {
            return CurrentAutoLoginState;
        }

        // ========================================
        // Public API
        // ========================================

        /// <summary>
        /// Start single player mode transition (스테이지 번호 미지정)
        /// </summary>
        public void StartGoSingle()
        {
            StartCoroutine(GoSingle(null));
        }

        /// <summary>
        /// Start single player mode transition (스테이지 번호 지정)
        /// - StageInfoModal에서 바로 호출 가능
        /// </summary>
        public void StartGoSingle(int stageNumber)
        {
            StartCoroutine(GoSingle(stageNumber));
        }

        /// <summary>
        /// Exit single player mode to main
        /// </summary>
        public void StartExitSingleToMain()
        {
            StartCoroutine(ExitSingleToMain());
        }

        /// <summary>
        /// Start multiplayer mode transition
        /// </summary>
        public void StartGoMulti()
        {
            if (isGoMultiInProgress)
            {
                if (debugMode)
                    Debug.LogWarning("[SceneFlowController] GoMulti already in progress, ignoring duplicate call");
                return;
            }
            
            StartCoroutine(GoMultiWithFlag());
        }
        
        /// <summary>
        /// GoMulti wrapper with progress flag management
        /// </summary>
        private System.Collections.IEnumerator GoMultiWithFlag()
        {
            isGoMultiInProgress = true;
            try
            {
                yield return StartCoroutine(GoMulti());
            }
            finally
            {
                isGoMultiInProgress = false;
            }
        }

        /// <summary>
        /// MultiCore에서 MultiGameplayScene으로 전환
        /// </summary>
        public void StartGoMultiGameplay()
        {
            if (isGoMultiGameplayInProgress)
            {
                if (debugMode)
                    Debug.LogWarning("[SceneFlowController] GoMultiGameplay already in progress, ignoring duplicate call");
                return;
            }
            
            StartCoroutine(GoMultiGameplayWithFlag());
        }
        
        /// <summary>
        /// GoMultiGameplay wrapper with progress flag management
        /// </summary>
        private System.Collections.IEnumerator GoMultiGameplayWithFlag()
        {
            isGoMultiGameplayInProgress = true;
            try
            {
                yield return StartCoroutine(GoMultiGameplay());
            }
            finally
            {
                isGoMultiGameplayInProgress = false;
            }
        }
        
        /// <summary>
        /// Multi → Main 씬 전환 시작 (로그아웃)
        /// </summary>
        public void StartExitMultiToMain()
        {
            StartCoroutine(ExitMultiToMain());
        }
        

        /// <summary>
        /// Check if scene is currently loaded
        /// </summary>
        public bool IsSceneLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        /// <summary>
        /// Get current active scene name
        /// </summary>
        public string GetActiveSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }
    }
}
