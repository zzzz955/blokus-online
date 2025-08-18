using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.UI.Messages;

namespace BlokusUnity.UI.Messages
{
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
                messageText = GetComponentInChildren<TMP_Text>();

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

            // 게임오브젝트 활성화 및 애니메이션
            gameObject.SetActive(true);
            StartCoroutine(ShowAnimation());

            // 자동 숨김 스케줄
            if (messageData.duration > 0)
            {
                if (hideCoroutine != null)
                    StopCoroutine(hideCoroutine);
                
                hideCoroutine = StartCoroutine(AutoHide(messageData.duration));
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

            StartCoroutine(HideAnimation());
        }

        /// <summary>
        /// UI 컴포넌트 업데이트
        /// </summary>
        private void UpdateUI()
        {
            // 메시지 텍스트 설정
            if (messageText != null)
            {
                messageText.text = currentMessage.message;
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
        /// 표시 애니메이션
        /// </summary>
        private System.Collections.IEnumerator ShowAnimation()
        {
            if (canvasGroup == null) yield break;

            // 시작 위치 설정 (아래에서 위로 슬라이드)
            Vector3 startPos = rectTransform.anchoredPosition;
            Vector3 targetPos = startPos;
            startPos.y -= slideDistance;
            rectTransform.anchoredPosition = startPos;

            // 페이드 인 + 슬라이드 업
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
                rectTransform.anchoredPosition = Vector3.Lerp(startPos, targetPos, t);

                yield return null;
            }

            // 최종 상태 설정
            canvasGroup.alpha = 1f;
            rectTransform.anchoredPosition = targetPos;
        }

        /// <summary>
        /// 숨김 애니메이션
        /// </summary>
        private System.Collections.IEnumerator HideAnimation()
        {
            if (canvasGroup == null) yield break;

            Vector3 startPos = rectTransform.anchoredPosition;
            Vector3 targetPos = startPos;
            targetPos.y += slideDistance; // 위로 슬라이드 아웃

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
                rectTransform.anchoredPosition = Vector3.Lerp(startPos, targetPos, t);

                yield return null;
            }

            // 최종 상태
            canvasGroup.alpha = 0f;
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

        void OnDestroy()
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }
        }
    }
}