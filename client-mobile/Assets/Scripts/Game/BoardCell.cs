using UnityEngine;
using UnityEngine.EventSystems;
using BlokusUnity.Common;

namespace BlokusUnity.Game
{
    /// <summary>
    /// 게임보드 셀 터치/드래그 처리 컴포넌트
    /// 모바일 터치 입력과 드래그 제스처 지원
    /// </summary>
    public class BoardCell : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        private int row;
        private int col;
        private GameBoard gameBoard;
        private bool isDragging = false;
        private Vector2 dragStartPosition;
        private bool isInitialized = false;
        private RectTransform boardRect;
        private float uiCellSize;
        private int uiBoardSize;

        // ========================================
        // 초기화
        // ========================================

        /// <summary>
        /// 셀 초기화
        /// </summary>
        public void Initialize(int cellRow, int cellCol, GameBoard board)
        {
            row = cellRow;
            col = cellCol;
            gameBoard = board;
            isInitialized = true;

            // GameBoard에서 설정값 가져오기
            uiBoardSize = gameBoard.GetBoardSize();
            uiCellSize = gameBoard.CellSize;              // GameBoard에 public getter 추가 필요
            boardRect = gameBoard.CellParent;            // GameBoard에 public RectTransform CellParent { get; } 추가
        }

        // ========================================
        // 터치 이벤트 처리
        // ========================================

        /// <summary>
        /// 터치 시작
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (gameBoard != null) gameBoard.OnCellHoverInternal(row, col);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isInitialized || gameBoard == null) return;
            dragStartPosition = eventData.position;
            isDragging = false;
            // Debug.Log($"BoardCell 클릭: ({row}, {col})");
        }

        /// <summary>
        /// 드래그 시작
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = true;
            // Debug.Log($"드래그 시작: Cell ({row}, {col})");
        }

        /// <summary>
        /// 드래그 중
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || gameBoard == null) return;
            var pos = GetCellFromScreenPosition(eventData.position);
            if (ValidationUtility.IsValidPosition(pos))
                gameBoard.OnCellClickedInternal(pos.row, pos.col);
            else
                gameBoard.ClearTouchPreview();
        }

        /// <summary>
        /// 드래그 종료
        /// </summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;

            // 드래그 종료 위치에서 셀 찾기
            Position endCell = GetCellFromScreenPosition(eventData.position);

            if (ValidationUtility.IsValidPosition(endCell))
            {
                // 유효한 셀에서 드래그 종료시 해당 위치에 미리보기 유지
                gameBoard.OnCellClicked?.Invoke(endCell);
                // Debug.Log($"드래그 종료: Cell ({endCell.row}, {endCell.col})");
            }
            else
            {
                // 보드 바깥에서 드래그 종료시 미리보기 취소
                gameBoard.ClearTouchPreview();
                // Debug.Log("드래그 종료: 보드 바깥 (미리보기 취소)");
            }
        }

        /// <summary>
        /// 터치 종료
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (isDragging) return; // 드래그 중이면 처리하지 않음

            // 단순 탭인 경우 처리
            float dragDistance = Vector2.Distance(dragStartPosition, eventData.position);
            if (dragDistance < 10f) // 10픽셀 이하 움직임은 탭으로 간주
            {
                Position cellPosition = new Position(row, col);
                gameBoard.OnCellClicked?.Invoke(cellPosition);
                // Debug.Log($"BoardCell 탭 완료: ({row}, {col})");
            }
        }

        /// <summary>
        /// 포인터가 셀을 벗어남
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            // 드래그 중이 아닐 때만 호버 해제
            if (!isDragging && gameBoard != null)
            {
                // 호버 해제는 드래그가 아닌 경우에만
            }
        }

        // ========================================
        // 유틸리티 메서드
        // ========================================

        /// <summary>
        /// 스크린 좌표에서 보드 셀 위치 찾기
        /// </summary>
        private Position GetCellFromScreenPosition(Vector2 screenPos)
        {
            if (gameBoard == null) return new Position(-1, -1);
            // GameBoard에 ScreenToBoard(Vector2 screenPos) 공개 메서드 추가
            return gameBoard.ScreenToBoard(screenPos);
        }

        /// <summary>
        /// 로컬 좌표를 보드 좌표로 변환
        /// </summary>
        private Position ScreenToBoard(Vector2 localPosition)
        {
            if (gameBoard == null) return new Position(-1, -1);

            // GameBoard의 cellParent 기준으로 좌표 변환
            Transform cellParent = gameBoard.transform.Find("GridContainer");
            if (cellParent == null) return new Position(-1, -1);

            RectTransform parentRect = cellParent.GetComponent<RectTransform>();
            if (parentRect == null) return new Position(-1, -1);

            // 셀 크기로 나누어 행/열 계산
            float cellSize = 25f; // GameBoard의 cellSize와 동일해야 함
            int boardSize = 20; // GameBoard의 boardSize와 동일해야 함

            Vector2 relativePos = localPosition - (Vector2)parentRect.localPosition;

            int col = Mathf.FloorToInt((relativePos.x + boardSize * cellSize * 0.5f) / cellSize);
            int row = Mathf.FloorToInt((-relativePos.y + boardSize * cellSize * 0.5f) / cellSize);

            if (row >= 0 && row < boardSize && col >= 0 && col < boardSize)
            {
                return new Position(row, col);
            }

            return new Position(-1, -1);
        }

        // ========================================
        // 공개 메서드
        // ========================================

        /// <summary>
        /// 현재 셀의 위치 정보
        /// </summary>
        public Position GetPosition()
        {
            return new Position(row, col);
        }

        /// <summary>
        /// 셀이 초기화되었는지 확인
        /// </summary>
        public bool IsInitialized()
        {
            return isInitialized;
        }
    }
}