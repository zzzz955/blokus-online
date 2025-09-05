using UnityEngine;
using Features.Multi.UI;
using Features.Multi.Core;
using Features.Multi.Net;
using App.Core;
using App.UI;

namespace Features.Multi
{
    /// <summary>
    /// MultiGameplayScene 컨트롤러
    /// LobbyPanel과 GameRoomPanel 관리
    /// </summary>
    public class MultiGameplaySceneController : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private LobbyPanel lobbyPanel;
        [SerializeField] private GameRoomPanel gameRoomPanel;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Scene state
        private MultiCoreBootstrap multiCore;
        private NetworkManager networkManager;
        private MultiUserDataCache dataCache;
        private SceneState currentState = SceneState.Lobby;
        
        // Panel state
        private enum SceneState
        {
            Lobby,
            GameRoom
        }

        void Start()
        {
            InitializeScene();
        }

        /// <summary>
        /// 씬 초기화
        /// </summary>
        private void InitializeScene()
        {
            if (debugMode)
                Debug.Log("[MultiGameplaySceneController] Initializing MultiGameplayScene");

            FindDependencies();
            SetupUI();
            SubscribeToEvents();
            
            // 로비 패널로 시작
            ShowLobbyPanel();
        }

        /// <summary>
        /// 의존성 검색
        /// </summary>
        private void FindDependencies()
        {
            // MultiCore에서 넘어온 NetworkManager 찾기
            multiCore = MultiCoreBootstrap.Instance;
            networkManager = NetworkManager.Instance;
            dataCache = MultiUserDataCache.Instance;
            
            // UI 패널 찾기 (Inspector에서 설정되지 않은 경우)
            if (lobbyPanel == null)
                lobbyPanel = FindObjectOfType<LobbyPanel>();
                
            if (gameRoomPanel == null)
                gameRoomPanel = FindObjectOfType<GameRoomPanel>();
                
            // 유효성 검증
            if (networkManager == null)
                Debug.LogError("[MultiGameplaySceneController] NetworkManager not found!");
                
            if (dataCache == null)
                Debug.LogError("[MultiGameplaySceneController] MultiUserDataCache not found!");
        }

        /// <summary>
        /// UI 설정
        /// </summary>
        private void SetupUI()
        {
            // 모든 패널 비활성화로 시작
            if (lobbyPanel != null)
                lobbyPanel.gameObject.SetActive(false);
                
            if (gameRoomPanel != null)
                gameRoomPanel.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnRoomJoined += OnRoomJoined;
                networkManager.OnRoomLeft += OnRoomLeft;
                networkManager.OnErrorReceived += OnNetworkError;
            }
            
            if (lobbyPanel != null)
            {
                // 로비 패널 이벤트 구독 (구현 시)
            }
            
            if (gameRoomPanel != null)
            {
                // 게임룸 패널 이벤트 구독 (구현 시)
            }
        }

        /// <summary>
        /// 로비 패널 표시
        /// </summary>
        public void ShowLobbyPanel()
        {
            currentState = SceneState.Lobby;
            
            if (lobbyPanel != null)
            {
                lobbyPanel.gameObject.SetActive(true);
                if (debugMode)
                    Debug.Log("[MultiGameplaySceneController] Lobby panel activated");
            }
            
            if (gameRoomPanel != null)
            {
                gameRoomPanel.gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// 게임룸 패널 표시
        /// </summary>
        public void ShowGameRoomPanel()
        {
            currentState = SceneState.GameRoom;
            
            if (gameRoomPanel != null)
            {
                gameRoomPanel.gameObject.SetActive(true);
                if (debugMode)
                    Debug.Log("[MultiGameplaySceneController] GameRoom panel activated");
            }
            
            if (lobbyPanel != null)
            {
                lobbyPanel.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnRoomJoined -= OnRoomJoined;
                networkManager.OnRoomLeft -= OnRoomLeft;
                networkManager.OnErrorReceived -= OnNetworkError;
            }
        }
        
        // ========================================
        // Event Handlers
        // ========================================
        
        private void OnRoomJoined()
        {
            if (debugMode)
                Debug.Log("[MultiGameplaySceneController] Joined room");
                
            ShowGameRoomPanel();
        }
        
        private void OnRoomLeft()
        {
            if (debugMode)
                Debug.Log("[MultiGameplaySceneController] Left room");
                
            ShowLobbyPanel();
        }
        
        private void OnNetworkError(string error)
        {
            if (debugMode)
                Debug.LogError($"[MultiGameplaySceneController] Network error: {error}");
                
            // 에러 시 메인 씬으로 돌아가기
            ReturnToMainScene();
        }
        
        /// <summary>
        /// 로비로 돌아가기 (방에서 나갈 때)
        /// </summary>
        public void ShowLobby()
        {
            if (debugMode)
                Debug.Log("[MultiGameplaySceneController] Switching to Lobby");
                
            // 게임방에서 로비로 전환
            if (gameRoomPanel != null)
                gameRoomPanel.gameObject.SetActive(false);
                
            if (lobbyPanel != null)
                lobbyPanel.gameObject.SetActive(true);
                
            currentState = SceneState.Lobby;
            
            if (debugMode)
                Debug.Log("[MultiGameplaySceneController] Switched to Lobby successfully");
        }
        
        /// <summary>
        /// 메인 씬으로 돌아가기
        /// </summary>
        public void ReturnToMainScene()
        {
            if (debugMode)
                Debug.Log("[MultiGameplaySceneController] Returning to MainScene");

            if (SceneFlowController.Instance != null)
            {
                SceneFlowController.Instance.StartExitMultiToMain();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Show Lobby Panel")]
        private void TestShowLobbyPanel()
        {
            ShowLobbyPanel();
        }

        [ContextMenu("Show GameRoom Panel")]
        private void TestShowGameRoomPanel()
        {
            ShowGameRoomPanel();
        }
        
        [ContextMenu("Return to Main Scene")]
        private void TestReturnToMain()
        {
            ReturnToMainScene();
        }
#endif
    }
}