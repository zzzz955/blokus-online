using UnityEngine;
using UnityEngine.UI;
using BlokusUnity.Common;

namespace BlokusUnity.Game
{
    /// <summary>
    /// 개별 블록 버튼 컴포넌트
    /// 블록 팔레트에서 사용되는 각 블록의 UI 표현
    /// </summary>
    public class BlockButton : MonoBehaviour
    {
        private Block block;
        private BlockPalette parentPalette;
        private Button button;
        private Image backgroundImage;
        private bool isInitialized = false;
        
        // ========================================
        // 초기화
        // ========================================
        
        /// <summary>
        /// 블록 버튼 초기화
        /// </summary>
        public void Initialize(Block blockData, BlockPalette palette)
        {
            block = blockData;
            parentPalette = palette;
            
            SetupComponents();
            SetupButton();
            
            isInitialized = true;
        }
        
        /// <summary>
        /// 필요한 컴포넌트들 설정
        /// </summary>
        private void SetupComponents()
        {
            button = GetComponent<Button>();
            if (button == null)
            {
                button = gameObject.AddComponent<Button>();
            }
            
            backgroundImage = GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = gameObject.AddComponent<Image>();
            }
        }
        
        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButton()
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnButtonClicked);
            }
        }
        
        // ========================================
        // 버튼 이벤트
        // ========================================
        
        /// <summary>
        /// 버튼 클릭 이벤트
        /// </summary>
        private void OnButtonClicked()
        {
            if (!isInitialized || parentPalette == null || block == null)
                return;
            
            parentPalette.OnBlockButtonClicked(block.Type);
            
            // 시각적 피드백
            StartCoroutine(ClickFeedback());
        }
        
        /// <summary>
        /// 클릭 피드백 애니메이션
        /// </summary>
        private System.Collections.IEnumerator ClickFeedback()
        {
            if (backgroundImage == null) yield break;
            
            Color originalColor = backgroundImage.color;
            
            // 크기 애니메이션
            Vector3 originalScale = transform.localScale;
            
            // 작게 → 크게
            transform.localScale = originalScale * 0.95f;
            yield return new WaitForSeconds(0.05f);
            
            transform.localScale = originalScale * 1.05f;
            yield return new WaitForSeconds(0.05f);
            
            transform.localScale = originalScale;
        }
        
        // ========================================
        // 시각적 업데이트
        // ========================================
        
        /// <summary>
        /// 버튼 배경 색상 설정
        /// </summary>
        public void SetButtonColor(Color color)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }
        }
        
        /// <summary>
        /// 블록 시각 새로고침
        /// </summary>
        public void RefreshVisual()
        {
            if (!isInitialized || block == null) return;
            
            // 기존 블록 셀들 제거
            ClearBlockCells();
            
            // 새로운 블록 모양으로 다시 그리기
            RedrawBlock();
        }
        
        /// <summary>
        /// 기존 블록 셀들 정리
        /// </summary>
        private void ClearBlockCells()
        {
            // "Cell_" 로 시작하는 자식 오브젝트들 제거
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Cell_"))
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
        
        /// <summary>
        /// 블록 다시 그리기
        /// </summary>
        private void RedrawBlock()
        {
            var shape = block.GetCurrentShape();
            var boundingRect = block.GetBoundingRect();
            
            foreach (var pos in shape)
            {
                CreateBlockCell(pos, boundingRect);
            }
        }
        
        /// <summary>
        /// 블록 셀 생성
        /// </summary>
        private void CreateBlockCell(Position pos, Block.BoundingRect bounds)
        {
            GameObject cellObj = new GameObject($"Cell_{pos.row}_{pos.col}", typeof(RectTransform));
            cellObj.transform.SetParent(transform, false);
            
            var image = cellObj.AddComponent<Image>();
            image.color = GetBlockColor();
            
            var rectTransform = cellObj.GetComponent<RectTransform>();
            
            // 블록 크기 및 스케일 설정
            float cellSize = 20f; // 기본 셀 크기
            float blockScale = 0.8f; // 버튼 내 블록 스케일
            
            // 중앙 정렬을 위한 위치 계산
            float cellX = (pos.col - bounds.width * 0.5f + 0.5f) * cellSize * blockScale;
            float cellY = (pos.row - bounds.height * 0.5f + 0.5f) * cellSize * blockScale;
            
            rectTransform.anchoredPosition = new Vector2(cellX, -cellY); // Y 좌표 반전 (UI 좌표계)
            rectTransform.sizeDelta = Vector2.one * cellSize * blockScale;
        }
        
        /// <summary>
        /// 블록 색상 가져오기
        /// </summary>
        private Color GetBlockColor()
        {
            if (block == null) return Color.white;
            
            return block.Player switch
            {
                PlayerColor.Blue => Color.blue,
                PlayerColor.Yellow => Color.yellow,
                PlayerColor.Red => Color.red,
                PlayerColor.Green => Color.green,
                _ => Color.white
            };
        }
        
        // ========================================
        // 유틸리티
        // ========================================
        
        /// <summary>
        /// 버튼이 초기화되었는지 확인
        /// </summary>
        public bool IsInitialized()
        {
            return isInitialized;
        }
        
        /// <summary>
        /// 연결된 블록 가져오기
        /// </summary>
        public Block GetBlock()
        {
            return block;
        }
        
        /// <summary>
        /// 버튼 활성화/비활성화
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
            
            // 시각적 피드백
            if (backgroundImage != null)
            {
                backgroundImage.color = interactable ? Color.white : Color.gray;
            }
        }
        
        /// <summary>
        /// 블록 정보 툴팁 표시 (향후 확장용)
        /// </summary>
        public void ShowTooltip()
        {
            if (block != null)
            {
                string tooltip = $"{BlockFactory.GetBlockName(block.Type)}\n" +
                               $"크기: {block.GetSize()}칸\n" +
                               $"점수: {BlockFactory.GetBlockScore(block.Type)}점";
                
                Debug.Log($"블록 정보: {tooltip}");
                // TODO: 실제 툴팁 UI 구현
            }
        }
    }
}