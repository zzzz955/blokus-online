using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PanelDebugTest : MonoBehaviour
{
    [Header("디버그 테스트")]
    [SerializeField] private Button forceShowButton;
    [SerializeField] private Button checkCanvasButton;
    
    void Start()
    {
        // 버튼이 없으면 자동 생성
        if (forceShowButton == null)
        {
            CreateDebugButtons();
        }
    }
    
    private void CreateDebugButtons()
    {
        // Force Show 버튼 생성
        var buttonGO1 = new GameObject("ForceShowButton");
        buttonGO1.transform.SetParent(transform, false);
        forceShowButton = buttonGO1.AddComponent<Button>();
        
        var image1 = buttonGO1.AddComponent<Image>();
        image1.color = Color.green;
        
        var text1GO = new GameObject("Text");
        text1GO.transform.SetParent(buttonGO1.transform, false);
        var text1 = text1GO.AddComponent<TextMeshProUGUI>();
        text1.text = "Force Show";
        text1.color = Color.black;
        text1.alignment = TextAlignmentOptions.Center;
        
        var rect1 = buttonGO1.GetComponent<RectTransform>();
        rect1.sizeDelta = new Vector2(200, 50);
        rect1.anchoredPosition = new Vector2(-100, 100);
        
        var textRect1 = text1GO.GetComponent<RectTransform>();
        textRect1.sizeDelta = new Vector2(200, 50);
        textRect1.anchoredPosition = Vector2.zero;
        
        forceShowButton.onClick.AddListener(ForceShowPanel);
        
        // Check Canvas 버튼 생성
        var buttonGO2 = new GameObject("CheckCanvasButton");
        buttonGO2.transform.SetParent(transform, false);
        checkCanvasButton = buttonGO2.AddComponent<Button>();
        
        var image2 = buttonGO2.AddComponent<Image>();
        image2.color = Color.yellow;
        
        var text2GO = new GameObject("Text");
        text2GO.transform.SetParent(buttonGO2.transform, false);
        var text2 = text2GO.AddComponent<TextMeshProUGUI>();
        text2.text = "Check Canvas";
        text2.color = Color.black;
        text2.alignment = TextAlignmentOptions.Center;
        
        var rect2 = buttonGO2.GetComponent<RectTransform>();
        rect2.sizeDelta = new Vector2(200, 50);
        rect2.anchoredPosition = new Vector2(100, 100);
        
        var textRect2 = text2GO.GetComponent<RectTransform>();
        textRect2.sizeDelta = new Vector2(200, 50);
        textRect2.anchoredPosition = Vector2.zero;
        
        checkCanvasButton.onClick.AddListener(CheckCanvasSettings);
        
        Debug.Log("디버그 버튼들 생성 완료");
    }
    
    public void ForceShowPanel()
    {
        Debug.Log("=== Panel 강제 표시 테스트 ===");
        
        // GameObject 상태
        gameObject.SetActive(true);
        Debug.Log($"GameObject Active: {gameObject.activeInHierarchy}");
        
        // CanvasGroup 설정
        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            Debug.Log("CanvasGroup 강제 설정 완료");
        }
        
        // 배경색 추가
        var image = GetComponent<Image>();
        if (image != null)
        {
            image.color = Color.red; // 빨간 배경으로 테스트
            Debug.Log("빨간 배경 설정 완료");
        }
        else
        {
            // Image 컴포넌트 추가
            image = gameObject.AddComponent<Image>();
            image.color = Color.red;
            Debug.Log("Image 컴포넌트 추가 및 빨간 배경 설정 완료");
        }
        
        // 테스트 텍스트 추가
        CreateTestText();
    }
    
    private void CreateTestText()
    {
        var testTextGO = new GameObject("TestText");
        testTextGO.transform.SetParent(transform, false);
        
        var textMesh = testTextGO.AddComponent<TextMeshProUGUI>();
        textMesh.text = "스테이지 선택 패널 테스트";
        textMesh.fontSize = 48;
        textMesh.color = Color.white;
        textMesh.alignment = TextAlignmentOptions.Center;
        
        var rectTransform = testTextGO.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(800, 200);
        rectTransform.anchoredPosition = Vector2.zero;
        
        Debug.Log("테스트 텍스트 생성 완료");
    }
    
    [ContextMenu("Check Canvas Settings")]
    public void CheckCanvasSettings()
    {
        Debug.Log("=== Canvas 설정 확인 ===");
        
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Debug.Log($"Canvas Render Mode: {canvas.renderMode}");
            Debug.Log($"Canvas Sort Order: {canvas.sortingOrder}");
            Debug.Log($"Canvas Enabled: {canvas.enabled}");
            
            var canvasScaler = canvas.GetComponent<CanvasScaler>();
            if (canvasScaler != null)
            {
                Debug.Log($"UI Scale Mode: {canvasScaler.uiScaleMode}");
                Debug.Log($"Reference Resolution: {canvasScaler.referenceResolution}");
            }
        }
        else
        {
            Debug.LogError("Canvas를 찾을 수 없습니다!");
        }
        
        var camera = Camera.main;
        if (camera != null)
        {
            Debug.Log($"Camera Clear Flags: {camera.clearFlags}");
            Debug.Log($"Camera Culling Mask: {camera.cullingMask}");
        }
        else
        {
            Debug.LogError("Main Camera를 찾을 수 없습니다!");
        }
    }
}