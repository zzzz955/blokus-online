using System.Collections.Generic;
using UnityEngine;
using BlokusUnity.Common;
using BlokusUnity.Data;

namespace BlokusUnity.UI
{
    /// <summary>
    /// Unity 블로쿠스 UI 매니저
    /// 모든 UI 패널 전환을 중앙에서 관리
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private BaseUIPanel loginPanel;
        [SerializeField] private BaseUIPanel modeSelectionPanel;
        [SerializeField] private BaseUIPanel stageSelectPanel;
        [SerializeField] private BaseUIPanel lobbyPanel;
        [SerializeField] private BaseUIPanel gameRoomPanel;
        [SerializeField] private BaseUIPanel loadingPanel;
        
        private Dictionary<UIState, BaseUIPanel> panels;
        private UIState currentState;
        private BaseUIPanel currentPanel;
        
        public static UIManager Instance { get; private set; }
        
        void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePanels();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void Start()
        {
            // 첫 화면: 로그인
            ShowPanel(UIState.Login, false);
        }
        
        /// <summary>
        /// UI 패널 딕셔너리 초기화
        /// </summary>
        private void InitializePanels()
        {
            panels = new Dictionary<UIState, BaseUIPanel>
            {
                { UIState.Login, loginPanel },
                { UIState.ModeSelection, modeSelectionPanel },
                { UIState.StageSelect, stageSelectPanel },
                { UIState.Lobby, lobbyPanel },
                { UIState.GameRoom, gameRoomPanel },
                { UIState.Loading, loadingPanel }
            };
            
            // 모든 패널 비활성화
            foreach (var panel in panels.Values)
            {
                if (panel != null)
                    panel.Hide(false);
            }
        }
        
        /// <summary>
        /// 지정된 UI 패널로 전환
        /// </summary>
        public void ShowPanel(UIState state, bool animated = true)
        {
            if (currentState == state) return;
            
            // 현재 패널 숨기기
            if (currentPanel != null)
            {
                currentPanel.Hide(animated);
            }
            
            // 새 패널 표시
            if (panels.TryGetValue(state, out BaseUIPanel newPanel) && newPanel != null)
            {
                currentPanel = newPanel;
                currentState = state;
                currentPanel.Show(animated);
                
                Debug.Log($"UI State Changed: {state}");
            }
            else
            {
                Debug.LogError($"Panel not found for state: {state}");
            }
        }
        
        /// <summary>
        /// UI 전환 플로우 정의
        /// </summary>
        public void HandleGameFlow()
        {
            /*
            📱 예상 UI 플로우:
            
            Login → ModeSelection
                 ├── Single → StageSelect → Gameplay
                 └── Multi → Lobby → GameRoom → Gameplay
            
            🔄 구현할 전환 함수들:
            - OnLoginSuccess() → ShowModeSelection()
            - OnSingleModeSelected() → ShowStageSelect()  
            - OnMultiModeSelected() → ShowLobby()
            - OnStageSelected() → LoadGameplay()
            - OnRoomJoined() → ShowGameRoom()
            */
        }
        
        // ===========================================
        // UI 전환 이벤트 함수들
        // ===========================================
        
        public void OnLoginSuccess()
        {
            ShowPanel(UIState.ModeSelection);
        }
        
        public void OnSingleModeSelected()
        {
            ShowPanel(UIState.StageSelect);
        }
        
        public void OnMultiModeSelected()
        {
            ShowPanel(UIState.Lobby);
        }
        
        public void OnStageSelected(int stageNumber)
        {
            // 스테이지 데이터 매니저를 통해 스테이지 선택
            if (StageDataManager.Instance != null)
            {
                StageDataManager.Instance.SelectStage(stageNumber);
                StageDataManager.Instance.PassDataToSingleGameManager();
            }
            
            Debug.Log($"스테이지 {stageNumber} 선택됨");
            
            // 싱글게임 씬으로 전환
            LoadSingleGameplayScene();
        }
        
        public void OnRoomJoined()
        {
            ShowPanel(UIState.GameRoom);
        }
        
        public void OnGameStart()
        {
            // 멀티플레이 게임 시작
            LoadMultiGameplayScene();
        }
        
        public void OnBackToMenu()
        {
            ShowPanel(UIState.ModeSelection);
        }
        
        /// <summary>
        /// 싱글플레이 게임 씬 로드
        /// </summary>
        private void LoadSingleGameplayScene()
        {
            StartCoroutine(LoadSingleGameplaySceneCoroutine());
        }
        
        private System.Collections.IEnumerator LoadSingleGameplaySceneCoroutine()
        {
            // 로딩 패널 표시
            ShowPanel(UIState.Loading, false);
            
            // 싱글게임 씬 로드 (완전 전환)
            var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SingleGameplayScene");
            
            while (!asyncLoad.isDone)
            {
                // 로딩 진행률 업데이트 (TODO: 로딩 바 구현)
                yield return null;
            }
            
            Debug.Log("Single Gameplay Scene Loaded");
        }
        
        /// <summary>
        /// 멀티플레이 게임 씬 로드 (나중에 구현)
        /// </summary>
        private void LoadMultiGameplayScene()
        {
            StartCoroutine(LoadMultiGameplaySceneCoroutine());
        }
        
        private System.Collections.IEnumerator LoadMultiGameplaySceneCoroutine()
        {
            // 로딩 패널 표시
            ShowPanel(UIState.Loading, false);
            
            // 멀티게임 씬 Additive 로드 (UI 유지)
            var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("MultiGameplayScene", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
            
            // UI 패널들 숨기기 (게임 플레이 중)
            if (currentPanel != null)
            {
                currentPanel.Hide(false);
            }
            
            Debug.Log("Multi Gameplay Scene Loaded");
        }
    }
}