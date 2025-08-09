using System.Collections.Generic;
using UnityEngine;
using BlokusUnity.Common;

namespace BlokusUnity.Data
{
    /// <summary>
    /// 싱글플레이 스테이지 데이터
    /// ScriptableObject로 관리하여 에디터에서 쉽게 편집 가능
    /// </summary>
    [CreateAssetMenu(fileName = "Stage_", menuName = "Blokus/Stage Data")]
    public class StageData : ScriptableObject
    {
        [Header("Stage Info")]
        public int stageNumber;
        public string stageName = "스테이지";
        public string stageDescription = "블록을 최대한 많이 배치하세요!";
        
        [Header("Difficulty")]
        [Range(1, 5)]
        public int difficulty = 1;
        public Sprite stageIcon;
        
        [Header("Board Setup")]
        [Tooltip("초기 보드 상태 (미리 배치된 블록들)")]
        public List<BlockPlacement> initialBlocks = new List<BlockPlacement>();
        
        [Header("Player Blocks")]
        [Tooltip("플레이어가 사용할 수 있는 블록 타입들")]
        public List<BlockType> availableBlocks = new List<BlockType>();
        
        [Header("Scoring")]
        [Tooltip("이론상 최고 점수 (별 3개 기준)")]
        public int optimalScore = 100;
        
        [Tooltip("별점 기준 점수")]
        public int threeStar = 100;  // 100%
        public int twoStar = 80;     // 80%
        public int oneStar = 50;     // 50%
        
        [Header("Game Rules")]
        [Tooltip("제한 시간 (0이면 무제한)")]
        public int timeLimit = 0;
        
        [Tooltip("최대 실행취소 횟수")]
        public int maxUndoCount = 3;
        
        /// <summary>
        /// 점수에 따른 별점 계산
        /// </summary>
        public int CalculateStarRating(int playerScore)
        {
            float percentage = (float)playerScore / optimalScore * 100f;
            
            if (percentage >= threeStar) return 3;
            if (percentage >= twoStar) return 2;
            if (percentage >= oneStar) return 1;
            return 0;
        }
        
        /// <summary>
        /// 보드에 초기 블록들 배치
        /// </summary>
        public void ApplyInitialBoard(GameLogic gameLogic)
        {
            gameLogic.clearBoard();
            
            foreach (var blockPlacement in initialBlocks)
            {
                gameLogic.placeBlock(blockPlacement);
            }
        }
        
        /// <summary>
        /// 개발용: 스테이지 검증
        /// </summary>
        [ContextMenu("Validate Stage")]
        private void ValidateStage()
        {
            if (availableBlocks.Count == 0)
            {
                Debug.LogWarning($"Stage {stageNumber}: 사용 가능한 블록이 없습니다!");
            }
            
            if (optimalScore <= 0)
            {
                Debug.LogWarning($"Stage {stageNumber}: 최적 점수가 설정되지 않았습니다!");
            }
            
            // 초기 블록 배치 유효성 검사
            // TODO: GameLogic으로 검증
            
            Debug.Log($"Stage {stageNumber} 검증 완료");
        }
    }
    
    /// <summary>
    /// 스테이지 진행 상황 저장
    /// </summary>
    [System.Serializable]
    public class StageProgress
    {
        public int stageNumber;
        public bool isUnlocked = false;
        public bool isCompleted = false;
        public int bestScore = 0;
        public int starCount = 0;
        public int playCount = 0;
        
        /// <summary>
        /// 스테이지 결과 업데이트
        /// </summary>
        public void UpdateProgress(int newScore, int newStars)
        {
            isCompleted = true;
            playCount++;
            
            if (newScore > bestScore)
            {
                bestScore = newScore;
            }
            
            if (newStars > starCount)
            {
                starCount = newStars;
            }
        }
    }
    
    /// <summary>
    /// 전체 스테이지 관리자
    /// </summary>
    [CreateAssetMenu(fileName = "StageManager", menuName = "Blokus/Stage Manager")]
    public class StageManager : ScriptableObject
    {
        [Header("All Stages")]
        public List<StageData> allStages = new List<StageData>();
        
        [Header("Progress Data")]
        public List<StageProgress> stageProgresses = new List<StageProgress>();
        
        /// <summary>
        /// 스테이지 데이터 가져오기
        /// </summary>
        public StageData GetStageData(int stageNumber)
        {
            return allStages.Find(stage => stage.stageNumber == stageNumber);
        }
        
        /// <summary>
        /// 스테이지 진행 상황 가져오기
        /// </summary>
        public StageProgress GetStageProgress(int stageNumber)
        {
            var progress = stageProgresses.Find(p => p.stageNumber == stageNumber);
            if (progress == null)
            {
                progress = new StageProgress { stageNumber = stageNumber };
                stageProgresses.Add(progress);
            }
            return progress;
        }
        
        /// <summary>
        /// 다음 스테이지 언락
        /// </summary>
        public void UnlockNextStage(int currentStage)
        {
            var nextStageProgress = GetStageProgress(currentStage + 1);
            nextStageProgress.isUnlocked = true;
        }
        
        /// <summary>
        /// 총 획득 별 개수
        /// </summary>
        public int GetTotalStars()
        {
            int total = 0;
            foreach (var progress in stageProgresses)
            {
                total += progress.starCount;
            }
            return total;
        }
        
        /// <summary>
        /// 클리어한 스테이지 개수
        /// </summary>
        public int GetCompletedStageCount()
        {
            int count = 0;
            foreach (var progress in stageProgresses)
            {
                if (progress.isCompleted) count++;
            }
            return count;
        }
    }
}