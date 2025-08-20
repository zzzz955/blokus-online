using System;

namespace Shared.Models
{
    // ========================================
    // 사용자 정보 구조체
    // ========================================

    /// <summary>
    /// 사용자 정보
    /// </summary>
    [System.Serializable]
    public class UserInfo
    {
        public string username = "익명";       // 사용자명
        public string display_name = "익명";
        public int level = 1;                  // 경험치 레벨 (게임 수에 따라 증가)
        public int totalGames = 0;             // 총 게임 수
        public int wins = 0;                   // 승리 수
        public int losses = 0;                 // 패배 수
        public int averageScore = 0;           // 평균 점수
        public bool isOnline = true;           // 온라인 상태
        public string status = "로비";         // "로비", "게임중", "자리비움"
        
        // 싱글플레이어 진행도
        public int maxStageCompleted = 0;      // 최대 클리어한 스테이지 번호

        /// <summary>
        /// 승률 계산
        /// </summary>
        public double GetWinRate()
        {
            return totalGames > 0 ? (double)wins / totalGames * 100.0 : 0.0;
        }

        /// <summary>
        /// 레벨 계산 (10게임당 1레벨)
        /// </summary>
        public int CalculateLevel()
        {
            return (totalGames / 10) + 1;
        }
    }
    
    /// <summary>
    /// 사용자 스테이지 진행도 - API 응답 구조에 맞게 업데이트
    /// </summary>
    [System.Serializable]
    public struct UserStageProgress
    {
        public int stage_number;
        public bool is_completed;
        public int stars_earned;
        public int best_score;
        public int best_completion_time;
        public int total_attempts;
        public int successful_attempts;
        public string first_played_at;
        public string first_completed_at;
        public string last_played_at;
        
        // Unity에서 사용하기 위한 편의 프로퍼티
        public int stageNumber => stage_number;
        public bool isCompleted => is_completed;
        public int starsEarned => stars_earned;
        public int bestScore => best_score;
        public int bestTime => best_completion_time;
        public int totalAttempts => total_attempts;
        public int successfulAttempts => successful_attempts;
    }
}