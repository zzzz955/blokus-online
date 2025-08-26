using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace App.UI
{
    /// <summary>
    /// Loading overlay with spinner indicator
    /// Migration Plan: Show(string note=null) / Hide() 정적 접근 or 싱글턴(DDOL)
    /// </summary>
    public class LoadingOverlay : MonoBehaviour
    {
        [Header("Loading Components (선택사항 - 없으면 자동 생성)")]
        [SerializeField] private GameObject loadingOverlayPrefab;
        [SerializeField] private GameObject overlayPanel;
        [SerializeField] private Image spinnerImage;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation Settings")]
        [SerializeField] private float spinSpeed = 180f; // degrees per second
        [SerializeField] private float fadeSpeed = 3f;

        [SerializeField] private float minSweep = 0.08f;   // 최소 호 길이(비율)
        [SerializeField] private float maxSweep = 0.6f;    // 최대 호 길이(비율)
        [SerializeField] private float sweepCycle = 1.2f;  // 호가 커졌다 작아지는 한 사이클(초)

        // Singleton pattern
        public static LoadingOverlay Instance { get; private set; }

        // State
        private bool isShowing = false;
        private Coroutine spinCoroutine;
        private Coroutine fadeCoroutine;
        private RectTransform stackRoot;

        void Awake()
        {
            // Singleton pattern with DontDestroyOnLoad
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);

                InitializeComponents();
                Debug.Log("LoadingOverlay initialized with DontDestroyOnLoad");
            }
            else
            {
                Debug.Log("LoadingOverlay duplicate instance destroyed");

                // Transfer prefab reference if this instance has it but the singleton doesn't
                if (loadingOverlayPrefab != null && Instance.loadingOverlayPrefab == null)
                {
                    Instance.loadingOverlayPrefab = loadingOverlayPrefab;
                    Instance.InitializeComponents();
                    Debug.Log("LoadingOverlay prefab transferred to singleton instance");
                }

                Destroy(gameObject);
            }
        }

        void Start()
        {
            // Start hidden
            Hide();
        }

        private void InitializeComponents()
        {
            // Use prefab if assigned, otherwise auto-create
            if (loadingOverlayPrefab != null)
            {
                UsePrefabComponents();
            }
            else if (overlayPanel == null)
            {
                CreateOverlayPanel();
            }

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            // Ensure overlay starts at top UI layer
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999; // Top layer

                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void UsePrefabComponents()
        {
            // Instantiate prefab as child
            GameObject prefabInstance = Instantiate(loadingOverlayPrefab, transform);

            // Find components in prefab
            overlayPanel = prefabInstance;
            spinnerImage = prefabInstance.GetComponentInChildren<Image>();
            loadingText = prefabInstance.GetComponentInChildren<TMP_Text>();

            Debug.Log("LoadingOverlay using assigned prefab");
        }

        private void CreateOverlayPanel()
        {
            // Create overlay panel GameObject
            GameObject panel = new GameObject("OverlayPanel");
            panel.transform.SetParent(transform);

            // Setup RectTransform to fill screen
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Add semi-transparent background
            Image bgImage = panel.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.5f);

            overlayPanel = panel;

            CreateCenterStack();
            // Create spinner
            CreateSpinner();

            // Create loading text
            CreateLoadingText();

            Debug.Log("LoadingOverlay components auto-created");
        }

        private void CreateCenterStack()
        {
            GameObject stack = new GameObject("CenterStack");
            stack.transform.SetParent(overlayPanel.transform);

            stackRoot = stack.AddComponent<RectTransform>();
            stackRoot.anchorMin = new Vector2(0.5f, 0.5f);
            stackRoot.anchorMax = new Vector2(0.5f, 0.5f);
            stackRoot.pivot = new Vector2(0.5f, 0.5f);
            stackRoot.anchoredPosition = Vector2.zero;
            stackRoot.sizeDelta = Vector2.zero;

            var layout = stack.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 50;                        // 스피너-텍스트 간격
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            stack.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
                UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        }

        private void CreateSpinner()
        {
            GameObject spinner = new GameObject("Spinner");
            spinner.transform.SetParent(stackRoot);
            RectTransform spinnerRect = spinner.AddComponent<RectTransform>();
            spinnerRect.sizeDelta = new Vector2(180, 180);

            spinnerImage = spinner.AddComponent<Image>();
            spinnerImage.raycastTarget = false;
            spinnerImage.color = Color.white;

            // 각도 그라디언트가 들어간 링 스프라이트 생성
            CreateDefaultSpinnerSprite(180);

            // 안드로이드 스피너처럼 '부분 호'만 보이도록 Filled 설정
            spinnerImage.type = Image.Type.Filled;
            spinnerImage.fillMethod = Image.FillMethod.Radial360;
            spinnerImage.fillOrigin = 0;        // Top(0) 기준 시작
            spinnerImage.fillClockwise = true;  // 시계방향으로 채움
            spinnerImage.fillAmount = 0.15f;    // 시작 호 길이(15%)
            spinnerImage.preserveAspect = true;
        }

        private void CreateDefaultSpinnerSprite(int diameter = 64)
        {
            int N = Mathf.Max(32, diameter);
            float thickness = Mathf.Max(3f, N * 0.10f);   // 링 두께(지름의 10%)
            Texture2D texture = new Texture2D(N, N, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] colors = new Color[N * N];
            Vector2 center = new Vector2(N * 0.5f, N * 0.5f);
            float radius = (N * 0.5f) - 2f;

            // 각도 그라디언트 파라미터
            // angleNorm: 0(머리) -> 1(꼬리)로 갈수록 알파가 줄어드는 램프
            // gamma로 꼬리 페이드 곡률 조정
            float gamma = 1.8f;

            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    int idx = y * N + x;
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Vector2.Distance(p, center);

                    bool inRing = (d <= radius && d >= radius - thickness);
                    if (!inRing)
                    {
                        colors[idx] = Color.clear;
                        continue;
                    }

                    // 각도 계산: Top 기준(위쪽이 0도), 시계방향 증가로 정규화
                    float angleRad = Mathf.Atan2(p.y - center.y, p.x - center.x); // -pi..pi, 기준: +X축
                    float angleDeg = angleRad * Mathf.Rad2Deg;                    // -180..180
                                                                                  // +X축 기준을 'Top(북쪽)' 기준으로 회전 보정: +90도
                    angleDeg = (angleDeg + 450f) % 360f; // (angle+90) % 360, +360 보정

                    // 0..1 정규화 (0: 머리, 1: 꼬리)
                    float angleNorm = angleDeg / 360f;

                    // 꼬리로 갈수록 옅어지도록: alpha = 1 - ramp^gamma
                    float alpha = 1f - Mathf.Pow(angleNorm, gamma);

                    // 링 픽셀 내에서도 가장자리 안티앨리어싱(선택)
                    float inner = radius - thickness;
                    float t = Mathf.InverseLerp(inner, radius, d);
                    float edgeAA = Mathf.SmoothStep(0f, 1f, Mathf.Min(t, 1f - t) * 2f);

                    Color c = Color.white;
                    c.a = Mathf.Clamp01(alpha * edgeAA);
                    colors[idx] = c;
                }
            }

            texture.SetPixels(colors);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            spinnerImage.sprite = sprite;
        }

        private System.Collections.IEnumerator SpinnerAnimCoroutine()
        {
            float t = 0f;
            bool expanding = true;

            while (isShowing && spinnerImage != null)
            {
                // 회전
                spinnerImage.transform.Rotate(0f, 0f, -spinSpeed * Time.deltaTime);

                // 호 길이 애니메이션 (Ping-Pong)
                t += Time.deltaTime / (sweepCycle * 0.5f); // 반 주기
                if (t >= 1f)
                {
                    t = 0f;
                    expanding = !expanding;

                    // 매 전환 시 약간의 각도 점프를 줘서 시작점이 바뀌는 느낌 (선택)
                    spinnerImage.transform.Rotate(0f, 0f, -30f);
                }

                float sweep = expanding
                    ? Mathf.Lerp(minSweep, maxSweep, Mathf.SmoothStep(0f, 1f, t))
                    : Mathf.Lerp(maxSweep, minSweep, Mathf.SmoothStep(0f, 1f, t));

                spinnerImage.fillAmount = sweep;

                yield return null;
            }
        }

        private void CreateLoadingText()
        {
            GameObject textObj = new GameObject("LoadingText");
            textObj.transform.SetParent(stackRoot);     // ⭐ 스택의 자식으로
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(600, 40);

            loadingText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            loadingText.text = "로딩 중...";
            loadingText.fontSize = 60;
            loadingText.color = Color.white;
            loadingText.alignment = TMPro.TextAlignmentOptions.Center;
        }
        /// <summary>
        /// Show loading overlay with optional note
        /// Migration Plan: 스피너는 항상 최상위 UI로 표시
        /// </summary>
        public static void Show(string note = null)
        {
            if (Instance == null)
            {
                Debug.LogError("LoadingOverlay.Show() called but Instance is null!");
                return;
            }

            Instance.ShowInternal(note);
        }

        /// <summary>
        /// Hide loading overlay
        /// Migration Plan: 전환 전후 깜빡임/입력 누수 없음
        /// </summary>
        public static void Hide()
        {
            if (Instance == null)
            {
                Debug.LogWarning("LoadingOverlay.Hide() called but Instance is null");
                return;
            }

            Instance.HideInternal();
        }

        private void ShowInternal(string note = null)
        {
            if (isShowing) return;

            isShowing = true;

            // Check if components are still valid
            if (overlayPanel == null || loadingText == null || spinnerImage == null)
            {
                Debug.LogWarning("LoadingOverlay components are null, re-initializing...");
                InitializeComponents();
            }

            // Update text if provided
            if (loadingText != null && !string.IsNullOrEmpty(note))
            {
                loadingText.text = note;
            }

            // Show overlay (null check)
            if (overlayPanel != null)
            {
                overlayPanel.SetActive(true);
            }
            else
            {
                Debug.LogError("LoadingOverlay: overlayPanel is null!");
                return;
            }

            // Start spinner animation
            if (spinCoroutine != null)
                StopCoroutine(spinCoroutine);
            spinCoroutine = StartCoroutine(SpinnerAnimCoroutine());

            // Fade in
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeIn());

            Debug.Log($"LoadingOverlay shown: {note ?? "로딩 중..."}");
        }

        private void HideInternal()
        {
            if (!isShowing) return;

            isShowing = false;

            // Stop spinner
            if (spinCoroutine != null)
            {
                StopCoroutine(spinCoroutine);
                spinCoroutine = null;
            }

            // Fade out (null check)
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            if (canvasGroup != null && overlayPanel != null)
            {
                fadeCoroutine = StartCoroutine(FadeOut());
            }
            else
            {
                Debug.LogWarning("LoadingOverlay: Components are null during hide, forcing immediate hide");
                if (overlayPanel != null)
                    overlayPanel.SetActive(false);
            }

            Debug.Log("LoadingOverlay hidden");
        }

        private System.Collections.IEnumerator SpinCoroutine()
        {
            while (isShowing && spinnerImage != null)
            {
                if (spinnerImage != null) // Double check for null
                {
                    spinnerImage.transform.Rotate(0, 0, -spinSpeed * Time.deltaTime);
                }
                yield return null;
            }
        }

        private System.Collections.IEnumerator FadeIn()
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            float duration = 1f / fadeSpeed;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;

            while (elapsed < duration && canvasGroup != null)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
                yield return null;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        private System.Collections.IEnumerator FadeOut()
        {
            if (canvasGroup == null)
            {
                if (overlayPanel != null)
                    overlayPanel.SetActive(false);
                yield break;
            }

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            float duration = 1f / fadeSpeed;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            while (elapsed < duration && canvasGroup != null)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (overlayPanel != null)
                overlayPanel.SetActive(false);
        }

        /// <summary>
        /// Check if loading overlay is currently showing
        /// </summary>
        public static bool IsShowing
        {
            get { return Instance != null && Instance.isShowing; }
        }
    }
}