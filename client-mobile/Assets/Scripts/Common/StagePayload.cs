// Assets/Scripts/Common/StagePayload.cs
using BlokusUnity.Common;

namespace BlokusUnity.Common
{
    /// <summary>
    /// 싱글 게임 시작에 필요한 페이로드 (네트워크/캐시에서 주입)
    /// </summary>
    public sealed class StagePayload
    {
        public int BoardSize = 20;
        public BlockType[] AvailableBlocks;   // null이면 기본 풀세트 사용
        public string LayoutSeedOrJson;       // 퍼즐 레이아웃/시드 (옵션)
        public string StageName;
        public int ParScore = 0;              // 별 계산 기준 (옵션)
    }
}
