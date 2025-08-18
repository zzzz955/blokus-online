using UnityEngine;
using BlokusUnity.Features.Single;

namespace BlokusUnity.Features.Single
{
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
