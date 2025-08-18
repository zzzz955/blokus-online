using System.Collections.Generic;
using UnityEngine;
using BlokusUnity.Common;
using BlokusUnity.Data;
using BlokusUnity.UI.Messages;

namespace BlokusUnity.UI
{
    /// <summary>
    /// Unity 블로쿠스 UI 매니저
    /// 모든 UI 패널 전환을 중앙에서 관리
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private BlokusUnity.UI.PanelBase loginPanel;
        [SerializeField] private BlokusUnity.UI.PanelBase modeSelectionPanel;
        [SerializeField] private BlokusUnity.UI.PanelBase stageSelectPanel;
        [SerializeField] private BlokusUnity.UI.PanelBase lobbyPanel;
        [SerializeField] private BlokusUnity.UI.PanelBase gameRoomPanel;
        [SerializeField] private BlokusUnity.UI.PanelBase loadingPanel;

        private Dictionary<UIState, BlokusUnity.UI.PanelBase> panels;
        private UIState currentState = (UIState)(-1); // 초기값을 무효한 값으로 설정
        private BlokusUnity.UI.PanelBase currentPanel;

        public static UIManager Instance { get; private set; }

        void Awake()
        {
            Debug.Log("=== UIManager Awake 시작 ===");
            
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                Debug.Log("UIManager 싱글톤 설정 완료");
                
                // 루트 GameObject로 이동 (DontDestroyOnLoad 적용을 위해)
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                Debug.Log("UIManager DontDestroyOnLoad 적용됨");
                
                InitializePanels();
                InitializeSystemMessageManager();
                
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
            
            // Exit으로 돌아온 경우 확인
            bool returnedFromGame = PlayerPrefs.GetInt("ReturnedFromGame", 0) == 1;
            if (returnedFromGame)
            {
                // 플래그 초기화
                PlayerPrefs.DeleteKey("ReturnedFromGame");
                PlayerPrefs.Save();
                
                // 로그인 상태 확인 후 적절한 패널 표시
                if (BlokusUnity.Features.Single.UserDataCache.Instance != null && BlokusUnity.Features.Single.UserDataCache.Instance.IsLoggedIn())
                {
                    Debug.Log("Exit으로 돌아옴 + 로그인됨 - 스테이지 선택 패널 표시");
                    ShowPanel(UIState.StageSelect, false);
                }
                else
                {
                    Debug.Log("Exit으로 돌아옴 + 로그인 안됨 - 로그인 패널 표시");
                    ShowPanel(UIState.Login, false);
                }
            }
            else
            {
                // 일반적인 게임 시작 - 항상 로그인 패널
                Debug.Log("일반 게임 시작 - 로그인 패널 표시");
                ShowPanel(UIState.Login, false);
            }
            
            Debug.Log("UIManager Start 완료");
        }

        /// <summary>
        /// UI 패널 딕셔너리 초기화
        /// </summary>
        private void InitializePanels()
        {
            Debug.Log("=== InitializePanels 시작 ===");
            
            panels = new Dictionary<UIState, BlokusUnity.UI.PanelBase>
            {
                { UIState.Login, loginPanel },
                { UIState.ModeSelection, modeSelectionPanel },
                { UIState.StageSelect, stageSelectPanel },
                // 임시로 누락된 패널들은 null 허용
                { UIState.Lobby, lobbyPanel },
                { UIState.GameRoom, gameRoomPanel },
                { UIState.Loading, loadingPanel }
            };
            
            // 패널 연결 상태 확인 (강화된 로깅)
            Debug.Log($"Login Panel: {loginPanel != null}");
            if (loginPanel != null) 
            {
                Debug.Log($"Login Panel Name: {loginPanel.name}");
                Debug.Log($"Login Panel Type: {loginPanel.GetType().Name}");
                Debug.Log($"Login Panel GameObject null?: {loginPanel.gameObject == null}");
            }
            
            Debug.Log($"ModeSelection Panel: {modeSelectionPanel != null}");
            if (modeSelectionPanel != null) 
            {
                Debug.Log($"ModeSelection Panel Name: {modeSelectionPanel.name}");
                Debug.Log($"ModeSelection Panel GameObject null?: {modeSelectionPanel.gameObject == null}");
            }
            
            Debug.Log($"StageSelect Panel: {stageSelectPanel != null}");
            if (stageSelectPanel != null) 
            {
                Debug.Log($"StageSelect Panel Name: {stageSelectPanel.name}");
                Debug.Log($"StageSelect Panel GameObject null?: {stageSelectPanel.gameObject == null}");
            }
            
            Debug.Log($"Lobby Panel: {lobbyPanel != null}");
            Debug.Log($"GameRoom Panel: {gameRoomPanel != null}");
            Debug.Log($"Loading Panel: {loadingPanel != null}");
            if (loadingPanel != null) 
            {
                Debug.Log($"Loading Panel Name: {loadingPanel.name}");
                Debug.Log($"Loading Panel GameObject null?: {loadingPanel.gameObject == null}");
            }
            
            Debug.Log("InitializePanels 완료");

            // LoginPanel을 제외한 모든 패널 비활성화 (LoginPanel은 startActive=true이므로)
            foreach (var kvp in panels)
            {
                if (kvp.Value != null && kvp.Key != UIState.Login)
                {
                    kvp.Value.Hide();
                    Debug.Log($"{kvp.Key} 패널 숨기기");
                }
                else if (kvp.Key == UIState.Login && kvp.Value != null)
                {
                    Debug.Log($"LoginPanel은 startActive=true이므로 숨기지 않음");
                }
            }
        }

        /// <summary>
        /// SystemMessageManager 초기화
        /// </summary>
        private void InitializeSystemMessageManager()
        {
            Debug.Log("=== InitializeSystemMessageManager 시작 ===");
            
            // SystemMessageManager가 없으면 생성
            if (SystemMessageManager.Instance == null)
            {
                GameObject messageManagerObj = new GameObject("SystemMessageManager");
                messageManagerObj.AddComponent<SystemMessageManager>();
                Debug.Log("SystemMessageManager 자동 생성됨");
            }
            else
            {
                Debug.Log("SystemMessageManager 이미 존재함");
            }
            
            Debug.Log("InitializeSystemMessageManager 완료");
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
                currentPanel.Hide();
            }

            // 새 패널 표시
            Debug.Log($"panels 딕셔너리에서 {state} 찾는 중...");
            if (panels.TryGetValue(state, out BlokusUnity.UI.PanelBase newPanel))
            {
                Debug.Log($"패널 딕셔너리에서 찾음: {newPanel != null}");
                
                if (newPanel == null)
                {
                    Debug.LogWarning($"Panel is null for state: {state}. 해당 Panel이 생성되지 않았거나 연결되지 않았습니다.");
                    return;
                }
                
                // gameObject null 체크 추가
                if (newPanel.gameObject == null)
                {
                    Debug.LogError($"Panel gameObject is null for state: {state}. Panel: {newPanel.GetType().Name}");
                    return;
                }
                
                Debug.Log($"패널 찾음: {newPanel.name}");
                Debug.Log($"패널 타입: {newPanel.GetType().Name}");
                Debug.Log($"패널 Active: {newPanel.gameObject.activeInHierarchy}");
                
                currentPanel = newPanel;
                currentState = state;
                
                Debug.Log("패널 Show() 호출 직전");
                currentPanel.Show();
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
            if (BlokusUnity.Features.Single.StageDataManager.Instance != null)
            {
                BlokusUnity.Features.Single.StageDataManager.Instance.SelectStage(stageNumber);
                BlokusUnity.Features.Single.StageDataManager.Instance.PassDataToSingleGameManager();
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
                currentPanel.Hide();

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
                currentPanel.Hide();
            }

            Debug.Log("Multi Gameplay Scene Loaded");
        }
    }
}