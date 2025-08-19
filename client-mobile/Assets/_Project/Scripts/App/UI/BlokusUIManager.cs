using System.Collections.Generic;
using UnityEngine;
using Shared.UI;

namespace App.UI
{
    /// <summary>
    /// 새로운 Blokus UI 매니저 - Inspector SerializeField 복구
    /// 클래스명을 변경하여 Unity 캐시 문제 해결
    /// </summary>
    public class BlokusUIManager : MonoBehaviour
    {
        [Header("===== UI PANELS =====")]
        [Space(10)]
        
        [SerializeField]
        [Tooltip("로그인 패널 - LoginPanelController가 있는 GameObject")]
        public PanelBase loginPanel;
        
        [SerializeField]
        [Tooltip("모드 선택 패널 - ModeSelectionPanel GameObject")]
        public PanelBase modeSelectionPanel;
        
        [SerializeField]
        [Tooltip("스테이지 선택 패널 - StageSelect GameObject")]
        public PanelBase stageSelectPanel;

        [Header("===== DEBUG INFO =====")]
        [SerializeField] private bool enableDebugLogs = true;

        private Dictionary<UIState, PanelBase> panels;
        private UIState currentState = (UIState)(-1);
        private PanelBase currentPanel;

        public static BlokusUIManager Instance { get; private set; }

        void Awake()
        {
            if (enableDebugLogs) Debug.Log("=== BlokusUIManager Awake 시작 ===");
            
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                
                // Inspector 필드 상태 확인
                if (enableDebugLogs)
                {
                    Debug.Log($"[BlokusUIManager] Inspector 필드 확인:");
                    Debug.Log($"  loginPanel: {loginPanel != null} -> {loginPanel}");
                    Debug.Log($"  modeSelectionPanel: {modeSelectionPanel != null} -> {modeSelectionPanel}");
                    Debug.Log($"  stageSelectPanel: {stageSelectPanel != null} -> {stageSelectPanel}");
                }
                
                InitializePanels();
                InitializeSystemMessageManager();
                
                if (enableDebugLogs) Debug.Log("BlokusUIManager 초기화 완료");
            }
            else
            {
                if (enableDebugLogs) Debug.Log("BlokusUIManager 중복 인스턴스 제거");
                Destroy(gameObject);
            }
        }

        private void InitializePanels()
        {
            if (enableDebugLogs) Debug.Log("=== InitializePanels 시작 ===");
            
            // 동적으로 패널 찾기 (Inspector 참조 백업)
            if (loginPanel == null)
            {
                if (enableDebugLogs) Debug.Log("[BlokusUIManager] LoginPanel null, 동적 탐색 중...");
                var loginController = Object.FindObjectOfType<LoginPanelController>();
                if (loginController != null)
                {
                    loginPanel = loginController.GetComponent<PanelBase>();
                    if (enableDebugLogs && loginPanel != null) Debug.Log("[BlokusUIManager] LoginPanel 동적 탐색 성공");
                }
            }
            
            if (modeSelectionPanel == null)
            {
                if (enableDebugLogs) Debug.Log("[BlokusUIManager] ModeSelectionPanel null, 동적 탐색 중...");
                var modePanel = GameObject.Find("ModeSelectionPanel");
                if (modePanel != null)
                {
                    modeSelectionPanel = modePanel.GetComponent<PanelBase>();
                    if (enableDebugLogs && modeSelectionPanel != null) Debug.Log("[BlokusUIManager] ModeSelectionPanel 동적 탐색 성공");
                }
            }

            panels = new Dictionary<UIState, PanelBase>
            {
                { UIState.Login, loginPanel },
                { UIState.ModeSelection, modeSelectionPanel },
                { UIState.StageSelect, stageSelectPanel }
            };

            // 패널 상태 로깅
            if (enableDebugLogs)
            {
                Debug.Log($"[BlokusUIManager] 최종 패널 상태:");
                Debug.Log($"  Login: {loginPanel != null}");
                Debug.Log($"  ModeSelection: {modeSelectionPanel != null}");
                Debug.Log($"  StageSelect: {stageSelectPanel != null}");
            }

            // LoginPanel 제외하고 모든 패널 숨기기
            foreach (var kvp in panels)
            {
                if (kvp.Value != null && kvp.Key != UIState.Login)
                {
                    kvp.Value.Hide();
                    if (enableDebugLogs) Debug.Log($"[BlokusUIManager] {kvp.Key} 패널 숨김");
                }
            }
        }

        private void InitializeSystemMessageManager()
        {
            if (enableDebugLogs) Debug.Log("=== InitializeSystemMessageManager 시작 ===");
            
            if (SystemMessageManager.Instance == null)
            {
                GameObject messageManagerObj = new GameObject("SystemMessageManager");
                messageManagerObj.AddComponent<SystemMessageManager>();
                if (enableDebugLogs) Debug.Log("SystemMessageManager 자동 생성됨");
            }
            else
            {
                if (enableDebugLogs) Debug.Log("SystemMessageManager 이미 존재함");
            }
        }

        void Start()
        {
            if (enableDebugLogs) Debug.Log("=== BlokusUIManager Start 시작 ===");
            
            // 게임 시작시 로그인 패널 표시
            ShowPanel(UIState.Login, false);
            
            if (enableDebugLogs) Debug.Log("BlokusUIManager Start 완료");
        }

        /// <summary>
        /// UI 패널 전환
        /// </summary>
        public void ShowPanel(UIState state, bool animated = true)
        {
            Debug.Log($"========== [BlokusUIManager] ShowPanel 요청: {state} ==========");
            Debug.Log($"[BlokusUIManager] 현재 상태: currentState={currentState}, currentPanel={currentPanel?.name ?? "null"}");
            Debug.Log($"[BlokusUIManager] panels 딕셔너리 상태:");
            if (panels != null)
            {
                foreach (var kvp in panels)
                {
                    Debug.Log($"  {kvp.Key}: {(kvp.Value != null ? kvp.Value.name : "null")}");
                }
            }
            else
            {
                Debug.LogError("[BlokusUIManager] panels 딕셔너리가 null입니다!");
                return;
            }

            if (currentState == state && currentPanel != null) 
            {
                Debug.Log($"[BlokusUIManager] 동일한 패널이 이미 활성화됨: {state}");
                return;
            }

            // 현재 패널 숨기기
            if (currentPanel != null)
            {
                Debug.Log($"[BlokusUIManager] 현재 패널 숨기기: {currentPanel.name}");
                try
                {
                    currentPanel.Hide();
                    Debug.Log($"[BlokusUIManager] 현재 패널 숨기기 완료");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[BlokusUIManager] 현재 패널 숨기기 실패: {ex.Message}");
                }
            }

            // 새 패널 표시
            if (panels.TryGetValue(state, out PanelBase newPanel))
            {
                Debug.Log($"[BlokusUIManager] 새 패널 발견: {newPanel?.name ?? "null"}");
                
                if (newPanel != null)
                {
                    currentPanel = newPanel;
                    currentState = state;
                    
                    Debug.Log($"[BlokusUIManager] 새 패널 Show() 호출 시작: {newPanel.name}");
                    try
                    {
                        currentPanel.Show();
                        Debug.Log($"[BlokusUIManager] 새 패널 Show() 호출 완료: {newPanel.name}");
                        Debug.Log($"[BlokusUIManager] 패널 활성화 상태: {currentPanel.gameObject.activeInHierarchy}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[BlokusUIManager] 새 패널 Show() 실패: {ex.Message}");
                    }
                    
                    Debug.Log($"========== [BlokusUIManager] 패널 전환 완료: {state} ==========");
                }
                else
                {
                    Debug.LogWarning($"[BlokusUIManager] 패널이 null입니다: {state}");
                }
            }
            else
            {
                Debug.LogError($"[BlokusUIManager] 패널을 찾을 수 없습니다: {state}");
                Debug.LogError($"[BlokusUIManager] 사용 가능한 패널 목록:");
                foreach (var key in panels.Keys)
                {
                    Debug.LogError($"  - {key}");
                }
            }
        }

        // ========================================
        // UI 전환 이벤트 함수들
        // ========================================

        public void OnLoginSuccess()
        {
            if (enableDebugLogs) Debug.Log("[BlokusUIManager] OnLoginSuccess() 호출됨");
            
            // 강화된 로깅으로 상태 확인
            if (enableDebugLogs)
            {
                Debug.Log($"[BlokusUIManager] 현재 패널 상태:");
                Debug.Log($"  Current State: {currentState}");
                Debug.Log($"  Current Panel: {(currentPanel != null ? currentPanel.name : "null")}");
                Debug.Log($"  ModeSelection Panel: {(modeSelectionPanel != null ? modeSelectionPanel.name : "null")}");
                Debug.Log($"  Panels Dictionary Count: {panels?.Count ?? 0}");
            }
            
            ShowPanel(UIState.ModeSelection);
            
            if (enableDebugLogs) Debug.Log("[BlokusUIManager] OnLoginSuccess() 완료");
        }

        public void OnSingleModeSelected()
        {
            if (enableDebugLogs) Debug.Log("[BlokusUIManager] OnSingleModeSelected() 호출됨");
            
            // 5-씬 아키텍처: SingleCore 및 SingleGameplayScene 로드 필요
            if (enableDebugLogs) Debug.Log("[BlokusUIManager] SingleCore 씬 전환 시작");
            
            // SceneFlowController를 통한 씬 전환 시도
            var sceneFlowController = App.Core.SceneFlowController.Instance;
            if (sceneFlowController != null)
            {
                if (enableDebugLogs) Debug.Log("[BlokusUIManager] SceneFlowController 발견! GoSingle() 호출");
                sceneFlowController.StartGoSingle();
            }
            else
            {
                Debug.LogError("[BlokusUIManager] SceneFlowController를 찾을 수 없습니다!");
                
                // 폴백: 직접 씬 로드 시도
                if (enableDebugLogs) Debug.Log("[BlokusUIManager] 폴백: 직접 SingleGameplayScene 로드 시도");
                StartCoroutine(LoadSingleGameplaySceneDirectly());
            }
        }
        
        /// <summary>
        /// 폴백: 직접 SingleGameplayScene 로드
        /// </summary>
        private System.Collections.IEnumerator LoadSingleGameplaySceneDirectly()
        {
            Debug.Log("[BlokusUIManager] 직접 SingleGameplayScene 로드 시작");
            
            // 로딩 표시 (LoadingOverlay 사용)
            App.UI.LoadingOverlay.Show("싱글플레이 화면 로딩 중...");
            
            // SingleGameplayScene Additive 로드
            var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SingleGameplayScene", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
            
            Debug.Log("[BlokusUIManager] SingleGameplayScene 로드 완료, StageSelect 패널 탐색");
            
            // 씬 로드 후 StageSelect 패널 찾기
            yield return new WaitForSeconds(0.5f); // 씬 초기화 대기
            
            var stageSelectComponent = Object.FindObjectOfType<Features.Single.UI.StageSelect.CandyCrushStageMapView>();
            if (stageSelectComponent != null)
            {
                panels[UIState.StageSelect] = stageSelectComponent.GetComponent<PanelBase>();
                Debug.Log("[BlokusUIManager] StageSelect 패널 동적 탐색 성공");
                
                // 로딩 숨기기
                App.UI.LoadingOverlay.Hide();
                
                // StageSelect 표시
                ShowPanel(UIState.StageSelect);
            }
            else
            {
                Debug.LogError("[BlokusUIManager] StageSelect 패널을 찾을 수 없습니다!");
                
                // 로딩 숨기기
                App.UI.LoadingOverlay.Hide();
                
                // 메인 화면으로 돌아가기
                ShowPanel(UIState.ModeSelection);
                
                // 사용자에게 알림
                if (SystemMessageManager.Instance != null)
                {
                    SystemMessageManager.ShowToast("싱글플레이 화면을 로드할 수 없습니다.", MessagePriority.Error);
                }
            }
        }

        public void OnMultiModeSelected()
        {
            ShowPanel(UIState.Lobby);
        }

        /// <summary>
        /// 🔥 개선: SceneFlowController 기반으로 Scene 전환
        /// </summary>
        public void OnStageSelected(int stageNumber)
        {
            if (enableDebugLogs) Debug.Log($"[BlokusUIManager] 스테이지 {stageNumber} 선택됨");

            // 1. 스테이지 데이터 설정
            if (Features.Single.Core.StageDataManager.Instance != null)
            {
                Features.Single.Core.StageDataManager.Instance.SelectStage(stageNumber);
                Features.Single.Core.StageDataManager.Instance.PassDataToSingleGameManager();
                if (enableDebugLogs) Debug.Log($"[BlokusUIManager] StageDataManager에 스테이지 {stageNumber} 설정 완료");
            }
            else
            {
                Debug.LogError("[BlokusUIManager] StageDataManager.Instance가 null입니다!");
            }

            // 2. SceneFlowController를 통해 SingleGameplayScene 전환
            if (App.Core.SceneFlowController.Instance != null)
            {
                if (enableDebugLogs) Debug.Log("[BlokusUIManager] SceneFlowController로 SingleGameplayScene 전환 시작");
                StartCoroutine(App.Core.SceneFlowController.Instance.GoSingle());
            }
            else
            {
                Debug.LogError("[BlokusUIManager] SceneFlowController.Instance가 null입니다! 레거시 방식으로 전환 시도");
                // 백업: 레거시 방식
                LoadSingleGameplayScene();
            }
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
            if (currentPanel != null && currentState == UIState.Loading)
                currentPanel.Hide();
        }

        /// <summary>
        /// 안전한 인스턴스 접근
        /// </summary>
        public static BlokusUIManager GetInstance()
        {
            if (Instance == null)
            {
                Instance = Object.FindObjectOfType<BlokusUIManager>();
            }
            return Instance;
        }

        /// <summary>
        /// 디버그 로그 활성화/비활성화
        /// </summary>
        public void SetDebugLogs(bool enabled)
        {
            enableDebugLogs = enabled;
            Debug.Log($"[BlokusUIManager] 디버그 로그 {(enabled ? "활성화" : "비활성화")}");
        }
    }
}