using System.Linq;
using UnityEngine;
using App.Network;
using App.Services;
using Features.Single.Gameplay;
using Shared.Models;
using NetworkUserStageProgress = App.Network.UserStageProgress;
using NetworkStageData = App.Network.StageData;
using LocalStageData = Shared.Models.StageData;
namespace Features.Single.Core
{
    /// <summary>
    /// 스테이지 데이터 전역 관리자
    /// 씬 전환시 데이터 전달 및 보존, 서버 데이터와 로컬 데이터 통합 관리
    /// </summary>
    public class StageDataManager : MonoBehaviour
    {
        [Header("API Integration")]
        [SerializeField] private bool enableApiIntegration = true;

        [Header("서버 통합 설정")]
        [SerializeField] private bool useServerData = true;
        [SerializeField] private bool fallbackToLocalData = true;
        [SerializeField] private bool autoRequestMissingStages = true;

        // 싱글톤 인스턴스
        public static StageDataManager Instance { get; private set; }

        // 현재 선택된 스테이지 (API 데이터 기반)  
        private LocalStageData currentSelectedStage;

        // 중복 요청 방지를 위한 pending requests 추적
        private static System.Collections.Generic.HashSet<string> pendingRequests = new System.Collections.Generic.HashSet<string>();
        private int currentSelectedStageNumber;

        // 이벤트
        public event System.Action<int> OnStageUnlocked; // 스테이지 언락됨
        public event System.Action<int, int, int> OnStageCompleted; // 스테이지 완룄 (stageNumber, score, stars)

        // API 기반 스테이지 매니저
        private StageManager apiStageManager;

        // Migration Plan: Initialization state and dependency injection
        private bool isInitialized = false;
        private UserDataCache userDataCache;

        void Awake()
        {
            // Migration Plan: Remove DontDestroyOnLoad - SingleCore scene management
            if (Instance == null)
            {
                Instance = this;

                Debug.Log("[StageDataManager] Awake - Ready for initialization");
            }
            else
            {
                Debug.Log("[StageDataManager] Duplicate instance destroyed");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Initialize StageDataManager (Migration Plan)
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            SetupCacheEventHandlers();
            InitializeStageManager();
            isInitialized = true;

            Debug.Log("[StageDataManager] Initialized for SingleCore");
        }

        /// <summary>
        /// Set UserDataCache dependency (Migration Plan)
        /// </summary>
        public void SetUserDataCache(UserDataCache cache)
        {
            userDataCache = cache;
            Debug.Log("[StageDataManager] UserDataCache dependency set");
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
            CleanupCacheEventHandlers();
            isInitialized = false;

            Debug.Log("[StageDataManager] Cleaned up for scene unload");
        }

        private void InitializeStageManager()
        {
            if (apiStageManager == null)
            {
                apiStageManager = new Shared.Models.StageManager();
            }

            // API 기반 StageManager 초기화
            if (enableApiIntegration)
            {
                apiStageManager = new StageManager();
                Debug.Log("API 기반 StageManager 초기화 완료");
            }
        }

        void OnDestroy()
        {
            CleanupCacheEventHandlers();
        }

        // ========================================
        // 이벤트 설정
        // ========================================

        /// <summary>
        /// UserDataCache 이벤트 핸들러 설정
        /// </summary>
        private void SetupCacheEventHandlers()
        {
            if (UserDataCache.Instance != null)
            {
                UserDataCache.Instance.OnStageDataUpdated += OnServerStageDataUpdated;
                UserDataCache.Instance.OnStageProgressUpdated += OnStageProgressUpdated;
            }
            else
            {
                // UserDataCache가 늦게 초기화될 수 있으므로 재시도
                Invoke(nameof(SetupCacheEventHandlers), 1f);
            }
        }

        /// <summary>
        /// 이벤트 핸들러 정리
        /// </summary>
        private void CleanupCacheEventHandlers()
        {
            if (UserDataCache.Instance != null)
            {
                UserDataCache.Instance.OnStageDataUpdated -= OnServerStageDataUpdated;
                UserDataCache.Instance.OnStageProgressUpdated -= OnStageProgressUpdated;
            }
        }

        // ========================================
        // 스테이지 선택 및 데이터 로드
        // ========================================

        /// <summary>
        /// 스테이지 선택 (서버 데이터 우선)
        /// </summary>
        public void SelectStage(int stageNumber)
        {
            currentSelectedStageNumber = stageNumber;
            Debug.Log($"[SelectStage] 스테이지 {stageNumber} 선택 시작");

            // 1. 서버 데이터부터 시도
            Debug.Log($"[SelectStage] 1단계: 서버 데이터 시도 (useServerData={useServerData})");
            if (useServerData && TryLoadServerStageData(stageNumber))
            {
                Debug.Log($"[SelectStage] ✅ 1단계 성공 - 서버 데이터로 완료");
                return;
            }

            // 2. 로컬 StageManager 데이터 시도
            Debug.Log($"[SelectStage] 2단계: 로컬 데이터 시도 (fallbackToLocalData={fallbackToLocalData})");
            if (fallbackToLocalData && TryLoadLocalStageData(stageNumber))
            {
                Debug.Log($"[SelectStage] ✅ 2단계 성공 - 로컬 데이터로 완료");
                return;
            }

            // 3. 서버에서 데이터 요청
            Debug.Log($"[SelectStage] 3단계: API 서버 요청 (autoRequestMissingStages={autoRequestMissingStages})");
            if (autoRequestMissingStages && RequestStageDataFromServer(stageNumber))
            {
                Debug.Log($"[SelectStage] ✅ 3단계 성공 - 서버 요청 완료");
                return;
            }

            // 4. 최후의 수단: 테스트 스테이지 생성
            Debug.LogWarning($"[SelectStage] ❌ 모든 단계 실패 - 스테이지 {stageNumber} 데이터를 찾을 수 없습니다.");
        }

        /// <summary>
        /// API 서버 스테이지 데이터 로드 시도
        /// </summary>
        private bool TryLoadServerStageData(int stageNumber)
        {
            if (UserDataCache.Instance == null || !enableApiIntegration)
                return false;

            // 압축된 메타데이터에서 기본 정보 확인
            var metadata = UserDataCache.Instance.GetStageMetadata(stageNumber);
            if (metadata != null)
            {
                // 메타데이터를 기반으로 StageData 생성 (이미 변환됨)
                currentSelectedStage = ApiDataConverter.ConvertCompactMetadata(metadata);

                Debug.Log($"[TryLoadServerStageData] ✅ API 메타데이터에서 스테이지 {stageNumber} 선택 - return true");
                return true;
            }

            // 캐시된 전체 스테이지 데이터 확인
            var cachedStageData = UserDataCache.Instance.GetStageData(stageNumber);
            if (cachedStageData != null)
            {
                // NetworkStageData를 직접 StageData로 변환 (동일한 구조)
                currentSelectedStage = ConvertNetworkToStageData(cachedStageData);
                Debug.Log($"캐시된 API 데이터에서 스테이지 {stageNumber} 선택");
                return true;
            }

            return false;
        }

        /// <summary>
        /// API 캐시에서 스테이지 데이터 로드 시도
        /// </summary>
        private bool TryLoadLocalStageData(int stageNumber)
        {
            if (apiStageManager != null && enableApiIntegration)
            {
                var apiStageData = apiStageManager.GetStageData(stageNumber);

                if (apiStageData != null)
                {
                    currentSelectedStage = apiStageData;
                    Debug.Log($"API 캐시에서 스테이지 {stageNumber} 선택");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// API 서버에서 스테이지 데이터 요청
        /// </summary>
        private bool RequestStageDataFromServer(int stageNumber)
        {
            if (HttpApiClient.Instance != null && UserDataCache.Instance.IsLoggedIn() && enableApiIntegration)
            {
                Debug.Log($"API 서버에 스테이지 {stageNumber} 데이터 요청");

                // 스테이지 메타데이터가 없으면 전체 목록 요청
                if (UserDataCache.Instance.GetStageMetadata() == null)
                {
                    HttpApiClient.Instance.GetStageList();
                }

                // 구체적인 스테이지 데이터 요청
                HttpApiClient.Instance.GetStageData(stageNumber);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 현재 선택된 스테이지 데이터 반환
        /// </summary>
        public LocalStageData GetCurrentStageData()
        {
            return currentSelectedStage;
        }

        /// <summary>
        /// 현재 스테이지 번호 반환
        /// </summary>
        public int GetCurrentStageNumber()
        {
            return currentSelectedStageNumber;
        }

        /// <summary>
        /// API 기반 스테이지 매니저 반환
        /// </summary>
        public StageManager GetStageManager()
        {
            return apiStageManager;
        }

        /// <summary>
        /// 싱글게임매니저에 API 데이터 전달
        /// </summary>
        public void PassDataToSingleGameManager()
        {
            if (currentSelectedStage != null)
            {
                // SingleGameManager의 static 프로퍼티에 데이터 설정
                SingleGameManager.SetStageContext(currentSelectedStage.stage_number, this);

                Debug.Log($"SingleGameManager에 API 스테이지 데이터 전달: {currentSelectedStage.stage_number}");
            }
            else
            {
                Debug.LogError("전달할 스테이지 데이터가 없습니다!");
            }
        }

        // ========================================
        // 이벤트 핸들러들
        // ========================================

        /// <summary>
        /// 서버 스테이지 데이터 업데이트 처리
        /// </summary>
        private void OnServerStageDataUpdated(NetworkStageData serverStageData)
        {
            Debug.Log($"서버 스테이지 데이터 업데이트: {serverStageData.stageNumber}");

            // 현재 선택된 스테이지가 업데이트된 경우 다시 로드
            if (currentSelectedStageNumber == serverStageData.stageNumber)
            {
                currentSelectedStage = ConvertNetworkToStageData(serverStageData);
                Debug.Log($"현재 스테이지 데이터 업데이트: {currentSelectedStage.stage_number}");
            }
        }

        /// <summary>
        /// API 기반 스테이지 진행도 업데이트 처리
        /// </summary>
        private void OnStageProgressUpdated(NetworkUserStageProgress progress)
        {
            Debug.Log($"API 스테이지 진행도 업데이트: {progress.stageNumber} (별: {progress.starsEarned})");

            // API 기반 StageManager 업데이트
            if (apiStageManager != null && enableApiIntegration)
            {
                var localProgress = apiStageManager.GetStageProgress(progress.stageNumber);
                if (localProgress != null)
                {
                    localProgress.UpdateProgress(progress.bestScore, progress.starsEarned);

                    // 완료된 경우 다음 스테이지 언락
                    if (progress.isCompleted)
                    {
                        apiStageManager.UnlockNextStage(progress.stageNumber);
                    }
                }
            }
        }

        // ========================================
        // 스테이지 완료 처리 (서버 연동)
        // ========================================

        /// <summary>
        /// API 기반 스테이지 완료 처리 (서버 동기화 포함, 중복 요청 방지)
        /// </summary>
        public void CompleteStage(int stageNumber, int score, int stars, int completionTime = 0)
        {
            // 중복 요청 방지
            string requestKey = $"complete_stage_{stageNumber}_{score}_{stars}_{completionTime}";
            if (pendingRequests.Contains(requestKey))
            {
                Debug.LogWarning($"[StageDataManager] 중복 완료 요청 감지 및 차단: Stage {stageNumber}");
                return;
            }

            pendingRequests.Add(requestKey);
            Debug.Log($"API 스테이지 {stageNumber} 완료 처리: {score}점, {stars}별 (RequestKey: {requestKey})");

            // 1. API 기반 StageManager 업데이트
            if (apiStageManager != null && enableApiIntegration)
            {
                var progress = apiStageManager.GetStageProgress(stageNumber);
                progress.UpdateProgress(score, stars);

                // 다음 스테이지 언락
                apiStageManager.UnlockNextStage(stageNumber);
            }

            // 2. API 서버에 완료 보고 (먼저 실행)
            if (HttpApiClient.Instance != null && UserDataCache.Instance.IsLoggedIn() && enableApiIntegration)
            {
                // 서버 응답을 기다린 후 로컬 상태 업데이트
                System.Action<bool, string> onServerResponse = null;
                onServerResponse = (success, message) =>
                {
                    // 요청 완료 처리
                    pendingRequests.Remove(requestKey);

                    // 이벤트 구독 해제
                    if (HttpApiClient.Instance != null)
                        HttpApiClient.Instance.OnStageCompleteResponse -= onServerResponse;

                    if (success)
                    {
                        Debug.Log($"[StageDataManager] 스테이지 {stageNumber} 서버 저장 성공: {message}");
                    }
                    else
                    {
                        Debug.LogWarning($"[StageDataManager] 스테이지 {stageNumber} 서버 저장 실패: {message}");
                    }
                    
                    // 유효한 완료(stars >= 1)인 경우 서버 실패와 관계없이 로컬 업데이트
                    // 게임플레이 자체는 성공했으므로 진행도는 저장되어야 함
                    if (stars >= 1)
                    {
                        Debug.Log($"[StageDataManager] 유효한 완료(stars={stars}) - 서버 상태와 관계없이 로컬 진행도 업데이트");
                        UpdateLocalStageProgress(stageNumber, score, stars, completionTime);

                        // 이벤트 발생
                        OnStageCompleted?.Invoke(stageNumber, score, stars);

                        // 다음 스테이지 언락 확인 및 이벤트 발생
                        int nextStage = stageNumber + 1;
                        if (IsStageUnlocked(nextStage))
                        {
                            OnStageUnlocked?.Invoke(nextStage);
                            Debug.Log($"스테이지 {nextStage} 언락됨!");
                        }
                    }
                    else
                    {
                        Debug.Log($"[StageDataManager] 실패한 완료(stars={stars}) - 로컬 진행도 업데이트 건너뜀");
                    }
                };

                // 서버 응답 이벤트 구독
                HttpApiClient.Instance.OnStageCompleteResponse += onServerResponse;
                HttpApiClient.Instance.CompleteStage(stageNumber, score, completionTime, true);
            }
            else
            {
                // API 통합이 비활성화된 경우 바로 로컬 업데이트
                pendingRequests.Remove(requestKey);
                UpdateLocalStageProgress(stageNumber, score, stars, completionTime);
                OnStageCompleted?.Invoke(stageNumber, score, stars);

                int nextStage = stageNumber + 1;
                if (IsStageUnlocked(nextStage))
                {
                    OnStageUnlocked?.Invoke(nextStage);
                    Debug.Log($"스테이지 {nextStage} 언락됨!");
                }
            }
        }

        /// <summary>
        /// 로컬 스테이지 진행도 업데이트 (서버 성공 후 호출)
        /// </summary>
        private void UpdateLocalStageProgress(int stageNumber, int score, int stars, int completionTime)
        {
            // UserDataCache 업데이트
            if (UserDataCache.Instance != null)
            {
                var cacheProgress = UserDataCache.Instance.GetStageProgress(stageNumber);

                // 새로운 기록인지 확인
                bool isNewBest = score > cacheProgress.bestScore;
                bool isFirstComplete = !cacheProgress.isCompleted;

                // 🔥 수정: GameEndResult 규칙 적용 - stars >= 1일 때만 isCompleted = true
                bool isActuallyCompleted = stars >= 1;
                
                // 진행도 업데이트
                var updatedProgress = new NetworkUserStageProgress
                {
                    stageNumber = stageNumber,
                    isCompleted = isActuallyCompleted, // 🔥 별점 기반 클리어 판정
                    starsEarned = Mathf.Max(stars, cacheProgress.starsEarned),
                    bestScore = Mathf.Max(score, cacheProgress.bestScore),
                    bestCompletionTime = (completionTime > 0 && (cacheProgress.bestCompletionTime == 0 || completionTime < cacheProgress.bestCompletionTime)) ? completionTime : cacheProgress.bestCompletionTime,
                    totalAttempts = cacheProgress.totalAttempts + 1,
                    successfulAttempts = isActuallyCompleted ? cacheProgress.successfulAttempts + 1 : cacheProgress.successfulAttempts,
                    lastPlayedAt = System.DateTime.Now
                };

                UserDataCache.Instance.SetStageProgress(updatedProgress);
                
                // 🔥 규칙 검증 로그
                if (stars == 0 && isActuallyCompleted)
                {
                    Debug.LogError($"🚨 [StageDataManager] 규칙 위반: Stage {stageNumber}에서 0별인데 isCompleted=true");
                }
                if (stars > 0 && !isActuallyCompleted)
                {
                    Debug.LogError($"🚨 [StageDataManager] 규칙 위반: Stage {stageNumber}에서 {stars}별인데 isCompleted=false");
                }
                
                Debug.Log($"[StageDataManager] 로컬 스테이지 진행도 업데이트: Stage {stageNumber}, Stars {stars}, Completed {isActuallyCompleted}, Score {score}");
            }
        }

        /// <summary>
        /// 스테이지 시도 실패 처리
        /// </summary>
        public void FailStage(int stageNumber)
        {
            Debug.Log($"스테이지 {stageNumber} 실패 처리");

            // UserDataCache 업데이트 (시도 횟수만 증가)
            if (UserDataCache.Instance != null)
            {
                var cacheProgress = UserDataCache.Instance.GetStageProgress(stageNumber);
                var updatedProgress = new NetworkUserStageProgress
                {
                    stageNumber = stageNumber,
                    isCompleted = cacheProgress.isCompleted,
                    starsEarned = cacheProgress.starsEarned,
                    bestScore = cacheProgress.bestScore,
                    bestCompletionTime = cacheProgress.bestCompletionTime,
                    totalAttempts = cacheProgress.totalAttempts + 1,
                    successfulAttempts = cacheProgress.successfulAttempts,
                    lastPlayedAt = System.DateTime.Now
                };

                UserDataCache.Instance.SetStageProgress(updatedProgress);
            }
        }

        // ========================================
        // 데이터 변환 유틸리티
        // ========================================

        /// <summary>
        /// 네트워크 스테이지 데이터를 LocalStageData로 변환
        /// </summary>
        private LocalStageData ConvertNetworkToStageData(NetworkStageData networkStage)
        {
            return new LocalStageData
            {
                stage_number = networkStage.stageNumber,
                stage_title = networkStage.stageName,
                difficulty = networkStage.difficulty,
                optimal_score = networkStage.optimalScore,
                time_limit = networkStage.timeLimit.GetValueOrDefault(0),
                max_undo_count = networkStage.maxUndoCount,
                available_blocks = networkStage.availableBlocks?.Select(bt => (int)bt).ToArray() ?? new int[0],
                initial_board_state = ConvertToInitialBoardState(networkStage.initialBoardStateJson),
                hints = "", // NetworkStageData에는 hints 필드가 없음
                stage_description = networkStage.stageDescription,
                thumbnail_url = networkStage.thumbnail_url
            };
        }

        /// <summary>
        /// NetworkStageData의 initialBoardState를 변환
        /// </summary>
        private Shared.Models.InitialBoardState ConvertToInitialBoardState(string initialBoardStateJson)
        {
            if (string.IsNullOrEmpty(initialBoardStateJson))
                return null;

            try
            {
                // JSON에서 INTEGER[] 형식으로 파싱 시도
                var jsonObject = JsonUtility.FromJson<InitialBoardStateJson>(initialBoardStateJson);

                var state = new Shared.Models.InitialBoardState();

                if (jsonObject.boardPositions != null && jsonObject.boardPositions.Length > 0)
                {
                    state.boardPositions = jsonObject.boardPositions;
                    Debug.Log($"[StageDataManager] 새로운 INTEGER[] 형식으로 초기 보드 상태 파싱: {jsonObject.boardPositions.Length}개 위치");
                }
                else
                {
                    // 빈 상태
                    state.boardPositions = new int[0];
                }

                return state;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[StageDataManager] 초기 보드 상태 JSON 파싱 실패: {ex.Message}");
                return new Shared.Models.InitialBoardState { boardPositions = new int[0] };
            }
        }

        /// <summary>
        /// JSON 파싱을 위한 내부 클래스
        /// </summary>
        [System.Serializable]
        private class InitialBoardStateJson
        {
            public int[] boardPositions;
            // Legacy support
            public object[] pre;
            public int[] obsIdx;
        }

        /// <summary>
        /// API 기반 스테이지 언락 상태 확인
        /// </summary>
        public bool IsStageUnlocked(int stageNumber)
        {
            // 1) 스테이지 1은 항상 언락
            if (stageNumber <= 1)
            {
                Debug.Log($"[IsStageUnlocked] 스테이지 {stageNumber}: 첫 스테이지이므로 언락=true");
                return true;
            }

            // 2) 권장: UserDataCache의 집계값(MaxStageCompleted) 기반
            var cache = Features.Single.Core.UserDataCache.Instance;
            if (cache != null)
            {
                int maxDone = cache.MaxStageCompleted;   // 프로필/캐시 합산의 최신값
                bool unlocked = stageNumber <= (maxDone + 1);
                Debug.Log($"[IsStageUnlocked] 스테이지 {stageNumber}: MaxStageCompleted={maxDone} → 언락={unlocked}");
                if (unlocked) return true;

                // 2-보강) 방어적 폴백: 직전 스테이지 완료/별 ≥1 이면 언락
                var prev = cache.GetStageProgress(stageNumber - 1);
                Debug.Log($"[IsStageUnlocked] 스테이지 {stageNumber}: 이전 {stageNumber - 1} 완료={prev?.isCompleted}, 별={prev?.starsEarned}");
                if (prev != null && prev.isCompleted && prev.starsEarned > 0)
                {
                    Debug.Log($"[IsStageUnlocked] 스테이지 {stageNumber}: 이전 스테이지 조건 만족 - 언락=true");
                    return true;
                }
            }
            else
            {
                Debug.LogWarning("[IsStageUnlocked] UserDataCache.Instance가 null - 레거시 폴백 사용");
            }

            return false;
        }


        /// <summary>
        /// API 기반 스테이지 진행도 가져오기 (서버 우선)
        /// </summary>
        public NetworkUserStageProgress GetStageProgress(int stageNumber)
        {
            // API 서버 진행도 우선
            if (UserDataCache.Instance != null && enableApiIntegration)
            {
                return UserDataCache.Instance.GetStageProgress(stageNumber);
            }

            // API StageManager 진행도 폴백
            if (apiStageManager != null)
            {
                var localProgress = apiStageManager.GetStageProgress(stageNumber);
                if (localProgress != null)
                {
                    // 로컬 진행도를 네트워크 형식으로 변환
                    return ConvertLocalProgressToNetwork(localProgress, stageNumber);
                }
            }

            // 기본 진행도 반환
            return new NetworkUserStageProgress
            {
                stageNumber = stageNumber,
                isCompleted = false,
                starsEarned = 0,
                bestScore = 0
            };
        }

        /// <summary>
        /// 로컬 진행도를 네트워크 형식으로 변환 (임시 구현)
        /// </summary>
        private NetworkUserStageProgress ConvertLocalProgressToNetwork(object localProgress, int stageNumber)
        {
            // 실제 구현에서는 로컬 진행도 타입에 따라 적절히 변환
            return new NetworkUserStageProgress
            {
                stageNumber = stageNumber,
                isCompleted = false,
                starsEarned = 0,
                bestScore = 0
            };
        }

        /// <summary>
        /// 별점이 1개 이상인 스테이지 중 최대 번호 반환
        /// </summary>
        private int GetMaxClearedStageWithStars()
        {
            if (UserDataCache.Instance == null) return 0;

            int maxStage = 0;

            // 🔥 수정: 실제 메타데이터 기반으로 상한선 설정
            int totalStages = 14; // 기본값
            if (UserDataCache.Instance != null)
            {
                var metadata = UserDataCache.Instance.GetStageMetadata();
                if (metadata != null && metadata.Length > 0)
                {
                    totalStages = metadata.Length;
                }
            }
            
            // 캐시된 진행도에서 별점이 1개 이상인 스테이지 찾기
            for (int i = 1; i <= totalStages; i++)
            {
                var progress = UserDataCache.Instance.GetStageProgress(i);
                if (progress != null && progress.isCompleted && progress.starsEarned > 0)
                {
                    maxStage = i;
                    Debug.Log($"[GetMaxClearedStageWithStars] 스테이지 {i}: 별 {progress.starsEarned}개 (maxStage 업데이트)");
                }
                else if (progress == null)
                {
                    // 진행도가 아직 로드되지 않은 스테이지는 계속 확인
                    continue;
                }
                else if (!progress.isCompleted || progress.starsEarned == 0)
                {
                    // 명시적으로 미완료인 스테이지는 계속 확인 (건너뛸 수 있음)
                    continue;
                }
            }

            Debug.Log($"[GetMaxClearedStageWithStars] 최종 결과: {maxStage}");
            return maxStage;
        }

        // ========================================
        // 디버그 및 상태 정보
        // ========================================

        /// <summary>
        /// API 기반 스테이지 매니저 상태 정보 반환
        /// </summary>
        public string GetStatusInfo()
        {
            string apiStatus = enableApiIntegration ? "활성" : "비활성";
            string serverStatus = useServerData ? "활성" : "비활성";
            string cacheInfo = UserDataCache.Instance?.GetCacheStatusInfo() ?? "없음";

            return $"API연동: {apiStatus}, 서버연동: {serverStatus}, 캐시: {cacheInfo}";
        }

        /// <summary>
        /// 강제로 API 서버에서 스테이지 데이터 새로고침
        /// </summary>
        public void RefreshStageDataFromServer(int stageNumber)
        {
            if (HttpApiClient.Instance != null && UserDataCache.Instance.IsLoggedIn() && enableApiIntegration)
            {
                Debug.Log($"API 서버에서 스테이지 {stageNumber} 데이터 새로고침");

                // 스테이지 메타데이터 새로고침
                HttpApiClient.Instance.GetStageList();

                // 구체적인 스테이지 데이터 새로고침
                HttpApiClient.Instance.GetStageData(stageNumber);

                // 사용자 진행도 새로고침
                HttpApiClient.Instance.GetUserProgress(stageNumber);
            }
        }

        /// <summary>
        /// 캐시된 스테이지 데이터 완전 정리 (로그아웃 시 호출)
        /// </summary>
        public void ClearCache()
        {
            Debug.Log("[StageDataManager] 캐시 데이터 정리 시작");
            
            // 현재 선택된 스테이지 초기화
            currentSelectedStage = null;
            currentSelectedStageNumber = 0;
            
            // API 스테이지 매니저 정리
            if (apiStageManager != null)
            {
                apiStageManager = null;
            }
            
            Debug.Log("[StageDataManager] 캐시 데이터 정리 완료");
        }
    }
}