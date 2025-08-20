using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using App.Core;
using App.Network;
using App.Services;
using App.UI;
using Features.Single.Core;
using Shared.Models;
using Shared.UI;
using DataStageData = Shared.Models.StageData;
using ApiStageData = App.Network.HttpApiClient.ApiStageData;
using GameUserStageProgress = Features.Single.Core.UserStageProgress;
using NetworkUserStageProgress = App.Network.UserStageProgress;
using UserInfo = Shared.Models.UserInfo;
namespace Features.Single.UI.StageSelect
{
    /// <summary>
    /// 캔디크러시 사가 스타일의 메인 스테이지 선택 뷰
    /// 뱀 모양 레이아웃과 스크롤 기반 동적 로딩을 제공
    /// </summary>
    public class CandyCrushStageMapView : Shared.UI.PanelBase
    {
        [Header("스크롤 컴포넌트")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentTransform;
        [SerializeField] private RectTransform viewportTransform;

        [Header("스테이지 시스템")]
        [SerializeField] private StageFeed stageFeed;
        [SerializeField] private StageButtonPool buttonPool;

        [Header("UI 컴포넌트")]
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI totalStarsText;
        [SerializeField] private Button backButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private StageInfoModal stageInfoModal; // Inspector에서 직접 할당

        [Header("성능 설정")]
        [SerializeField] private float viewportBuffer = 200f; // 뷰포트 확장 영역
        [SerializeField] private float updateInterval = 0.1f; // 업데이트 간격
        [SerializeField] private float topPadding = 100f; // 1번 스테이지 위 여백

        // 스테이지 관리
        private Dictionary<int, StageButton> activeButtons = new Dictionary<int, StageButton>();
        private Features.Single.Core.StageProgressManager progressManager;

        // 뷰포트 관리
        private int firstVisibleStage = 1;
        private int lastVisibleStage = 1;
        private float lastUpdateTime = 0f;
        private Vector2 lastScrollPosition;

        // 상태
        private bool isInitialized = false;
        private bool hasSyncedOnce = false;

        // 🔥 추가: 중복 프로필 업데이트 방지
        private bool isProfileUpdateInProgress = false;

        protected override void Awake()
        {
            base.Awake();

            // 버튼 이벤트 연결
            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackButtonClicked);
            }

            if (refreshButton != null)
            {
                refreshButton.onClick.AddListener(RefreshStageMap);
            }

            // 스크롤 이벤트 연결
            if (scrollRect != null)
            {
                scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
            }

            // API 이벤트 구독
            SetupApiEventHandlers();
        }

        protected void OnEnable()
        {
            // 🔥 추가: Scene 상태 확인 - MainScene 비활성화 상태에서는 프로필 업데이트 스킵
            if (!IsMainSceneActiveOrCurrent())
            {
                Debug.LogWarning("[CandyCrushStageMapView] OnEnable - MainScene 비활성화 상태로 인해 프로필 업데이트 스킵");
                return;
            }

            // StageInfoModal 참조 확보 (Inspector 할당이 사라질 경우 대비)
            EnsureStageInfoModalReference();

            // 🔥 수정: 중복 방지 - 이미 진행 중이면 건너뜀
            if (!isProfileUpdateInProgress && Features.Single.Core.UserDataCache.Instance != null && Features.Single.Core.UserDataCache.Instance.IsLoggedIn())
            {
                var currentUser = Features.Single.Core.UserDataCache.Instance.GetCurrentUser();
                if (currentUser != null)
                {
                    isProfileUpdateInProgress = true;
                    StartSafe(DelayedProfileUpdate(currentUser));
                }
            }
            else
            {
            }
        }

        protected override void Start()
        {
            base.Start();

            // ScrollRect 설정 - Horizontal 스크롤 비활성화
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
            }

            InitializeStageMap();

            // Features.Single.Core.StageDataManager 이벤트 구독
            if (Features.Single.Core.StageDataManager.Instance != null)
            {
                Features.Single.Core.StageDataManager.Instance.OnStageCompleted += OnStageCompleted;
                Features.Single.Core.StageDataManager.Instance.OnStageUnlocked += OnStageUnlocked;
            }
        }

        void Update()
        {
            // 초기화되지 않았으면 스킵
            if (!isInitialized || stageFeed == null || scrollRect == null) return;

            // 스크롤 위치가 실제로 변경되었을 때만 업데이트
            Vector2 currentScrollPosition = scrollRect.normalizedPosition;
            float scrollDelta = Vector2.Distance(currentScrollPosition, lastScrollPosition);

            // 스크롤 변화량이 임계값을 넘거나 일정 시간 경과시에만 업데이트
            if (scrollDelta > 0.01f || Time.time - lastUpdateTime > updateInterval)
            {
                UpdateViewport();
                lastScrollPosition = currentScrollPosition;
                lastUpdateTime = Time.time;
            }
        }

        void OnDestroy()
        {
            // API 이벤트 정리
            CleanupApiEventHandlers();

            // Features.Single.Core.StageDataManager 이벤트 해제
            if (Features.Single.Core.StageDataManager.Instance != null)
            {
                Features.Single.Core.StageDataManager.Instance.OnStageCompleted -= OnStageCompleted;
                Features.Single.Core.StageDataManager.Instance.OnStageUnlocked -= OnStageUnlocked;
            }

            // progressManager 이벤트는 Features.Single.Core.StageDataManager 이벤트와 동일하므로 중복 해제 불필요

            if (stageFeed != null)
            {
                stageFeed.OnPathGenerated -= OnPathGenerated;
            }
        }

        /// <summary>
        /// 스테이지 맵 초기화
        /// </summary>
        private void InitializeStageMap()
        {
            if (isInitialized)
            {
                return;
            }

            // 컴포넌트 검증
            if (!ValidateComponents())
            {
                Debug.LogError("컴포넌트 검증 실패!");
                return;
            }

            // 진행도 매니저 연결
            progressManager = Features.Single.Core.StageProgressManager.Instance;

            // 스테이지 피드 연결
            if (stageFeed != null)
            {
                stageFeed.OnPathGenerated += OnPathGenerated;

                // 이미 경로가 생성되어 있다면 바로 설정
                if (stageFeed.GetPathPoints().Count > 0)
                {
                    SetupScrollContent();
                }
            }

            // 스테이지 정보 모달 찾기 또는 생성
            FindOrCreateStageInfoModal();

            isInitialized = true;
        }

        /// <summary>
        /// 필수 컴포넌트들 검증
        /// </summary>
        private bool ValidateComponents()
        {
            // 🔥 추가: Scene 상태 검증 - MainScene이 활성 상태인지 확인
            if (!IsMainSceneActiveOrCurrent())
            {
                Debug.LogWarning("[CandyCrushStageMapView] MainScene이 비활성화 상태 - 초기화 지연");
                return false;
            }

            if (scrollRect == null)
            {
                Debug.LogError("ScrollRect가 설정되지 않았습니다!");
                return false;
            }

            if (contentTransform == null)
            {
                contentTransform = scrollRect.content;
            }

            if (viewportTransform == null)
            {
                viewportTransform = scrollRect.viewport;
            }

            if (stageFeed == null)
            {
                stageFeed = GetComponentInChildren<StageFeed>();
                if (stageFeed == null)
                {
                    Debug.LogError("StageFeed 컴포넌트를 찾을 수 없습니다!");
                    return false;
                }
            }

            if (buttonPool == null)
            {
                buttonPool = StageButtonPool.Instance;
                if (buttonPool == null)
                {
                    Debug.LogError("StageButtonPool 인스턴스를 찾을 수 없습니다!");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 🔥 수정: Scene 상태 및 컴포넌트 접근 가능성 확인
        /// </summary>
        private bool IsMainSceneActiveOrCurrent()
        {
            // 현재 Scene이 MainScene인지 확인
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (currentScene.name == "MainScene")
            {
                return true;
            }

            // MainScene이 로드되어 있고 사용 가능한지 확인
            var mainScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("MainScene");
            if (!mainScene.IsValid() || !mainScene.isLoaded)
            {
                Debug.LogWarning("[CandyCrushStageMapView] MainScene이 로드되지 않음");
                return false;
            }

            // 🔥 수정: StageButtonPool 접근성 확인 - SingleGameplayScene에서도 찾아보기
            if (StageButtonPool.Instance == null)
            {
                // SingleGameplayScene에서 StageButtonPool 찾기 시도
                var singleGameplayScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("SingleGameplayScene");
                if (singleGameplayScene.IsValid() && singleGameplayScene.isLoaded)
                {
                    Debug.Log("[CandyCrushStageMapView] SingleGameplayScene에서 StageButtonPool 접근 시도");
                    // 잠시 대기 후 재확인 (Scene 로딩 완료 대기)
                    return false; // 일단 false 반환하고 재시도하도록 함
                }

                Debug.LogWarning("[CandyCrushStageMapView] StageButtonPool.Instance 접근 불가");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 스테이지 정보 모달 찾기 또는 생성
        /// </summary>
        private void FindOrCreateStageInfoModal()
        {
            if (stageInfoModal != null) return;

            // 1) 현재 오브젝트 기준으로 부모를 타고 올라가며 각 부모의 자식들에서 탐색 (비활성 포함)
            Transform cursor = transform;
            while (cursor != null)
            {
                var local = cursor.GetComponentInChildren<StageInfoModal>(true);
                if (local != null)
                {
                    stageInfoModal = local;
                    return;
                }
                cursor = cursor.parent;
            }

            // 2) 싱글톤/글로벌 백업 탐색 (비활성 포함)
            stageInfoModal = StageInfoModal.Instance;
#if UNITY_2020_1_OR_NEWER
            if (stageInfoModal == null) stageInfoModal = FindObjectOfType<StageInfoModal>(true);
#else
    if (stageInfoModal == null)
    {
        var all = Resources.FindObjectsOfTypeAll<StageInfoModal>();
        foreach (var m in all)
        {
            if (m != null && m.gameObject.hideFlags == HideFlags.None)
            {
                stageInfoModal = m;
                break;
            }
        }
    }
#endif

            if (stageInfoModal == null)
            {
                Debug.LogError("StageInfoModal을 찾을 수 없습니다. 씬에 StageInfoModal이 있는지 확인하세요.");
            }
        }


        /// <summary>
        /// 경로 생성 완료 이벤트
        /// </summary>
        private void OnPathGenerated()
        {
            SetupScrollContent();
        }

        /// <summary>
        /// 스크롤 콘텐츠 설정
        /// </summary>
        private void SetupScrollContent()
        {
            if (stageFeed == null || contentTransform == null) return;

            // 콘텐츠 크기 설정 (상단 여백 추가)
            float totalHeight = stageFeed.GetTotalHeight() + topPadding;
            float totalWidth = stageFeed.GetTotalWidth();

            // ContentTransform 앵커와 피벗을 중앙으로 설정
            contentTransform.anchorMin = new Vector2(0.5f, 1f); // 상단 중앙 기준
            contentTransform.anchorMax = new Vector2(0.5f, 1f);
            contentTransform.pivot = new Vector2(0.5f, 1f);
            contentTransform.sizeDelta = new Vector2(totalWidth, totalHeight);
            contentTransform.anchoredPosition = new Vector2(0f, 0f);

            // 한 프레임 기다린 후 뷰포트 업데이트 (Layout 시스템이 완료될 때까지)
            StartSafe(DelayedViewportUpdate());

            // UI 정보 업데이트
            UpdateUIInfo();
        }

        /// <summary>
        /// 지연된 뷰포트 업데이트
        /// </summary>
        private System.Collections.IEnumerator DelayedViewportUpdate()
        {
            yield return null; // 한 프레임 대기
            yield return null; // 안전을 위해 한 프레임 더 대기

            UpdateViewport();

            // 첫 번째 스테이지가 보이지 않으면 강제로 처음 몇 개 스테이지 표시
            if (activeButtons.Count == 0)
            {
                Debug.LogWarning("활성 버튼이 없음. 강제로 첫 20개 스테이지 표시");
                UpdateVisibleButtons(1, Mathf.Min(20, stageFeed.GetTotalStages()));
                firstVisibleStage = 1;
                lastVisibleStage = Mathf.Min(20, stageFeed.GetTotalStages());
            }

        }

        /// <summary>
        /// 스크롤 값 변경 이벤트
        /// </summary>
        private void OnScrollValueChanged(Vector2 scrollValue)
        {
            // 실시간 뷰포트 업데이트는 Update에서 처리 (성능 최적화)
        }

        /// <summary>
        /// 뷰포트 업데이트 (가시 영역의 스테이지만 활성화)
        /// </summary>
        private void UpdateViewport()
        {
            if (stageFeed == null || viewportTransform == null) return;

            // 현재 뷰포트 범위 계산
            Vector2 viewportMin, viewportMax;
            GetViewportBounds(out viewportMin, out viewportMax);

            // 버퍼 영역 추가
            viewportMin.y -= viewportBuffer;
            viewportMax.y += viewportBuffer;

            // 가시 스테이지 범위 계산
            int newFirstVisible, newLastVisible;
            CalculateVisibleStageRange(viewportMin, viewportMax, out newFirstVisible, out newLastVisible);

            // 범위가 변경되었다면 버튼 업데이트
            if (newFirstVisible != firstVisibleStage || newLastVisible != lastVisibleStage)
            {
                UpdateVisibleButtons(newFirstVisible, newLastVisible);
                firstVisibleStage = newFirstVisible;
                lastVisibleStage = newLastVisible;
            }
        }

        /// <summary>
        /// 뷰포트 경계 계산
        /// </summary>
        private void GetViewportBounds(out Vector2 min, out Vector2 max)
        {
            // 뷰포트의 월드 좌표를 콘텐츠 로컬 좌표로 변환
            Vector3[] corners = new Vector3[4];
            viewportTransform.GetWorldCorners(corners);

            Vector2 bottomLeft = contentTransform.InverseTransformPoint(corners[0]);
            Vector2 topRight = contentTransform.InverseTransformPoint(corners[2]);

            min = bottomLeft;
            max = topRight;
        }

        /// <summary>
        /// 가시 스테이지 범위 계산 (뷰포트 기반 동적 로딩)
        /// </summary>
        private void CalculateVisibleStageRange(Vector2 viewportMin, Vector2 viewportMax,
                                                out int firstVisible, out int lastVisible)
        {
            firstVisible = int.MaxValue;
            lastVisible = 0;

            int totalStages = stageFeed.GetTotalStages();

            // 모든 스테이지를 검사하여 뷰포트 내에 있는지 확인
            for (int stage = 1; stage <= totalStages; stage++)
            {
                Vector2 stagePos = stageFeed.GetStagePosition(stage);

                // 스테이지가 뷰포트 내에 있는지 확인
                if (IsPositionInViewport(stagePos, viewportMin, viewportMax))
                {
                    if (firstVisible == int.MaxValue)
                        firstVisible = stage;
                    lastVisible = stage;
                }
            }

            // 뷰포트에 아무것도 없으면 기본값 설정
            if (firstVisible == int.MaxValue)
            {
                firstVisible = 1;
                lastVisible = Mathf.Min(10, totalStages);
            }
        }

        /// <summary>
        /// 위치가 뷰포트 내에 있는지 확인
        /// </summary>
        private bool IsPositionInViewport(Vector2 position, Vector2 viewportMin, Vector2 viewportMax)
        {
            return position.x >= viewportMin.x && position.x <= viewportMax.x &&
                   position.y >= viewportMin.y && position.y <= viewportMax.y;
        }

        /// <summary>
        /// 가시 버튼들 업데이트 (활성화/비활성화 기반)
        /// </summary>
        private void UpdateVisibleButtons(int newFirstVisible, int newLastVisible)
        {
            // 모든 기존 버튼들을 순회하면서 가시성 업데이트
            foreach (var kvp in activeButtons)
            {
                int stage = kvp.Key;
                StageButton button = kvp.Value;

                if (button != null)
                {
                    bool shouldBeVisible = stage >= newFirstVisible && stage <= newLastVisible;
                    bool currentlyVisible = button.gameObject.activeSelf;

                    // 상태가 변경된 경우에만 업데이트
                    if (shouldBeVisible != currentlyVisible)
                    {
                        button.gameObject.SetActive(shouldBeVisible);
                    }
                }
            }

            // 새로운 범위에서 아직 생성되지 않은 버튼들 생성
            for (int stage = newFirstVisible; stage <= newLastVisible; stage++)
            {
                if (!activeButtons.ContainsKey(stage))
                {
                    CreateStageButton(stage);
                }
            }

            // 성능 디버그 (1초에 한번)
            if (Time.frameCount % 60 == 0)
            {
                int activeCount = 0;
                int totalCount = activeButtons.Count;

                foreach (var kvp in activeButtons)
                {
                    if (kvp.Value != null && kvp.Value.gameObject.activeSelf)
                        activeCount++;
                }

            }
        }

        /// <summary>
        /// 스테이지 버튼 생성 및 설정
        /// </summary>
        private void CreateStageButton(int stageNumber)
        {
            if (!stageFeed.IsValidStage(stageNumber)) return;

            // 풀에서 버튼 가져오기
            StageButton button = buttonPool.GetButton();
            if (button == null) return;

            // 버튼 초기화 (상단 여백 고려)
            Vector2 stagePosition = stageFeed.GetStagePosition(stageNumber);
            Vector3 adjustedPosition = new Vector3(stagePosition.x, stagePosition.y - topPadding, 0);

            button.transform.SetParent(contentTransform, false);
            button.transform.localPosition = adjustedPosition;

            // StageButton 초기화 (기존 API 사용)
            button.Initialize(stageNumber, OnStageButtonClicked);

            // 진행도 정보 적용
            UpdateButtonState(button, stageNumber);

            // 활성 버튼 목록에 추가
            activeButtons[stageNumber] = button;
        }

        /// <summary>
        /// 🔥 개선: CacheManager 기반 언락 상태 확인
        /// </summary>
        private bool GetStageUnlockedStatus(int stageNumber)
        {
            if (stageNumber <= 1)
            {
                return true; // 첫 번째 스테이지는 항상 언락
            }

            // CacheManager 우선 사용
            if (CacheManager.Instance != null)
            {
                var profile = CacheManager.Instance.GetUserProfile();
                if (profile != null)
                {
                    bool isUnlocked = stageNumber <= profile.maxStageCompleted + 1;
                    return isUnlocked;
                }
            }

            // Fallback: Features.Single.Core.UserDataCache 직접 사용
            if (Features.Single.Core.UserDataCache.Instance != null && Features.Single.Core.UserDataCache.Instance.IsLoggedIn())
            {
                int maxStageCompleted = UserDataCache.Instance.MaxStageCompleted;
                bool isUnlocked = stageNumber <= maxStageCompleted + 1;
                return isUnlocked;
            }

            return false; // 로그인되지 않은 경우 첫 스테이지만 언락
        }

        /// <summary>
        /// 버튼 상태 업데이트 (클리어된 스테이지 포함)
        /// </summary>
        private void UpdateButtonState(StageButton button, int stageNumber)
        {

            // 🔥 수정: 견고한 언락 상태 확인 사용
            bool isUnlocked = GetStageUnlockedStatus(stageNumber);

            // 🔥 수정: Features.Single.Core.UserDataCache에서 직접 데이터 가져오기 (progressManager 대신)
            NetworkUserStageProgress networkProgress = null;
            if (Features.Single.Core.UserDataCache.Instance != null)
            {
                networkProgress = Features.Single.Core.UserDataCache.Instance.GetStageProgress(stageNumber);
            }
            else
            {
                Debug.LogWarning($"[UpdateButtonState] Features.Single.Core.UserDataCache.Instance가 null입니다");
            }

            // NetworkUserStageProgress를 GameUserStageProgress로 변환 (UpdateButtonsFromCache와 동일한 로직)
            GameUserStageProgress userProgress = null;
            if (networkProgress != null)
            {
                // 🔥 수정: null 체크 후 안전하게 변환
                userProgress = new GameUserStageProgress
                {
                    stageNumber = networkProgress.stageNumber,
                    isCompleted = networkProgress.isCompleted,
                    starsEarned = networkProgress.starsEarned,
                    bestScore = networkProgress.bestScore,
                    bestCompletionTime = networkProgress.bestCompletionTime,
                    totalAttempts = networkProgress.totalAttempts,
                    successfulAttempts = networkProgress.successfulAttempts,
                    firstPlayedAt = networkProgress.firstPlayedAt,
                    lastPlayedAt = networkProgress.lastPlayedAt
                };

            }
            else
            {
                // 🔥 수정: null인 경우 기본값으로 생성
                userProgress = new GameUserStageProgress
                {
                    stageNumber = stageNumber,
                    isCompleted = false,
                    starsEarned = 0,
                    bestScore = 0,
                    bestCompletionTime = 0,
                    totalAttempts = 0,
                    successfulAttempts = 0,
                    firstPlayedAt = System.DateTime.MinValue,
                    lastPlayedAt = System.DateTime.MinValue
                };

            }

            button.UpdateState(isUnlocked, userProgress);

        }

        /// <summary>
        /// 스테이지 버튼 클릭 이벤트
        /// </summary>
        private void OnStageButtonClicked(int stageNumber)
        {

            // 🔥 수정: 견고한 언락 상태 확인 사용
            if (!GetStageUnlockedStatus(stageNumber))
            {
                ShowUnlockedRequiredMessage(stageNumber);
                return;
            }

            // 클릭 시점에 참조 재확보
            if (stageInfoModal == null)
            {
                FindOrCreateStageInfoModal();
                if (stageInfoModal == null)
                {
                    Debug.LogError("StageInfoModal Instance를 찾을 수 없습니다!");
                    return;
                }
            }

            // 스테이지 정보 모달 표시
            var stageData = GetStageData(stageNumber);
            if (stageData == null)
            {
                // API 요청을 보낸 경우 (pendingStageNumber가 설정됨) 대기 메시지 표시
                if (pendingStageNumber == stageNumber)
                {
                    // TODO: 로딩 인디케이터 표시
                    return;
                }

                // API 요청도 실패한 경우
                Debug.LogError($"스테이지 {stageNumber} 데이터를 로드할 수 없습니다!");
                return;
            }

            var progress = progressManager?.GetCachedStageProgress(stageNumber);

            // StageProgress를 UserStageProgress로 변환
            GameUserStageProgress userProgress = null;
            if (progress != null)
            {
                userProgress = new GameUserStageProgress
                {
                    stageNumber = stageNumber,
                    isCompleted = progress.isCompleted,
                    bestScore = progress.bestScore,
                    starsEarned = CalculateStarsEarned(progress.bestScore, stageData)
                };
            }
            else
            {
                // 진행도가 없는 경우 기본값
                userProgress = new GameUserStageProgress
                {
                    stageNumber = stageNumber,
                    isCompleted = false,
                    bestScore = 0,
                    starsEarned = 0
                };
            }

            stageInfoModal.ShowStageInfo(stageData, userProgress);
        }

        // 대기 중인 스테이지 번호 (API 응답 대기)
        private int pendingStageNumber = 0;

        /// <summary>
        /// API 이벤트 핸들러 설정
        /// </summary>
        private void SetupApiEventHandlers()
        {

            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.OnStageDataReceived += OnStageDataReceived;
                HttpApiClient.Instance.OnStageProgressReceived += OnStageProgressReceived;

                // ✅ 추가: 메타데이터 수신에도 반응
                HttpApiClient.Instance.OnStageMetadataReceived += OnStageMetadataReceived;

            }
            else
            {
                Debug.LogWarning("[CandyCrushStageMapView] HttpApiClient 인스턴스 없음 - 1초 후 재시도");
                InvokeLater(1f, SetupApiEventHandlers);
            }

            // Features.Single.Core.UserDataCache 이벤트 구독(기존 유지)
            Debug.Log($"[CandyCrushStageMapView] Features.Single.Core.UserDataCache.Instance null 여부: {Features.Single.Core.UserDataCache.Instance == null}");
            if (Features.Single.Core.UserDataCache.Instance != null)
            {
                Features.Single.Core.UserDataCache.Instance.OnUserDataUpdated += OnUserDataUpdated;
                Debug.Log("[CandyCrushStageMapView] Features.Single.Core.UserDataCache 이벤트 핸들러 설정 완료");
                Debug.Log("[CandyCrushStageMapView] 프로필 데이터 즉시 업데이트는 OnEnable에서 처리");
            }
        }


        /// <summary>
        /// API 이벤트 핸들러 정리
        /// </summary>
        private void CleanupApiEventHandlers()
        {
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.OnStageDataReceived -= OnStageDataReceived;
                HttpApiClient.Instance.OnStageProgressReceived -= OnStageProgressReceived;

                // ✅ 추가: 해제도 함께
                HttpApiClient.Instance.OnStageMetadataReceived -= OnStageMetadataReceived;
            }

            if (Features.Single.Core.UserDataCache.Instance != null)
            {
                Features.Single.Core.UserDataCache.Instance.OnUserDataUpdated -= OnUserDataUpdated;
            }
        }

        private void OnStageMetadataReceived(HttpApiClient.CompactStageMetadata[] metadata)
        {
            if (pendingStageNumber <= 0) return; // 대기 중인 스테이지 없음

            // 캐시에 방금 들어온 메타데이터가 있는지 확인
            var cached = Features.Single.Core.UserDataCache.Instance?.GetStageMetadata(pendingStageNumber);
            if (cached.n == pendingStageNumber)
            {
                Debug.Log($"[CandyCrushStageMapView] 메타데이터 수신 확인 - 대기 중이던 스테이지 {pendingStageNumber} 모달 표시");
                // 개별 데이터 수신이 없어도, 메타데이터만으로 StageData 구성 가능 → 바로 모달 표시
                ShowStageModalDirectly(pendingStageNumber);
                pendingStageNumber = 0; // 대기 해제
            }
            else
            {
                Debug.Log($"[CandyCrushStageMapView] 메타데이터 수신됨 but 대기 스테이지({pendingStageNumber})는 아직 없음");
            }
        }

        /// <summary>
        /// API에서 스테이지 데이터 수신 처리
        /// </summary>
        private void OnStageDataReceived(ApiStageData stageData)
        {
            if (stageData == null) return;

            Debug.Log($"[CandyCrushStageMapView] API에서 스테이지 {stageData.stage_number} 데이터 수신");

            // Features.Single.Core.StageDataManager 캐시에 저장
            if (Features.Single.Core.StageDataManager.Instance?.GetStageManager() != null)
            {
                // API.StageData를 Data.StageData로 변환
                var convertedStageData = ConvertApiToDataStageData(stageData);
                Features.Single.Core.StageDataManager.Instance.GetStageManager().CacheStageData(convertedStageData);

                // 모달이 이 스테이지를 기다리고 있었다면 표시
                ShowStageInfoModalIfReady(stageData.stage_number);
            }
        }

        /// <summary>
        /// API에서 스테이지 진행도 수신 처리
        /// </summary>
        private void OnStageProgressReceived(App.Network.UserStageProgress progress)
        {
            if (progress == null) return;

            Debug.Log($"[CandyCrushStageMapView] API에서 스테이지 {progress.stageNumber} 진행도 수신");

            // Features.Single.Core.UserDataCache에 저장되므로 별도 처리 불필요
        }

        /// <summary>
        /// 🔥 추가: 사용자 데이터 업데이트 처리 (프로필 로드 후 UI 새로고침)
        /// </summary>
        private void OnUserDataUpdated(UserInfo userInfo)
        {
            if (userInfo == null) return;

            // 🔥 추가: 중복 방지 - 이미 진행 중이면 건너뜀
            if (isProfileUpdateInProgress)
            {
                Debug.Log($"[CandyCrushStageMapView] OnUserDataUpdated 중복 방지 - 이미 프로필 업데이트 진행 중");
                return;
            }

            Debug.Log($"[CandyCrushStageMapView] 사용자 데이터 업데이트됨: {userInfo.username}, maxStageCompleted={userInfo.maxStageCompleted}");
            Debug.Log($"[CandyCrushStageMapView] 프로필 로드 완료 - 모든 스테이지 버튼 상태 새로고침 시작");

            // 🔥 추가: progressManager와 Features.Single.Core.UserDataCache 동기화
            if (progressManager != null && userInfo.maxStageCompleted > 0)
            {
                Debug.Log($"[CandyCrushStageMapView] progressManager 동기화: max_stage_completed={userInfo.maxStageCompleted}");
                // progressManager의 최대 언락 스테이지를 Features.Single.Core.UserDataCache 데이터와 동기화
                for (int stage = 1; stage <= userInfo.maxStageCompleted + 1; stage++)
                {
                    if (!progressManager.IsStageUnlocked(stage))
                    {
                        // 필요시 progressManager 업데이트 로직 추가
                        Debug.Log($"[CandyCrushStageMapView] 스테이지 {stage} progressManager 동기화 필요");
                    }
                }
            }

            // 🔥 핵심: 프로필 로드 후 모든 활성 스테이지 버튼의 상태를 새로고침
            RefreshAllStageButtons();

            // 진행도 텍스트 업데이트  
            UpdateUIInfo();
        }

        /// <summary>
        /// 🔥 추가: 모든 활성 스테이지 버튼 상태 새로고침
        /// </summary>
        private void RefreshAllStageButtons()
        {
            Debug.Log($"[CandyCrushStageMapView] RefreshAllStageButtons 시작 - 활성 버튼 수: {activeButtons.Count}");

            // 현재 활성화된 모든 스테이지 버튼의 상태를 새로고침
            foreach (var kvp in activeButtons)
            {
                int stageNumber = kvp.Key;
                StageButton stageButton = kvp.Value;

                Debug.Log($"[CandyCrushStageMapView] 스테이지 {stageNumber} 버튼 상태 새로고침 중...");
                UpdateButtonState(stageButton, stageNumber);
            }

            Debug.Log($"[CandyCrushStageMapView] RefreshAllStageButtons 완료");
        }

        /// <summary>
        /// 🔥 추가: 지연된 프로필 업데이트 (초기화 완료 대기)
        /// </summary>
        private System.Collections.IEnumerator DelayedProfileUpdate(UserInfo userInfo)
        {
            // 스테이지 맵 초기화 완료까지 대기
            Debug.Log($"[CandyCrushStageMapView] DelayedProfileUpdate 시작: {userInfo.username} - 초기화 완료 대기 중...");

            float waitTime = 0f;
            const float maxWaitTime = 3f; // 최대 3초 대기
            const float checkInterval = 0.1f;

            // 스테이지 맵이 초기화되고 활성 버튼이 생성될 때까지 대기
            while (!isInitialized || activeButtons.Count == 0)
            {
                yield return new WaitForSeconds(checkInterval);
                waitTime += checkInterval;

                if (waitTime >= maxWaitTime)
                {
                    Debug.LogWarning($"[CandyCrushStageMapView] DelayedProfileUpdate 타임아웃 - 강제 실행 (waitTime={waitTime:F1}s, isInitialized={isInitialized}, activeButtons={activeButtons.Count})");
                    break;
                }
            }

            Debug.Log($"[CandyCrushStageMapView] DelayedProfileUpdate 실행: {userInfo.username} (대기시간={waitTime:F1}s, 활성버튼={activeButtons.Count}개)");

            // 🔥 수정: 직접 RefreshAllStageButtons 호출 (OnUserDataUpdated 대신)
            RefreshAllStageButtons();
            UpdateUIInfo();

            // 🔥 추가: 완료 후 플래그 초기화
            isProfileUpdateInProgress = false;
        }

        /// <summary>
        /// API.StageData를 Data.StageData로 변환
        /// </summary>
        private DataStageData ConvertApiToDataStageData(ApiStageData apiStageData)
        {
            return new DataStageData
            {
                stage_number = apiStageData.stage_number,
                difficulty = apiStageData.difficulty,
                optimal_score = apiStageData.optimal_score,
                time_limit = apiStageData.time_limit ?? 0,
                max_undo_count = apiStageData.max_undo_count,
                available_blocks = apiStageData.available_blocks ?? new int[0],
                hints = (apiStageData.hints != null && apiStageData.hints.Length > 0) ? string.Join("|", apiStageData.hints) : "",
                // initial_board_state, special_rules는 필요시 추가 변환
                initial_board_state = null,
                stage_description = apiStageData.stage_description,
                thumbnail_url = apiStageData.thumbnail_url
            };
        }

        /// <summary>
        /// 스테이지 데이터가 준비되면 모달 표시
        /// </summary>
        private void ShowStageInfoModalIfReady(int stageNumber)
        {
            if (pendingStageNumber == stageNumber)
            {
                pendingStageNumber = 0; // 대기 해제

                Debug.Log($"[CandyCrushStageMapView] API 응답 받음. 모달 표시 재시도: 스테이지 {stageNumber}");

                // 직접 모달 표시 (OnStageButtonClicked 재호출 대신)
                ShowStageModalDirectly(stageNumber);
            }
        }

        /// <summary>
        /// 스테이지 모달 직접 표시 (API 응답 후)
        /// </summary>
        private void ShowStageModalDirectly(int stageNumber)
        {
            // 🔥 수정: 견고한 언락 상태 확인 사용
            if (!GetStageUnlockedStatus(stageNumber))
            {
                ShowUnlockedRequiredMessage(stageNumber);
                return;
            }

            // StageInfoModal 재확인
            if (stageInfoModal == null)
            {
                stageInfoModal = StageInfoModal.Instance;

                if (stageInfoModal == null)
                {
                    Debug.LogError("StageInfoModal Instance를 찾을 수 없습니다!");
                    return;
                }
            }

            // 스테이지 데이터 가져오기 (이제 캐시에 있어야 함)
            var stageData = GetStageData(stageNumber);
            if (stageData == null)
            {
                Debug.LogError($"스테이지 {stageNumber} 데이터를 캐시에서 찾을 수 없습니다!");
                return;
            }

            var progress = progressManager?.GetCachedStageProgress(stageNumber);

            // StageProgress를 UserStageProgress로 변환
            GameUserStageProgress userProgress = null;
            if (progress != null)
            {
                userProgress = new GameUserStageProgress
                {
                    stageNumber = stageNumber,
                    isCompleted = progress.isCompleted,
                    bestScore = progress.bestScore,
                    starsEarned = CalculateStarsEarned(progress.bestScore, stageData)
                };
            }
            else
            {
                // 진행도가 없는 경우 기본값
                userProgress = new GameUserStageProgress
                {
                    stageNumber = stageNumber,
                    isCompleted = false,
                    bestScore = 0,
                    starsEarned = 0
                };
            }

            Debug.Log($"[CandyCrushStageMapView] 스테이지 {stageNumber} 모달 표시 시도");
            stageInfoModal.ShowStageInfo(stageData, userProgress);
        }

        /// <summary>
        /// 스테이지 데이터 가져오기 (캐싱된 메타데이터 우선 사용)
        /// </summary>
        private DataStageData GetStageData(int stageNumber)
        {
            Debug.Log($"[CandyCrushStageMapView] 스테이지 {stageNumber} 데이터 요청");

            // 1. 먼저 Features.Single.Core.UserDataCache의 캐싱된 메타데이터에서 확인
            if (Features.Single.Core.UserDataCache.Instance != null)
            {
                Debug.Log($"[CandyCrushStageMapView] Features.Single.Core.UserDataCache 확인 중...");

                // 전체 메타데이터 캐시 상태 확인
                var allMetadata = Features.Single.Core.UserDataCache.Instance.GetStageMetadata();
                Debug.Log($"[CandyCrushStageMapView] 전체 메타데이터 캐시: {allMetadata?.Length ?? 0}개");

                var metadata = Features.Single.Core.UserDataCache.Instance.GetStageMetadata(stageNumber);
                if (metadata != null)
                {
                    Debug.Log($"[CandyCrushStageMapView] ✅ 캐싱된 메타데이터에서 스테이지 {stageNumber} 로드");
                    return ApiDataConverter.ConvertCompactMetadata(metadata);
                }
                else
                {
                    Debug.Log($"[CandyCrushStageMapView] ❌ 스테이지 {stageNumber} 메타데이터가 캐시에 없음");

                    // 메타데이터가 아직 로드 중일 수 있으므로 짧은 지연 후 한 번 더 시도
                    if (allMetadata == null || allMetadata.Length == 0)
                    {
                        Debug.Log($"[CandyCrushStageMapView] 메타데이터가 전혀 없음. 0.5초 후 재시도");
                        pendingStageNumber = stageNumber; // 재시도할 스테이지 번호 저장
                        retryCount = 0; // 🔥 초기화
                        InvokeLater(0.5f, RetryStageDataLoad);
                        // 임시로 null 반환하여 로딩 인디케이터 표시
                        return null;
                    }
                }
            }
            else
            {
                Debug.LogWarning("[CandyCrushStageMapView] Features.Single.Core.UserDataCache.Instance가 null입니다");
            }

            // 2. Features.Single.Core.StageDataManager 캐시에서 확인
            if (Features.Single.Core.StageDataManager.Instance != null)
            {
                var stageManager = Features.Single.Core.StageDataManager.Instance.GetStageManager();
                if (stageManager != null)
                {
                    var cachedData = stageManager.GetStageData(stageNumber);
                    if (cachedData != null)
                    {
                        Debug.Log($"[CandyCrushStageMapView] Features.Single.Core.StageDataManager 캐시에서 스테이지 {stageNumber} 로드");
                        return cachedData;
                    }
                }
            }

            // 3. 캐시에 없으면 API에서 요청 (백업 방식)
            if (HttpApiClient.Instance != null)
            {
                if (!HttpApiClient.Instance.IsAuthenticated())
                {
                    Debug.LogWarning($"API 인증이 되어있지 않습니다. 스테이지 {stageNumber} 데이터를 요청할 수 없습니다.");
                    return null;
                }

                Debug.Log($"[CandyCrushStageMapView] 스테이지 {stageNumber} API 요청 (백업)");
                HttpApiClient.Instance.GetStageData(stageNumber);

                // 비동기 요청이므로 현재는 null 반환하고 대기 상태로 설정
                pendingStageNumber = stageNumber;
                retryCount = 0; // 🔥 초기화
                return null;
            }

            // 4. API 클라이언트도 없으면 오류
            Debug.LogError($"HttpApiClient를 찾을 수 없습니다. 스테이지 {stageNumber} 데이터를 로드할 수 없습니다.");
            return null;
        }


        /// <summary>
        /// 스테이지 시작
        /// </summary>
        public void StartStage(int stageNumber)
        {
            Debug.Log($"스테이지 {stageNumber} 시작!");
            UIManager.Instance?.OnStageSelected(stageNumber);
        }

        /// <summary>
        /// 뒤로가기 버튼 클릭 처리
        /// </summary>
        private void OnBackButtonClicked()
        {
            Debug.Log("[CandyCrushStageMapView] 뒤로가기 버튼 클릭");
            
            // UIManager를 통해 SingleGameplayScene 언로드 후 ModeSelection으로 이동
            var uiManager = UIManager.GetInstanceSafe();
            if (uiManager != null)
            {
                Debug.Log("[CandyCrushStageMapView] UIManager로 SingleGameplayScene 종료 요청");
                uiManager.OnExitSingleToModeSelection();
            }
            else
            {
                Debug.LogError("[CandyCrushStageMapView] UIManager를 찾을 수 없습니다!");
            }
        }

        // 🔥 추가: 재시도 횟수 제한 및 무한 루프 방지
        private int retryCount = 0;
        private const int MAX_RETRY_COUNT = 3;

        /// <summary>
        /// 메타데이터 재시도 로직 (무한 루프 방지)
        /// </summary>
        private void RetryStageDataLoad()
        {
            if (pendingStageNumber > 0)
            {
                retryCount++;
                Debug.Log($"[CandyCrushStageMapView] 스테이지 {pendingStageNumber} 메타데이터 재시도 ({retryCount}/{MAX_RETRY_COUNT})");

                if (retryCount >= MAX_RETRY_COUNT)
                {
                    Debug.LogWarning($"[CandyCrushStageMapView] 스테이지 {pendingStageNumber} 메타데이터 로드 실패 - 최대 재시도 횟수 초과");
                }

                // 메타데이터는 CacheManager에서 자동으로 관리됩니다
            }
        }

        /// <summary>
        /// 별점 계산 (점수 기반)
        /// </summary>
        private int CalculateStarsEarned(int score, DataStageData stageData)
        {
            if (score <= 0 || stageData == null) return 0;

            if (score >= stageData.threeStar) return 3;
            if (score >= stageData.twoStar) return 2;
            if (score >= stageData.oneStar) return 1;

            return 0;
        }

        /// <summary>
        /// 언락 필요 메시지 표시
        /// </summary>
        private void ShowUnlockedRequiredMessage(int stageNumber)
        {
            // TODO: 토스트 메시지나 간단한 알림 표시
            Debug.LogWarning($"스테이지 {stageNumber - 1}을 먼저 클리어하세요!");
        }

        /// <summary>
        /// StageInfoModal 참조를 확실하게 확보하는 함수
        /// Inspector 할당이 사라져도 동적으로 찾아서 연결
        /// </summary>
        private void EnsureStageInfoModalReference()
        {
            if (stageInfoModal != null)
            {
                Debug.Log("StageInfoModal 참조가 이미 있습니다.");
                return;
            }

            Debug.Log("StageInfoModal 참조를 동적으로 찾는 중...");

            // 1단계: 자식 오브젝트에서 찾기 (가장 일반적)
            stageInfoModal = GetComponentInChildren<StageInfoModal>(true);
            if (stageInfoModal != null)
            {
                Debug.Log($"✅ 자식에서 StageInfoModal 찾음: {stageInfoModal.name}");
                return;
            }

            // 2단계: 부모/형제 오브젝트에서 찾기 (StageSelectPanel 내 다른 위치)
            if (transform.parent != null)
            {
                stageInfoModal = transform.parent.GetComponentInChildren<StageInfoModal>(true);
                if (stageInfoModal != null)
                {
                    Debug.Log($"✅ 부모/형제에서 StageInfoModal 찾음: {stageInfoModal.name}");
                    return;
                }
            }

            // 3단계: 씬 전체에서 찾기 (비활성 오브젝트 포함)
            stageInfoModal = FindObjectOfType<StageInfoModal>(true);
            if (stageInfoModal != null)
            {
                Debug.Log($"✅ 씬 전체에서 StageInfoModal 찾음: {stageInfoModal.name}");
                return;
            }

            // 4단계: 싱글톤 인스턴스에서 찾기
            stageInfoModal = StageInfoModal.Instance;
            if (stageInfoModal != null)
            {
                Debug.Log($"✅ 싱글톤에서 StageInfoModal 찾음: {stageInfoModal.name}");
                return;
            }

            Debug.LogError("❌ StageInfoModal을 어디에서도 찾을 수 없습니다!");
            Debug.LogError("StageSelectPanel 하위에 StageInfoModal GameObject를 추가해주세요.");
        }

        // ========================================
        // 이벤트 핸들러들
        // ========================================

        /// <summary>
        /// 스테이지 완료 이벤트
        /// </summary>
        private void OnStageCompleted(int stageNumber, int score, int stars)
        {
            Debug.Log($"스테이지 {stageNumber} 완료: {score}점, {stars}별");

            // 해당 버튼 업데이트
            if (activeButtons.ContainsKey(stageNumber))
            {
                UpdateButtonState(activeButtons[stageNumber], stageNumber);

                // StageButton은 자체적으로 클릭 애니메이션을 처리하므로 별도 애니메이션 불필요
            }

            // UI 정보 업데이트
            UpdateUIInfo();
        }

        /// <summary>
        /// 스테이지 언락 이벤트
        /// </summary>
        private void OnStageUnlocked(int unlockedStageNumber)
        {
            Debug.Log($"스테이지 {unlockedStageNumber} 언락!");

            // 해당 버튼 업데이트
            if (activeButtons.ContainsKey(unlockedStageNumber))
            {
                UpdateButtonState(activeButtons[unlockedStageNumber], unlockedStageNumber);
                // StageButton은 자체적으로 상태 변화 애니메이션을 처리
            }

            // 새로 언락된 스테이지로 스크롤
            ScrollToStage(unlockedStageNumber);

            // UI 정보 업데이트
            UpdateUIInfo();
        }

        // ========================================
        // UI 정보 업데이트
        // ========================================

        /// <summary>
        /// UI 진행도 정보 업데이트
        /// </summary>
        private void UpdateUIInfo()
        {
            if (progressManager == null) return;

            int totalStages = stageFeed.GetTotalStages();

            // 진행률 텍스트
            if (progressText != null)
            {
                float progress = progressManager.GetOverallProgress(totalStages);
                int maxUnlocked = progressManager.GetMaxUnlockedStage();
                progressText.text = $"진행률: {progress:F1}% ({maxUnlocked}/{totalStages})";
            }

            // 총 별 개수
            if (totalStarsText != null)
            {
                int earnedStars = progressManager.GetTotalStarsEarned();
                int maxStars = totalStages * 3;
                totalStarsText.text = $"별: {earnedStars}/{maxStars} ★";
            }
        }

        /// <summary>
        /// 특정 스테이지로 스크롤
        /// </summary>
        public void ScrollToStage(int stageNumber)
        {
            if (!stageFeed.IsValidStage(stageNumber) || scrollRect == null) return;

            Vector2 stagePosition = stageFeed.GetStagePosition(stageNumber);
            float totalHeight = stageFeed.GetTotalHeight();

            // 정규화된 스크롤 위치 계산 (0-1)
            float normalizedY = 1f - (Mathf.Abs(stagePosition.y) / totalHeight);
            normalizedY = Mathf.Clamp01(normalizedY);

            // 부드러운 스크롤 애니메이션
            StartSafe(SmoothScrollTo(new Vector2(scrollRect.normalizedPosition.x, normalizedY)));
        }

        /// <summary>
        /// 부드러운 스크롤 애니메이션
        /// </summary>
        private System.Collections.IEnumerator SmoothScrollTo(Vector2 targetPosition)
        {
            Vector2 startPosition = scrollRect.normalizedPosition;
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                // Ease out 커브 적용
                progress = 1f - Mathf.Pow(1f - progress, 3f);

                scrollRect.normalizedPosition = Vector2.Lerp(startPosition, targetPosition, progress);
                yield return null;
            }

            scrollRect.normalizedPosition = targetPosition;
        }

        // ========================================
        // Public API
        // ========================================

        /// <summary>
        /// 스테이지 맵 새로고침
        /// </summary>
        public void RefreshStageMap()
        {
            Debug.Log("스테이지 맵 새로고침");

            // 초기 표시용으로만 한 번 캐시 업데이트 실행
            if (!hasSyncedOnce && Features.Single.Core.UserDataCache.Instance != null)
            {
                Debug.Log("[RefreshStageMap] 캐시된 데이터로 UI 업데이트 (서버 요청 생략)");
                StartSafe(UpdateButtonsFromCache());
                hasSyncedOnce = true;
            }
            else
            {
                Debug.Log("[RefreshStageMap] 초기 동기화는 이미 수행됨 - 중복 호출 생략");
            }

            // 현재 활성 버튼들 상태 안전 재확인(필요시)
            foreach (var kvp in activeButtons)
            {
                int stageNumber = kvp.Key;
                StageButton button = kvp.Value;
                if (button != null)
                {
                    // 이 부분을 유지할지/뺄지는 팀 정책에 따라 선택.
                    // 잦은 중복이 문제면 주석 처리 가능.
                    // UpdateButtonState(button, stageNumber);
                }
            }

            // 추가적인 UI 정보 업데이트(텍스트 등)는 유지
            UpdateUIInfo();
        }

        /// <summary>
        /// PanelBase 오버라이드
        /// </summary>
        public override void Show()
        {
            Debug.Log("=== CandyCrushStageMapView Show 호출 ===");

            // 부모 Show 호출 (PanelBase가 gameObject.SetActive(true) 처리)
            base.Show();

            // 🔥 추가: Scene 상태 확인 후 초기화
            if (IsMainSceneActiveOrCurrent())
            {
                // 표시될 때마다 초기화 및 새로고침
                InitializeStageMap();
                RefreshStageMap();

                Debug.Log($"Panel 최종 상태: Active={gameObject.activeInHierarchy}");

                // 초기화 완료 후 스크롤 위치 조정
                StartSafe(DelayedScrollToStage());
            }
            else
            {
                Debug.LogWarning("[CandyCrushStageMapView] Show - MainScene 비활성화 상태로 인해 초기화 지연. 재시도 예약");
                // 0.5초 후 재시도
                StartSafe(RetryShowAfterDelay());
            }
        }

        /// <summary>
        /// 🔥 추가: Scene 상태 확인 후 Show 재시도
        /// </summary>
        private System.Collections.IEnumerator RetryShowAfterDelay()
        {
            yield return new WaitForSeconds(0.5f);

            if (IsMainSceneActiveOrCurrent() && gameObject.activeInHierarchy)
            {
                Debug.Log("[CandyCrushStageMapView] 재시도 성공 - 초기화 진행");
                InitializeStageMap();
                RefreshStageMap();
                StartSafe(DelayedScrollToStage());
            }
            else
            {
                Debug.LogWarning("[CandyCrushStageMapView] 재시도 실패 - Scene 여전히 비활성화 상태");
            }
        }

        /// <summary>
        /// 지연된 스크롤 위치 조정 (도전할 스테이지 중앙 배치)
        /// </summary>
        private System.Collections.IEnumerator DelayedScrollToStage()
        {
            // Layout 시스템과 뷰포트 업데이트 완료 대기
            yield return new WaitForSeconds(0.1f);

            // 🔥 수정: Features.Single.Core.UserDataCache 기반으로 도전해야 할 스테이지 계산
            int challengeStage = 1;
            if (Features.Single.Core.UserDataCache.Instance != null && Features.Single.Core.UserDataCache.Instance.IsLoggedIn())
            {
                // Features.Single.Core.UserDataCache에서 직접 max_stage_completed 사용
                int maxStageCompleted = UserDataCache.Instance.MaxStageCompleted;
                int maxUnlocked = maxStageCompleted + 1; // 다음 스테이지까지 언락됨

                // 현재 언락된 가장 높은 스테이지가 도전 스테이지
                challengeStage = maxUnlocked;

                // 스테이지 1은 항상 도전 가능하므로 최소값 보장
                challengeStage = Mathf.Max(1, challengeStage);

                Debug.Log($"[CandyCrushStageMapView] Features.Single.Core.UserDataCache 기반: max_stage_completed={maxStageCompleted}, 최대 언락 스테이지={maxUnlocked}, 도전 스테이지={challengeStage}");
            }
            else if (progressManager != null)
            {
                // fallback: progressManager 사용
                int maxUnlocked = progressManager.GetMaxUnlockedStage();
                challengeStage = maxUnlocked;
                challengeStage = Mathf.Max(1, challengeStage);
                Debug.Log($"[CandyCrushStageMapView] progressManager fallback: 최대 언락 스테이지={maxUnlocked}, 도전 스테이지={challengeStage}");
            }

            Debug.Log($"도전 스테이지 {challengeStage}를 중앙으로 스크롤 시도");

            Debug.Log($"스크롤 조정 시작: 도전 스테이지 = {challengeStage}");

            // 항상 맨 위로 스크롤 (1번 스테이지를 확실히 보이도록)
            if (scrollRect != null)
            {
                Vector2 targetPosition = new Vector2(0.5f, 1f); // 맨 위 
                scrollRect.normalizedPosition = targetPosition;
                Debug.Log($"스크롤 위치를 맨 위로 설정: {targetPosition}");

                // 스크롤 후 뷰포트 정보 로그
                yield return new WaitForSeconds(0.1f);
                LogScrollRectInfo();
            }
        }

        /// <summary>
        /// 스크롤 상태 디버그 로그 출력
        /// </summary>
        private void LogScrollRectInfo()
        {
            if (scrollRect == null || contentTransform == null || viewportTransform == null) return;

            Debug.Log("=== ScrollRect 상태 정보 ===");
            Debug.Log($"ScrollRect normalizedPosition: {scrollRect.normalizedPosition}");
            Debug.Log($"Content sizeDelta: {contentTransform.sizeDelta}");
            Debug.Log($"Content anchoredPosition: {contentTransform.anchoredPosition}");
            Debug.Log($"Viewport sizeDelta: {viewportTransform.sizeDelta}");

            // 현재 뷰포트 범위 계산
            Vector2 viewportMin, viewportMax;
            GetViewportBounds(out viewportMin, out viewportMax);
            // Debug.Log($"뷰포트 범위: Min({viewportMin.x:F1}, {viewportMin.y:F1}) ~ Max({viewportMax.x:F1}, {viewportMax.y:F1})");

            // 1번 스테이지 위치 확인
            if (stageFeed != null)
            {
                Vector2 stage1Pos = stageFeed.GetStagePosition(1);
                Debug.Log($"1번 스테이지 위치: ({stage1Pos.x:F1}, {stage1Pos.y:F1})");

                bool stage1InViewport = IsPositionInViewport(stage1Pos, viewportMin, viewportMax);
                Debug.Log($"1번 스테이지 뷰포트 내 여부: {stage1InViewport}");
            }
        }

        /// <summary>
        /// 캐시된 데이터로 버튼 상태 업데이트 (서버 요청 없음)
        /// </summary>
        private System.Collections.IEnumerator UpdateButtonsFromCache()
        {
            Debug.Log("[UpdateButtonsFromCache] 캐시된 데이터로 UI 업데이트 시작");

            if (Features.Single.Core.UserDataCache.Instance == null)
            {
                Debug.LogWarning("[UpdateButtonsFromCache] Features.Single.Core.UserDataCache가 없음");
                yield break;
            }

            int updatedCount = 0;

            // 현재 활성 버튼들 업데이트
            foreach (var kvp in activeButtons)
            {
                int stageNumber = kvp.Key;
                StageButton button = kvp.Value;

                if (button != null)
                {
                    Debug.Log($"[UpdateButtonsFromCache] 스테이지 {stageNumber} 처리 시작");

                    // 캐시에서 진행도 데이터 가져오기
                    var networkProgress = Features.Single.Core.UserDataCache.Instance.GetStageProgress(stageNumber);

                    GameUserStageProgress gameProgress = null;

                    if (networkProgress != null)
                    {
                        // 🔥 수정: null 체크 후 안전하게 변환
                        gameProgress = new GameUserStageProgress
                        {
                            stageNumber = networkProgress.stageNumber,
                            isCompleted = networkProgress.isCompleted,
                            starsEarned = networkProgress.starsEarned,
                            bestScore = networkProgress.bestScore,
                            totalAttempts = networkProgress.totalAttempts,
                            successfulAttempts = networkProgress.successfulAttempts
                        };

                        Debug.Log($"[UpdateButtonsFromCache] 스테이지 {stageNumber} 캐시 데이터 변환: 완료={gameProgress.isCompleted}, 별={gameProgress.starsEarned}");
                        updatedCount++;
                    }
                    else
                    {
                        Debug.Log($"[UpdateButtonsFromCache] 스테이지 {stageNumber} 캐시 데이터 없음 - 기본값 사용");

                        // 🔥 수정: null인 경우 기본값으로 생성
                        gameProgress = new GameUserStageProgress
                        {
                            stageNumber = stageNumber,
                            isCompleted = false,
                            starsEarned = 0,
                            bestScore = 0,
                            totalAttempts = 0,
                            successfulAttempts = 0
                        };
                    }

                    // 🔥 수정: 견고한 언락 상태 확인 사용
                    bool isUnlocked = GetStageUnlockedStatus(stageNumber);

                    // 버튼 상태 업데이트
                    button.UpdateState(isUnlocked, gameProgress);

                    Debug.Log($"[UpdateButtonsFromCache] 스테이지 {stageNumber}: 언락={isUnlocked}, 완료={gameProgress.isCompleted}, 별={gameProgress.starsEarned}");
                }

                // 프레임 분할을 위한 yield
                if (stageNumber % 10 == 0)
                {
                    yield return null;
                }
            }

            Debug.Log($"[UpdateButtonsFromCache] ✅ 캐시된 데이터로 UI 업데이트 완료 - {updatedCount}개 스테이지 데이터 적용됨");
        }

        public override void Hide()
        {
            base.Hide();

            // 모든 버튼을 비활성화 (풀에 반환하지 않고 재사용 준비)
            foreach (var kvp in activeButtons)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.gameObject.SetActive(false);
                }
            }
            // activeButtons는 유지 (다시 Show할 때 재사용)
        }

        private Coroutine StartSafe(System.Collections.IEnumerator routine)
        {
            return isActiveAndEnabled ? StartCoroutine(routine) : CoroutineRunner.Run(routine);
        }

        private void InvokeLater(float seconds, System.Action action)
        {
            System.Collections.IEnumerator Wrapper()
            {
                yield return new WaitForSeconds(seconds);
                if (this != null) action?.Invoke();
            }
            StartSafe(Wrapper());
        }
    }
}