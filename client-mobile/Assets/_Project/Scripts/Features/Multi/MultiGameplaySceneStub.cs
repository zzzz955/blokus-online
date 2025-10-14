using UnityEngine;
using Features.Multi.UI;
using Features.Multi.Core;
using Features.Multi.Net;
using App.Core;
using App.UI;
using Shared.UI;
using App.Audio; // AudioManager

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
            
            // UI 패널 찾기 (Inspector에서 설정되지 않은 경우)
            if (lobbyPanel == null)
                lobbyPanel = FindObjectOfType<LobbyPanel>();
                
            if (gameRoomPanel == null)
                gameRoomPanel = FindObjectOfType<GameRoomPanel>();
                
            // 유효성 검증
            if (networkManager == null)
                Debug.LogError("[MultiGameplaySceneController] NetworkManager not found!");
                
            // MultiUserDataCache 제거됨 - NetworkManager 직접 사용
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

            // BGM: Gameplay BGM으로 전환 (멀티플레이 방 생성 또는 입장)
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(BGMTrack.Gameplay);
                if (debugMode)
                    Debug.Log("[MultiGameplaySceneController] Gameplay BGM 전환");
            }

            ShowGameRoomPanel();
        }
        
        private void OnRoomLeft()
        {
            if (debugMode)
                Debug.Log("[MultiGameplaySceneController] Left room");

            // BGM: Lobby BGM으로 복귀 (멀티플레이 방 퇴장)
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(BGMTrack.Lobby);
                if (debugMode)
                    Debug.Log("[MultiGameplaySceneController] Lobby BGM 전환");
            }

            ShowLobbyPanel();
        }
        
        private void OnNetworkError(string error)
        {
            if (debugMode)
                Debug.LogError($"[MultiGameplaySceneController] Network error: {error}");
            
            // 심각한 에러인지 확인
            if (IsCriticalError(error))
            {
                if (debugMode)
                    Debug.LogError($"[MultiGameplaySceneController] Critical error detected, returning to MainScene: {error}");
                
                // 심각한 에러 - 메인 씬으로 돌아가기
                ReturnToMainScene();
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning($"[MultiGameplaySceneController] Non-critical error, showing toast: {error}");
                
                // 일반 에러 - 토스트 메시지만 표시
                ShowErrorToast(error);
                
                // 현재 상태가 게임룸이면 로비로 전환 (방 관련 에러인 경우)
                if (currentState == SceneState.GameRoom && IsRoomRelatedError(error))
                {
                    if (debugMode)
                        Debug.Log("[MultiGameplaySceneController] Room-related error, switching to lobby");
                    ShowLobbyPanel();
                }
            }
        }
        
        /// <summary>
        /// 심각한 에러인지 확인 (연결 끊김, 인증 실패)
        /// </summary>
        private bool IsCriticalError(string error)
        {
            if (string.IsNullOrEmpty(error))
                return false;
            
            string lowerError = error.ToLower();
            
            // 연결 관련 심각한 에러
            if (lowerError.Contains("연결") && (lowerError.Contains("끊어") || lowerError.Contains("실패") || lowerError.Contains("timeout")))
                return true;
            if (lowerError.Contains("connection") && (lowerError.Contains("lost") || lowerError.Contains("failed") || lowerError.Contains("timeout")))
                return true;
            if (lowerError.Contains("disconnect") || lowerError.Contains("네트워크 오류"))
                return true;
                
            // 인증 관련 심각한 에러
            if (lowerError.Contains("인증") && lowerError.Contains("실패"))
                return true;
            if (lowerError.Contains("토큰") && (lowerError.Contains("유효하지") || lowerError.Contains("만료")))
                return true;
            if (lowerError.Contains("token") && (lowerError.Contains("invalid") || lowerError.Contains("expired")))
                return true;
            if (lowerError.Contains("권한") && lowerError.Contains("없습니다"))
                return true;
            if (lowerError.Contains("unauthorized") || lowerError.Contains("forbidden"))
                return true;
                
            return false;
        }
        
        /// <summary>
        /// 방 관련 에러인지 확인
        /// </summary>
        private bool IsRoomRelatedError(string error)
        {
            if (string.IsNullOrEmpty(error))
                return false;
                
            string lowerError = error.ToLower();
            return lowerError.Contains("방") || lowerError.Contains("room") || 
                   lowerError.Contains("참여") || lowerError.Contains("join") ||
                   lowerError.Contains("생성") || lowerError.Contains("create");
        }
        
        /// <summary>
        /// 에러 토스트 메시지 표시
        /// </summary>
        private void ShowErrorToast(string error)
        {
            // ToastMessage 컴포넌트 찾기
            ToastMessage toastMessage = FindObjectOfType<ToastMessage>();
            if (toastMessage != null)
            {
                if (debugMode)
                    Debug.Log($"[MultiGameplaySceneController] Showing error toast: {error}");
                
                // Toast 메시지 표시 (구현에 따라 메서드명이 다를 수 있음)
                toastMessage.gameObject.SetActive(true);
                
                // messageText 직접 설정 (ToastMessage 구조에 따라)
                var messageText = toastMessage.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (messageText != null)
                {
                    messageText.text = $"⚠️ {error}";
                }
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("[MultiGameplaySceneController] ToastMessage 컴포넌트를 찾을 수 없습니다.");
            }
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