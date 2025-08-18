using System;
using System.Collections.Generic;
using UnityEngine;
using BlokusUnity.Data;

namespace BlokusUnity.Game
{
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
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                
                // 루트 GameObject로 이동 (DontDestroyOnLoad 적용을 위해)
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                
                stageProgressCache = new Dictionary<int, UserStageProgress>();
                InitializeNetworkEvents();
                Debug.Log("StageProgressManager 초기화 완료 - DontDestroyOnLoad 적용됨");
            }
            else
            {
                Destroy(gameObject);
            }
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
            if (BlokusUnity.Network.HttpApiClient.Instance != null)
            {
                var httpClient = BlokusUnity.Network.HttpApiClient.Instance;
                httpClient.OnStageProgressReceived += OnHttpStageProgressReceived;
                httpClient.OnStageCompleteResponse += OnHttpStageCompleteResponse;
                
                Debug.Log("HTTP API 이벤트 구독 완료");
            }
        }
        
        void OnDestroy()
        {
            // HTTP API 이벤트 구독 해제
            if (BlokusUnity.Network.HttpApiClient.Instance != null)
            {
                var httpClient = BlokusUnity.Network.HttpApiClient.Instance;
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
        public float GetOverallProgress(int totalStages = 1000)
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
        private void OnHttpStageProgressReceived(BlokusUnity.Network.UserStageProgress networkProgress)
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
            if (completed && BlokusUnity.Network.HttpApiClient.Instance != null)
            {
                // HTTP API를 통한 스테이지 완료 보고
                BlokusUnity.Network.HttpApiClient.Instance.CompleteStage(stageNumber, score, (int)time, completed);
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
            if (BlokusUnity.Network.HttpApiClient.Instance != null && BlokusUnity.Network.HttpApiClient.Instance.IsAuthenticated())
            {
                BlokusUnity.Network.HttpApiClient.Instance.GetStageProgress(stageNumber);
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
            if (BlokusUnity.Network.HttpApiClient.Instance != null && 
                BlokusUnity.Network.HttpApiClient.Instance.IsAuthenticated())
            {
                Debug.Log($"[StageProgressManager] HTTP API로 진행도 일괄 요청: {startStage}-{endStage}");
                BlokusUnity.Network.HttpApiClient.Instance.GetBatchProgress();
                return;
            }
            
            // 3. 아무 연결도 없을 때 - 로컬 기본값 사용
            Debug.Log($"[StageProgressManager] 네트워크 연결 없음 - 로컬 기본 진행도 사용 ({startStage}-{endStage})");
            
            // 기본 진행도 생성하여 캐시에 저장
            if (BlokusUnity.Data.UserDataCache.Instance != null)
            {
                for (int stageNum = startStage; stageNum <= endStage; stageNum++)
                {
                    var defaultProgress = new BlokusUnity.Network.UserStageProgress
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
                    var existingProgress = BlokusUnity.Data.UserDataCache.Instance.GetStageProgress(stageNum);
                    if (existingProgress.totalAttempts == 0) // 새로운 스테이지
                    {
                        BlokusUnity.Data.UserDataCache.Instance.SetStageProgress(defaultProgress);
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