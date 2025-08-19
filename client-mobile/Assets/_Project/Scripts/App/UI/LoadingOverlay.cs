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
            spinner.transform.SetParent(stackRoot);     // ⭐ 스택의 자식으로
            RectTransform spinnerRect = spinner.AddComponent<RectTransform>();
            spinnerRect.sizeDelta = new Vector2(180, 180); // 크기만 조정하면 자동 배치!

            spinnerImage = spinner.AddComponent<Image>();
            spinnerImage.color = Color.white;
            CreateDefaultSpinnerSprite(180);
        }

        private void CreateDefaultSpinnerSprite(int diameter = 64)
        {
            int N = Mathf.Max(32, diameter);   // 너무 작지 않게
            Texture2D texture = new Texture2D(N, N, TextureFormat.ARGB32, false);
            Color[] colors = new Color[N * N];

            Vector2 center = new Vector2(N * 0.5f, N * 0.5f);
            float radius = (N * 0.5f) - 2f;

            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    colors[y * N + x] = (d <= radius && d >= radius - Mathf.Max(3f, N * 0.05f)) ? Color.white : Color.clear;
                }
            }
            texture.SetPixels(colors);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            spinnerImage.sprite = sprite;
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
            spinCoroutine = StartCoroutine(SpinCoroutine());

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