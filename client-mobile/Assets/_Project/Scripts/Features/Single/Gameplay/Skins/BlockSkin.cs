using UnityEngine;
using Shared.Models;
namespace Features.Single.Gameplay.Skins{
    /// <summary>
    /// Block skin data container
    /// Migration Plan: Phase 1로 enum 매핑 + 내부 리소스 할당(Registry/Resources)
    /// </summary>
    [CreateAssetMenu(menuName = "Blokus/Block Skin", fileName = "BlockSkin_Default")]
    public class BlockSkin : ScriptableObject
    {
        [Header("Skin Information")]
        public BlockSkinId skinId = BlockSkinId.Default;
        public string skinName = "Default";
        public string skinDescription = "Basic skin";

        [Header("Player Tints")]
        public Color blue   = new Color(0.35f, 0.60f, 1.00f, 1f);
        public Color yellow = new Color(1.00f, 0.85f, 0.30f, 1f);
        public Color red    = new Color(1.00f, 0.40f, 0.40f, 1f);
        public Color green  = new Color(0.40f, 0.90f, 0.55f, 1f);

        [Header("Resources (Phase 1: Resources folder)")]
        public Material blockMaterial;
        public Texture2D blockTexture;
        public GameObject blockPrefab;

        [Header("Phase 2 Preparation: Addressables")]
        [Tooltip("Addressable key for future Addressables integration")]
        public string addressableKey;

        /// <summary>
        /// Get player color tint
        /// </summary>
        public Color GetTint(PlayerColor p) => p switch
        {
            PlayerColor.Blue   => blue,
            PlayerColor.Yellow => yellow,
            PlayerColor.Red    => red,
            PlayerColor.Green  => green,
            _ => Color.white
        };

        /// <summary>
        /// Validate skin data
        /// Migration Plan: 잘못된 enum 수신 시 디폴트 스킨 + 오류 토스트
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(skinName) && 
                   blockMaterial != null; // Basic validation
        }

        /// <summary>
        /// Get display name for UI
        /// </summary>
        public string GetDisplayName()
        {
            return string.IsNullOrEmpty(skinName) ? skinId.ToString() : skinName;
        }
    }
}
