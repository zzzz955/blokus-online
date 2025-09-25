using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using App.Services;
using Shared.Models;
using Shared.UI;
using Features.Single.Gameplay;
using SharedGameLogic = App.Core.GameLogic;
using SharedPosition = Shared.Models.Position;

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

        [Header("스킨")]
        [SerializeField] private Features.Single.Gameplay.Skins.BlockSkin skin;

        [Header("셀 스프라이트 시스템")]
        [SerializeField] private Features.Single.Gameplay.CellSpriteProvider cellSpriteProvider;

        [Header("줌/팬 기능")]
        [SerializeField] private GameBoardZoomPan zoomPanComponent;

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
            InitializeZoomPan();
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
            float overflow = 0f;
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
        /// 개별 셀 업데이트 (GAME_STATE_UPDATE 동기화용)
        /// </summary>
        public void UpdateCell(int row, int col, PlayerColor playerColor)
        {
            if (row >= 0 && row < boardSize && col >= 0 && col < boardSize)
            {
                UpdateCellVisual(row, col, playerColor);
                SetBoardCell(row, col, playerColor);
            }
        }

        /// <summary>
        /// 블록 배치 (서버에서 확정된 배치 적용)
        /// </summary>
        public void PlaceBlock(Position position, int playerId, List<Position> occupiedCells)
        {
            PlayerColor playerColor = (PlayerColor)(playerId + 1);
            bool isMyBlock = playerId == ((int)myPlayerColor - 1);
            string playerType = isMyBlock ? "내" : "상대";
            
            Debug.Log($"[MultiGameBoard] {playerType} 블록 배치 확정: Player {playerId}({playerColor}) at ({position.row}, {position.col}), " +
                     $"점유셀 {occupiedCells.Count}개");
            
            try
            {
                int updatedCells = 0;
                
                // 실제 점유된 셀들에 색상 적용
                foreach (var cellPos in occupiedCells)
                {
                    if (ValidationUtility.IsValidPosition(cellPos) && 
                        cellPos.row < boardSize && cellPos.col < boardSize)
                    {
                        UpdateCellVisual(cellPos.row, cellPos.col, playerColor);
                        SetBoardCell(cellPos.row, cellPos.col, playerColor);
                        updatedCells++;
                    }
                    else
                    {
                        Debug.LogWarning($"[MultiGameBoard] 범위 밖 셀 위치: ({cellPos.row}, {cellPos.col})");
                    }
                }
                
                Debug.Log($"[MultiGameBoard] {playerType} 블록 배치 완료: {updatedCells}/{occupiedCells.Count}개 셀 업데이트");
                
                // 내 블록이면 미리보기 정리, 상대 블록이면 정리하지 않음
                if (isMyBlock)
                {
                    ClearTouchPreview();
                    Debug.Log("[MultiGameBoard] 내 블록 배치 완료 - 미리보기 정리");
                }
                else
                {
                    Debug.Log("[MultiGameBoard] 상대 블록 배치 완료 - 미리보기 유지");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MultiGameBoard] {playerType} 블록 배치 중 오류 발생: {ex.Message}");
            }
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

            // CellSpriteProvider 우선 사용 (스프라이트 기반 렌더링)
            if (cellSpriteProvider != null)
            {
                var sprite = cellSpriteProvider.GetSprite(color);
                if (sprite != null)
                {
                    img.sprite = sprite;
                    // 스프라이트 사용 시 색상을 흰색으로 설정하여 스프라이트 원본 색상이 보이도록 함
                    img.color = Color.white;
                    return;
                }
            }

            // CellSpriteProvider가 없거나 스프라이트가 null인 경우 색상 기반 렌더링
            img.color = GetPlayerColor(color);
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

            Debug.Log("[MultiGameBoard] 터치 미리보기 클리어 - ActionButtonPanel 숨김");
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

            // PositionActionButtonsAtBlock(); // 제거됨: ActionButtonPanel 호버링 기능 비활성화
        }

        private void HideActionButtons()
        {
            if (actionButtonPanel == null) return;

            actionButtonsVisible = false;
            actionButtonPanel.gameObject.SetActive(false);
        }

        // PositionActionButtonsAtBlock() 메서드 제거됨: ActionButtonPanel 호버링 기능 비활성화

        // IsPositionWithinScreenBounds() 및 ClampPositionToScreenBounds() 메서드 제거됨: ActionButtonPanel 호버링 기능 비활성화

        private Vector3 BoardToWorld(Position p)
        {
            // 기본적인 보드 좌표 → 월드 좌표 변환
            // 보드 중앙을 (0,0)으로 하는 좌표계
            float worldX = (p.col - boardSize * 0.5f + 0.5f) * cellSize;
            float worldY = (boardSize * 0.5f - p.row - 0.5f) * cellSize;
            return transform.TransformPoint(new Vector3(worldX, worldY, 0f));
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
        public GameBoardZoomPan ZoomPanComponent => zoomPanComponent;
        public bool CanInteract => isInteractable && isMyTurn;

        /// <summary>
        /// Single GameBoard 호환용 - BoardSize 속성
        /// </summary>
        public int BoardSize => boardSize;

        /// <summary>
        /// 스크린 좌표를 보드 좌표로 변환 (줌/팬 상태 고려)
        /// </summary>
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

        /// <summary>
        /// 플레이어 색상 반환 (BlockButton과 색상 통일을 위함)
        /// 블록 스킨이 설정되어 있으면 스킨 색상을 우선 사용
        /// </summary>
        public Color GetPlayerColor(PlayerColor player)
        {
            // 스킨이 설정되어 있으면 스킨 색상 사용
            if (skin != null)
            {
                return skin.GetTint(player);
            }

            // 기본 색상 사용
            return player switch
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
        /// 지정된 위치의 셀 색상을 가져옵니다
        /// </summary>
        public PlayerColor GetCellColor(int row, int col)
        {
            if (gameLogic == null)
            {
                Debug.LogWarning("[GameBoard] gameLogic이 null입니다.");
                return PlayerColor.None;
            }

            if (row < 0 || row >= boardSize || col < 0 || col >= boardSize)
            {
                Debug.LogWarning($"[GameBoard] 잘못된 좌표: ({row}, {col})");
                return PlayerColor.None;
            }

            return gameLogic.GetCellColor(new SharedPosition(row, col));
        }

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

        /// <summary>
        /// 줌/팬 기능 초기화
        /// </summary>
        private void InitializeZoomPan()
        {
            Debug.Log("[MultiGameBoard] ===== 줌/팬 기능 초기화 시작 =====");

            // RectTransform 확인
            RectTransform rect = GetComponent<RectTransform>();
            if (rect == null)
            {
                Debug.LogWarning("[MultiGameBoard] ⚠️ GameBoard GameObject에 RectTransform이 없습니다.");
                Debug.Log("[MultiGameBoard] GameObject 이름: " + gameObject.name);
                Debug.Log("[MultiGameBoard] 부모: " + (transform.parent != null ? transform.parent.name : "없음"));
                Debug.Log("[MultiGameBoard] Canvas를 찾을 수 있나요: " + (GetComponentInParent<Canvas>() != null));

                // 부모에서 Canvas를 찾아보고 정보 출력
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    Debug.Log("[MultiGameBoard] 발견된 Canvas: " + canvas.name + " (renderMode: " + canvas.renderMode + ")");
                }

                // cellParent를 대신 사용하는 방식으로 시도
                if (cellParent != null)
                {
                    Debug.Log("[MultiGameBoard]  GameBoard 대신 cellParent에 줌/팬 기능을 추가합니다.");

                    // cellParent에 GameBoardZoomPan 추가
                    GameBoardZoomPan cellParentZoomPan = cellParent.GetComponent<GameBoardZoomPan>();
                    if (cellParentZoomPan == null)
                    {
                        cellParentZoomPan = cellParent.gameObject.AddComponent<GameBoardZoomPan>();
                        Debug.Log("[MultiGameBoard]  cellParent에 GameBoardZoomPan 컴포넌트 추가됨");
                    }

                    // 줌 타겟을 cellParent 자기 자신으로 설정
                    cellParentZoomPan.SetZoomTarget(cellParent);

                    // 참조 저장 (GameBoard에서 접근할 수 있도록)
                    zoomPanComponent = cellParentZoomPan;

                    Debug.Log("[MultiGameBoard]  cellParent 기반 줌/팬 기능 초기화 완료");
                    return;
                }
                else
                {
                    Debug.LogError("[MultiGameBoard]  cellParent도 null이어서 줌/팬 기능을 초기화할 수 없습니다!");
                    return;
                }
            }

            Debug.Log("[MultiGameBoard]  GameBoard에 RectTransform 발견됨");

            // GameBoardZoomPan 컴포넌트가 없으면 추가
            if (zoomPanComponent == null)
            {
                zoomPanComponent = GetComponent<GameBoardZoomPan>();
                if (zoomPanComponent == null)
                {
                    zoomPanComponent = gameObject.AddComponent<GameBoardZoomPan>();
                    Debug.Log("[MultiGameBoard] GameBoardZoomPan 컴포넌트 자동 추가됨");
                }
            }

            // 줌 타겟을 cellParent로 설정
            if (cellParent != null)
            {
                zoomPanComponent.SetZoomTarget(cellParent);
                Debug.Log("[MultiGameBoard]  줌/팬 기능 초기화 완료 - Target: cellParent");
            }
            else
            {
                Debug.LogError("[MultiGameBoard]  cellParent가 null이어서 줌/팬 기능을 초기화할 수 없습니다!");
            }

            Debug.Log("[MultiGameBoard] ===== 줌/팬 기능 초기화 완료 =====");
        }

        /// <summary>
        /// 줌/팬 상태 초기화
        /// </summary>
        public void ResetZoomPan()
        {
            if (zoomPanComponent != null)
            {
                zoomPanComponent.ResetZoomPan();
                Debug.Log("[MultiGameBoard] 줌/팬 상태 초기화됨");
            }
        }

        /// <summary>
        /// 현재 줌 레벨 반환
        /// </summary>
        public float GetCurrentZoom()
        {
            return zoomPanComponent != null ? zoomPanComponent.GetCurrentZoom() : 1.0f;
        }

        /// <summary>
        /// 현재 팬 오프셋 반환
        /// </summary>
        public Vector2 GetCurrentPan()
        {
            return zoomPanComponent != null ? zoomPanComponent.GetCurrentPan() : Vector2.zero;
        }

        /// <summary>
        /// 셀들의 raycastTarget을 조건부로 비활성화하여 드래그 이벤트가 GridContainer에 도달하도록 함
        /// </summary>
        /// <param name="enableCellRaycast">true면 셀 raycast 활성화, false면 비활성화</param>
        public void SetCellRaycastEnabled(bool enableCellRaycast)
        {
            if (cellImages == null) return;

            Debug.Log($"[MultiGameBoard] 셀 raycastTarget 설정: {enableCellRaycast}");

            for (int row = 0; row < boardSize; row++)
            {
                for (int col = 0; col < boardSize; col++)
                {
                    if (cellObjects[row, col] != null)
                    {
                        // 메인 셀 이미지
                        Image mainImage = cellObjects[row, col].GetComponent<Image>();
                        if (mainImage != null)
                        {
                            mainImage.raycastTarget = enableCellRaycast;
                        }

                        // Border와 Inner 이미지들
                        Transform border = cellObjects[row, col].transform.Find("Border");
                        if (border != null)
                        {
                            Image borderImage = border.GetComponent<Image>();
                            if (borderImage != null)
                            {
                                borderImage.raycastTarget = enableCellRaycast;
                            }

                            Transform inner = border.Find("Inner");
                            if (inner != null)
                            {
                                Image innerImage = inner.GetComponent<Image>();
                                if (innerImage != null)
                                {
                                    innerImage.raycastTarget = enableCellRaycast;
                                }
                            }
                        }

                        // Button 컴포넌트는 클릭 기능을 위해 유지하되, interactable로 제어
                        Button cellButton = cellObjects[row, col].GetComponent<Button>();
                        if (cellButton != null)
                        {
                            cellButton.interactable = enableCellRaycast;
                        }
                    }
                }
            }

            Debug.Log($"[MultiGameBoard] 모든 셀의 raycastTarget 설정 완료: {enableCellRaycast}");
        }
    }
}

