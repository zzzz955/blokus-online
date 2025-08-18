using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BlokusUnity.Network;
using BlokusUnity.Common;

namespace BlokusUnity.Data
{
    /// <summary>
    /// 새로운 캐싱 전략 관리자
    /// 라이트 동기화, 메타데이터 TTL, 버전 관리 등을 담당
    /// </summary>
    public class CacheManager : MonoBehaviour
    {
        [Header("동기화 설정")]
        [SerializeField] private int lightSyncIntervalMinutes = 45;
        [SerializeField] private int metadataTTLMinutes = 120;
        [SerializeField] private bool enableAutoSync = true;
        [SerializeField] private bool debugMode = true;

        // 싱글톤
        public static CacheManager Instance { get; private set; }

        // 캐시 상태
        private UserProfileData userProfile;
        private Dictionary<int, StageMetadata> stageMetadataCache = new Dictionary<int, StageMetadata>();
        private Dictionary<int, StageProgressData> progressDataCache = new Dictionary<int, StageProgressData>();

        // 버전 및 타임스탬프
        private int cachedProgressVersion = 0;
        private string cachedMetadataVersion = "";
        private DateTime lastSyncTime = DateTime.MinValue;
        private DateTime metadataLoadTime = DateTime.MinValue;
        private DateTime lastLightSyncTime = DateTime.MinValue;

        // 상태 플래그
        private bool isMetadataLoaded = false;
        private bool isLightSyncInProgress = false;
        private bool isProgressSyncInProgress = false;
        private bool isMetadataSyncInProgress = false;

        // 이벤트
        public event Action<UserProfileData> OnUserProfileUpdated;
        public event Action<int> OnProgressDataUpdated; // stageNumber
        public event Action OnMetadataLoaded;
        public event Action<bool> OnSyncCompleted; // success

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                
                if (debugMode)
                    Debug.Log("[CacheManager] 초기화 완료");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            // 앱 시작시 자동 동기화
            if (enableAutoSync)
            {
                StartCoroutine(OnAppStartSyncCoroutine());
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && enableAutoSync)
            {
                StartCoroutine(OnForegroundSyncCoroutine());
            }
        }

        // ========================================
        // 앱 시작시 동기화
        // ========================================

        /// <summary>
        /// 앱 시작시 동기화 코루틴
        /// </summary>
        private IEnumerator OnAppStartSyncCoroutine()
        {
            if (debugMode)
                Debug.Log("[CacheManager] 앱 시작 동기화 시작");

            // 1. 토큰 유효성 체크 (HttpApiClient 대기)
            yield return new WaitUntil(() => HttpApiClient.Instance != null);
            
            if (!UserDataCache.Instance.IsLoggedIn())
            {
                if (debugMode)
                    Debug.Log("[CacheManager] 로그인되지 않음 - 동기화 건너뜀");
                yield break;
            }

            // 2. 라이트 동기화 수행
            yield return StartCoroutine(PerformLightSync());
        }

        /// <summary>
        /// 포어그라운드 복귀시 동기화 코루틴
        /// </summary>
        private IEnumerator OnForegroundSyncCoroutine()
        {
            // 마지막 동기화로부터 N분이 지났는지 확인
            var timeSinceLastSync = DateTime.Now - lastLightSyncTime;
            
            if (timeSinceLastSync.TotalMinutes >= lightSyncIntervalMinutes)
            {
                if (debugMode)
                    Debug.Log($"[CacheManager] 포어그라운드 라이트 동기화 ({timeSinceLastSync.TotalMinutes:F1}분 경과)");
                
                yield return StartCoroutine(PerformLightSync());
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[CacheManager] 동기화 스킵 ({timeSinceLastSync.TotalMinutes:F1}분 < {lightSyncIntervalMinutes}분)");
            }
        }

        // ========================================
        // 라이트 동기화 (빠른 버전 체크)
        // ========================================

        /// <summary>
        /// 라이트 동기화 수행
        /// </summary>
        private IEnumerator PerformLightSync()
        {
            if (isLightSyncInProgress)
            {
                if (debugMode)
                    Debug.Log("[CacheManager] 라이트 동기화 이미 진행 중");
                yield break;
            }

            isLightSyncInProgress = true;
            bool success = false;

            try
            {
                if (debugMode)
                    Debug.Log("[CacheManager] 라이트 동기화 시작");

                // API 호출
                bool requestSent = false;
                LightSyncResponse response = null;
                string errorMessage = "";

                // 응답 핸들러 설정
                Action<bool, LightSyncResponse, string> onResponse = (isSuccess, data, error) =>
                {
                    requestSent = true;
                    if (isSuccess)
                    {
                        response = data;
                        success = true;
                    }
                    else
                    {
                        errorMessage = error;
                    }
                };

                // HTTP API 호출
                HttpApiClient.Instance.GetLightSync(onResponse);

                // 응답 대기 (최대 10초)
                float timeout = 10f;
                while (!requestSent && timeout > 0)
                {
                    yield return new WaitForSeconds(0.1f);
                    timeout -= 0.1f;
                }

                if (!requestSent)
                {
                    Debug.LogWarning("[CacheManager] 라이트 동기화 타임아웃");
                    yield break;
                }

                if (success && response != null)
                {
                    // 응답 처리
                    yield return StartCoroutine(ProcessLightSyncResponse(response));
                    lastLightSyncTime = DateTime.Now;
                    
                    if (debugMode)
                        Debug.Log("[CacheManager] 라이트 동기화 완료");
                }
                else
                {
                    Debug.LogWarning($"[CacheManager] 라이트 동기화 실패: {errorMessage}");
                }
            }
            finally
            {
                isLightSyncInProgress = false;
                OnSyncCompleted?.Invoke(success);
            }
        }

        /// <summary>
        /// 라이트 동기화 응답 처리
        /// </summary>
        private IEnumerator ProcessLightSyncResponse(LightSyncResponse response)
        {
            // 1. 사용자 프로필 업데이트
            var newUserProfile = new UserProfileData
            {
                username = response.user_profile.username,
                level = response.user_profile.level,
                maxStageCompleted = response.user_profile.max_stage_completed,
                totalGames = response.user_profile.total_games,
                totalScore = response.user_profile.total_score,
                progressVersion = response.user_profile.progress_version,
                progressUpdatedAt = DateTime.Parse(response.user_profile.progress_updated_at)
            };

            bool profileUpdated = userProfile == null || !userProfile.Equals(newUserProfile);
            if (profileUpdated)
            {
                userProfile = newUserProfile;
                OnUserProfileUpdated?.Invoke(userProfile);
                
                if (debugMode)
                    Debug.Log($"[CacheManager] 사용자 프로필 업데이트: Lv.{userProfile.level}, 최대스테이지: {userProfile.maxStageCompleted}");
            }

            // 2. 진행도 버전 체크
            if (response.user_profile.progress_version != cachedProgressVersion)
            {
                if (debugMode)
                    Debug.Log($"[CacheManager] 진행도 버전 불일치: {cachedProgressVersion} -> {response.user_profile.progress_version}");
                
                yield return StartCoroutine(SyncFullProgress());
            }

            // 3. 메타데이터 버전 체크
            var serverMetadataVersion = response.stages_last_updated;
            if (serverMetadataVersion != cachedMetadataVersion)
            {
                if (debugMode)
                    Debug.Log($"[CacheManager] 메타데이터 버전 불일치: {cachedMetadataVersion} -> {serverMetadataVersion}");
                
                yield return StartCoroutine(SyncStageMetadata());
            }

            lastSyncTime = DateTime.Now;
        }

        // ========================================
        // 전체 진행도 동기화
        // ========================================

        /// <summary>
        /// 전체 진행도 동기화
        /// </summary>
        private IEnumerator SyncFullProgress()
        {
            if (isProgressSyncInProgress)
                yield break;

            isProgressSyncInProgress = true;

            try
            {
                if (debugMode)
                    Debug.Log("[CacheManager] 전체 진행도 동기화 시작");

                bool requestSent = false;
                ProgressSyncResponse response = null;
                string errorMessage = "";

                Action<bool, ProgressSyncResponse, string> onResponse = (isSuccess, data, error) =>
                {
                    requestSent = true;
                    if (isSuccess)
                    {
                        response = data;
                    }
                    else
                    {
                        errorMessage = error;
                    }
                };

                HttpApiClient.Instance.GetProgressSync(onResponse, 1, 1000);

                // 응답 대기
                float timeout = 15f;
                while (!requestSent && timeout > 0)
                {
                    yield return new WaitForSeconds(0.1f);
                    timeout -= 0.1f;
                }

                if (requestSent && response != null)
                {
                    // 진행도 데이터 업데이트
                    progressDataCache.Clear();
                    foreach (var progress in response.progress_data)
                    {
                        var progressData = new StageProgressData
                        {
                            stageNumber = progress.stage_number,
                            isCompleted = progress.is_completed,
                            starsEarned = progress.stars_earned,
                            bestScore = progress.best_score,
                            bestCompletionTime = progress.best_completion_time,
                            totalAttempts = progress.total_attempts,
                            successfulAttempts = progress.successful_attempts,
                            firstCompletedAt = !string.IsNullOrEmpty(progress.first_completed_at) ? DateTime.Parse(progress.first_completed_at) : (DateTime?)null,
                            lastPlayedAt = !string.IsNullOrEmpty(progress.last_played_at) ? DateTime.Parse(progress.last_played_at) : (DateTime?)null
                        };

                        progressDataCache[progress.stage_number] = progressData;
                    }

                    cachedProgressVersion = response.progress_version;

                    if (debugMode)
                        Debug.Log($"[CacheManager] 진행도 동기화 완료: {response.progress_data.Length}개 스테이지");
                }
                else
                {
                    Debug.LogWarning($"[CacheManager] 진행도 동기화 실패: {errorMessage}");
                }
            }
            finally
            {
                isProgressSyncInProgress = false;
            }
        }

        // ========================================
        // 스테이지 메타데이터 동기화
        // ========================================

        /// <summary>
        /// 스테이지 메타데이터 동기화 (TTL 확인 포함)
        /// </summary>
        public IEnumerator EnsureMetadataLoaded()
        {
            // TTL 확인
            var timeSinceLoad = DateTime.Now - metadataLoadTime;
            bool isTTLExpired = timeSinceLoad.TotalMinutes >= metadataTTLMinutes;
            
            if (debugMode)
                Debug.Log($"[CacheManager] EnsureMetadataLoaded 호출 - 로드됨: {isMetadataLoaded}, TTL만료: {isTTLExpired}");
            
            if (!isMetadataLoaded || isTTLExpired)
            {
                if (debugMode)
                    Debug.Log($"[CacheManager] 메타데이터 동기화 시작 (TTL만료: {isTTLExpired})");
                
                yield return StartCoroutine(SyncStageMetadata());
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[CacheManager] 메타데이터 이미 로드됨 - 동기화 건너뜀");
            }
        }

        /// <summary>
        /// 스테이지 메타데이터 동기화
        /// </summary>
        private IEnumerator SyncStageMetadata()
        {
            if (isMetadataSyncInProgress)
                yield break;

            isMetadataSyncInProgress = true;

            try
            {
                if (debugMode)
                    Debug.Log("[CacheManager] 메타데이터 동기화 시작");

                bool requestSent = false;
                MetadataSyncResponse response = null;
                string errorMessage = "";

                Action<bool, MetadataSyncResponse, string> onResponse = (isSuccess, data, error) =>
                {
                    requestSent = true;
                    if (isSuccess)
                    {
                        response = data;
                    }
                    else
                    {
                        errorMessage = error;
                    }
                };

                HttpApiClient.Instance.GetMetadataSync(onResponse, cachedMetadataVersion);

                // 응답 대기
                float timeout = 15f;
                while (!requestSent && timeout > 0)
                {
                    yield return new WaitForSeconds(0.1f);
                    timeout -= 0.1f;
                }

                if (requestSent && response != null)
                {
                    if (response.not_modified)
                    {
                        if (debugMode)
                            Debug.Log("[CacheManager] 메타데이터 변경 없음 (304)");
                    }
                    else
                    {
                        // 메타데이터 업데이트
                        stageMetadataCache.Clear();
                        foreach (var stage in response.stages)
                        {
                            var metadata = new StageMetadata
                            {
                                stageId = stage.stage_id,
                                stageNumber = stage.stage_number,
                                difficulty = stage.difficulty,
                                optimalScore = stage.optimal_score,
                                timeLimit = stage.time_limit,
                                maxUndoCount = stage.max_undo_count,
                                description = stage.description,
                                hints = stage.hints,
                                isFeatured = stage.is_featured,
                                thumbnailUrl = stage.thumbnail_url
                            };

                            stageMetadataCache[stage.stage_number] = metadata;
                        }

                        cachedMetadataVersion = response.metadata_version;
                        metadataLoadTime = DateTime.Now;
                        isMetadataLoaded = true;

                        OnMetadataLoaded?.Invoke();

                        if (debugMode)
                            Debug.Log($"[CacheManager] 메타데이터 동기화 완료: {response.stages.Length}개 스테이지");
                    }
                }
                else
                {
                    Debug.LogWarning($"[CacheManager] 메타데이터 동기화 실패: {errorMessage}");
                }
            }
            finally
            {
                isMetadataSyncInProgress = false;
            }
        }

        // ========================================
        // 스테이지 완료 후 즉시 캐시 갱신
        // ========================================

        /// <summary>
        /// 스테이지 완료 후 로컬 캐시 즉시 갱신
        /// </summary>
        public void UpdateProgressAfterCompletion(int stageNumber, int score, int stars, int completionTime, 
            CompleteStageResponse serverResponse)
        {
            if (serverResponse?.updated_progress != null)
            {
                // 서버 응답으로 캐시 업데이트
                var progress = serverResponse.updated_progress;
                var progressData = new StageProgressData
                {
                    stageNumber = progress.stage_id,
                    isCompleted = progress.is_completed,
                    starsEarned = progress.stars_earned,
                    bestScore = progress.best_score,
                    bestCompletionTime = progress.best_completion_time,
                    totalAttempts = (progressDataCache.ContainsKey(stageNumber) ? progressDataCache[stageNumber].totalAttempts : 0) + 1,
                    successfulAttempts = (progressDataCache.ContainsKey(stageNumber) ? progressDataCache[stageNumber].successfulAttempts : 0) + 1,
                    lastPlayedAt = DateTime.Now
                };

                progressDataCache[stageNumber] = progressData;
                OnProgressDataUpdated?.Invoke(stageNumber);

                // 사용자 프로필도 업데이트
                if (serverResponse.user_profile != null)
                {
                    var profile = serverResponse.user_profile;
                    userProfile = new UserProfileData
                    {
                        username = userProfile?.username ?? "",
                        level = profile.level,
                        maxStageCompleted = profile.max_stage_completed,
                        totalGames = profile.total_games > 0 ? profile.total_games : (userProfile?.totalGames ?? 0),
                        totalScore = profile.total_score > 0 ? profile.total_score : (userProfile?.totalScore ?? 0),
                        progressVersion = serverResponse.progress_version,
                        progressUpdatedAt = DateTime.Now
                    };

                    cachedProgressVersion = serverResponse.progress_version;
                    OnUserProfileUpdated?.Invoke(userProfile);
                }

                if (debugMode)
                    Debug.Log($"[CacheManager] 스테이지 {stageNumber} 완료 후 캐시 즉시 갱신");
            }
        }

        // ========================================
        // 공개 API 메서드들
        // ========================================

        /// <summary>
        /// 사용자 프로필 조회
        /// </summary>
        public UserProfileData GetUserProfile()
        {
            return userProfile;
        }

        /// <summary>
        /// 스테이지 진행도 조회
        /// </summary>
        public StageProgressData GetStageProgress(int stageNumber)
        {
            if (progressDataCache.ContainsKey(stageNumber))
            {
                return progressDataCache[stageNumber];
            }

            // 기본값 반환
            return new StageProgressData
            {
                stageNumber = stageNumber,
                isCompleted = false,
                starsEarned = 0,
                bestScore = 0
            };
        }

        /// <summary>
        /// 스테이지 메타데이터 조회
        /// </summary>
        public StageMetadata GetStageMetadata(int stageNumber)
        {
            return stageMetadataCache.ContainsKey(stageNumber) ? stageMetadataCache[stageNumber] : null;
        }

        /// <summary>
        /// 메타데이터 로드 상태 확인
        /// </summary>
        public bool IsMetadataLoaded()
        {
            return isMetadataLoaded;
        }

        /// <summary>
        /// 강제 전체 동기화
        /// </summary>
        public void ForceFullSync()
        {
            StartCoroutine(ForceFullSyncCoroutine());
        }

        private IEnumerator ForceFullSyncCoroutine()
        {
            if (debugMode)
                Debug.Log("[CacheManager] 강제 전체 동기화 시작");

            yield return StartCoroutine(PerformLightSync());
        }

        /// <summary>
        /// 캐시 상태 정보
        /// </summary>
        public string GetCacheStatus()
        {
            return $"프로필: {(userProfile != null ? "로드됨" : "없음")}, " +
                   $"진행도: {progressDataCache.Count}개, " +
                   $"메타데이터: {(isMetadataLoaded ? stageMetadataCache.Count + "개" : "없음")}, " +
                   $"버전: {cachedProgressVersion}";
        }
    }

    // ========================================
    // 데이터 구조체들
    // ========================================

    [Serializable]
    public class UserProfileData : IEquatable<UserProfileData>
    {
        public string username;
        public int level;
        public int maxStageCompleted;
        public int totalGames;
        public int totalScore;
        public int progressVersion;
        public DateTime progressUpdatedAt;

        public bool Equals(UserProfileData other)
        {
            if (other == null) return false;
            return username == other.username &&
                   level == other.level &&
                   maxStageCompleted == other.maxStageCompleted &&
                   totalGames == other.totalGames &&
                   totalScore == other.totalScore &&
                   progressVersion == other.progressVersion;
        }
    }

    [Serializable]
    public class StageProgressData
    {
        public int stageNumber;
        public bool isCompleted;
        public int starsEarned;
        public int bestScore;
        public int bestCompletionTime;
        public int totalAttempts;
        public int successfulAttempts;
        public DateTime? firstCompletedAt;
        public DateTime? lastPlayedAt;
    }

    [Serializable]
    public class StageMetadata
    {
        public int stageId;
        public int stageNumber;
        public int difficulty;
        public int optimalScore;
        public int timeLimit;
        public int maxUndoCount;
        public string description;
        public string hints;
        public bool isFeatured;
        public string thumbnailUrl;
    }

    // ========================================
    // API 응답 구조체들
    // ========================================

    [Serializable]
    public class LightSyncResponse
    {
        public UserProfileResponse user_profile;
        public string stages_last_updated;
        public string server_time;
        public string sync_completed_at;
    }

    [Serializable]
    public class UserProfileResponse
    {
        public string username;
        public int level;
        public int max_stage_completed;
        public int total_games;
        public int total_score;
        public int progress_version;
        public string progress_updated_at;
    }

    [Serializable]
    public class ProgressSyncResponse
    {
        public int progress_version;
        public string progress_updated_at;
        public int from_stage;
        public int to_stage;
        public ProgressDataResponse[] progress_data;
        public int total_count;
        public string sync_completed_at;
    }

    [Serializable]
    public class ProgressDataResponse
    {
        public int stage_number;
        public int stage_id;
        public bool is_completed;
        public int stars_earned;
        public int best_score;
        public int best_completion_time;
        public int total_attempts;
        public int successful_attempts;
        public string first_completed_at;
        public string last_played_at;
    }

    [Serializable]
    public class MetadataSyncResponse
    {
        public string metadata_version;
        public bool not_modified;
        public StageMetadataResponse[] stages;
        public int total_count;
        public string sync_completed_at;
    }

    [Serializable]
    public class StageMetadataResponse
    {
        public int stage_id;
        public int stage_number;
        public int difficulty;
        public int optimal_score;
        public int time_limit;
        public int max_undo_count;
        public string description;
        public string hints;
        public bool is_featured;
        public string thumbnail_url;
    }

    [Serializable]
    public class CompleteStageResponse
    {
        public bool success;
        public UpdatedProgressResponse updated_progress;
        public UserProfileResponse user_profile;
        public int progress_version;
    }

    [Serializable]
    public class UpdatedProgressResponse
    {
        public int stage_id;
        public bool is_completed;
        public int stars_earned;
        public int best_score;
        public int best_completion_time;
    }
}