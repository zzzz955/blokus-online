using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Features.Multi.Models;

namespace Features.Multi.UI
{
    /// <summary>
    /// 멀티플레이어 게임 보드 (Stub 구현)
    /// Single 버전과 충돌하지 않도록 별도 구현
    /// </summary>
    public class GameBoard : MonoBehaviour
    {
        [Header("보드 설정")]
        [SerializeField] private int boardSize = 20;
        [SerializeField] private GridLayoutGroup gridLayout;
        [SerializeField] private GameObject cellPrefab;
        
        [Header("색상 설정")]
        [SerializeField] private Color[] playerColors = {
            Color.red,    // Player 0
            Color.blue,   // Player 1  
            Color.yellow, // Player 2
            Color.green   // Player 3
        };
        
        // 보드 데이터
        private GameObject[,] boardCells;
        private int[,] boardState; // 0=빈칸, 1-4=플레이어 ID
        
        // 이벤트
        public event System.Action<Vector2Int> OnCellClicked;
        public event System.Action<BlockPlacement> OnBlockPlaced;
        
        private void Start()
        {
            InitializeBoard();
        }
        
        /// <summary>
        /// 보드 초기화
        /// </summary>
        private void InitializeBoard()
        {
            Debug.Log("[GameBoard] 보드 초기화 (Stub)");
            
            boardCells = new GameObject[boardSize, boardSize];
            boardState = new int[boardSize, boardSize];
            
            // 그리드 레이아웃 설정
            if (gridLayout != null)
            {
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = boardSize;
            }
            
            // 셀 생성 (Stub)
            for (int y = 0; y < boardSize; y++)
            {
                for (int x = 0; x < boardSize; x++)
                {
                    CreateCell(x, y);
                }
            }
        }
        
        /// <summary>
        /// 셀 생성
        /// </summary>
        private void CreateCell(int x, int y)
        {
            if (cellPrefab == null) return;
            
            GameObject cell = Instantiate(cellPrefab, gridLayout.transform);
            cell.name = $"Cell_{x}_{y}";
            
            // 클릭 이벤트 연결
            Button cellButton = cell.GetComponent<Button>();
            if (cellButton != null)
            {
                Vector2Int position = new Vector2Int(x, y);
                cellButton.onClick.AddListener(() => OnCellClickHandler(position));
            }
            
            boardCells[x, y] = cell;
            boardState[x, y] = 0; // 빈칸으로 초기화
        }
        
        /// <summary>
        /// 셀 클릭 핸들러
        /// </summary>
        private void OnCellClickHandler(Vector2Int position)
        {
            Debug.Log($"[GameBoard] 셀 클릭: ({position.x}, {position.y})");
            OnCellClicked?.Invoke(position);
        }
        
        /// <summary>
        /// 블록 배치 (Stub)
        /// </summary>
        public bool PlaceBlock(Vector2Int position, int playerId, List<Vector2Int> blockShape)
        {
            Debug.Log($"[GameBoard] 블록 배치 (Stub): Player {playerId} at ({position.x}, {position.y})");
            
            // 실제 구현에서는 블록 배치 로직 필요
            // 현재는 단순히 단일 셀만 칠함
            SetCell(position.x, position.y, playerId);
            
            return true; // Stub에서는 항상 성공
        }
        
        /// <summary>
        /// 셀 상태 설정
        /// </summary>
        public void SetCell(int x, int y, int playerId)
        {
            if (x < 0 || x >= boardSize || y < 0 || y >= boardSize)
                return;
                
            boardState[x, y] = playerId;
            
            // 셀 색상 업데이트
            if (boardCells[x, y] != null)
            {
                Image cellImage = boardCells[x, y].GetComponent<Image>();
                if (cellImage != null)
                {
                    if (playerId == 0)
                    {
                        cellImage.color = Color.white; // 빈칸
                    }
                    else if (playerId > 0 && playerId <= playerColors.Length)
                    {
                        cellImage.color = playerColors[playerId - 1];
                    }
                }
            }
        }
        
        /// <summary>
        /// 셀 상태 가져오기
        /// </summary>
        public int GetCell(int x, int y)
        {
            if (x < 0 || x >= boardSize || y < 0 || y >= boardSize)
                return -1;
                
            return boardState[x, y];
        }
        
        /// <summary>
        /// 보드 초기화
        /// </summary>
        public void ResetBoard()
        {
            Debug.Log("[GameBoard] 보드 리셋");
            
            for (int y = 0; y < boardSize; y++)
            {
                for (int x = 0; x < boardSize; x++)
                {
                    SetCell(x, y, 0); // 모든 셀을 빈칸으로
                }
            }
        }
        
        /// <summary>
        /// 보드 크기 가져오기
        /// </summary>
        public int GetBoardSize()
        {
            return boardSize;
        }
        
        /// <summary>
        /// 보드 상호작용 설정
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            Debug.Log($"[GameBoard] 상호작용 설정: {interactable}");
            
            for (int y = 0; y < boardSize; y++)
            {
                for (int x = 0; x < boardSize; x++)
                {
                    if (boardCells[x, y] != null)
                    {
                        Button cellButton = boardCells[x, y].GetComponent<Button>();
                        if (cellButton != null)
                        {
                            cellButton.interactable = interactable;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 선택된 블록 설정 (Stub)
        /// </summary>
        public void SetSelectedBlock(BlockType blockType)
        {
            Debug.Log($"[GameBoard] 선택된 블록 설정 (Stub): {blockType}");
            // Stub: 실제 구현에서는 선택된 블록을 보드에 표시
        }
    }
}