using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Features.Single.Core;
using Shared.UI;
using App.Core;

namespace App.UI
{
    /// <summary>
    /// Unity 블로블로 UI 매니저
    /// 모든 UI 패널 전환을 중앙에서 관리
    /// 
    /// Inspector SerializeField 참조 복구용 강화 버전
    /// FormerlySerializedAs를 통한 필드 복구
    /// </summary>
    [System.Serializable]
    public class UIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] 
        [FormerlySerializedAs("loginPanel")]
        public PanelBase loginPanel;
        
        [SerializeField]
        [FormerlySerializedAs("modeSelectionPanel")] 
        public PanelBase modeSelectionPanel;
        
        [SerializeField]
        [FormerlySerializedAs("stageSelectPanel")]
        public PanelBase stageSelectPanel;

        //  추가: 게임 패널 (SingleGameplayScene 전환 시 사용)
        [Header("Game Integration")]
        [SerializeField] private bool enableGameIntegration = true;
        
        //  BlokusUIManager 기능 통합
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogs = true;

        private Dictionary<UIState, PanelBase> panels;
        private UIState currentState = (UIState)(-1); // 초기값을 무효한 값으로 설정
        private PanelBase currentPanel;

        public static UIManager Instance { get; private set; }
        
        /// <summary>
        ///  개선: 안전한 UIManager 접근 (MainScene 및 Scene 전환 지원)
        /// </summary>
        public static UIManager GetInstance()
        {
            if (Instance == null)
            {
                // UIManager 찾기 시도
                Instance = Object.FindObjectOfType<UIManager>();
                if (Instance != null)
                {
                    Debug.Log("[UIManager] Instance를 씬에서 재발견했습니다");
                }
                else
                {
                    Debug.LogWarning("[UIManager] UIManager Instance를 찾을 수 없습니다.");
                }
            }
            return Instance;
        }

        /// <summary>
        ///  추가: Scene 전환 지원을 위한 강화된 Instance 접근
        /// </summary>
        public static UIManager GetInstanceSafe()
        {
            var manager = GetInstance();
            if (manager == null)
            {
                Debug.LogError("[UIManager] Instance를 찾을 수 없습니다. MainScene이 활성화되어 있는지 확인하세요.");
            }
            return manager;
        }

        void Awake()
        {
            Debug.Log("=== UIManager Awake 시작 ===");
            Debug.Log($"[UIManager] 스크립트 컴파일 상태 확인 - GetType(): {GetType()}");
            Debug.Log($"[UIManager] Inspector 필드 상태:");
            Debug.Log($"  loginPanel SerializeField: {loginPanel != null} (값: {loginPanel})");
            Debug.Log($"  modeSelectionPanel SerializeField: {modeSelectionPanel != null} (값: {modeSelectionPanel})");
            Debug.Log($"  stageSelectPanel SerializeField: {stageSelectPanel != null} (값: {stageSelectPanel})");
            
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                Debug.Log("UIManager 싱글톤 설정 완료");
                
                // MainScene에 유지 (패널 참조 유지를 위해 DontDestroyOnLoad 사용 안함)
                Debug.Log("UIManager MainScene에 유지됨 - 패널 참조 보존");
                
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
                if (Features.Single.Core.UserDataCache.Instance != null && Features.Single.Core.UserDataCache.Instance.IsLoggedIn())
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
                // 일반적인 게임 시작 - 자동 로그인 상태 확인
                CheckAutoLoginStatus();
            }
            
            Debug.Log("UIManager Start 완료");
        }

        /// <summary>
        /// 자동 로그인 상태 확인 (UIManager 초기화 후)
        /// </summary>
        private void CheckAutoLoginStatus()
        {
            Debug.Log("[UIManager] 자동 로그인 상태 확인 시작");

            //  수정: SceneFlowController의 자동 로그인 상태를 확인
            var autoLoginState = App.Core.SceneFlowController.GetAutoLoginState();
            Debug.Log($"[UIManager] SceneFlowController 자동 로그인 상태: {autoLoginState}");

            if (autoLoginState == App.Core.SceneFlowController.AutoLoginState.Success)
            {
                // 자동 로그인 성공 → ModeSelection 패널로 바로 이동
                Debug.Log("[UIManager] 자동 로그인 성공 - ModeSelection 패널 표시");
                ShowPanel(UIState.ModeSelection, false);
            }
            else if (autoLoginState == App.Core.SceneFlowController.AutoLoginState.Failed)
            {
                // 자동 로그인 실패 → Login 패널 표시
                Debug.Log("[UIManager] 자동 로그인 실패 - Login 패널 표시");
                ShowPanel(UIState.Login, false);
            }
            else if (autoLoginState == App.Core.SceneFlowController.AutoLoginState.InProgress)
            {
                // 자동 로그인 진행 중 → 잠시 후 다시 체크
                Debug.Log("[UIManager] 자동 로그인 진행 중 - 1초 후 재체크");
                StartCoroutine(DelayedAutoLoginCheck());
            }
            else
            {
                // NotChecked 또는 기타 → 기존 로직으로 폴백
                Debug.Log("[UIManager] 자동 로그인 상태 미확정 - 기존 로직 사용");
                
                if (SessionManager.Instance != null && SessionManager.Instance.IsLoggedIn)
                {
                    Debug.Log("[UIManager] SessionManager 기준 로그인됨 - ModeSelection 패널 표시");
                    ShowPanel(UIState.ModeSelection, false);
                }
                else
                {
                    Debug.Log("[UIManager] SessionManager 기준 로그인 안됨 - Login 패널 표시");
                    ShowPanel(UIState.Login, false);
                }
            }
        }

        /// <summary>
        /// 자동 로그인이 진행 중일 때 지연된 재체크
        /// </summary>
        private System.Collections.IEnumerator DelayedAutoLoginCheck()
        {
            // 최대 5초까지 기다리면서 자동 로그인 완료 대기
            float timeout = 5f;
            while (timeout > 0)
            {
                yield return new WaitForSeconds(1f);
                timeout -= 1f;

                var autoLoginState = App.Core.SceneFlowController.GetAutoLoginState();
                Debug.Log($"[UIManager] 재체크 - 자동 로그인 상태: {autoLoginState}");

                if (autoLoginState == App.Core.SceneFlowController.AutoLoginState.Success)
                {
                    Debug.Log("[UIManager] 지연된 자동 로그인 성공 - ModeSelection 패널 표시");
                    ShowPanel(UIState.ModeSelection, false);
                    yield break;
                }
                else if (autoLoginState == App.Core.SceneFlowController.AutoLoginState.Failed)
                {
                    Debug.Log("[UIManager] 지연된 자동 로그인 실패 - Login 패널 표시");
                    ShowPanel(UIState.Login, false);
                    yield break;
                }
                // InProgress면 계속 대기
            }

            // 타임아웃 - 기본적으로 Login 패널 표시
            Debug.LogWarning("[UIManager] 자동 로그인 체크 타임아웃 - Login 패널 표시");
            ShowPanel(UIState.Login, false);
        }

        /// <summary>
        /// UI 패널 딕셔너리 초기화
        /// </summary>
        private void InitializePanels()
        {
            Debug.Log("=== InitializePanels 시작 ===");
            
            // 런타임에 패널들을 동적으로 찾기 (Inspector 참조가 사라진 경우 대비)
            if (loginPanel == null)
            {
                Debug.Log("[UIManager] LoginPanel이 null, 동적으로 찾는 중...");
                loginPanel = Object.FindObjectOfType<LoginPanel>()?.GetComponent<PanelBase>();
                if (loginPanel != null) Debug.Log("[UIManager] LoginPanel 동적으로 찾음");
            }
            
            if (modeSelectionPanel == null)
            {
                Debug.Log("[UIManager] ModeSelectionPanel이 null, 동적으로 찾는 중...");
                var modePanel = GameObject.Find("ModeSelectionPanel");
                if (modePanel != null) 
                {
                    modeSelectionPanel = modePanel.GetComponent<PanelBase>();
                    if (modeSelectionPanel != null) Debug.Log("[UIManager] ModeSelectionPanel 동적으로 찾음");
                }
            }
            
            if (stageSelectPanel == null)
            {
                Debug.Log("[UIManager] StageSelectPanel이 null, CandyCrushStageMapView 찾는 중...");
                // CandyCrushStageMapView를 찾기 (SingleGameplayScene에 있을 수 있음)
                var candyCrushView = Object.FindObjectOfType<Features.Single.UI.StageSelect.CandyCrushStageMapView>();
                if (candyCrushView != null)
                {
                    stageSelectPanel = candyCrushView.GetComponent<PanelBase>();
                    if (stageSelectPanel != null) Debug.Log("[UIManager] CandyCrushStageMapView를 StageSelectPanel로 동적 할당");
                }
                
                // 백업: GameObject 이름으로 찾기
                if (stageSelectPanel == null)
                {
                    var stagePanel = GameObject.Find("StageSelectPanel");
                    if (stagePanel != null)
                    {
                        stageSelectPanel = stagePanel.GetComponent<PanelBase>();
                        if (stageSelectPanel != null) Debug.Log("[UIManager] StageSelectPanel GameObject로 동적으로 찾음");
                    }
                }
            }
            
            panels = new Dictionary<UIState, PanelBase>
            {
                { UIState.Login, loginPanel },
                { UIState.ModeSelection, modeSelectionPanel },
                { UIState.StageSelect, stageSelectPanel }
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
            if (enableDebugLogs)
            {
                Debug.Log($"=== ShowPanel 시작 ===");
                Debug.Log($"요청된 State: {state}");
                Debug.Log($"현재 State: {currentState}");
                Debug.Log($"현재 패널: {(currentPanel != null ? currentPanel.name : "null")}");
                Debug.Log($"현재 패널 활성 상태: {(currentPanel != null ? currentPanel.gameObject.activeInHierarchy.ToString() : "N/A")}");
            }
            
            //  수정: 패널이 실제로 활성화되어 있을 때만 early return 
            // StageSelect 패널의 경우 씬 간 전환이 있을 수 있으므로 재초기화 허용
            if (currentState == state && currentPanel != null && currentPanel.gameObject.activeInHierarchy && state != UIState.StageSelect) 
            {
                if (enableDebugLogs)
                    Debug.Log("동일한 State이고 패널이 활성화되어 있어서 return");
                return;
            }

            //  추가: currentPanel이 null이 아니지만 gameObject가 파괴된 경우 상태 리셋
            if (currentPanel != null && currentPanel.gameObject == null)
            {
                Debug.LogWarning($"[UIManager] currentPanel의 gameObject가 파괴됨 - 상태 리셋");
                currentPanel = null;
                currentState = (UIState)(-1);
            }

            // 현재 패널 숨기기
            if (currentPanel != null)
            {
                Debug.Log($"현재 패널 숨기기: {currentPanel.name}");
                currentPanel.Hide();
            }

            // 새 패널 표시
            Debug.Log($"panels 딕셔너리에서 {state} 찾는 중...");
            if (panels.TryGetValue(state, out PanelBase newPanel))
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
            
             구현할 전환 함수들:
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
            Debug.Log("[UIManager] OnLoginSuccess() 호출됨");
            ShowPanel(UIState.ModeSelection);
        }

        public void OnSingleModeSelected()
        {
            Debug.Log("[UIManager] OnSingleModeSelected() 호출됨");
            
            //  핵심 해결: 스테이지 선택을 먼저 표시하고, 실제 게임플레이는 스테이지 선택 후에
            if (App.Core.SceneFlowController.Instance != null)
            {
                Debug.Log("[UIManager] SceneFlowController로 스테이지 선택 모드 진입");
                StartCoroutine(LoadScenesForStageSelection());
            }
            else
            {
                Debug.LogError("[UIManager] SceneFlowController.Instance가 null입니다!");
                SystemMessageManager.ShowToast("싱글플레이 화면을 로드할 수 없습니다.", MessagePriority.Error);
            }
        }
        
        /// <summary>
        ///  핵심 수정: 스테이지 선택 화면으로만 진입 (게임플레이 초기화 안함)
        /// </summary>
        private IEnumerator LoadScenesForStageSelection()
        {
            Debug.Log("[UIManager] 스테이지 선택 화면 로드 시작");
            
            // 1. SingleCore와 SingleGameplayScene을 로드하되, 게임 데이터 없이 스테이지 선택용으로만 사용
            // 2. SingleGameManager.IsInGameplayMode = false 상태로 유지 (스테이지 선택 모드)
            
            //  중요: CurrentStage = 0으로 설정하여 테스트 데이터 초기화 방지
            Features.Single.Gameplay.SingleGameManager.SetStageContext(0, null);
            
            // SceneFlowController의 GoSingle을 호출 (하지만 스테이지 데이터는 없음)
            yield return StartCoroutine(App.Core.SceneFlowController.Instance.GoSingle());
            
            // MainScene 패널들을 조건부로 숨김 (스테이지 선택 모드에서는 MainScene 패널 유지)
            HideMainScenePanelsForStageSelection();
            
            Debug.Log("[UIManager]  스테이지 선택 화면 준비 완료 - IsInGameplayMode = false");
        }
        
        /// <summary>
        ///  추가: 스테이지 선택 모드에 따른 패널 표시/숨김 제어
        /// </summary>
        private void HideMainScenePanelsForStageSelection()
        {
            // 스테이지 선택 모드에서는 MainScene 패널들을 유지
            // (실제 게임플레이가 시작되면 그때 숨김)
            Debug.Log("[UIManager] 스테이지 선택 모드 - MainScene 패널들 유지");
        }

        public void OnMultiModeSelected()
        {
            Debug.Log("[UIManager] OnMultiModeSelected() 호출됨");
            
            // 멀티플레이 버튼 비활성화
            DisableMultiplayerButton();
            
            // 멀티플레이 모드 선택 시 MultiCore 씬으로 전환
            if (App.Core.SceneFlowController.Instance != null)
            {
                Debug.Log("[UIManager] SceneFlowController로 멀티플레이 모드 진입");
                App.Core.SceneFlowController.Instance.StartGoMulti();
            }
            else
            {
                Debug.LogError("[UIManager] SceneFlowController.Instance가 null입니다!");
                SystemMessageManager.ShowToast("멀티플레이 화면을 로드할 수 없습니다.", MessagePriority.Error);
                
                // 실패 시 버튼 재활성화
                EnableMultiplayerButton();
            }
        }

        /// <summary>
        ///  핵심 수정: 스테이지 선택 후 실제 게임플레이 모드로 전환
        /// </summary>
        public void OnStageSelected(int stageNumber)
        {
            Debug.Log($"[UIManager] 스테이지 {stageNumber} 선택됨 - 게임플레이 모드 전환 시작");

            // 1. 스테이지 데이터 매니저를 통해 스테이지 선택
            if (Features.Single.Core.StageDataManager.Instance != null)
            {
                Features.Single.Core.StageDataManager.Instance.SelectStage(stageNumber);
                
                //  중요: 게임플레이 모드로 전환하기 위한 스테이지 컨텍스트 설정
                Features.Single.Gameplay.SingleGameManager.SetStageContext(stageNumber, Features.Single.Core.StageDataManager.Instance);
                Debug.Log($"[UIManager] SingleGameManager 스테이지 컨텍스트 설정: {stageNumber} (IsInGameplayMode=true)");
                
                // StageDataManager에 데이터 전달
                Features.Single.Core.StageDataManager.Instance.PassDataToSingleGameManager();
                Debug.Log($"[UIManager] StageDataManager에 스테이지 {stageNumber} 설정 완료");
            }
            else
            {
                Debug.LogError("[UIManager] StageDataManager.Instance가 null입니다!");
                return;
            }

            // 2.  핵심: 게임플레이 모드로 전환 (Scene은 이미 로드됨)
            StartCoroutine(TransitionToGameplayMode());
        }
        
        /// <summary>
        ///  수정: 스테이지 선택 모드에서 게임플레이 모드로 전환 (StageSelectPanel 유지)
        /// 기획 의도: StageSelectPanel 비활성화하지 않고 GamePanel만 활성화
        /// </summary>
        private IEnumerator TransitionToGameplayMode()
        {
            Debug.Log("[UIManager] 게임플레이 모드 전환 시작");
            
            //  핵심 수정: MainScene 패널들 숨기되, StageSelectPanel은 유지
            HideMainScenePanelsForGameplay();
            
            // 2.  핵심 수정: SingleGameManager 초기화 + UI 활성화
            // Scene은 이미 로드되어 있으므로 SingleGameManager 직접 호출
            var singleGameManager = Features.Single.Gameplay.SingleGameManager.Instance;
            if (singleGameManager != null && Features.Single.Gameplay.SingleGameManager.CurrentStage > 0)
            {
                Debug.Log($"[UIManager] SingleGameManager 발견 - 스테이지 {Features.Single.Gameplay.SingleGameManager.CurrentStage} 초기화 시작");
                
                // 스테이지 데이터를 이용해 게임 초기화 (GameBoard, BlockPalette 등 초기화)
                singleGameManager.RequestStartByNumber(Features.Single.Gameplay.SingleGameManager.CurrentStage);
                Debug.Log("[UIManager] SingleGameManager 초기화 완료");
                
                // UI 활성화 (SingleGameManager.Init에서 OnGameReady 이벤트가 발생하여 자동 활성화됨)
                yield return new WaitForSeconds(0.1f); // Init 완료 대기
            }
            else
            {
                Debug.LogError("[UIManager] SingleGameManager Instance가 없거나 CurrentStage가 설정되지 않음!");
                
                // 백업: UI만이라도 활성화
                var screenController = Object.FindObjectOfType<Features.Single.UI.Scene.SingleGameplayUIScreenController>();
                if (screenController != null)
                {
                    Debug.Log("[UIManager] 백업: UIScreenController를 통한 GamePanel 활성화");
                    screenController.ShowGameplay();
                }
                else
                {
                    var gamePanel = GameObject.Find("GamePanel");
                    if (gamePanel != null)
                    {
                        gamePanel.SetActive(true);
                        Debug.Log("[UIManager] 백업: GamePanel 직접 활성화 완료");
                    }
                }
            }
            
            Debug.Log("[UIManager]  게임플레이 모드 전환 완료 - 게임 시작!");
        }
        
        /// <summary>
        ///  신규: 게임플레이 시작 시 MainScene 패널 숨기기 (StageSelectPanel 제외)
        /// 기획 의도: StageSelectPanel은 유지하고 다른 패널들만 숨김
        /// </summary>
        private void HideMainScenePanelsForGameplay()
        {
            Debug.Log("[UIManager] 게임플레이 시작 - MainScene 패널들 숨기기 (StageSelectPanel 제외)");
            
            // Login과 ModeSelection 패널만 숨기기 (StageSelect는 유지)
            if (panels.TryGetValue(UIState.Login, out var loginPanel) && loginPanel != null)
            {
                Debug.Log("[UIManager] Login 패널 숨기기");
                loginPanel.Hide();
            }
            
            if (panels.TryGetValue(UIState.ModeSelection, out var modePanel) && modePanel != null)
            {
                Debug.Log("[UIManager] ModeSelection 패널 숨기기");
                modePanel.Hide();
                
                //  중요: ModeSelection 패널이 숨겨지면 currentState와 currentPanel을 리셋
                // 나중에 ShowPanel(ModeSelection)이 호출될 때 정상적으로 표시되도록 함
                if (currentState == UIState.ModeSelection && currentPanel == modePanel)
                {
                    Debug.Log("[UIManager] ModeSelection 패널 숨김으로 인한 상태 리셋");
                    currentState = (UIState)(-1);
                    currentPanel = null;
                }
            }
            
            //  핵심: StageSelect 패널은 유지 (기획 의도)
            if (panels.TryGetValue(UIState.StageSelect, out var stagePanel) && stagePanel != null)
            {
                if (!stagePanel.gameObject.activeSelf)
                {
                    Debug.Log("[UIManager] StageSelect 패널 활성화 유지");
                    stagePanel.Show();
                }
            }
            
            Debug.Log("[UIManager] MainScene 패널 숨기기 완료 - StageSelectPanel 유지됨");
        }

        /// <summary>
        ///  기존: MainScene의 모든 패널 숨기기 (완전 게임플레이 모드 또는 씬 전환 시)
        /// </summary>
        private void HideAllMainScenePanels()
        {
            Debug.Log("[UIManager] 게임플레이 시작 - 모든 MainScene 패널 숨기기");
            
            // 현재 활성 패널이 있다면 숨기기
            if (currentPanel != null)
            {
                Debug.Log($"[UIManager] 현재 패널 숨기기: {currentPanel.name}");
                currentPanel.Hide();
                currentPanel = null;
                currentState = (UIState)(-1); // 무효 상태로 설정
            }
            
            // 모든 MainScene 패널들을 강제로 숨기기
            foreach (var kvp in panels)
            {
                if (kvp.Value != null)
                {
                    Debug.Log($"[UIManager] 패널 강제 숨기기: {kvp.Key}");
                    kvp.Value.Hide();
                }
            }
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
            Debug.Log("[UIManager] OnBackToMenu() 호출됨");
            
            // 현재 상태에 따라 다른 동작 수행
            if (currentState == UIState.ModeSelection)
            {
                // ModeSelection에서 뒤로가기 = 로그아웃
                Debug.Log("[UIManager] ModeSelection에서 뒤로가기 - 로그아웃 처리");
                OnLogoutRequested();
            }
            else if (currentState == UIState.StageSelect)
            {
                // StageSelect에서 뒤로가기 = SingleGameplayScene 언로드 후 ModeSelection으로
                Debug.Log("[UIManager] StageSelect에서 뒤로가기 - SingleGameplayScene 언로드 후 ModeSelection으로");
                OnExitSingleToModeSelection();
            }
            else
            {
                // 기본 동작 - ModeSelection으로
                ShowPanel(UIState.ModeSelection);
            }
        }

        /// <summary>
        /// 로그아웃 요청 처리
        /// </summary>
        public void OnLogoutRequested()
        {
            Debug.Log("[UIManager] 로그아웃 처리 시작");
            
            // SessionManager를 통해 로그아웃
            if (App.Core.SessionManager.Instance != null)
            {
                App.Core.SessionManager.Instance.Logout();
                Debug.Log("[UIManager] SessionManager.Logout() 호출 완료");
            }
            else
            {
                Debug.LogWarning("[UIManager] SessionManager.Instance가 null입니다!");
            }
            
            // 로그인 화면으로 전환
            ShowPanel(UIState.Login);
        }

        /// <summary>
        /// SingleGameplayScene에서 ModeSelection으로 돌아가기
        /// </summary>
        public void OnExitSingleToModeSelection()
        {
            Debug.Log("[UIManager] SingleGameplayScene에서 ModeSelection으로 돌아가기 시작");
            
            if (App.Core.SceneFlowController.Instance != null)
            {
                // ExitSingleToMain 코루틴 실행 후 ModeSelection 패널 표시
                StartCoroutine(ExitSingleAndShowModeSelection());
            }
            else
            {
                Debug.LogError("[UIManager] SceneFlowController.Instance가 null입니다!");
                // Fallback: 단순히 ModeSelection 패널만 표시
                ShowPanel(UIState.ModeSelection);
            }
        }

        /// <summary>
        /// SingleGameplayScene 언로드 후 ModeSelection 패널 표시
        /// </summary>
        private System.Collections.IEnumerator ExitSingleAndShowModeSelection()
        {
            Debug.Log("[UIManager] ExitSingleAndShowModeSelection 코루틴 시작");
            
            // SceneFlowController의 ExitSingleToMain 실행 (SingleGameplayScene 언로드)
            yield return StartCoroutine(App.Core.SceneFlowController.Instance.ExitSingleToMain());
            
            Debug.Log("[UIManager] ExitSingleToMain 완료 - ModeSelection 패널 표시");
            
            // MainScene으로 돌아온 후 ModeSelection 패널 표시
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
        
        // ========================================
        //  BlokusUIManager 호환성 메서드들
        // ========================================
        
        /// <summary>
        /// BlokusUIManager 호환: StartGoSingle() 래퍼 메서드
        /// </summary>
        public void StartGoSingle()
        {
            if (enableDebugLogs) Debug.Log("[UIManager] StartGoSingle() 호출됨 - GoSingle() 코루틴 시작");
            
            if (App.Core.SceneFlowController.Instance != null)
            {
                StartCoroutine(App.Core.SceneFlowController.Instance.GoSingle());
            }
            else
            {
                Debug.LogError("[UIManager] SceneFlowController.Instance가 null입니다!");
            }
        }
        
        /// <summary>
        /// BlokusUIManager 호환: 디버그 로그 설정
        /// </summary>
        public void SetDebugLogs(bool enabled)
        {
            enableDebugLogs = enabled;
            Debug.Log($"[UIManager] 디버그 로그 {(enabled ? "활성화" : "비활성화")}");
        }
        
        // ========================================
        // 멀티플레이 버튼 제어
        // ========================================
        
        /// <summary>
        /// 멀티플레이 버튼 비활성화
        /// </summary>
        public void DisableMultiplayerButton()
        {
            var modeSelectionPanel = panels.TryGetValue(UIState.ModeSelection, out var panel) ? panel : null;
            if (modeSelectionPanel != null)
            {
                var modePanel = modeSelectionPanel.GetComponent<ModeSelectionPanel>();
                if (modePanel != null)
                {
                    var multiButton = modePanel.GetComponent<UnityEngine.UI.Button>();
                    if (multiButton == null)
                    {
                        // ModeSelectionPanel의 multiPlayerButton 필드 직접 접근
                        var field = typeof(ModeSelectionPanel).GetField("multiPlayerButton", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            var button = field.GetValue(modePanel) as UnityEngine.UI.Button;
                            if (button != null)
                            {
                                button.interactable = false;
                                Debug.Log("[UIManager] 멀티플레이 버튼 비활성화");
                            }
                        }
                    }
                }
            }
            
            if (enableDebugLogs)
                Debug.Log("[UIManager] DisableMultiplayerButton() 호출");
        }
        
        /// <summary>
        /// 멀티플레이 버튼 재활성화
        /// </summary>
        public void EnableMultiplayerButton()
        {
            var modeSelectionPanel = panels.TryGetValue(UIState.ModeSelection, out var panel) ? panel : null;
            if (modeSelectionPanel != null)
            {
                var modePanel = modeSelectionPanel.GetComponent<ModeSelectionPanel>();
                if (modePanel != null)
                {
                    var multiButton = modePanel.GetComponent<UnityEngine.UI.Button>();
                    if (multiButton == null)
                    {
                        // ModeSelectionPanel의 multiPlayerButton 필드 직접 접근
                        var field = typeof(ModeSelectionPanel).GetField("multiPlayerButton", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            var button = field.GetValue(modePanel) as UnityEngine.UI.Button;
                            if (button != null)
                            {
                                button.interactable = true;
                                Debug.Log("[UIManager] 멀티플레이 버튼 재활성화");
                            }
                        }
                    }
                }
            }
            
            if (enableDebugLogs)
                Debug.Log("[UIManager] EnableMultiplayerButton() 호출");
        }
        
    }
}