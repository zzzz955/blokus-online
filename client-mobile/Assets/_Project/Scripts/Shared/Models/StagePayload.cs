// Assets/Scripts/Common/StagePayload.cs
using System.Collections.Generic;
namespace Shared.Models{
    /// <summary>
    /// 싱글 게임 시작에 필요한 페이로드 (네트워크/캐시에서 주입)
    /// API 기반 스테이지 데이터와 호환
    /// </summary>
    public sealed class StagePayload
    {
        public int BoardSize = 20;
        public BlockType[] AvailableBlocks;   // null이면 기본 풀세트 사용
        public string StageName;
        public int ParScore = 0;              // 별 계산 기준 (옵션)
        
        // API 확장 필드들
        public int StageNumber = 0;           // API에서 받은 스테이지 번호
        public int Difficulty = 1;            // 난이도 (1-5)
        public int TimeLimit = 0;             // 제한시간 (0이면 무제한)
        public int MaxUndoCount = 5;          // 최대 언두 횟수
        public InitialBoardData InitialBoard; // 초기 보드 상태 (파싱된 데이터)
        public int[] InitialBoardPositions;   // 원시 initial_board_state 데이터 (GameLogic.SetInitialBoardState용)
    }
    
    /// <summary>
    /// 파싱된 초기 보드 데이터
    /// </summary>
    public class InitialBoardData
    {
        public List<Position> obstacles;          // 장애물 위치들
        public List<BlockPlacement> preplaced;    // 사전 배치된 블록들
    }
}
