using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlokusUnity.Game;
using BlokusUnity.Data;

namespace BlokusUnity.UI
{
    /// <summary>
    /// 스테이지 선택 UI 컴포넌트
    /// 그리드 레이아웃으로 스테이지 버튼들을 표시하고 언락 상태 관리
    /// </summary>
    public class StageSelectUI : BaseUIPanel
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Transform stageButtonParent;
        [SerializeField] private GameObject stageButtonPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Text progressText;
        [SerializeField] private Text totalStarsText;
        [SerializeField] private Button backButton;
        
        [Header("스테이지 설정")]
        [SerializeField] private int stagesPerRow = 5;
        [SerializeField] private int totalStages = 100; // 처음엔 100개로 시작
        
        // 스테이지 버튼들 캐싱
        private List<StageButton> stageButtons = new List<StageButton>();
        private StageProgressManager progressManager;
        
        void Awake()
        {
            // Back 버튼 이벤트 연결
            if (backButton != null)
            {
                backButton.onClick.AddListener(() => UIManager.Instance?.OnBackToMenu());
            }
        }
        
        void Start()
        {
            progressManager = StageProgressManager.Instance;
            if (progressManager != null)
            {
                // 스테이지 완료 이벤트 구독
                progressManager.OnStageCompleted += OnStageCompleted;
                progressManager.OnStageUnlocked += OnStageUnlocked;
            }
            
            CreateStageButtons();
            UpdateUI();
        }
        
        void OnDestroy()
        {
            if (progressManager != null)
            {
                progressManager.OnStageCompleted -= OnStageCompleted;
                progressManager.OnStageUnlocked -= OnStageUnlocked;
            }
        }
        
        // ========================================
        // UI 생성 및 업데이트
        // ========================================
        
        /// <summary>
        /// 스테이지 버튼들 생성
        /// </summary>
        private void CreateStageButtons()
        {
            if (stageButtonPrefab == null || stageButtonParent == null)
            {
                Debug.LogError("StageButtonPrefab 또는 StageButtonParent가 설정되지 않았습니다!");
                return;
            }
            
            // 기존 버튼들 제거
            foreach (Transform child in stageButtonParent)
            {
                DestroyImmediate(child.gameObject);
            }
            stageButtons.Clear();
            
            // 그리드 레이아웃 설정
            GridLayoutGroup gridLayout = stageButtonParent.GetComponent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                gridLayout.constraintCount = stagesPerRow;
            }
            
            // 스테이지 버튼들 생성
            for (int i = 1; i <= totalStages; i++)
            {
                GameObject buttonObj = Instantiate(stageButtonPrefab, stageButtonParent);
                StageButton stageButton = buttonObj.GetComponent<StageButton>();
                
                if (stageButton != null)
                {
                    stageButton.Initialize(i, OnStageButtonClicked);
                    stageButtons.Add(stageButton);
                }
                else
                {
                    Debug.LogError($"StageButton 컴포넌트가 프리팹에 없습니다: {buttonObj.name}");
                }
            }
            
            Debug.Log($"{totalStages}개 스테이지 버튼 생성 완료");
        }
        
        /// <summary>
        /// UI 정보 업데이트 (진행률, 총 별 개수 등)
        /// </summary>
        private void UpdateUI()
        {
            if (progressManager == null) return;
            
            // 진행률 텍스트 업데이트
            if (progressText != null)
            {
                float progress = progressManager.GetOverallProgress(totalStages);
                int maxUnlocked = progressManager.GetMaxUnlockedStage();
                progressText.text = $"진행률: {progress:F1}% ({maxUnlocked}/{totalStages})";
            }
            
            // 총 별 개수 업데이트
            if (totalStarsText != null)
            {
                int earnedStars = progressManager.GetTotalStarsEarned();
                int maxStars = totalStages * 3; // 총 가능한 별 개수
                totalStarsText.text = $"별: {earnedStars}/{maxStars} ⭐";
            }
            
            // 각 스테이지 버튼 상태 업데이트
            UpdateStageButtons();
        }
        
        /// <summary>
        /// 모든 스테이지 버튼 상태 업데이트
        /// </summary>
        private void UpdateStageButtons()
        {
            if (progressManager == null) return;
            
            foreach (var stageButton in stageButtons)
            {
                int stageNumber = stageButton.StageNumber;
                
                // 언락 상태 확인
                bool isUnlocked = progressManager.IsStageUnlocked(stageNumber);
                
                // 진행도 정보 가져오기
                var progress = progressManager.GetCachedStageProgress(stageNumber);
                
                // 버튼 상태 업데이트
                stageButton.UpdateState(isUnlocked, progress);
            }
        }
        
        // ========================================
        // 이벤트 핸들러들
        // ========================================
        
        /// <summary>
        /// 스테이지 버튼 클릭 처리
        /// </summary>
        private void OnStageButtonClicked(int stageNumber)
        {
            Debug.Log($"스테이지 {stageNumber} 선택됨");
            
            // 언락 상태 확인
            if (!progressManager.IsStageUnlocked(stageNumber))
            {
                Debug.LogWarning($"스테이지 {stageNumber}는 아직 언락되지 않았습니다!");
                ShowUnlockMessage(stageNumber);
                return;
            }
            
            // UIManager를 통해 스테이지 선택 처리
            UIManager.Instance?.OnStageSelected(stageNumber);
        }
        
        /// <summary>
        /// 스테이지 완료 이벤트 처리
        /// </summary>
        private void OnStageCompleted(int stageNumber, int stars, bool isNewRecord, bool isFirstClear)
        {
            Debug.Log($"스테이지 {stageNumber} 완료: {stars}별 (신기록: {isNewRecord}, 첫클리어: {isFirstClear})");
            
            // UI 업데이트
            UpdateUI();
            
            // 완료 효과 표시 (선택사항)
            if (isFirstClear)
            {
                ShowStageCompletedEffect(stageNumber, stars);
            }
        }
        
        /// <summary>
        /// 새 스테이지 언락 이벤트 처리
        /// </summary>
        private void OnStageUnlocked(int unlockedStageNumber)
        {
            Debug.Log($"새 스테이지 언락: {unlockedStageNumber}");
            
            // UI 업데이트
            UpdateUI();
            
            // 언락 효과 표시 (선택사항)
            ShowStageUnlockedEffect(unlockedStageNumber);
            
            // 해당 스테이지로 스크롤 (선택사항)
            ScrollToStage(unlockedStageNumber);
        }
        
        // ========================================
        // UI 효과 및 피드백 (기본 구현)
        // ========================================
        
        /// <summary>
        /// 언락 메시지 표시
        /// </summary>
        private void ShowUnlockMessage(int stageNumber)
        {
            // TODO: 모달 팝업으로 "이전 스테이지를 클리어하세요" 메시지 표시
            Debug.Log($"스테이지 {stageNumber - 1}를 먼저 클리어하세요!");
        }
        
        /// <summary>
        /// 스테이지 완료 효과 표시
        /// </summary>
        private void ShowStageCompletedEffect(int stageNumber, int stars)
        {
            // TODO: 파티클 효과, 사운드, 애니메이션 등
            Debug.Log($"🎉 스테이지 {stageNumber} 클리어! {progressManager.GetStarString(stars)}");
        }
        
        /// <summary>
        /// 스테이지 언락 효과 표시
        /// </summary>
        private void ShowStageUnlockedEffect(int stageNumber)
        {
            // TODO: 글로우 효과, 사운드 등
            Debug.Log($"✨ 스테이지 {stageNumber} 언락!");
        }
        
        /// <summary>
        /// 특정 스테이지로 스크롤
        /// </summary>
        private void ScrollToStage(int stageNumber)
        {
            if (scrollRect == null) return;
            
            // 간단한 스크롤 계산 (개선 가능)
            int rowIndex = (stageNumber - 1) / stagesPerRow;
            float normalizedPosition = 1f - ((float)rowIndex / (totalStages / stagesPerRow));
            
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
        }
        
        // ========================================
        // BaseUIPanel 오버라이드
        // ========================================
        
        public override void Show(bool animated = true)
        {
            base.Show(animated);
            
            // 패널이 표시될 때마다 UI 업데이트
            UpdateUI();
            
            // 서버에서 최신 진행도 정보 요청 (선택사항)
            RequestLatestProgress();
        }
        
        /// <summary>
        /// 서버에서 최신 진행도 정보 요청
        /// </summary>
        private void RequestLatestProgress()
        {
            if (progressManager != null)
            {
                // 현재 표시된 스테이지 범위의 진행도 일괄 요청
                int endStage = Mathf.Min(totalStages, 50); // 처음 50개 스테이지만 요청
                progressManager.RequestBatchStageProgressFromServer(1, endStage);
                
                Debug.Log($"서버에서 스테이지 1-{endStage} 진행도 요청");
            }
        }
        
        // ========================================
        // 디버그 및 개발용 함수들
        // ========================================
        
        /// <summary>
        /// 모든 스테이지 언락 (개발용)
        /// </summary>
        [ContextMenu("Unlock All Stages")]
        public void UnlockAllStages()
        {
            if (progressManager != null)
            {
                progressManager.SetMaxStageCompleted(totalStages);
                UpdateUI();
                Debug.Log("모든 스테이지 언락됨 (개발용)");
            }
        }
        
        /// <summary>
        /// 진행도 초기화 (개발용)
        /// </summary>
        [ContextMenu("Reset Progress")]
        public void ResetProgress()
        {
            if (progressManager != null)
            {
                progressManager.SetMaxStageCompleted(0);
                UpdateUI();
                Debug.Log("진행도 초기화됨 (개발용)");
            }
        }
    }
}