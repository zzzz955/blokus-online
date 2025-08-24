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
        
        // 🔥 추가: StageFeed 초기화 완료 플래그
        private bool stageFeedInitialized = false;

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
        
        // 🔥 추가: 중복 버튼 리프레시 방지
        private bool isButtonRefreshInProgress = false;
        private float lastButtonRefreshTime = 0f;
        private const float BUTTON_REFRESH_THROTTLE = 0.2f; // 0.2초로 단축

        // 🔥 추가: 중복 UI 업데이트 방지
        private bool isUIUpdateInProgress = false;
        private float lastUIUpdateTime = 0f;
        private const float UI_UPDATE_THROTTLE = 0.1f; // 0.1초로 단축
        
        // 🔥 신규: 비동기 업데이트 큐 및 상태 관리
        private readonly Queue<System.Action> updateQueue = new Queue<System.Action>();
        private bool isProcessingQueue = false;

        // 🔥 추가: 총 스테이지 수 캐싱 및 로그 스팸 방지
        private int cachedTotalStages = -1;
        private float lastTotalStagesLogTime = 0f;
        private const float TOTAL_STAGES_LOG_THROTTLE = 5f; // 5초마다 로그

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

            // 🔥 추가: 데이터 로딩 상태를 먼저 확인하여 StageFeed 초기화
            CheckAndInitializeStageFeed();

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

            // 🔥 추가: GameObject 활성화 상태 검증
            if (!this.gameObject.activeSelf) return;

            // 🔥 추가: 메타데이터 로딩 상태 검증
            if (Features.Single.Core.UserDataCache.Instance == null || 
                Features.Single.Core.UserDataCache.Instance.GetStageMetadata() == null)
            {
                return; // 메타데이터가 로딩되지 않은 상태에서는 Update 스킵
            }

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

            // 🔥 안전장치: StageFeed의 totalStages 값 검증 후 Content Height 계산
            int safeTotalStages = stageFeed.GetTotalStages(); // 안전장치가 적용된 값
            
            // 🔥 추가: 데이터 로딩 실패시 ScrollContent 설정 건너뛰기
            if (safeTotalStages == 0)
            {
                Debug.LogError("[CandyCrushStageMapView] 스테이지 데이터 없음 - ScrollContent 설정 건너뛰기");
                return;
            }
            
            Debug.Log($"[CandyCrushStageMapView] SetupScrollContent - safeTotalStages: {safeTotalStages}개");
            
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
                int totalStages = stageFeed.GetTotalStages();
                if (totalStages == 0)
                {
                    Debug.LogError("활성 버튼 없음 + 스테이지 데이터 없음 - 초기화 건너뛰기");
                    yield break;
                }
                
                Debug.LogWarning("활성 버튼이 없음. 강제로 첫 20개 스테이지 표시");
                UpdateVisibleButtons(1, Mathf.Min(20, totalStages));
                firstVisibleStage = 1;
                lastVisibleStage = Mathf.Min(20, totalStages);
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

            // 🔥 추가: StageFeed 초기화 완료 확인
            if (!stageFeedInitialized)
            {
                //Debug.Log("[CandyCrushStageMapView] StageFeed가 아직 초기화되지 않았습니다. UpdateViewport 건너뜀");
                return;
            }

            // 🔥 추가: 총 스테이지 수 검증으로 무한 루프 방지
            int actualTotalStages = GetActualTotalStages();
            if (actualTotalStages <= 0 || actualTotalStages > 1000)
            {
                Debug.LogError($"[CandyCrushStageMapView] UpdateViewport - 비정상적인 총 스테이지 수: {actualTotalStages}. 뷰포트 업데이트 중단.");
                return;
            }

            // 현재 뷰포트 범위 계산
            Vector2 viewportMin, viewportMax;
            GetViewportBounds(out viewportMin, out viewportMax);

            // 버퍼 영역 추가
            viewportMin.y -= viewportBuffer;
            viewportMax.y += viewportBuffer;

            // 가시 스테이지 범위 계산
            int newFirstVisible, newLastVisible;
            CalculateVisibleStageRange(viewportMin, viewportMax, out newFirstVisible, out newLastVisible);

            // 🔥 추가: 계산된 범위 재검증 (무효한 범위 차단)
            if (newFirstVisible < 1 || newLastVisible > actualTotalStages || newFirstVisible > newLastVisible)
            {
                Debug.LogError($"[CandyCrushStageMapView] UpdateViewport - 비정상적인 가시 범위: [{newFirstVisible}, {newLastVisible}] (총 스테이지: {actualTotalStages})");
                return;
            }

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

            // 🔥 수정: 실제 메타데이터 기반 스테이지 수 사용
            int totalStages = GetActualTotalStages();
            
            // 🔥 추가: 총 스테이지 수 유효성 검증
            if (totalStages <= 0 || totalStages > 1000)
            {
                Debug.LogError($"[CandyCrushStageMapView] 비정상적인 총 스테이지 수: {totalStages}. 기본값 14 사용.");
                totalStages = 14;
            }

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
            
            // 🔥 추가: 계산된 범위가 유효한지 재검증
            firstVisible = Mathf.Clamp(firstVisible, 1, totalStages);
            lastVisible = Mathf.Clamp(lastVisible, firstVisible, totalStages);
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

            // 🔥 수정: 새로운 범위에서 아직 생성되지 않은 버튼들 생성 (경계 검증 추가)
            int actualTotalStages = GetActualTotalStages();
            for (int stage = newFirstVisible; stage <= newLastVisible; stage++)
            {
                // 🔥 추가: 유효한 스테이지 범위 검증
                if (stage < 1 || stage > actualTotalStages)
                {
                    Debug.LogError($"[CandyCrushStageMapView] 유효하지 않은 스테이지 번호 스킵: {stage} (총 스테이지: {actualTotalStages})");
                    continue;
                }

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

            // 🔥 수정: GameObject 활성화 상태 검증 및 강제 활성화
            if (!this.gameObject.activeSelf)
            {
                Debug.LogWarning($"[CandyCrushStageMapView] CreateStageButton({stageNumber}) - GameObject 비활성화 상태에서 호출됨. 강제 활성화.");
                this.gameObject.SetActive(true);
            }

            // 풀에서 버튼 가져오기
            StageButton button = buttonPool.GetButton();
            if (button == null) return;

            // 버튼 초기화 (상단 여백 고려)
            Vector2 stagePosition = stageFeed.GetStagePosition(stageNumber);
            Vector3 adjustedPosition = new Vector3(stagePosition.x, stagePosition.y - topPadding, 0);

            // 🔥 추가: 위치 유효성 검증
            if (Mathf.Abs(adjustedPosition.y) > 10000f || Mathf.Abs(adjustedPosition.x) > 5000f)
            {
                Debug.LogError($"[CandyCrushStageMapView] 비정상적인 버튼 위치 감지! Stage={stageNumber}, Position={adjustedPosition}, Original={stagePosition}");
                
                // 🔥 비상 위치 보정: 스테이지 번호 기반으로 안전한 위치 계산
                float safeY = -(stageNumber - 1) * 180f - topPadding;
                float safeX = 0f;
                adjustedPosition = new Vector3(safeX, safeY, 0);
                
                Debug.LogWarning($"[CandyCrushStageMapView] 비상 위치 보정 적용: Stage={stageNumber}, SafePosition={adjustedPosition}");
            }

            button.transform.SetParent(contentTransform, false);
            button.transform.localPosition = adjustedPosition;

            // 🔥 추가: 설정 후 위치 재검증
            Vector3 finalPosition = button.transform.localPosition;
            if (Mathf.Abs(finalPosition.y) > 10000f || Mathf.Abs(finalPosition.x) > 5000f)
            {
                Debug.LogError($"[CandyCrushStageMapView] 버튼 위치 설정 실패! Stage={stageNumber}, FinalPosition={finalPosition}");
                
                // 🔥 최종 비상 조치: 직접 안전한 위치 재설정
                float emergencyY = -(stageNumber - 1) * 180f - topPadding;
                button.transform.localPosition = new Vector3(0, emergencyY, 0);
                Debug.LogWarning($"[CandyCrushStageMapView] 최종 비상 위치 적용: Stage={stageNumber}, EmergencyPosition={button.transform.localPosition}");
            }

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

            // 🔥 추가: SingleCoreBootstrap 데이터 로딩 완료 이벤트 구독
            if (Features.Single.Core.SingleCoreBootstrap.Instance != null)
            {
                Features.Single.Core.SingleCoreBootstrap.Instance.OnDataLoadingComplete += OnDataLoadingComplete;
                Features.Single.Core.SingleCoreBootstrap.Instance.OnDataLoadingFailed += OnDataLoadingFailed;
                Debug.Log("[CandyCrushStageMapView] SingleCoreBootstrap 데이터 로딩 이벤트 핸들러 설정 완료");
                
                // 🔥 핵심 수정: 이미 데이터가 로드되어 있다면 즉시 StageFeed 초기화
                if (Features.Single.Core.SingleCoreBootstrap.Instance.IsDataLoaded())
                {
                    Debug.Log("[CandyCrushStageMapView] 데이터 이미 로드됨 - 즉시 StageFeed 초기화");
                    OnDataLoadingComplete();
                }
            }

            // Features.Single.Core.UserDataCache 이벤트 구독(기존 유지)
            Debug.Log($"[CandyCrushStageMapView] Features.Single.Core.UserDataCache.Instance null 여부: {Features.Single.Core.UserDataCache.Instance == null}");
            if (Features.Single.Core.UserDataCache.Instance != null)
            {
                Features.Single.Core.UserDataCache.Instance.OnUserDataUpdated += OnUserDataUpdated;
                Features.Single.Core.UserDataCache.Instance.OnLoginStatusChanged += OnLoginStatusChanged;
                Debug.Log("[CandyCrushStageMapView] Features.Single.Core.UserDataCache 이벤트 핸들러 설정 완료");
                Debug.Log("[CandyCrushStageMapView] 프로필 데이터 즉시 업데이트는 OnEnable에서 처리");
            }

            // 🔥 추가: HttpApiClient의 진행도 관련 이벤트 구독 (사용자 전환 시 즉시 UI 업데이트)
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.OnBatchProgressReceived += OnProgressDataChanged;
                HttpApiClient.Instance.OnCurrentStatusReceived += OnCurrentStatusChanged;
                Debug.Log("[CandyCrushStageMapView] 진행도 변경 이벤트 핸들러 설정 완료");
            }
        }


        /// <summary>
        /// API 이벤트 핸들러 정리
        /// </summary>
        private void CleanupApiEventHandlers()
        {
            // 🔥 추가: SingleCoreBootstrap 이벤트 해제
            if (Features.Single.Core.SingleCoreBootstrap.Instance != null)
            {
                Features.Single.Core.SingleCoreBootstrap.Instance.OnDataLoadingComplete -= OnDataLoadingComplete;
                Features.Single.Core.SingleCoreBootstrap.Instance.OnDataLoadingFailed -= OnDataLoadingFailed;
            }

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
                Features.Single.Core.UserDataCache.Instance.OnLoginStatusChanged -= OnLoginStatusChanged;
            }

            // 🔥 추가: HttpApiClient 이벤트 구독 해제
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.OnBatchProgressReceived -= OnProgressDataChanged;
                HttpApiClient.Instance.OnCurrentStatusReceived -= OnCurrentStatusChanged;
            }
        }

        private void OnStageMetadataReceived(HttpApiClient.CompactStageMetadata[] metadata)
        {
            // 🔥 추가: 메타데이터 변경 시 캐시 무효화
            InvalidateTotalStagesCache();
            
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

            // 🔥 최적화: StageFeed 업데이트는 선택적 (GetActualTotalStages()에서 직접 메타데이터 사용)
            // StageFeed의 totalStages도 동기화하여 일관성 유지 (경로 재생성 등에서 사용)
            if (stageFeed != null)
            {
                stageFeed.UpdateTotalStagesFromMetadata();
            }

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

            // 🔥 개선: 프로필 로드 후에는 전체 새로고침 필요 (사용자 전환)
            RefreshChangedStageButtons(null);

            // 진행도 텍스트 업데이트  
            UpdateUIInfo();
        }

        /// <summary>
        /// 🔥 수정: 진행도 데이터 변경 시 UI 즉시 새로고침
        /// 사용자 전환 감지 로직 개선 - 스테이지 완료와 구분
        /// </summary>
        private void OnProgressDataChanged(HttpApiClient.CompactUserProgress[] progressArray)
        {
            Debug.Log($"[CandyCrushStageMapView] 진행도 데이터 변경됨: {progressArray?.Length ?? 0}개");
            
            // 🔥 수정: 사용자 전환 감지 조건 강화 - 더 엄격한 조건
            bool shouldReset = false;
            if (progressArray != null && progressArray.Length > 0)
            {
                // 모든 진행도가 0인지 확인
                bool allProgressIsZero = true;
                foreach (var progress in progressArray)
                {
                    if (progress.c == true || progress.s > 0) // 완료되었거나 별이 있음
                    {
                        allProgressIsZero = false;
                        break;
                    }
                }
                
                // 🔥 수정: 사용자 전환 감지 조건 강화
                // 1. 모든 진행도가 0이고
                // 2. 기존에 많은 버튼이 있었고 (5개 이상)
                // 3. 현재 로그인 상태이지만 maxStageCompleted가 0인 경우만
                if (allProgressIsZero && activeButtons.Count > 5)
                {
                    int currentMaxCompleted = 0;
                    if (UserDataCache.Instance != null && UserDataCache.Instance.IsLoggedIn())
                    {
                        currentMaxCompleted = UserDataCache.Instance.MaxStageCompleted;
                    }
                    
                    // maxStageCompleted가 0인 경우에만 사용자 전환으로 판단
                    if (currentMaxCompleted == 0)
                    {
                        Debug.Log("[CandyCrushStageMapView] 🔄 새 사용자 감지 (모든 진행도 0 + maxCompleted 0) - UI 완전 리셋");
                        shouldReset = true;
                    }
                    else
                    {
                        // 🔥 로그 축소: 스테이지 완료 후 정상 갱신은 일반적이므로 로그 생략
                    }
                }
            }
            
            if (shouldReset)
            {
                ResetForUserSwitch();
                return; // 리셋 후에는 업데이트 불필요
            }
            
            // 🔥 개선: 순차적 업데이트 큐에 추가
            HashSet<int> changedStages = ExtractChangedStages(progressArray);
            if (changedStages.Count > 0)
            {
                Debug.Log($"[CandyCrushStageMapView] 진행도 변경 감지 - 업데이트할 스테이지: {string.Join(", ", changedStages)}");
                QueueUpdate(() => RefreshChangedStageButtons(changedStages));
            }
            else
            {
                Debug.Log("[CandyCrushStageMapView] 진행도 변경 없음 - UI 업데이트 스킵");
            }
        }

        /// <summary>
        /// 🔥 수정: 현재 상태 변경 시 UI 즉시 새로고침
        /// 사용자 전환 감지 로직 개선 - 스테이지 완료와 구분
        /// </summary>
        private void OnCurrentStatusChanged(HttpApiClient.CurrentStatus currentStatus)
        {
            Debug.Log($"[CandyCrushStageMapView] 현재 상태 변경됨: max_stage_completed={currentStatus.max_stage_completed}");
            
            // 🔥 수정: 사용자 전환 감지 조건 강화
            // max_stage_completed가 0이고 기존 버튼이 많이 있으면서 (5개 이상)
            // 현재 UserDataCache의 maxStageCompleted도 실제로 0인 경우에만 리셋
            bool shouldReset = false;
            if (currentStatus.max_stage_completed == 0 && activeButtons.Count > 5)
            {
                // 실제 UserDataCache에서도 maxStageCompleted가 0인지 확인
                int cachedMaxCompleted = 0;
                if (UserDataCache.Instance != null && UserDataCache.Instance.IsLoggedIn())
                {
                    cachedMaxCompleted = UserDataCache.Instance.MaxStageCompleted;
                }
                
                if (cachedMaxCompleted == 0)
                {
                    Debug.Log("[CandyCrushStageMapView] 🔄 새 사용자 감지 (max_completed=0 + 캐시도 0) - UI 완전 리셋");
                    shouldReset = true;
                }
                else
                {
                    // 🔥 로그 축소: 일시적 동기화 문제는 정상적이므로 로그 생략
                }
            }
            
            if (shouldReset)
            {
                ResetForUserSwitch();
                return; // 리셋 후에는 업데이트 불필요
            }
            
            // 🔥 개선: 상태 변경으로 인한 언락 스테이지 변화만 업데이트
            // 이전 maxStageCompleted와 비교하여 변경된 스테이지만 감지
            HashSet<int> changedStages = new HashSet<int>();
            int newMaxCompleted = currentStatus.max_stage_completed;
            
            // 새로 언락된 스테이지 (maxCompleted + 1)만 업데이트
            int newUnlockedStage = newMaxCompleted + 1;
            if (newUnlockedStage > 0)
            {
                changedStages.Add(newUnlockedStage);
            }
            
            if (changedStages.Count > 0)
            {
                Debug.Log($"[CandyCrushStageMapView] 상태 변경으로 업데이트할 스테이지: {string.Join(", ", changedStages)}");
                QueueUpdate(() => RefreshChangedStageButtons(changedStages));
            }
        }

        /// <summary>
        /// 🔥 추가: 로그인 상태 변경 시 처리 (로그아웃/사용자 전환)
        /// </summary>
        private void OnLoginStatusChanged()
        {
            Debug.Log("[CandyCrushStageMapView] 로그인 상태 변경됨");
            
            // 현재 로그인 상태 확인
            bool isCurrentlyLoggedIn = Features.Single.Core.UserDataCache.Instance?.IsLoggedIn() ?? false;
            
            if (!isCurrentlyLoggedIn)
            {
                Debug.Log("[CandyCrushStageMapView] 사용자 로그아웃 감지 - UI 완전 초기화 시작");
                ResetForUserSwitch();
            }
            else
            {
                Debug.Log("[CandyCrushStageMapView] 사용자 로그인/전환 감지 - 새 사용자 데이터 대기");
                // 새 로그인의 경우, OnUserDataUpdated에서 처리됨
            }
        }

        /// <summary>
        /// 🔥 개선: 변경된 스테이지만 선택적으로 업데이트 (성능 최적화)
        /// 전체 새로고침 대신 변경 사항만 적용
        /// </summary>
        private void RefreshChangedStageButtons(HashSet<int> changedStages = null)
        {
            // 🔥 수정: GameObject 활성화 상태 검증
            if (!this.gameObject.activeSelf)
            {
                Debug.LogWarning("[CandyCrushStageMapView] RefreshStageButtons - GameObject 비활성화 상태에서 호출됨. 강제 활성화.");
                this.gameObject.SetActive(true);
            }

            // 🔥 추가: StageSelectPanel 활성화 상태 검증
            var stageSelectPanel = GameObject.Find("StageSelectPanel");
            if (stageSelectPanel != null && !stageSelectPanel.activeSelf)
            {
                Debug.LogWarning("[CandyCrushStageMapView] RefreshStageButtons - StageSelectPanel 비활성화 상태 감지. 활성화.");
                stageSelectPanel.SetActive(true);
            }

            // 🔥 추가: 중복 리프레시 방지 - Throttling
            float currentTime = Time.time;
            if (isButtonRefreshInProgress)
            {
                Debug.Log("[CandyCrushStageMapView] RefreshStageButtons 진행 중 - 스킵");
                return;
            }
            
            if (currentTime - lastButtonRefreshTime < BUTTON_REFRESH_THROTTLE)
            {
                Debug.Log($"[CandyCrushStageMapView] RefreshStageButtons 너무 빠른 호출 - 스킵 (마지막: {lastButtonRefreshTime:F2}s, 현재: {currentTime:F2}s)");
                return;
            }
            
            isButtonRefreshInProgress = true;
            lastButtonRefreshTime = currentTime;
            
            // 🔥 개선: 변경된 스테이지만 업데이트하여 성능 최적화
            if (changedStages == null)
            {
                Debug.Log($"[CandyCrushStageMapView] 전체 새로고침 모드 - 기존 활성 버튼 수: {activeButtons.Count}");
                // 전체 새로고침이 필요한 경우 (사용자 전환 등)
                RefreshAllStageButtonsInternal();
            }
            else
            {
                Debug.Log($"[CandyCrushStageMapView] 선택적 업데이트 모드 - 변경된 스테이지: [{string.Join(", ", changedStages)}]");
                // 변경된 스테이지만 업데이트
                RefreshSpecificStageButtons(changedStages);
            }
            
            // 🔥 추가: UI 정보 업데이트 (진행률, 별 개수)
            UpdateUIInfo();
            
            // 🔥 추가: Throttling 플래그 해제
            isButtonRefreshInProgress = false;
        }

        /// <summary>
        /// 🔥 신규: 전체 스테이지 버튼 새로고침 (기존 로직 유지)
        /// </summary>
        private void RefreshAllStageButtonsInternal()
        {
            // 현재 사용자의 진행도 기준으로 언락된 스테이지 수 계산
            int maxStageCompleted = 0;
            if (UserDataCache.Instance != null && UserDataCache.Instance.IsLoggedIn())
            {
                maxStageCompleted = UserDataCache.Instance.MaxStageCompleted;
            }

            // 언락된 스테이지 수 = 완료된 스테이지 + 1 (도전 스테이지)
            int targetUnlockedCount = maxStageCompleted + 1;
            
            Debug.Log($"[CandyCrushStageMapView] 사용자 진행도: max_completed={maxStageCompleted}, 목표 언락 수={targetUnlockedCount}");

            // 총 스테이지 수 확인 (메타데이터에서)
            int totalStages = 14; // 기본값
            if (UserDataCache.Instance != null)
            {
                var metadata = UserDataCache.Instance.GetStageMetadata();
                if (metadata != null && metadata.Length > 0)
                {
                    totalStages = metadata.Length;
                }
            }

            // 모든 스테이지에 대해 버튼 확보 및 상태 업데이트
            for (int stageNumber = 1; stageNumber <= totalStages; stageNumber++)
            {
                bool shouldBeUnlocked = stageNumber <= targetUnlockedCount;
                
                // 버튼이 없거나 비활성화되어 있으면 새로 생성/활성화
                if (!activeButtons.ContainsKey(stageNumber) || 
                    (activeButtons.ContainsKey(stageNumber) && activeButtons[stageNumber] != null && !activeButtons[stageNumber].gameObject.activeSelf))
                {
                    if (activeButtons.ContainsKey(stageNumber) && activeButtons[stageNumber] != null && !activeButtons[stageNumber].gameObject.activeSelf)
                    {
                        activeButtons[stageNumber].gameObject.SetActive(true);
                    }
                    else
                    {
                        CreateStageButton(stageNumber);
                    }
                }

                // 버튼 상태 업데이트 (언락/잠금)
                if (activeButtons.ContainsKey(stageNumber) && activeButtons[stageNumber] != null)
                {
                    UpdateButtonState(activeButtons[stageNumber], stageNumber);
                }
            }

            Debug.Log($"[CandyCrushStageMapView] 전체 새로고침 완료 - 최종 활성 버튼 수: {activeButtons.Count}");
        }

        /// <summary>
        /// 🔥 신규: 특정 스테이지들만 선택적 업데이트 (성능 최적화)
        /// </summary>
        private void RefreshSpecificStageButtons(HashSet<int> changedStages)
        {
            // 현재 사용자의 진행도 확인
            int maxStageCompleted = 0;
            if (UserDataCache.Instance != null && UserDataCache.Instance.IsLoggedIn())
            {
                maxStageCompleted = UserDataCache.Instance.MaxStageCompleted;
            }
            int targetUnlockedCount = maxStageCompleted + 1;

            foreach (int stageNumber in changedStages)
            {
                bool shouldBeUnlocked = stageNumber <= targetUnlockedCount;
                
                // 버튼이 없으면 생성
                if (!activeButtons.ContainsKey(stageNumber))
                {
                    Debug.Log($"[CandyCrushStageMapView] 스테이지 {stageNumber} 버튼 생성 (선택적 업데이트)");
                    CreateStageButton(stageNumber);
                }
                
                // 버튼이 비활성화되어 있으면 활성화
                if (activeButtons.ContainsKey(stageNumber) && activeButtons[stageNumber] != null)
                {
                    if (!activeButtons[stageNumber].gameObject.activeSelf)
                    {
                        Debug.Log($"[CandyCrushStageMapView] 스테이지 {stageNumber} 버튼 재활성화");
                        activeButtons[stageNumber].gameObject.SetActive(true);
                    }
                    
                    // 상태 업데이트
                    UpdateButtonState(activeButtons[stageNumber], stageNumber);
                    // 🔥 로그 축소: 핵심 변경사항만 출력
                    if (shouldBeUnlocked)
                    {
                        Debug.Log($"[CandyCrushStageMapView] ✅ 스테이지 {stageNumber} 언락 상태 업데이트");
                    }
                }
            }

            Debug.Log($"[CandyCrushStageMapView] 선택적 업데이트 완료 - {changedStages.Count}개 스테이지 처리됨");
        }

        /// <summary>
        /// 🔥 신규: 강제 스테이지 버튼 리프레시 (Throttling 무시, 게임 완료 후 사용)
        /// GameResultModal에서 호출되어 즉시 UI 업데이트를 보장
        /// </summary>
        public void ForceRefreshStageButtons()
        {
            Debug.Log("[CandyCrushStageMapView] ForceRefreshStageButtons 호출됨 - Throttling 무시하고 즉시 실행");
            
            // 🔥 핵심: GameObject와 StageSelectPanel 강제 활성화
            if (!this.gameObject.activeSelf)
            {
                Debug.LogWarning("[CandyCrushStageMapView] ForceRefresh - GameObject 강제 활성화");
                this.gameObject.SetActive(true);
            }

            var stageSelectPanel = GameObject.Find("StageSelectPanel");
            if (stageSelectPanel != null && !stageSelectPanel.activeSelf)
            {
                Debug.LogWarning("[CandyCrushStageMapView] ForceRefresh - StageSelectPanel 강제 활성화");
                stageSelectPanel.SetActive(true);
            }

            // 🔥 핵심: Throttling 플래그 강제 초기화 (즉시 실행 보장)
            isButtonRefreshInProgress = false;
            lastButtonRefreshTime = 0f; // 강제 리셋으로 즉시 실행 허용
            
            // 🔥 추가: StageFeed 업데이트 및 Content Height 재계산 보장
            if (stageFeed != null)
            {
                stageFeed.UpdateTotalStagesFromMetadata(); // totalStages 업데이트 → GeneratePath() 호출 → OnPathGenerated() → SetupScrollContent()
            }
            
            // 🔥 현재 완료된 스테이지 기반으로 선택적 업데이트
            HashSet<int> priorityStages = new HashSet<int>();
            
            // 최근 완료된 스테이지 포함 (현재 스테이지가 있다면)
            int currentStage = Features.Single.Gameplay.SingleGameManager.CurrentStage;
            if (currentStage > 0)
            {
                priorityStages.Add(currentStage);
                
                // 새로 언락될 가능성이 있는 다음 스테이지도 포함
                int totalStages = 14; // 기본값
                if (UserDataCache.Instance != null)
                {
                    var metadata = UserDataCache.Instance.GetStageMetadata();
                    if (metadata != null && metadata.Length > 0)
                    {
                        // 🔥 안전장치: 비정상적으로 큰 값일 때 기본값 사용
                        if (metadata.Length > 100)
                        {
                            Debug.LogError($"[CandyCrushStageMapView] ForceRefresh - 비정상적인 메타데이터 길이: {metadata.Length}개. 기본값 14개 사용.");
                            totalStages = 14;
                        }
                        else
                        {
                            totalStages = metadata.Length;
                        }
                    }
                }
                
                if (currentStage + 1 <= totalStages)
                {
                    priorityStages.Add(currentStage + 1);
                }
            }
            
            // 전체 활성 버튼들의 위치 재검증 및 보정
            foreach (var kvp in activeButtons)
            {
                if (kvp.Value != null && kvp.Value.gameObject.activeSelf)
                {
                    priorityStages.Add(kvp.Key);
                }
            }
            
            Debug.Log($"[CandyCrushStageMapView] ForceRefresh - 우선순위 스테이지: [{string.Join(", ", priorityStages)}]");
            
            // 선택적 업데이트 호출 (throttling 이미 해제됨)
            RefreshChangedStageButtons(priorityStages.Count > 0 ? priorityStages : null);
            
            Debug.Log("[CandyCrushStageMapView] ForceRefreshStageButtons 완료");
        }

        /// <summary>
        /// 🔥 신규: 진행도 배열에서 변경된 스테이지 감지 (성능 최적화)
        /// </summary>
        private HashSet<int> ExtractChangedStages(HttpApiClient.CompactUserProgress[] progressArray)
        {
            HashSet<int> changedStages = new HashSet<int>();
            
            if (progressArray == null) return changedStages;

            foreach (var progress in progressArray)
            {
                int stageNum = progress.n;
                
                // 기존 캐시와 비교하여 변경 사항 확인
                var cachedProgress = UserDataCache.Instance?.GetStageProgress(stageNum);
                
                bool hasChanged = false;
                
                if (cachedProgress == null && (progress.c || progress.s > 0))
                {
                    // 새로 완료된 스테이지
                    hasChanged = true;
                }
                else if (cachedProgress != null)
                {
                    // 기존 진행도와 비교
                    if (cachedProgress.isCompleted != progress.c || 
                        cachedProgress.starsEarned != progress.s)
                    {
                        hasChanged = true;
                    }
                }
                
                if (hasChanged)
                {
                    changedStages.Add(stageNum);
                    
                    // 언락 상태도 변경될 수 있으므로 다음 스테이지도 확인
                    if (progress.c) // 완료된 경우 다음 스테이지가 언락됨
                    {
                        int nextStage = stageNum + 1;
                        int totalStages = UserDataCache.Instance?.GetStageMetadata()?.Length ?? 14;
                        if (nextStage <= totalStages)
                        {
                            changedStages.Add(nextStage);
                        }
                    }
                }
            }
            
            return changedStages;
        }

        /// <summary>
        /// 🔥 신규: 업데이트 작업을 큐에 추가하여 순차적 처리
        /// </summary>
        private void QueueUpdate(System.Action updateAction)
        {
            updateQueue.Enqueue(updateAction);
            
            if (!isProcessingQueue)
            {
                StartSafe(ProcessUpdateQueue());
            }
        }

        /// <summary>
        /// 🔥 신규: 업데이트 큐 순차 처리 (LoadingOverlay 지원)
        /// </summary>
        private System.Collections.IEnumerator ProcessUpdateQueue()
        {
            if (isProcessingQueue) yield break;
            
            isProcessingQueue = true;
            
            // LoadingOverlay 표시 (큐에 2개 이상의 작업이 있는 경우)
            bool shouldShowLoading = updateQueue.Count > 1;
            
            if (shouldShowLoading && App.UI.LoadingOverlay.Instance != null)
            {
                App.UI.LoadingOverlay.Show("스테이지 정보 업데이트 중...");
                yield return new WaitForSeconds(0.1f); // 로딩 화면 표시 시간
            }
            
            while (updateQueue.Count > 0)
            {
                var updateAction = updateQueue.Dequeue();
                
                try
                {
                    updateAction?.Invoke();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[CandyCrushStageMapView] 업데이트 처리 중 오류: {ex.Message}");
                }
                
                // 각 업데이트 작업 사이에 프레임 대기
                yield return null;
            }
            
            // LoadingOverlay 숨김
            if (shouldShowLoading && App.UI.LoadingOverlay.Instance != null)
            {
                App.UI.LoadingOverlay.Hide();
            }
            
            isProcessingQueue = false;
        }

        /// <summary>
        /// 🔥 추가: 사용자 전환/로그아웃 시 UI 완전 초기화
        /// 이전 사용자의 UI 상태가 새 사용자에게 영향주지 않도록 완전히 리셋
        /// </summary>
        public void ResetForUserSwitch()
        {
            Debug.Log("[CandyCrushStageMapView] 사용자 전환으로 인한 UI 완전 초기화 시작");

            // 모든 활성 버튼 풀에 반환 및 상태 초기화
            if (buttonPool != null)
            {
                buttonPool.ReturnAllButtons();
                Debug.Log($"[CandyCrushStageMapView] 모든 버튼 풀에 반환 완료");
            }

            // activeButtons 딕셔너리 완전 정리
            activeButtons.Clear();

            // 스크롤 상태 초기화
            firstVisibleStage = 1;
            lastVisibleStage = 1;
            
            // 동기화 상태 초기화
            hasSyncedOnce = false;
            isProfileUpdateInProgress = false;

            // UI 텍스트 초기화
            if (progressText != null)
                progressText.text = "";
            if (totalStarsText != null)
                totalStarsText.text = "⭐ 0";

            // 스크롤을 맨 위로 리셋
            if (scrollRect != null && scrollRect.content != null)
            {
                scrollRect.content.anchoredPosition = Vector2.zero;
                lastScrollPosition = Vector2.zero;
            }

            Debug.Log("[CandyCrushStageMapView] 사용자 전환 UI 초기화 완료");
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

            // 🔥 개선: 직접 전체 새로고침 호출 (DelayedProfileUpdate는 전체 새로고침 필요)
            RefreshChangedStageButtons(null);
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
        /// 🔥 개선: 스테이지 완료 이벤트 - 완료된 스테이지와 새로 언락된 스테이지만 업데이트
        /// </summary>
        private void OnStageCompleted(int stageNumber, int score, int stars)
        {
            Debug.Log($"[CandyCrushStageMapView] 스테이지 {stageNumber} 완료: {score}점, {stars}별");

            // 🔥 개선: 변경된 스테이지만 선택적 업데이트
            HashSet<int> changedStages = new HashSet<int>();
            changedStages.Add(stageNumber); // 완료된 스테이지
            
            // 다음 스테이지가 새로 언락될 수 있으므로 추가
            int nextStage = stageNumber + 1;
            int totalStages = UserDataCache.Instance?.GetStageMetadata()?.Length ?? 14;
            if (nextStage <= totalStages)
            {
                changedStages.Add(nextStage);
            }

            Debug.Log($"[CandyCrushStageMapView] 스테이지 완료로 업데이트할 스테이지: [{string.Join(", ", changedStages)}]");
            RefreshChangedStageButtons(changedStages);
        }

        /// <summary>
        /// 🔥 개선: 스테이지 언락 이벤트 - 언락된 스테이지만 업데이트
        /// </summary>
        private void OnStageUnlocked(int unlockedStageNumber)
        {
            Debug.Log($"[CandyCrushStageMapView] 스테이지 {unlockedStageNumber} 언락!");

            // 🔥 개선: 언락된 스테이지만 선택적 업데이트
            HashSet<int> changedStages = new HashSet<int> { unlockedStageNumber };
            RefreshChangedStageButtons(changedStages);

            // 새로 언락된 스테이지로 스크롤
            ScrollToStage(unlockedStageNumber);
        }

        // ========================================
        // UI 정보 업데이트
        // ========================================

        /// <summary>
        /// UI 진행도 정보 업데이트
        /// </summary>
        private void UpdateUIInfo()
        {
            if (progressManager == null) 
            {
                Debug.LogWarning("[CandyCrushStageMapView] UpdateUIInfo - progressManager가 null입니다");
                return;
            }

            // 🔥 추가: 중복 업데이트 방지 - Throttling
            float currentTime = Time.time;
            if (isUIUpdateInProgress)
            {
                Debug.Log("[CandyCrushStageMapView] UpdateUIInfo 진행 중 - 스킵");
                return;
            }
            
            if (currentTime - lastUIUpdateTime < UI_UPDATE_THROTTLE)
            {
                Debug.Log($"[CandyCrushStageMapView] UpdateUIInfo 너무 빠른 호출 - 스킵 (마지막: {lastUIUpdateTime:F2}s, 현재: {currentTime:F2}s)");
                return;
            }
            
            isUIUpdateInProgress = true;
            lastUIUpdateTime = currentTime;

            // 🔥 수정: 실제 메타데이터에서 총 스테이지 수 가져오기
            int totalStages = GetActualTotalStages();
            Debug.Log($"[CandyCrushStageMapView] UpdateUIInfo 시작 - totalStages: {totalStages}");

            // 진행률 텍스트 (완료한 스테이지 / 총 스테이지)
            if (progressText != null)
            {
                int maxCompleted = 0;
                if (UserDataCache.Instance != null && UserDataCache.Instance.IsLoggedIn())
                {
                    maxCompleted = UserDataCache.Instance.MaxStageCompleted;
                }
                
                float progress = (float)maxCompleted / totalStages * 100f;
                string progressString = $"진행률: {progress:F1}% ({maxCompleted}/{totalStages})";
                progressText.text = progressString;
                
                Debug.Log($"[CandyCrushStageMapView] 진행률 업데이트: {progressString}");
            }
            else
            {
                Debug.LogWarning("[CandyCrushStageMapView] progressText가 null입니다");
            }

            // 총 별 개수 (실제 획득한 별 / 총 가능한 별)
            if (totalStarsText != null)
            {
                int earnedStars = 0;
                
                // UserDataCache에서 실제 획득한 별 개수 계산
                if (UserDataCache.Instance != null)
                {
                    Debug.Log($"[CandyCrushStageMapView] 별 개수 계산 시작 - 총 스테이지: {totalStages}");
                    
                    for (int i = 1; i <= totalStages; i++)
                    {
                        var progress = UserDataCache.Instance.GetStageProgress(i);
                        if (progress != null && progress.starsEarned > 0)
                        {
                            earnedStars += progress.starsEarned;
                            Debug.Log($"[CandyCrushStageMapView] 스테이지 {i} - 별: {progress.starsEarned}개 (누적: {earnedStars})");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[CandyCrushStageMapView] UserDataCache.Instance가 null입니다");
                }
                
                int maxStars = totalStages * 3;
                string starsString = $"별: {earnedStars}/{maxStars} ★";
                totalStarsText.text = starsString;
                
                Debug.Log($"[CandyCrushStageMapView] 별 정보 업데이트: {starsString}");
            }
            else
            {
                Debug.LogWarning("[CandyCrushStageMapView] totalStarsText가 null입니다");
            }

            // 🔥 추가: Throttling 플래그 해제
            isUIUpdateInProgress = false;
        }

        /// <summary>
        /// 특정 스테이지로 스크롤
        /// </summary>
        public void ScrollToStage(int stageNumber)
        {
            if (!stageFeed.IsValidStage(stageNumber) || scrollRect == null) return;

            // 🔥 추가: 데이터 로딩 실패시 스크롤 건너뛰기
            int totalStages = stageFeed.GetTotalStages();
            if (totalStages == 0)
            {
                Debug.LogError($"[CandyCrushStageMapView] 스테이지 데이터 없음 - ScrollToStage({stageNumber}) 건너뛰기");
                return;
            }

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
            if (isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                return StartCoroutine(routine);
            }
            else
            {
                Debug.Log("[CandyCrushStageMapView] GameObject 비활성화 상태 - CoroutineRunner 사용");
                return App.Core.CoroutineRunner.Run(routine);
            }
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

        /// <summary>
        /// 🔥 추가: 실제 메타데이터 기반으로 총 스테이지 수 가져오기
        /// stageFeed.GetTotalStages()가 Inspector 설정에 의존하는 대신, 실제 데이터에서 가져옴
        /// </summary>
        private int GetActualTotalStages()
        {
            // 🔥 추가: 캐시된 값이 있으면 재사용 (메타데이터는 자주 변경되지 않음)
            if (cachedTotalStages > 0)
            {
                return cachedTotalStages;
            }

            // 1. UserDataCache의 메타데이터에서 실제 스테이지 수 가져오기 (우선순위)
            if (Features.Single.Core.UserDataCache.Instance != null)
            {
                var metadata = Features.Single.Core.UserDataCache.Instance.GetStageMetadata();
                if (metadata != null && metadata.Length > 0)
                {
                    // 🔥 안전장치: 비정상적으로 큰 값일 때 기본값 사용
                    if (metadata.Length > 100)
                    {
                        Debug.LogError($"[CandyCrushStageMapView] 비정상적인 메타데이터 길이 감지: {metadata.Length}개. 기본값 14개 사용.");
                        cachedTotalStages = 14;
                    }
                    else
                    {
                        cachedTotalStages = metadata.Length;
                    }
                    
                    // 🔥 수정: 로그 스팸 방지 - 5초마다만 로그 출력
                    float currentTime = Time.time;
                    if (currentTime - lastTotalStagesLogTime > TOTAL_STAGES_LOG_THROTTLE)
                    {
                        Debug.Log($"[CandyCrushStageMapView] 실제 메타데이터 기반 총 스테이지 수: {cachedTotalStages}개 (원본 길이: {metadata.Length})");
                        lastTotalStagesLogTime = currentTime;
                    }
                    
                    return cachedTotalStages;
                }
            }

            // 2. StageFeed 백업 (Inspector 설정)
            if (stageFeed != null)
            {
                int stageFeedTotal = stageFeed.GetTotalStages();
                cachedTotalStages = stageFeedTotal;
                Debug.LogWarning($"[CandyCrushStageMapView] 메타데이터 없음. StageFeed 백업 사용: {stageFeedTotal}개");
                return cachedTotalStages;
            }

            // 3. 최종 백업 (기본값)
            cachedTotalStages = 14;
            Debug.LogError("[CandyCrushStageMapView] 총 스테이지 수를 가져올 수 없음. 기본값 14 사용");
            return cachedTotalStages;
        }

        /// <summary>
        /// 🔥 추가: 메타데이터 변경 시 캐시 무효화
        /// </summary>
        private void InvalidateTotalStagesCache()
        {
            cachedTotalStages = -1;
        }

        /// <summary>
        /// 🔥 추가: SingleCoreBootstrap 데이터 로딩 완료 이벤트 핸들러
        /// </summary>
        private void OnDataLoadingComplete()
        {
            Debug.Log("[CandyCrushStageMapView] 데이터 로딩 완료됨 - StageFeed 초기화 시작");
            
            // 🔥 핵심: 데이터 로딩 완료 즉시 StageFeed 업데이트
            if (stageFeed != null)
            {
                stageFeed.UpdateTotalStagesFromMetadata();
                stageFeedInitialized = true; // 🔥 추가: StageFeed 초기화 완료 플래그 설정
                Debug.Log("[CandyCrushStageMapView] StageFeed 데이터 기반 초기화 완료");
            }
            
            // 🔥 추가: 스테이지 맵 새로고침
            RefreshStageMap();
        }

        /// <summary>
        /// 🔥 추가: SingleCoreBootstrap 데이터 로딩 실패 이벤트 핸들러
        /// </summary>
        private void OnDataLoadingFailed(string error)
        {
            Debug.LogError($"[CandyCrushStageMapView] 데이터 로딩 실패: {error}");
            
            // 🔥 데이터 로딩 실패시 StageFeed 기능 비활성화
            stageFeedInitialized = false; // 🔥 추가: StageFeed 초기화 실패 플래그 설정
            if (stageFeed != null)
            {
                // StageFeed에게 데이터 로딩 실패를 알려서 기능 비활성화하도록 함
                stageFeed.UpdateTotalStagesFromMetadata(); // UserDataCache가 null이므로 비활성화됨
            }
            
            // 사용자에게 알림
            SystemMessageManager.ShowToast("스테이지 데이터를 불러올 수 없습니다", MessagePriority.Error);
        }

        /// <summary>
        /// 🔥 추가: 데이터 로딩 상태 확인 및 StageFeed 초기화
        /// </summary>
        private void CheckAndInitializeStageFeed()
        {
            Debug.Log("[CandyCrushStageMapView] 데이터 로딩 상태 확인 중...");
            
            // SingleCoreBootstrap이 있고 이미 데이터가 로드되어 있다면
            if (Features.Single.Core.SingleCoreBootstrap.Instance != null && 
                Features.Single.Core.SingleCoreBootstrap.Instance.IsDataLoaded())
            {
                Debug.Log("[CandyCrushStageMapView] 데이터 이미 로드 완료 - StageFeed 즉시 초기화");
                
                // StageFeed 초기화
                if (stageFeed != null)
                {
                    stageFeed.UpdateTotalStagesFromMetadata();
                    stageFeedInitialized = true; // 🔥 추가: StageFeed 초기화 완료 플래그 설정
                    Debug.Log("[CandyCrushStageMapView] StageFeed 데이터 기반 초기화 완료");
                }
            }
            else
            {
                Debug.Log("[CandyCrushStageMapView] 데이터 로딩 대기 중 - 이벤트로 나중에 초기화");
            }
        }
    }
}