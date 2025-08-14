using UnityEngine;
using BlokusUnity.Common;
using BlokusUnity.Network;

namespace BlokusUnity.Game
{
    /// <summary>
    /// 싱글플레이 게임 씬 부트스트랩
    /// API 기반 스테이지 데이터 로딩 또는 테스트 모드 지원
    /// </summary>
    public sealed class SingleGameBootstrap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SingleGameManager gameManager;
        
        [Header("Test Settings")]
        [SerializeField] private bool useTestMode = false;
        [SerializeField] private int testStageNumber = 1;
        
        private bool isInitializing = false;

        private void Start()
        {
            if (gameManager == null) gameManager = FindObjectOfType<SingleGameManager>();
            if (gameManager == null)
            {
                Debug.LogError("[SingleGameBootstrap] SingleGameManager를 찾을 수 없습니다!");
                return;
            }

            if (!gameManager.IsInitialized && !isInitializing)
            {
                isInitializing = true;
                
                if (useTestMode)
                {
                    InitializeTestMode();
                }
                else
                {
                    InitializeWithApiData();
                }
            }
        }
        
        /// <summary>
        /// 테스트 모드로 초기화 (API 없이 로컬 데이터 사용)
        /// </summary>
        private void InitializeTestMode()
        {
            Debug.Log("[SingleGameBootstrap] 테스트 모드로 초기화");
            
            var payload = new StagePayload
            {
                StageName = $"테스트 스테이지 {testStageNumber}",
                BoardSize = 20,
                AvailableBlocks = null, // null이면 기본 풀세트 적용
                ParScore = 89 // 테스트용 목표 점수
            };
            
            gameManager.Init(payload);
            isInitializing = false;
        }
        
        /// <summary>
        /// API 데이터로 초기화
        /// </summary>
        private void InitializeWithApiData()
        {
            Debug.Log("[SingleGameBootstrap] API 데이터로 초기화 시도");
            
            // StageDataIntegrator가 있는지 확인
            if (StageDataIntegrator.Instance == null)
            {
                Debug.LogWarning("[SingleGameBootstrap] StageDataIntegrator가 없습니다. 테스트 모드로 대체");
                InitializeTestMode();
                return;
            }
            
            // StageDataManager에서 스테이지 번호 가져오기
            int stageNumber = GetTargetStageNumber();
            if (stageNumber <= 0)
            {
                Debug.LogWarning("[SingleGameBootstrap] 유효한 스테이지 번호를 찾을 수 없습니다. 테스트 모드로 대체");
                InitializeTestMode();
                return;
            }
            
            // 스테이지 데이터 로딩 이벤트 구독
            StageDataIntegrator.Instance.OnStageDataLoaded += HandleStageDataLoaded;
            StageDataIntegrator.Instance.OnLoadingError += HandleLoadingError;
            
            // 스테이지 데이터 로딩 시작
            Debug.Log($"[SingleGameBootstrap] 스테이지 {stageNumber} 데이터 로딩 시작");
            StageDataIntegrator.Instance.LoadStageData(stageNumber);
        }
        
        /// <summary>
        /// 목표 스테이지 번호 결정 (다양한 소스에서 가져오기 시도)
        /// </summary>
        private int GetTargetStageNumber()
        {
            // 1. SingleGameManager의 정적 컨텍스트에서 확인
            if (SingleGameManager.CurrentStage > 0)
            {
                Debug.Log($"[SingleGameBootstrap] SingleGameManager.CurrentStage에서 스테이지 번호 발견: {SingleGameManager.CurrentStage}");
                return SingleGameManager.CurrentStage;
            }
            
            // 2. StageDataManager에서 확인 (씬 간 데이터 전달)
            if (SingleGameManager.StageManager != null)
            {
                var stageData = SingleGameManager.StageManager.GetCurrentStageData();
                if (stageData != null && stageData.stage_number > 0)
                {
                    Debug.Log($"[SingleGameBootstrap] StageDataManager에서 스테이지 번호 발견: {stageData.stage_number}");
                    return stageData.stage_number;
                }
            }
            
            // 3. PlayerPrefs에서 마지막 플레이한 스테이지 확인
            int lastPlayedStage = PlayerPrefs.GetInt("LastPlayedStage", 0);
            if (lastPlayedStage > 0)
            {
                Debug.Log($"[SingleGameBootstrap] PlayerPrefs에서 스테이지 번호 발견: {lastPlayedStage}");
                return lastPlayedStage;
            }
            
            // 4. 기본값으로 첫 번째 스테이지
            Debug.Log("[SingleGameBootstrap] 기본 스테이지 번호 사용: 1");
            return 1;
        }
        
        /// <summary>
        /// API에서 스테이지 데이터가 로드되었을 때 처리
        /// </summary>
        private void HandleStageDataLoaded(BlokusUnity.Network.StageData stageData)
        {
            Debug.Log($"[SingleGameBootstrap] 스테이지 데이터 로드 완료: {stageData.stageName}");
            
            // 이벤트 구독 해제
            if (StageDataIntegrator.Instance != null)
            {
                StageDataIntegrator.Instance.OnStageDataLoaded -= HandleStageDataLoaded;
                StageDataIntegrator.Instance.OnLoadingError -= HandleLoadingError;
            }
            
            // 스테이지 시작
            if (StageDataIntegrator.Instance != null)
            {
                StageDataIntegrator.Instance.StartStageWithData(stageData, gameManager);
            }
            
            isInitializing = false;
        }
        
        /// <summary>
        /// 로딩 에러 처리
        /// </summary>
        private void HandleLoadingError(string errorMessage)
        {
            Debug.LogError($"[SingleGameBootstrap] 스테이지 데이터 로딩 실패: {errorMessage}");
            
            // 이벤트 구독 해제
            if (StageDataIntegrator.Instance != null)
            {
                StageDataIntegrator.Instance.OnStageDataLoaded -= HandleStageDataLoaded;
                StageDataIntegrator.Instance.OnLoadingError -= HandleLoadingError;
            }
            
            // 테스트 모드로 대체
            Debug.Log("[SingleGameBootstrap] 테스트 모드로 대체");
            InitializeTestMode();
        }
        
        void OnDestroy()
        {
            // 이벤트 구독 해제
            if (StageDataIntegrator.Instance != null)
            {
                StageDataIntegrator.Instance.OnStageDataLoaded -= HandleStageDataLoaded;
                StageDataIntegrator.Instance.OnLoadingError -= HandleLoadingError;
            }
        }
    }
}
