using UnityEngine;

namespace BlokusUnity.Game
{
    /// <summary>
    /// 개별 보드 셀 컴포넌트
    /// 터치 입력 처리 및 시각적 피드백 담당
    /// </summary>
    public class BoardCell : MonoBehaviour
    {
        private int row;
        private int col;
        private GameBoard parentBoard;
        private bool isInitialized = false;
        
        // 터치 상태
        private bool isPressed = false;
        private Camera mainCamera;
        
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
            parentBoard = board;
            isInitialized = true;
            
            // 메인 카메라 캐싱
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindObjectOfType<Camera>();
            }
        }
        
        // ========================================
        // 마우스/터치 이벤트 (PC 테스트용)
        // ========================================
        
        void OnMouseDown()
        {
            if (!isInitialized) return;
            
            isPressed = true;
            HandleClick();
        }
        
        void OnMouseUp()
        {
            isPressed = false;
        }
        
        void OnMouseEnter()
        {
            if (!isInitialized) return;
            
            HandleHover();
        }
        
        void OnMouseExit()
        {
            // 호버 종료 처리 (필요시)
        }
        
        // ========================================
        // 모바일 터치 처리
        // ========================================
        
        void Update()
        {
            if (!isInitialized) return;
            
            HandleTouch();
        }
        
        /// <summary>
        /// 모바일 터치 입력 처리
        /// </summary>
        private void HandleTouch()
        {
            // 마우스 클릭 (에디터 테스트용)
            if (Input.GetMouseButtonDown(0))
            {
                CheckTouchAt(Input.mousePosition);
            }
            
            // 터치 입력 처리
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                
                if (touch.phase == TouchPhase.Began)
                {
                    CheckTouchAt(touch.position);
                }
                else if (touch.phase == TouchPhase.Moved)
                {
                    CheckHoverAt(touch.position);
                }
            }
        }
        
        /// <summary>
        /// 지정된 스크린 좌표에서 터치 확인
        /// </summary>
        private void CheckTouchAt(Vector2 screenPos)
        {
            if (mainCamera == null) return;
            
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
            worldPos.z = 0; // 2D 보정
            
            // 이 셀이 터치된 위치인지 확인
            if (IsTouchingThis(worldPos))
            {
                HandleClick();
            }
        }
        
        /// <summary>
        /// 지정된 스크린 좌표에서 호버 확인
        /// </summary>
        private void CheckHoverAt(Vector2 screenPos)
        {
            if (mainCamera == null) return;
            
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
            worldPos.z = 0; // 2D 보정
            
            // 이 셀이 호버된 위치인지 확인
            if (IsTouchingThis(worldPos))
            {
                HandleHover();
            }
        }
        
        /// <summary>
        /// 월드 좌표가 이 셀을 터치하는지 확인
        /// </summary>
        private bool IsTouchingThis(Vector3 worldPos)
        {
            Collider2D collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                return collider.OverlapPoint(worldPos);
            }
            
            // 콜라이더가 없으면 거리 기반 체크
            float distance = Vector3.Distance(transform.position, worldPos);
            return distance < 0.5f; // 셀 크기의 절반 정도
        }
        
        // ========================================
        // 이벤트 처리
        // ========================================
        
        /// <summary>
        /// 클릭 이벤트 처리
        /// </summary>
        private void HandleClick()
        {
            if (parentBoard != null)
            {
                parentBoard.OnCellClickedInternal(row, col);
            }
            
            // 시각적 피드백 (선택적)
            StartCoroutine(ShowClickFeedback());
        }
        
        /// <summary>
        /// 호버 이벤트 처리
        /// </summary>
        private void HandleHover()
        {
            if (parentBoard != null)
            {
                parentBoard.OnCellHoverInternal(row, col);
            }
        }
        
        /// <summary>
        /// 클릭 피드백 애니메이션
        /// </summary>
        private System.Collections.IEnumerator ShowClickFeedback()
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer == null) yield break;
            
            Color originalColor = renderer.color;
            
            // 밝게 깜빡임
            renderer.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            
            renderer.color = originalColor;
        }
        
        // ========================================
        // 공개 메서드
        // ========================================
        
        /// <summary>
        /// 셀 좌표 가져오기
        /// </summary>
        public (int row, int col) GetCoordinates()
        {
            return (row, col);
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