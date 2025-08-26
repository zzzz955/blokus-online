using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using App.Services;
using Features.Single.Core;
using Features.Single.UI.InGame;
using Shared.Models;
using App.Core; // GameLogic
namespace Features.Single.Gameplay
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
        [SerializeField] private Color emptyColor = new Color(0.9f, 0.9f, 0.9f, 1f); // 밝은 회색
        [SerializeField] private Color previewColor = Color.yellow;
        [SerializeField] private Color invalidColor = Color.red;
        [SerializeField] private Color blueColor = Color.blue;
        [SerializeField] private Color yellowColor = Color.yellow;
        [SerializeField] private Color redColor = Color.red;
        [SerializeField] private Color greenColor = Color.green;
        [SerializeField] private Color obstacleColor = new Color(0.3f, 0.3f, 0.3f, 1f); // 어두운 회색

        [Header("터치 UI")]
        // === Action Buttons (우측 상단 패널) ===
        [SerializeField] private RectTransform actionButtonPanel;
        [SerializeField] private UnityEngine.UI.Button rotateButton;
        [SerializeField] private UnityEngine.UI.Button flipButton;
        [SerializeField] private UnityEngine.UI.Button placeButton;
        [SerializeField] private Sprite uiFallbackSprite;

        [Header("Action Button Sprites")]
        [SerializeField] private Sprite placeButtonEnabledSprite;   // 배치 가능 시 sprite
        [SerializeField] private Sprite placeButtonDisabledSprite;  // 배치 불가 시 sprite

        [Header("스킨")]
        [SerializeField] private Features.Single.Gameplay.Skins.BlockSkin skin;

        [Header("셀 스프라이트 시스템")]
        [SerializeField] private CellSpriteProvider cellSpriteProvider;

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
                _ => Color.white
            };
        }

        private void Awake()
        {
            if (gameLogic == null) gameLogic = new GameLogic();

            // ActionButtonPanel을 즉시 숨김 (Inspector에서 활성화되어 있을 수 있음)
            if (actionButtonPanel != null)
            {
                // ActionButtonPanel 크기 강제 설정 (Canvas 전체 크기로 잘못 설정되는 문제 해결)
                actionButtonPanel.sizeDelta = new Vector2(200f, 100f);
                actionButtonPanel.gameObject.SetActive(false);
                actionButtonsVisible = false;
                Debug.Log($"[GameBoard] Awake에서 ActionButtonPanel 강제 숨김 및 크기 설정: {actionButtonPanel.sizeDelta}");
            }
        }

        private void Start()
        {
            if (initialized) return;
            initialized = true;

            // CellSpriteProvider 연결 상태 디버깅
            Debug.Log($"[GameBoard] Start() - CellSpriteProvider 연결됨: {cellSpriteProvider != null}");

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

                // 초기에는 숨김 상태
                actionButtonPanel.gameObject.SetActive(false);
                actionButtonsVisible = false;

                Debug.Log("[GameBoard] ActionButtonPanel 초기화 완료 - 숨김 상태로 설정");
            }
            else
            {
                Debug.LogError("[GameBoard] ActionButtonPanel이 Inspector에서 연결되지 않았습니다!");
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

        /// <summary>
        /// 보드만 클리어 (터치 미리보기는 건드리지 않음) - Undo 전용
        /// </summary>
        public void ClearBoardOnly()
        {
            gameLogic.ClearBoard();
            RefreshBoard();
            // ClearTouchPreview() 호출하지 않음 - Undo에서 UI 충돌 방지
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

            // 스프라이트 시스템이 설정되어 있으면 빈 칸 스프라이트 적용
            if (cellSpriteProvider != null)
            {
                Sprite emptySprite = cellSpriteProvider.GetSprite(PlayerColor.None);
                inner.sprite = emptySprite;
                inner.color = Color.white; // 스프라이트 원본 색상 유지
                // Debug.Log($"[GameBoard] CreateCellAt({row},{col}) - 빈 칸 스프라이트 설정: {(emptySprite != null ? emptySprite.name : "NULL")}");
            }
            else
            {
                inner.color = emptyColor; // 폴백: 기존 색상 시스템
                Debug.LogWarning($"[GameBoard] CreateCellAt({row},{col}) - cellSpriteProvider가 null, 색상 시스템 사용");
            }

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
            borderImage.sprite = uiFallbackSprite; // ★추가
            borderImage.type = Image.Type.Simple;
            borderImage.color = gridLineColor;

            var borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-overflow, -overflow); // 왼/아래 바깥으로
            borderRect.offsetMax = new Vector2(overflow, overflow);  // 오른/위 바깥으로

            var innerObj = new GameObject("Inner", typeof(RectTransform));
            innerObj.transform.SetParent(borderObj.transform, false);

            var innerImage = innerObj.AddComponent<Image>();

            // 스프라이트 시스템이 설정되어 있으면 빈 칸 스프라이트 적용
            if (cellSpriteProvider != null)
            {
                Sprite emptySprite = cellSpriteProvider.GetSprite(PlayerColor.None);
                innerImage.sprite = emptySprite;
                innerImage.color = Color.white; // 스프라이트 원본 색상 유지
                // Debug.Log($"[GameBoard] CreateCellBorder - 빈 칸 스프라이트 설정: {(emptySprite != null ? emptySprite.name : "NULL")}");
            }
            else
            {
                innerImage.sprite = uiFallbackSprite;
                innerImage.color = emptyColor; // 폴백: 기존 색상 시스템
                Debug.LogWarning($"[GameBoard] CreateCellBorder - cellSpriteProvider가 null, uiFallbackSprite 사용");
            }

            innerImage.type = Image.Type.Simple;

            var innerRect = innerObj.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = Vector2.one * Mathf.Max(0, gridLineWidth - overflow);
            innerRect.offsetMax = Vector2.one * -Mathf.Max(0, gridLineWidth - overflow);
        }

        // ========================================
        // 보드 리프레시/색상
        // ========================================
        public void RefreshBoard()
        {
            if (cellImages == null) return;

            int nonEmptyCount = 0;

            for (int r = 0; r < boardSize; r++)
                for (int c = 0; c < boardSize; c++)
                {
                    var color = gameLogic.GetBoardCell(r, c);
                    UpdateCellVisual(r, c, color);

                    // 비어있지 않은 셀 카운트
                    if (color != PlayerColor.None)
                        nonEmptyCount++;
                }

            Debug.Log($"[GameBoard] RefreshBoard 완료 - 채워진 셀 수: {nonEmptyCount}");
        }

        private void UpdateCellVisual(int row, int col, PlayerColor color)
        {
            var img = cellImages[row, col];
            if (img == null)
            {
                Debug.LogError($"[GameBoard] UpdateCellVisual({row},{col}) - cellImages가 null입니다!");
                return;
            }

            // 디버그 로그
            // Debug.Log($"[GameBoard] UpdateCellVisual({row},{col}) - Color: {color}, cellSpriteProvider: {cellSpriteProvider != null}");

            // 스프라이트 시스템이 설정되어 있으면 스프라이트 사용
            if (cellSpriteProvider != null)
            {
                Sprite sprite = cellSpriteProvider.GetSprite(color);
                img.sprite = sprite;
                img.color = Color.white; // 스프라이트는 원본 색상 유지
                // Debug.Log($"[GameBoard] 스프라이트 설정: ({row},{col}) = {color} → {(sprite != null ? sprite.name : "NULL")}");
            }
            else
            {
                Debug.LogWarning($"[GameBoard] cellSpriteProvider가 null입니다! 기존 색상 시스템 사용");
                // 폴백: 기존 색상 시스템 사용
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
            var col = canPlace ? previewColor : invalidColor;

            foreach (var pos in cells)
            {
                if (!ValidationUtility.IsValidPosition(pos)) continue;
                if (pos.row >= boardSize || pos.col >= boardSize) continue;

                // 빈 칸만 프리뷰 컬러
                if (gameLogic.GetCellColor(pos) == PlayerColor.None)
                {
                    var img = cellImages[pos.row, pos.col];
                    if (img != null)
                    {
                        // 스프라이트 시스템에서는 색상 틴트로 프리뷰 표현
                        if (cellSpriteProvider != null)
                        {
                            img.color = col; // 스프라이트에 색상 틴트 적용
                        }
                        else
                        {
                            img.color = col; // 기존 방식 유지
                        }
                    }
                }
            }
            // PositionActionButtonsAtBlock() 제거 - ShowActionButtons에서 이미 처리됨
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
                    if (img != null)
                    {
                        // 스프라이트 시스템에서는 빈 칸 스프라이트로 복원
                        if (cellSpriteProvider != null)
                        {
                            img.sprite = cellSpriteProvider.GetSprite(PlayerColor.None);
                            img.color = Color.white;
                        }
                        else
                        {
                            img.color = emptyColor; // 기존 방식 유지
                        }
                    }
                }
            }

            previewBlock = null;
            previewPosition = new Position(-1, -1);
        }

        // 터치 경로(미리보기 + 확정 버튼)
        public void SetTouchPreview(Block block, Position position)
        {
            // Debug.Log($"[GameBoard] SetTouchPreview 호출됨 - Block: {block?.Type}, Position: ({position.row}, {position.col})");

            if (block == null || !ValidationUtility.IsValidPosition(position))
            {
                Debug.Log("[GameBoard] SetTouchPreview - 잘못된 블록 또는 위치로 인한 클리어");
                ClearTouchPreview();
                return;
            }

            // 이전 상태 완전 클리어
            ClearPreview();
            HideActionButtons();

            // 새로운 블록 설정
            pendingBlock = block;
            pendingPosition = position;
            hasPendingPlacement = true;

            // Debug.Log($"[GameBoard] pendingBlock 설정됨: {pendingBlock.Type} at ({pendingPosition.row}, {pendingPosition.col})");

            // 미리보기 표시
            SetPreview(block, position);

            // 배치 가능 여부 확인 및 액션 버튼 표시
            bool canPlace = gameLogic.CanPlaceBlock(block, position);
            // Debug.Log($"[GameBoard] 블록 배치 가능 여부: {canPlace}");

            ShowActionButtons(canPlace);

            // Debug.Log($"터치 미리보기 완료: {block.Type} @ ({position.row},{position.col}) - {(canPlace ? "가능" : "불가")}");
        }

        public void ClearTouchPreview()
        {
            ClearPreview();
            HideActionButtons();

            // 펜딩 상태 초기화
            pendingBlock = null;
            pendingPosition = new Position(-1, -1);
            hasPendingPlacement = false;

            Debug.Log("[GameBoard] 터치 미리보기 클리어 - ActionButtonPanel 숨김");
        }

        private void OnRotatePendingBlock()
        {
            if (pendingBlock == null || !ValidationUtility.IsValidPosition(pendingPosition)) return;

            // ★ 먼저 이전 프리뷰 지우기
            ClearPreview();

            pendingBlock.RotateClockwise();
            SetPreview(pendingBlock, pendingPosition);
            bool canPlace = gameLogic.CanPlaceBlock(pendingBlock, pendingPosition);

            // 배치 가능성에 따른 버튼 상태 업데이트
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

            // 배치 가능성에 따른 버튼 상태 업데이트
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
                // OnBlockPlaced 이벤트는 TryPlaceBlock()에서 이미 호출됨 (중복 제거)
            }
            else
            {
                Debug.LogError("블록 배치 확정 실패!");
                ClearPreview();
                HideActionButtons();
            }
        }

        // ===== 액션 버튼 표시/숨김/위치 =====
        private void ShowActionButtons(bool canPlace)
        {
            if (actionButtonPanel == null)
            {
                Debug.LogError("[GameBoard] ActionButtonPanel이 연결되지 않았습니다! Inspector에서 ActionButtonPanel을 연결해주세요.");
                return;
            }

            Vector2 beforePosition = actionButtonPanel.anchoredPosition;

            actionButtonsVisible = true;
            actionButtonPanel.gameObject.SetActive(true);

            // Debug.Log($"[GameBoard] ★ ActionButtonPanel 표시됨 ★ - 배치 가능: {canPlace}, 현재 위치: {beforePosition}");

            if (placeButton != null)
            {
                placeButton.interactable = canPlace;
                var img = placeButton.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    // sprite 기반 상태 변경 (색상 변경 대신)
                    if (canPlace && placeButtonEnabledSprite != null)
                    {
                        img.sprite = placeButtonEnabledSprite;
                        img.color = Color.white; // sprite 원본 색상 유지
                    }
                    else if (!canPlace && placeButtonDisabledSprite != null)
                    {
                        img.sprite = placeButtonDisabledSprite;
                        img.color = Color.white; // sprite 원본 색상 유지
                    }
                    else
                    {
                        // 폴백: sprite가 설정되지 않은 경우 기존 색상 시스템 사용
                        img.color = canPlace ? Color.green : Color.red;
                    }
                }
            }

            PositionActionButtonsAtBlock();

            Vector2 afterPosition = actionButtonPanel.anchoredPosition;
            // Debug.Log($"[GameBoard] ★ ActionButtonPanel 위치 업데이트 완료 ★ - {beforePosition} → {afterPosition}");
        }

        private void HideActionButtons()
        {
            if (actionButtonPanel == null) return;

            // Debug.Log("[GameBoard] ActionButtonPanel 숨김 처리");
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
            if (!actionButtonsVisible || actionButtonPanel == null || pendingBlock == null)
            {
                Debug.LogWarning($"[GameBoard] ActionButtonPanel 위치 설정 실패 - actionButtonsVisible: {actionButtonsVisible}, actionButtonPanel: {actionButtonPanel != null}, pendingBlock: {pendingBlock != null}");
                return;
            }

            // Debug.Log($"[GameBoard] ActionButtonPanel 위치 계산 시작 - 블록: {pendingBlock.Type}, 위치: ({pendingPosition.row}, {pendingPosition.col})");

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

            // Canvas 좌표로 변환/적용
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Camera uiCamera = canvas.worldCamera ?? Camera.main;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            RectTransform buttonRect = actionButtonPanel;

            if (buttonRect != null && uiCamera != null)
            {
                Vector2 panelSize = buttonRect.sizeDelta;
                float safeMargin = 20f; // 안전 마진 증가

                // 여러 위치 후보를 시도 (우선순위: 우상 → 좌상 → 우하 → 좌하)
                Vector3[] candidateWorldPositions = new Vector3[]
                {
                    BoardToWorld(new Position(minRow, maxCol)), // 우측 상단
                    BoardToWorld(new Position(minRow, minCol)), // 좌측 상단  
                    BoardToWorld(new Position(maxRow, maxCol)), // 우측 하단
                    BoardToWorld(new Position(maxRow, minCol))  // 좌측 하단
                };

                Vector2[] offsets = new Vector2[]
                {
                    new Vector2(cellSize * 0.5f + panelSize.x * 0.5f + safeMargin, cellSize * 0.5f + panelSize.y * 0.5f + safeMargin),   // 우상
                    new Vector2(-(cellSize * 0.5f + panelSize.x * 0.5f + safeMargin), cellSize * 0.5f + panelSize.y * 0.5f + safeMargin), // 좌상
                    new Vector2(cellSize * 0.5f + panelSize.x * 0.5f + safeMargin, -(cellSize * 0.5f + panelSize.y * 0.5f + safeMargin)), // 우하
                    new Vector2(-(cellSize * 0.5f + panelSize.x * 0.5f + safeMargin), -(cellSize * 0.5f + panelSize.y * 0.5f + safeMargin)) // 좌하
                };

                Vector2 finalPosition = Vector2.zero;
                bool positionFound = false;

                // 각 위치 후보를 시도하여 화면 안에 들어오는 첫 번째 위치 사용
                for (int i = 0; i < candidateWorldPositions.Length; i++)
                {
                    Vector2 screenPos = uiCamera.WorldToScreenPoint(candidateWorldPositions[i]);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out var localPos))
                    {
                        Vector2 candidatePosition = localPos + offsets[i];

                        // 화면 경계 체크 (실제 화면 크기 기준)
                        if (IsPositionWithinScreenBounds(candidatePosition, panelSize, canvasRect, safeMargin))
                        {
                            finalPosition = candidatePosition;
                            positionFound = true;
                            break;
                        }
                    }
                }

                // 모든 후보가 실패하면 강제 클램핑으로 화면 안에 위치시키기
                if (!positionFound)
                {
                    Vector2 screenPos = uiCamera.WorldToScreenPoint(candidateWorldPositions[0]);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out var localPos))
                    {
                        Vector2 targetPosition = localPos + offsets[0];
                        finalPosition = ClampPositionToScreenBounds(targetPosition, panelSize, canvasRect, safeMargin);
                    }
                }

                // 최종 위치 설정
                buttonRect.anchoredPosition = finalPosition;

                // Debug.Log($"[GameBoard] ActionButtonPanel 위치 설정 완료 - 최종: {finalPosition}");
            }
        }

        /// <summary>
        /// 주어진 위치가 화면 경계 안에 있는지 확인
        /// </summary>
        private bool IsPositionWithinScreenBounds(Vector2 position, Vector2 panelSize, RectTransform canvasRect, float safeMargin)
        {
            Vector2 canvasSize = canvasRect.sizeDelta;
            Vector2 panelHalfSize = panelSize * 0.5f;

            float minX = -canvasSize.x * 0.5f + panelHalfSize.x + safeMargin;
            float maxX = canvasSize.x * 0.5f - panelHalfSize.x - safeMargin;
            float minY = -canvasSize.y * 0.5f + panelHalfSize.y + safeMargin;
            float maxY = canvasSize.y * 0.5f - panelHalfSize.y - safeMargin;

            return position.x >= minX && position.x <= maxX && position.y >= minY && position.y <= maxY;
        }

        /// <summary>
        /// 위치를 화면 경계 안으로 강제 클램핑
        /// </summary>
        private Vector2 ClampPositionToScreenBounds(Vector2 position, Vector2 panelSize, RectTransform canvasRect, float safeMargin)
        {
            Vector2 canvasSize = canvasRect.sizeDelta;
            Vector2 panelHalfSize = panelSize * 0.5f;

            float minX = -canvasSize.x * 0.5f + panelHalfSize.x + safeMargin;
            float maxX = canvasSize.x * 0.5f - panelHalfSize.x - safeMargin;
            float minY = -canvasSize.y * 0.5f + panelHalfSize.y + safeMargin;
            float maxY = canvasSize.y * 0.5f - panelHalfSize.y - safeMargin;

            return new Vector2(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minY, maxY)
            );
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
