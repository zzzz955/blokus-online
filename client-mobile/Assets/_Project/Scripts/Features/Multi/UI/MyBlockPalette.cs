using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Features.Multi.UI
{
    /// <summary>
    /// 내 블록 팔레트 (Stub 구현)
    /// 플레이어가 사용할 수 있는 블록들을 표시
    /// </summary>
    public class MyBlockPalette : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform blockContainer;
        [SerializeField] private GameObject blockItemPrefab;
        
        [Header("플레이어 설정")]
        [SerializeField] private int playerId = 0;
        [SerializeField] private Color playerColor = Color.blue;
        
        // 블록 데이터
        private List<BlockItem> availableBlocks = new List<BlockItem>();
        private BlockItem selectedBlock = null;
        
        // 이벤트
        public event System.Action<BlockItem> OnBlockSelected;
        public event System.Action<BlockItem> OnBlockUsed;
        
        private void Start()
        {
            InitializeBlocks();
        }
        
        /// <summary>
        /// 블록 팔레트 초기화
        /// </summary>
        private void InitializeBlocks()
        {
            Debug.Log("[MyBlockPalette] 블록 팔레트 초기화 (Stub)");
            
            // Stub: 간단한 블록들만 생성
            CreateStubBlocks();
            
            // UI 업데이트
            UpdateBlockPaletteUI();
        }
        
        /// <summary>
        /// Stub 블록들 생성
        /// </summary>
        private void CreateStubBlocks()
        {
            // 실제 블로커스 블록 대신 간단한 Stub 블록들
            var blocks = new[]
            {
                new BlockItem { id = 1, name = "1x1", size = new Vector2Int(1, 1), isUsed = false },
                new BlockItem { id = 2, name = "2x1", size = new Vector2Int(2, 1), isUsed = false },
                new BlockItem { id = 3, name = "L자", size = new Vector2Int(2, 2), isUsed = false },
                new BlockItem { id = 4, name = "T자", size = new Vector2Int(3, 2), isUsed = false },
                new BlockItem { id = 5, name = "십자", size = new Vector2Int(3, 3), isUsed = false }
            };
            
            availableBlocks.AddRange(blocks);
        }
        
        /// <summary>
        /// 블록 팔레트 UI 업데이트
        /// </summary>
        private void UpdateBlockPaletteUI()
        {
            if (blockContainer == null) return;
            
            // 기존 UI 정리
            for (int i = blockContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(blockContainer.GetChild(i).gameObject);
            }
            
            // 새 블록 UI 생성
            foreach (var block in availableBlocks)
            {
                CreateBlockUI(block);
            }
        }
        
        /// <summary>
        /// 블록 UI 생성
        /// </summary>
        private void CreateBlockUI(BlockItem block)
        {
            if (blockItemPrefab == null) return;
            
            GameObject blockUI = Instantiate(blockItemPrefab, blockContainer);
            blockUI.name = $"Block_{block.id}_{block.name}";
            
            // 블록 이미지 설정 (Stub)
            Image blockImage = blockUI.GetComponent<Image>();
            if (blockImage != null)
            {
                blockImage.color = block.isUsed ? Color.gray : playerColor;
            }
            
            // 블록 이름 표시
            Text nameText = blockUI.GetComponentInChildren<Text>();
            if (nameText != null)
            {
                nameText.text = block.name;
            }
            
            // 클릭 이벤트 연결
            Button blockButton = blockUI.GetComponent<Button>();
            if (blockButton != null)
            {
                blockButton.onClick.AddListener(() => OnBlockClicked(block));
                blockButton.interactable = !block.isUsed;
            }
        }
        
        /// <summary>
        /// 블록 클릭 처리
        /// </summary>
        private void OnBlockClicked(BlockItem block)
        {
            if (block.isUsed) return;
            
            Debug.Log($"[MyBlockPalette] 블록 선택: {block.name}");
            
            selectedBlock = block;
            OnBlockSelected?.Invoke(block);
            
            // 선택된 블록 하이라이트 (Stub)
            UpdateBlockSelection();
        }
        
        /// <summary>
        /// 블록 선택 UI 업데이트
        /// </summary>
        private void UpdateBlockSelection()
        {
            // Stub: 실제 구현에서는 선택된 블록을 하이라이트
            Debug.Log($"[MyBlockPalette] 블록 선택 상태 업데이트: {selectedBlock?.name ?? "None"}");
        }
        
        /// <summary>
        /// 블록 사용 처리
        /// </summary>
        public void UseBlock(BlockItem block)
        {
            var targetBlock = availableBlocks.Find(b => b.id == block.id);
            if (targetBlock != null)
            {
                targetBlock.isUsed = true;
                OnBlockUsed?.Invoke(targetBlock);
                
                Debug.Log($"[MyBlockPalette] 블록 사용: {block.name}");
                
                // UI 업데이트
                UpdateBlockPaletteUI();
            }
        }
        
        /// <summary>
        /// 선택된 블록 가져오기
        /// </summary>
        public BlockItem GetSelectedBlock()
        {
            return selectedBlock;
        }
        
        /// <summary>
        /// 플레이어 색상 설정
        /// </summary>
        public void SetPlayerColor(Color color)
        {
            playerColor = color;
            UpdateBlockPaletteUI();
        }
        
        /// <summary>
        /// 사용 가능한 블록 개수
        /// </summary>
        public int GetAvailableBlockCount()
        {
            return availableBlocks.FindAll(b => !b.isUsed).Count;
        }
        
        /// <summary>
        /// 팔레트 초기화
        /// </summary>
        public void ResetPalette()
        {
            Debug.Log("[MyBlockPalette] 팔레트 리셋");
            
            foreach (var block in availableBlocks)
            {
                block.isUsed = false;
            }
            
            selectedBlock = null;
            UpdateBlockPaletteUI();
        }
        
        /// <summary>
        /// 팔레트 상호작용 설정
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            Debug.Log($"[MyBlockPalette] 상호작용 설정: {interactable}");
            
            if (blockContainer == null) return;
            
            for (int i = 0; i < blockContainer.childCount; i++)
            {
                Transform child = blockContainer.GetChild(i);
                Button blockButton = child.GetComponent<Button>();
                if (blockButton != null)
                {
                    blockButton.interactable = interactable && !availableBlocks[i].isUsed;
                }
            }
        }
    }
    
    /// <summary>
    /// 블록 아이템 구조체
    /// </summary>
    [System.Serializable]
    public class BlockItem
    {
        public int id;
        public string name;
        public Vector2Int size;
        public bool isUsed;
        public List<Vector2Int> shape = new List<Vector2Int>(); // 블록의 실제 모양 (상대 좌표)
        
        public BlockItem()
        {
            shape = new List<Vector2Int>();
        }
    }
}