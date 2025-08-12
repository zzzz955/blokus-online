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
        private UIState currentState = (UIState)(-1); // 초기값을 무효한 값으로 설정
        private BaseUIPanel currentPanel;

        public static UIManager Instance { get; private set; }

        void Awake()
        {
            Debug.Log("=== UIManager Awake 시작 ===");
            
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                Debug.Log("UIManager 싱글톤 설정 완료");
                
                DontDestroyOnLoad(gameObject);
                InitializePanels();
                
                Debug.Log("UIManager Awake 완료");
            }
            else
            {
                Debug.Log("UIManager 중복 인스턴스 제거");
                Destroy(gameObject);
            }
        }

        void Start()
        {
            Debug.Log("=== UIManager Start 시작 ===");
            
            // 첫 화면: 로그인
            Debug.Log("로그인 패널 표시 시도");
            ShowPanel(UIState.Login, false);
            
            Debug.Log("UIManager Start 완료");
        }

        /// <summary>
        /// UI 패널 딕셔너리 초기화
        /// </summary>
        private void InitializePanels()
        {
            Debug.Log("=== InitializePanels 시작 ===");
            
            panels = new Dictionary<UIState, BaseUIPanel>
            {
                { UIState.Login, loginPanel },
                { UIState.ModeSelection, modeSelectionPanel },
                { UIState.StageSelect, stageSelectPanel },
                // 임시로 누락된 패널들은 null 허용
                { UIState.Lobby, lobbyPanel },
                { UIState.GameRoom, gameRoomPanel },
                { UIState.Loading, loadingPanel }
            };
            
            // 패널 연결 상태 확인
            Debug.Log($"Login Panel: {loginPanel != null}");
            if (loginPanel != null) Debug.Log($"Login Panel Name: {loginPanel.name}");
            
            Debug.Log($"ModeSelection Panel: {modeSelectionPanel != null}");
            if (modeSelectionPanel != null) Debug.Log($"ModeSelection Panel Name: {modeSelectionPanel.name}");
            
            Debug.Log($"StageSelect Panel: {stageSelectPanel != null}");
            if (stageSelectPanel != null) Debug.Log($"StageSelect Panel Name: {stageSelectPanel.name}");
            
            Debug.Log($"Lobby Panel: {lobbyPanel != null}");
            Debug.Log($"GameRoom Panel: {gameRoomPanel != null}");
            Debug.Log($"Loading Panel: {loadingPanel != null}");
            if (loadingPanel != null) Debug.Log($"Loading Panel Name: {loadingPanel.name}");
            
            Debug.Log("InitializePanels 완료");

            // LoginPanel을 제외한 모든 패널 비활성화 (LoginPanel은 startActive=true이므로)
            foreach (var kvp in panels)
            {
                if (kvp.Value != null && kvp.Key != UIState.Login)
                {
                    kvp.Value.Hide(false);
                    Debug.Log($"{kvp.Key} 패널 숨기기");
                }
                else if (kvp.Key == UIState.Login && kvp.Value != null)
                {
                    Debug.Log($"LoginPanel은 startActive=true이므로 숨기지 않음");
                }
            }
        }

        /// <summary>
        /// 지정된 UI 패널로 전환
        /// </summary>
        public void ShowPanel(UIState state, bool animated = true)
        {
            Debug.Log($"=== ShowPanel 시작 ===");
            Debug.Log($"요청된 State: {state}");
            Debug.Log($"현재 State: {currentState}");
            
            if (currentState == state && currentPanel != null) 
            {
                Debug.Log("동일한 State이고 패널이 활성화되어 있어서 return");
                return;
            }

            // 현재 패널 숨기기
            if (currentPanel != null)
            {
                Debug.Log($"현재 패널 숨기기: {currentPanel.name}");
                currentPanel.Hide(animated);
            }

            // 새 패널 표시
            Debug.Log($"panels 딕셔너리에서 {state} 찾는 중...");
            if (panels.TryGetValue(state, out BaseUIPanel newPanel))
            {
                Debug.Log($"패널 딕셔너리에서 찾음: {newPanel != null}");
                
                if (newPanel == null)
                {
                    Debug.LogWarning($"Panel is null for state: {state}. 해당 Panel이 생성되지 않았거나 연결되지 않았습니다.");
                    return;
                }
                
                Debug.Log($"패널 찾음: {newPanel.name}");
                Debug.Log($"패널 타입: {newPanel.GetType().Name}");
                Debug.Log($"패널 Active: {newPanel.gameObject.activeInHierarchy}");
                
                currentPanel = newPanel;
                currentState = state;
                
                Debug.Log("패널 Show() 호출 직전");
                currentPanel.Show(animated);
                Debug.Log("패널 Show() 호출 완료");

                Debug.Log($"UI State Changed: {state}");
            }
            else
            {
                Debug.LogError($"Panel not found for state: {state}");
                Debug.Log($"panels 딕셔너리 키 목록:");
                foreach(var key in panels.Keys)
                {
                    Debug.Log($"  - {key}");
                }
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
            ShowPanel(UIState.Loading, false);
            var async = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SingleGameplayScene");

            while (!async.isDone) yield return null;

            // 로딩 종료
            if (currentPanel != null && currentPanel == panels[UIState.Loading])
                currentPanel.Hide(false);

            // 필요시 게임 HUD 패널 표시(게임플레이용 패널이 등록돼있다면)
            // ShowPanel(UIState.Gameplay, false);
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