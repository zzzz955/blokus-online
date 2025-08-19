using UnityEngine;
using Features.Single.Core;
namespace Features.Single.Core{
    /// <summary>
    /// SingleCore scene bootstrap manager
    /// Migration Plan: SingleCoreBootstrap가 위 매니저 초기화 및 의존 관계 연결
    /// 메인 복귀 시 유지, 멀티 진입 전 언로드
    /// </summary>
    public class SingleCoreBootstrap : MonoBehaviour
    {
        [Header("Core Managers")]
        [SerializeField] private StageDataManager stageDataManager;
        [SerializeField] private StageProgressManager stageProgressManager;
        [SerializeField] private UserDataCache userDataCache;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Singleton for scene-scoped access
        public static SingleCoreBootstrap Instance { get; private set; }

        // 🔥 추가: 데이터 로딩 상태 관리
        private bool isDataLoaded = false;
        private bool isDataLoading = false;

        // 🔥 추가: 데이터 로딩 완료 이벤트
        public event System.Action OnDataLoadingComplete;
        public event System.Action<string> OnDataLoadingFailed;

        void Awake()
        {
            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] Awake - Initializing SingleCore");

            // Set instance for scene-scoped access (not DontDestroyOnLoad)
            Instance = this;
            
            // Find managers if not assigned
            FindManagers();
        }

        void Start()
        {
            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] Start - Connecting dependencies");

            InitializeManagers();
            ConnectDependencies();
            
            // 🔥 추가: 초기화 후 데이터 로딩 시작
            StartCoroutine(LoadInitialDataCoroutine());
        }

        void OnDestroy()
        {
            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] OnDestroy - Cleaning up SingleCore");

            CleanupManagers();
            Instance = null;
        }

        // ========================================
        // Initialization
        // ========================================

        private void FindManagers()
        {
            if (stageDataManager == null)
                stageDataManager = FindObjectOfType<StageDataManager>();

            if (stageProgressManager == null)
                stageProgressManager = FindObjectOfType<StageProgressManager>();

            if (userDataCache == null)
                userDataCache = FindObjectOfType<UserDataCache>();

            // Validate all managers are found
            if (stageDataManager == null || stageProgressManager == null || userDataCache == null)
            {
                Debug.LogError("[SingleCoreBootstrap] Not all required managers found in scene!", this);
            }
        }

        private void InitializeManagers()
        {
            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] Initializing managers...");

            // Initialize in dependency order
            if (userDataCache != null)
            {
                userDataCache.Initialize();
                if (debugMode)
                    Debug.Log("[SingleCoreBootstrap] UserDataCache initialized");
            }

            if (stageDataManager != null)
            {
                stageDataManager.Initialize();
                if (debugMode)
                    Debug.Log("[SingleCoreBootstrap] StageDataManager initialized");
            }

            if (stageProgressManager != null)
            {
                stageProgressManager.Initialize();
                if (debugMode)
                    Debug.Log("[SingleCoreBootstrap] StageProgressManager initialized");
            }
        }

        private void ConnectDependencies()
        {
            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] Connecting dependencies...");

            // Migration Plan: 의존 관계 연결
            if (stageDataManager != null && userDataCache != null)
            {
                stageDataManager.SetUserDataCache(userDataCache);
            }

            if (stageProgressManager != null && userDataCache != null)
            {
                stageProgressManager.SetUserDataCache(userDataCache);
            }

            if (stageProgressManager != null && stageDataManager != null)
            {
                stageProgressManager.SetStageDataManager(stageDataManager);
            }

            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] Dependencies connected successfully");
        }

        private void CleanupManagers()
        {
            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] Cleaning up managers...");

            // Migration Plan: 멀티로 갈 때 메모리 릭/핸들 잔류 없음
            if (stageProgressManager != null)
                stageProgressManager.Cleanup();

            if (stageDataManager != null)
                stageDataManager.Cleanup();

            if (userDataCache != null)
                userDataCache.Cleanup();
        }

        // ========================================
        // Public API
        // ========================================

        /// <summary>
        /// Get StageDataManager instance
        /// </summary>
        public StageDataManager GetStageDataManager()
        {
            return stageDataManager;
        }

        /// <summary>
        /// Get StageProgressManager instance
        /// </summary>
        public StageProgressManager GetStageProgressManager()
        {
            return stageProgressManager;
        }

        /// <summary>
        /// Get UserDataCache instance
        /// </summary>
        public UserDataCache GetUserDataCache()
        {
            return userDataCache;
        }

        /// <summary>
        /// Check if all managers are initialized
        /// </summary>
        public bool IsInitialized()
        {
            return stageDataManager != null && 
                   stageProgressManager != null && 
                   userDataCache != null &&
                   stageDataManager.IsInitialized &&
                   stageProgressManager.IsInitialized &&
                   userDataCache.IsInitialized;
        }

        /// <summary>
        /// 🔥 추가: 데이터 로딩 완료 여부 확인
        /// </summary>
        public bool IsDataLoaded()
        {
            return isDataLoaded;
        }

        /// <summary>
        /// 🔥 추가: 데이터 로딩 중인지 확인
        /// </summary>
        public bool IsDataLoading()
        {
            return isDataLoading;
        }

        /// <summary>
        /// 🔥 추가: 초기 데이터 로딩 코루틴
        /// </summary>
        private System.Collections.IEnumerator LoadInitialDataCoroutine()
        {
            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] 데이터 로딩 시작...");

            isDataLoading = true;
            isDataLoaded = false;

            // 매니저 초기화 완료까지 대기
            while (!IsInitialized())
            {
                yield return new WaitForSeconds(0.1f);
            }

            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] 매니저 초기화 완료. 서버 데이터 로딩 시작...");

            // UserDataCache가 로그인된 상태인지 확인하고 데이터 로드
            if (userDataCache != null && userDataCache.IsLoggedIn())
            {
                if (debugMode)
                    Debug.Log("[SingleCoreBootstrap] 로그인된 사용자 - 서버 데이터 로드 시작");

                // 데이터 로딩 완료 이벤트 구독
                userDataCache.OnStageMetadataUpdated += OnStageMetadataLoaded;
                
                // UserDataCache의 초기 데이터 로드 트리거
                yield return StartCoroutine(TriggerUserDataLoad());
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[SingleCoreBootstrap] 사용자가 로그인되지 않음 - 데이터 로딩 스킵");
                
                // 로그인되지 않은 상태에서도 완료 처리
                CompleteDataLoading();
            }
        }

        /// <summary>
        /// 🔥 추가: UserDataCache 데이터 로드 트리거
        /// </summary>
        private System.Collections.IEnumerator TriggerUserDataLoad()
        {
            if (userDataCache == null) yield break;

            // HttpApiClient가 준비될 때까지 대기
            while (App.Network.HttpApiClient.Instance == null)
            {
                yield return new WaitForSeconds(0.1f);
            }

            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] HttpApiClient 준비됨. 데이터 로드 요청...");

            // UserDataCache의 LoadInitialDataFromServer 호출
            try
            {
                // Reflection으로 private 메서드 호출하거나 public 메서드 추가 필요
                var method = userDataCache.GetType().GetMethod("LoadInitialDataFromServer", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (method != null)
                {
                    method.Invoke(userDataCache, null);
                    if (debugMode)
                        Debug.Log("[SingleCoreBootstrap] LoadInitialDataFromServer 호출 완료");
                }
                else
                {
                    Debug.LogError("[SingleCoreBootstrap] LoadInitialDataFromServer 메서드를 찾을 수 없습니다");
                    OnDataLoadingFailed?.Invoke("LoadInitialDataFromServer 메서드 접근 실패");
                    yield break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SingleCoreBootstrap] 데이터 로드 중 오류: {e.Message}");
                OnDataLoadingFailed?.Invoke($"데이터 로드 오류: {e.Message}");
                yield break;
            }

            // 메타데이터 로드 완료까지 대기 (최대 10초)
            float timeout = 10f;
            float elapsed = 0f;

            while (!IsMetadataLoaded() && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (elapsed >= timeout)
            {
                Debug.LogWarning("[SingleCoreBootstrap] 메타데이터 로드 타임아웃");
                OnDataLoadingFailed?.Invoke("메타데이터 로드 타임아웃");
            }
        }

        /// <summary>
        /// 🔥 추가: 메타데이터 로드 완료 확인
        /// </summary>
        private bool IsMetadataLoaded()
        {
            if (userDataCache == null) return false;
            
            var metadata = userDataCache.GetStageMetadata();
            return metadata != null && metadata.Length > 0;
        }

        /// <summary>
        /// 🔥 추가: 스테이지 메타데이터 로드 완료 이벤트 핸들러
        /// </summary>
        private void OnStageMetadataLoaded(App.Network.HttpApiClient.CompactStageMetadata[] metadata)
        {
            if (debugMode)
                Debug.Log($"[SingleCoreBootstrap] 메타데이터 로드 완료: {metadata?.Length ?? 0}개");

            // 이벤트 구독 해제
            if (userDataCache != null)
            {
                userDataCache.OnStageMetadataUpdated -= OnStageMetadataLoaded;
            }

            CompleteDataLoading();
        }

        /// <summary>
        /// 🔥 추가: 데이터 로딩 완료 처리
        /// </summary>
        private void CompleteDataLoading()
        {
            isDataLoading = false;
            isDataLoaded = true;

            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] 🎉 데이터 로딩 완료!");

            OnDataLoadingComplete?.Invoke();
        }

        /// <summary>
        /// Trigger synchronization with server
        /// Migration Plan: 싱글 → 메인 → 싱글 반복에서 진행도/캐시 유지
        /// </summary>
        public void SyncWithServer()
        {
            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] Syncing with server...");

            if (userDataCache != null && userDataCache.IsInitialized)
            {
                userDataCache.SyncWithServer();
            }
        }
    }
}
