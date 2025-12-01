using UnityEngine;
using UnityEngine.SceneManagement;
using Shared.UI;

namespace App.UI
{
    /// <summary>
    /// 전역 Android 뒤로가기 버튼 관리자
    /// 씬과 UI 상태에 따라 우선순위 기반으로 뒤로가기 동작 처리
    /// </summary>
    public class BackButtonManager : MonoBehaviour
    {
        public static BackButtonManager Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                if (enableDebugLogs) Debug.Log("[BackButtonManager] 초기화 완료");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Android 뒤로가기 버튼 감지 (에뮬레이터에서는 ESC)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleBackButton();
            }
        }

        /// <summary>
        /// 뒤로가기 버튼 처리 - 우선순위 기반
        /// </summary>
        private void HandleBackButton()
        {
            if (enableDebugLogs) Debug.Log("[BackButtonManager] ESC 버튼 감지");

            // 1순위: 활성화된 모달 닫기
            if (TryCloseActiveModal())
            {
                if (enableDebugLogs) Debug.Log("[BackButtonManager] 활성 모달 닫기 처리됨");
                return;
            }

            // 2순위: 현재 씬 상태에 따른 처리
            if (HandleGameScenes())
            {
                return;
            }

            // 3순위: MainScene 패널 상태에 따른 처리
            HandleMainScenePanels();
        }

        /// <summary>
        /// 활성화된 모달이 있으면 닫기
        /// </summary>
        private bool TryCloseActiveModal()
        {
            // StageInfoModal 체크 (비활성화된 오브젝트도 검색)
            var stageInfoModal = FindObjectOfType<Features.Single.UI.StageSelect.StageInfoModal>(true);
            if (stageInfoModal != null && stageInfoModal.gameObject.activeInHierarchy)
            {
                stageInfoModal.HideModal();
                return true;
            }

            // ConfirmModal 체크 (비활성화된 오브젝트도 검색)
            var confirmModal = FindObjectOfType<ConfirmModal>(true);
            if (confirmModal != null && confirmModal.gameObject.activeInHierarchy)
            {
                // ConfirmModal은 명시적으로 닫는 메서드가 있다면 호출, 없으면 무시
                return true; // 이미 표시 중이면 처리된 것으로 간주
            }

            // SettingsModal 체크 (비활성화된 오브젝트도 검색)
            var settingsModal = FindObjectOfType<UI.Settings.SettingsModal>(true);
            if (settingsModal != null && settingsModal.gameObject.activeInHierarchy)
            {
                settingsModal.HideModal();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 게임 씬(Single/Multi) 상태에 따른 처리
        /// </summary>
        private bool HandleGameScenes()
        {
            // SingleGameplayScene 체크
            bool singleSceneLoaded = SceneManager.GetSceneByName("SingleGameplayScene").isLoaded;
            if (singleSceneLoaded)
            {
                return HandleSingleGameplayScene();
            }

            // MultiGameplayScene 체크
            bool multiSceneLoaded = SceneManager.GetSceneByName("MultiGameplayScene").isLoaded;
            if (multiSceneLoaded)
            {
                return HandleMultiGameplayScene();
            }

            return false;
        }

        /// <summary>
        /// 싱글 플레이 씬 처리
        /// </summary>
        private bool HandleSingleGameplayScene()
        {
            var screenController = FindObjectOfType<Features.Single.UI.Scene.SingleGameplayUIScreenController>(true);
            if (screenController == null)
            {
                if (enableDebugLogs) Debug.LogWarning("[BackButtonManager] SingleGameplayUIScreenController를 찾을 수 없음");
                return false;
            }

            // GamePanel과 StageSelectPanel 상태 확인
            var gamePanel = GameObject.Find("GamePanel");
            var stageSelectPanel = GameObject.Find("StageSelectPanel");

            bool isGamePanelActive = gamePanel != null && gamePanel.activeSelf;
            bool isStageSelectPanelActive = stageSelectPanel != null && stageSelectPanel.activeSelf;

            if (isGamePanelActive)
            {
                // 게임 플레이 중 -> GamePanel 하위의 ConfirmModal 표시 (게임 나가기)
                if (enableDebugLogs) Debug.Log("[BackButtonManager] 싱글 게임 플레이 중 -> GamePanel의 ConfirmModal");

                // GamePanel 하위의 ConfirmModal 찾기
                var confirmModal = gamePanel.GetComponentInChildren<ConfirmModal>(true);
                if (confirmModal != null)
                {
                    confirmModal.ShowExitConfirmation(
                        onConfirm: () => {
                            if (enableDebugLogs) Debug.Log("[BackButtonManager] 게임 나가기 확인됨 -> StageSelect로");
                            screenController.SendMessage("ShowSelection", SendMessageOptions.DontRequireReceiver);
                        },
                        onCancel: () => {
                            if (enableDebugLogs) Debug.Log("[BackButtonManager] 게임 나가기 취소됨");
                        }
                    );
                }
                else
                {
                    if (enableDebugLogs) Debug.LogError("[BackButtonManager] GamePanel 하위에서 ConfirmModal을 찾을 수 없음");
                }
                return true;
            }
            else if (isStageSelectPanelActive && !isGamePanelActive)
            {
                // 스테이지 선택 화면 -> ExitConfirmModal 표시 (MainScene으로 나가기 확인)
                if (enableDebugLogs) Debug.Log("[BackButtonManager] 스테이지 선택 화면 -> ExitConfirmModal");

                // SingleGameplayScene의 exitConfirmModal 찾기
                var exitConfirmModal = FindObjectOfType<ConfirmModal>(true);
                if (exitConfirmModal != null)
                {
                    exitConfirmModal.ShowExitConfirmation(
                        onConfirm: () => {
                            if (enableDebugLogs) Debug.Log("[BackButtonManager] MainScene으로 나가기 확인됨");

                            var uiManager = UIManager.GetInstanceSafe();
                            if (uiManager != null)
                            {
                                uiManager.OnExitSingleToModeSelection();
                            }
                        },
                        onCancel: () => {
                            if (enableDebugLogs) Debug.Log("[BackButtonManager] MainScene으로 나가기 취소됨");
                        }
                    );
                }
                else
                {
                    if (enableDebugLogs) Debug.LogError("[BackButtonManager] ExitConfirmModal을 찾을 수 없음");
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 멀티 플레이 씬 처리
        /// </summary>
        private bool HandleMultiGameplayScene()
        {
            // LobbyPanel 체크 (비활성화된 오브젝트도 검색)
            var lobbyPanel = FindObjectOfType<Features.Multi.UI.LobbyPanel>(true);
            if (lobbyPanel != null && lobbyPanel.gameObject.activeInHierarchy)
            {
                // 로비 상태 -> LogoutConfirmModal 표시
                if (enableDebugLogs) Debug.Log("[BackButtonManager] 멀티 로비 -> LogoutConfirmModal");
                lobbyPanel.SendMessage("OnLogoutButtonClicked", SendMessageOptions.DontRequireReceiver);
                return true;
            }

            // GameRoomPanel 체크 (비활성화된 오브젝트도 검색)
            var gameRoomPanel = FindObjectOfType<Features.Multi.UI.GameRoomPanel>(true);
            if (gameRoomPanel != null && gameRoomPanel.gameObject.activeInHierarchy)
            {
                // 게임룸 상태 -> 방 나가기
                if (enableDebugLogs) Debug.Log("[BackButtonManager] 멀티 게임룸 -> 방 나가기");
                gameRoomPanel.SendMessage("OnAndroidBackButtonPressed", SendMessageOptions.DontRequireReceiver);
                return true;
            }

            return false;
        }

        /// <summary>
        /// MainScene 패널 처리
        /// </summary>
        private void HandleMainScenePanels()
        {
            // LoginPanel 체크 (비활성화된 오브젝트도 검색)
            var loginPanel = FindObjectOfType<LoginPanel>(true);
            if (loginPanel != null && loginPanel.gameObject.activeInHierarchy)
            {
                // LoginPanel -> 게임 종료 모달
                if (enableDebugLogs) Debug.Log("[BackButtonManager] LoginPanel -> GameExitModal");
                ShowGameExitModal();
                return;
            }

            // ModeSelectionPanel 체크 (비활성화된 오브젝트도 검색)
            var modeSelectionPanel = FindObjectOfType<ModeSelectionPanel>(true);
            if (modeSelectionPanel != null && modeSelectionPanel.gameObject.activeInHierarchy)
            {
                // ModeSelectionPanel -> 게임 종료 모달
                if (enableDebugLogs) Debug.Log("[BackButtonManager] ModeSelectionPanel -> GameExitModal");
                ShowGameExitModal();
                return;
            }

            // 기타 상황
            if (enableDebugLogs) Debug.LogWarning("[BackButtonManager] 처리할 수 없는 상태");
        }

        /// <summary>
        /// 게임 종료 모달 표시
        /// </summary>
        private void ShowGameExitModal()
        {
            // 비활성화된 오브젝트도 검색
            var gameExitModal = FindObjectOfType<GameExitModal>(true);
            if (gameExitModal != null)
            {
                if (enableDebugLogs) Debug.Log($"[BackButtonManager] GameExitModal 찾음: {gameExitModal.name}");
                gameExitModal.ShowModal();
            }
            else
            {
                if (enableDebugLogs) Debug.LogError("[BackButtonManager] GameExitModal을 찾을 수 없음 - 직접 종료");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}
