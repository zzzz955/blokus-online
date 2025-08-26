using UnityEngine;
using UnityEngine.UI;
using Shared.UI;

namespace App.UI{
    /// <summary>
    /// 전역 시스템 메시지 매니저
    /// Toast, Alert, Loading 등 모든 시스템 메시지를 중앙에서 관리
    /// </summary>
    public class SystemMessageManager : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Canvas messageCanvas;
        [SerializeField] private ToastMessage toastPrefab;
        [SerializeField] private Transform toastContainer;

        [Header("Toast 설정")]
        [SerializeField] private int fixedToastCount = 3; // 미리 생성할 Toast 개수
        [SerializeField] private float toastSpacing = 10f; // Toast 간 간격
        [SerializeField] private int maxQueueSize = 10; // 최대 대기열 크기

        [Header("보안 설정")]
        [SerializeField] private float rateLimitWindow = 1f; // Rate limit 시간 (초)
        [SerializeField] private int maxToastsPerSecond = 5; // 초당 최대 Toast 개수

        [Header("개발용 설정")]
        [SerializeField] private bool enableDebugMessages = true;

        // 싱글톤
        public static SystemMessageManager Instance { get; private set; }

        // Toast 관리 (고정 개수 + 큐잉 시스템)
        private ToastMessage[] toastSlots; // 고정된 3개 Toast 슬롯
        private System.Collections.Generic.Queue<MessageData> messageQueue = new System.Collections.Generic.Queue<MessageData>(); // 대기열
        private System.Collections.Generic.Queue<float> toastRequestTimes = new System.Collections.Generic.Queue<float>(); // Rate limiting용

        void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                
                // 루트 GameObject로 이동 (DontDestroyOnLoad 적용을 위해)
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);

                InitializeComponents();
                // Debug.Log("SystemMessageManager 초기화 완료 - DontDestroyOnLoad 적용됨");
            }
            else
            {
                // Debug.Log("SystemMessageManager 중복 인스턴스 제거");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            // Debug.Log("[SystemMessageManager] InitializeComponents 시작");
            
            // Canvas가 없으면 자동 생성
            if (messageCanvas == null)
            {
                // Debug.Log("[SystemMessageManager] Canvas가 없어서 생성 중...");
                CreateMessageCanvas();
            }
            else
            {
                // Debug.Log("[SystemMessageManager] Canvas 이미 존재함");
            }

            // Toast Container가 없으면 자동 생성
            if (toastContainer == null)
            {
                // Debug.Log("[SystemMessageManager] ToastContainer가 없어서 생성 중...");
                CreateToastContainer();
            }
            else
            {
                // Debug.Log("[SystemMessageManager] ToastContainer 이미 존재함");
            }

            // Toast Prefab이 없으면 기본 Toast 생성
            if (toastPrefab == null)
            {
                // Debug.Log("[SystemMessageManager] ToastPrefab이 없어서 생성 중...");
                CreateDefaultToastPrefab();
            }
            else
            {
                // Debug.Log("[SystemMessageManager] ToastPrefab 이미 존재함");
            }

            // 고정된 Toast 슬롯 생성
            CreateFixedToastSlots();
            
            // Debug.Log("[SystemMessageManager] InitializeComponents 완료");
        }

        /// <summary>
        /// 메시지 캔버스 생성
        /// </summary>
        private void CreateMessageCanvas()
        {
            GameObject canvasObj = new GameObject("SystemMessageCanvas");
            // 루트 GameObject로 생성 (DontDestroyOnLoad 적용을 위해)
            canvasObj.transform.SetParent(null);
            DontDestroyOnLoad(canvasObj);

            messageCanvas = canvasObj.AddComponent<Canvas>();
            messageCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            messageCanvas.sortingOrder = 100; // 다른 UI보다 위에 표시

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();

            // // Debug.Log($"SystemMessageCanvas 자동 생성됨 - Active: {canvasObj.activeInHierarchy}, DontDestroyOnLoad 적용됨");
        }

        /// <summary>
        /// Toast 컨테이너 생성
        /// </summary>
        private void CreateToastContainer()
        {
            // Canvas가 제대로 생성되었는지 확인
            if (messageCanvas == null)
            {
                // Debug.LogError("[SystemMessageManager] Canvas가 null입니다! CreateMessageCanvas()를 먼저 호출하세요.");
                return;
            }

            GameObject containerObj = new GameObject("ToastContainer");
            containerObj.transform.SetParent(messageCanvas.transform, false); // worldPositionStays = false

            toastContainer = containerObj.transform;
            RectTransform rectTransform = containerObj.AddComponent<RectTransform>();

            // 전체 화면 크기로 설정
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // 고정 위치 시스템 사용 (VerticalLayoutGroup 제거)
            // Toast들은 개별적으로 고정된 위치에 배치됨

            // Debug.Log($"ToastContainer 생성됨 - Active: {containerObj.activeInHierarchy}, Parent Active: {messageCanvas.gameObject.activeInHierarchy}");

            // Debug.Log("ToastContainer 자동 생성됨");
        }

        /// <summary>
        /// 기본 Toast Prefab 생성
        /// </summary>
        private void CreateDefaultToastPrefab()
        {
            GameObject toastObj = new GameObject("DefaultToast");
            toastObj.transform.SetParent(toastContainer);

            // RectTransform 설정
            RectTransform rectTransform = toastObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(300, 60);

            // CanvasGroup 추가
            toastObj.AddComponent<CanvasGroup>();

            // 배경 이미지
            Image bgImage = toastObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // 메시지 텍스트
            GameObject textObj = new GameObject("MessageText");
            textObj.transform.SetParent(toastObj.transform);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            TMPro.TMP_Text messageText = textObj.AddComponent<TMPro.TMP_Text>();
            messageText.text = "Default Toast";
            messageText.fontSize = 14;
            messageText.color = Color.white;
            messageText.alignment = TMPro.TextAlignmentOptions.Center;
            messageText.verticalAlignment = TMPro.VerticalAlignmentOptions.Middle;

            // ToastMessage 컴포넌트 추가
            toastPrefab = toastObj.AddComponent<ToastMessage>();

            // Prefab 준비를 위해 비활성화
            toastObj.SetActive(false);

            // Debug.Log("기본 ToastPrefab 자동 생성됨");
        }

        /// <summary>
        /// 고정된 Toast 슬롯 생성
        /// </summary>
        private void CreateFixedToastSlots()
        {
            toastSlots = new ToastMessage[fixedToastCount];
            
            for (int i = 0; i < fixedToastCount; i++)
            {
                ToastMessage toast = CreateSafeToastInstance(i);
                if (toast != null)
                {
                    toastSlots[i] = toast;
                    toast.OnToastClosed += OnToastClosed;
                    
                    // 각 슬롯에 고정된 Y 위치 설정 (위에서부터 차례로)
                    SetToastSlotPosition(toast, i);
                    
                    // Debug.Log($"[SystemMessageManager] Toast 슬롯 {i} 생성 완료 - Y위치: {-i * (60 + toastSpacing)}");
                }
                else
                {
                    // Debug.LogError($"[SystemMessageManager] Toast 슬롯 {i} 생성 실패!");
                }
            }
        }

        /// <summary>
        /// Toast 슬롯의 고정 위치 설정
        /// </summary>
        private void SetToastSlotPosition(ToastMessage toast, int slotIndex)
        {
            RectTransform rectTransform = toast.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // 상단에서부터 차례로 배치 (0번이 가장 위, 1번이 그 아래...)
                float yPosition = -slotIndex * (60 + toastSpacing); // 토스트 높이 60 + 간격
                
                rectTransform.anchoredPosition = new Vector2(0, yPosition);
                rectTransform.anchorMin = new Vector2(0.5f, 1f); // 상단 중앙 기준
                rectTransform.anchorMax = new Vector2(0.5f, 1f);
                
                // Debug.Log($"[SystemMessageManager] Toast 슬롯 {slotIndex} 위치 설정: Y={yPosition}");
            }
        }

        /// <summary>
        /// 안전한 Toast 인스턴스 생성
        /// </summary>
        private ToastMessage CreateSafeToastInstance(int slotIndex)
        {
            try
            {
                if (toastPrefab == null)
                {
                    // Debug.LogWarning("[SystemMessageManager] ToastPrefab이 null, 간단한 Toast 생성");
                    return CreateSimpleToast(slotIndex);
                }
                
                GameObject instance = Instantiate(toastPrefab.gameObject, toastContainer, false);
                instance.name = $"ToastSlot_{slotIndex}";
                
                ToastMessage toast = instance.GetComponent<ToastMessage>();
                if (toast == null)
                {
                    // Debug.LogError("[SystemMessageManager] ToastMessage 컴포넌트가 없습니다!");
                    Destroy(instance);
                    return CreateSimpleToast(slotIndex);
                }
                
                instance.SetActive(false);
                return toast;
            }
            catch (System.Exception e)
            {
                // Debug.LogError($"[SystemMessageManager] Toast 생성 중 오류: {e.Message}");
                return CreateSimpleToast(slotIndex);
            }
        }

        /// <summary>
        /// 간단한 Toast 런타임 생성 (대안)
        /// </summary>
        private ToastMessage CreateSimpleToast(int slotIndex)
        {
            GameObject toastObj = new GameObject($"ToastSlot_{slotIndex}");
            toastObj.transform.SetParent(toastContainer, false);
            
            RectTransform rectTransform = toastObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(300, 60);
            
            toastObj.AddComponent<CanvasGroup>();
            
            Image bgImage = toastObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.8f);
            
            // 간단한 텍스트 추가
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(toastObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            Text text = textObj.AddComponent<Text>();
            text.text = "Toast Message";
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            
            ToastMessage toastComponent = toastObj.AddComponent<ToastMessage>();
            toastObj.SetActive(false);
            
            // Debug.Log($"[SystemMessageManager] 간단한 Toast 생성 완료: {slotIndex}");
            return toastComponent;
        }

        /// <summary>
        /// 사용 가능한 Toast 슬롯 찾기
        /// </summary>
        private ToastMessage GetAvailableToastSlot()
        {
            for (int i = 0; i < toastSlots.Length; i++)
            {
                if (toastSlots[i] != null && !toastSlots[i].gameObject.activeSelf)
                {
                    // Debug.Log($"[SystemMessageManager] Toast 슬롯 {i} 사용 가능");
                    return toastSlots[i];
                }
            }
            
            // Debug.LogWarning("[SystemMessageManager] 사용 가능한 Toast 슬롯이 없습니다. 큐에 추가합니다.");
            return null;
        }

        /// <summary>
        /// Toast 큐 처리
        /// </summary>
        private void ProcessToastQueue()
        {
            if (messageQueue.Count == 0) return;

            ToastMessage availableSlot = GetAvailableToastSlot();
            if (availableSlot != null)
            {
                MessageData messageData = messageQueue.Dequeue();
                DisplayToastInSlot(availableSlot, messageData);
                // Debug.Log($"[SystemMessageManager] 큐에서 메시지 처리: {messageData.message}");
            }
        }

        /// <summary>
        /// 특정 슬롯에 Toast 표시
        /// </summary>
        private void DisplayToastInSlot(ToastMessage toast, MessageData messageData)
        {
            if (toast == null) return;
            
            // 안전한 활성화 및 표시
            toast.gameObject.SetActive(true);
            
            // GameObject가 제대로 활성화되었는지 확인
            if (toast.gameObject.activeSelf)
            {
                toast.Show(messageData);
                // Debug.Log($"[SystemMessageManager] Toast 슬롯에서 메시지 표시 성공: {messageData.message}");
            }
            else
            {
                // Debug.LogError($"[SystemMessageManager] Toast 활성화 실패, 직접 표시: {messageData.message}");
                // 대안: 직접 메시지 출력 (Console이나 다른 방법)
                // Debug.Log($"[TOAST] {messageData.priority}: {messageData.message}");
            }
        }

        /// <summary>
        /// Toast 닫힘 이벤트 처리
        /// </summary>
        private void OnToastClosed(ToastMessage toast)
        {
            // Toast 슬롯이 비워졌으므로 큐에서 대기 중인 메시지 처리
            ProcessToastQueue();
        }

        // ========================================
        // 공개 API
        // ========================================

        /// <summary>
        /// Toast 메시지 표시 (Migration Plan: 간단한 API)
        /// </summary>
        public static void ShowToast(string message, MessagePriority priority = MessagePriority.Info, float duration = 3f)
        {
            if (Instance == null)
            {
                // Debug.LogError("SystemMessageManager.ShowToast() called but Instance is null!");
                return;
            }
            
            Instance.ShowToastInternal(message, priority, duration);
        }

        /// <summary>
        /// Rate limiting 체크
        /// </summary>
        private bool IsRateLimited()
        {
            float currentTime = Time.time;
            
            // 오래된 요청들 제거 (rateLimitWindow보다 오래된 것들)
            while (toastRequestTimes.Count > 0 && currentTime - toastRequestTimes.Peek() > rateLimitWindow)
            {
                toastRequestTimes.Dequeue();
            }
            
            // Rate limit 체크
            if (toastRequestTimes.Count >= maxToastsPerSecond)
            {
                // Debug.LogWarning($"[SystemMessageManager] Rate limit 초과! 초당 최대 {maxToastsPerSecond}개");
                return true; // Rate limited
            }
            
            // 현재 요청 시간 기록
            toastRequestTimes.Enqueue(currentTime);
            return false; // Not rate limited
        }

        private void ShowToastInternal(string message, MessagePriority priority, float duration)
        {
            // 개발용 메시지 필터링
            if (priority == MessagePriority.Debug && !enableDebugMessages)
                return;

            // 보안 체크: Rate limiting
            if (IsRateLimited())
            {
                return; // 요청 거부
            }

            MessageData messageData = MessageData.CreateToast(message, priority, duration);
            ShowToastInternal(messageData);
        }

        /// <summary>
        /// Toast 메시지 표시 (고급) - 고정 슬롯 시스템
        /// </summary>
        private void ShowToastInternal(MessageData messageData)
        {
            // 사용 가능한 슬롯 확인
            ToastMessage availableSlot = GetAvailableToastSlot();
            
            if (availableSlot != null)
            {
                // 즉시 표시
                DisplayToastInSlot(availableSlot, messageData);
                // Debug.Log($"Toast 즉시 표시: [{messageData.priority}] {messageData.message}");
            }
            else
            {
                // 큐에 추가 (큐 크기 제한)
                if (messageQueue.Count < maxQueueSize)
                {
                    messageQueue.Enqueue(messageData);
                    // Debug.Log($"Toast 큐에 추가: [{messageData.priority}] {messageData.message} (큐 크기: {messageQueue.Count})");
                }
                else
                {
                    // Debug.LogWarning($"Toast 큐 포화! 메시지 무시: [{messageData.priority}] {messageData.message}");
                }
            }
        }

        /// <summary>
        /// 성공 메시지 표시
        /// </summary>
        public void ShowSuccess(string message, float duration = 3f)
        {
            ShowToast(message, MessagePriority.Success, duration);
        }

        /// <summary>
        /// 경고 메시지 표시
        /// </summary>
        public void ShowWarning(string message, float duration = 4f)
        {
            ShowToast(message, MessagePriority.Warning, duration);
        }

        /// <summary>
        /// 오류 메시지 표시
        /// </summary>
        public void ShowError(string message, float duration = 5f)
        {
            ShowToast(message, MessagePriority.Error, duration);
        }

        /// <summary>
        /// 모든 Toast 메시지 숨김
        /// </summary>
        public void HideAllToasts()
        {
            // 모든 활성 Toast 슬롯 숨김
            for (int i = 0; i < toastSlots.Length; i++)
            {
                if (toastSlots[i] != null && toastSlots[i].gameObject.activeSelf)
                {
                    toastSlots[i].Hide();
                }
            }
            
            // 대기 큐 비우기
            messageQueue.Clear();
            // Debug.Log("[SystemMessageManager] 모든 Toast 숨김 및 큐 비우기 완료");
        }

        /// <summary>
        /// 디버그 메시지 활성화/비활성화
        /// </summary>
        public void SetDebugMessagesEnabled(bool enabled)
        {
            enableDebugMessages = enabled;
            // Debug.Log($"디버그 메시지 {(enabled ? "활성화" : "비활성화")}");
        }

        // ========================================
        // 향후 확장용 (Alert, Loading 등)
        // ========================================

        /// <summary>
        /// Alert 메시지 표시 (향후 구현)
        /// </summary>
        public void ShowAlert(string message, string title = "", System.Action onConfirm = null, System.Action onCancel = null)
        {
            // TODO: AlertModal 구현 후 추가
            // Debug.LogWarning("Alert 기능은 아직 구현되지 않았습니다. Toast로 대체 표시합니다.");
            ShowWarning($"{title}: {message}");
        }

        /// <summary>
        /// 로딩 오버레이 표시 (향후 구현)
        /// </summary>
        public void ShowLoading(string message = "로딩 중...")
        {
            // TODO: LoadingOverlay 구현 후 추가
            // Debug.LogWarning("Loading 기능은 아직 구현되지 않았습니다. Toast로 대체 표시합니다.");
            ShowToast(message, MessagePriority.Info, 0f); // 지속시간 0 = 수동 종료
        }

        /// <summary>
        /// 로딩 오버레이 숨김 (향후 구현)
        /// </summary>
        public void HideLoading()
        {
            // TODO: LoadingOverlay 구현 후 추가
            // Debug.LogWarning("Loading 숨김 기능은 아직 구현되지 않았습니다.");
        }
    }
}