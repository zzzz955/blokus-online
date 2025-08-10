using UnityEngine;
using BlokusUnity.Data;
using BlokusUnity.Game;

namespace BlokusUnity.Data
{
    /// <summary>
    /// 스테이지 데이터 전역 관리자
    /// 씬 전환시 데이터 전달 및 보존
    /// </summary>
    public class StageDataManager : MonoBehaviour
    {
        [Header("Stage Management")]
        [SerializeField] private StageManager stageManager;
        
        // 싱글톤 인스턴스
        public static StageDataManager Instance { get; private set; }
        
        // 현재 선택된 스테이지
        private StageData currentSelectedStage;
        private int currentSelectedStageNumber;

        void Awake()
        {
            // 싱글톤 패턴 + DontDestroyOnLoad
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // StageManager 초기화
            if (stageManager == null)
            {
                // TODO: Resources에서 StageManager 로드
                // stageManager = Resources.Load<StageManager>("StageManager");
                Debug.LogWarning("StageManager가 설정되지 않았습니다!");
            }
        }

        /// <summary>
        /// 스테이지 선택
        /// </summary>
        public void SelectStage(int stageNumber)
        {
            currentSelectedStageNumber = stageNumber;
            
            if (stageManager != null)
            {
                currentSelectedStage = stageManager.GetStageData(stageNumber);
                
                if (currentSelectedStage != null)
                {
                    Debug.Log($"스테이지 {stageNumber} 선택: {currentSelectedStage.stageName}");
                }
                else
                {
                    Debug.LogError($"스테이지 {stageNumber} 데이터를 찾을 수 없습니다!");
                    CreateTestStage(stageNumber);
                }
            }
            else
            {
                Debug.LogWarning("StageManager가 없어서 테스트 스테이지를 생성합니다.");
                CreateTestStage(stageNumber);
            }
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
        /// 스테이지 매니저 반환
        /// </summary>
        public StageManager GetStageManager()
        {
            return stageManager;
        }

        /// <summary>
        /// 싱글게임매니저에 데이터 전달
        /// </summary>
        public void PassDataToSingleGameManager()
        {
            if (currentSelectedStage != null && stageManager != null)
            {
                // SingleGameManager의 static 프로퍼티에 데이터 설정
                SingleGameManager.SetStageContext(currentSelectedStage.stageNumber, this);
                
                Debug.Log($"SingleGameManager에 스테이지 데이터 전달: {currentSelectedStage.stageName}");
            }
            else
            {
                Debug.LogError("전달할 스테이지 데이터가 없습니다!");
            }
        }

        /// <summary>
        /// 테스트용 스테이지 생성 (개발 중)
        /// </summary>
        private void CreateTestStage(int stageNumber)
        {
            // 런타임에서 스테이지 데이터 생성 (개발용)
            currentSelectedStage = ScriptableObject.CreateInstance<StageData>();
            currentSelectedStage.stageNumber = stageNumber;
            currentSelectedStage.stageName = $"테스트 스테이지 {stageNumber}";
            currentSelectedStage.stageDescription = "개발용 테스트 스테이지입니다.";
            currentSelectedStage.difficulty = 1;
            
            // 기본 블록 설정 (모든 블록 사용 가능)
            currentSelectedStage.availableBlocks = new System.Collections.Generic.List<BlokusUnity.Common.BlockType>();
            for (int i = 1; i <= 21; i++)
            {
                currentSelectedStage.availableBlocks.Add((BlokusUnity.Common.BlockType)i);
            }
            
            // 점수 설정
            currentSelectedStage.optimalScore = 100;
            currentSelectedStage.threeStar = 100;
            currentSelectedStage.twoStar = 80;
            currentSelectedStage.oneStar = 50;
            
            currentSelectedStage.maxUndoCount = 3;
            
            Debug.Log($"테스트 스테이지 {stageNumber} 생성됨");
        }

        /// <summary>
        /// 스테이지 완료 처리
        /// </summary>
        public void CompleteStage(int stageNumber, int score, int stars)
        {
            if (stageManager != null)
            {
                var progress = stageManager.GetStageProgress(stageNumber);
                progress.UpdateProgress(score, stars);
                
                // 다음 스테이지 언락
                stageManager.UnlockNextStage(stageNumber);
                
                Debug.Log($"스테이지 {stageNumber} 완료: {score}점, {stars}별");
            }
        }
    }
}