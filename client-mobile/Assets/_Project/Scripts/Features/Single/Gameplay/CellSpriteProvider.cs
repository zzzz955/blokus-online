using UnityEngine;
using Shared.Models;

namespace Features.Single.Gameplay
{
    /// <summary>
    /// PlayerColor에 따른 셀 스프라이트 매핑을 관리하는 중앙 시스템
    /// Inspector에서 설정 가능하며, GameBoard와 BlockButton에서 공통 사용
    /// </summary>
    [System.Serializable]
    public class CellSpriteProvider : MonoBehaviour
    {
        [Header("Cell Sprites - Stone Style")]
        [SerializeField] private Sprite bubbleSprite;      // PlayerColor.None
        [SerializeField] private Sprite blueStoneSprite;   // PlayerColor.Blue  
        [SerializeField] private Sprite redStoneSprite;    // PlayerColor.Red
        [SerializeField] private Sprite yellowStoneSprite; // PlayerColor.Yellow
        [SerializeField] private Sprite greenStoneSprite;  // PlayerColor.Green
        [SerializeField] private Sprite brownStoneSprite;  // PlayerColor.Obstacle
        
        [Header("Alternative - Diamond Style (Optional)")]
        [SerializeField] private bool useDiamondVariant = false;
        [SerializeField] private Sprite blueDiamondSprite;
        [SerializeField] private Sprite redDiamondSprite;
        [SerializeField] private Sprite yellowDiamondSprite;
        [SerializeField] private Sprite greenDiamondSprite;
        [SerializeField] private Sprite brownDiamondSprite;

        [Header("Fallback")]
        [SerializeField] private Sprite fallbackSprite;

        /// <summary>
        /// PlayerColor에 해당하는 스프라이트 반환
        /// </summary>
        public Sprite GetSprite(PlayerColor playerColor)
        {
            Sprite result;
            
            if (useDiamondVariant)
            {
                result = playerColor switch
                {
                    PlayerColor.None => bubbleSprite,
                    PlayerColor.Blue => blueDiamondSprite,
                    PlayerColor.Red => redDiamondSprite,
                    PlayerColor.Yellow => yellowDiamondSprite,
                    PlayerColor.Green => greenDiamondSprite,
                    PlayerColor.Obstacle => brownDiamondSprite,
                    _ => fallbackSprite
                };
            }
            else
            {
                result = playerColor switch
                {
                    PlayerColor.None => bubbleSprite,
                    PlayerColor.Blue => blueStoneSprite,
                    PlayerColor.Red => redStoneSprite,
                    PlayerColor.Yellow => yellowStoneSprite,
                    PlayerColor.Green => greenStoneSprite,
                    PlayerColor.Obstacle => brownStoneSprite,
                    _ => fallbackSprite
                };
            }
            
            // 디버그 로그
            // Debug.Log($"[CellSpriteProvider] {playerColor} → {(result != null ? result.name : "NULL")} (Diamond: {useDiamondVariant})");
            return result;
        }

        /// <summary>
        /// PlayerColor에 해당하는 스프라이트 반환 (디버깅 포함)
        /// </summary>
        public Sprite GetSpriteWithDebug(PlayerColor playerColor)
        {
            Sprite result = GetSprite(playerColor);
            if (result == null)
            {
                Debug.LogWarning($"[CellSpriteProvider] {playerColor}에 대한 스프라이트가 null입니다! fallbackSprite 사용");
            }
            else
            {
                Debug.Log($"[CellSpriteProvider] {playerColor} → {result.name}");
            }
            return result;
        }
        
        /// <summary>
        /// 스프라이트가 올바르게 설정되었는지 검증
        /// </summary>
        public bool ValidateSprites()
        {
            bool isValid = true;
            
            if (bubbleSprite == null)
            {
                Debug.LogWarning("[CellSpriteProvider] bubbleSprite (None)이 설정되지 않았습니다.");
                isValid = false;
            }
            
            if (!useDiamondVariant)
            {
                if (blueStoneSprite == null) { Debug.LogWarning("[CellSpriteProvider] blueStoneSprite가 설정되지 않았습니다."); isValid = false; }
                if (redStoneSprite == null) { Debug.LogWarning("[CellSpriteProvider] redStoneSprite가 설정되지 않았습니다."); isValid = false; }
                if (yellowStoneSprite == null) { Debug.LogWarning("[CellSpriteProvider] yellowStoneSprite가 설정되지 않았습니다."); isValid = false; }
                if (greenStoneSprite == null) { Debug.LogWarning("[CellSpriteProvider] greenStoneSprite가 설정되지 않았습니다."); isValid = false; }
                if (brownStoneSprite == null) { Debug.LogWarning("[CellSpriteProvider] brownStoneSprite가 설정되지 않았습니다."); isValid = false; }
            }
            else
            {
                if (blueDiamondSprite == null) { Debug.LogWarning("[CellSpriteProvider] blueDiamondSprite가 설정되지 않았습니다."); isValid = false; }
                if (redDiamondSprite == null) { Debug.LogWarning("[CellSpriteProvider] redDiamondSprite가 설정되지 않았습니다."); isValid = false; }
                if (yellowDiamondSprite == null) { Debug.LogWarning("[CellSpriteProvider] yellowDiamondSprite가 설정되지 않았습니다."); isValid = false; }
                if (greenDiamondSprite == null) { Debug.LogWarning("[CellSpriteProvider] greenDiamondSprite가 설정되지 않았습니다."); isValid = false; }
                if (brownDiamondSprite == null) { Debug.LogWarning("[CellSpriteProvider] brownDiamondSprite가 설정되지 않았습니다."); isValid = false; }
            }
            
            if (fallbackSprite == null)
            {
                Debug.LogWarning("[CellSpriteProvider] fallbackSprite가 설정되지 않았습니다.");
                isValid = false;
            }
            
            return isValid;
        }

        private void Start()
        {
            ValidateSprites();
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Inspector에서 스프라이트 검증
        /// </summary>
        [ContextMenu("Validate Sprites")]
        private void ValidateSpritesInEditor()
        {
            bool isValid = ValidateSprites();
            if (isValid)
            {
                Debug.Log("[CellSpriteProvider] 모든 스프라이트가 올바르게 설정되었습니다!");
            }
            else
            {
                Debug.LogError("[CellSpriteProvider] 일부 스프라이트가 누락되었습니다. 위의 경고를 확인하세요.");
            }
        }
        
        /// <summary>
        /// Inspector에서 자동으로 스프라이트를 찾아서 할당하는 헬퍼 메소드
        /// </summary>
        [ContextMenu("Auto-Assign Sprites from Assets/_Project/Sprites/Blocks")]
        private void AutoAssignSprites()
        {
            // 스프라이트 자동 할당 로직
            string basePath = "Assets/_Project/Sprites/Blocks/";
            
            bubbleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "bubble.png");
            blueStoneSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "blueStone.png");
            redStoneSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "redStone.png");
            yellowStoneSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "yellowStone.png");
            greenStoneSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "greenStone.png");
            brownStoneSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "brownStone.png");
            
            blueDiamondSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "blueDimond.png");
            redDiamondSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "redDimond.png");
            yellowDiamondSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "yellowDimond.png");
            greenDiamondSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "greenDimond.png");
            brownDiamondSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(basePath + "brownDimond.png");
            
            // Fallback으로 bubble 사용
            fallbackSprite = bubbleSprite;
            
            Debug.Log("[CellSpriteProvider] 스프라이트 자동 할당 완료!");
            UnityEditor.EditorUtility.SetDirty(this);
        }
        #endif
    }
}