// Assets/_Project/Scripts/Shared/Models/GameEndResult.cs
using System;
using UnityEngine;

namespace Shared.Models
{
    /// <summary>
    ///  GameEndResult - 단일 진실원천 (Single Source of Truth)
    /// 
    /// 게임 종료 시 최종 결과를 한 번만 계산하고, 모든 UI/네트워크/진행도 업데이트가
    /// 이 객체의 데이터를 참조하도록 하여 일관성을 보장합니다.
    /// 
    /// 핵심 규칙:
    /// - isCleared = stars >= 1 (절대 변경 금지)
    /// - 0별 = 실패, 1별 이상 = 클리어
    /// - 모든 UI 표시와 네트워크 호출은 이 객체 기반으로만 수행
    /// </summary>
    [System.Serializable]
    public class GameEndResult
    {
        [Header("Game Context")]
        public int stageNumber;
        public string stageName;
        
        [Header("Performance")]
        public int finalScore;
        public int optimalScore;
        public float elapsedTime;
        
        [Header("Results - Single Source of Truth")]
        public int stars;           // 0-3: 서버 기준 별점
        public bool isCleared;      // stars >= 1에서만 true
        public bool isNewBest;      // 최고 점수 갱신 여부
        
        [Header("Metadata")]
        public DateTime completedAt;
        public string endReason;    // "All blocks placed", "No valid moves", "Exit requested"

        /// <summary>
        /// 안전한 생성자 - 별점 기반 클리어 상태 자동 계산
        /// </summary>
        public GameEndResult(int stageNumber, string stageName, int finalScore, int optimalScore, 
                           float elapsedTime, int stars, bool isNewBest = false, string endReason = "")
        {
            this.stageNumber = stageNumber;
            this.stageName = stageName ?? $"Stage {stageNumber}";
            this.finalScore = finalScore;
            this.optimalScore = optimalScore;
            this.elapsedTime = elapsedTime;
            this.stars = Mathf.Clamp(stars, 0, 3);
            
            //  핵심 규칙: 별점 기반 클리어 상태 결정 (절대 변경 금지)
            this.isCleared = this.stars >= 1;
            
            this.isNewBest = isNewBest;
            this.endReason = endReason;
            this.completedAt = DateTime.Now;
            
            // 검증 로그
            Debug.Log($"[GameEndResult] 생성: Stage={stageNumber}, Score={finalScore}, Stars={this.stars}, " +
                     $"IsCleared={this.isCleared}, Reason='{endReason}'");
                     
            //  규칙 위반 검사
            if (this.stars == 0 && this.isCleared)
            {
                Debug.LogError($"[GameEndResult]  규칙 위반: 0별인데 isCleared=true - Stage {stageNumber}");
            }
            if (this.stars > 0 && !this.isCleared)
            {
                Debug.LogError($"[GameEndResult]  규칙 위반: {this.stars}별인데 isCleared=false - Stage {stageNumber}");
            }
        }

        /// <summary>
        /// 빈 결과 (에러 상황용)
        /// </summary>
        public static GameEndResult CreateEmpty(int stageNumber, string reason = "Unknown error")
        {
            return new GameEndResult(stageNumber, $"Stage {stageNumber}", 0, 0, 0f, 0, false, reason);
        }

        /// <summary>
        /// 결과 요약 문자열 (로깅/디버깅용)
        /// </summary>
        public override string ToString()
        {
            return $"GameEndResult[Stage={stageNumber}, Score={finalScore}/{optimalScore}, " +
                   $"Stars={stars}, Cleared={isCleared}, Time={elapsedTime:F1}s, Reason='{endReason}']";
        }

        /// <summary>
        /// 성공 여부 검증 (UI 표시용)
        /// </summary>
        public string GetResultTitle()
        {
            return isCleared ? "클리어 성공!" : "클리어 실패";
        }

        /// <summary>
        /// 결과 색상 (UI 표시용)
        /// </summary>
        public Color GetResultColor()
        {
            return isCleared ? Color.green : Color.red;
        }

        /// <summary>
        /// 완료/진행도 API 호출 여부 결정
        /// </summary>
        public bool ShouldCallCompletionAPI()
        {
            return isCleared; // 1별 이상에서만 완료 API 호출
        }

        /// <summary>
        /// 진행도 업데이트 API 호출 여부 결정  
        /// </summary>
        public bool ShouldCallProgressAPI()
        {
            return !isCleared; // 0별(실패)에서만 진행도 API 호출
        }

        /// <summary>
        /// 다음 스테이지 해금 가능 여부
        /// </summary>
        public bool CanUnlockNextStage()
        {
            return isCleared; // 1별 이상에서만 다음 스테이지 해금
        }
    }
}