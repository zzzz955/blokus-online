using System.Collections.Generic;
using UnityEngine;
using App.Core;
using System;
namespace Shared.Models{
    /// <summary>
    /// 싱글플레이 스테이지 데이터 - API 서버에서 생성된 데이터 구조
    /// 이전 ScriptableObject 방식에서 API 기반으로 변경
    /// </summary>
    [System.Serializable]
    public class StageData
    {
        // API 응답 데이터 구조에 맞게 업데이트
        public int stage_number;
        public string stage_title;
        public int difficulty;
        public int optimal_score;
        public int time_limit;
        public int max_undo_count;
        public int[] available_blocks;
        public InitialBoardState initial_board_state;
        public string hints;
        public string stage_description;
        public string thumbnail_url;  // API에서 받아오는 썸네일 이미지 URL

        // 별점 기준 점수들 (계산된 값)
        public int threeStar => optimal_score; // 100%
        public int twoStar => Mathf.RoundToInt(optimal_score * 0.7f); // 70%
        public int oneStar => Mathf.RoundToInt(optimal_score * 0.5f); // 50%

        // 사용 가능한 블록들 (BlockType 리스트로 변환)
        public System.Collections.Generic.List<Shared.Models.BlockType> availableBlocks
        {
            get
            {
                var blockList = new System.Collections.Generic.List<Shared.Models.BlockType>();
                if (available_blocks != null)
                {
                    foreach (var blockId in available_blocks)
                    {
                        if (blockId >= 1 && blockId <= 21)
                        {
                            blockList.Add((Shared.Models.BlockType)(byte)blockId);
                        }
                    }
                }
                return blockList;
            }
        }

        /// <summary>
        /// 점수에 따른 별점 계산 - API 서버와 동일한 로직
        /// </summary>
        public int CalculateStarRating(int playerScore)
        {
            float percentage = (float)playerScore / optimal_score;

            if (percentage >= 0.9f) return 3;      // 90% 이상: 3별
            if (percentage >= 0.7f) return 2;      // 70% 이상: 2별  
            if (percentage >= 0.5f) return 1;      // 50% 이상: 1별
            return 0;                               // 50% 미만: 0별
        }

        /// <summary>
        /// 보드에 초기 블록들 배치 - INTEGER[] 형식 사용
        /// </summary>
        public void ApplyInitialBoard(GameLogic gameLogic)
        {
            gameLogic.ClearBoard();

            if (initial_board_state != null)
            {
                var placements = initial_board_state.GetPlacements();
                
                foreach (var placement in placements)
                {
                    var blockPlacement = new BlockPlacement(
                        (BlockType)(byte)placement.block_type,
                        new Position(placement.row, placement.col),
                        (Rotation)(byte)placement.rotation,
                        (FlipState)(byte)placement.flip_state,
                        (PlayerColor)(byte)placement.color
                    );
                    gameLogic.PlaceBlock(blockPlacement);
                }
                
                Debug.Log($"초기 보드 배치 완료: {placements.Length}개 블록 (INTEGER[] 형식)");
            }
        }

        /// <summary>
        /// 개발용: 스테이지 검증 - API 데이터 기반
        /// </summary>
        public bool ValidateStage()
        {
            bool isValid = true;

            if (available_blocks == null || available_blocks.Length == 0)
            {
                Debug.LogWarning($"Stage {stage_number}: 사용 가능한 블록이 없습니다!");
                isValid = false;
            }

            if (optimal_score <= 0)
            {
                Debug.LogWarning($"Stage {stage_number}: 최적 점수가 설정되지 않았습니다!");
                isValid = false;
            }

            Debug.Log($"Stage {stage_number} 검증 {(isValid ? "성공" : "실패")}");
            return isValid;
        }

        public string GetAbsoluteThumbnailUrl(string baseUrl) 
        {
            return baseUrl + thumbnail_url;
        }
    }
    
    /// <summary>
    /// API 데이터에서 사용되는 초기 보드 상태 구조
    /// INTEGER[] 형식 사용
    /// </summary>
    [System.Serializable]
    public class InitialBoardState
    {
        // INTEGER[] format from database migration
        // Format: color_index * 400 + (row * 20 + col)
        // Colors: 검정(0), 파랑(1), 노랑(2), 빨강(3), 초록(4)
        public int[] boardPositions;
        
        /// <summary>
        /// Get placement data from INTEGER[] format
        /// </summary>
        public BlockPlacementData[] GetPlacements()
        {
            if (boardPositions == null || boardPositions.Length == 0)
                return new BlockPlacementData[0];
            
            return ConvertIntegerArrayToPlacements();
        }
        
        /// <summary>
        /// Convert INTEGER[] format to BlockPlacementData array
        /// </summary>
        private BlockPlacementData[] ConvertIntegerArrayToPlacements()
        {
            var placementList = new List<BlockPlacementData>();
            
            foreach (int encoded in boardPositions)
            {
                // Decode: color_index * 400 + (row * 20 + col)
                int colorIndex = encoded / 400;
                int position = encoded % 400;
                int col = position % 20;
                int row = position / 20;
                
                // Create placement data
                // Single-cell blocks (obstacles or preset pieces)
                var placement = new BlockPlacementData
                {
                    block_type = 1, // Single cell block type
                    row = row,
                    col = col,
                    rotation = 0,
                    flip_state = 0,
                    color = colorIndex
                };
                
                placementList.Add(placement);
            }
            
            return placementList.ToArray();
        }
    }
    
    /// <summary>
    /// API에서 사용되는 블록 배치 데이터
    /// </summary>
    [System.Serializable]
    public class BlockPlacementData
    {
        public int block_type;
        public int row;
        public int col;
        public int rotation;
        public int flip_state;
        public int color;
    }
    
    /// <summary>
    /// API에서 사용되는 특별 규칙 구조
    /// </summary>
    [System.Serializable]
    public class SpecialRules
    {
        public bool time_pressure;
        public float bonus_multiplier;
        public bool limited_blocks;
        public int max_block_placements;
    }
    
    /// <summary>
    /// API에서 사용되는 생성 정보 구조
    /// </summary>
    [System.Serializable]
    public class GenerationInfo
    {
        public string algorithm;
        public string seed;
        public float complexity_score;
        public bool is_solvable;
    }
    
    /// <summary>
    /// 스테이지 진행 상황 저장 - API 데이터에 맞게 업데이트
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
    /// 전체 스테이지 관리자 - API 기반으로 업데이트 (더 이상 ScriptableObject 사용 안함)
    /// </summary>
    [System.Serializable]
    public class StageManager
    {
        public Dictionary<int, StageData> cachedStageData = new Dictionary<int, StageData>();
        public List<StageProgress> stageProgresses = new List<StageProgress>();
        
        /// <summary>
        /// 스테이지 데이터 가져오기 (캐시에서)
        /// </summary>
        public StageData GetStageData(int stageNumber)
        {
            if (cachedStageData.TryGetValue(stageNumber, out StageData stageData))
            {
                return stageData;
            }
            
            // 캐시에 없으면 null 반환 (API에서 로드 해야 함)
            return null;
        }
        
        /// <summary>
        /// 스테이지 데이터 캐시에 추가
        /// </summary>
        public void CacheStageData(StageData stageData)
        {
            if (stageData != null)
            {
                cachedStageData[stageData.stage_number] = stageData;
            }
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