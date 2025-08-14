using System.Linq;
using UnityEngine;
using BlokusUnity.Data;
using BlokusUnity.Game;
using BlokusUnity.Network;
using NetworkUserStageProgress = BlokusUnity.Network.UserStageProgress;
using GameUserStageProgress = BlokusUnity.Game.UserStageProgress;
using NetworkStageData = BlokusUnity.Network.StageData;

namespace BlokusUnity.Data
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
        private StageData currentSelectedStage;
        private int currentSelectedStageNumber;

        // API 기반 스테이지 매니저
        private StageManager apiStageManager;

        void Awake()
        {
            // 싱글톤 패턴 + DontDestroyOnLoad
            if (Instance == null)
            {
                Instance = this;

                // 루트 GameObject인지 확인하고 DontDestroyOnLoad 적용
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
                else
                {
                    Debug.LogWarning("StageDataManager가 루트 GameObject가 아닙니다. DontDestroyOnLoad를 적용할 수 없습니다.");
                }

                SetupCacheEventHandlers();
                Debug.Log("StageDataManager 초기화 완료");
            }
            else
            {
                Destroy(gameObject);
                return;
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

            // 1. 서버 데이터부터 시도
            if (useServerData && TryLoadServerStageData(stageNumber))
            {
                return;
            }

            // 2. 로컬 StageManager 데이터 시도
            if (fallbackToLocalData && TryLoadLocalStageData(stageNumber))
            {
                return;
            }

            // 3. 서버에서 데이터 요청
            if (autoRequestMissingStages && RequestStageDataFromServer(stageNumber))
            {
                return;
            }

            // 4. 최후의 수단: 테스트 스테이지 생성
            Debug.LogWarning($"스테이지 {stageNumber} 데이터를 찾을 수 없습니다.");
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
                currentSelectedStage = BlokusUnity.Utils.ApiDataConverter.ConvertCompactMetadata(metadata);

                Debug.Log($"API 메타데이터에서 스테이지 {stageNumber} 선택");
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
        public StageData GetCurrentStageData()
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
        /// API 기반 스테이지 완료 처리 (서버 동기화 포함)
        /// </summary>
        public void CompleteStage(int stageNumber, int score, int stars, int completionTime = 0)
        {
            Debug.Log($"API 스테이지 {stageNumber} 완료 처리: {score}점, {stars}별");

            // 1. API 기반 StageManager 업데이트
            if (apiStageManager != null && enableApiIntegration)
            {
                var progress = apiStageManager.GetStageProgress(stageNumber);
                progress.UpdateProgress(score, stars);

                // 다음 스테이지 언락
                apiStageManager.UnlockNextStage(stageNumber);
            }

            // 2. UserDataCache 업데이트
            if (UserDataCache.Instance != null)
            {
                var cacheProgress = UserDataCache.Instance.GetStageProgress(stageNumber);

                // 새로운 기록인지 확인
                bool isNewBest = score > cacheProgress.bestScore;
                bool isFirstComplete = !cacheProgress.isCompleted;

                // 진행도 업데이트
                var updatedProgress = new NetworkUserStageProgress
                {
                    stageNumber = stageNumber,
                    isCompleted = true,
                    starsEarned = Mathf.Max(stars, cacheProgress.starsEarned),
                    bestScore = Mathf.Max(score, cacheProgress.bestScore),
                    bestCompletionTime = (completionTime > 0 && (cacheProgress.bestCompletionTime == 0 || completionTime < cacheProgress.bestCompletionTime)) ? completionTime : cacheProgress.bestCompletionTime,
                    totalAttempts = cacheProgress.totalAttempts + 1,
                    successfulAttempts = cacheProgress.successfulAttempts + 1,
                    lastPlayedAt = System.DateTime.Now
                };

                UserDataCache.Instance.SetStageProgress(updatedProgress);
            }

            // 3. API 서버에 완료 보고
            if (HttpApiClient.Instance != null && UserDataCache.Instance.IsLoggedIn() && enableApiIntegration)
            {
                HttpApiClient.Instance.CompleteStage(stageNumber, score, completionTime, true);
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
        /// 네트워크 스테이지 데이터를 StageData로 변환
        /// </summary>
        private StageData ConvertNetworkToStageData(NetworkStageData networkStage)
        {
            return new StageData
            {
                stage_number = networkStage.stageNumber,
                title = networkStage.stageName,
                difficulty = networkStage.difficulty,
                optimal_score = networkStage.optimalScore,
                time_limit = networkStage.timeLimit.GetValueOrDefault(0),
                max_undo_count = networkStage.maxUndoCount,
                available_blocks = networkStage.availableBlocks?.Select(bt => (int)bt).ToArray() ?? new int[0],
                initial_board_state = null, // TODO: JSON 파싱 구현
                hints = new string[0]
            };
        }

        /// <summary>
        /// API 기반 스테이지 언락 상태 확인
        /// </summary>
        public bool IsStageUnlocked(int stageNumber)
        {
            if (stageNumber == 1)
                return true; // 첫 스테이지는 항상 언락

            // API 서버 진행도 확인
            if (UserDataCache.Instance != null && enableApiIntegration)
            {
                int maxCleared = UserDataCache.Instance.GetMaxClearedStage();
                return stageNumber <= maxCleared + 1; // 다음 스테이지까지 언락
            }

            // API StageManager 확인
            if (apiStageManager != null)
            {
                var progress = apiStageManager.GetStageProgress(stageNumber);
                return progress.isUnlocked;
            }

            // 기본값: 순차적 언락 시스템 (첫 10개 스테이지)
            return stageNumber <= 10;
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
    }
}