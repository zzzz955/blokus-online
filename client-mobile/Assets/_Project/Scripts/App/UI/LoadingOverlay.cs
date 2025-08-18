using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BlokusUnity.UI
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
            
            // Create spinner
            CreateSpinner();
            
            // Create loading text
            CreateLoadingText();
            
            Debug.Log("LoadingOverlay components auto-created");
        }

        private void CreateSpinner()
        {
            GameObject spinner = new GameObject("Spinner");
            spinner.transform.SetParent(overlayPanel.transform);
            
            RectTransform spinnerRect = spinner.AddComponent<RectTransform>();
            spinnerRect.anchorMin = new Vector2(0.5f, 0.5f);
            spinnerRect.anchorMax = new Vector2(0.5f, 0.5f);
            spinnerRect.sizeDelta = new Vector2(60, 60);
            spinnerRect.anchoredPosition = new Vector2(0, 20);
            
            spinnerImage = spinner.AddComponent<Image>();
            spinnerImage.color = Color.white;
            
            // Create a simple spinner sprite if none exists
            // In a real project, you'd assign a proper spinner sprite
            CreateDefaultSpinnerSprite();
        }

        private void CreateDefaultSpinnerSprite()
        {
            // Create a simple circle texture for spinner
            Texture2D texture = new Texture2D(64, 64);
            Color[] colors = new Color[64 * 64];
            
            Vector2 center = new Vector2(32, 32);
            float radius = 30f;
            
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    
                    if (distance <= radius && distance >= radius - 4)
                    {
                        colors[y * 64 + x] = Color.white;
                    }
                    else
                    {
                        colors[y * 64 + x] = Color.clear;
                    }
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            spinnerImage.sprite = sprite;
        }

        private void CreateLoadingText()
        {
            GameObject textObj = new GameObject("LoadingText");
            textObj.transform.SetParent(overlayPanel.transform);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(200, 30);
            textRect.anchoredPosition = new Vector2(0, -30);
            
            loadingText = textObj.AddComponent<TMP_Text>();
            loadingText.text = "로딩 중...";
            loadingText.fontSize = 16;
            loadingText.color = Color.white;
            loadingText.alignment = TextAlignmentOptions.Center;
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
            
            // Update text if provided
            if (loadingText != null && !string.IsNullOrEmpty(note))
            {
                loadingText.text = note;
            }
            
            // Show overlay
            overlayPanel.SetActive(true);
            
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
            
            // Fade out
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOut());
            
            Debug.Log("LoadingOverlay hidden");
        }

        private System.Collections.IEnumerator SpinCoroutine()
        {
            while (isShowing && spinnerImage != null)
            {
                spinnerImage.transform.Rotate(0, 0, -spinSpeed * Time.deltaTime);
                yield return null;
            }
        }

        private System.Collections.IEnumerator FadeIn()
        {
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            float duration = 1f / fadeSpeed;
            
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
                yield return null;
            }
            
            canvasGroup.alpha = 1f;
        }

        private System.Collections.IEnumerator FadeOut()
        {
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            float duration = 1f / fadeSpeed;
            
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }
            
            canvasGroup.alpha = 0f;
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