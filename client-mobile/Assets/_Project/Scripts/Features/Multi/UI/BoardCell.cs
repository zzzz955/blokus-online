using UnityEngine;
using UnityEngine.EventSystems;
using Shared.Models;
using Shared.UI;

namespace Features.Multi.UI
{
    /// <summary>
    /// 멀티플레이어 게임보드 셀 터치/드래그 처리 컴포넌트
    /// Single BoardCell 기반 + 멀티플레이어 전용 기능 (턴 제어, 서버 동기화)
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
            uiCellSize = gameBoard.CellSize;
            boardRect = gameBoard.CellParent;
        }

        // ========================================
        // 터치 이벤트 처리 (Multi 전용: 턴 기반 제어 포함)
        // ========================================

        /// <summary>
        /// 터치 시작
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (gameBoard != null && gameBoard.CanInteract) // Multi: 턴 제어
            {
                gameBoard.OnCellHoverInternal(row, col);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isInitialized || gameBoard == null) return;
            dragStartPosition = eventData.position;
            isDragging = false;
        }

        /// <summary>
        /// 드래그 시작
        /// </summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = true;
        }

        /// <summary>
        /// 드래그 중
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || gameBoard == null) return;

            // Multi 전용: 상호작용 가능 여부 체크 (턴 기반)
            if (!gameBoard.CanInteract) return;

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

            // Multi 전용: 상호작용 가능 여부 체크
            if (!gameBoard.CanInteract) return;

            // 드래그 종료 위치에서 셀 찾기
            Position endCell = GetCellFromScreenPosition(eventData.position);

            if (ValidationUtility.IsValidPosition(endCell))
            {
                // 유효한 셀에서 드래그 종료시 해당 위치에 미리보기 유지
                gameBoard.OnCellClicked?.Invoke(endCell);
            }
            else
            {
                // 보드 바깥에서 드래그 종료시 미리보기 취소
                gameBoard.ClearTouchPreview();
            }
        }

        /// <summary>
        /// 터치 종료
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (isDragging) return; // 드래그 중이면 처리하지 않음

            // Multi 전용: 상호작용 가능 여부 체크
            if (!gameBoard.CanInteract) return;

            // 단순 탭인 경우 처리
            float dragDistance = Vector2.Distance(dragStartPosition, eventData.position);
            if (dragDistance < 10f) // 10픽셀 이하 움직임은 탭으로 간주
            {
                Position cellPosition = new Position(row, col);
                gameBoard.OnCellClicked?.Invoke(cellPosition);
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
        // 유틸리티 메서드 (Single 기반)
        // ========================================

        /// <summary>
        /// 스크린 좌표에서 보드 셀 위치 찾기
        /// </summary>
        private Position GetCellFromScreenPosition(Vector2 screenPos)
        {
            if (gameBoard == null) return new Position(-1, -1);
            // GameBoard의 ScreenToBoard 메서드 사용 (Single 방식)
            return gameBoard.ScreenToBoard(screenPos);
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