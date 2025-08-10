using UnityEngine;
using BlokusUnity.Common;

namespace BlokusUnity.Game
{
    /// <summary>
    /// 모바일 터치 입력 매니저
    /// 블록 조작을 위한 제스처 인식 및 처리
    /// </summary>
    public class TouchInputManager : MonoBehaviour
    {
        [Header("터치 설정")]
        [SerializeField] private float swipeThreshold = 50f;
        [SerializeField] private float tapTimeout = 0.3f;
        [SerializeField] private float longPressTime = 0.8f;
        
        // 터치 상태 추적
        private Vector2 touchStartPos;
        private float touchStartTime;
        private bool isDragging = false;
        private bool isProcessingGesture = false;
        
        // 컴포넌트 참조
        private SingleGameManager gameManager;
        private BlockPalette blockPalette;
        private Camera mainCamera;
        
        // 이벤트
        public System.Action OnRotateClockwise;
        public System.Action OnRotateCounterClockwise;
        public System.Action OnFlipHorizontal;
        public System.Action OnFlipVertical;
        public System.Action OnUndoMove;
        public System.Action OnShowBlockMenu;
        
        // ========================================
        // Unity 생명주기
        // ========================================
        
        void Awake()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                mainCamera = FindObjectOfType<Camera>();
        }
        
        void Start()
        {
            gameManager = SingleGameManager.Instance;
            blockPalette = FindObjectOfType<BlockPalette>();
            
            SetupEvents();
        }
        
        void Update()
        {
            HandleTouchInput();
        }
        
        // ========================================
        // 초기화
        // ========================================
        
        /// <summary>
        /// 이벤트 연결
        /// </summary>
        private void SetupEvents()
        {
            // 블록 조작 이벤트 연결
            OnRotateClockwise += RotateBlockClockwise;
            OnRotateCounterClockwise += RotateBlockCounterClockwise;
            OnFlipHorizontal += FlipBlockHorizontal;
            OnFlipVertical += FlipBlockVertical;
            OnUndoMove += UndoLastMove;
        }
        
        // ========================================
        // 터치 입력 처리
        // ========================================
        
        /// <summary>
        /// 터치 입력 메인 처리 로직
        /// </summary>
        private void HandleTouchInput()
        {
            // PC 테스트용 마우스 입력
            if (Application.isEditor)
            {
                HandleMouseInput();
                return;
            }
            
            // 모바일 터치 입력
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                HandleTouch(touch);
            }
            
            // 멀티터치 제스처
            if (Input.touchCount == 2)
            {
                HandleTwoFingerGestures();
            }
        }
        
        /// <summary>
        /// 에디터 테스트용 마우스 입력 처리
        /// </summary>
        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                touchStartPos = Input.mousePosition;
                touchStartTime = Time.time;
                isDragging = false;
                isProcessingGesture = false;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (!isDragging && !isProcessingGesture)
                {
                    float touchDuration = Time.time - touchStartTime;
                    
                    if (touchDuration > longPressTime)
                    {
                        HandleLongPress(Input.mousePosition);
                    }
                    else
                    {
                        HandleTap(Input.mousePosition);
                    }
                }
                
                isDragging = false;
                isProcessingGesture = false;
            }
            else if (Input.GetMouseButton(0))
            {
                Vector2 currentPos = Input.mousePosition;
                float distance = Vector2.Distance(touchStartPos, currentPos);
                
                if (distance > swipeThreshold && !isProcessingGesture)
                {
                    HandleSwipe(touchStartPos, currentPos);
                    isProcessingGesture = true;
                }
                
                if (distance > 10f) // 작은 움직임도 드래그로 간주
                {
                    isDragging = true;
                }
            }
            
            // 키보드 단축키 (PC 전용)
            HandleKeyboardInput();
        }
        
        /// <summary>
        /// 단일 터치 처리
        /// </summary>
        private void HandleTouch(Touch touch)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    touchStartPos = touch.position;
                    touchStartTime = Time.time;
                    isDragging = false;
                    isProcessingGesture = false;
                    break;
                    
                case TouchPhase.Moved:
                    float distance = Vector2.Distance(touchStartPos, touch.position);
                    
                    if (distance > swipeThreshold && !isProcessingGesture)
                    {
                        HandleSwipe(touchStartPos, touch.position);
                        isProcessingGesture = true;
                    }
                    
                    if (distance > 10f)
                    {
                        isDragging = true;
                    }
                    break;
                    
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (!isDragging && !isProcessingGesture)
                    {
                        float touchDuration = Time.time - touchStartTime;
                        
                        if (touchDuration > longPressTime)
                        {
                            HandleLongPress(touch.position);
                        }
                        else
                        {
                            HandleTap(touch.position);
                        }
                    }
                    
                    isDragging = false;
                    isProcessingGesture = false;
                    break;
            }
        }
        
        /// <summary>
        /// 두 손가락 제스처 처리
        /// </summary>
        private void HandleTwoFingerGestures()
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);
            
            // 두 손가락 탭 (블록 뒤집기)
            if (touch1.phase == TouchPhase.Ended && touch2.phase == TouchPhase.Ended)
            {
                // 두 터치가 거의 동시에 끝났는지 확인
                Vector2 distance = touch1.position - touch2.position;
                if (distance.magnitude < 100f) // 두 터치가 가까운 거리에서
                {
                    OnFlipHorizontal?.Invoke();
                    Debug.Log("두 손가락 탭: 수평 뒤집기");
                }
            }
        }
        
        // ========================================
        // 제스처 인식
        // ========================================
        
        /// <summary>
        /// 탭 제스처 처리
        /// </summary>
        private void HandleTap(Vector2 screenPos)
        {
            // 일반적인 탭은 보드 클릭으로 전달
            Debug.Log($"탭 감지: {screenPos}");
        }
        
        /// <summary>
        /// 롱 프레스 제스처 처리
        /// </summary>
        private void HandleLongPress(Vector2 screenPos)
        {
            OnShowBlockMenu?.Invoke();
            Debug.Log("롱 프레스: 블록 메뉴 표시");
        }
        
        /// <summary>
        /// 스와이프 제스처 처리
        /// </summary>
        private void HandleSwipe(Vector2 startPos, Vector2 endPos)
        {
            Vector2 swipeVector = endPos - startPos;
            swipeVector.Normalize();
            
            // 수평 vs 수직 판단
            if (Mathf.Abs(swipeVector.x) > Mathf.Abs(swipeVector.y))
            {
                // 수평 스와이프
                if (swipeVector.x > 0)
                {
                    OnRotateClockwise?.Invoke();
                    Debug.Log("오른쪽 스와이프: 시계방향 회전");
                }
                else
                {
                    OnRotateCounterClockwise?.Invoke();
                    Debug.Log("왼쪽 스와이프: 반시계방향 회전");
                }
            }
            else
            {
                // 수직 스와이프
                if (swipeVector.y > 0)
                {
                    OnFlipVertical?.Invoke();
                    Debug.Log("위쪽 스와이프: 수직 뒤집기");
                }
                else
                {
                    OnUndoMove?.Invoke();
                    Debug.Log("아래쪽 스와이프: 실행취소");
                }
            }
        }
        
        /// <summary>
        /// 키보드 단축키 처리 (PC 테스트용)
        /// </summary>
        private void HandleKeyboardInput()
        {
            if (Input.GetKeyDown(KeyCode.R))
                OnRotateClockwise?.Invoke();
            
            if (Input.GetKeyDown(KeyCode.E))
                OnRotateCounterClockwise?.Invoke();
            
            if (Input.GetKeyDown(KeyCode.F))
                OnFlipHorizontal?.Invoke();
            
            if (Input.GetKeyDown(KeyCode.G))
                OnFlipVertical?.Invoke();
            
            if (Input.GetKeyDown(KeyCode.Z))
                OnUndoMove?.Invoke();
        }
        
        // ========================================
        // 블록 조작 메서드들
        // ========================================
        
        /// <summary>
        /// 블록 시계방향 회전
        /// </summary>
        private void RotateBlockClockwise()
        {
            if (blockPalette != null)
            {
                blockPalette.RotateSelectedBlock(true);
            }
        }
        
        /// <summary>
        /// 블록 반시계방향 회전
        /// </summary>
        private void RotateBlockCounterClockwise()
        {
            if (blockPalette != null)
            {
                blockPalette.RotateSelectedBlock(false);
            }
        }
        
        /// <summary>
        /// 블록 수평 뒤집기
        /// </summary>
        private void FlipBlockHorizontal()
        {
            if (blockPalette != null)
            {
                blockPalette.FlipSelectedBlock(true);
            }
        }
        
        /// <summary>
        /// 블록 수직 뒤집기
        /// </summary>
        private void FlipBlockVertical()
        {
            if (blockPalette != null)
            {
                blockPalette.FlipSelectedBlock(false);
            }
        }
        
        /// <summary>
        /// 마지막 수 되돌리기
        /// </summary>
        private void UndoLastMove()
        {
            if (gameManager != null)
            {
                gameManager.OnUndoMove();
            }
        }
        
        // ========================================
        // 공개 메서드
        // ========================================
        
        /// <summary>
        /// 터치 입력 활성화/비활성화
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            this.enabled = enabled;
        }
        
        /// <summary>
        /// 제스처 설정 업데이트
        /// </summary>
        public void UpdateGestureSettings(float swipeThresh, float tapTime, float longPressTime)
        {
            swipeThreshold = swipeThresh;
            tapTimeout = tapTime;
            this.longPressTime = longPressTime;
        }
        
        /// <summary>
        /// 현재 터치 상태 확인
        /// </summary>
        public bool IsTouching()
        {
            return Input.touchCount > 0 || Input.GetMouseButton(0);
        }
        
        /// <summary>
        /// 제스처 처리 중인지 확인
        /// </summary>
        public bool IsProcessingGesture()
        {
            return isProcessingGesture || isDragging;
        }
    }
}