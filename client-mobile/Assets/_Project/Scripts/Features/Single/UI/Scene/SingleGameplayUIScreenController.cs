// Assets/_Project/Scripts/Features/Single/UI/Scene/SingleGameplayUIScreenController.cs
using UnityEngine;
using Features.Single.Gameplay; // SingleGameManager
using Shared.UI; // ConfirmModal
using App.Audio; // AudioManager

namespace Features.Single.UI.Scene
{
    /// <summary>
    /// 싱글플레이 씬의 패널 전환 전담 컨트롤러
    /// - 진입 시: StageSelectPanel ON, GamePanel OFF
    /// - 게임 시작(OnGameReady): StageSelectPanel은 그대로 유지(ON), GamePanel만 ON
    /// - 게임 종료/나가기: GamePanel만 OFF → StageSelect 화면으로 복귀
    /// </summary>
    public class SingleGameplayUIScreenController : MonoBehaviour
    {
        [Header("Scene Panels")]
        [SerializeField] private GameObject stageSelectPanelRoot; // ex) "StageSelectPanel"
        [SerializeField] private GameObject gamePanelRoot;        // ex) "GamePanel"
        [SerializeField] private bool verboseLog = true;

        [Header("Back Button & Modals")]
        [SerializeField] private ConfirmModal exitConfirmModal; // 게임 나가기 확인 모달
        [SerializeField] private Features.Single.UI.StageSelect.StageInfoModal stageInfoModal; // 스테이지 정보 모달

        private void Awake()
        {
            if (!stageSelectPanelRoot) stageSelectPanelRoot = GameObject.Find("StageSelectPanel");
            if (!gamePanelRoot) gamePanelRoot = GameObject.Find("GamePanel");

            //  씬 진입 초기 상태 강제
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf) stageSelectPanelRoot.SetActive(true);
            if (gamePanelRoot && gamePanelRoot.activeSelf) gamePanelRoot.SetActive(false);

            if (verboseLog) Debug.Log("[UIScreenController] 초기 상태: StageSelect=ON, GamePanel=OFF");
            
            // ExitConfirmModal 자동 찾기 (설정되지 않은 경우)
            if (!exitConfirmModal) exitConfirmModal = FindObjectOfType<ConfirmModal>();
            
            // StageInfoModal 자동 찾기 (설정되지 않은 경우)
            if (!stageInfoModal) stageInfoModal = Features.Single.UI.StageSelect.StageInfoModal.Instance;
        }

        private void OnEnable()
        {
            SingleGameManager.OnGameReady -= HandleGameReady;
            SingleGameManager.OnGameReady += HandleGameReady;

            // 늦게 들어와도 보정: 이미 초기화 끝난 상태면 즉시 전환
            var gm = SingleGameManager.Instance;
            if (gm != null && gm.IsInitialized) HandleGameReady();
        }

        private void OnDisable()
        {
            SingleGameManager.OnGameReady -= HandleGameReady;
        }

        // Android 뒤로가기 처리는 BackButtonManager에서 전역 관리

        /// <summary>
        /// StageSelectPanel에서 뒤로가기 - BackButton과 동일한 이벤트
        /// </summary>
        private void HandleBackButtonFromStageSelect()
        {
            Debug.Log("[SingleGameplayUIScreenController] StageSelectPanel에서 뒤로가기 - MainScene으로 이동");
            
            // 더 안전한 방법: UIManager의 기존 메서드 사용
            var uiManager = App.UI.UIManager.GetInstanceSafe();
            if (uiManager != null)
            {
                Debug.Log("[SingleGameplayUIScreenController] UIManager.OnExitSingleToModeSelection() 사용");
                uiManager.OnExitSingleToModeSelection();
            }
            else
            {
                // 폴백: 직접 구현한 방법 사용
                Debug.LogWarning("[SingleGameplayUIScreenController] UIManager 없음 - 직접 구현 방법 사용");
                if (App.Core.SceneFlowController.Instance != null)
                {
                    StartCoroutine(ExitToMainAndShowModeSelection());
                }
                else
                {
                    Debug.LogError("[SingleGameplayUIScreenController] SceneFlowController도 없습니다!");
                    UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
                }
            }
        }

        /// <summary>
        /// MainScene으로 이동 후 ModeSelectionPanel 표시
        /// </summary>
        private System.Collections.IEnumerator ExitToMainAndShowModeSelection()
        {
            Debug.Log("[SingleGameplayUIScreenController] MainScene 이동 및 ModeSelectionPanel 표시 시작");
            
            // SceneFlowController로 MainScene 전환
            yield return StartCoroutine(App.Core.SceneFlowController.Instance.ExitSingleToMain());
            
            // 추가 대기 시간으로 씬 전환 완료 보장
            yield return new WaitForSeconds(0.5f);
            
            // LoadingOverlay와 InputLocker 강제 해제 (SceneFlowController에서 처리되지 않은 경우 대비)
            if (App.UI.LoadingOverlay.Instance != null)
            {
                App.UI.LoadingOverlay.Hide();
                Debug.Log("[SingleGameplayUIScreenController] LoadingOverlay 강제 숨김");
            }
            
            if (App.UI.InputLocker.Instance != null)
            {
                App.UI.InputLocker.Disable();
                Debug.Log("[SingleGameplayUIScreenController] InputLocker 강제 비활성화");
            }
            
            // MainScene으로 돌아온 후 UIManager를 통해 ModeSelectionPanel 표시
            var uiManager = App.UI.UIManager.GetInstanceSafe();
            if (uiManager != null)
            {
                Debug.Log("[SingleGameplayUIScreenController] UIManager를 통해 ModeSelectionPanel 표시");
                uiManager.ShowPanel(App.UI.UIState.ModeSelection);
                
                // 추가 확인: ModeSelectionPanel이 실제로 활성화되었는지 검증
                yield return new WaitForSeconds(0.1f);
                
                var modeSelectionPanel = GameObject.Find("ModeSelectionPanel");
                if (modeSelectionPanel != null && modeSelectionPanel.activeInHierarchy)
                {
                    Debug.Log("[SingleGameplayUIScreenController]  ModeSelectionPanel 활성화 확인됨");
                }
                else
                {
                    Debug.LogWarning("[SingleGameplayUIScreenController] ⚠️ ModeSelectionPanel이 활성화되지 않음 - 직접 활성화 시도");
                    if (modeSelectionPanel != null)
                    {
                        modeSelectionPanel.SetActive(true);
                    }
                }
            }
            else
            {
                Debug.LogError("[SingleGameplayUIScreenController] UIManager를 찾을 수 없습니다!");
            }
            
            Debug.Log("[SingleGameplayUIScreenController]  MainScene 복귀 및 ModeSelectionPanel 표시 완료");
        }

        /// <summary>
        /// GamePanel에서 뒤로가기 - TopBarUI.OnClickExit과 동일한 이벤트
        /// </summary>
        private void HandleExitButtonFromGame()
        {
            Debug.Log("[SingleGameplayUIScreenController] GamePanel에서 뒤로가기 - TopBarUI.OnClickExit과 동일한 처리");
            
            // TopBarUI 찾기
            var topBarUI = FindObjectOfType<Features.Single.UI.InGame.TopBarUI>();
            if (topBarUI != null)
            {
                // TopBarUI의 OnClickExit과 동일한 로직 수행
                var confirmationModal = GetConfirmationModal();
                if (confirmationModal != null)
                {
                    // 확인 모달 표시
                    confirmationModal.ShowExitConfirmation(
                        onConfirm: ExitToSelection,
                        onCancel: () => { Debug.Log("[SingleGameplayUIScreenController] 게임 종료 취소"); }
                    );
                }
                else
                {
                    // 바로 종료
                    ExitToSelection();
                }
            }
            else
            {
                Debug.LogWarning("[SingleGameplayUIScreenController] TopBarUI를 찾을 수 없음 - 직접 ExitConfirmModal 처리");
                // 폴백: ExitConfirmModal 직접 처리
                if (exitConfirmModal != null)
                {
                    exitConfirmModal.ShowModal();
                }
                else
                {
                    Debug.LogWarning("[SingleGameplayUIScreenController] ExitConfirmModal도 설정되지 않음 - 직접 StageSelect로 복귀");
                    ShowSelection();
                }
            }
        }

        /// <summary>
        /// ConfirmationModal 찾기 (TopBarUI 로직과 동일)
        /// </summary>
        private ConfirmModal GetConfirmationModal()
        {
            // 활성 오브젝트에서 먼저 찾기
            var active = FindObjectOfType<ConfirmModal>();
            if (active != null) return active;
            
            // 비활성까지 검색
            var all = Resources.FindObjectsOfTypeAll<ConfirmModal>();
            if (all != null && all.Length > 0)
            {
                var confirmationModal = all[0];
                // 모달이 비활성 GameObject라면 활성화
                if (!confirmationModal.gameObject.activeInHierarchy)
                    confirmationModal.gameObject.SetActive(true);
                return confirmationModal;
            }
            
            return null;
        }

        /// <summary>
        /// 도중 나가기: TopBarUI.ExitToSelection과 동일한 로직
        /// </summary>
        private void ExitToSelection()
        {
            Debug.Log("[SingleGameplayUIScreenController] ExitToSelection - TopBarUI와 동일한 처리");
            
            var gm = SingleGameManager.Instance;
            if (gm != null) gm.OnExitRequested(); // 유지(서버에 포기/실패 보고)

            // ShowSelection 호출 (GamePanel 비활성, StageSelectPanel 활성)
            ShowSelection();
        }

        /// <summary>
        /// 게임 시작 준비 완료 → GamePanel만 ON (StageSelect는 그대로 둠)
        /// </summary>
        private void HandleGameReady()
        {
            if (verboseLog) Debug.Log("[UIScreenController] OnGameReady → GamePanel ON (StageSelect KEEP)");

            // StageSelect는 절대 끄지 않는다
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf)
                stageSelectPanelRoot.SetActive(true);

            if (gamePanelRoot && !gamePanelRoot.activeSelf)
                gamePanelRoot.SetActive(true);
        }

        /// <summary>
        /// 외부에서 '게임 화면만' 켜고 싶을 때 사용 (StageSelect는 건드리지 않음)
        /// </summary>
        public void ShowGameplay()
        {
            if (gamePanelRoot && !gamePanelRoot.activeSelf) gamePanelRoot.SetActive(true);
            if (verboseLog) Debug.Log("[UIScreenController] ShowGameplay → GamePanel ON (StageSelect KEEP)");
        }

        /// <summary>
        ///  수정: 외부에서 '선택 화면'으로 복귀할 때 사용 (GamePanel만 OFF, UI 안정화 보장)
        /// </summary>
        public void ShowSelection()
        {
            //  수정: StageSelectPanel 강제 활성화 우선 처리
            if (stageSelectPanelRoot)
            {
                if (!stageSelectPanelRoot.activeSelf)
                {
                    stageSelectPanelRoot.SetActive(true);
                    if (verboseLog) Debug.Log("[UIScreenController] ShowSelection → StageSelectPanel 강제 활성화");
                }
                
                //  추가: CandyCrushStageMapView 컴포넌트 즉시 활성화 확인
                var stageMapView = stageSelectPanelRoot.GetComponent<Features.Single.UI.StageSelect.CandyCrushStageMapView>();
                if (stageMapView != null && !stageMapView.gameObject.activeSelf)
                {
                    stageMapView.gameObject.SetActive(true);
                    if (verboseLog) Debug.Log("[UIScreenController] ShowSelection → CandyCrushStageMapView 강제 활성화");
                }
            }
            
            // GamePanel 비활성화
            if (gamePanelRoot && gamePanelRoot.activeSelf)
            {
                gamePanelRoot.SetActive(false);
                if (verboseLog) Debug.Log("[UIScreenController] ShowSelection → GamePanel 비활성화");
            }

            // BGM: Lobby BGM으로 복귀 (게임 종료/클리어/중도 퇴장)
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(BGMTrack.Lobby);
                if (verboseLog) Debug.Log("[UIScreenController] ShowSelection → Lobby BGM 전환");
            }

            if (verboseLog) Debug.Log("[UIScreenController] ShowSelection → GamePanel OFF, StageSelect ON");

            //  추가: UI 안정화를 위한 코루틴 시작
            StartCoroutine(EnsureUIStabilityAfterShowSelection());
        }
        
        /// <summary>
        ///  신규: ShowSelection 후 UI 안정화 보장 코루틴
        /// </summary>
        private System.Collections.IEnumerator EnsureUIStabilityAfterShowSelection()
        {
            // 1프레임 대기로 UI 업데이트 완료 보장
            yield return null;
            
            // StageSelectPanel 재확인
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf)
            {
                Debug.LogWarning("[UIScreenController] UI 안정화 - StageSelectPanel 재활성화");
                stageSelectPanelRoot.SetActive(true);
            }
            
            //  추가: CandyCrushStageMapView의 버튼 위치 재검증
            if (stageSelectPanelRoot)
            {
                var stageMapView = stageSelectPanelRoot.GetComponent<Features.Single.UI.StageSelect.CandyCrushStageMapView>();
                if (stageMapView != null)
                {
                    // ForceRefreshStageButtons 메서드 호출로 위치 재보정
                    stageMapView.SendMessage("ForceRefreshStageButtons", SendMessageOptions.DontRequireReceiver);
                    if (verboseLog) Debug.Log("[UIScreenController] UI 안정화 - CandyCrushStageMapView 강제 리프레시 완료");
                }
            }
        }

        /// <summary>
        /// 결과 모달/언락 코루틴 안전 보장을 위해 StageSelect를 반드시 켜두고 싶을 때 호출
        /// </summary>
        public void EnsureSelectionActive()
        {
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf)
            {
                stageSelectPanelRoot.SetActive(true);
                if (verboseLog) Debug.Log("[UIScreenController] EnsureSelectionActive → StageSelect forced ON");
            }
        }

        /// <summary>
        /// 결과 모달 '확인/배경 클릭' 등에서 호출: 선택화면 노출 + 게임화면 닫기
        /// </summary>
        public void ReturnToSelectionAndHideGame()
        {
            EnsureSelectionActive();
            if (gamePanelRoot && gamePanelRoot.activeSelf) gamePanelRoot.SetActive(false);
            if (verboseLog) Debug.Log("[UIScreenController] ReturnToSelectionAndHideGame → StageSelect ON, GamePanel OFF");
        }

        public void EnsureStageSelectVisible()
        {
            if (stageSelectPanelRoot && !stageSelectPanelRoot.activeSelf)
                stageSelectPanelRoot.SetActive(true); //  강제로 켜기
        }
    }
}
