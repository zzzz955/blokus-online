using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BlokusUnity.Common;
using BlokusUnity.Data;
using BlokusUnity.Game;
using BlokusUnity.Network;
using NetworkStageData = BlokusUnity.Network.StageData;
using NetworkUserStageProgress = BlokusUnity.Network.UserStageProgress;
using ApiStageData = BlokusUnity.Network.HttpApiClient.StageData;

namespace BlokusUnity.Network
{
    /// <summary>
    /// HTTP API와 Unity 게임 시스템을 연결하는 통합 관리자
    /// 스테이지 데이터 로딩, 캐싱, 게임 시작, 완료 보고 등을 관리
    /// </summary>
    public class StageDataIntegrator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SingleGameManager gameManager;
        
        // 싱글톤
        public static StageDataIntegrator Instance { get; private set; }
        
        // 현재 로딩 중인 스테이지
        private int currentLoadingStage = 0;
        private bool isLoadingStageData = false;
        
        // 이벤트
        public event System.Action<NetworkStageData> OnStageDataLoaded;
        public event System.Action<NetworkUserStageProgress> OnStageProgressLoaded;
        public event System.Action<string> OnLoadingError;
        public event System.Action<bool, string> OnStageCompleted; // success, message
        public event System.Action<HttpApiClient.CompactStageMetadata[]> OnStageMetadataLoaded;
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
                
                InitializeIntegration();
                Debug.Log("StageDataIntegrator 초기화 완료");
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void OnDestroy()
        {
            if (Instance == this)
            {
                UnsubscribeFromEvents();
                Instance = null;
            }
        }
        
        /// <summary>
        /// HTTP API 클라이언트와 데이터 캐시 이벤트 구독
        /// </summary>
        private void InitializeIntegration()
        {
            // HTTP API 클라이언트 이벤트 구독
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.OnStageDataReceived += HandleApiStageDataReceived;
                HttpApiClient.Instance.OnStageProgressReceived += HandleApiStageProgressReceived;
                HttpApiClient.Instance.OnStageCompleteResponse += HandleApiStageCompleteResponse;
                HttpApiClient.Instance.OnStageMetadataReceived += HandleApiStageMetadataReceived;
            }
            else
            {
                Debug.LogWarning("HttpApiClient 인스턴스가 없습니다. Start에서 재시도합니다.");
            }
            
            // 사용자 데이터 캐시 이벤트 구독
            if (UserDataCache.Instance != null)
            {
                UserDataCache.Instance.OnStageDataUpdated += HandleCacheStageDataUpdated;
                UserDataCache.Instance.OnStageProgressUpdated += HandleCacheStageProgressUpdated;
            }
            else
            {
                Debug.LogWarning("UserDataCache 인스턴스가 없습니다. Start에서 재시도합니다.");
            }
        }
        
        void Start()
        {
            // 늦은 초기화 시도
            if (HttpApiClient.Instance != null && UserDataCache.Instance != null)
            {
                InitializeIntegration();
            }
        }
        
        private void UnsubscribeFromEvents()
        {
            if (HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.OnStageDataReceived -= HandleApiStageDataReceived;
                HttpApiClient.Instance.OnStageProgressReceived -= HandleApiStageProgressReceived;
                HttpApiClient.Instance.OnStageCompleteResponse -= HandleApiStageCompleteResponse;
                HttpApiClient.Instance.OnStageMetadataReceived -= HandleApiStageMetadataReceived;
            }
            
            if (UserDataCache.Instance != null)
            {
                UserDataCache.Instance.OnStageDataUpdated -= HandleCacheStageDataUpdated;
                UserDataCache.Instance.OnStageProgressUpdated -= HandleCacheStageProgressUpdated;
            }
        }
        
        // ========================================
        // 스테이지 데이터 로딩 API
        // ========================================
        
        /// <summary>
        /// 스테이지 데이터를 로드하고 게임 시작 준비
        /// </summary>
        public void LoadStageData(int stageNumber)
        {
            if (isLoadingStageData)
            {
                Debug.LogWarning($"이미 스테이지 {currentLoadingStage}을 로딩 중입니다.");
                return;
            }
            
            currentLoadingStage = stageNumber;
            isLoadingStageData = true;
            
            Debug.Log($"스테이지 {stageNumber} 데이터 로딩 시작");
            
            // 먼저 캐시에서 확인
            if (UserDataCache.Instance != null && UserDataCache.Instance.HasStageData(stageNumber))
            {
                NetworkStageData cachedData = UserDataCache.Instance.GetStageData(stageNumber);
                Debug.Log($"캐시에서 스테이지 {stageNumber} 데이터 로드됨");
                HandleStageDataReady(cachedData);
                return;
            }
            
            // 캐시에 없으면 API에서 로드
            if (HttpApiClient.Instance != null && HttpApiClient.Instance.IsAuthenticated())
            {
                HttpApiClient.Instance.GetStageData(stageNumber);
            }
            else
            {
                isLoadingStageData = false;
                OnLoadingError?.Invoke("인증되지 않은 상태입니다. 로그인 후 다시 시도하세요.");
            }
        }
        
        /// <summary>
        /// 스테이지 진행도를 로드
        /// </summary>
        public void LoadStageProgress(int stageNumber)
        {
            Debug.Log($"스테이지 {stageNumber} 진행도 로딩 시작");
            
            // 먼저 캐시에서 확인
            if (UserDataCache.Instance != null)
            {
                NetworkUserStageProgress cachedProgress = UserDataCache.Instance.GetStageProgress(stageNumber);
                if (cachedProgress != null && cachedProgress.stageNumber == stageNumber)
                {
                    Debug.Log($"캐시에서 스테이지 {stageNumber} 진행도 로드됨");
                    OnStageProgressLoaded?.Invoke(cachedProgress);
                    return;
                }
            }
            
            // 캐시에 없으면 API에서 로드
            if (HttpApiClient.Instance != null && HttpApiClient.Instance.IsAuthenticated())
            {
                HttpApiClient.Instance.GetStageProgress(stageNumber);
            }
            else
            {
                OnLoadingError?.Invoke("인증되지 않은 상태입니다. 로그인 후 다시 시도하세요.");
            }
        }
        
        /// <summary>
        /// 모든 스테이지 메타데이터를 로드 (목록 표시용)
        /// </summary>
        public void LoadStageMetadata()
        {
            Debug.Log("스테이지 메타데이터 로딩 시작");
            
            // 먼저 캐시에서 확인
            if (UserDataCache.Instance != null)
            {
                var cachedMetadata = UserDataCache.Instance.GetStageMetadata();
                if (cachedMetadata != null && cachedMetadata.Length > 0)
                {
                    Debug.Log($"캐시에서 스테이지 메타데이터 로드됨: {cachedMetadata.Length}개");
                    OnStageMetadataLoaded?.Invoke(cachedMetadata);
                    return;
                }
            }
            
            // 캐시에 없으면 API에서 로드
            if (HttpApiClient.Instance != null && HttpApiClient.Instance.IsAuthenticated())
            {
                HttpApiClient.Instance.GetStageMetadata();
            }
            else
            {
                OnLoadingError?.Invoke("인증되지 않은 상태입니다. 로그인 후 다시 시도하세요.");
            }
        }
        
        /// <summary>
        /// 스테이지 완료 보고
        /// </summary>
        public void ReportStageCompletion(int stageNumber, int score, int completionTimeSeconds, bool completed)
        {
            Debug.Log($"스테이지 {stageNumber} 완료 보고: 점수={score}, 시간={completionTimeSeconds}s, 완료={completed}");
            
            if (HttpApiClient.Instance != null && HttpApiClient.Instance.IsAuthenticated())
            {
                HttpApiClient.Instance.CompleteStage(stageNumber, score, completionTimeSeconds, completed);
            }
            else
            {
                OnStageCompleted?.Invoke(false, "인증되지 않은 상태입니다. 결과를 서버에 저장할 수 없습니다.");
            }
        }
        
        // ========================================
        // 게임 시스템 통합 API
        // ========================================
        
        /// <summary>
        /// 스테이지 데이터로 게임 시작
        /// </summary>
        public void StartStageWithData(NetworkStageData stageData, SingleGameManager targetGameManager = null)
        {
            if (targetGameManager == null)
                targetGameManager = gameManager ?? FindObjectOfType<SingleGameManager>();
            
            if (targetGameManager == null)
            {
                Debug.LogError("SingleGameManager를 찾을 수 없습니다!");
                return;
            }
            
            // API 스테이지 데이터를 StagePayload로 변환
            StagePayload payload = ConvertApiDataToPayload(stageData);
            
            // 게임 매니저 초기화
            targetGameManager.Init(payload);
            
            Debug.Log($"스테이지 {stageData.stageNumber} 게임 시작: {stageData.stageName}");
        }
        
        // ========================================
        // API 이벤트 핸들러들
        // ========================================
        
        private void HandleApiStageDataReceived(ApiStageData apiStageData)
        {
            Debug.Log($"API에서 스테이지 {apiStageData.stage_number} 데이터 수신: {apiStageData.title}");
            
            // API StageData를 Network StageData로 변환
            NetworkStageData networkStageData = ConvertApiToNetworkStageData(apiStageData);
            
            // 캐시에 저장
            if (UserDataCache.Instance != null)
            {
                UserDataCache.Instance.SetStageData(networkStageData);
            }
            
            HandleStageDataReady(networkStageData);
        }
        
        private void HandleApiStageProgressReceived(NetworkUserStageProgress progress)
        {
            Debug.Log($"API에서 스테이지 {progress.stageNumber} 진행도 수신: 완료={progress.isCompleted}, 별={progress.starsEarned}");
            
            // 캐시에 저장
            if (UserDataCache.Instance != null)
            {
                UserDataCache.Instance.SetStageProgress(progress);
            }
            
            OnStageProgressLoaded?.Invoke(progress);
        }
        
        private void HandleApiStageCompleteResponse(bool success, string message)
        {
            Debug.Log($"스테이지 완료 응답: {(success ? "성공" : "실패")} - {message}");
            OnStageCompleted?.Invoke(success, message);
        }
        
        private void HandleApiStageMetadataReceived(HttpApiClient.CompactStageMetadata[] metadata)
        {
            Debug.Log($"API에서 스테이지 메타데이터 수신: {metadata.Length}개");
            
            // 캐시에 저장
            if (UserDataCache.Instance != null)
            {
                UserDataCache.Instance.SetStageMetadata(metadata);
            }
            
            OnStageMetadataLoaded?.Invoke(metadata);
        }
        
        // ========================================
        // 캐시 이벤트 핸들러들
        // ========================================
        
        private void HandleCacheStageDataUpdated(NetworkStageData stageData)
        {
            Debug.Log($"캐시에서 스테이지 {stageData.stageNumber} 데이터 업데이트됨");
            OnStageDataLoaded?.Invoke(stageData);
        }
        
        private void HandleCacheStageProgressUpdated(NetworkUserStageProgress progress)
        {
            Debug.Log($"캐시에서 스테이지 {progress.stageNumber} 진행도 업데이트됨");
            OnStageProgressLoaded?.Invoke(progress);
        }
        
        // ========================================
        // 데이터 변환 헬퍼들
        // ========================================
        
        /// <summary>
        /// API StageData를 Network StageData로 변환
        /// </summary>
        private NetworkStageData ConvertApiToNetworkStageData(ApiStageData apiData)
        {
            List<BlockType> availableBlocks = null;
            if (apiData.available_blocks != null)
            {
                availableBlocks = apiData.available_blocks.Select(id => (BlockType)id).ToList();
            }
            
            return new NetworkStageData
            {
                stageNumber = apiData.stage_number,
                stageName = apiData.title,
                difficulty = apiData.difficulty,
                optimalScore = apiData.optimal_score,
                timeLimit = apiData.time_limit,
                maxUndoCount = apiData.max_undo_count,
                availableBlocks = availableBlocks,
                initialBoardStateJson = apiData.initial_board_state != null ? 
                    UnityEngine.JsonUtility.ToJson(apiData.initial_board_state) : null,
                stageDescription = apiData.stage_description
            };
        }
        
        /// <summary>
        /// API 스테이지 데이터를 Unity 게임용 StagePayload로 변환
        /// </summary>
        private StagePayload ConvertApiDataToPayload(NetworkStageData apiData)
        {
            // 사용 가능한 블록 변환
            BlockType[] availableBlocks = null;
            if (apiData.availableBlocks != null && apiData.availableBlocks.Count > 0)
            {
                availableBlocks = new BlockType[apiData.availableBlocks.Count];
                for (int i = 0; i < apiData.availableBlocks.Count; i++)
                {
                    availableBlocks[i] = apiData.availableBlocks[i];
                }
            }
            
            // StagePayload 생성
            var payload = new StagePayload
            {
                StageName = apiData.stageName,
                BoardSize = 20, // API 데이터에 boardSize가 없으므로 기본값 사용
                AvailableBlocks = availableBlocks,
                ParScore = apiData.optimalScore,
                LayoutSeedOrJson = apiData.initialBoardStateJson, // JSONB 보드 상태
                
                // API 확장 필드들
                StageNumber = apiData.stageNumber,
                Difficulty = apiData.difficulty,
                TimeLimit = apiData.timeLimit ?? 0,
                MaxUndoCount = apiData.maxUndoCount
            };
            
            // 초기 보드 상태 파싱
            if (!string.IsNullOrEmpty(apiData.initialBoardStateJson))
            {
                payload.ParseInitialBoardFromJson(apiData.initialBoardStateJson);
            }
            
            Debug.Log($"[StageDataIntegrator] 스테이지 {apiData.stageNumber} 데이터 변환 완료: {apiData.stageName}");
            
            return payload;
        }
        
        /// <summary>
        /// 스테이지 데이터가 준비되었을 때 공통 처리
        /// </summary>
        private void HandleStageDataReady(NetworkStageData stageData)
        {
            isLoadingStageData = false;
            currentLoadingStage = 0;
            
            OnStageDataLoaded?.Invoke(stageData);
            
            Debug.Log($"스테이지 {stageData.stageNumber} 데이터 준비 완료");
        }
        
        // ========================================
        // 유틸리티 메서드들
        // ========================================
        
        /// <summary>
        /// 현재 로딩 중인 스테이지 번호 반환
        /// </summary>
        public int GetCurrentLoadingStage()
        {
            return currentLoadingStage;
        }
        
        /// <summary>
        /// 로딩 중인지 확인
        /// </summary>
        public bool IsLoading()
        {
            return isLoadingStageData;
        }
        
        /// <summary>
        /// 스테이지가 언락되어 있는지 확인 (간단한 순차 언락 로직)
        /// </summary>
        public bool IsStageUnlocked(int stageNumber)
        {
            if (stageNumber <= 1) return true; // 첫 번째 스테이지는 항상 언락
            
            if (UserDataCache.Instance != null && UserDataCache.Instance.IsLoggedIn())
            {
                int maxClearedStage = UserDataCache.Instance.GetMaxClearedStage();
                return stageNumber <= maxClearedStage + 1;
            }
            
            return false; // 로그인되지 않은 경우 첫 스테이지만 언락
        }
        
        /// <summary>
        /// 현재 캐시 상태 정보 반환 (디버깅용)
        /// </summary>
        public string GetCacheStatusInfo()
        {
            if (UserDataCache.Instance != null)
            {
                return UserDataCache.Instance.GetCacheStatusInfo();
            }
            return "UserDataCache 없음";
        }
    }
}