using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.Application.Stages;
using BlokusUnity.Game;
using BlokusUnity.Data;

namespace BlokusUnity.UI
{
    /// <summary>
    /// 캔디크러시 사가 스타일의 메인 스테이지 선택 뷰
    /// 뱀 모양 레이아웃과 스크롤 기반 동적 로딩을 제공
    /// </summary>
    public class CandyCrushStageMapView : BaseUIPanel
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
        
        [Header("성능 설정")]
        [SerializeField] private float viewportBuffer = 200f; // 뷰포트 확장 영역
        [SerializeField] private float updateInterval = 0.1f; // 업데이트 간격
        
        // 스테이지 관리
        private Dictionary<int, StageButton> activeButtons = new Dictionary<int, StageButton>();
        private StageProgressManager progressManager;
        private StageInfoModal stageInfoModal;
        
        // 뷰포트 관리
        private int firstVisibleStage = 1;
        private int lastVisibleStage = 1;
        private float lastUpdateTime = 0f;
        
        // 상태
        private bool isInitialized = false;
        
        protected override void Awake()
        {
            base.Awake();
            
            // 버튼 이벤트 연결
            if (backButton != null)
            {
                backButton.onClick.AddListener(() => UIManager.Instance?.OnBackToMenu());
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
        }
        
        protected override void Start()
        {
            base.Start();
            InitializeStageMap();
        }
        
        void Update()
        {
            // 주기적으로 뷰포트 업데이트 (성능 최적화)
            if (Time.time - lastUpdateTime > updateInterval)
            {
                UpdateViewport();
                lastUpdateTime = Time.time;
            }
        }
        
        void OnDestroy()
        {
            // 이벤트 해제
            if (progressManager != null)
            {
                progressManager.OnStageCompleted -= OnStageCompleted;
                progressManager.OnStageUnlocked -= OnStageUnlocked;
            }
            
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
            if (isInitialized) return;
            
            // 컴포넌트 검증
            if (!ValidateComponents()) return;
            
            // 진행도 매니저 연결
            progressManager = StageProgressManager.Instance;
            if (progressManager != null)
            {
                progressManager.OnStageCompleted += OnStageCompleted;
                progressManager.OnStageUnlocked += OnStageUnlocked;
            }
            
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
            Debug.Log("CandyCrushStageMapView 초기화 완료");
        }
        
        /// <summary>
        /// 필수 컴포넌트들 검증
        /// </summary>
        private bool ValidateComponents()
        {
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
        /// 스테이지 정보 모달 찾기 또는 생성
        /// </summary>
        private void FindOrCreateStageInfoModal()
        {
            stageInfoModal = FindObjectOfType<StageInfoModal>();
            
            // TODO: 모달이 없으면 생성 로직 추가
            if (stageInfoModal == null)
            {
                Debug.LogWarning("StageInfoModal을 찾을 수 없습니다. 나중에 구현 예정");
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
            
            // 콘텐츠 크기 설정
            float totalHeight = stageFeed.GetTotalHeight();
            float totalWidth = stageFeed.GetTotalWidth();
            
            contentTransform.sizeDelta = new Vector2(totalWidth, totalHeight);
            
            // 초기 뷰포트 업데이트
            UpdateViewport();
            
            // UI 정보 업데이트
            UpdateUIInfo();
            
            Debug.Log($"스크롤 콘텐츠 설정 완료: {totalWidth}x{totalHeight}");
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
        /// 가시 스테이지 범위 계산
        /// </summary>
        private void CalculateVisibleStageRange(Vector2 viewportMin, Vector2 viewportMax, 
                                                out int firstVisible, out int lastVisible)
        {
            firstVisible = 1;
            lastVisible = 1;
            
            int totalStages = stageFeed.GetTotalStages();
            
            for (int stage = 1; stage <= totalStages; stage++)
            {
                Vector2 stagePos = stageFeed.GetStagePosition(stage);
                
                // 스테이지가 뷰포트 내에 있는지 확인
                if (IsPositionInViewport(stagePos, viewportMin, viewportMax))
                {
                    if (firstVisible == 1 || stage < firstVisible)
                        firstVisible = stage;
                    
                    if (stage > lastVisible)
                        lastVisible = stage;
                }
            }
            
            // 최소한 하나의 스테이지는 보이도록
            if (firstVisible > lastVisible)
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
        /// 가시 버튼들 업데이트
        /// </summary>
        private void UpdateVisibleButtons(int newFirstVisible, int newLastVisible)
        {
            // 범위 밖의 버튼들 비활성화
            var buttonsToRemove = new List<int>();
            foreach (var kvp in activeButtons)
            {
                int stage = kvp.Key;
                if (stage < newFirstVisible || stage > newLastVisible)
                {
                    // 버튼을 풀에 반환
                    buttonPool.ReturnButton(kvp.Value);
                    buttonsToRemove.Add(stage);
                }
            }
            
            foreach (int stage in buttonsToRemove)
            {
                activeButtons.Remove(stage);
            }
            
            // 새로운 범위의 버튼들 활성화
            for (int stage = newFirstVisible; stage <= newLastVisible; stage++)
            {
                if (!activeButtons.ContainsKey(stage))
                {
                    CreateStageButton(stage);
                }
            }
            
            Debug.Log($"가시 버튼 업데이트: {newFirstVisible}-{newLastVisible} " +
                     $"({activeButtons.Count}개 활성)");
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
            
            // 버튼 초기화
            Vector2 stagePosition = stageFeed.GetStagePosition(stageNumber);
            button.transform.SetParent(contentTransform, false);
            button.transform.localPosition = new Vector3(stagePosition.x, stagePosition.y, 0);
            
            // StageButton 초기화 (기존 API 사용)
            button.Initialize(stageNumber, OnStageButtonClicked);
            
            // 진행도 정보 적용
            UpdateButtonState(button, stageNumber);
            
            // 활성 버튼 목록에 추가
            activeButtons[stageNumber] = button;
        }
        
        /// <summary>
        /// 버튼 상태 업데이트
        /// </summary>
        private void UpdateButtonState(StageButton button, int stageNumber)
        {
            if (progressManager == null) return;
            
            bool isUnlocked = progressManager.IsStageUnlocked(stageNumber);
            var progress = progressManager.GetCachedStageProgress(stageNumber);
            
            // StageProgress를 UserStageProgress로 변환 (StageButton 호환성)
            UserStageProgress userProgress = null;
            if (progress != null)
            {
                userProgress = new UserStageProgress
                {
                    stageNumber = stageNumber,
                    isCompleted = progress.isCompleted,
                    starsEarned = progress.starsEarned,
                    bestScore = progress.bestScore
                };
            }
            
            button.UpdateState(isUnlocked, userProgress);
        }
        
        /// <summary>
        /// 스테이지 버튼 클릭 이벤트
        /// </summary>
        private void OnStageButtonClicked(int stageNumber)
        {
            Debug.Log($"스테이지 {stageNumber} 클릭됨");
            
            // 잠금 확인
            if (!progressManager.IsStageUnlocked(stageNumber))
            {
                ShowUnlockedRequiredMessage(stageNumber);
                return;
            }
            
            // 스테이지 정보 모달 표시
            if (stageInfoModal != null)
            {
                var stageData = GetStageData(stageNumber);
                var progress = progressManager.GetCachedStageProgress(stageNumber);
                
                // StageProgress를 UserStageProgress로 변환
                UserStageProgress userProgress = null;
                if (progress != null)
                {
                    userProgress = new UserStageProgress
                    {
                        stageNumber = stageNumber,
                        isCompleted = progress.isCompleted,
                        bestScore = progress.bestScore
                    };
                }
                
                stageInfoModal.ShowStageInfo(stageData, userProgress);
            }
            else
            {
                // 모달이 없으면 바로 게임 시작
                StartStage(stageNumber);
            }
        }
        
        /// <summary>
        /// 스테이지 데이터 가져오기
        /// </summary>
        private StageData GetStageData(int stageNumber)
        {
            // StageDataManager를 통해 스테이지 데이터 가져오기
            if (StageDataManager.Instance != null)
            {
                var stageManager = StageDataManager.Instance.GetStageManager();
                if (stageManager != null)
                {
                    return stageManager.GetStageData(stageNumber);
                }
            }
            
            // 기본 테스트 데이터 반환
            return CreateTestStageData(stageNumber);
        }
        
        /// <summary>
        /// 테스트 스테이지 데이터 생성
        /// </summary>
        private StageData CreateTestStageData(int stageNumber)
        {
            var stageData = ScriptableObject.CreateInstance<StageData>();
            stageData.stageNumber = stageNumber;
            stageData.stageName = $"스테이지 {stageNumber}";
            stageData.stageDescription = $"테스트용 스테이지 {stageNumber}입니다.";
            stageData.difficulty = Mathf.CeilToInt(stageNumber / 10f);
            stageData.optimalScore = 100 + (stageNumber * 5);
            return stageData;
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
        /// 언락 필요 메시지 표시
        /// </summary>
        private void ShowUnlockedRequiredMessage(int stageNumber)
        {
            // TODO: 토스트 메시지나 간단한 알림 표시
            Debug.LogWarning($"스테이지 {stageNumber - 1}을 먼저 클리어하세요!");
        }
        
        // ========================================
        // 이벤트 핸들러들
        // ========================================
        
        /// <summary>
        /// 스테이지 완료 이벤트
        /// </summary>
        private void OnStageCompleted(int stageNumber, int stars, bool isNewRecord, bool isFirstClear)
        {
            Debug.Log($"스테이지 {stageNumber} 완료: {stars}별");
            
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
                totalStarsText.text = $"별: {earnedStars}/{maxStars} ⭐";
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
            StartCoroutine(SmoothScrollTo(new Vector2(scrollRect.normalizedPosition.x, normalizedY)));
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
            
            // 서버에서 최신 진행도 정보 요청
            if (progressManager != null)
            {
                int totalStages = stageFeed.GetTotalStages();
                int batchSize = 50;
                
                for (int start = 1; start <= totalStages; start += batchSize)
                {
                    int end = Mathf.Min(start + batchSize - 1, totalStages);
                    progressManager.RequestBatchStageProgressFromServer(start, end);
                }
            }
            
            // 현재 활성 버튼들 업데이트
            foreach (var kvp in activeButtons)
            {
                UpdateButtonState(kvp.Value, kvp.Key);
            }
            
            // UI 정보 업데이트
            UpdateUIInfo();
        }
        
        /// <summary>
        /// BaseUIPanel 오버라이드
        /// </summary>
        public override void Show(bool animated = true)
        {
            base.Show(animated);
            
            // 표시될 때마다 초기화 및 새로고침
            InitializeStageMap();
            RefreshStageMap();
            
            // 현재 최대 언락 스테이지로 스크롤
            if (progressManager != null)
            {
                int maxUnlocked = progressManager.GetMaxUnlockedStage();
                if (maxUnlocked > 1)
                {
                    ScrollToStage(maxUnlocked);
                }
            }
        }
        
        public override void Hide(bool animated = true)
        {
            base.Hide(animated);
            
            // 모든 버튼을 풀에 반환
            if (buttonPool != null)
            {
                buttonPool.ReturnAllButtons();
            }
            activeButtons.Clear();
        }
    }
}