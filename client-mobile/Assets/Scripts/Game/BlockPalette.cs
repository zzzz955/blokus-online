using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.Common;

namespace BlokusUnity.Game
{
    /// <summary>
    /// 블록 선택 팔레트 UI 시스템
    /// 사용 가능한 블록들을 표시하고 선택 처리
    /// </summary>
    public class BlockPalette : MonoBehaviour
    {
        [Header("UI 설정")]
        [SerializeField] private Transform blockContainer;
        [SerializeField] private GameObject blockButtonPrefab;
        [SerializeField] private ScrollRect scrollRect;
        
        [Header("블록 표시 설정")]
        [SerializeField] private float blockScale = 0.8f;
        [SerializeField] private float cellSize = 20f;
        [SerializeField] private Color selectedColor = Color.cyan;
        [SerializeField] private Color availableColor = Color.white;
        [SerializeField] private Color usedColor = Color.gray;
        
        [Header("플레이어 색상")]
        [SerializeField] private PlayerColor playerColor = PlayerColor.Blue;
        
        // 내부 데이터
        private List<BlockType> availableBlockTypes;
        private List<Block> playerBlocks;
        private Dictionary<BlockType, BlockButton> blockButtons;
        private BlockType selectedBlockType = BlockType.Single;
        private Block selectedBlock;
        
        // 이벤트
        public System.Action<Block> OnBlockSelected;
        public System.Action OnBlockDeselected;
        
        // ========================================
        // Unity 생명주기
        // ========================================
        
        void Awake()
        {
            blockButtons = new Dictionary<BlockType, BlockButton>();
            playerBlocks = new List<Block>();
        }
        
        // ========================================
        // 초기화
        // ========================================
        
        /// <summary>
        /// 플레이어 블록 세트로 팔레트 초기화
        /// </summary>
        public void InitializePalette(List<BlockType> blockTypes, PlayerColor player)
        {
            availableBlockTypes = new List<BlockType>(blockTypes);
            playerColor = player;
            
            CreatePlayerBlocks();
            CreateBlockButtons();
            
            Debug.Log($"블록 팔레트 초기화: {blockTypes.Count}개 블록, 플레이어 {player}");
        }
        
        /// <summary>
        /// 플레이어 블록들 생성
        /// </summary>
        private void CreatePlayerBlocks()
        {
            playerBlocks.Clear();
            
            foreach (var blockType in availableBlockTypes)
            {
                Block block = new Block(blockType, playerColor);
                playerBlocks.Add(block);
            }
        }
        
        /// <summary>
        /// 블록 버튼들 생성
        /// </summary>
        private void CreateBlockButtons()
        {
            // 기존 버튼들 정리
            ClearExistingButtons();
            
            foreach (var block in playerBlocks)
            {
                CreateBlockButton(block);
            }
            
            // 첫 번째 블록 자동 선택
            if (playerBlocks.Count > 0)
            {
                SelectBlock(playerBlocks[0].Type);
            }
        }
        
        /// <summary>
        /// 기존 버튼들 정리
        /// </summary>
        private void ClearExistingButtons()
        {
            blockButtons.Clear();
            
            if (blockContainer != null)
            {
                for (int i = blockContainer.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(blockContainer.GetChild(i).gameObject);
                }
            }
        }
        
        /// <summary>
        /// 개별 블록 버튼 생성
        /// </summary>
        private void CreateBlockButton(Block block)
        {
            GameObject buttonObj = CreateButtonObject();
            BlockButton blockButton = SetupBlockButton(buttonObj, block);
            
            blockButtons[block.Type] = blockButton;
        }
        
        /// <summary>
        /// 버튼 오브젝트 생성
        /// </summary>
        private GameObject CreateButtonObject()
        {
            if (blockButtonPrefab != null)
            {
                return Instantiate(blockButtonPrefab, blockContainer);
            }
            
            // 기본 버튼 생성
            GameObject buttonObj = new GameObject("BlockButton", typeof(RectTransform));
            buttonObj.transform.SetParent(blockContainer, false);
            
            // UI 컴포넌트들 추가
            var button = buttonObj.AddComponent<Button>();
            var image = buttonObj.AddComponent<Image>();
            image.color = availableColor;
            
            // 크기 설정
            var rectTransform = buttonObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = Vector2.one * (cellSize * 4); // 4x4 기본 크기
            
            return buttonObj;
        }
        
        /// <summary>
        /// 블록 버튼 설정
        /// </summary>
        private BlockButton SetupBlockButton(GameObject buttonObj, Block block)
        {
            var blockButton = buttonObj.GetComponent<BlockButton>();
            if (blockButton == null)
            {
                blockButton = buttonObj.AddComponent<BlockButton>();
            }
            
            blockButton.Initialize(block, this);
            DrawBlockOnButton(buttonObj, block);
            
            return blockButton;
        }
        
        /// <summary>
        /// 버튼에 블록 모양 그리기
        /// </summary>
        private void DrawBlockOnButton(GameObject buttonObj, Block block)
        {
            var shape = block.GetCurrentShape();
            var boundingRect = block.GetBoundingRect();
            
            // 블록 셀들을 버튼 위에 시각적으로 표현
            foreach (var pos in shape)
            {
                CreateBlockCell(buttonObj, pos, boundingRect);
            }
            
            // 블록 이름 라벨 추가
            CreateBlockLabel(buttonObj, block);
        }
        
        /// <summary>
        /// 블록 셀 시각화 생성
        /// </summary>
        private void CreateBlockCell(GameObject parent, Position pos, Block.BoundingRect bounds)
        {
            GameObject cellObj = new GameObject($"Cell_{pos.row}_{pos.col}", typeof(RectTransform));
            cellObj.transform.SetParent(parent.transform, false);
            
            var image = cellObj.AddComponent<Image>();
            image.color = GetPlayerColor(playerColor);
            
            var rectTransform = cellObj.GetComponent<RectTransform>();
            
            // 중앙 정렬을 위한 위치 계산
            float cellX = (pos.col - bounds.width * 0.5f + 0.5f) * cellSize * blockScale;
            float cellY = (pos.row - bounds.height * 0.5f + 0.5f) * cellSize * blockScale;
            
            rectTransform.anchoredPosition = new Vector2(cellX, cellY);
            rectTransform.sizeDelta = Vector2.one * cellSize * blockScale;
        }
        
        /// <summary>
        /// 블록 이름 라벨 생성
        /// </summary>
        private void CreateBlockLabel(GameObject parent, Block block)
        {
            GameObject labelObj = new GameObject("Label", typeof(RectTransform));
            labelObj.transform.SetParent(parent.transform, false);
            
            var text = labelObj.AddComponent<TextMeshProUGUI>();
            text.text = BlockFactory.GetBlockName(block.Type);
            text.color = Color.black;
            text.fontSize = 12;
            text.alignment = TextAlignmentOptions.Center;
            
            var rectTransform = labelObj.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = new Vector2(0, -30); // 하단 여백
        }
        
        // ========================================
        // 블록 선택 처리
        // ========================================
        
        /// <summary>
        /// 블록 선택 (BlockButton에서 호출)
        /// </summary>
        public void OnBlockButtonClicked(BlockType blockType)
        {
            SelectBlock(blockType);
        }
        
        /// <summary>
        /// 블록 선택 처리
        /// </summary>
        public void SelectBlock(BlockType blockType)
        {
            selectedBlockType = blockType;
            selectedBlock = GetBlock(blockType);
            
            UpdateButtonVisuals();
            OnBlockSelected?.Invoke(selectedBlock);
            
            Debug.Log($"블록 선택: {BlockFactory.GetBlockName(blockType)}");
        }
        
        /// <summary>
        /// 블록 선택 해제
        /// </summary>
        public void DeselectBlock()
        {
            selectedBlockType = BlockType.Single;
            selectedBlock = null;
            
            UpdateButtonVisuals();
            OnBlockDeselected?.Invoke();
        }
        
        /// <summary>
        /// 버튼 시각 상태 업데이트
        /// </summary>
        private void UpdateButtonVisuals()
        {
            foreach (var kvp in blockButtons)
            {
                var blockType = kvp.Key;
                var button = kvp.Value;
                
                if (button != null)
                {
                    Color buttonColor = GetButtonColor(blockType);
                    button.SetButtonColor(buttonColor);
                }
            }
        }
        
        /// <summary>
        /// 블록 타입에 따른 버튼 색상 결정
        /// </summary>
        private Color GetButtonColor(BlockType blockType)
        {
            if (blockType == selectedBlockType)
            {
                return selectedColor;
            }
            
            var block = GetBlock(blockType);
            return block != null && IsBlockUsed(block) ? usedColor : availableColor;
        }
        
        // ========================================
        // 블록 상태 관리
        // ========================================
        
        /// <summary>
        /// 블록 사용 처리
        /// </summary>
        public void MarkBlockAsUsed(BlockType blockType)
        {
            var block = GetBlock(blockType);
            if (block != null)
            {
                // 사용된 블록 표시 (실제로는 GameLogic에서 관리)
                UpdateButtonVisuals();
                Debug.Log($"블록 사용됨: {BlockFactory.GetBlockName(blockType)}");
            }
        }
        
        /// <summary>
        /// 블록이 사용되었는지 확인
        /// </summary>
        private bool IsBlockUsed(Block block)
        {
            // TODO: GameLogic과 연동하여 실제 사용 여부 확인
            return false;
        }
        
        /// <summary>
        /// 블록 타입으로 블록 인스턴스 가져오기
        /// </summary>
        private Block GetBlock(BlockType blockType)
        {
            return playerBlocks.Find(b => b.Type == blockType);
        }
        
        // ========================================
        // 블록 변환 처리
        // ========================================
        
        /// <summary>
        /// 선택된 블록 회전
        /// </summary>
        public void RotateSelectedBlock(bool clockwise = true)
        {
            if (selectedBlock != null)
            {
                if (clockwise)
                    selectedBlock.RotateClockwise();
                else
                    selectedBlock.RotateCounterClockwise();
                
                // 버튼 시각 업데이트
                if (blockButtons.TryGetValue(selectedBlockType, out BlockButton button))
                {
                    button.RefreshVisual();
                }
                
                OnBlockSelected?.Invoke(selectedBlock); // 변경 알림
                Debug.Log($"블록 회전: {selectedBlock.CurrentRotation}");
            }
        }
        
        /// <summary>
        /// 선택된 블록 뒤집기
        /// </summary>
        public void FlipSelectedBlock(bool horizontal = true)
        {
            if (selectedBlock != null)
            {
                if (horizontal)
                    selectedBlock.FlipHorizontal();
                else
                    selectedBlock.FlipVertical();
                
                // 버튼 시각 업데이트
                if (blockButtons.TryGetValue(selectedBlockType, out BlockButton button))
                {
                    button.RefreshVisual();
                }
                
                OnBlockSelected?.Invoke(selectedBlock); // 변경 알림
                Debug.Log($"블록 뒤집기: {selectedBlock.CurrentFlipState}");
            }
        }
        
        // ========================================
        // 공개 메서드
        // ========================================
        
        /// <summary>
        /// 현재 선택된 블록 가져오기
        /// </summary>
        public Block GetSelectedBlock()
        {
            return selectedBlock;
        }
        
        /// <summary>
        /// 사용 가능한 블록들 가져오기
        /// </summary>
        public List<Block> GetAvailableBlocks()
        {
            // 사용되지 않은 블록들만 반환
            return playerBlocks.FindAll(block => !IsBlockUsed(block));
        }
        
        /// <summary>
        /// 플레이어 색상에 따른 Color 반환
        /// </summary>
        private Color GetPlayerColor(PlayerColor color)
        {
            return color switch
            {
                PlayerColor.Blue => Color.blue,
                PlayerColor.Yellow => Color.yellow,
                PlayerColor.Red => Color.red,
                PlayerColor.Green => Color.green,
                _ => Color.white
            };
        }
    }
}