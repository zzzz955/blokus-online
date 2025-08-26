using Shared.Models;
namespace Features.Single.Gameplay.Skins
{
    /// <summary>
    /// Block skin identifier enum
    /// Migration Plan: DB에서 상수값(enum) 수신 → BlockSkinId 매핑
    /// </summary>
    public enum BlockSkinId
    {
        Default = 0,
        Classic = 1,
        Neon = 2,
        Wood = 3,
        Metal = 4,
        Pastel = 5,
        Galaxy = 6,
        Ocean = 7,
        Forest = 8,
        Fire = 9,
        Ice = 10
    }
}