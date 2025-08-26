using System;
using System.Collections.Generic;
using UnityEngine;
using App.Network;
using Features.Multi.Net;
namespace Features.Single.Core{
    /// <summary>
    /// 스테이지 진행도 및 언락 시스템 관리
    /// 별점 계산 및 언락 조건 검증을 클라이언트에서 처리
    /// </summary>
    public class StageProgressManager : MonoBehaviour
    {
        [Header("별점 시스템 설정")]
        [SerializeField, Range(0f, 1f)] private float threeStarThreshold = 0.9f; // 90% 이상
        [SerializeField, Range(0f, 1f)] private float twoStarThreshold = 0.7f;   // 70% 이상
        [SerializeField, Range(0f, 1f)] private float oneStarThreshold = 0.5f;   // 50% 이상
        
        // 현재 유저의 최대 클리어 스테이지 (서버에서 받아옴)
        private int maxStageCompleted = 0;
        
        // 캐싱된 스테이지 진행도 (user_id별로 관리)
        private Dictionary<int, UserStageProgress> stageProgressCache;
        
        // 싱글톤 패턴
        public static StageProgressManager Instance { get; private set; }
        
        // Migration Plan: Initialization state and dependencies
        private bool isInitialized = false;
        private UserDataCache userDataCache;
        private StageDataManager stageDataManager;

        void Awake()
        {
            // Migration Plan: Remove DontDestroyOnLoad - SingleCore scene management
            if (Instance == null)
            {
                Instance = this;
                stageProgressCache = new Dictionary<int, UserStageProgress>();
                Debug.Log("[StageProgressManager] Awake - Ready for initialization");
            }
            else
            {
                Debug.Log("[StageProgressManager] Duplicate instance destroyed");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Initialize StageProgressManager (Migration Plan)
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;
            
            InitializeNetworkEvents();
            isInitialized = true;
            
            Debug.Log("[StageProgressManager] Initialized for SingleCore");
        }

        /// <summary>
        /// Set UserDataCache dependency (Migration Plan)
        /// </summary>
        public void SetUserDataCache(UserDataCache cache)
        {
            userDataCache = cache;
            Debug.Log("[StageProgressManager] UserDataCache dependency set");
        }

        /// <summary>
        /// Set StageDataManager dependency (Migration Plan)
        /// </summary>
        public void SetStageDataManager(StageDataManager manager)
        {
            stageDataManager = manager;
            Debug.Log("[StageProgressManager] StageDataManager dependency set");
        }

        /// <summary>
        /// Check if initialized (Migration Plan)
        /// </summary>
        public bool IsInitialized => isInitialized;

        /// <summary>
        /// Cleanup for scene unload (Migration Plan)
        /// </summary>
        public void Cleanup()
        {
            CleanupNetworkEvents();
            isInitialized = false;
            
            Debug.Log("[StageProgressManager] Cleaned up for scene unload");
        }

        private void CleanupNetworkEvents()
        {
            // Cleanup network event subscriptions
            // Implementation depends on existing network event structure
        }
        
        /// <summary>
        /// 네트워크 이벤트 초기화 및 구독
        /// </summary>
        private void InitializeNetworkEvents()
        {
            // MessageHandler 이벤트 구독 (지연 구독 패턴)
            DelayedNetworkSubscription();
        }
        
        /// <summary>
        /// MessageHandler가 준비될 때까지 기다린 후 이벤트 구독
        /// </summary>
        private void DelayedNetworkSubscription()
        {
            
            // HTTP API 클라이언트 이벤트 구독
            if (App.Network.HttpApiClient.Instance != null)
            {
                var httpClient = App.Network.HttpApiClient.Instance;
                httpClient.OnStageProgressReceived += OnHttpStageProgressReceived;
                httpClient.OnStageCompleteResponse += OnHttpStageCompleteResponse;
                
                Debug.Log("HTTP API 이벤트 구독 완료");
            }
        }
        
        void OnDestroy()
        {
            // HTTP API 이벤트 구독 해제
            if (App.Network.HttpApiClient.Instance != null)
            {
                var httpClient = App.Network.HttpApiClient.Instance;
                httpClient.OnStageProgressReceived -= OnHttpStageProgressReceived;
                httpClient.OnStageCompleteResponse -= OnHttpStageCompleteResponse;
            }
        }
        
        // ========================================
        // 별점 계산 시스템
        // ========================================
        
        /// <summary>
        /// 플레이어 점수를 기반으로 별점 계산
        /// </summary>
        /// <param name="playerScore">플레이어 획득 점수</param>
        /// <param name="optimalScore">스테이지 이론상 최대 점수</param>
        /// <returns>별 개수 (0-3)</returns>
        public int CalculateStars(int playerScore, int optimalScore)
        {
            if (optimalScore <= 0 || playerScore <= 0)
                return 0;
            
            float scoreRatio = (float)playerScore / optimalScore;
            
            if (scoreRatio >= threeStarThreshold)
                return 3;
            else if (scoreRatio >= twoStarThreshold)
                return 2;
            else if (scoreRatio >= oneStarThreshold)
                return 1;
            else
                return 0; // 실패
        }
        
        /// <summary>
        /// 별점별 필요 점수 계산
        /// </summary>
        public StarThresholds GetStarThresholds(int optimalScore)
        {
            return new StarThresholds
            {
                threeStar = Mathf.CeilToInt(optimalScore * threeStarThreshold),
                twoStar = Mathf.CeilToInt(optimalScore * twoStarThreshold),
                oneStar = Mathf.CeilToInt(optimalScore * oneStarThreshold)
            };
        }
        
        // ========================================
        // 언락 시스템
        // ========================================
        
        /// <summary>
        /// 스테이지가 언락되었는지 확인
        /// 규칙: 이전 스테이지를 클리어해야 다음 스테이지 언락
        /// </summary>
        /// <param name="stageNumber">확인할 스테이지 번호</param>
        /// <returns>언락 여부</returns>
        public bool IsStageUnlocked(int stageNumber)
        {
            // 1번 스테이지는 항상 언락
            if (stageNumber <= 1)
                return true;
            
            // 이전 스테이지가 클리어되어야 언락
            return maxStageCompleted >= (stageNumber - 1);
        }
        
        /// <summary>
        /// 현재 언락된 최대 스테이지 반환
        /// </summary>
        public int GetMaxUnlockedStage()
        {
            return maxStageCompleted + 1; // 다음 플레이 가능한 스테이지
        }
        
        /// <summary>
        /// 전체 진행률 계산 (UI 표시용)
        /// </summary>
        /// <param name="totalStages">총 스테이지 수</param>
        public float GetOverallProgress(int totalStages = 14) // 실제 구현된 스테이지 개수
        {
            return (float)maxStageCompleted / totalStages * 100f;
        }
        
        // ========================================
        // 스테이지 진행도 관리
        // ========================================
        
        /// <summary>
        /// 서버에서 유저의 최대 클리어 스테이지 설정
        /// </summary>
        public void SetMaxStageCompleted(int maxStage)
        {
            maxStageCompleted = maxStage;
            Debug.Log($"최대 클리어 스테이지: {maxStageCompleted}");
        }
        
        /// <summary>
        /// 특정 스테이지 진행도 캐싱
        /// </summary>
        public void CacheStageProgress(int stageNumber, UserStageProgress progress)
        {
            stageProgressCache[stageNumber] = progress;
        }
        
        /// <summary>
        /// 캐싱된 스테이지 진행도 반환
        /// </summary>
        public UserStageProgress GetCachedStageProgress(int stageNumber)
        {
            return stageProgressCache.TryGetValue(stageNumber, out var progress) ? progress : null;
        }
        
        // ========================================
        // 네트워크 이벤트 핸들러들
        // ========================================
        
        /// <summary>
        /// HTTP API에서 스테이지 진행도 수신 처리
        /// </summary>
        private void OnHttpStageProgressReceived(App.Network.UserStageProgress networkProgress)
        {
            // 네트워크 데이터를 로컬 데이터로 변환
            var localProgress = new UserStageProgress
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
            
            // 캐시에 저장
            CacheStageProgress(networkProgress.stageNumber, localProgress);
            
            Debug.Log($"서버에서 스테이지 {networkProgress.stageNumber} 진행도 수신됨");
        }
        
        // OnNetworkMaxStageUpdated 제거됨 - HTTP API에서 별도 제공하지 않음
        
        /// <summary>
        /// HTTP API에서 스테이지 완료 응답 수신 처리
        /// </summary>
        private void OnHttpStageCompleteResponse(bool success, string message)
        {
            if (success)
            {
                Debug.Log($"서버 스테이지 완료 처리 성공: {message}");
            }
            else
            {
                Debug.LogWarning($"서버 스테이지 완료 처리 실패: {message}");
            }
        }
        
        // ========================================
        // 서버 통신
        // ========================================
        
        /// <summary>
        /// 서버에 스테이지 진행도 업데이트 (HTTP API 사용)
        /// </summary>
        private void UpdateStageProgressOnServer(int stageNumber, bool completed, int stars, int score, float time)
        {
            if (completed && App.Network.HttpApiClient.Instance != null)
            {
                // HTTP API를 통한 스테이지 완료 보고
                App.Network.HttpApiClient.Instance.CompleteStage(stageNumber, score, (int)time, completed);
            }
            else if (!completed)
            {
                Debug.Log($"스테이지 {stageNumber} 실패 - 서버 전송 안함 (로컬에만 저장)");
            }
            else
            {
                Debug.LogWarning("HttpApiClient가 초기화되지 않음 - 오프라인 모드");
            }
        }
        
        /// <summary>
        /// 서버에서 스테이지 진행도 요청
        /// </summary>
        public void RequestStageProgressFromServer(int stageNumber)
        {
            if (App.Network.HttpApiClient.Instance != null && App.Network.HttpApiClient.Instance.IsAuthenticated())
            {
                App.Network.HttpApiClient.Instance.GetStageProgress(stageNumber);
                Debug.Log($"HTTP API를 통해 스테이지 {stageNumber} 진행도 요청");
            }
            else
            {
                Debug.LogWarning($"HTTP API 클라이언트가 인증되지 않아 스테이지 {stageNumber} 진행도 요청 실패");
            }
        }
        
        /// <summary>
        /// 서버에서 여러 스테이지 진행도 일괄 요청
        /// </summary>
        public void RequestBatchStageProgressFromServer(int startStage, int endStage)
        {
            // 하이브리드 네트워크: 싱글플레이어는 HTTP API, 멀티플레이어는 TCP 사용
            
            // 1. HTTP API 클라이언트 우선 시도 (싱글플레이어)
            if (App.Network.HttpApiClient.Instance != null && 
                App.Network.HttpApiClient.Instance.IsAuthenticated())
            {
                Debug.Log($"[StageProgressManager] HTTP API로 진행도 일괄 요청: {startStage}-{endStage}");
                App.Network.HttpApiClient.Instance.GetBatchProgress();
                return;
            }
            
            // 3. 아무 연결도 없을 때 - 로컬 기본값 사용
            Debug.Log($"[StageProgressManager] 네트워크 연결 없음 - 로컬 기본 진행도 사용 ({startStage}-{endStage})");
            
            // 기본 진행도 생성하여 캐시에 저장
            if (Features.Single.Core.UserDataCache.Instance != null)
            {
                for (int stageNum = startStage; stageNum <= endStage; stageNum++)
                {
                    var defaultProgress = new App.Network.UserStageProgress
                    {
                        stageNumber = stageNum,
                        isCompleted = false,
                        starsEarned = 0,
                        bestScore = 0,
                        bestCompletionTime = 0,
                        totalAttempts = 0,
                        successfulAttempts = 0,
                        lastPlayedAt = System.DateTime.Now
                    };
                    
                    // 기존 진행도가 없을 때만 기본값 설정
                    var existingProgress = Features.Single.Core.UserDataCache.Instance.GetStageProgress(stageNum);
                    if (existingProgress.totalAttempts == 0) // 새로운 스테이지
                    {
                        Features.Single.Core.UserDataCache.Instance.SetStageProgress(defaultProgress);
                    }
                }
                
                Debug.Log($"[StageProgressManager] 로컬 기본 진행도 생성 완료: {startStage}-{endStage}");
            }
        }
        
        /// <summary>
        /// 총 획득 별 개수 계산 (전체 진행도 표시용)
        /// </summary>
        public int GetTotalStarsEarned()
        {
            int totalStars = 0;
            foreach (var progress in stageProgressCache.Values)
            {
                if (progress.isCompleted)
                {
                    totalStars += progress.starsEarned;
                }
            }
            return totalStars;
        }

        /// <summary>
        /// 캐시된 진행도 데이터 완전 정리 (로그아웃 시 호출)
        /// </summary>
        public void ClearCache()
        {
            Debug.Log("[StageProgressManager] 캐시 데이터 정리 시작");
            
            // 진행도 캐시 완전 정리
            if (stageProgressCache != null)
            {
                stageProgressCache.Clear();
                Debug.Log("[StageProgressManager] 진행도 캐시 정리 완료");
            }
            
            // 최대 스테이지 초기화
            maxStageCompleted = 0;
            
            Debug.Log("[StageProgressManager] 캐시 데이터 정리 완료");
        }
    }
    
    // ========================================
    // 데이터 구조체들
    // ========================================
    
    /// <summary>
    /// 별점별 필요 점수 정보
    /// </summary>
    [System.Serializable]
    public struct StarThresholds
    {
        public int threeStar;  // 3별 필요 점수
        public int twoStar;    // 2별 필요 점수  
        public int oneStar;    // 1별 필요 점수
    }
    
    /// <summary>
    /// 사용자 스테이지 진행도 (캐싱용)
    /// </summary>
    [System.Serializable]
    public class UserStageProgress
    {
        public int stageNumber;
        public bool isCompleted;
        public int starsEarned;
        public int bestScore;
        public int bestCompletionTime; // 초 단위
        public int totalAttempts;
        public int successfulAttempts;
        public DateTime firstPlayedAt;
        public DateTime lastPlayedAt;
    }
}