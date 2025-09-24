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

        //  추가: 데이터 로딩 상태 관리
        private bool isDataLoaded = false;
        private bool isDataLoading = false;
        private string lastLoadedUserId = null; // 마지막 로딩된 사용자 ID 추적

        //  추가: 데이터 로딩 완료 이벤트
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

            //  추가: 이전 사용자 데이터 완전 정리
            ClearAllCachedData();

            InitializeManagers();
            ConnectDependencies();
            
            //  추가: 초기화 후 데이터 로딩 시작
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
        ///  추가: 데이터 로딩 완료 여부 확인 (완전한 동기화 기준)
        /// </summary>
        public bool IsDataLoaded()
        {
            return isDataLoaded && (userDataCache?.IsInitialSyncCompleted ?? false);
        }

        /// <summary>
        ///  추가: 데이터 로딩 중인지 확인
        /// </summary>
        public bool IsDataLoading()
        {
            return isDataLoading;
        }

        /// <summary>
        ///  수정: 사용자 변경 확인 및 강제 데이터 재로딩 (null 체크 개선)
        /// </summary>
        public bool CheckUserChangedAndReload()
        {
            // SessionManager의 로그인 상태를 직접 확인
            if (App.Core.SessionManager.Instance == null || !App.Core.SessionManager.Instance.IsLoggedIn)
            {
                if (debugMode)
                    Debug.Log("[SingleCoreBootstrap] SessionManager 로그인 상태가 아님");
                return false;
            }

            string currentUserId = App.Core.SessionManager.Instance.UserId.ToString();
            
            if (string.IsNullOrEmpty(currentUserId))
            {
                if (debugMode)
                    Debug.LogWarning("[SingleCoreBootstrap] 현재 사용자 ID가 없음");
                return false;
            }

            //  수정: 첫 로딩인 경우와 실제 사용자 변경을 구분
            bool isFirstLoad = string.IsNullOrEmpty(lastLoadedUserId);
            bool userActuallyChanged = !isFirstLoad && (lastLoadedUserId != currentUserId);
            
            if (debugMode)
            {
                if (isFirstLoad)
                    Debug.Log($"[SingleCoreBootstrap] 첫 로딩 감지: (없음) → {currentUserId}");
                else if (userActuallyChanged)
                    Debug.Log($"[SingleCoreBootstrap] 사용자 변경 감지: {lastLoadedUserId} → {currentUserId}");
                else
                    Debug.Log($"[SingleCoreBootstrap] 동일 사용자: {currentUserId}");
            }
            
            //  수정: 실제 사용자 변경일 때만 데이터 재로딩
            if (userActuallyChanged)
            {
                ForceReloadData();
                return true;
            }
            
            //  추가: 첫 로딩인 경우 lastLoadedUserId 설정하고 기존 데이터 사용
            if (isFirstLoad)
            {
                lastLoadedUserId = currentUserId;
                if (debugMode)
                    Debug.Log($"[SingleCoreBootstrap] 첫 로딩 - lastLoadedUserId 설정: {lastLoadedUserId}");
            }
            
            return false;
        }

        /// <summary>
        ///  추가: 강제 데이터 재로딩
        /// </summary>
        public void ForceReloadData()
        {
            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] 강제 데이터 재로딩 시작");

            // 이전 데이터 완전 정리
            ClearAllCachedData();
            
            // 다시 초기화 및 로딩 시작
            InitializeManagers();
            ConnectDependencies();
            StartCoroutine(LoadInitialDataCoroutine());
        }

        /// <summary>
        ///  추가: 초기 데이터 로딩 코루틴
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

            // SessionManager의 로그인 상태를 직접 확인하고 데이터 로드
            if (App.Core.SessionManager.Instance != null && App.Core.SessionManager.Instance.IsLoggedIn)
            {
                if (debugMode)
                    Debug.Log("[SingleCoreBootstrap] SessionManager 로그인 감지 - 서버 데이터 로드 시작");

                //  수정: 메타데이터뿐만 아니라 완전한 동기화 대기
                
                // UserDataCache의 초기 데이터 로드 트리거
                yield return StartCoroutine(TriggerUserDataLoad());
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[SingleCoreBootstrap] SessionManager 로그인 상태가 아님 - 데이터 로딩 스킵");
                
                // 로그인되지 않은 상태에서도 완료 처리
                CompleteDataLoading();
            }
        }

        /// <summary>
        ///  추가: UserDataCache 데이터 로드 트리거
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

            //  수정: 완전한 동기화 완료까지 대기 (메타데이터 + 진행도 + 현재 상태)
            yield return StartCoroutine(userDataCache.WaitUntilSynced(15f));

            if (!userDataCache.IsInitialSyncCompleted)
            {
                Debug.LogWarning("[SingleCoreBootstrap] 초기 동기화 타임아웃");
                OnDataLoadingFailed?.Invoke("초기 동기화 타임아웃");
            }
            else
            {
                // 동기화 완료 시 CompleteDataLoading 호출
                CompleteDataLoading();
            }
        }

        /// <summary>
        ///  제거됨: 이제 완전한 동기화를 위해 IsMetadataLoaded와 OnStageMetadataLoaded는 사용하지 않음
        /// WaitUntilSynced()를 통해 metadata + progress + status 모두 대기
        /// </summary>

        /// <summary>
        ///  추가: 데이터 로딩 완료 처리
        /// </summary>
        private void CompleteDataLoading()
        {
            isDataLoading = false;
            isDataLoaded = true;

            // 현재 사용자 ID 기록
            if (userDataCache != null && userDataCache.IsLoggedIn())
            {
                lastLoadedUserId = userDataCache.GetCurrentUserId();
                if (debugMode)
                    Debug.Log($"[SingleCoreBootstrap] 데이터 로딩 완료 - 사용자 ID 기록: {lastLoadedUserId}");
            }

            //  추가: 사용자 진행도 기반 CurrentStage 설정 (스테이지 선택 모드)
            if (stageProgressManager != null && stageDataManager != null)
            {
                int nextStage = stageProgressManager.GetMaxUnlockedStage(); // 다음 도전할 스테이지
                nextStage = UnityEngine.Mathf.Max(1, nextStage); // 최소 1스테이지
                
                //  수정: IsInGameplayMode=false로 설정하여 스테이지 선택 모드 유지
                Features.Single.Gameplay.SingleGameManager.SetStageContext(nextStage, stageDataManager, false);
                
                if (debugMode)
                    Debug.Log($"[SingleCoreBootstrap] CurrentStage를 진행도 기반으로 설정: {nextStage} (스테이지 선택 모드)");
            }

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

        /// <summary>
        ///  추가: 이전 사용자 데이터 완전 정리
        /// SingleCore 로딩 시 이전 사용자의 캐시 데이터가 남아있지 않도록 정리
        /// </summary>
        private void ClearAllCachedData()
        {
            if (debugMode)
                Debug.Log("[SingleCoreBootstrap] 이전 사용자 데이터 완전 정리 시작");

            try
            {
                // UserDataCache 정리
                if (userDataCache != null)
                {
                    userDataCache.ClearCache();
                    if (debugMode)
                        Debug.Log("[SingleCoreBootstrap] UserDataCache.ClearCache() 완료");
                }

                // StageDataManager 정리
                if (stageDataManager != null)
                {
                    stageDataManager.ClearCache();
                    if (debugMode)
                        Debug.Log("[SingleCoreBootstrap] StageDataManager.ClearCache() 완료");
                }

                // StageProgressManager 정리
                if (stageProgressManager != null)
                {
                    stageProgressManager.ClearCache();
                    if (debugMode)
                        Debug.Log("[SingleCoreBootstrap] StageProgressManager.ClearCache() 완료");
                }

                // 데이터 로딩 상태 초기화
                isDataLoaded = false;
                isDataLoading = false;
                lastLoadedUserId = null;

                if (debugMode)
                    Debug.Log("[SingleCoreBootstrap] 이전 사용자 데이터 정리 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SingleCoreBootstrap] 캐시 정리 중 오류 발생: {ex.Message}");
            }
        }
    }
}
