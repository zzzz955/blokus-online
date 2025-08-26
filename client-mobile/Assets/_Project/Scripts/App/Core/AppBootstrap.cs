using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using App.UI;

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
        
        private const string MainSceneName = "MainScene";
        
        void Start()
        {
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
    }
}