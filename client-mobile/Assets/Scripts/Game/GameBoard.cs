using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.Common;

namespace BlokusUnity.Game
{
    /// <summary>
    /// 2D 게임보드(UI) 시각화 및 터치 상호작용
    /// </summary>
    public class GameBoard : MonoBehaviour
    {
        [Header("보드 설정")]
        [SerializeField] private int boardSize = 20;
        [SerializeField] private float cellSize = 20f;                 // px (Canvas 스케일 포함)
        [SerializeField] private Color gridLineColor = Color.gray;
        [SerializeField] private float gridLineWidth = 1f;

        [Header("셀 프리팹 (선택)")]
        [SerializeField] private GameObject cellPrefab;                 // UI 기반(prefab 내부에 Border/Inner 구조 권장)
        [SerializeField] private RectTransform cellParent;              // GridContainer(必: RectTransform)

        [Header("보드 색상")]
        [SerializeField] private Color emptyColor = Color.white;
        [SerializeField] private Color previewColor = Color.yellow;
        [SerializeField] private Color invalidColor = Color.red;
        [SerializeField] private Color blueColor = Color.blue;
        [SerializeField] private Color yellowColor = Color.yellow;
        [SerializeField] private Color redColor = Color.red;
        [SerializeField] private Color greenColor = Color.green;

        [Header("터치 UI")]
        // === Action Buttons (우측 상단 패널) ===
        [SerializeField] private RectTransform actionButtonPanel;
        [SerializeField] private UnityEngine.UI.Button rotateButton;
        [SerializeField] private UnityEngine.UI.Button flipButton;
        [SerializeField] private UnityEngine.UI.Button placeButton;
        [SerializeField] private Sprite uiFallbackSprite;

        [Header("스킨")]
        [SerializeField] private BlockSkin skin;

        // 내부 상태
        private GameLogic gameLogic;
        private GameObject[,] cellObjects;
        private Image[,] cellImages;                // 색상 적용 대상(Inner)
        private RectTransform[,] cellRects;         // 위치 계산용
        private bool actionButtonsVisible = false;

        private Block previewBlock;
        private Position previewPosition = new Position(-1, -1);

        // 펜딩 배치
        private Block pendingBlock;
        private Position pendingPosition = new Position(-1, -1);
        private bool hasPendingPlacement = false;

        // 이벤트
        public System.Action<Position> OnCellClicked;
        public System.Action<Position> OnCellHover;
        public System.Action<Block, Position> OnBlockPlaced;

        private bool initialized = false;

        // 안전한 노출
        public float CellSize => cellSize;
        public int BoardSize => boardSize;
        public RectTransform CellParent => cellParent;

        private void Awake()
        {
            if (gameLogic == null) gameLogic = new GameLogic();
        }

        private void Start()
        {
            if (initialized) return;
            initialized = true;

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

            // --- Action Buttons 초기화 ---
            if (actionButtonPanel != null)
            {
                // ★ 패널 배경이 보드 클릭을 가로채지 않도록 차단
                var bg = actionButtonPanel.GetComponent<Image>();
                if (bg != null) bg.raycastTarget = false;
                actionButtonPanel.gameObject.SetActive(false);
            }
            if (rotateButton != null)
            {
                rotateButton.onClick.RemoveAllListeners();
                rotateButton.onClick.AddListener(() => OnRotatePendingBlock());
            }
            if (flipButton != null)
            {
                flipButton.onClick.RemoveAllListeners();
                flipButton.onClick.AddListener(() => OnFlipPendingBlock());
            }
            if (placeButton != null)
            {
                placeButton.onClick.RemoveAllListeners();
                placeButton.onClick.AddListener(() => OnConfirmPlacement());
            }
        }

        public void OnCellClickedInternal(int row, int col)
        {
            var pos = new Position(row, col);
            OnCellClicked?.Invoke(pos);
        }

        public void OnCellHoverInternal(int row, int col)
        {
            var pos = new Position(row, col);
            OnCellHover?.Invoke(pos);
        }

        // ========================================
        // 외부 연동
        // ========================================
        public void SetGameLogic(GameLogic logic)
        {
            gameLogic = logic ?? new GameLogic();
            RefreshBoard();
        }

        public GameLogic GetGameLogic() => gameLogic;
        public int GetBoardSize() => boardSize;

        public void ClearBoard()
        {
            gameLogic.ClearBoard();
            RefreshBoard();
            ClearTouchPreview();
        }

        public Position ScreenToBoard(Vector2 screenPos)
        {
            var cam = GetComponentInParent<Canvas>()?.worldCamera ?? Camera.main;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(cellParent, screenPos, cam, out var local);

            float x0 = -(boardSize * 0.5f - 0.5f) * cellSize;
            float y0 = +(boardSize * 0.5f - 0.5f) * cellSize;
            int col = Mathf.FloorToInt((local.x - x0) / cellSize);
            int row = Mathf.FloorToInt((y0 - local.y) / cellSize);
            col = Mathf.Clamp(col, 0, boardSize - 1);
            row = Mathf.Clamp(row, 0, boardSize - 1);
            return new Position(row, col);
        }

        // ========================================
        // 보드 생성(UI)
        // ========================================
        private void CreateBoardCells()
        {
            if (cellParent == null)
            {
                Debug.LogError("CellParent(RectTransform)가 없습니다.");
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

                // 메인(배경) 이미지
                var bg = cellObj.AddComponent<Image>();
                bg.sprite = uiFallbackSprite;        // ★추가
                bg.type = Image.Type.Simple;
                bg.color = Color.white;             // 기본 빈칸색


                // 테두리 + Inner
                CreateCellBorder(cellObj);

                // 클릭 감지용 버튼
                cellObj.AddComponent<Button>().targetGraphic = bg;
            }

            var rect = cellObj.GetComponent<RectTransform>();
            if (rect == null) rect = cellObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(cellSize, cellSize);
            rect.localScale = Vector3.one;

            // 중앙 기준 배치(UI 상단이 +Y)
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
            var borderObj = new GameObject("Border", typeof(RectTransform));
            borderObj.transform.SetParent(cellObj.transform, false);

            var borderImage = borderObj.AddComponent<Image>();
            borderImage.sprite = uiFallbackSprite; // ★추가
            borderImage.type = Image.Type.Simple;
            borderImage.color = gridLineColor;

            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;

            var innerObj = new GameObject("Inner", typeof(RectTransform));
            innerObj.transform.SetParent(borderObj.transform, false);

            var innerImage = innerObj.AddComponent<Image>();
            innerImage.sprite = uiFallbackSprite; // ★추가
            innerImage.type = Image.Type.Simple;
            innerImage.color = emptyColor;

            var innerRect = innerObj.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = Vector2.one * gridLineWidth;
            innerRect.offsetMax = Vector2.one * -gridLineWidth;
        }

        // ========================================
        // 보드 리프레시/색상
        // ========================================
        public void RefreshBoard()
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
                _ => emptyColor
            };
        }

        // ========================================
        // 배치/프리뷰 + (첫 수 4코너 허용 오버라이드)
        // ========================================
        public bool TryPlaceBlock(Block block, Position position)
        {
            if (block == null || !ValidationUtility.IsValidPosition(position)) return false;

            // 보드 밖이면 실패
            if (!CanPlaceWithinBoard(block, position)) return false;

            // 원래 규칙으로 가능 → 그대로 PlaceBlock
            if (!gameLogic.CanPlaceBlock(block, position)) return false;
            var placement = new BlockPlacement(block.Type, position, block.CurrentRotation, block.CurrentFlipState, block.Player);
            var ok = gameLogic.PlaceBlock(placement);   // bool이면 그대로, void면 호출 후 RefreshBoard로 확인
            if (!ok) return false;                      // (PlaceBlock이 bool일 때만)
            RefreshBoard();
            OnBlockPlaced?.Invoke(block, position);
            return true;
        }

        public void SetPreview(Block block, Position position)
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
            ShowActionButtons(canPlace);
            var col = canPlace ? previewColor : invalidColor;

            foreach (var pos in cells)
            {
                if (!ValidationUtility.IsValidPosition(pos)) continue;
                if (pos.row >= boardSize || pos.col >= boardSize) continue;

                // 빈 칸만 프리뷰 컬러
                if (gameLogic.GetCellColor(pos) == PlayerColor.None)
                {
                    var img = cellImages[pos.row, pos.col];
                    if (img != null) img.color = col;
                }
            }
            PositionActionButtonsAtBlock();
        }

        public void ClearPreview()
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

        // 터치 경로(미리보기 + 확정 버튼)
        public void SetTouchPreview(Block block, Position position)
        {
            if (block == null || !ValidationUtility.IsValidPosition(position))
            {
                ClearTouchPreview();
                return;
            }

            SetPreview(block, position);

            pendingBlock = block;
            pendingPosition = position;
            hasPendingPlacement = true;

            bool canPlace = gameLogic.CanPlaceBlock(block, position);
            ShowActionButtons(canPlace);

            Debug.Log($"터치 미리보기: {block.Type} @ ({position.row},{position.col}) - {(canPlace ? "가능" : "불가")}");
        }

        public void ClearTouchPreview()
        {
            ClearPreview();
            HideActionButtons();
        }

        private void OnRotatePendingBlock()
        {
            if (pendingBlock == null || !ValidationUtility.IsValidPosition(pendingPosition)) return;

            // ★ 먼저 이전 프리뷰 지우기
            ClearPreview();

            pendingBlock.RotateClockwise();
            SetPreview(pendingBlock, pendingPosition);
            bool canPlace = gameLogic.CanPlaceBlock(pendingBlock, pendingPosition);
            ShowActionButtons(canPlace);
        }

        private void OnFlipPendingBlock()
        {
            if (pendingBlock == null || !ValidationUtility.IsValidPosition(pendingPosition)) return;

            // ★ 먼저 이전 프리뷰 지우기
            ClearPreview();

            pendingBlock.FlipHorizontal();
            SetPreview(pendingBlock, pendingPosition);
            bool canPlace = gameLogic.CanPlaceBlock(pendingBlock, pendingPosition);
            ShowActionButtons(canPlace);
        }

        public void OnConfirmPlacement()
        {
            if (!hasPendingPlacement || pendingBlock == null || !ValidationUtility.IsValidPosition(pendingPosition))
            {
                Debug.LogWarning("확정할 배치가 없습니다");
                return;
            }

            if (TryPlaceBlock(pendingBlock, pendingPosition))
            {
                Debug.Log($"블록 배치 확정 완료: {pendingBlock.Type} at ({pendingPosition.row}, {pendingPosition.col})");
                ClearPreview();
                HideActionButtons();

                OnBlockPlaced?.Invoke(pendingBlock, pendingPosition);
            }
            else
            {
                Debug.LogError("블록 배치 확정 실패!");
                // ★ 실패 후에도 UI/입력 정상화
                ClearPreview();
                HideActionButtons();
            }
        }

        // ===== 액션 버튼 표시/숨김/위치 =====
        private void ShowActionButtons(bool canPlace)
        {
            if (actionButtonPanel == null) return;
            actionButtonsVisible = true;
            actionButtonPanel.gameObject.SetActive(true);

            if (placeButton != null)
            {
                placeButton.interactable = canPlace;
                var img = placeButton.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.color = canPlace ? Color.green : Color.red;
            }

            PositionActionButtonsAtBlock();
        }

        private void HideActionButtons()
        {
            if (actionButtonPanel == null) return;
            actionButtonsVisible = false;
            actionButtonPanel.gameObject.SetActive(false);
        }

        private Vector3 BoardToWorld(Position p)
        {
            // 안전 체크 후 해당 셀 RectTransform의 월드 좌표(중심) 반환
            if (cellRects != null &&
                p.row >= 0 && p.row < boardSize &&
                p.col >= 0 && p.col < boardSize &&
                cellRects[p.row, p.col] != null)
            {
                return cellRects[p.row, p.col].TransformPoint(Vector3.zero);
                // (pivot이 0.5,0.5일 때 중심 좌표)
            }

            // 폴백: 보드 중심
            if (cellParent != null) return cellParent.TransformPoint(Vector3.zero);
            return transform.TransformPoint(Vector3.zero);
        }

        private void PositionActionButtonsAtBlock()
        {
            if (!actionButtonsVisible || actionButtonPanel == null || pendingBlock == null) return;

            // 펜딩 블록 경계 계산
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

            // 블록 우측 상단 근처
            Vector3 targetWorldPos = BoardToWorld(new Position(minRow, maxCol));

            // Canvas 좌표로 변환/적용
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Camera uiCamera = canvas.worldCamera ?? Camera.main;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            RectTransform buttonRect = actionButtonPanel;

            if (buttonRect != null && uiCamera != null)
            {
                Vector2 screenPos = uiCamera.WorldToScreenPoint(targetWorldPos);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out var localPos))
                {
                    Vector2 buttonOffset = new Vector2(cellSize * 1.2f, -cellSize * 0.6f);
                    buttonRect.anchoredPosition = localPos + buttonOffset;
                }
            }
        }

        private void EnsureFallbackSprite()
        {
            if (uiFallbackSprite != null) return;
            var tex = Texture2D.whiteTexture; // 내장 화이트
            uiFallbackSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private bool CanPlaceWithinBoard(Block block, Position position)
        {
            foreach (var p in block.GetAbsolutePositions(position))
                if (p.row < 0 || p.col < 0 || p.row >= boardSize || p.col >= boardSize)
                    return false;
            return true;
        }
    }
}
