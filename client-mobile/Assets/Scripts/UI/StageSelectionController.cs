using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using BlokusUnity.Network;
using BlokusUnity.Data;
using BlokusUnity.Common;
using NetworkUserStageProgress = BlokusUnity.Network.UserStageProgress;
using GameUserStageProgress = BlokusUnity.Game.UserStageProgress;
using UserInfo = BlokusUnity.Common.UserInfo;

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
        
        // 🔥 추가: 중복 새로고침 방지
        private bool isRefreshing = false;
        
        // 이벤트
        public event System.Action<int> OnStageSelected;
        
        void Start()
        {
            Debug.Log("[StageSelection] Start() 호출됨");
            InitializeUI();
            
            // UserDataCache 인스턴스가 준비될 때까지 대기 후 이벤트 재구독
            StartCoroutine(RetryEventSubscription());
            
            LoadStageData();
        }
        
        /// <summary>
        /// UserDataCache 준비 대기 및 이벤트 재구독
        /// </summary>
        private System.Collections.IEnumerator RetryEventSubscription()
        {
            int retryCount = 0;
            const int maxRetries = 10;
            
            while (retryCount < maxRetries)
            {
                if (UserDataCache.Instance != null)
                {
                    Debug.Log("[StageSelection] UserDataCache 준비됨 - 이벤트 재구독");
                    
                    // 기존 구독 해제 후 재구독
                    UserDataCache.Instance.OnStageProgressUpdated -= HandleStageProgressUpdated;
                    UserDataCache.Instance.OnStageProgressUpdated += HandleStageProgressUpdated;
                    
                    // 🔥 추가: 사용자 프로필 업데이트 이벤트 재구독
                    UserDataCache.Instance.OnUserDataUpdated -= HandleUserDataUpdated;
                    UserDataCache.Instance.OnUserDataUpdated += HandleUserDataUpdated;
                    
                    Debug.Log("[StageSelection] ✅ 이벤트 재구독 완료 (진행도 + 프로필)");
                    
                    // 🔥 핵심 수정: 이벤트 구독 후 기존 캐시 데이터를 즉시 적용
                    RefreshAllButtonsFromCache();
                    
                    yield break;
                }
                
                retryCount++;
                Debug.Log($"[StageSelection] UserDataCache 대기 중... ({retryCount}/{maxRetries})");
                yield return new WaitForSeconds(0.1f);
            }
            
            Debug.LogWarning("[StageSelection] UserDataCache 초기화 실패 - 이벤트 구독 불가");
        }
        
        /// <summary>
        /// 모든 스테이지 버튼을 캐시 데이터로 새로고침
        /// </summary>
        private void RefreshAllButtonsFromCache()
        {
            // 🔥 추가: 중복 새로고침 방지
            if (isRefreshing)
            {
                Debug.Log("[StageSelection] RefreshAllButtonsFromCache 중복 방지 - 이미 새로고침 중");
                return;
            }
            
            if (UserDataCache.Instance == null || stageButtons.Count == 0)
            {
                Debug.Log("[StageSelection] RefreshAllButtonsFromCache 건너뜀 - UserDataCache 또는 버튼이 없음");
                return;
            }
            
            isRefreshing = true;
            Debug.Log($"[StageSelection] 🔄 RefreshAllButtonsFromCache 시작 - {stageButtons.Count}개 버튼 업데이트");
            
            int updatedCount = 0;
            
            foreach (var stageButton in stageButtons)
            {
                if (stageButton != null)
                {
                    int stageNumber = stageButton.StageNumber;
                    
                    // 캐시에서 진행도 가져오기
                    var networkProgress = UserDataCache.Instance.GetStageProgress(stageNumber);
                    
                    // 언락 상태 확인
                    bool isUnlocked = StageDataIntegrator.Instance?.IsStageUnlocked(stageNumber) ?? (stageNumber == 1);
                    
                    // 진행도 변환 및 적용
                    var gameProgress = ConvertToGameUserProgress(networkProgress);
                    stageButton.UpdateState(isUnlocked, gameProgress);
                    
                    updatedCount++;
                }
            }
            
            Debug.Log($"[StageSelection] ✅ RefreshAllButtonsFromCache 완료 - {updatedCount}개 버튼 업데이트됨");
            isRefreshing = false;
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
            
            // UserDataCache 진행도 업데이트 이벤트 구독 (캐시 데이터 실시간 반영)
            if (UserDataCache.Instance != null)
            {
                UserDataCache.Instance.OnStageProgressUpdated += HandleStageProgressUpdated;
                
                // 🔥 추가: 사용자 프로필 업데이트 이벤트 구독 (max_stage_completed 변경시 스테이지 버튼 새로고침)
                UserDataCache.Instance.OnUserDataUpdated += HandleUserDataUpdated;
                
                Debug.Log("[StageSelection] UserDataCache 이벤트 구독 완료 (진행도 + 프로필)");
            }
            else
            {
                Debug.LogWarning("[StageSelection] UserDataCache.Instance가 null이어서 이벤트 구독 실패");
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
            
            // UserDataCache 이벤트 구독 해제
            if (UserDataCache.Instance != null)
            {
                UserDataCache.Instance.OnStageProgressUpdated -= HandleStageProgressUpdated;
                
                // 🔥 추가: 사용자 프로필 업데이트 이벤트 구독 해제
                UserDataCache.Instance.OnUserDataUpdated -= HandleUserDataUpdated;
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
            
            // 🔥 추가: 버튼 생성 후 캐시 데이터로 즉시 업데이트
            if (UserDataCache.Instance != null)
            {
                Debug.Log("[StageSelection] 버튼 생성 후 캐시 데이터 즉시 적용");
                RefreshAllButtonsFromCache();
            }
            
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
            Debug.Log($"[StageSelection] 스테이지 {stageInfo.n} 진행도 요청 중...");
            var networkProgress = UserDataCache.Instance?.GetStageProgress(stageInfo.n);
            Debug.Log($"[StageSelection] 스테이지 {stageInfo.n} 캐시 결과: {(networkProgress != null ? $"완료={networkProgress.isCompleted}, 별={networkProgress.starsEarned}" : "null")}");
            
            var gameProgress = ConvertToGameUserProgress(networkProgress);
            Debug.Log($"[StageSelection] 스테이지 {stageInfo.n} 변환 결과: {(gameProgress != null ? $"완료={gameProgress.isCompleted}, 별={gameProgress.starsEarned}" : "null")}");
            
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
        /// UserDataCache에서 스테이지 진행도 업데이트 이벤트 처리
        /// </summary>
        private void HandleStageProgressUpdated(NetworkUserStageProgress progress)
        {
            Debug.Log($"[StageSelection] ✅ HandleStageProgressUpdated 호출됨! 스테이지 {progress.stageNumber} 진행도: 완료={progress.isCompleted}, 별={progress.starsEarned}");
            
            // 해당 스테이지 버튼 찾아서 업데이트
            Debug.Log($"[StageSelection] 스테이지 버튼 검색 중... 현재 버튼 수: {stageButtons.Count}개");
            var stageButton = stageButtons.Find(btn => btn.StageNumber == progress.stageNumber);
            if (stageButton != null)
            {
                Debug.Log($"[StageSelection] 스테이지 {progress.stageNumber} 버튼 찾음!");
                
                // 언락 상태 확인
                bool isUnlocked = StageDataIntegrator.Instance?.IsStageUnlocked(progress.stageNumber) ?? (progress.stageNumber == 1);
                
                // 네트워크 진행도를 게임 진행도로 변환
                var gameProgress = ConvertToGameUserProgress(progress);
                
                // 상태 업데이트
                stageButton.UpdateState(isUnlocked, gameProgress);
                
                Debug.Log($"[StageSelection] ✅ 스테이지 {progress.stageNumber} 버튼 상태 업데이트 완료");
            }
            else
            {
                Debug.LogWarning($"[StageSelection] ❌ 스테이지 {progress.stageNumber} 버튼을 찾을 수 없음 (총 {stageButtons.Count}개 버튼)");
                
                // 디버깅을 위해 현재 버튼들의 스테이지 번호 출력
                if (stageButtons.Count > 0)
                {
                    var buttonNumbers = string.Join(", ", stageButtons.Select(btn => btn.StageNumber.ToString()).Take(10));
                    Debug.Log($"[StageSelection] 현재 버튼들: {buttonNumbers}{(stageButtons.Count > 10 ? "..." : "")}");
                }
            }
        }
        
        /// <summary>
        /// 🔥 추가: UserDataCache에서 사용자 프로필 업데이트 이벤트 처리 (max_stage_completed 변경시)
        /// </summary>
        private void HandleUserDataUpdated(UserInfo userInfo)
        {
            if (userInfo != null)
            {
                Debug.Log($"[StageSelection] ✅ HandleUserDataUpdated 호출됨! 사용자: {userInfo.username}, max_stage_completed: {userInfo.maxStageCompleted}");
                
                // 모든 스테이지 버튼의 언락 상태를 새로고침 (max_stage_completed 기준)
                Debug.Log($"[StageSelection] 🔄 사용자 프로필 업데이트로 인한 전체 스테이지 버튼 새로고침 시작");
                RefreshAllButtonsFromCache();
                
                Debug.Log($"[StageSelection] ✅ 사용자 프로필 업데이트 처리 완료");
            }
            else
            {
                Debug.LogWarning($"[StageSelection] ❌ HandleUserDataUpdated - userInfo가 null입니다");
            }
        }
        
        /// <summary>
        /// 네트워크 진행도를 게임 진행도로 변환
        /// </summary>
        private GameUserStageProgress ConvertToGameUserProgress(NetworkUserStageProgress networkProgress)
        {
            if (networkProgress == null) 
            {
                Debug.Log("[StageSelection] NetworkUserStageProgress가 null - 데이터 아직 로드되지 않음");
                return null;
            }
            
            var gameProgress = new GameUserStageProgress
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
            
            Debug.Log($"[StageSelection] 스테이지 {gameProgress.stageNumber} 진행도 변환: 완료={gameProgress.isCompleted}, 별={gameProgress.starsEarned}");
            return gameProgress;
        }
    }
}