using UnityEngine;
using UnityEngine.UI;
using BlokusUnity.UI.Messages;

namespace BlokusUnity.UI.Messages
{
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
        [SerializeField] private int maxToastCount = 3; // 동시에 표시할 수 있는 Toast 최대 개수
        [SerializeField] private float toastSpacing = 10f; // Toast 간 간격

        [Header("개발용 설정")]
        [SerializeField] private bool enableDebugMessages = true;

        // 싱글톤
        public static SystemMessageManager Instance { get; private set; }

        // Toast 관리
        private System.Collections.Generic.Queue<ToastMessage> toastPool = new System.Collections.Generic.Queue<ToastMessage>();
        private System.Collections.Generic.List<ToastMessage> activeToasts = new System.Collections.Generic.List<ToastMessage>();

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
                Debug.Log("SystemMessageManager 초기화 완료 - DontDestroyOnLoad 적용됨");
            }
            else
            {
                Debug.Log("SystemMessageManager 중복 인스턴스 제거");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            // Canvas가 없으면 자동 생성
            if (messageCanvas == null)
            {
                CreateMessageCanvas();
            }

            // Toast Container가 없으면 자동 생성
            if (toastContainer == null)
            {
                CreateToastContainer();
            }

            // Toast Prefab이 없으면 기본 Toast 생성
            if (toastPrefab == null)
            {
                CreateDefaultToastPrefab();
            }

            // 초기 Toast 풀 생성
            PrewarmToastPool();
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

            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            Debug.Log("SystemMessageCanvas 자동 생성됨 - DontDestroyOnLoad 적용됨");
        }

        /// <summary>
        /// Toast 컨테이너 생성
        /// </summary>
        private void CreateToastContainer()
        {
            GameObject containerObj = new GameObject("ToastContainer");
            containerObj.transform.SetParent(messageCanvas.transform);

            toastContainer = containerObj.transform;
            RectTransform rectTransform = containerObj.AddComponent<RectTransform>();

            // 화면 상단 중앙에 배치
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0, -50); // 상단에서 50px 아래

            // 세로 레이아웃 그룹 추가
            VerticalLayoutGroup layoutGroup = containerObj.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = toastSpacing;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            Debug.Log("ToastContainer 자동 생성됨");
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

            Debug.Log("기본 ToastPrefab 자동 생성됨");
        }

        /// <summary>
        /// Toast 풀 사전 생성
        /// </summary>
        private void PrewarmToastPool()
        {
            for (int i = 0; i < maxToastCount; i++)
            {
                CreateToastInstance();
            }
        }

        /// <summary>
        /// Toast 인스턴스 생성
        /// </summary>
        private ToastMessage CreateToastInstance()
        {
            GameObject instance = Instantiate(toastPrefab.gameObject, toastContainer);
            ToastMessage toast = instance.GetComponent<ToastMessage>();
            
            toast.OnToastClosed += OnToastClosed;
            toast.gameObject.SetActive(false);
            toastPool.Enqueue(toast);

            return toast;
        }

        /// <summary>
        /// Toast 풀에서 가져오기
        /// </summary>
        private ToastMessage GetToastFromPool()
        {
            if (toastPool.Count == 0)
            {
                return CreateToastInstance();
            }

            return toastPool.Dequeue();
        }

        /// <summary>
        /// Toast 풀에 반환
        /// </summary>
        private void ReturnToastToPool(ToastMessage toast)
        {
            toast.gameObject.SetActive(false);
            toastPool.Enqueue(toast);
        }

        /// <summary>
        /// Toast 닫힘 이벤트 처리
        /// </summary>
        private void OnToastClosed(ToastMessage toast)
        {
            activeToasts.Remove(toast);
            ReturnToastToPool(toast);
        }

        // ========================================
        // 공개 API
        // ========================================

        /// <summary>
        /// Toast 메시지 표시
        /// </summary>
        public void ShowToast(string message, MessagePriority priority = MessagePriority.Info, float duration = 3f)
        {
            // 개발용 메시지 필터링
            if (priority == MessagePriority.Debug && !enableDebugMessages)
                return;

            MessageData messageData = MessageData.CreateToast(message, priority, duration);
            ShowToast(messageData);
        }

        /// <summary>
        /// Toast 메시지 표시 (고급)
        /// </summary>
        public void ShowToast(MessageData messageData)
        {
            // 최대 Toast 개수 제한
            if (activeToasts.Count >= maxToastCount)
            {
                // 가장 오래된 Toast 제거
                ToastMessage oldestToast = activeToasts[0];
                oldestToast.Hide();
            }

            // 새 Toast 생성 및 표시
            ToastMessage toast = GetToastFromPool();
            activeToasts.Add(toast);
            toast.Show(messageData);

            Debug.Log($"Toast 표시: [{messageData.priority}] {messageData.message}");
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
            for (int i = activeToasts.Count - 1; i >= 0; i--)
            {
                activeToasts[i].Hide();
            }
        }

        /// <summary>
        /// 디버그 메시지 활성화/비활성화
        /// </summary>
        public void SetDebugMessagesEnabled(bool enabled)
        {
            enableDebugMessages = enabled;
            Debug.Log($"디버그 메시지 {(enabled ? "활성화" : "비활성화")}");
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
            Debug.LogWarning("Alert 기능은 아직 구현되지 않았습니다. Toast로 대체 표시합니다.");
            ShowWarning($"{title}: {message}");
        }

        /// <summary>
        /// 로딩 오버레이 표시 (향후 구현)
        /// </summary>
        public void ShowLoading(string message = "로딩 중...")
        {
            // TODO: LoadingOverlay 구현 후 추가
            Debug.LogWarning("Loading 기능은 아직 구현되지 않았습니다. Toast로 대체 표시합니다.");
            ShowToast(message, MessagePriority.Info, 0f); // 지속시간 0 = 수동 종료
        }

        /// <summary>
        /// 로딩 오버레이 숨김 (향후 구현)
        /// </summary>
        public void HideLoading()
        {
            // TODO: LoadingOverlay 구현 후 추가
            Debug.LogWarning("Loading 숨김 기능은 아직 구현되지 않았습니다.");
        }
    }
}