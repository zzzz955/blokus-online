using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Shared.UI;
using App.Services;
using Features.Single.Core;
using Shared.Models;
using App.Core;
namespace App.UI
{
    public class ModeSelectionPanel : Shared.UI.PanelBase
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button multiPlayerButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button backButton; // 로그아웃 버튼으로 사용
        [SerializeField] private TMP_Text GreatingMessage;

        [Header("게임 종료 모달")]
        [SerializeField] private GameExitModal gameExitModal;

        private void OnEnable()
        {
            // 나중에 값이 들어오는 상황까지 커버하고 싶다면(선택):
            if (SessionManager.Instance != null)
                SessionManager.Instance.OnUserDataReceived += OnUserDataReceived;
        }
        private void OnDisable()
        {
            if (SessionManager.Instance != null)
                SessionManager.Instance.OnUserDataReceived -= OnUserDataReceived;
        }

        private void OnUserDataReceived(string username, int _)
        {
            var displayName = SessionManager.Instance?.DisplayName;
            UpdateGreetingMessage(displayName ?? username);
        }
        protected override void Start()
        {
            base.Start();
            Debug.Log("ModeSelectionPanel 초기화 완료");

            // 인스펙터 할당 버튼 이벤트 연결
            SetupButtons();
            SetupTexts();

            // GameExitModal 참조 확인
            if (gameExitModal == null)
            {
                Debug.LogWarning("[ModeSelectionPanel] GameExitModal이 Inspector에서 할당되지 않음 - 자동으로 찾기 시도");
                gameExitModal = FindObjectOfType<GameExitModal>();

                if (gameExitModal != null)
                {
                    Debug.Log($"[ModeSelectionPanel] GameExitModal 자동으로 발견: {gameExitModal.name}");
                }
                else
                {
                    Debug.LogError("[ModeSelectionPanel] GameExitModal을 찾을 수 없습니다! Scene에 GameExitModal이 있는지 확인하세요.");
                }
            }
            else
            {
                Debug.Log($"[ModeSelectionPanel] GameExitModal 참조 확인됨: {gameExitModal.name}");
            }
        }

        private void Update()
        {
            // Android 뒤로가기 버튼 처리 (에뮬레이터에서는 ESC키)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnAndroidBackButtonPressed();
            }
        }

        /// <summary>
        /// Android 뒤로가기 버튼 클릭 시 처리
        /// ModeSelectPanel이 메인 패널일 때만 GameExitModal 표시
        /// </summary>
        private void OnAndroidBackButtonPressed()
        {
            Debug.Log("[ModeSelectionPanel] Android 뒤로가기 버튼 클릭됨");

            // 게임 씬이 로드되어 있는지 확인
            bool singleSceneLoaded = UnityEngine.SceneManagement.SceneManager.GetSceneByName("SingleGameplayScene").isLoaded;
            bool multiSceneLoaded = UnityEngine.SceneManagement.SceneManager.GetSceneByName("MultiGameplayScene").isLoaded;

            if (singleSceneLoaded || multiSceneLoaded)
            {
                Debug.Log("[ModeSelectionPanel] 게임 씬이 활성화되어 있어 뒤로가기 버튼 무시");
                return;
            }

            // 게임 종료 모달이 이미 표시 중이면 무시
            if (gameExitModal != null && gameExitModal.gameObject.activeInHierarchy)
            {
                Debug.Log("[ModeSelectionPanel] 게임 종료 모달이 이미 표시 중");
                return;
            }

            // GameExitModal 표시
            if (gameExitModal != null)
            {
                Debug.Log("[ModeSelectionPanel] ModeSelectPanel이 메인 패널 - 게임 종료 모달 표시");
                gameExitModal.ShowModal();
            }
            else
            {
                Debug.LogWarning("[ModeSelectionPanel] GameExitModal이 설정되지 않음 - 직접 종료");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        private void SetupTexts()
        {
            if (GreatingMessage == null)
            {
                Debug.LogWarning("GreatingMessage가 인스펙터에서 할당되지 않았습니다!");
                return;
            }

            // SessionManager에서 displayName 가져오기 (fallback: username)
            var displayName = SessionManager.Instance?.DisplayName;
            if (string.IsNullOrEmpty(displayName))
                displayName = SessionManager.Instance?.CachedId;

            UpdateGreetingMessage(displayName);
        }
        
        private void UpdateGreetingMessage(string displayName)
        {
            if (GreatingMessage == null) return;
            
            GreatingMessage.text = string.IsNullOrEmpty(displayName) 
                ? "환영합니다!" 
                : $"환영합니다! {displayName}님";
                
            Debug.Log($"[ModeSelectionPanel] 인사말 업데이트: {GreatingMessage.text}");
        }

        /// <summary>
        /// 인스펙터에서 할당된 버튼들의 이벤트 연결
        /// </summary>
        private void SetupButtons()
        {
            if (singlePlayerButton != null)
            {
                singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);
                Debug.Log("싱글플레이 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogWarning("singlePlayerButton이 인스펙터에서 할당되지 않았습니다!");
            }

            if (multiPlayerButton != null)
            {
                multiPlayerButton.onClick.AddListener(OnMultiPlayerClicked);
            }
            else
            {
                Debug.LogWarning("multiPlayerButton이 인스펙터에서 할당되지 않았습니다!");
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnMultiPlayerClicked);
                settingsButton.interactable = false;
                Debug.Log("멀티플레이 버튼 이벤트 연결 완료, 비활성화(스텁)");
            }
            else
            {
                Debug.LogWarning("settingsButton 인스펙터에서 할당되지 않았습니다!");
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(OnLogoutButtonClicked);
                Debug.Log("로그아웃 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogWarning("backButton(로그아웃)이 인스펙터에서 할당되지 않았습니다!");
            }

            Debug.Log("ModeSelectionPanel 버튼 설정 완료");
        }

        private string TryGetDisplayNameNow()
        {
            // 1) CacheManager에 이미 라이트 동기화가 반영되어 있으면 그걸 사용
            var cm = CacheManager.Instance;
            var profile = cm?.GetUserProfile();
            if (profile != null && !string.IsNullOrEmpty(profile.displayName))
                return profile.displayName;

            // 2) 로그인 응답을 UserDataCache가 이미 들고 있다면 그걸 사용
            var udc = UserDataCache.Instance;
            var user = udc?.GetCurrentUser();
            if (user != null && !string.IsNullOrEmpty(user.display_name))
                return user.display_name;

            // (선택) 3) 정말 아무것도 없으면 null 반환 — 이벤트로 나중에 갱신
            return null;
        }

        private void OnUserProfileUpdated(UserProfileData data)
        {
            if (GreatingMessage != null && data != null && !string.IsNullOrEmpty(data.displayName))
                GreatingMessage.text = $"환영합니다! {data.displayName}님";
        }

        private void OnUserDataUpdated(UserInfo info)
        {
            if (GreatingMessage != null && info != null && !string.IsNullOrEmpty(info.display_name))
                GreatingMessage.text = $"환영합니다! {info.display_name}님";
        }

        public void OnSinglePlayerClicked()
        {
            Debug.Log("싱글플레이 버튼 클릭");

            var uiManager = UIManager.GetInstanceSafe();
            if (uiManager != null)
            {
                Debug.Log("[ModeSelectionPanel] UIManager로 싱글플레이 모드 선택");
                uiManager.OnSingleModeSelected();
            }
            else
            {
                Debug.LogError("[ModeSelectionPanel] UIManager를 찾을 수 없습니다!");
            }
        }

        public void OnMultiPlayerClicked()
        {
            Debug.Log("멀티플레이 버튼 클릭");

            var uiManager = UIManager.GetInstanceSafe();
            if (uiManager != null)
            {
                Debug.Log("[ModeSelectionPanel] UIManager로 멀티플레이 모드 선택");
                uiManager.OnMultiModeSelected();
            }
            else
            {
                Debug.LogError("[ModeSelectionPanel] UIManager를 찾을 수 없습니다!");
            }
        }

        public void OnLogoutButtonClicked()
        {
            Debug.Log("로그아웃 버튼 클릭");

            if (SessionManager.Instance != null)
            {
                Debug.Log("[ModeSelectionPanel] SessionManager로 로그아웃 처리");
                SessionManager.Instance.LogoutAndClearSession();

                // 로그아웃 후 LoginPanel로 이동
                var uiManager = UIManager.GetInstanceSafe();
                if (uiManager != null)
                {
                    Debug.Log("[ModeSelectionPanel] UIManager로 로그인 화면 복귀");
                    uiManager.ShowPanel(App.UI.UIState.Login);
                }
                else
                {
                    Debug.LogError("[ModeSelectionPanel] UIManager를 찾을 수 없습니다!");
                }
            }
            else
            {
                Debug.LogError("[ModeSelectionPanel] SessionManager를 찾을 수 없습니다!");
            }
        }
    }
}