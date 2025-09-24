using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Shared.Models;

namespace Shared.UI
{
    /// <summary>
    /// 게임 보드 확대/축소 및 팬 기능을 제공하는 컴포넌트
    /// Unity UI 기반 GameBoard에 첨부하여 사용
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GameBoardZoomPan : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler, IInitializePotentialDragHandler, IScrollHandler, IPointerClickHandler
    {
        [Header("Zoom Settings")]
        [SerializeField] private float minZoom = 1.0f;
        [SerializeField] private float maxZoom = 2.0f;
        [SerializeField] private float zoomSpeed = 0.1f;
        [SerializeField] private float zoomSensitivity = 1.0f;

        [Header("Pan Settings")]
        [SerializeField] private float panSpeed = 2.0f;
        [SerializeField] private bool enablePanOnlyWhenZoomed = false;
        [SerializeField] private float dragThreshold = 10f;

        [Header("Delayed Pan Mode")]
        [SerializeField] private bool disablePanLimits = false; // 디버깅용: 팬 제한 비활성화

        [Header("Mobile Settings")]
        [SerializeField] private float pinchSensitivity = 0.01f;
        [SerializeField] private float minPinchDistance = 50f;

        [Header("Target")]
        [SerializeField] private RectTransform zoomTarget;

        [Header("Intelligent Intention Detection")]
        [SerializeField] private float intentionPanDistanceThreshold = 15f; // 팬 의도로 감지할 최소 드래그 거리
        [SerializeField] private float intentionPanSpeedThreshold = 100f; // 팬 의도로 감지할 최소 드래그 속도
        [SerializeField] private float intentionAnalysisThreshold = 0.6f; // 팬 의도 확정 임계값

        // 사용자 의도 감지
        public enum UserIntention
        {
            Unknown,     // 아직 판단할 수 없음
            BlockPlace,  // 블록 배치 의도
            Pan          // 팬 이동 의도
        }

        // 내부 상태
        private float currentZoom = 1.0f;
        private Vector2 currentPan = Vector2.zero;
        private Vector2 originalAnchoredPosition;
        private Vector3 originalLocalScale;

        // 입력 상태
        private bool isDragging = false;
        private bool isActuallyDragging = false; // 임계값을 넘은 실제 드래그 상태
        private Vector2 lastPointerPosition;
        private Vector2 dragStartPosition;
        private bool isPinching = false;
        private float lastPinchDistance = 0f;

        // Time-based Event Detection (시간 기반 이벤트 감지)
        private Vector2 pointerDownPosition;
        private Vector2 dragStartPanPosition; // 드래그 시작 시점의 팬 위치
        private float clickStartTime; // 클릭 시작 시간
        private bool isDragInProgress = false;
        private const float CLICK_TO_PAN_THRESHOLD = 0.3f; // 0.3초 임계값
        private bool isPanModeActive = false; // 팬 모드 활성화 상태

        // 지능형 의도 감지 변수들
        private UserIntention currentIntention = UserIntention.Unknown;
        private Vector2 currentDragPosition;
        private float dragStartTime;
        private float totalDragDistance = 0f;
        private List<Vector2> dragPath = new List<Vector2>(); // 드래그 경로 추적

        // Legacy variables (for compatibility)
        private float pointerDownTime;
        private float panDragThreshold = 15f; // 15px (사용 안함)
        private float clickTimeThreshold = 0.3f; // 0.3초 (deprecated)
        private Vector2 pinchCenter = Vector2.zero;

        // GameBoard 연동 (셀 raycast 제어용)
        private Features.Single.Gameplay.GameBoard singleGameBoard;
        private Features.Multi.UI.GameBoard multiGameBoard;

        // 경계 계산용
        private RectTransform containerRect;
        private Vector2 originalSize;
        private Rect viewBounds;

        // 터치 입력
        private Touch[] lastTouches = new Touch[0];

        private void Awake()
        {
            // RectTransform 확인 및 추가
            containerRect = GetComponent<RectTransform>();
            if (containerRect == null)
            {
                // Transform을 RectTransform으로 변경할 수 없으므로, 부모에서 RectTransform 찾기
                containerRect = GetComponentInParent<RectTransform>();
                if (containerRect == null)
                {
                    Debug.LogError("[GameBoardZoomPan] RectTransform을 찾을 수 없습니다. GameBoard가 Canvas 하위에 있고 RectTransform을 가져야 합니다.");
                    enabled = false;
                    return;
                }
                else
                {
                    Debug.LogWarning("[GameBoardZoomPan] GameBoard에 RectTransform이 없어 부모의 RectTransform을 사용합니다.");
                }
            }

            // zoomTarget이 설정되지 않은 경우 자동으로 찾기
            if (zoomTarget == null)
            {
                // "GridContainer" 또는 "CellParent" 이름의 자식 찾기
                Transform gridContainer = transform.Find("GridContainer");
                if (gridContainer == null)
                {
                    gridContainer = transform.Find("CellParent");
                }

                if (gridContainer != null)
                {
                    zoomTarget = gridContainer.GetComponent<RectTransform>();
                }
                else
                {
                    Debug.LogWarning("[GameBoardZoomPan] zoomTarget을 찾을 수 없습니다. Inspector에서 수동으로 설정해주세요.");
                }
            }
        }

        private void Start()
        {
            // EventSystem 드래그 임계값 조정 (OnDrag 이벤트 활성화)
            if (EventSystem.current != null)
            {
                EventSystem.current.pixelDragThreshold = 1; // 1픽셀로 낮춰서 OnDrag 쉽게 호출
            }

            // GameBoard 이벤트 우선권 보장
            EnsureGameBoardEventPriority();

            // 강제로 이벤트 수신 활성화
            ForceEnableEventReception();

            InitializeZoomPan();
            ValidateEventSystemSetup();

            // 초기 상태 설정: 블록 선택 여부에 따라 적절한 이벤트 수신 주체 설정
            RestoreCellInteractionState();

            // 추가 raycastTarget 강제 재설정 (지연 실행)
            StartCoroutine(ForceRaycastTargetAfterDelay());

            // ActionButtonPanel 가시성 지속 모니터링 시작
            StartCoroutine(MonitorActionButtonPanelVisibility());
        }

        /// <summary>
        /// GameBoard가 적절한 이벤트 우선권을 갖도록 보장하면서 ActionButtonPanel 가시성 유지
        /// </summary>
        private void EnsureGameBoardEventPriority()
        {
            // ActionButtonPanel을 먼저 최상위로 이동시켜 가시성 보장
            EnsureActionButtonPanelVisibility();

            // GraphicRaycaster가 올바르게 설정되어 있는지 확인
            GraphicRaycaster raycaster = GetComponentInParent<GraphicRaycaster>();
            if (raycaster == null)
            {
                Debug.LogWarning("[GameBoardZoomPan] ⚠️ GraphicRaycaster가 없습니다. Canvas에 추가해야 합니다.");
            }

            Debug.Log("[GameBoardZoomPan] 🎯 GameBoard 이벤트 우선권 설정 완료 (ActionButtonPanel 가시성 보장)");
        }

        /// <summary>
        /// ActionButtonPanel이 GameBoard보다 앞에 렌더링되도록 보장
        /// </summary>
        private void EnsureActionButtonPanelVisibility()
        {
            try
            {
                if (singleGameBoard != null)
                {
                    // reflection으로 actionButtonPanel 접근
                    var actionButtonPanelField = singleGameBoard.GetType()
                        .GetField("actionButtonPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (actionButtonPanelField != null)
                    {
                        var actionButtonPanel = actionButtonPanelField.GetValue(singleGameBoard) as RectTransform;

                        if (actionButtonPanel != null && actionButtonPanel.gameObject.activeInHierarchy)
                        {
                            // ActionButtonPanel을 GameBoard보다 앞에 렌더링되도록 설정
                            actionButtonPanel.SetAsLastSibling();

                            // ActionButtonPanel의 Canvas Group 설정으로 항상 최상위 렌더링 보장
                            var canvasGroup = actionButtonPanel.GetComponent<CanvasGroup>();
                            if (canvasGroup == null)
                            {
                                canvasGroup = actionButtonPanel.gameObject.AddComponent<CanvasGroup>();
                            }
                            canvasGroup.blocksRaycasts = true;
                            canvasGroup.interactable = true;
                            canvasGroup.alpha = 1f;

                            Debug.Log("[GameBoardZoomPan] ✅ ActionButtonPanel 가시성 보장 완료");
                        }
                        else
                        {
                            Debug.LogWarning("[GameBoardZoomPan] ⚠️ ActionButtonPanel이 비활성화되어 있거나 null입니다");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[GameBoardZoomPan] ⚠️ actionButtonPanel 필드를 찾을 수 없습니다");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameBoardZoomPan] ❌ ActionButtonPanel 가시성 설정 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// ActionButtonPanel 가시성 보장 (로그 없는 버전 - 주기적 모니터링용)
        /// </summary>
        private void EnsureActionButtonPanelVisibilitySilent()
        {
            try
            {
                if (singleGameBoard != null)
                {
                    var actionButtonPanelField = singleGameBoard.GetType()
                        .GetField("actionButtonPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (actionButtonPanelField != null)
                    {
                        var actionButtonPanel = actionButtonPanelField.GetValue(singleGameBoard) as RectTransform;

                        if (actionButtonPanel != null && actionButtonPanel.gameObject.activeInHierarchy)
                        {
                            // ActionButtonPanel을 최상위에 유지
                            actionButtonPanel.SetAsLastSibling();

                            // CanvasGroup 설정 유지
                            var canvasGroup = actionButtonPanel.GetComponent<CanvasGroup>();
                            if (canvasGroup == null)
                            {
                                canvasGroup = actionButtonPanel.gameObject.AddComponent<CanvasGroup>();
                            }
                            canvasGroup.blocksRaycasts = true;
                            canvasGroup.interactable = true;
                            canvasGroup.alpha = 1f;
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                // 주기적 모니터링이므로 에러 로그 생략
            }
        }

        /// <summary>
        /// 강제로 이벤트 수신을 활성화
        /// </summary>
        private void ForceEnableEventReception()
        {
            Debug.Log("[GameBoardZoomPan] 🔧 강제 이벤트 수신 활성화 시작");

            // Image raycastTarget 강제 활성화
            var image = GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
                Debug.Log($"[GameBoardZoomPan] 🔧 Image raycastTarget 강제 활성화: {image.raycastTarget}");
            }
            else
            {
                Debug.LogWarning("[GameBoardZoomPan] ⚠️ Image 컴포넌트를 찾을 수 없습니다!");
            }

            // 자체 CanvasGroup 활성화
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
                Debug.Log($"[GameBoardZoomPan] 🔧 자체 CanvasGroup 강제 활성화");
            }

            // 모든 부모 CanvasGroup들 활성화
            Transform parent = transform.parent;
            while (parent != null)
            {
                var parentCanvasGroup = parent.GetComponent<CanvasGroup>();
                if (parentCanvasGroup != null)
                {
                    bool wasBlocked = !parentCanvasGroup.blocksRaycasts || !parentCanvasGroup.interactable;
                    parentCanvasGroup.blocksRaycasts = true;
                    parentCanvasGroup.interactable = true;

                    if (wasBlocked)
                    {
                        Debug.Log($"[GameBoardZoomPan] 🔧 부모 CanvasGroup 강제 활성화: {parent.name}");
                    }
                }
                parent = parent.parent;
            }

            // **핵심 수정: 기본 상태에서 셀 raycast 비활성화 (GameBoard가 이벤트를 받을 수 있도록)**
            SetCellRaycastEnabled(false);
            Debug.Log("[GameBoardZoomPan] 🔧 기본 상태에서 셀 raycast 비활성화 - GameBoard 이벤트 수신 활성화");

            Debug.Log("[GameBoardZoomPan] 🔧 강제 이벤트 수신 활성화 완료");
        }

        /// <summary>
        /// 지연 후 raycastTarget을 강제로 다시 설정 (Unity 내부 업데이트 대응)
        /// </summary>
        private System.Collections.IEnumerator ForceRaycastTargetAfterDelay()
        {
            yield return new WaitForSeconds(0.1f); // 100ms 대기

            var image = GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
                Debug.Log("[GameBoardZoomPan] 🔧 지연된 raycastTarget 강제 재설정 완료");
            }
        }

        /// <summary>
        /// ActionButtonPanel 가시성을 지속적으로 모니터링하고 보장
        /// </summary>
        private System.Collections.IEnumerator MonitorActionButtonPanelVisibility()
        {
            while (true)
            {
                yield return new WaitForSeconds(3.0f); // 3초마다 체크 (성능 최적화)

                // ActionButtonPanel 가시성 재보장 (로그 없이)
                EnsureActionButtonPanelVisibilitySilent();
            }
        }

        private void Update()
        {
            HandleMobileInput();

            // 디버깅용: Ctrl+클릭으로 실시간 Raycast 테스트
            if (Input.GetKeyDown(KeyCode.Mouse0) && Input.GetKey(KeyCode.LeftControl))
            {
                TestEventReception();
            }

            // 디버깅용: Shift+클릭으로 셀 raycast 상태 강제 토글
            if (Input.GetKeyDown(KeyCode.Mouse0) && Input.GetKey(KeyCode.LeftShift))
            {
                bool currentState = singleGameBoard != null; // 임시로 상태 확인
                SetCellRaycastEnabled(!currentState);
                Debug.Log($"[GameBoardZoomPan] 🔧 강제 셀 raycast 토글: {!currentState}");
                TestEventReception(); // 바로 테스트
            }

            // 실시간 드래그 상태 모니터링 제거 (로그 폭증 방지)

            // 디버깅용: Ctrl+클릭으로 간단 진단 (로그 폭증 방지)
            if (Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftControl))
            {
                Debug.Log($"[GameBoardZoomPan] 🔧 간단 진단: GameObject활성={gameObject.activeInHierarchy}, 컴포넌트활성={enabled}");

                var image = GetComponent<Image>();
                if (image != null)
                {
                    Debug.Log($"[GameBoardZoomPan] 🔧 Image: raycastTarget={image.raycastTarget}, alpha={image.color.a:F3}");
                }

                Debug.Log($"[GameBoardZoomPan] 🔧 상태: isDragging={isDragging}, isPanModeActive={isPanModeActive}");
                // 복잡한 raycast 테스트는 제거하여 로그 폭증 방지
            }
        }

        /// <summary>
        /// 확대/축소 및 팬 기능 초기화
        /// </summary>
        private void InitializeZoomPan()
        {
            if (zoomTarget == null)
            {
                Debug.LogError("[GameBoardZoomPan] zoomTarget이 설정되지 않았습니다!");
                enabled = false;
                return;
            }

            // 원본 상태 저장
            originalAnchoredPosition = zoomTarget.anchoredPosition;
            originalLocalScale = zoomTarget.localScale;

            // containerRect 크기 설정 - zoomTarget 크기를 기준으로 함
            if (containerRect.sizeDelta.x <= 100 || containerRect.sizeDelta.y <= 100)
            {
                // containerRect가 너무 작으면 zoomTarget 크기로 설정
                Vector2 targetSize = zoomTarget.sizeDelta;
                if (targetSize.x > 0 && targetSize.y > 0)
                {
                    containerRect.sizeDelta = targetSize;
                    Debug.Log($"[GameBoardZoomPan] containerRect 크기를 zoomTarget 크기로 설정: {targetSize}");
                }
                else
                {
                    // zoomTarget 크기도 없으면 기본값 사용
                    containerRect.sizeDelta = new Vector2(1000, 1000);
                    Debug.Log("[GameBoardZoomPan] 기본 크기(1000x1000)로 설정");
                }
            }

            originalSize = containerRect.sizeDelta;

            // 뷰 경계 설정
            viewBounds = new Rect(
                -originalSize.x * 0.5f,
                -originalSize.y * 0.5f,
                originalSize.x,
                originalSize.y
            );

            // Mask 컴포넌트 확인 및 추가
            EnsureMaskComponent();

            // GameBoard 참조 찾기 (셀 raycast 제어용)
            FindGameBoardReference();

            Debug.Log($"[GameBoardZoomPan] 초기화 완료 - Target: {zoomTarget.name}, ContainerSize: {originalSize}, TargetSize: {zoomTarget.sizeDelta}");
        }

        /// <summary>
        /// Mask 컴포넌트가 있는지 확인하고 없으면 추가
        /// </summary>
        private void EnsureMaskComponent()
        {
            Mask mask = GetComponent<Mask>();
            if (mask == null)
            {
                mask = gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                Debug.Log("[GameBoardZoomPan] Mask 컴포넌트 추가됨");
            }

            // Image 컴포넌트가 없으면 추가 (Mask 작동을 위해 필요)
            Image image = GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
                image.color = new Color(1, 1, 1, 0.1f); // 반투명하지만 Mask 동작 및 raycast 감도를 위해 적절한 알파값 설정
                image.raycastTarget = true; // UI 이벤트 수신을 위해 활성화
                Debug.Log("[GameBoardZoomPan] 반투명 Image 컴포넌트 추가됨 (alpha=0.1, raycastTarget=true)");
            }
            else
            {
                // 기존 Image가 있다면 설정 확인 및 수정
                if (!image.raycastTarget)
                {
                    image.raycastTarget = true;
                    Debug.Log("[GameBoardZoomPan] 기존 Image의 raycastTarget을 true로 설정");
                }

                // 알파값이 0이면 Mask가 작동하지 않으므로 약간의 값 설정 (raycast 감도 향상을 위해 0.1로 설정)
                if (image.color.a <= 0.001f)
                {
                    Color currentColor = image.color;
                    currentColor.a = 0.1f;
                    image.color = currentColor;
                    Debug.Log("[GameBoardZoomPan] Image 알파값을 0.1로 설정 (Mask 동작 및 raycast 감도 향상을 위해)");
                }
            }

            // 현재 GameObject가 zoomTarget과 같은 경우 (cellParent에 추가된 경우)
            // Mask가 자기 자신을 클리핑하는 문제를 해결하기 위해 RectMask2D 사용 시도
            if (zoomTarget != null && zoomTarget.gameObject == gameObject)
            {
                Debug.LogWarning("[GameBoardZoomPan] ⚠️ Mask가 zoomTarget과 동일한 GameObject에 있습니다.");
                Debug.LogWarning("[GameBoardZoomPan] Scale 확대 시 클리핑이 제대로 작동하지 않을 수 있습니다.");

                // RectMask2D를 대신 사용해보기
                RectMask2D rectMask = GetComponent<RectMask2D>();
                if (rectMask == null)
                {
                    rectMask = gameObject.AddComponent<RectMask2D>();
                    // 기존 Mask 제거
                    if (mask != null)
                    {
                        DestroyImmediate(mask);
                        Debug.Log("[GameBoardZoomPan] Mask를 RectMask2D로 변경");
                    }
                }

                // RectMask2D 설정 - 패딩으로 더 엄격한 클리핑
                rectMask.padding = new Vector4(0, 0, 0, 0);
                Debug.Log("[GameBoardZoomPan] RectMask2D 클리핑 설정 완료");
            }
            else
            {
                // GameBoard에 직접 추가된 경우, 부모 GameBoard에 클리핑 영역 설정 요청
                if (singleGameBoard != null || multiGameBoard != null)
                {
                    Debug.Log("[GameBoardZoomPan] GameBoard 레벨에서 클리핑 영역 설정 권장");
                }
            }
        }

        /// <summary>
        /// 모바일 입력 처리 (터치 기반)
        /// </summary>
        private void HandleMobileInput()
        {
            if (zoomTarget == null) return;

            if (Input.touchCount == 2)
            {
                // 두 손가락 핀치 줌
                Touch touch1 = Input.GetTouch(0);
                Touch touch2 = Input.GetTouch(1);

                Vector2 touch1Pos = touch1.position;
                Vector2 touch2Pos = touch2.position;

                float currentPinchDistance = Vector2.Distance(touch1Pos, touch2Pos);
                Vector2 currentPinchCenter = (touch1Pos + touch2Pos) * 0.5f;

                if (!isPinching)
                {
                    // 핀치 시작
                    isPinching = true;
                    lastPinchDistance = currentPinchDistance;
                    pinchCenter = currentPinchCenter;
                }
                else if (currentPinchDistance > minPinchDistance)
                {
                    // 핀치 줌 처리
                    float deltaDistance = currentPinchDistance - lastPinchDistance;
                    float zoomDelta = deltaDistance * pinchSensitivity;

                    // 화면 좌표를 로컬 좌표로 변환
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        containerRect, pinchCenter, null, out Vector2 localCenter);

                    ApplyZoom(zoomDelta, localCenter);

                    lastPinchDistance = currentPinchDistance;
                }
            }
            else
            {
                if (isPinching)
                {
                    isPinching = false;
                    Debug.Log("[GameBoardZoomPan] 핀치 종료");
                }
            }

            lastTouches = Input.touches;
        }


        /// <summary>
        /// 확대/축소 적용
        /// </summary>
        /// <param name="zoomDelta">확대/축소 변화량</param>
        /// <param name="zoomCenter">확대/축소 중심점 (로컬 좌표)</param>
        private void ApplyZoom(float zoomDelta, Vector2 zoomCenter)
        {
            if (zoomTarget == null) return;

            float newZoom = Mathf.Clamp(currentZoom + zoomDelta, minZoom, maxZoom);

            if (Mathf.Approximately(newZoom, currentZoom)) return;

            // 확대/축소 중심점을 기준으로 확대
            Vector2 beforeZoomPos = zoomTarget.anchoredPosition;
            Vector2 targetLocalPos = zoomCenter - beforeZoomPos;

            float zoomRatio = newZoom / currentZoom;
            zoomTarget.localScale = originalLocalScale * newZoom;

            // 확대 중심점 유지를 위한 위치 조정
            Vector2 afterZoomPos = zoomCenter - targetLocalPos * zoomRatio;

            currentZoom = newZoom;
            SetPanPosition(afterZoomPos);

            Debug.Log($"[GameBoardZoomPan] 줌 적용: {currentZoom:F2}x, 중심: {zoomCenter}");
        }

        /// <summary>
        /// 팬 위치 설정 (경계 제한 포함)
        /// GridContainer가 GameBoard 영역을 완전히 채우도록 가장자리 제약 적용
        /// </summary>
        /// <param name="newPosition">새로운 위치</param>
        private void SetPanPosition(Vector2 newPosition)
        {
            if (zoomTarget == null) return;

            Vector2 clampedPosition = newPosition;

            if (disablePanLimits)
            {
                // 디버깅용: 제한 없이 자유롭게 이동
                clampedPosition = newPosition;
            }
            else
            {
                // 가장자리 제약 적용: GridContainer가 GameBoard를 완전히 채우도록 제한
                if (currentZoom <= 1.0f)
                {
                    // 100% 이하 줌에서는 팬 불가 (GridContainer가 GameBoard와 같거나 작음)
                    clampedPosition = originalAnchoredPosition;
                }
                else
                {
                    // 줌 시 정확한 가장자리 제약: GridContainer 가장자리가 GameBoard 가장자리와 인접하도록 제한
                    Vector2 scaledSize = originalSize * currentZoom;
                    Vector2 maxPan = (scaledSize - originalSize) * 0.5f;

                    clampedPosition.x = Mathf.Clamp(newPosition.x, -maxPan.x, maxPan.x);
                    clampedPosition.y = Mathf.Clamp(newPosition.y, -maxPan.y, maxPan.y);
                }
            }

            zoomTarget.anchoredPosition = clampedPosition;
            currentPan = clampedPosition - originalAnchoredPosition;

            // 로그는 마우스 이벤트 시작/종료 시점에만 출력 (드래그 중 과도한 로그 방지)
        }

        /// <summary>
        /// 확대/축소 상태를 초기화
        /// </summary>
        public void ResetZoomPan()
        {
            if (zoomTarget == null) return;

            currentZoom = 1.0f;
            currentPan = Vector2.zero;

            zoomTarget.localScale = originalLocalScale;
            zoomTarget.anchoredPosition = originalAnchoredPosition;

            Debug.Log("[GameBoardZoomPan] 줌/팬 초기화");

            // 초기화 시 셀 클릭 모드로 복구
            SetCellRaycastEnabled(true);
            Debug.Log("[GameBoardZoomPan] 🔄 셀 클릭 모드로 복구");
        }

        /// <summary>
        /// 현재 확대 비율 반환
        /// </summary>
        public float GetCurrentZoom() => currentZoom;

        /// <summary>
        /// 현재 팬 오프셋 반환
        /// </summary>
        public Vector2 GetCurrentPan() => currentPan;

        /// <summary>
        /// 드래그 임계값 설정/반환
        /// </summary>
        public float DragThreshold
        {
            get => dragThreshold;
            set => dragThreshold = Mathf.Max(0f, value);
        }

        /// <summary>
        /// 팬 제한 비활성화 설정/반환 (디버깅용)
        /// </summary>
        public bool DisablePanLimits
        {
            get => disablePanLimits;
            set => disablePanLimits = value;
        }

        /// <summary>
        /// 확대/축소 대상 설정
        /// </summary>
        public void SetZoomTarget(RectTransform target)
        {
            zoomTarget = target;
            if (target != null)
            {
                InitializeZoomPan();
            }
        }

        /// <summary>
        /// 강제로 컨테이너 크기 설정 (디버깅용)
        /// </summary>
        public void ForceSetContainerSize(Vector2 size)
        {
            if (containerRect != null)
            {
                containerRect.sizeDelta = size;
                originalSize = size;

                // 뷰 경계 재설정
                viewBounds = new Rect(
                    -originalSize.x * 0.5f,
                    -originalSize.y * 0.5f,
                    originalSize.x,
                    originalSize.y
                );

                Debug.Log($"[GameBoardZoomPan] 컨테이너 크기 강제 설정: {size}");
            }
        }

        /// <summary>
        /// 컴포넌트 상태 확인 (디버깅용)
        /// </summary>
        public bool ValidateSetup()
        {
            bool isValid = true;

            if (containerRect == null)
            {
                Debug.LogError("[GameBoardZoomPan] containerRect가 null입니다!");
                isValid = false;
            }

            if (zoomTarget == null)
            {
                Debug.LogError("[GameBoardZoomPan] zoomTarget이 null입니다!");
                isValid = false;
            }

            if (containerRect != null && (containerRect.sizeDelta.x <= 0 || containerRect.sizeDelta.y <= 0))
            {
                Debug.LogWarning($"[GameBoardZoomPan] containerRect 크기가 유효하지 않습니다: {containerRect.sizeDelta}");
                isValid = false;
            }

            return isValid;
        }


        #region Unity Event Handlers

        public void OnPointerDown(PointerEventData eventData)
        {
            if (zoomTarget == null)
            {
                Debug.LogError("[GameBoardZoomPan] zoomTarget이 null입니다!");
                return;
            }

            // 핀치 중이면 드래그 무시
            if (isPinching)
            {
                return;
            }

            // 기존 시간 기반 이벤트 감지 (호환성 유지)
            clickStartTime = Time.time;
            pointerDownPosition = eventData.position;
            dragStartPanPosition = zoomTarget.anchoredPosition;
            isPanModeActive = false;
            isDragInProgress = false;

            // 지능형 의도 감지 시스템 초기화
            ResetIntentionAnalysis();
            dragStartTime = Time.time;
            dragStartPosition = eventData.position;
            currentDragPosition = eventData.position;
            dragPath.Add(eventData.position);

            // 액션 버튼 패널이 이벤트를 방해하지 않도록 차단
            DisableActionButtonRaycast();

            Debug.Log($"[GameBoardZoomPan] ⬇️ OnPointerDown - 지능형 의도 감지 시작: {eventData.position}");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 액션 버튼 raycast 복원
            RestoreActionButtonRaycast();

            // 최종 의도 분석
            currentDragPosition = eventData.position;
            UserIntention finalIntention = AnalyzeUserIntention();

            if (finalIntention == UserIntention.BlockPlace)
            {
                // 블록 배치 의도 감지
                bool isBlockSelected = IsBlockCurrentlySelected();
                if (isBlockSelected)
                {
                    // 블록이 선택된 상태: 블록 배치 처리
                    HandleBlockPlacementClick(eventData.position);
                    Debug.Log($"[GameBoardZoomPan] ⬆️ OnPointerUp - 🧠 블록 배치 완료 (의도 감지 방식)");
                }
                else
                {
                    // 블록이 선택되지 않은 상태: 블록 미선택 경고 없이 조용히 무시
                    Debug.Log($"[GameBoardZoomPan] ⬆️ OnPointerUp - 블록 미선택 상태에서 탭 (무시)");
                }
            }
            else if (finalIntention == UserIntention.Pan)
            {
                // 팬 의도 감지
                float elapsedTime = Time.time - dragStartTime;
                float dragDistance = Vector2.Distance(dragStartPosition, currentDragPosition);
                Debug.Log($"[GameBoardZoomPan] ⬆️ OnPointerUp - 🧠 팬 완료: {elapsedTime:F3}초, 거리={dragDistance:F1}px, 줌={currentZoom:F2}x");
            }
            else
            {
                // 의도 불명확: 기본 처리
                Debug.Log($"[GameBoardZoomPan] ⬆️ OnPointerUp - 의도 불명확 (Unknown)");
            }

            // 지능형 의도 감지 시스템 유지 (항상 GameBoardZoomPan 활성화)
            RestoreCellInteractionState();

            // 상태 초기화
            isPanModeActive = false;
            isDragInProgress = false;
            ResetIntentionAnalysis();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            // Unity EventSystem에서 드래그 가능성을 인식했을 때 호출
            // 로그 제거 - OnPointerUp에서 최종 결과만 표시
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // 드래그가 시작될 때 호출
            // 로그 제거 - OnPointerUp에서 최종 결과만 표시
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // 드래그 종료 시 상태 정리
            if (isDragInProgress)
            {
                RestoreCellInteractionState();
                isDragInProgress = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (zoomTarget == null) return;

            // 핀치 중이면 드래그 무시
            if (isPinching) return;

            // 지능형 의도 감지: 드래그 경로 업데이트
            currentDragPosition = eventData.position;
            dragPath.Add(eventData.position);

            // 실시간 의도 분석
            UserIntention detectedIntention = AnalyzeUserIntention();

            // 의도가 팬으로 감지되면 즉시 팬 모드 활성화
            if (detectedIntention == UserIntention.Pan && !isPanModeActive)
            {
                isPanModeActive = true;
                isDragInProgress = true;
                currentIntention = UserIntention.Pan;
                Debug.Log("[GameBoardZoomPan] 🧠 팬 의도 감지 → 팬 모드 활성화");

                // 블록 선택 상태여도 팬 기능 우선 (블록은 선택 상태 유지)
                // 더 이상 블록 선택 해제하지 않음 - 사용자 요구사항
            }

            // 팬 모드에서 팬 적용
            if (isPanModeActive && (currentZoom > 1.0f || !enablePanOnlyWhenZoomed))
            {
                // 프레임간 마우스 이동량 사용 (더 안정적)
                Vector2 delta = eventData.delta;

                // Canvas scaleFactor 보정
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    delta /= canvas.scaleFactor;
                }

                // 민감도 적용
                delta *= panSpeed;

                // 현재 위치에 상대적으로 적용
                Vector2 currentPosition = zoomTarget.anchoredPosition;
                Vector2 newPosition = currentPosition + delta;

                SetPanPosition(newPosition);
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (zoomTarget == null) return;

            // 마우스 휠 확대/축소 (에디터 전용)
            float scrollDelta = eventData.scrollDelta.y * zoomSpeed * zoomSensitivity;

            // 마우스 위치를 로컬 좌표로 변환
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                containerRect, eventData.position, eventData.pressEventCamera, out Vector2 localMousePos);

            ApplyZoom(scrollDelta, localMousePos);

            // 줌 동작 시 다른 UI 요소로 이벤트가 전파되지 않도록 함
            eventData.Use();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 실제 드래그가 발생하지 않은 클릭만 하위 요소로 전달
            if (!isActuallyDragging)
            {
                Debug.Log("[GameBoardZoomPan] 클릭 이벤트 - 하위 요소로 전달");
                // 이벤트를 사용하지 않아서 하위 UI 요소들이 클릭을 받을 수 있도록 함
                // eventData.Use()를 호출하지 않음
            }
            else
            {
                Debug.Log("[GameBoardZoomPan] 드래그 후 클릭 - 이벤트 소비");
                // 드래그가 발생한 후의 클릭은 소비
                eventData.Use();
            }
        }

        #endregion

        /// <summary>
        /// GameBoard 참조 찾기 (셀 raycast 제어용)
        /// </summary>
        private void FindGameBoardReference()
        {
            // Single GameBoard 찾기
            singleGameBoard = GetComponentInParent<Features.Single.Gameplay.GameBoard>();
            if (singleGameBoard == null)
            {
                singleGameBoard = FindObjectOfType<Features.Single.Gameplay.GameBoard>();
            }

            // Multi GameBoard 찾기
            multiGameBoard = GetComponentInParent<Features.Multi.UI.GameBoard>();
            if (multiGameBoard == null)
            {
                multiGameBoard = FindObjectOfType<Features.Multi.UI.GameBoard>();
            }

            if (singleGameBoard != null)
            {
                Debug.Log("[GameBoardZoomPan] Single GameBoard 참조 연결됨");
            }
            else if (multiGameBoard != null)
            {
                Debug.Log("[GameBoardZoomPan] Multi GameBoard 참조 연결됨");
            }
            else
            {
                Debug.LogWarning("[GameBoardZoomPan] GameBoard 참조를 찾을 수 없습니다. 셀 raycast 제어가 불가능합니다.");
            }
        }

        /// <summary>
        /// 셀 raycast 상태 제어
        /// </summary>
        /// <param name="enableCellRaycast">true면 셀 클릭 가능, false면 드래그 우선</param>
        private void SetCellRaycastEnabled(bool enableCellRaycast)
        {
            Debug.Log($"[GameBoardZoomPan] 🎛️ 셀 raycast 설정 시작: {enableCellRaycast}");

            if (singleGameBoard != null)
            {
                singleGameBoard.SetCellRaycastEnabled(enableCellRaycast);
                Debug.Log($"[GameBoardZoomPan] ✅ Single GameBoard에 raycast={enableCellRaycast} 설정 완료");

                // 설정 후 즉시 Raycast 테스트로 효과 확인
                if (Input.mousePresent)
                {
                    var currentMousePos = Input.mousePosition;
                    Debug.Log($"[GameBoardZoomPan] 🔄 raycast 변경 후 마우스 위치에서 Raycast 재테스트...");

                    // 짧은 지연 후 테스트 (다음 프레임에서)
                    StartCoroutine(TestRaycastAfterDelay());
                }
            }
            else if (multiGameBoard != null)
            {
                // Multi GameBoard에도 동일한 메서드가 있다면 호출
                // multiGameBoard.SetCellRaycastEnabled(enableCellRaycast);
                Debug.Log("[GameBoardZoomPan] ⚠️ Multi GameBoard의 셀 raycast 제어는 아직 구현되지 않았습니다.");
            }
            else
            {
                Debug.LogWarning("[GameBoardZoomPan] ⚠️ GameBoard 참조가 없어 셀 raycast를 제어할 수 없습니다!");
            }
        }

        /// <summary>
        /// 지능형 의도 감지 시스템: 항상 GameBoardZoomPan이 이벤트를 받아서 의도에 따라 기능 선택
        /// 블록 선택 여부와 무관하게 팬 + 블록 배치 기능 모두 지원
        /// </summary>
        private void RestoreCellInteractionState()
        {
            // 지능형 의도 감지 시스템: 항상 GameBoardZoomPan이 마스터 이벤트 핸들러 역할
            SetGameBoardZoomPanRaycast(true);
            SetCellRaycastEnabled(false);
            Debug.Log("[GameBoardZoomPan] 🧠 지능형 의도 감지 시스템 활성화 (팬 + 블록배치 모두 가능)");
        }

        /// <summary>
        /// 현재 블록이 선택된 상태인지 확인
        /// </summary>
        private bool IsBlockCurrentlySelected()
        {
            // SingleGameManager에서 현재 선택된 블록 상태 확인
            var singleGameManager = FindObjectOfType<Features.Single.Gameplay.SingleGameManager>();
            if (singleGameManager != null)
            {
                // SingleGameManager의 현재 선택된 블록 확인
                // 리플렉션을 사용하여 private 필드에 접근
                var currentSelectedBlockField = singleGameManager.GetType().GetField("_currentSelectedBlock",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (currentSelectedBlockField != null)
                {
                    var currentSelectedBlock = currentSelectedBlockField.GetValue(singleGameManager);
                    bool isSelected = currentSelectedBlock != null;
                    // 블록 선택 상태 확인 로그 제거 (노이즈 감소)
                    return isSelected;
                }
            }

            // MultiGameManager도 확인 (필요 시)
            // var multiGameManager = FindObjectOfType<Features.Multi.Gameplay.MultiGameManager>();
            // if (multiGameManager != null)
            // {
            //     // Multi 게임의 블록 선택 상태 확인 로직 추가 가능
            //     Debug.Log("[GameBoardZoomPan] ℹ️ Multi 게임 블록 선택 상태 확인은 구현되지 않음");
            // }

            // GameManager 없음 - 블록 미선택으로 간주 (로그 제거)
            return false; // 확인할 수 없으면 false 반환 (팬 모드 허용)
        }

        /// <summary>
        /// 사용자가 의도적으로 팬을 시작했을 때 블록 선택을 해제 (UX 개선)
        /// </summary>
        private void ClearBlockSelectionForPan()
        {
            var singleGameManager = FindObjectOfType<Features.Single.Gameplay.SingleGameManager>();
            if (singleGameManager != null)
            {
                // 1. SingleGameManager의 _currentSelectedBlock 해제
                var currentSelectedBlockField = singleGameManager.GetType().GetField("_currentSelectedBlock",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (currentSelectedBlockField != null)
                {
                    currentSelectedBlockField.SetValue(singleGameManager, null);
                    Debug.Log("[GameBoardZoomPan] 🎯 SingleGameManager 블록 선택 해제 완료");
                }

                // 2. 게임 보드의 터치 프리뷰 해제
                var gameBoard = FindObjectOfType<Features.Single.Gameplay.GameBoard>();
                if (gameBoard != null)
                {
                    gameBoard.ClearTouchPreview();
                    Debug.Log("[GameBoardZoomPan] 🎯 게임 보드 터치 프리뷰 해제 완료");
                }

                // 3. BlockPalette의 시각적 선택 상태 해제
                var blockPalette = FindObjectOfType<Features.Single.Gameplay.BlockPalette>();
                if (blockPalette != null)
                {
                    // 현재 선택된 버튼 해제
                    var currentSelectedButtonField = blockPalette.GetType().GetField("_currentSelectedButton",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (currentSelectedButtonField != null)
                    {
                        var currentSelectedButton = currentSelectedButtonField.GetValue(blockPalette);
                        if (currentSelectedButton != null)
                        {
                            // 선택 상태 해제
                            var setSelectedMethod = currentSelectedButton.GetType().GetMethod("SetSelected");
                            setSelectedMethod?.Invoke(currentSelectedButton, new object[] { false });

                            // 필드들 초기화
                            var selectedTypeField = blockPalette.GetType().GetField("_selectedType",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var selectedBlockField = blockPalette.GetType().GetField("_selectedBlock",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                            selectedTypeField?.SetValue(blockPalette, null);
                            selectedBlockField?.SetValue(blockPalette, null);
                            currentSelectedButtonField.SetValue(blockPalette, null);

                            Debug.Log("[GameBoardZoomPan] 🎯 BlockPalette 시각적 선택 해제 완료");
                        }
                    }
                }

                Debug.Log("[GameBoardZoomPan] 🎯 팬 시작으로 인한 완전한 블록 선택 해제 완료");
            }
        }

        private System.Collections.IEnumerator TestRaycastAfterDelay()
        {
            yield return null; // 다음 프레임까지 대기
            // PerformManualRaycastTest(); // 반복 로그 방지를 위해 주석 처리
        }

        #region Debug and Utilities

        /// <summary>
        /// Unity EventSystem 설정 상태를 확인하고 문제점을 진단
        /// </summary>
        private void ValidateEventSystemSetup()
        {
            Debug.Log("[GameBoardZoomPan] ===== EventSystem 진단 시작 =====");

            // 1. EventSystem 존재 확인
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Debug.LogError("[GameBoardZoomPan] ❌ EventSystem이 씬에 없습니다! EventSystem을 추가해야 합니다.");
                return;
            }
            else
            {
                Debug.Log($"[GameBoardZoomPan] ✅ EventSystem 발견: {eventSystem.name}");
                Debug.Log($"[GameBoardZoomPan] 드래그 임계값: {eventSystem.pixelDragThreshold}px");
            }

            // 2. RectTransform 크기 검증
            var rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector2 size = rectTransform.sizeDelta;
                Vector2 worldSize = rectTransform.rect.size;
                Vector3 worldScale = rectTransform.lossyScale;
                Debug.Log($"[GameBoardZoomPan] 🔍 RectTransform 정보: sizeDelta={size}, worldSize={worldSize}, scale={worldScale}");

                if (worldSize.x < 10f || worldSize.y < 10f)
                {
                    Debug.LogWarning($"[GameBoardZoomPan] ⚠️ RectTransform 크기가 너무 작습니다! worldSize={worldSize}");
                }

                // 화면에서의 실제 위치와 크기 확인
                Vector3[] worldCorners = new Vector3[4];
                rectTransform.GetWorldCorners(worldCorners);
                Canvas parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas != null && parentCanvas.worldCamera != null)
                {
                    Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(parentCanvas.worldCamera, worldCorners[0]);
                    Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(parentCanvas.worldCamera, worldCorners[2]);
                    Vector2 screenSize = screenMax - screenMin;
                    Debug.Log($"[GameBoardZoomPan] 🔍 화면 크기: {screenSize}, 위치: {screenMin} ~ {screenMax}");
                }
            }
            else
            {
                Debug.LogWarning("[GameBoardZoomPan] ⚠️ RectTransform이 없습니다!");
            }

            // 3. Canvas 및 GraphicRaycaster 확인
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[GameBoardZoomPan] ❌ Canvas를 찾을 수 없습니다!");
            }
            else
            {
                Debug.Log($"[GameBoardZoomPan] ✅ Canvas 발견: {canvas.name}, renderMode: {canvas.renderMode}");

                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                {
                    Debug.LogError("[GameBoardZoomPan] ❌ Canvas에 GraphicRaycaster가 없습니다!");
                }
                else
                {
                    Debug.Log($"[GameBoardZoomPan] ✅ GraphicRaycaster 발견");
                }
            }

            // 3. 현재 GameObject의 설정 확인
            Image image = GetComponent<Image>();
            if (image == null)
            {
                Debug.LogError("[GameBoardZoomPan] ❌ Image 컴포넌트가 없습니다!");
            }
            else
            {
                Debug.Log($"[GameBoardZoomPan] ✅ Image 컴포넌트: raycastTarget={image.raycastTarget}, alpha={image.color.a:F3}");
            }

            // 4. 인터페이스 구현 확인
            var interfaces = this.GetType().GetInterfaces();
            string implementedInterfaces = string.Join(", ", interfaces.Where(i => i.Name.Contains("Handler")).Select(i => i.Name));
            Debug.Log($"[GameBoardZoomPan] 구현된 이벤트 핸들러: {implementedInterfaces}");

            // 5. Raycast 대상 확인
            if (canvas != null && eventSystem != null)
            {
                PointerEventData testPointer = new PointerEventData(eventSystem);
                testPointer.position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, transform.position);

                List<RaycastResult> results = new List<RaycastResult>();
                eventSystem.RaycastAll(testPointer, results);

                Debug.Log($"[GameBoardZoomPan] Raycast 결과 ({results.Count}개):");
                bool foundSelf = false;
                for (int i = 0; i < Mathf.Min(results.Count, 10); i++) // 10개까지 확인
                {
                    var result = results[i];
                    bool isThisObject = result.gameObject == this.gameObject;
                    if (isThisObject) foundSelf = true;
                    string marker = isThisObject ? " ★ 이 GameObject!" : "";
                    Debug.Log($"  {i+1}. {result.gameObject.name} (거리: {result.distance}){marker}");
                }

                if (!foundSelf && results.Count > 0)
                {
                    Debug.LogWarning("[GameBoardZoomPan] ⚠️ Raycast 결과에 자기 자신이 없습니다! 다른 UI가 이벤트를 가로채고 있습니다.");
                }
                else if (foundSelf)
                {
                    Debug.Log("[GameBoardZoomPan] ✅ Raycast 결과에 자기 자신이 포함되어 있습니다.");
                }

                if (results.Count == 0)
                {
                    Debug.LogWarning("[GameBoardZoomPan] ⚠️ Raycast 결과가 없습니다! 이벤트를 받을 수 없는 상태입니다.");
                }
            }

            Debug.Log("[GameBoardZoomPan] ===== EventSystem 진단 완료 =====");
        }

        /// <summary>
        /// 이벤트 수신 테스트
        /// </summary>
        [ContextMenu("Test Event Reception")]
        private void TestEventReception()
        {
            Debug.Log("[GameBoardZoomPan] ===== 이벤트 수신 테스트 =====");

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Debug.LogError("[GameBoardZoomPan] EventSystem이 없어 테스트할 수 없습니다!");
                return;
            }

            // 현재 마우스 위치에서 Raycast 수행
            PointerEventData pointer = new PointerEventData(eventSystem);
            pointer.position = Input.mousePosition;

            List<RaycastResult> results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, results);

            Debug.Log($"[GameBoardZoomPan] 마우스 위치 {Input.mousePosition}에서 Raycast:");
            bool foundThis = false;

            foreach (var result in results)
            {
                Debug.Log($"  - {result.gameObject.name}");
                if (result.gameObject == this.gameObject)
                {
                    foundThis = true;
                    Debug.Log("    ★ 이 GameObject입니다!");
                }
            }

            if (!foundThis)
            {
                Debug.LogWarning("[GameBoardZoomPan] ⚠️ 현재 마우스 위치에서 이 GameObject가 Raycast 대상이 아닙니다!");
            }

            Debug.Log("[GameBoardZoomPan] ===== 테스트 완료 =====");
        }

        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Print Debug Info")]
        private void PrintDebugInfo()
        {
            Debug.Log($"[GameBoardZoomPan] 디버그 정보:");
            Debug.Log($"  - Current Zoom: {currentZoom:F2}x");
            Debug.Log($"  - Current Pan: {currentPan}");
            Debug.Log($"  - Original Size: {originalSize}");
            Debug.Log($"  - Original Position: {originalAnchoredPosition}");
            Debug.Log($"  - Current Position: {(zoomTarget != null ? zoomTarget.anchoredPosition : Vector2.zero)}");
            Debug.Log($"  - Container Scale: {(containerRect != null ? containerRect.lossyScale : Vector3.zero)}");
            Debug.Log($"  - View Bounds: {viewBounds}");
            Debug.Log($"  - Is Dragging: {isDragging}");
            Debug.Log($"  - Is Actually Dragging: {isActuallyDragging}");
            Debug.Log($"  - Is Pinching: {isPinching}");
            Debug.Log($"  - Drag Threshold: {dragThreshold}");
            Debug.Log($"  - Pan Speed: {panSpeed}");
            Debug.Log($"  - Enable Pan Only When Zoomed: {enablePanOnlyWhenZoomed}");

            // EventSystem 정보도 함께 출력
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                Debug.Log($"  - EventSystem Drag Threshold: {eventSystem.pixelDragThreshold}px");
            }
        }

        /// <summary>
        /// 수동 Raycast 테스트 - OnPointerDown이 호출되지 않는 이유 분석
        /// </summary>
        private void PerformManualRaycastTest()
        {
            Vector2 mousePosition = Input.mousePosition;

            // GraphicRaycaster를 통한 수동 Raycast
            var graphicRaycaster = GetComponentInParent<GraphicRaycaster>();
            if (graphicRaycaster == null)
            {
                Debug.LogError("[GameBoardZoomPan] 🚨 GraphicRaycaster를 찾을 수 없습니다!");
                return;
            }

            // PointerEventData 생성
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = mousePosition
            };

            // Raycast 수행
            var raycastResults = new List<RaycastResult>();
            graphicRaycaster.Raycast(eventData, raycastResults);

            Debug.Log($"[GameBoardZoomPan] 🎯 수동 Raycast 결과 ({raycastResults.Count}개):");

            bool foundSelf = false;
            for (int i = 0; i < raycastResults.Count && i < 5; i++) // 상위 5개만 출력
            {
                var result = raycastResults[i];
                bool isSelf = result.gameObject == gameObject;
                if (isSelf) foundSelf = true;

                Debug.Log($"[GameBoardZoomPan] {i+1}. {result.gameObject.name} " +
                         $"(거리: {result.distance:F1}) {(isSelf ? "⭐ 자기자신" : "")}");
            }

            if (!foundSelf)
            {
                Debug.LogWarning($"[GameBoardZoomPan] ⚠️ Raycast 결과에 자기 자신이 없습니다! 다른 UI가 이벤트를 가로채고 있습니다.");
            }
            else
            {
                Debug.Log("[GameBoardZoomPan] ✅ Raycast에서 자기 자신을 발견 - 하지만 이벤트 미수신으로 부모 CanvasGroup 차단 의심");

                // 강제 이벤트 테스트
                Debug.Log("[GameBoardZoomPan] 🧪 강제 OnPointerDown 테스트 실행...");
                try
                {
                    PointerEventData testPointer = new PointerEventData(EventSystem.current)
                    {
                        position = Input.mousePosition,
                        button = PointerEventData.InputButton.Left
                    };
                    OnPointerDown(testPointer);
                    Debug.Log("[GameBoardZoomPan] ✅ 강제 OnPointerDown 성공 - 컴포넌트 자체는 정상 동작");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[GameBoardZoomPan] ❌ 강제 OnPointerDown 실패: {e.Message}");
                }
                Debug.Log($"[GameBoardZoomPan] ✅ Raycast에서 자기 자신을 발견 - UI 이벤트 전달 문제");
            }
        }

        /// <summary>
        /// 블록 배치 클릭 이벤트를 수동으로 처리 (줌/팬 상태의 GridContainer에서 직접 좌표 변환)
        /// </summary>
        private void HandleBlockPlacementClick(Vector2 screenPosition)
        {
            try
            {
                // Single GameBoard 찾기
                var singleGameBoard = FindObjectOfType<Features.Single.Gameplay.GameBoard>();
                if (singleGameBoard != null)
                {
                    // GameBoard.ScreenToBoard() 대신 직접 변환
                    Position cellPosition = DirectScreenToBoard(screenPosition, singleGameBoard);

                    // 유효한 위치인지 확인
                    if (cellPosition.row >= 0 && cellPosition.col >= 0 && cellPosition.row < 20 && cellPosition.col < 20)
                    {
                        // GameBoard의 OnCellClicked 이벤트 직접 호출
                        singleGameBoard.OnCellClicked?.Invoke(cellPosition);

                        Debug.Log($"[GameBoardZoomPan] 블록 배치: ({cellPosition.row}, {cellPosition.col}) [스크린:{screenPosition}]");
                    }
                    else
                    {
                        Debug.Log($"[GameBoardZoomPan] ⚠️ 유효하지 않은 셀 위치: ({cellPosition.row}, {cellPosition.col}) [스크린:{screenPosition}]");
                    }
                }

                // Multi GameBoard도 동일하게 처리 (필요시)
                // var multiGameBoard = FindObjectOfType<Features.Multi.UI.GameBoard>();
                // if (multiGameBoard != null) { ... }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameBoardZoomPan] ❌ 블록 배치 클릭 처리 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 스크린 좌표를 줌/팬이 적용된 GridContainer 기준으로 직접 보드 좌표 변환
        /// GameBoard.ScreenToBoard()를 우회하여 현재 변환 상태에서 정확한 변환 수행
        /// </summary>
        private Position DirectScreenToBoard(Vector2 screenPosition, Features.Single.Gameplay.GameBoard gameBoard)
        {
            // 1. GameBoard의 GridContainer(cellParent) 찾기
            Transform cellParent = gameBoard.transform.Find("GridContainer");
            if (cellParent == null)
            {
                Debug.LogError("[GameBoardZoomPan] GridContainer를 찾을 수 없습니다!");
                return new Position(-1, -1);
            }

            RectTransform cellParentRect = cellParent.GetComponent<RectTransform>();
            if (cellParentRect == null)
            {
                Debug.LogError("[GameBoardZoomPan] GridContainer에 RectTransform이 없습니다!");
                return new Position(-1, -1);
            }

            // 2. 스크린 좌표를 현재 변환된 GridContainer 로컬 좌표로 변환
            Canvas canvas = GetComponentInParent<Canvas>();
            Vector2 localPosition;

            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    cellParentRect, screenPosition, null, out localPosition);
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    cellParentRect, screenPosition, canvas?.worldCamera, out localPosition);
            }

            // 3. GameBoard 설정값 가져오기 (reflection 필요할 수 있음)
            int boardSize = 20; // 기본값
            float cellSize = 25f; // 기본값

            try
            {
                // GameBoard에서 실제 값 가져오기 (public property가 있다면)
                var boardSizeField = gameBoard.GetType().GetField("boardSize",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var cellSizeField = gameBoard.GetType().GetField("cellSize",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (boardSizeField != null) boardSize = (int)boardSizeField.GetValue(gameBoard);
                if (cellSizeField != null) cellSize = (float)cellSizeField.GetValue(gameBoard);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameBoardZoomPan] GameBoard 설정값 가져오기 실패, 기본값 사용: {e.Message}");
            }

            // 4. 로컬 좌표를 보드 좌표로 변환 (GameBoard.ScreenToBoard()와 동일한 로직)
            float x0 = -(boardSize * 0.5f - 0.5f) * cellSize;
            float y0 = +(boardSize * 0.5f - 0.5f) * cellSize;

            int col = Mathf.FloorToInt((localPosition.x - x0) / cellSize);
            int row = Mathf.FloorToInt((y0 - localPosition.y) / cellSize);

            col = Mathf.Clamp(col, 0, boardSize - 1);
            row = Mathf.Clamp(row, 0, boardSize - 1);

            Debug.Log($"[GameBoardZoomPan] 좌표 변환: 스크린{screenPosition} → 로컬{localPosition} → 셀({row},{col}) [boardSize:{boardSize}, cellSize:{cellSize}]");

            return new Position(row, col);
        }

        /// <summary>
        /// 액션 버튼 패널의 raycast를 일시적으로 비활성화하여 드래그 이벤트가 차단되지 않도록 함
        /// </summary>
        private void DisableActionButtonRaycast()
        {
            try
            {
                // Single GameBoard의 액션 버튼 패널을 reflection으로 직접 접근
                var singleGameBoard = FindObjectOfType<Features.Single.Gameplay.GameBoard>();
                if (singleGameBoard != null)
                {
                    // reflection으로 actionButtonPanel private 필드 접근
                    var actionButtonPanelField = singleGameBoard.GetType()
                        .GetField("actionButtonPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (actionButtonPanelField != null)
                    {
                        var actionButtonPanel = actionButtonPanelField.GetValue(singleGameBoard) as RectTransform;

                        if (actionButtonPanel != null && actionButtonPanel.gameObject.activeInHierarchy)
                        {
                            // ActionButtonPanel 내의 모든 Button과 Image 컴포넌트의 raycastTarget 비활성화
                            var buttons = actionButtonPanel.GetComponentsInChildren<UnityEngine.UI.Button>();
                            var images = actionButtonPanel.GetComponentsInChildren<UnityEngine.UI.Image>();

                            foreach (var button in buttons)
                            {
                                var buttonImage = button.GetComponent<UnityEngine.UI.Image>();
                                if (buttonImage != null)
                                {
                                    buttonImage.raycastTarget = false;
                                }
                            }

                            foreach (var image in images)
                            {
                                image.raycastTarget = false;
                            }

                            Debug.Log($"[GameBoardZoomPan] 🔧 액션 버튼 raycast 비활성화 완료 - {buttons.Length}개 버튼, {images.Length}개 이미지");
                        }
                        else
                        {
                            // ActionButtonPanel이 없는 것은 정상적인 상황일 수 있음 (씬에 따라)
                            // Debug.Log("[GameBoardZoomPan] ActionButtonPanel 없음 (정상)");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[GameBoardZoomPan] ⚠️ actionButtonPanel 필드를 찾을 수 없습니다");
                    }
                }

            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameBoardZoomPan] ❌ 액션 버튼 raycast 비활성화 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 액션 버튼 패널의 raycast를 복원하여 정상적인 버튼 기능이 동작하도록 함
        /// </summary>
        private void RestoreActionButtonRaycast()
        {
            try
            {
                // Single GameBoard의 액션 버튼 패널을 reflection으로 직접 접근
                var singleGameBoard = FindObjectOfType<Features.Single.Gameplay.GameBoard>();
                if (singleGameBoard != null)
                {
                    // reflection으로 actionButtonPanel private 필드 접근
                    var actionButtonPanelField = singleGameBoard.GetType()
                        .GetField("actionButtonPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (actionButtonPanelField != null)
                    {
                        var actionButtonPanel = actionButtonPanelField.GetValue(singleGameBoard) as RectTransform;

                        if (actionButtonPanel != null && actionButtonPanel.gameObject.activeInHierarchy)
                        {
                            // ActionButtonPanel 내의 모든 Button과 Image 컴포넌트의 raycastTarget 복원
                            var buttons = actionButtonPanel.GetComponentsInChildren<UnityEngine.UI.Button>();
                            var images = actionButtonPanel.GetComponentsInChildren<UnityEngine.UI.Image>();

                            foreach (var button in buttons)
                            {
                                var buttonImage = button.GetComponent<UnityEngine.UI.Image>();
                                if (buttonImage != null)
                                {
                                    buttonImage.raycastTarget = true;
                                }
                            }

                            foreach (var image in images)
                            {
                                // 버튼의 자식이 아닌 독립적인 이미지들만 복원 (버튼 이미지는 위에서 처리됨)
                                if (image.GetComponentInParent<UnityEngine.UI.Button>() == null)
                                {
                                    image.raycastTarget = true;
                                }
                            }

                            Debug.Log($"[GameBoardZoomPan] 🔧 액션 버튼 raycast 복원 완료 - {buttons.Length}개 버튼, {images.Length}개 이미지");

                            // ActionButtonPanel 가시성 재보장
                            actionButtonPanel.SetAsLastSibling();
                        }
                        else
                        {
                            // ActionButtonPanel이 없는 것은 정상적인 상황일 수 있음 (씬에 따라)
                            // Debug.Log("[GameBoardZoomPan] ActionButtonPanel 없음 (정상)");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[GameBoardZoomPan] ⚠️ actionButtonPanel 필드를 찾을 수 없습니다");
                    }
                }

            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameBoardZoomPan] ❌ 액션 버튼 raycast 복원 실패: {e.Message}");
            }
        }

        /// <summary>
        /// GameBoardZoomPan 자체의 raycast 상태를 제어
        /// 블록 선택 상태에 따라 이벤트 수신 주체를 분리하기 위함
        /// </summary>
        private void SetGameBoardZoomPanRaycast(bool enabled)
        {
            try
            {
                // GameBoardZoomPan이 부착된 GameObject의 Image 컴포넌트 raycastTarget 제어
                var image = GetComponent<Image>();
                if (image != null)
                {
                    image.raycastTarget = enabled;
                    Debug.Log($"[GameBoardZoomPan] GameBoardZoomPan raycast {(enabled ? "활성화" : "비활성화")}");
                }
                else
                {
                    Debug.LogWarning("[GameBoardZoomPan] ⚠️ GameBoardZoomPan에 Image 컴포넌트가 없어 raycast를 제어할 수 없습니다!");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameBoardZoomPan] ❌ GameBoardZoomPan raycast 제어 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 실시간 사용자 의도 분석 엔진
        /// 드래그 패턴을 분석해서 팬 또는 블록 배치 의도를 감지
        /// </summary>
        private UserIntention AnalyzeUserIntention()
        {
            float dragTime = Time.time - dragStartTime;
            float dragDistance = Vector2.Distance(dragStartPosition, currentDragPosition);
            float dragSpeed = dragTime > 0.01f ? dragDistance / dragTime : 0f;

            // 가중치 기반 스코어링 시스템
            float panScore = 0f;

            // 1. 거리 기준: 15픽셀 이상 드래그 = 팬 의도 가능성
            if (dragDistance > intentionPanDistanceThreshold)
            {
                panScore += 0.4f;
            }

            // 2. 속도 기준: 빠른 드래그 = 팬 의도
            if (dragSpeed > intentionPanSpeedThreshold)
            {
                panScore += 0.3f;
            }

            // 3. 시간 기준: 0.3초 초과 = 팬 확정
            if (dragTime > CLICK_TO_PAN_THRESHOLD)
            {
                panScore += 0.5f;
            }

            // 4. 줌 상태: 확대된 상태에서는 팬 확률 높음
            if (currentZoom > 1.1f)
            {
                panScore += 0.2f;
            }

            // 5. 연속 드래그: 드래그 경로가 직선/곡선이면 팬 의도
            if (dragPath.Count > 3)
            {
                float pathLinearity = CalculatePathLinearity();
                if (pathLinearity > 0.7f) // 직선성이 높으면 팬 의도
                {
                    panScore += 0.1f;
                }
            }

            // 임계값 기준으로 의도 결정
            if (panScore >= intentionAnalysisThreshold)
            {
                return UserIntention.Pan;
            }
            else if (dragTime < CLICK_TO_PAN_THRESHOLD && dragDistance < intentionPanDistanceThreshold)
            {
                return UserIntention.BlockPlace;
            }
            else
            {
                return UserIntention.Unknown;
            }
        }

        /// <summary>
        /// 드래그 경로의 직선성 계산 (0.0 = 완전 무작위, 1.0 = 완전 직선)
        /// </summary>
        private float CalculatePathLinearity()
        {
            if (dragPath.Count < 3) return 0f;

            Vector2 startToEnd = dragPath[dragPath.Count - 1] - dragPath[0];
            float idealDistance = startToEnd.magnitude;

            if (idealDistance < 0.1f) return 0f;

            float actualDistance = 0f;
            for (int i = 1; i < dragPath.Count; i++)
            {
                actualDistance += Vector2.Distance(dragPath[i], dragPath[i - 1]);
            }

            // 직선성 = 이상적 거리 / 실제 거리 (1에 가까울수록 직선)
            return Mathf.Clamp01(idealDistance / actualDistance);
        }

        /// <summary>
        /// 의도 분석 상태 초기화
        /// </summary>
        private void ResetIntentionAnalysis()
        {
            currentIntention = UserIntention.Unknown;
            dragStartPosition = Vector2.zero;
            currentDragPosition = Vector2.zero;
            dragStartTime = 0f;
            totalDragDistance = 0f;
            dragPath.Clear();
        }

        #endregion
    }
}