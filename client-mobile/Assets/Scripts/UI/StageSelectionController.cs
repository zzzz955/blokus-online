using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlokusUnity.Network;
using BlokusUnity.Data;
using NetworkUserStageProgress = BlokusUnity.Network.UserStageProgress;
using GameUserStageProgress = BlokusUnity.Game.UserStageProgress;

namespace BlokusUnity.UI
{
    /// <summary>
    /// 스테이지 선택 UI 컨트롤러
    /// API 기반 스테이지 메타데이터를 로드하고 표시
    /// </summary>
    public class StageSelectionController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform stageButtonContainer;
        [SerializeField] private GameObject stageButtonPrefab;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Text statusText;
        [SerializeField] private ScrollRect scrollRect;
        
        [Header("Settings")]
        [SerializeField] private int maxVisibleStages = 50;
        
        // 스테이지 버튼들
        private List<StageButton> stageButtons = new List<StageButton>();
        private bool isLoading = false;
        
        // 이벤트
        public event System.Action<int> OnStageSelected;
        
        void Start()
        {
            InitializeUI();
            LoadStageData();
        }
        
        void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // 새로고침 버튼 이벤트
            if (refreshButton != null)
            {
                refreshButton.onClick.AddListener(OnRefreshButtonClicked);
            }
            
            // 상태 텍스트 초기값
            if (statusText != null)
            {
                statusText.text = "스테이지 데이터를 로딩 중...";
            }
            
            // StageDataIntegrator 이벤트 구독
            SubscribeToEvents();
        }
        
        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            if (StageDataIntegrator.Instance != null)
            {
                StageDataIntegrator.Instance.OnStageMetadataLoaded += HandleStageMetadataLoaded;
                StageDataIntegrator.Instance.OnLoadingError += HandleLoadingError;
            }
        }
        
        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (StageDataIntegrator.Instance != null)
            {
                StageDataIntegrator.Instance.OnStageMetadataLoaded -= HandleStageMetadataLoaded;
                StageDataIntegrator.Instance.OnLoadingError -= HandleLoadingError;
            }
        }
        
        /// <summary>
        /// 스테이지 데이터 로딩 시작
        /// </summary>
        public void LoadStageData()
        {
            if (isLoading)
            {
                Debug.LogWarning("[StageSelection] 이미 로딩 중입니다.");
                return;
            }
            
            isLoading = true;
            
            if (statusText != null)
            {
                statusText.text = "스테이지 목록을 불러오는 중...";
            }
            
            // 새로고침 버튼 비활성화
            if (refreshButton != null)
            {
                refreshButton.interactable = false;
            }
            
            // StageDataIntegrator를 통한 메타데이터 로딩
            if (StageDataIntegrator.Instance != null)
            {
                StageDataIntegrator.Instance.LoadStageMetadata();
            }
            else
            {
                Debug.LogError("[StageSelection] StageDataIntegrator가 없습니다!");
                HandleLoadingError("StageDataIntegrator를 찾을 수 없습니다.");
            }
        }
        
        /// <summary>
        /// 스테이지 메타데이터 로딩 완료 처리
        /// </summary>
        private void HandleStageMetadataLoaded(HttpApiClient.CompactStageMetadata[] metadata)
        {
            Debug.Log($"[StageSelection] 스테이지 메타데이터 로드 완료: {metadata.Length}개");
            
            isLoading = false;
            
            // 새로고침 버튼 활성화
            if (refreshButton != null)
            {
                refreshButton.interactable = true;
            }
            
            // UI 업데이트
            CreateStageButtons(metadata);
            
            if (statusText != null)
            {
                statusText.text = $"{metadata.Length}개의 스테이지를 불러왔습니다.";
            }
        }
        
        /// <summary>
        /// 로딩 에러 처리
        /// </summary>
        private void HandleLoadingError(string errorMessage)
        {
            Debug.LogError($"[StageSelection] 스테이지 메타데이터 로딩 실패: {errorMessage}");
            
            isLoading = false;
            
            // 새로고침 버튼 활성화
            if (refreshButton != null)
            {
                refreshButton.interactable = true;
            }
            
            if (statusText != null)
            {
                statusText.text = $"로딩 실패: {errorMessage}";
            }
        }
        
        /// <summary>
        /// 스테이지 버튼들 생성
        /// </summary>
        private void CreateStageButtons(HttpApiClient.CompactStageMetadata[] metadata)
        {
            // 기존 버튼들 제거
            ClearStageButtons();
            
            // 표시할 스테이지 수 제한
            int stageCount = Mathf.Min(metadata.Length, maxVisibleStages);
            
            for (int i = 0; i < stageCount; i++)
            {
                var stageInfo = metadata[i];
                CreateStageButton(stageInfo, i);
            }
            
            // 스크롤을 맨 위로
            if (scrollRect != null)
            {
                scrollRect.normalizedPosition = new Vector2(0, 1);
            }
        }
        
        /// <summary>
        /// 개별 스테이지 버튼 생성
        /// </summary>
        private void CreateStageButton(HttpApiClient.CompactStageMetadata stageInfo, int index)
        {
            if (stageButtonPrefab == null || stageButtonContainer == null)
            {
                Debug.LogError("[StageSelection] 스테이지 버튼 프리팹 또는 컨테이너가 설정되지 않았습니다!");
                return;
            }
            
            // 버튼 오브젝트 생성
            GameObject buttonObj = Instantiate(stageButtonPrefab, stageButtonContainer);
            StageButton stageButton = buttonObj.GetComponent<StageButton>();
            
            if (stageButton == null)
            {
                Debug.LogError("[StageSelection] 스테이지 버튼 프리팹에 StageButton 컴포넌트가 없습니다!");
                Destroy(buttonObj);
                return;
            }
            
            // 언락 상태 확인
            bool isUnlocked = StageDataIntegrator.Instance?.IsStageUnlocked(stageInfo.n) ?? (stageInfo.n == 1);
            
            // 사용자 진행도 정보 가져오기 (캐시에서)
            var networkProgress = UserDataCache.Instance?.GetStageProgress(stageInfo.n);
            var gameProgress = ConvertToGameUserProgress(networkProgress);
            
            // StageButton 초기화 (별도 파일의 StageButton 인터페이스 사용)
            stageButton.Initialize(stageInfo.n, HandleStageButtonClicked);
            
            // 상태 업데이트
            stageButton.UpdateState(isUnlocked, gameProgress);
            
            stageButtons.Add(stageButton);
        }
        
        /// <summary>
        /// 기존 스테이지 버튼들 제거
        /// </summary>
        private void ClearStageButtons()
        {
            foreach (var button in stageButtons)
            {
                if (button != null && button.gameObject != null)
                {
                    Destroy(button.gameObject);
                }
            }
            stageButtons.Clear();
        }
        
        /// <summary>
        /// 스테이지 버튼 클릭 처리 (별도 파일의 StageButton 콜백)
        /// </summary>
        private void HandleStageButtonClicked(int stageNumber)
        {
            // 언락 상태는 StageButton에서 이미 확인함
            Debug.Log($"[StageSelection] 스테이지 {stageNumber} 선택됨");
            
            // 선택된 스테이지 번호를 PlayerPrefs에 저장 (부트스트랩에서 사용)
            PlayerPrefs.SetInt("LastPlayedStage", stageNumber);
            PlayerPrefs.Save();
            
            // 이벤트 발생
            OnStageSelected?.Invoke(stageNumber);
        }
        
        /// <summary>
        /// 새로고침 버튼 클릭 처리
        /// </summary>
        private void OnRefreshButtonClicked()
        {
            Debug.Log("[StageSelection] 새로고침 버튼 클릭");
            LoadStageData();
        }
        
        /// <summary>
        /// 스테이지 진행도 업데이트 (다른 UI에서 호출 가능)
        /// </summary>
        public void RefreshStageProgress(int stageNumber)
        {
            var stageButton = stageButtons.Find(btn => btn.StageNumber == stageNumber);
            if (stageButton != null)
            {
                // 언락 상태 확인
                bool isUnlocked = StageDataIntegrator.Instance?.IsStageUnlocked(stageNumber) ?? (stageNumber == 1);
                
                // 캐시에서 최신 진행도 가져와서 업데이트
                var networkProgress = UserDataCache.Instance?.GetStageProgress(stageNumber);
                var gameProgress = ConvertToGameUserProgress(networkProgress);
                
                // 상태 업데이트 (별도 파일의 StageButton 인터페이스 사용)
                stageButton.UpdateState(isUnlocked, gameProgress);
            }
        }
        
        /// <summary>
        /// 네트워크 진행도를 게임 진행도로 변환
        /// </summary>
        private GameUserStageProgress ConvertToGameUserProgress(NetworkUserStageProgress networkProgress)
        {
            if (networkProgress == null) return null;
            
            return new GameUserStageProgress
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
    }
}