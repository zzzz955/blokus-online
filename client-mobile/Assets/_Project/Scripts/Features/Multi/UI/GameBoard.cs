using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using App.Services;
using Shared.Models;
using SharedGameLogic = App.Core.GameLogic;

namespace Features.Multi.UI
{
    /// <summary>
    /// 멀티플레이어 게임 보드 - Single 버전 기반으로 멀티플레이어 기능 추가
    /// 턴 기반 상호작용, 서버 동기화, 상대방 블록 표시
    /// </summary>
    public class GameBoard : MonoBehaviour
    {
        [Header("보드 설정")]
        [SerializeField] private int boardSize = 20;
        [SerializeField] private float cellSize = 20f;
        [SerializeField] private Color gridLineColor = Color.gray;
        [SerializeField] private float gridLineWidth = 1f;

        [Header("셀 프리팹 (선택)")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private RectTransform cellParent;

        [Header("보드 색상")]
        [SerializeField] private Color emptyColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        [SerializeField] private Color previewColor = Color.yellow;
        [SerializeField] private Color invalidColor = Color.red;
        [SerializeField] private Color blueColor = Color.blue;
        [SerializeField] private Color yellowColor = Color.yellow;
        [SerializeField] private Color redColor = Color.red;
        [SerializeField] private Color greenColor = Color.green;
        [SerializeField] private Color obstacleColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [Header("터치 UI - 멀티플레이어 전용")]
        [SerializeField] private RectTransform actionButtonPanel;
        [SerializeField] private UnityEngine.UI.Button rotateButton;
        [SerializeField] private UnityEngine.UI.Button flipButton;
        [SerializeField] private UnityEngine.UI.Button placeButton;
        [SerializeField] private UnityEngine.UI.Button cancelButton;
        [SerializeField] private UnityEngine.UI.Image placeImage;
        [SerializeField] private Sprite uiFallbackSprite;

        [Header("Action Button Sprites")]
        [SerializeField] private Sprite placeButtonEnabledSprite;
        [SerializeField] private Sprite placeButtonDisabledSprite;

        // 내부 상태
        private SharedGameLogic gameLogic;
        private GameObject[,] cellObjects;
        private Image[,] cellImages;
        private RectTransform[,] cellRects;
        private bool actionButtonsVisible = false;
        private bool isInteractable = false; // 멀티플레이어: 턴 기반 상호작용

        private Block previewBlock;
        private Position previewPosition = new Position(-1, -1);

        // 펜딩 배치 (멀티플레이어 전용)
        private Block pendingBlock;
        private Position pendingPosition = new Position(-1, -1);
        private bool hasPendingPlacement = false;

        // 멀티플레이어 상태
        private PlayerColor myPlayerColor = PlayerColor.None;
        private bool isMyTurn = false;

        // 이벤트
        public System.Action<Position> OnCellClicked;
        public System.Action<Position> OnCellHover;
        public System.Action<Block, Position> OnBlockPlaced;

        private void Awake()
        {
            if (gameLogic == null) gameLogic = new SharedGameLogic();

            // ActionButtonPanel을 즉시 숨김
            if (actionButtonPanel != null)
            {
                actionButtonPanel.sizeDelta = new Vector2(200f, 100f);
                actionButtonPanel.gameObject.SetActive(false);
                actionButtonsVisible = false;
                Debug.Log("[MultiGameBoard] ActionButtonPanel 초기화 - 숨김 상태");
            }
        }

        private void Start()
        {
            InitializeBoard();
            SetupUI();
        }

        /// <summary>
        /// 보드 초기화
        /// </summary>
        private void InitializeBoard()
        {
            Debug.Log("[MultiGameBoard] 멀티플레이어 보드 초기화");
            
            // cellParent 자동 연결
            if (cellParent == null)
            {
                var t = transform.Find("GridContainer");
                if (t == null)
                {
                    var go = new GameObject("GridContainer", typeof(RectTransform));
                    go.transform.SetParent(transform, false);
                    cellParent = go.GetComponent<RectTransform>();
                }
                else cellParent = t as RectTransform;
            }

            // 중앙 정렬
            cellParent.anchorMin = cellParent.anchorMax = new Vector2(0.5f, 0.5f);
            cellParent.anchoredPosition = Vector2.zero;
            cellParent.localScale = Vector3.one;

            CreateBoardCells();
            RefreshBoard();
        }

        private void SetupUI()
        {
            // Action Buttons 초기화
            if (actionButtonPanel != null)
            {
                var bg = actionButtonPanel.GetComponent<Image>();
                if (bg != null) bg.raycastTarget = false;

                actionButtonPanel.gameObject.SetActive(false);
                actionButtonsVisible = false;
            }

            if (rotateButton != null)
                rotateButton.onClick.AddListener(() => OnRotatePendingBlock());
            if (flipButton != null)
                flipButton.onClick.AddListener(() => OnFlipPendingBlock());
            if (placeButton != null)
                placeButton.onClick.AddListener(() => OnConfirmPlacement());
            if (cancelButton != null)
                cancelButton.onClick.AddListener(() => OnCancelPlacement());
        }

        private void CreateBoardCells()
        {
            if (cellParent == null)
            {
                Debug.LogError("[MultiGameBoard] CellParent가 없습니다.");
                return;
            }
            EnsureFallbackSprite();
            cellObjects = new GameObject[boardSize, boardSize];
            cellImages = new Image[boardSize, boardSize];
            cellRects = new RectTransform[boardSize, boardSize];

            for (int r = 0; r < boardSize; r++)
            {
                for (int c = 0; c < boardSize; c++)
                {
                    CreateCellAt(r, c);
                }
            }
        }

        private void CreateCellAt(int row, int col)
        {
            GameObject cellObj;

            if (cellPrefab != null)
            {
                cellObj = Instantiate(cellPrefab, cellParent);
                cellObj.name = $"Cell_{row}_{col}";
            }
            else
            {
                cellObj = new GameObject($"Cell_{row}_{col}", typeof(RectTransform));
                cellObj.transform.SetParent(cellParent, false);

                var bg = cellObj.AddComponent<Image>();
                bg.sprite = uiFallbackSprite;
                bg.type = Image.Type.Simple;
                bg.color = Color.white;

                CreateCellBorder(cellObj);
                cellObj.AddComponent<Button>().targetGraphic = bg;
            }

            var rect = cellObj.GetComponent<RectTransform>();
            if (rect == null) rect = cellObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(cellSize, cellSize);
            rect.localScale = Vector3.one;

            // 중앙 기준 배치
            rect.anchoredPosition = new Vector2(
                (col - boardSize * 0.5f + 0.5f) * cellSize,
                (boardSize * 0.5f - 0.5f - row) * cellSize
            );

            // Inner Image 획득
            Image inner = cellObj.transform.Find("Border/Inner")?.GetComponent<Image>();
            if (inner == null) inner = cellObj.GetComponent<Image>();
            inner.color = emptyColor;

            // 클릭/호버 전달
            var bc = cellObj.GetComponent<BoardCell>();
            if (bc == null) bc = cellObj.AddComponent<BoardCell>();
            bc.Initialize(row, col, this);

            cellObjects[row, col] = cellObj;
            cellImages[row, col] = inner;
            cellRects[row, col] = rect;
        }

        private void CreateCellBorder(GameObject cellObj)
        {
            float overflow = 5f;
            var borderObj = new GameObject("Border", typeof(RectTransform));
            borderObj.transform.SetParent(cellObj.transform, false);

            var borderImage = borderObj.AddComponent<Image>();
            borderImage.sprite = uiFallbackSprite;
            borderImage.type = Image.Type.Simple;
            borderImage.color = gridLineColor;

            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-overflow, -overflow);
            borderRect.offsetMax = new Vector2(overflow, overflow);

            var innerObj = new GameObject("Inner", typeof(RectTransform));
            innerObj.transform.SetParent(borderObj.transform, false);

            var innerImage = innerObj.AddComponent<Image>();
            innerImage.sprite = uiFallbackSprite;
            innerImage.color = emptyColor;
            innerImage.type = Image.Type.Simple;

            var innerRect = innerObj.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = Vector2.one * Mathf.Max(0, gridLineWidth - overflow);
            innerRect.offsetMax = Vector2.one * -Mathf.Max(0, gridLineWidth - overflow);
        }

        public void OnCellClickedInternal(int row, int col)
        {
            if (!isInteractable || !isMyTurn) return; // 멀티플레이어: 내 턴에만 클릭 가능
            
            var pos = new Position(row, col);
            OnCellClicked?.Invoke(pos);
        }

        public void OnCellHoverInternal(int row, int col)
        {
            if (!isInteractable || !isMyTurn) return;
            
            var pos = new Position(row, col);
            OnCellHover?.Invoke(pos);
        }

        /// <summary>
        /// 블록 배치 (서버에서 확정된 배치 적용)
        /// </summary>
        public void PlaceBlock(Position position, int playerId, List<Position> occupiedCells)
        {
            Debug.Log($"[MultiGameBoard] 서버에서 블록 배치 확정: Player {playerId} at ({position.row}, {position.col})");
            
            PlayerColor playerColor = (PlayerColor)(playerId + 1);
            
            // 실제 점유된 셀들에 색상 적용
            foreach (var cellPos in occupiedCells)
            {
                if (ValidationUtility.IsValidPosition(cellPos) && 
                    cellPos.row < boardSize && cellPos.col < boardSize)
                {
                    UpdateCellVisual(cellPos.row, cellPos.col, playerColor);
                    // GameLogic에는 SetBoardCell 메서드가 없으므로 직접 보드 업데이트
                    SetBoardCell(cellPos.row, cellPos.col, playerColor);
                }
            }
            
            // 미리보기 및 액션 버튼 정리
            ClearTouchPreview();
        }

        /// <summary>
        /// 블록 배치 시도 (내 턴에서 서버로 전송)
        /// </summary>
        public bool TryPlaceBlock(Block block, Position position)
        {
            if (block == null || !ValidationUtility.IsValidPosition(position)) return false;
            if (!isMyTurn || !isInteractable) return false;

            // 보드 밖이면 실패
            if (!CanPlaceWithinBoard(block, position)) return false;

            // 배치 가능 여부 확인
            if (!gameLogic.CanPlaceBlock(block, position)) return false;
            
            // 서버로 배치 요청 전송
            OnBlockPlaced?.Invoke(block, position);
            return true;
        }

        private void RefreshBoard()
        {
            if (cellImages == null) return;

            for (int r = 0; r < boardSize; r++)
                for (int c = 0; c < boardSize; c++)
                {
                    var color = gameLogic.GetBoardCell(r, c);
                    UpdateCellVisual(r, c, color);
                }
        }

        private void UpdateCellVisual(int row, int col, PlayerColor color)
        {
            var img = cellImages[row, col];
            if (img == null) return;

            img.color = color switch
            {
                PlayerColor.Blue => blueColor,
                PlayerColor.Yellow => yellowColor,
                PlayerColor.Red => redColor,
                PlayerColor.Green => greenColor,
                PlayerColor.Obstacle => obstacleColor,
                _ => emptyColor
            };
        }

        /// <summary>
        /// 터치 미리보기 설정 (내 턴에만 가능)
        /// </summary>
        public void SetTouchPreview(Block block, Position position)
        {
            if (!isMyTurn || !isInteractable)
            {
                ClearTouchPreview();
                return;
            }

            if (block == null || !ValidationUtility.IsValidPosition(position))
            {
                ClearTouchPreview();
                return;
            }

            // 이전 상태 클리어
            ClearPreview();
            HideActionButtons();

            // 새로운 블록 설정
            pendingBlock = block;
            pendingPosition = position;
            hasPendingPlacement = true;

            // 미리보기 표시
            SetPreview(block, position);

            // 배치 가능 여부 확인 및 액션 버튼 표시
            bool canPlace = gameLogic.CanPlaceBlock(block, position);
            ShowActionButtons(canPlace);
        }

        public void ClearTouchPreview()
        {
            ClearPreview();
            HideActionButtons();

            pendingBlock = null;
            pendingPosition = new Position(-1, -1);
            hasPendingPlacement = false;
        }

        private void SetPreview(Block block, Position position)
        {
            ClearPreview();
            if (block == null || !ValidationUtility.IsValidPosition(position)) return;

            previewBlock = block;
            previewPosition = position;
            ShowPreview();
        }

        private void ShowPreview()
        {
            if (previewBlock == null) return;

            var cells = previewBlock.GetAbsolutePositions(previewPosition);
            bool canPlace = gameLogic.CanPlaceBlock(previewBlock, previewPosition);
            var col = canPlace ? previewColor : invalidColor;

            foreach (var pos in cells)
            {
                if (!ValidationUtility.IsValidPosition(pos)) continue;
                if (pos.row >= boardSize || pos.col >= boardSize) continue;

                if (gameLogic.GetCellColor(pos) == PlayerColor.None)
                {
                    var img = cellImages[pos.row, pos.col];
                    if (img != null) img.color = col;
                }
            }
        }

        private void ClearPreview()
        {
            if (previewBlock == null || !ValidationUtility.IsValidPosition(previewPosition)) return;

            var cells = previewBlock.GetAbsolutePositions(previewPosition);
            foreach (var pos in cells)
            {
                if (!ValidationUtility.IsValidPosition(pos)) continue;
                if (pos.row >= boardSize || pos.col >= boardSize) continue;

                if (gameLogic.GetCellColor(pos) == PlayerColor.None)
                {
                    var img = cellImages[pos.row, pos.col];
                    if (img != null) img.color = emptyColor;
                }
            }

            previewBlock = null;
            previewPosition = new Position(-1, -1);
        }

        private void OnRotatePendingBlock()
        {
            if (!isMyTurn || pendingBlock == null || !ValidationUtility.IsValidPosition(pendingPosition)) return;

            ClearPreview();
            pendingBlock.RotateClockwise();
            SetPreview(pendingBlock, pendingPosition);
            bool canPlace = gameLogic.CanPlaceBlock(pendingBlock, pendingPosition);
            ShowActionButtons(canPlace);
        }

        private void OnFlipPendingBlock()
        {
            if (!isMyTurn || pendingBlock == null || !ValidationUtility.IsValidPosition(pendingPosition)) return;

            ClearPreview();
            pendingBlock.FlipHorizontal();
            SetPreview(pendingBlock, pendingPosition);
            bool canPlace = gameLogic.CanPlaceBlock(pendingBlock, pendingPosition);
            ShowActionButtons(canPlace);
        }

        public void OnConfirmPlacement()
        {
            if (!isMyTurn || !hasPendingPlacement || pendingBlock == null || !ValidationUtility.IsValidPosition(pendingPosition))
            {
                Debug.LogWarning("[MultiGameBoard] 확정할 배치가 없거나 내 턴이 아닙니다");
                return;
            }

            if (TryPlaceBlock(pendingBlock, pendingPosition))
            {
                Debug.Log($"[MultiGameBoard] 블록 배치 확정: {pendingBlock.Type} at ({pendingPosition.row}, {pendingPosition.col})");
                ClearTouchPreview();
            }
            else
            {
                Debug.LogError("[MultiGameBoard] 블록 배치 확정 실패!");
            }
        }

        private void OnCancelPlacement()
        {
            Debug.Log("[MultiGameBoard] 블록 배치 취소");
            ClearTouchPreview();
        }

        private void ShowActionButtons(bool canPlace)
        {
            if (actionButtonPanel == null) return;

            actionButtonsVisible = true;
            actionButtonPanel.gameObject.SetActive(true);

            if (placeButton != null)
            {
                placeButton.interactable = canPlace;
                var img = placeImage?.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    if (canPlace && placeButtonEnabledSprite != null)
                    {
                        img.sprite = placeButtonEnabledSprite;
                        img.color = Color.white;
                    }
                    else if (!canPlace && placeButtonDisabledSprite != null)
                    {
                        img.sprite = placeButtonDisabledSprite;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.color = canPlace ? Color.green : Color.red;
                    }
                }
            }

            PositionActionButtonsAtBlock();
        }

        private void HideActionButtons()
        {
            if (actionButtonPanel == null) return;

            actionButtonsVisible = false;
            actionButtonPanel.gameObject.SetActive(false);
        }

        private void PositionActionButtonsAtBlock()
        {
            if (!actionButtonsVisible || actionButtonPanel == null || pendingBlock == null) return;

            var blockPositions = pendingBlock.GetAbsolutePositions(pendingPosition);
            int minRow = int.MaxValue, maxRow = int.MinValue;
            int minCol = int.MaxValue, maxCol = int.MinValue;

            foreach (var pos in blockPositions)
            {
                if (ValidationUtility.IsValidPosition(pos) && pos.row < boardSize && pos.col < boardSize)
                {
                    minRow = Mathf.Min(minRow, pos.row);
                    maxRow = Mathf.Max(maxRow, pos.row);
                    minCol = Mathf.Min(minCol, pos.col);
                    maxCol = Mathf.Max(maxCol, pos.col);
                }
            }
            if (minRow == int.MaxValue) return;

            // 간단한 위치 설정 (우상단)
            Vector2 targetPos = new Vector2(
                (maxCol - boardSize * 0.5f + 1f) * cellSize,
                (boardSize * 0.5f - minRow + 1f) * cellSize
            );
            actionButtonPanel.anchoredPosition = targetPos;
        }

        private void EnsureFallbackSprite()
        {
            if (uiFallbackSprite != null) return;
            var tex = Texture2D.whiteTexture;
            uiFallbackSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private bool CanPlaceWithinBoard(Block block, Position position)
        {
            foreach (var p in block.GetAbsolutePositions(position))
                if (p.row < 0 || p.col < 0 || p.row >= boardSize || p.col >= boardSize)
                    return false;
            return true;
        }

        // ========================================
        // Public API (멀티플레이어 전용)
        // ========================================
        
        public void SetGameLogic(SharedGameLogic logic)
        {
            gameLogic = logic ?? new SharedGameLogic();
            RefreshBoard();
        }

        public void SetInteractable(bool interactable)
        {
            isInteractable = interactable;
            Debug.Log($"[MultiGameBoard] 상호작용 설정: {interactable}");
        }

        public void SetMyTurn(bool isMyTurn, PlayerColor myColor)
        {
            this.isMyTurn = isMyTurn;
            this.myPlayerColor = myColor;
            
            if (!isMyTurn)
            {
                ClearTouchPreview(); // 내 턴이 아닐 때 미리보기 제거
            }
            
            Debug.Log($"[MultiGameBoard] 턴 상태 변경: {(isMyTurn ? "내 턴" : "상대 턴")}, 내 색상: {myColor}");
        }

        public void ResetBoard()
        {
            gameLogic?.ClearBoard();
            RefreshBoard();
            ClearTouchPreview();
            Debug.Log("[MultiGameBoard] 보드 리셋");
        }

        public int GetBoardSize() => boardSize;
        public float CellSize => cellSize;
        public RectTransform CellParent => cellParent;

        /// <summary>
        /// 보드 셀 설정 (내부용)
        /// </summary>
        private void SetBoardCell(int row, int col, PlayerColor playerColor)
        {
            if (row >= 0 && row < boardSize && col >= 0 && col < boardSize)
            {
                // 실제 구현에서는 내부 보드 상태를 업데이트해야 함
                Debug.Log($"[GameBoard] 보드 셀 설정: ({row}, {col}) -> {playerColor}");
                // boardState[row, col] = playerColor; // 보드 상태 배열이 있다면
            }
        }
    }

    /// <summary>
    /// 보드 셀 컴포넌트 - 클릭/호버 이벤트 전달
    /// </summary>
    public class BoardCell : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler, UnityEngine.EventSystems.IPointerEnterHandler
    {
        private int row, col;
        private GameBoard gameBoard;

        public void Initialize(int row, int col, GameBoard board)
        {
            this.row = row;
            this.col = col;
            this.gameBoard = board;
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            gameBoard?.OnCellClickedInternal(row, col);
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            gameBoard?.OnCellHoverInternal(row, col);
        }
    }
}