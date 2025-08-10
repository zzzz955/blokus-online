using System.Collections.Generic;
using UnityEngine;
using BlokusUnity.Common;

namespace BlokusUnity.Game
{
    /// <summary>
    /// 2D 게임보드 시각화 및 상호작용 시스템
    /// Unity UI와 터치 입력을 통한 블록 배치 처리
    /// </summary>
    public class GameBoard : MonoBehaviour
    {
        [Header("보드 설정")]
        [SerializeField] private int boardSize = 20;
        [SerializeField] private float cellSize = 30f;
        [SerializeField] private Color gridLineColor = Color.gray;
        [SerializeField] private float gridLineWidth = 1f;
        
        [Header("셀 프리팹")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private Transform cellParent;
        
        [Header("블록 색상")]
        [SerializeField] private Color emptyColor = Color.white;
        [SerializeField] private Color previewColor = Color.yellow;
        [SerializeField] private Color invalidColor = Color.red;
        [SerializeField] private Color blueColor = Color.blue;
        [SerializeField] private Color yellowColor = Color.yellow;
        [SerializeField] private Color redColor = Color.red;
        [SerializeField] private Color greenColor = Color.green;
        
        // 내부 데이터
        private GameLogic gameLogic;
        private GameObject[,] cellObjects;
        private SpriteRenderer[,] cellRenderers;
        private Block previewBlock;
        private Position previewPosition = new Position(-1, -1);
        
        // 이벤트
        public System.Action<Position> OnCellClicked;
        public System.Action<Position> OnCellHover;
        public System.Action<Block, Position> OnBlockPlaced;
        
        // ========================================
        // Unity 생명주기
        // ========================================
        
        void Awake()
        {
            gameLogic = new GameLogic();
            InitializeBoard();
        }
        
        // ========================================
        // 보드 초기화
        // ========================================
        
        /// <summary>
        /// 게임보드 시각적 초기화
        /// </summary>
        private void InitializeBoard()
        {
            cellObjects = new GameObject[boardSize, boardSize];
            cellRenderers = new SpriteRenderer[boardSize, boardSize];
            
            // 보드 중앙 정렬을 위한 오프셋
            float startX = -(boardSize - 1) * cellSize * 0.5f;
            float startY = -(boardSize - 1) * cellSize * 0.5f;
            
            for (int row = 0; row < boardSize; row++)
            {
                for (int col = 0; col < boardSize; col++)
                {
                    // 셀 오브젝트 생성
                    Vector3 cellPosition = new Vector3(
                        startX + col * cellSize,
                        startY + row * cellSize,
                        0
                    );
                    
                    GameObject cell = CreateCell(cellPosition, row, col);
                    cellObjects[row, col] = cell;
                    cellRenderers[row, col] = cell.GetComponent<SpriteRenderer>();
                }
            }
            
            Debug.Log($"게임보드 초기화 완료: {boardSize}x{boardSize}");
        }
        
        /// <summary>
        /// 개별 셀 생성
        /// </summary>
        private GameObject CreateCell(Vector3 position, int row, int col)
        {
            GameObject cell = cellPrefab != null ? 
                Instantiate(cellPrefab, cellParent) : 
                CreateDefaultCell();
                
            cell.transform.position = position;
            cell.name = $"Cell_{row}_{col}";
            
            // 터치 이벤트 처리를 위한 컴포넌트
            var cellController = cell.GetComponent<BoardCell>();
            if (cellController == null)
            {
                cellController = cell.AddComponent<BoardCell>();
            }
            
            cellController.Initialize(row, col, this);
            
            return cell;
        }
        
        /// <summary>
        /// 기본 셀 생성 (프리팹이 없을 경우)
        /// </summary>
        private GameObject CreateDefaultCell()
        {
            GameObject cell = new GameObject();
            
            // 스프라이트 렌더러 추가
            var spriteRenderer = cell.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateCellSprite();
            spriteRenderer.color = emptyColor;
            
            // 콜라이더 추가 (터치 감지용)
            var collider = cell.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one * cellSize;
            
            return cell;
        }
        
        /// <summary>
        /// 셀용 기본 스프라이트 생성
        /// </summary>
        private Sprite CreateCellSprite()
        {
            // 1x1 하얀 텍스처 생성
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, cellSize);
        }
        
        // ========================================
        // 게임 로직 연동
        // ========================================
        
        /// <summary>
        /// 게임 로직 설정
        /// </summary>
        public void SetGameLogic(GameLogic logic)
        {
            gameLogic = logic;
            RefreshBoard();
        }
        
        /// <summary>
        /// 보드 상태 새로고침
        /// </summary>
        public void RefreshBoard()
        {
            for (int row = 0; row < boardSize; row++)
            {
                for (int col = 0; col < boardSize; col++)
                {
                    Position pos = new Position(row, col);
                    PlayerColor cellColor = gameLogic.GetCellColor(pos);
                    UpdateCellVisual(row, col, cellColor);
                }
            }
        }
        
        /// <summary>
        /// 셀 시각 업데이트
        /// </summary>
        private void UpdateCellVisual(int row, int col, PlayerColor color)
        {
            if (cellRenderers[row, col] != null)
            {
                cellRenderers[row, col].color = GetPlayerColor(color);
            }
        }
        
        /// <summary>
        /// 플레이어 색상 매핑
        /// </summary>
        private Color GetPlayerColor(PlayerColor playerColor)
        {
            return playerColor switch
            {
                PlayerColor.Blue => blueColor,
                PlayerColor.Yellow => yellowColor,
                PlayerColor.Red => redColor,
                PlayerColor.Green => greenColor,
                _ => emptyColor
            };
        }
        
        // ========================================
        // 블록 배치 시스템
        // ========================================
        
        /// <summary>
        /// 블록 배치 시도
        /// </summary>
        public bool TryPlaceBlock(Block block, Position position)
        {
            if (gameLogic.CanPlaceBlock(block, position))
            {
                var placement = new BlockPlacement(
                    block.Type,
                    position,
                    block.CurrentRotation,
                    block.CurrentFlipState,
                    block.Player
                );
                
                gameLogic.PlaceBlock(placement);
                RefreshBoard();
                
                OnBlockPlaced?.Invoke(block, position);
                Debug.Log($"블록 배치 성공: {block.Type} at ({position.row}, {position.col})");
                return true;
            }
            
            Debug.LogWarning($"블록 배치 실패: {block.Type} at ({position.row}, {position.col})");
            return false;
        }
        
        /// <summary>
        /// 블록 배치 미리보기 설정
        /// </summary>
        public void SetPreview(Block block, Position position)
        {
            ClearPreview();
            
            if (block != null && ValidationUtility.IsValidPosition(position))
            {
                previewBlock = block;
                previewPosition = position;
                ShowPreview();
            }
        }
        
        /// <summary>
        /// 미리보기 표시
        /// </summary>
        private void ShowPreview()
        {
            if (previewBlock == null) return;
            
            var positions = previewBlock.GetAbsolutePositions(previewPosition);
            bool canPlace = gameLogic.CanPlaceBlock(previewBlock, previewPosition);
            Color previewCol = canPlace ? previewColor : invalidColor;
            
            foreach (var pos in positions)
            {
                if (ValidationUtility.IsValidPosition(pos) && 
                    pos.row < boardSize && pos.col < boardSize)
                {
                    var renderer = cellRenderers[pos.row, pos.col];
                    if (renderer != null && gameLogic.GetCellColor(pos) == PlayerColor.None)
                    {
                        renderer.color = previewCol;
                    }
                }
            }
        }
        
        /// <summary>
        /// 미리보기 지우기
        /// </summary>
        public void ClearPreview()
        {
            if (previewBlock != null && ValidationUtility.IsValidPosition(previewPosition))
            {
                var positions = previewBlock.GetAbsolutePositions(previewPosition);
                
                foreach (var pos in positions)
                {
                    if (ValidationUtility.IsValidPosition(pos) && 
                        pos.row < boardSize && pos.col < boardSize)
                    {
                        var renderer = cellRenderers[pos.row, pos.col];
                        if (renderer != null && gameLogic.GetCellColor(pos) == PlayerColor.None)
                        {
                            renderer.color = emptyColor;
                        }
                    }
                }
            }
            
            previewBlock = null;
            previewPosition = new Position(-1, -1);
        }
        
        // ========================================
        // 터치 이벤트 처리
        // ========================================
        
        /// <summary>
        /// 셀 클릭 이벤트 (BoardCell에서 호출)
        /// </summary>
        public void OnCellClickedInternal(int row, int col)
        {
            Position clickedPos = new Position(row, col);
            OnCellClicked?.Invoke(clickedPos);
            
            Debug.Log($"셀 클릭: ({row}, {col})");
        }
        
        /// <summary>
        /// 셀 호버 이벤트 (BoardCell에서 호출)
        /// </summary>
        public void OnCellHoverInternal(int row, int col)
        {
            Position hoverPos = new Position(row, col);
            OnCellHover?.Invoke(hoverPos);
        }
        
        // ========================================
        // 유틸리티
        // ========================================
        
        /// <summary>
        /// 월드 좌표를 보드 좌표로 변환
        /// </summary>
        public Position WorldToBoard(Vector3 worldPos)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            
            float startX = -(boardSize - 1) * cellSize * 0.5f;
            float startY = -(boardSize - 1) * cellSize * 0.5f;
            
            int col = Mathf.RoundToInt((localPos.x - startX) / cellSize);
            int row = Mathf.RoundToInt((localPos.y - startY) / cellSize);
            
            return new Position(row, col);
        }
        
        /// <summary>
        /// 보드 좌표를 월드 좌표로 변환
        /// </summary>
        public Vector3 BoardToWorld(Position boardPos)
        {
            float startX = -(boardSize - 1) * cellSize * 0.5f;
            float startY = -(boardSize - 1) * cellSize * 0.5f;
            
            Vector3 localPos = new Vector3(
                startX + boardPos.col * cellSize,
                startY + boardPos.row * cellSize,
                0
            );
            
            return transform.TransformPoint(localPos);
        }
        
        /// <summary>
        /// 현재 게임 상태 가져오기
        /// </summary>
        public GameLogic GetGameLogic()
        {
            return gameLogic;
        }
        
        /// <summary>
        /// 보드 초기화
        /// </summary>
        public void ClearBoard()
        {
            gameLogic.ClearBoard();
            RefreshBoard();
        }
        
        /// <summary>
        /// 보드 크기 가져오기
        /// </summary>
        public int GetBoardSize()
        {
            return boardSize;
        }
    }
}