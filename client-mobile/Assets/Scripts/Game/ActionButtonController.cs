using UnityEngine;
using UnityEngine.UI;
using BlokusUnity.Common;

namespace BlokusUnity.Game
{
    /// <summary>
    /// Action Button (회전, 플립, 배치) 제어 컴포넌트
    /// BlockPalette와 GameBoard를 연결하여 블록 조작 기능 제공
    /// </summary>
    public class ActionButtonController : MonoBehaviour
    {
        [Header("Action Buttons")]
        [SerializeField] private Button rotateCWButton;      // 시계방향 회전
        [SerializeField] private Button rotateCCWButton;     // 반시계방향 회전
        [SerializeField] private Button flipHorizontalButton; // 가로 플립
        [SerializeField] private Button flipVerticalButton;   // 세로 플립
        [SerializeField] private Button placeButton;         // 배치

        [Header("UI Settings")]
        [SerializeField] private bool showButtonLabels = true;
        [SerializeField] private Color enabledColor = Color.white;
        [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] private Color placeEnabledColor = Color.green;
        [SerializeField] private Color placeDisabledColor = Color.red;

        // 연결된 컴포넌트들
        private BlockPalette blockPalette;
        private GameBoard gameBoard;
        private SingleGameManager gameManager;

        // 현재 상태
        private bool hasSelectedBlock = false;
        private bool canPlaceBlock = false;

        private void Awake()
        {
            // 컴포넌트 자동 찾기
            blockPalette = FindObjectOfType<BlockPalette>();
            gameBoard = FindObjectOfType<GameBoard>();
            gameManager = FindObjectOfType<SingleGameManager>();

            SetupButtonEvents();
            UpdateButtonStates();
        }

        private void OnEnable()
        {
            // 게임 이벤트 구독
            if (blockPalette != null)
                blockPalette.OnBlockSelected += OnBlockSelected;
        }

        private void OnDisable()
        {
            // 게임 이벤트 구독 해제
            if (blockPalette != null)
                blockPalette.OnBlockSelected -= OnBlockSelected;
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtonEvents()
        {
            if (rotateCWButton != null)
            {
                rotateCWButton.onClick.RemoveAllListeners();
                rotateCWButton.onClick.AddListener(() => RotateBlock(true));
                SetButtonLabel(rotateCWButton, "↻");
            }

            if (rotateCCWButton != null)
            {
                rotateCCWButton.onClick.RemoveAllListeners();
                rotateCCWButton.onClick.AddListener(() => RotateBlock(false));
                SetButtonLabel(rotateCCWButton, "↺");
            }

            if (flipHorizontalButton != null)
            {
                flipHorizontalButton.onClick.RemoveAllListeners();
                flipHorizontalButton.onClick.AddListener(() => FlipBlock(false));
                SetButtonLabel(flipHorizontalButton, "⟷");
            }

            if (flipVerticalButton != null)
            {
                flipVerticalButton.onClick.RemoveAllListeners();
                flipVerticalButton.onClick.AddListener(() => FlipBlock(true));
                SetButtonLabel(flipVerticalButton, "↕");
            }

            if (placeButton != null)
            {
                placeButton.onClick.RemoveAllListeners();
                placeButton.onClick.AddListener(PlaceBlock);
                SetButtonLabel(placeButton, "배치");
            }
        }

        /// <summary>
        /// 버튼 라벨 설정
        /// </summary>
        private void SetButtonLabel(Button button, string text)
        {
            if (!showButtonLabels || button == null) return;

            var label = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label == null)
            {
                // 라벨이 없으면 생성
                var textObj = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                textObj.transform.SetParent(button.transform, false);
                
                var rect = textObj.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                
                label = textObj.GetComponent<TMPro.TextMeshProUGUI>();
                label.text = text;
                label.fontSize = 18;
                label.color = Color.black;
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.raycastTarget = false;
            }
            else
            {
                label.text = text;
            }
        }

        /// <summary>
        /// 블록이 선택되었을 때 호출
        /// </summary>
        private void OnBlockSelected(Block block)
        {
            hasSelectedBlock = (block != null);
            UpdateButtonStates();
            Debug.Log($"[ActionButtonController] 블록 선택: {block?.Type}, 버튼 활성화: {hasSelectedBlock}");
        }

        /// <summary>
        /// 블록 회전
        /// </summary>
        private void RotateBlock(bool clockwise)
        {
            if (!hasSelectedBlock || blockPalette == null) return;

            Debug.Log($"[ActionButtonController] 블록 회전: {(clockwise ? "시계방향" : "반시계방향")}");
            blockPalette.RotateSelectedBlock(clockwise);
        }

        /// <summary>
        /// 블록 플립
        /// </summary>
        private void FlipBlock(bool vertical)
        {
            if (!hasSelectedBlock || blockPalette == null) return;

            Debug.Log($"[ActionButtonController] 블록 플립: {(vertical ? "세로" : "가로")}");
            blockPalette.FlipSelectedBlock(vertical);
        }

        /// <summary>
        /// 블록 배치
        /// </summary>
        private void PlaceBlock()
        {
            if (!hasSelectedBlock || gameBoard == null) return;

            Debug.Log("[ActionButtonController] 블록 배치 시도");
            
            // GameBoard의 확정 배치 메소드 호출
            gameBoard.OnConfirmPlacement();
        }

        /// <summary>
        /// 버튼 상태 업데이트
        /// </summary>
        public void UpdateButtonStates()
        {
            bool enableTransform = hasSelectedBlock;
            bool enablePlace = hasSelectedBlock && canPlaceBlock;

            // 회전/플립 버튼 상태
            SetButtonInteractable(rotateCWButton, enableTransform);
            SetButtonInteractable(rotateCCWButton, enableTransform);
            SetButtonInteractable(flipHorizontalButton, enableTransform);
            SetButtonInteractable(flipVerticalButton, enableTransform);

            // 배치 버튼 상태 (특별한 색상 처리)
            SetPlaceButtonState(enablePlace);
        }

        /// <summary>
        /// 배치 가능 여부 설정
        /// </summary>
        public void SetCanPlace(bool canPlace)
        {
            canPlaceBlock = canPlace;
            UpdateButtonStates();
        }

        /// <summary>
        /// 일반 버튼 활성화/비활성화
        /// </summary>
        private void SetButtonInteractable(Button button, bool interactable)
        {
            if (button == null) return;

            button.interactable = interactable;
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = interactable ? enabledColor : disabledColor;
            }
        }

        /// <summary>
        /// 배치 버튼 특별 처리
        /// </summary>
        private void SetPlaceButtonState(bool canPlace)
        {
            if (placeButton == null) return;

            placeButton.interactable = canPlace;
            var image = placeButton.GetComponent<Image>();
            if (image != null)
            {
                image.color = canPlace ? placeEnabledColor : placeDisabledColor;
            }
        }
    }
}