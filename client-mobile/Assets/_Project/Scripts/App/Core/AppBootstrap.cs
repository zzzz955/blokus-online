using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using App.UI;
using App.Network;

namespace App.Core
{
    /// <summary>
    /// AppPersistent 씬 부트스트랩 - MainScene 자동 로드
    /// Migration Plan: 부팅→AppPersistent→MainScene(additive 활성)
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        [Header("Boot Settings")]
        [SerializeField] private float loadingDelay = 1f; // 로딩 화면 최소 표시 시간
        [SerializeField] private bool debugMode = true;
        
        [Header(" Global Services")]
        [SerializeField] private bool initializeOidcAuthenticator = true;
        
        private const string MainSceneName = "MainScene";
        
        // Global services
        private OidcAuthenticator _oidcAuthenticator;

        void Start()
        {
            Application.runInBackground = true;
            if (debugMode)
                Debug.Log("[AppBootstrap] Starting application bootstrap");

            StartCoroutine(BootSequence());
        }
        
        private IEnumerator BootSequence()
        {
            // 1. 로딩 오버레이 표시
            LoadingOverlay.Show("게임 초기화 중...");
            
            if (debugMode)
                Debug.Log("[AppBootstrap] Loading overlay shown");
            
            //  1.5. 글로벌 서비스 초기화
            yield return InitializeGlobalServices();
            
            // 2. 최소 로딩 시간 대기 (스플래시 효과)
            yield return new WaitForSeconds(loadingDelay);
            
            // 3. MainScene이 이미 로드되어 있는지 확인
            Scene mainScene = SceneManager.GetSceneByName(MainSceneName);
            if (!mainScene.IsValid() || !mainScene.isLoaded)
            {
                if (debugMode)
                    Debug.Log($"[AppBootstrap] Loading {MainSceneName} additively");
                
                // MainScene 로드
                LoadingOverlay.Show("메인 화면 로드 중...");
                yield return LoadMainScene();
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[AppBootstrap] {MainSceneName} already loaded");
            }
            
            // 4. MainScene을 활성 씬으로 설정
            SetMainSceneActive();
            
            // 5. 로딩 오버레이 숨김
            LoadingOverlay.Hide();
            
            if (debugMode)
                Debug.Log("[AppBootstrap] Bootstrap sequence completed");
        }
        
        private IEnumerator LoadMainScene()
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(MainSceneName, LoadSceneMode.Additive);
            
            if (asyncLoad == null)
            {
                Debug.LogError($"[AppBootstrap] Failed to start loading {MainSceneName}");
                SystemMessageManager.ShowToast("메인 화면 로드 실패", Shared.UI.MessagePriority.Error);
                yield break;
            }
            
            // 로딩 진행률 표시
            while (!asyncLoad.isDone)
            {
                float progress = asyncLoad.progress * 100f;
                LoadingOverlay.Show($"메인 화면 로드 중... {progress:F0}%");
                yield return null;
            }
            
            if (debugMode)
                Debug.Log($"[AppBootstrap] {MainSceneName} loaded successfully");
        }
        
        private void SetMainSceneActive()
        {
            Scene mainScene = SceneManager.GetSceneByName(MainSceneName);
            if (mainScene.IsValid() && mainScene.isLoaded)
            {
                SceneManager.SetActiveScene(mainScene);
                
                if (debugMode)
                    Debug.Log($"[AppBootstrap] {MainSceneName} set as active scene");
            }
            else
            {
                Debug.LogError($"[AppBootstrap] Failed to set {MainSceneName} as active - scene not loaded");
                SystemMessageManager.ShowToast("메인 화면 활성화 실패", Shared.UI.MessagePriority.Error);
            }
        }
        
        /// <summary>
        ///  글로벌 서비스 초기화
        /// </summary>
        private IEnumerator InitializeGlobalServices()
        {
            if (debugMode)
                Debug.Log("[AppBootstrap] Initializing global services");
                
            LoadingOverlay.Show("글로벌 서비스 초기화 중...");
            
            // Token key migration (must be done before other services initialize)
            if (debugMode)
                Debug.Log("[AppBootstrap] Migrating legacy token keys");
            App.Security.SecureStorage.MigrateLegacyTokenKeys();
            
            // OIDC Authenticator 초기화
            if (initializeOidcAuthenticator)
            {
                yield return InitializeOidcAuthenticator();
            }
            
            // HttpApiClient 초기화 확인
            if (HttpApiClient.Instance == null)
            {
                var httpClientObj = new GameObject("[Global] HttpApiClient");
                DontDestroyOnLoad(httpClientObj);
                httpClientObj.AddComponent<HttpApiClient>();
                
                if (debugMode)
                    Debug.Log("[AppBootstrap] HttpApiClient created globally");
            }
            
            if (debugMode)
                Debug.Log("[AppBootstrap] Global services initialization completed");
        }
        
        /// <summary>
        /// OIDC Authenticator 초기화
        /// </summary>
        private IEnumerator InitializeOidcAuthenticator()
        {
            if (debugMode)
                Debug.Log("[AppBootstrap] Initializing OIDC Authenticator");
                
            LoadingOverlay.Show("OAuth 서비스 초기화 중...");
            
            // OIDC Authenticator 생성
            var oidcObj = new GameObject("[Global] OidcAuthenticator");
            DontDestroyOnLoad(oidcObj);
            _oidcAuthenticator = oidcObj.AddComponent<OidcAuthenticator>();
            
            // Discovery Document 로드 대기 (최대 10초)
            float timeout = 10f;
            float elapsed = 0f;
            
            while (!_oidcAuthenticator.IsReady() && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
                
                // 진행률 표시
                float progress = (elapsed / timeout) * 100f;
                LoadingOverlay.Show($"OAuth 서비스 초기화 중... {progress:F0}%");
            }
            
            if (_oidcAuthenticator.IsReady())
            {
                if (debugMode)
                    Debug.Log("[AppBootstrap] OIDC Authenticator ready");
            }
            else
            {
                Debug.LogWarning("[AppBootstrap] OIDC Authenticator initialization timeout");
                SystemMessageManager.ShowToast("OAuth 서비스 초기화 시간 초과", Shared.UI.MessagePriority.Warning);
                
                //  타임아웃이어도 객체는 유지 - 나중에 다시 시도할 수 있도록
                if (debugMode)
                    Debug.Log("[AppBootstrap] OIDC Authenticator object created but not ready");
            }
        }
        
        /// <summary>
        /// 부트스트랩이 완료되었는지 확인
        /// </summary>
        public bool IsBootstrapComplete
        {
            get
            {
                Scene mainScene = SceneManager.GetSceneByName(MainSceneName);
                return mainScene.IsValid() && mainScene.isLoaded && SceneManager.GetActiveScene().name == MainSceneName;
            }
        }
        
        /// <summary>
        /// 글로벌 OIDC Authenticator 인스턴스 가져오기
        /// </summary>
        public static OidcAuthenticator GetGlobalOidcAuthenticator()
        {
            var bootstrap = FindObjectOfType<AppBootstrap>();
            return bootstrap?._oidcAuthenticator;
        }
    }
}