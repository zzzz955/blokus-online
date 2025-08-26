using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Shared.UI;
namespace Shared.UI{
    /// <summary>
    /// Toast 메시지 UI 컴포넌트
    /// 자동으로 나타났다가 사라지는 간단한 알림 메시지
    /// </summary>
    public class ToastMessage : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Image iconImage;

        [Header("애니메이션 설정")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private float slideDistance = 50f;

        private MessageData currentMessage;
        private UnityEngine.Coroutine hideCoroutine;
        private RectTransform rectTransform;

        // 이벤트
        public System.Action<ToastMessage> OnToastClosed;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            
            // 컴포넌트 자동 탐색
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            if (messageText == null)
            {
                messageText = GetComponentInChildren<TMP_Text>();
                
                if (messageText == null)
                {
                    Debug.LogWarning($"[ToastMessage] {gameObject.name}: TMP_Text 컴포넌트를 찾을 수 없음, TextMeshProUGUI 생성 시도");
                    
                    // Text 컴포넌트를 위한 자식 오브젝트 찾기 또는 생성
                    Transform textChild = transform.Find("Text");
                    if (textChild == null)
                    {
                        GameObject textObj = new GameObject("Text");
                        textObj.transform.SetParent(transform);
                        textChild = textObj.transform;
                        
                        // RectTransform 설정
                        RectTransform textRect = textChild.gameObject.AddComponent<RectTransform>();
                        textRect.anchorMin = Vector2.zero;
                        textRect.anchorMax = Vector2.one;
                        textRect.offsetMin = Vector2.zero;
                        textRect.offsetMax = Vector2.zero;
                    }
                    
                    messageText = textChild.GetComponent<TMP_Text>();
                    if (messageText == null)
                    {
                        messageText = textChild.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
                        messageText.text = "Toast Message";
                        messageText.fontSize = 14;
                        messageText.color = Color.white;
                        messageText.alignment = TMPro.TextAlignmentOptions.Center;
                        Debug.Log($"[ToastMessage] {gameObject.name}: TextMeshProUGUI 컴포넌트 생성 완료");
                    }
                }
            }

            if (iconImage == null)
            {
                // 아이콘은 선택사항이므로 없어도 됨
                iconImage = transform.Find("Icon")?.GetComponent<Image>();
            }

            // 초기 상태 설정
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Toast 메시지 표시
        /// </summary>
        public void Show(MessageData messageData)
        {
            currentMessage = messageData;

            // UI 업데이트
            UpdateUI();

            // 게임오브젝트 상태 확인
            Debug.Log($"[ToastMessage] GameObject null?: {gameObject == null}");
            Debug.Log($"[ToastMessage] GameObject destroyed?: {gameObject == null}");
            Debug.Log($"[ToastMessage] SetActive(true) 호출 전 - Active: {gameObject.activeSelf}");
            
            // 안전한 활성화 시도
            try 
            {
                gameObject.SetActive(true);
                Debug.Log($"[ToastMessage] SetActive(true) 호출 후 - Active: {gameObject.activeSelf}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ToastMessage] SetActive 실패: {e.Message}");
            }
            
            // 대안: 직접 메시지 표시
            if (!gameObject.activeSelf)
            {
                Debug.LogWarning($"[ToastMessage] GameObject 활성화 실패, 메시지만 표시: {currentMessage.message}");
                // 코루틴 없이 직접 표시
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
                return; // 조기 반환
            }
            
            // 활성화 후 코루틴 시작
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(ShowAnimation());

                // 자동 숨김 스케줄
                if (messageData.duration > 0)
                {
                    if (hideCoroutine != null)
                        StopCoroutine(hideCoroutine);
                    
                    hideCoroutine = StartCoroutine(AutoHide(messageData.duration));
                }
            }
            else
            {
                Debug.LogError("[ToastMessage] GameObject is not active in hierarchy after SetActive(true)!");
                Debug.LogError($"[ToastMessage] Parent chain: {GetParentHierarchy()}");
                Debug.LogError($"[ToastMessage] Transform parent active: {(transform.parent?.gameObject.activeInHierarchy ?? false)}");
                
                // 전체 부모 체인의 활성화 상태 확인
                Transform current = transform;
                while (current != null)
                {
                    Debug.LogError($"[ToastMessage] {current.name} - Active: {current.gameObject.activeSelf}, InHierarchy: {current.gameObject.activeInHierarchy}");
                    current = current.parent;
                }
            }
        }

        /// <summary>
        /// Toast 메시지 숨김
        /// </summary>
        public void Hide()
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
                hideCoroutine = null;
            }

            // GameObject가 활성화되어 있을 때만 코루틴 시작
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(HideAnimation());
            }
            else
            {
                // 이미 비활성화되어 있으면 바로 콜백 호출
                OnToastClosed?.Invoke(this);
            }
        }

        /// <summary>
        /// UI 컴포넌트 업데이트
        /// </summary>
        private void UpdateUI()
        {
            // 메시지 텍스트 설정 - 강화된 방어 코드
            if (messageText != null)
            {
                messageText.text = currentMessage.message;
            }
            else
            {
                Debug.LogWarning($"[ToastMessage] messageText가 null입니다. 메시지: {currentMessage.message}");
                // 다시 한번 찾기 시도
                messageText = GetComponentInChildren<TMP_Text>();
                if (messageText != null)
                {
                    messageText.text = currentMessage.message;
                    Debug.Log($"[ToastMessage] messageText 재발견 성공");
                }
                else
                {
                    Debug.LogError($"[ToastMessage] messageText를 찾을 수 없어서 콘솔에만 출력: {currentMessage.message}");
                }
            }

            // 배경색 설정 (우선순위에 따라)
            if (backgroundImage != null)
            {
                Color bgColor = currentMessage.GetPriorityColor();
                bgColor.a = 0.9f; // 약간 투명
                backgroundImage.color = bgColor;
            }

            // 아이콘 설정
            if (iconImage != null)
            {
                if (currentMessage.icon != null)
                {
                    iconImage.sprite = currentMessage.icon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 표시 애니메이션 - 고정 위치 유지
        /// </summary>
        private System.Collections.IEnumerator ShowAnimation()
        {
            if (canvasGroup == null) yield break;

            // 현재 고정 위치 저장 (위치 변경 안함)
            Vector3 fixedPosition = rectTransform.anchoredPosition;

            // 페이드 인만 수행 (위치는 고정)
            float elapsed = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInDuration;
                
                // Ease out 애니메이션
                t = 1f - (1f - t) * (1f - t);

                canvasGroup.alpha = t;
                // 위치는 고정 유지
                rectTransform.anchoredPosition = fixedPosition;

                yield return null;
            }

            // 최종 상태 설정
            canvasGroup.alpha = 1f;
            rectTransform.anchoredPosition = fixedPosition;
        }

        /// <summary>
        /// 숨김 애니메이션 - 고정 위치 유지
        /// </summary>
        private System.Collections.IEnumerator HideAnimation()
        {
            if (canvasGroup == null) yield break;

            // 현재 고정 위치 저장 (위치 변경 안함)
            Vector3 fixedPosition = rectTransform.anchoredPosition;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;
                
                // Ease in 애니메이션
                t = t * t;

                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                // 위치는 고정 유지
                rectTransform.anchoredPosition = fixedPosition;

                yield return null;
            }

            // 최종 상태
            canvasGroup.alpha = 0f;
            rectTransform.anchoredPosition = fixedPosition; // 위치 복원
            gameObject.SetActive(false);

            // 이벤트 호출
            OnToastClosed?.Invoke(this);
        }

        /// <summary>
        /// 자동 숨김
        /// </summary>
        private System.Collections.IEnumerator AutoHide(float delay)
        {
            yield return new WaitForSeconds(delay);
            Hide();
        }

        /// <summary>
        /// 수동 클릭으로 닫기 (선택사항)
        /// </summary>
        public void OnToastClicked()
        {
            Hide();
        }

        /// <summary>
        /// 부모 계층 구조 디버그 정보
        /// </summary>
        private string GetParentHierarchy()
        {
            string hierarchy = gameObject.name;
            Transform current = transform.parent;
            
            while (current != null)
            {
                hierarchy = current.name + "/" + hierarchy;
                current = current.parent;
            }
            
            return hierarchy;
        }

        void OnDestroy()
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }
        }
    }
}