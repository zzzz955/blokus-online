using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BlokusUnity.Data;

namespace BlokusUnity.UI
{
    /// <summary>
    /// 스테이지 맵에서 CacheManager 통합 예제
    /// EnsureMetadataLoaded 게이트 및 사용자 경험 최적화
    /// </summary>
    public class StageMapIntegration : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Button[] stageButtons;
        [SerializeField] private GameObject loadingOverlay;
        [SerializeField] private Text loadingText;
        [SerializeField] private Text userProfileText;

        [Header("설정")]
        [SerializeField] private bool enableDebugLog = false;

        private CacheManager cacheManager;
        private bool isInitialized = false;

        void Start()
        {
            // CacheManager 대기
            StartCoroutine(WaitForCacheManagerAndInitialize());
        }

        /// <summary>
        /// CacheManager 초기화 대기 및 설정
        /// </summary>
        private IEnumerator WaitForCacheManagerAndInitialize()
        {
            // CacheManager 인스턴스 대기
            yield return new WaitUntil(() => CacheManager.Instance != null);
            
            cacheManager = CacheManager.Instance;
            
            // 이벤트 구독
            cacheManager.OnUserProfileUpdated += OnUserProfileUpdated;
            cacheManager.OnMetadataLoaded += OnMetadataLoaded;
            cacheManager.OnProgressDataUpdated += OnProgressDataUpdated;
            cacheManager.OnSyncCompleted += OnSyncCompleted;

            // EnsureMetadataLoaded 게이트 실행
            yield return StartCoroutine(EnsureMetadataLoadedGate());

            isInitialized = true;
            
            if (enableDebugLog)
                Debug.Log("[StageMapIntegration] 초기화 완료");
        }

        /// <summary>
        /// 메타데이터 로드 게이트 - 스테이지 맵 표시 전 필수 실행
        /// </summary>
        private IEnumerator EnsureMetadataLoadedGate()
        {
            if (enableDebugLog)
                Debug.Log("[StageMapIntegration] EnsureMetadataLoaded 게이트 시작");

            // 로딩 UI 표시
            ShowLoadingUI("스테이지 정보 로딩 중...");

            // 스테이지 버튼들 비활성화
            SetStageButtonsEnabled(false);

            // 메타데이터 로드 확인
            yield return StartCoroutine(cacheManager.EnsureMetadataLoaded());

            // 사용자 프로필 업데이트
            UpdateUserProfileUI();

            // 스테이지 버튼들 업데이트
            UpdateStageButtons();

            // 로딩 UI 숨김
            HideLoadingUI();

            // 스테이지 버튼들 활성화
            SetStageButtonsEnabled(true);

            if (enableDebugLog)
                Debug.Log("[StageMapIntegration] EnsureMetadataLoaded 게이트 완료");
        }

        /// <summary>
        /// 로딩 UI 표시
        /// </summary>
        private void ShowLoadingUI(string message = "로딩 중...")
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(true);
            
            if (loadingText != null)
                loadingText.text = message;
        }

        /// <summary>
        /// 로딩 UI 숨김
        /// </summary>
        private void HideLoadingUI()
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(false);
        }

        /// <summary>
        /// 스테이지 버튼들 활성화/비활성화
        /// </summary>
        private void SetStageButtonsEnabled(bool enabled)
        {
            if (stageButtons != null)
            {
                foreach (var button in stageButtons)
                {
                    if (button != null)
                        button.interactable = enabled;
                }
            }
        }

        /// <summary>
        /// 사용자 프로필 UI 업데이트
        /// </summary>
        private void UpdateUserProfileUI()
        {
            var profile = cacheManager.GetUserProfile();
            if (profile != null && userProfileText != null)
            {
                userProfileText.text = $"Lv.{profile.level} | 최대 스테이지: {profile.maxStageCompleted}";
            }
        }

        /// <summary>
        /// 스테이지 버튼들 상태 업데이트
        /// </summary>
        private void UpdateStageButtons()
        {
            if (stageButtons == null) return;

            for (int i = 0; i < stageButtons.Length; i++)
            {
                int stageNumber = i + 1;
                UpdateStageButton(stageNumber, stageButtons[i]);
            }
        }

        /// <summary>
        /// 개별 스테이지 버튼 상태 업데이트
        /// </summary>
        private void UpdateStageButton(int stageNumber, Button button)
        {
            if (button == null) return;

            // 진행도 정보 가져오기
            var progress = cacheManager.GetStageProgress(stageNumber);
            var metadata = cacheManager.GetStageMetadata(stageNumber);

            // 버튼 텍스트 업데이트
            var buttonText = button.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                string displayText = $"Stage {stageNumber}";
                
                if (progress.isCompleted)
                {
                    displayText += $"\n★{progress.starsEarned}";
                }

                buttonText.text = displayText;
            }

            // 버튼 색상 업데이트 (예시)
            var buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                if (progress.isCompleted && progress.starsEarned == 3)
                {
                    buttonImage.color = Color.yellow; // 3성 완료
                }
                else if (progress.isCompleted)
                {
                    buttonImage.color = Color.green; // 완료
                }
                else if (IsStageUnlocked(stageNumber))
                {
                    buttonImage.color = Color.white; // 언락됨
                }
                else
                {
                    buttonImage.color = Color.gray; // 잠김
                }
            }

            // 버튼 클릭 이벤트 설정
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnStageButtonClicked(stageNumber));
        }

        /// <summary>
        /// 스테이지 언락 상태 확인
        /// </summary>
        private bool IsStageUnlocked(int stageNumber)
        {
            if (stageNumber == 1) return true;

            var profile = cacheManager.GetUserProfile();
            return profile != null && stageNumber <= profile.maxStageCompleted + 1;
        }

        /// <summary>
        /// 스테이지 버튼 클릭 처리
        /// </summary>
        private void OnStageButtonClicked(int stageNumber)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("아직 초기화가 완료되지 않았습니다.");
                return;
            }

            if (!IsStageUnlocked(stageNumber))
            {
                Debug.Log($"스테이지 {stageNumber}는 아직 잠겨있습니다.");
                // UI에 잠김 메시지 표시
                return;
            }

            if (enableDebugLog)
                Debug.Log($"[StageMapIntegration] 스테이지 {stageNumber} 선택됨");

            // StageDataManager에 스테이지 선택 요청
            if (StageDataManager.Instance != null)
            {
                StageDataManager.Instance.SelectStage(stageNumber);
                
                // 씬 전환 등의 추가 로직
                // SceneManager.LoadScene("SingleGameplayScene");
            }
        }

        // ========================================
        // CacheManager 이벤트 핸들러들
        // ========================================

        /// <summary>
        /// 사용자 프로필 업데이트 이벤트
        /// </summary>
        private void OnUserProfileUpdated(UserProfileData profile)
        {
            if (enableDebugLog)
                Debug.Log($"[StageMapIntegration] 사용자 프로필 업데이트: Lv.{profile.level}");

            UpdateUserProfileUI();
        }

        /// <summary>
        /// 메타데이터 로드 완료 이벤트
        /// </summary>
        private void OnMetadataLoaded()
        {
            if (enableDebugLog)
                Debug.Log("[StageMapIntegration] 메타데이터 로드 완료");

            UpdateStageButtons();
        }

        /// <summary>
        /// 진행도 데이터 업데이트 이벤트
        /// </summary>
        private void OnProgressDataUpdated(int stageNumber)
        {
            if (enableDebugLog)
                Debug.Log($"[StageMapIntegration] 스테이지 {stageNumber} 진행도 업데이트");

            // 해당 스테이지 버튼만 업데이트
            if (stageButtons != null && stageNumber > 0 && stageNumber <= stageButtons.Length)
            {
                UpdateStageButton(stageNumber, stageButtons[stageNumber - 1]);
            }

            // 사용자 프로필도 함께 업데이트
            UpdateUserProfileUI();
        }

        /// <summary>
        /// 동기화 완료 이벤트
        /// </summary>
        private void OnSyncCompleted(bool success)
        {
            if (enableDebugLog)
                Debug.Log($"[StageMapIntegration] 동기화 완료: {(success ? "성공" : "실패")}");

            if (success)
            {
                // 성공시 UI 전체 새로고침
                UpdateUserProfileUI();
                UpdateStageButtons();
            }
        }

        /// <summary>
        /// 강제 새로고침 (버튼 등에서 호출)
        /// </summary>
        public void RefreshStageMap()
        {
            if (cacheManager != null)
            {
                StartCoroutine(RefreshStageMapCoroutine());
            }
        }

        private IEnumerator RefreshStageMapCoroutine()
        {
            ShowLoadingUI("새로고침 중...");
            
            // 강제 전체 동기화
            cacheManager.ForceFullSync();
            
            // 동기화 완료까지 대기 (최대 10초)
            float timeout = 10f;
            bool syncCompleted = false;
            
            System.Action<bool> onSyncComplete = (success) => {
                syncCompleted = true;
            };
            
            cacheManager.OnSyncCompleted += onSyncComplete;
            
            while (!syncCompleted && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }
            
            cacheManager.OnSyncCompleted -= onSyncComplete;
            
            HideLoadingUI();
            
            if (enableDebugLog)
                Debug.Log("[StageMapIntegration] 새로고침 완료");
        }

        void OnDestroy()
        {
            // 이벤트 구독 해제
            if (cacheManager != null)
            {
                cacheManager.OnUserProfileUpdated -= OnUserProfileUpdated;
                cacheManager.OnMetadataLoaded -= OnMetadataLoaded;
                cacheManager.OnProgressDataUpdated -= OnProgressDataUpdated;
                cacheManager.OnSyncCompleted -= OnSyncCompleted;
            }
        }
    }
}