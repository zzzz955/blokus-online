using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.Common;

namespace BlokusUnity.UI
{
    /// <summary>
    /// 모바일용 블록 조작 UI
    /// 회전, 뒤집기 버튼 제공
    /// </summary>
    public class BlockControlUI : MonoBehaviour
    {
        [Header("Control Buttons")]
        [SerializeField] private Button rotateLeftButton;
        [SerializeField] private Button rotateRightButton; 
        [SerializeField] private Button flipHorizontalButton;
        [SerializeField] private Button flipVerticalButton;
        [SerializeField] private Button undoButton;
        
        [Header("Button Icons (Optional)")]
        [SerializeField] private Sprite rotateLeftIcon;
        [SerializeField] private Sprite rotateRightIcon;
        [SerializeField] private Sprite flipHorizontalIcon;
        [SerializeField] private Sprite flipVerticalIcon;
        [SerializeField] private Sprite undoIcon;
        
        [Header("Visual Settings")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color pressedColor = Color.gray;
        [SerializeField] private Color disabledColor = Color.gray;
        
        // 컴포넌트 참조
        private Game.BlockPalette blockPalette;
        private Game.SingleGameManager gameManager;
        
        // 현재 선택된 블록
        private Block selectedBlock;
        
        // ========================================
        // Unity 생명주기
        // ========================================
        
        void Start()
        {
            SetupComponents();
            SetupButtonEvents();
            UpdateButtonStates();
        }
        
        // ========================================
        // 초기화
        // ========================================
        
        /// <summary>
        /// 컴포넌트 참조 설정
        /// </summary>
        private void SetupComponents()
        {
            blockPalette = FindObjectOfType<Game.BlockPalette>();
            gameManager = Game.SingleGameManager.Instance;
            
            // 블록 선택 이벤트 구독
            if (blockPalette != null)
            {
                blockPalette.OnBlockSelected += OnBlockSelected;
                blockPalette.OnBlockDeselected += OnBlockDeselected;
            }
        }
        
        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtonEvents()
        {
            if (rotateLeftButton != null)
            {
                rotateLeftButton.onClick.AddListener(RotateLeft);
                SetButtonIcon(rotateLeftButton, rotateLeftIcon);
            }
            
            if (rotateRightButton != null)
            {
                rotateRightButton.onClick.AddListener(RotateRight);
                SetButtonIcon(rotateRightButton, rotateRightIcon);
            }
            
            if (flipHorizontalButton != null)
            {
                flipHorizontalButton.onClick.AddListener(FlipHorizontal);
                SetButtonIcon(flipHorizontalButton, flipHorizontalIcon);
            }
            
            if (flipVerticalButton != null)
            {
                flipVerticalButton.onClick.AddListener(FlipVertical);
                SetButtonIcon(flipVerticalButton, flipVerticalIcon);
            }
            
            if (undoButton != null)
            {
                undoButton.onClick.AddListener(UndoMove);
                SetButtonIcon(undoButton, undoIcon);
            }
        }
        
        /// <summary>
        /// 버튼 아이콘 설정
        /// </summary>
        private void SetButtonIcon(Button button, Sprite icon)
        {
            if (button != null && icon != null)
            {
                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = icon;
                }
            }
        }
        
        /// <summary>
        /// Button 텍스트 설정 헬퍼 메서드
        /// Unity에서 Button 생성 후 이 메서드를 호출하여 텍스트 설정
        /// </summary>
        private void SetupButtonText(Button button, string text)
        {
            if (button == null) return;
            
            // 기존 Text 컴포넌트 제거
            var oldText = button.GetComponentInChildren<Text>();
            if (oldText != null)
            {
                DestroyImmediate(oldText.gameObject);
            }
            
            // Text 추가
            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(button.transform, false);
            
            var textComponent = textObj.AddComponent<Text>();
            textComponent.text = text;
            textComponent.color = Color.white;
            textComponent.fontSize = 18;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            
            // 전체 버튼 크기에 맞춤
            var rectTransform = textObj.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
        
        // ========================================
        // 블록 조작 메서드들
        // ========================================
        
        /// <summary>
        /// 블록 왼쪽으로 90도 회전 (반시계방향)
        /// </summary>
        private void RotateLeft()
        {
            if (selectedBlock == null) return;
            
            selectedBlock.RotateCounterClockwise();
            OnBlockTransformed();
            
            // 시각적 피드백
            StartCoroutine(ButtonPressEffect(rotateLeftButton));
            
            Debug.Log($"블록 좌회전: {selectedBlock.CurrentRotation}");
        }
        
        /// <summary>
        /// 블록 오른쪽으로 90도 회전 (시계방향)
        /// </summary>
        private void RotateRight()
        {
            if (selectedBlock == null) return;
            
            selectedBlock.RotateClockwise();
            OnBlockTransformed();
            
            // 시각적 피드백
            StartCoroutine(ButtonPressEffect(rotateRightButton));
            
            Debug.Log($"블록 우회전: {selectedBlock.CurrentRotation}");
        }
        
        /// <summary>
        /// 블록 수평 뒤집기 (정규화 포함)
        /// </summary>
        private void FlipHorizontal()
        {
            if (selectedBlock == null) return;
            
            selectedBlock.FlipHorizontal();
            OnBlockTransformed();
            
            // 시각적 피드백
            StartCoroutine(ButtonPressEffect(flipHorizontalButton));
            
            Debug.Log($"블록 수평 뒤집기: {selectedBlock.CurrentFlipState}");
        }
        
        /// <summary>
        /// 블록 수직 뒤집기 (정규화 포함)
        /// </summary>
        private void FlipVertical()
        {
            if (selectedBlock == null) return;
            
            selectedBlock.FlipVertical();
            OnBlockTransformed();
            
            // 시각적 피드백
            StartCoroutine(ButtonPressEffect(flipVerticalButton));
            
            Debug.Log($"블록 수직 뒤집기: {selectedBlock.CurrentFlipState}");
        }
        
        /// <summary>
        /// 마지막 수 되돌리기
        /// </summary>
        private void UndoMove()
        {
            if (gameManager != null)
            {
                gameManager.OnUndoMove();
                
                // 시각적 피드백
                StartCoroutine(ButtonPressEffect(undoButton));
                
                Debug.Log("실행취소");
            }
        }
        
        /// <summary>
        /// 블록 변형 후 처리
        /// </summary>
        private void OnBlockTransformed()
        {
            // 블록 팔레트에서 시각적 업데이트
            if (blockPalette != null && selectedBlock != null)
            {
                // 팔레트에 변경사항 반영
                blockPalette.OnBlockSelected?.Invoke(selectedBlock);
            }
        }
        
        // ========================================
        // 이벤트 처리
        // ========================================
        
        /// <summary>
        /// 블록 선택 이벤트
        /// </summary>
        private void OnBlockSelected(Block block)
        {
            selectedBlock = block;
            UpdateButtonStates();
        }
        
        /// <summary>
        /// 블록 선택 해제 이벤트
        /// </summary>
        private void OnBlockDeselected()
        {
            selectedBlock = null;
            UpdateButtonStates();
        }
        
        /// <summary>
        /// 버튼 상태 업데이트
        /// </summary>
        private void UpdateButtonStates()
        {
            bool hasSelectedBlock = selectedBlock != null;
            
            // 블록 조작 버튼들
            SetButtonInteractable(rotateLeftButton, hasSelectedBlock);
            SetButtonInteractable(rotateRightButton, hasSelectedBlock);
            SetButtonInteractable(flipHorizontalButton, hasSelectedBlock);
            SetButtonInteractable(flipVerticalButton, hasSelectedBlock);
            
            // 실행취소 버튼 (게임 상태에 따라)
            bool canUndo = gameManager != null; // TODO: 실제 실행취소 가능 여부 확인
            SetButtonInteractable(undoButton, canUndo);
        }
        
        /// <summary>
        /// 버튼 활성화/비활성화 설정
        /// </summary>
        private void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
                
                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = interactable ? normalColor : disabledColor;
                }
            }
        }
        
        // ========================================
        // 시각적 효과
        // ========================================
        
        /// <summary>
        /// 버튼 누름 효과
        /// </summary>
        private System.Collections.IEnumerator ButtonPressEffect(Button button)
        {
            if (button == null) yield break;
            
            var image = button.GetComponent<Image>();
            if (image == null) yield break;
            
            Color originalColor = image.color;
            
            // 크기 축소 효과
            Vector3 originalScale = button.transform.localScale;
            button.transform.localScale = originalScale * 0.9f;
            
            // 색상 변화
            image.color = pressedColor;
            
            yield return new WaitForSeconds(0.1f);
            
            // 원상복구
            button.transform.localScale = originalScale;
            image.color = originalColor;
        }
        
        // ========================================
        // 공개 메서드
        // ========================================
        
        /// <summary>
        /// UI 표시/숨김
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
        
        /// <summary>
        /// 버튼 색상 테마 변경
        /// </summary>
        public void SetColorTheme(Color normal, Color pressed, Color disabled)
        {
            normalColor = normal;
            pressedColor = pressed;
            disabledColor = disabled;
            UpdateButtonStates();
        }
        
        /// <summary>
        /// 현재 선택된 블록 정보 표시 (선택적 기능)
        /// </summary>
        public string GetSelectedBlockInfo()
        {
            if (selectedBlock == null)
                return "블록이 선택되지 않음";
            
            return $"{BlockFactory.GetBlockName(selectedBlock.Type)}\n" +
                   $"회전: {selectedBlock.CurrentRotation}\n" +
                   $"뒤집기: {selectedBlock.CurrentFlipState}";
        }
    }
}