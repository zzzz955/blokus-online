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
                DontDestroyOnLoad(gameObject);
                stageProgressCache = new Dictionary<int, UserStageProgress>();
                InitializeNetworkEvents();
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
            StartCoroutine(DelayedNetworkSubscription());
        }
        
        /// <summary>
        /// MessageHandler가 준비될 때까지 기다린 후 이벤트 구독
        /// </summary>
        private System.Collections.IEnumerator DelayedNetworkSubscription()
        {
            // MessageHandler가 초기화될 때까지 대기
            while (BlokusUnity.Network.MessageHandler.Instance == null)
            {
                yield return new WaitForSeconds(0.1f);
            }
            
            var messageHandler = BlokusUnity.Network.MessageHandler.Instance;
            
            // 싱글플레이어 메시지 이벤트 구독
            messageHandler.OnStageProgressReceived += OnNetworkStageProgressReceived;
            messageHandler.OnMaxStageUpdated += OnNetworkMaxStageUpdated;
            messageHandler.OnStageCompleteResponse += OnNetworkStageCompleteResponse;
            
            Debug.Log("네트워크 이벤트 구독 완료");
        }
        
        void OnDestroy()
        {
            // 이벤트 구독 해제
            if (BlokusUnity.Network.MessageHandler.Instance != null)
            {
                var messageHandler = BlokusUnity.Network.MessageHandler.Instance;
                messageHandler.OnStageProgressReceived -= OnNetworkStageProgressReceived;
                messageHandler.OnMaxStageUpdated -= OnNetworkMaxStageUpdated;
                messageHandler.OnStageCompleteResponse -= OnNetworkStageCompleteResponse;
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
        /// 다음 스테이지 언락 (스테이지 클리어시 호출)
        /// </summary>
        /// <param name="completedStageNumber">방금 클리어한 스테이지</param>
        public void UnlockNextStage(int completedStageNumber)
        {
            if (completedStageNumber > maxStageCompleted)
            {
                maxStageCompleted = completedStageNumber;
                Debug.Log($"스테이지 {completedStageNumber + 1} 언락됨!");
                
                // 서버에 업데이트 전송 (user_stats.max_stage_completed)
                UpdateMaxStageCompletedOnServer(maxStageCompleted);
            }
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
        
        /// <summary>
        /// 스테이지 완료 처리 (점수, 별점, 시간 기록)
        /// </summary>
        public void CompleteStage(StageData stageData, int playerScore, float completionTime)
        {
            int stars = CalculateStars(playerScore, stageData.optimalScore);
            
            // 실패한 경우 (0별)
            if (stars == 0)
            {
                Debug.Log($"스테이지 {stageData.stageNumber} 실패 (점수: {playerScore}/{stageData.optimalScore})");
                // 실패해도 기록은 남김 (시도 횟수 등)
                UpdateStageProgressOnServer(stageData.stageNumber, false, 0, playerScore, completionTime);
                return;
            }
            
            // 성공한 경우
            Debug.Log($"스테이지 {stageData.stageNumber} 클리어! (점수: {playerScore}, 별: {stars}개)");
            
            // 캐시 업데이트
            var progress = GetCachedStageProgress(stageData.stageNumber) ?? new UserStageProgress
            {
                stageNumber = stageData.stageNumber
            };
            
            bool isNewRecord = playerScore > progress.bestScore;
            bool isFirstClear = !progress.isCompleted;
            
            progress.isCompleted = true;
            progress.starsEarned = Mathf.Max(progress.starsEarned, stars); // 더 높은 별점으로 업데이트
            progress.bestScore = Mathf.Max(progress.bestScore, playerScore);
            progress.totalAttempts++;
            progress.successfulAttempts++;
            
            CacheStageProgress(stageData.stageNumber, progress);
            
            // 다음 스테이지 언락 (첫 클리어시에만)
            if (isFirstClear)
            {
                UnlockNextStage(stageData.stageNumber);
            }
            
            // 서버에 업데이트 전송
            UpdateStageProgressOnServer(stageData.stageNumber, true, stars, playerScore, completionTime);
            
            // UI 이벤트 발생
            OnStageCompleted?.Invoke(stageData.stageNumber, stars, isNewRecord, isFirstClear);
        }
        
        // ========================================
        // 네트워크 이벤트 핸들러들
        // ========================================
        
        /// <summary>
        /// 서버에서 스테이지 진행도 수신 처리
        /// </summary>
        private void OnNetworkStageProgressReceived(BlokusUnity.Network.UserStageProgress networkProgress)
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
        
        /// <summary>
        /// 서버에서 최대 스테이지 업데이트 수신 처리
        /// </summary>
        private void OnNetworkMaxStageUpdated(int newMaxStage)
        {
            int previousMax = maxStageCompleted;
            maxStageCompleted = newMaxStage;
            
            Debug.Log($"서버에서 최대 스테이지 업데이트: {previousMax} → {newMaxStage}");
            
            // 새로운 스테이지가 언락된 경우
            if (newMaxStage > previousMax)
            {
                OnStageUnlocked?.Invoke(newMaxStage + 1); // 다음 플레이 가능한 스테이지
            }
        }
        
        /// <summary>
        /// 서버에서 스테이지 완료 응답 수신 처리
        /// </summary>
        private void OnNetworkStageCompleteResponse(bool success, string message)
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
                BlokusUnity.Network.HttpApiClient.Instance.CompleteStage(stageNumber, stars, score, (int)time);
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
        /// 서버에 최대 클리어 스테이지 업데이트
        /// </summary>
        private void UpdateMaxStageCompletedOnServer(int maxStage)
        {
            if (BlokusUnity.Network.NetworkClient.Instance != null && BlokusUnity.Network.NetworkClient.Instance.IsConnected())
            {
                bool success = BlokusUnity.Network.NetworkClient.Instance.SendUpdateMaxStageRequest(maxStage);
                
                if (!success)
                {
                    Debug.LogWarning($"최대 스테이지 {maxStage} 서버 전송 실패");
                }
            }
            else
            {
                Debug.LogWarning("서버 연결되지 않음 - 최대 스테이지 로컬에만 저장");
            }
        }
        
        /// <summary>
        /// 서버에서 스테이지 진행도 요청
        /// </summary>
        public void RequestStageProgressFromServer(int stageNumber)
        {
            if (BlokusUnity.Network.NetworkClient.Instance != null && BlokusUnity.Network.NetworkClient.Instance.IsConnected())
            {
                bool success = BlokusUnity.Network.NetworkClient.Instance.SendStageProgressRequest(stageNumber);
                
                if (!success)
                {
                    Debug.LogWarning($"스테이지 {stageNumber} 진행도 요청 실패");
                }
            }
            else
            {
                Debug.LogWarning("서버 연결되지 않음 - 진행도 요청 불가");
            }
        }
        
        /// <summary>
        /// 서버에서 여러 스테이지 진행도 일괄 요청
        /// </summary>
        public void RequestBatchStageProgressFromServer(int startStage, int endStage)
        {
            if (BlokusUnity.Network.NetworkClient.Instance != null && BlokusUnity.Network.NetworkClient.Instance.IsConnected())
            {
                bool success = BlokusUnity.Network.NetworkClient.Instance.SendBatchStageProgressRequest(startStage, endStage);
                
                if (!success)
                {
                    Debug.LogWarning($"스테이지 {startStage}-{endStage} 일괄 진행도 요청 실패");
                }
            }
            else
            {
                Debug.LogWarning("서버 연결되지 않음 - 일괄 진행도 요청 불가");
            }
        }
        
        // ========================================
        // 이벤트 시스템
        // ========================================
        
        /// <summary>
        /// 스테이지 완료 이벤트 (stageNumber, stars, isNewRecord, isFirstClear)
        /// </summary>
        public event System.Action<int, int, bool, bool> OnStageCompleted;
        
        /// <summary>
        /// 새 스테이지 언락 이벤트 (unlockedStageNumber)
        /// </summary>
        public event System.Action<int> OnStageUnlocked;
        
        // ========================================
        // 유틸리티 함수들
        // ========================================
        
        /// <summary>
        /// 스테이지 난이도별 권장 별점 표시
        /// </summary>
        public string GetDifficultyText(int difficulty)
        {
            return difficulty switch
            {
                1 => "쉬움",
                2 => "보통", 
                3 => "어려움",
                4 => "매우 어려움",
                5 => "극한",
                _ => "알 수 없음"
            };
        }
        
        /// <summary>
        /// 별점을 별 아이콘 문자열로 변환
        /// </summary>
        public string GetStarString(int stars)
        {
            return stars switch
            {
                3 => "⭐⭐⭐",
                2 => "⭐⭐☆",
                1 => "⭐☆☆",
                _ => "☆☆☆"
            };
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