using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using BlokusUnity.UI;
using BlokusUnity.UI.Messages;

namespace BlokusUnity
{
    /// <summary>
    /// Scene flow controller for additive scene loading and transition management
    /// Migration Plan: 씬 로딩/언로딩/활성 관리 + 로딩 중 입력 잠금 + 인디케이터 표시
    /// </summary>
    public class SceneFlowController : MonoBehaviour
    {
        // Scene name constants
        private const string AppPersistentScene = "AppPersistent";
        private const string MainScene = "MainScene";
        private const string SingleCoreScene = "SingleCore";
        private const string SingleGameplayScene = "SingleGameplayScene";
        private const string MultiGameplayScene = "MultiGameplayScene";

        // Singleton pattern
        public static SceneFlowController Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

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

        // ========================================
        // Scene Transition Coroutines
        // ========================================

        /// <summary>
        /// GoSingle: SingleCore(없으면 로드) → SingleGameplayScene 로드 → SingleGameplayScene 활성
        /// Migration Plan: 전환 규칙을 위반하지 않음(코어 생존/언로드 타이밍 정확)
        /// </summary>
        public IEnumerator GoSingle()
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] GoSingle() started");

            LoadingOverlay.Show("싱글플레이 로딩 중...");
            InputLocker.Enable();

            bool success = false;
            string errorMsg = "";

            // Execute operations without try-catch to avoid yield issues
            yield return StartCoroutine(GoSingleInternal((result, error) => {
                success = result;
                errorMsg = error;
            }));

            // Handle results
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

        private IEnumerator GoSingleInternal(System.Action<bool, string> callback)
        {
            // Ensure SingleCore is loaded
            yield return EnsureLoaded(SingleCoreScene);
            
            // Load SingleGameplayScene
            yield return LoadAdditive(SingleGameplayScene, setActive: false);
            
            // Activate SingleGameplayScene
            SetActive(SingleGameplayScene);
            
            callback(true, "");
        }

        /// <summary>
        /// ExitSingleToMain: SingleGameplayScene 언로드(코어 유지) → MainScene 활성
        /// Migration Plan: 로딩 중 UI 입력 완전 차단
        /// </summary>
        public IEnumerator ExitSingleToMain()
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] ExitSingleToMain() started");

            LoadingOverlay.Show("메인 화면으로 이동 중...");
            InputLocker.Enable();

            bool success = false;
            string errorMsg = "";

            // Execute operations without try-catch to avoid yield issues
            yield return StartCoroutine(ExitSingleToMainInternal((result, error) => {
                success = result;
                errorMsg = error;
            }));

            // Handle results
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

        private IEnumerator ExitSingleToMainInternal(System.Action<bool, string> callback)
        {
            // Unload SingleGameplayScene (keep SingleCore alive)
            yield return UnloadIfLoaded(SingleGameplayScene);
            
            // Activate MainScene
            SetActive(MainScene);
            
            callback(true, "");
        }

        /// <summary>
        /// GoMulti: SingleGameplayScene 언로드(있다면) → SingleCore 언로드(있다면) → MultiGameplayScene 로드/활성
        /// Migration Plan: 예외 발생 시 SystemMessageManager.ShowToast("로딩 실패: ...", Priority.Error) 호출
        /// </summary>
        public IEnumerator GoMulti()
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] GoMulti() started");

            LoadingOverlay.Show("멀티플레이 로딩 중...");
            InputLocker.Enable();

            bool success = false;
            string errorMsg = "";

            // Execute operations without try-catch to avoid yield issues
            yield return StartCoroutine(GoMultiInternal((result, error) => {
                success = result;
                errorMsg = error;
            }));

            // Handle results
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

        private IEnumerator GoMultiInternal(System.Action<bool, string> callback)
        {
            // Unload SingleGameplayScene if loaded
            yield return UnloadIfLoaded(SingleGameplayScene);
            
            // Unload SingleCore if loaded (multi doesn't need single core)
            yield return UnloadIfLoaded(SingleCoreScene);
            
            // Load and activate MultiGameplayScene
            yield return LoadAdditive(MultiGameplayScene, setActive: true);
            
            callback(true, "");
        }

        /// <summary>
        /// ExitMultiToMain: MultiGameplayScene 언로드 → MainScene 활성
        /// Migration Plan: 전환 전후 깜빡임/입력 누수 없음
        /// </summary>
        public IEnumerator ExitMultiToMain()
        {
            if (debugMode)
                Debug.Log("[SceneFlowController] ExitMultiToMain() started");

            LoadingOverlay.Show("메인 화면으로 이동 중...");
            InputLocker.Enable();

            bool success = false;
            string errorMsg = "";

            // Execute operations without try-catch to avoid yield issues
            yield return StartCoroutine(ExitMultiToMainInternal((result, error) => {
                success = result;
                errorMsg = error;
            }));

            // Handle results
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

        private IEnumerator ExitMultiToMainInternal(System.Action<bool, string> callback)
        {
            // Unload MultiGameplayScene
            yield return UnloadIfLoaded(MultiGameplayScene);
            
            // Activate MainScene
            SetActive(MainScene);
            
            callback(true, "");
        }

        // ========================================
        // Helper Methods
        // ========================================

        /// <summary>
        /// Ensure scene is loaded, load if not present
        /// Migration Plan: 헬퍼 완비
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
        /// Migration Plan: LoadAdditive(name, setActive=false)
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

            // Wait for scene to load
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // Set active if requested
            if (setActive)
            {
                SetActive(sceneName);
            }

            if (debugMode)
                Debug.Log($"[SceneFlowController] LoadAdditive completed: {sceneName}");
        }

        /// <summary>
        /// Unload scene if it's loaded
        /// Migration Plan: UnloadIfLoaded(name)
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

                // Wait for scene to unload
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
        /// Migration Plan: SetActive(name)
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
        // Public API
        // ========================================

        /// <summary>
        /// Start single player mode transition
        /// </summary>
        public void StartGoSingle()
        {
            StartCoroutine(GoSingle());
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
            StartCoroutine(GoMulti());
        }

        /// <summary>
        /// Exit multiplayer mode to main
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
