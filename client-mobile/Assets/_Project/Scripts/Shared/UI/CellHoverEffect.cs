using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    /// <summary>
    /// 셀 호버링 시각 효과를 담당하는 컴포넌트
    /// 기존 색상 틴트 방식을 대체하여 테두리 + 아이콘 + 애니메이션으로 시각화
    /// </summary>
    public class CellHoverEffect : MonoBehaviour
    {
        public enum HoverState
        {
            None,           // 호버 없음
            Placeable,      // 배치 가능
            Occupied,       // 이미 블록 있음
            RuleViolation   // 게임 규칙 위반
        }

        [Header("시각 효과 컴포넌트")]
        [SerializeField] private GameObject hoverContainer;      // 호버 효과 전체 컨테이너
        [SerializeField] private Image borderImage;             // 테두리 이미지
        [SerializeField] private Image iconImage;               // 중앙 아이콘 이미지

        [Header("상태별 색상")]
        [SerializeField] private Color placeableColor = new Color(0.2f, 0.8f, 0.2f, 1f);    // 초록색
        [SerializeField] private Color occupiedColor = new Color(1f, 0.6f, 0.2f, 1f);       // 주황색
        [SerializeField] private Color violationColor = new Color(0.9f, 0.2f, 0.2f, 1f);    // 빨간색

        [Header("아이콘 스프라이트")]
        [SerializeField] private Sprite checkIcon;              // ✓ 체크마크
        [SerializeField] private Sprite crossIcon;              // ✗ X 표시
        [SerializeField] private Sprite blockedIcon;            // 🚫 금지 표시

        [Header("애니메이션 설정")]
        [SerializeField] private float pulseScale = 1.1f;       // 펄스 효과 크기
        [SerializeField] private float pulseDuration = 0.8f;    // 펄스 주기 (초)
        [SerializeField] private float blinkDuration = 0.5f;    // 깜빡임 주기 (초)

        // 내부 상태
        private HoverState currentState = HoverState.None;
        private Coroutine currentAnimation;
        private bool isAnimating = false;

        private void Awake()
        {
            // 초기화 시 호버 효과를 숨김
            if (hoverContainer != null)
            {
                hoverContainer.SetActive(false);
            }

            // 컴포넌트 자동 연결
            AutoConnectComponents();
        }

        /// <summary>
        /// 컴포넌트 자동 연결 (Inspector에서 연결되지 않은 경우)
        /// </summary>
        private void AutoConnectComponents()
        {
            // 스프라이트가 설정되지 않은 경우 런타임 생성
            if (checkIcon == null)
                checkIcon = IconSpriteGenerator.CreateCheckIcon();
            if (crossIcon == null)
                crossIcon = IconSpriteGenerator.CreateCrossIcon();
            if (blockedIcon == null)
                blockedIcon = IconSpriteGenerator.CreateBlockedIcon();
            // HoverContainer 자동 생성
            if (hoverContainer == null)
            {
                hoverContainer = new GameObject("HoverContainer");
                hoverContainer.transform.SetParent(transform, false);

                var rectTransform = hoverContainer.AddComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localScale = Vector3.one;
            }

            // BorderImage 자동 생성
            if (borderImage == null)
            {
                var borderObj = new GameObject("Border");
                borderObj.transform.SetParent(hoverContainer.transform, false);

                borderImage = borderObj.AddComponent<Image>();
                var borderRect = borderObj.GetComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = Vector2.zero;
                borderRect.offsetMax = Vector2.zero;
                borderRect.localScale = Vector3.one;

                // 기본 테두리 스프라이트 설정
                borderImage.sprite = IconSpriteGenerator.CreateBorderSprite();
                borderImage.color = Color.clear;
                borderImage.raycastTarget = false;
            }

            // IconImage 자동 생성
            if (iconImage == null)
            {
                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(hoverContainer.transform, false);

                iconImage = iconObj.AddComponent<Image>();
                var iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(12f, 12f); // 작은 아이콘 크기
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.localScale = Vector3.one;

                // 기본 아이콘 설정
                iconImage.color = Color.white;
                iconImage.raycastTarget = false;
                iconImage.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 호버 상태 설정
        /// </summary>
        /// <param name="state">새로운 호버 상태</param>
        public void SetHoverState(HoverState state)
        {
            if (currentState == state) return;

            currentState = state;

            // 이전 애니메이션 정지
            StopCurrentAnimation();

            switch (state)
            {
                case HoverState.None:
                    HideHoverEffect();
                    break;
                case HoverState.Placeable:
                    ShowPlaceableEffect();
                    break;
                case HoverState.Occupied:
                    ShowOccupiedEffect();
                    break;
                case HoverState.RuleViolation:
                    ShowViolationEffect();
                    break;
            }
        }

        /// <summary>
        /// 호버 효과 숨김
        /// </summary>
        private void HideHoverEffect()
        {
            if (hoverContainer != null)
            {
                hoverContainer.SetActive(false);
            }
        }

        /// <summary>
        /// 배치 가능 상태 효과 (초록 테두리 + 체크마크 + 펄스)
        /// </summary>
        private void ShowPlaceableEffect()
        {
            ShowHoverEffect(placeableColor, checkIcon);
            StartPulseAnimation();
        }

        /// <summary>
        /// 블록 점유 상태 효과 (주황 테두리 + 금지 아이콘 + 페이드)
        /// </summary>
        private void ShowOccupiedEffect()
        {
            ShowHoverEffect(occupiedColor, blockedIcon);
            StartFadeAnimation();
        }

        /// <summary>
        /// 규칙 위반 상태 효과 (빨간 테두리 + X 아이콘 + 깜빡임)
        /// </summary>
        private void ShowViolationEffect()
        {
            ShowHoverEffect(violationColor, crossIcon);
            StartBlinkAnimation();
        }

        /// <summary>
        /// 호버 효과 기본 표시
        /// </summary>
        /// <param name="color">테두리 색상</param>
        /// <param name="icon">아이콘 스프라이트</param>
        private void ShowHoverEffect(Color color, Sprite icon)
        {
            if (hoverContainer != null)
            {
                hoverContainer.SetActive(true);
            }

            // 테두리 설정
            if (borderImage != null)
            {
                borderImage.color = color;
            }

            // 아이콘 설정
            if (iconImage != null && icon != null)
            {
                iconImage.sprite = icon;
                iconImage.color = color;
                iconImage.gameObject.SetActive(true);
            }
            else if (iconImage != null)
            {
                iconImage.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 펄스 애니메이션 시작 (배치 가능)
        /// </summary>
        private void StartPulseAnimation()
        {
            if (hoverContainer == null) return;

            isAnimating = true;
            currentAnimation = StartCoroutine(PulseAnimationCoroutine());
        }

        /// <summary>
        /// 깜빡임 애니메이션 시작 (규칙 위반)
        /// </summary>
        private void StartBlinkAnimation()
        {
            if (borderImage == null) return;

            isAnimating = true;
            currentAnimation = StartCoroutine(BlinkAnimationCoroutine());
        }

        /// <summary>
        /// 페이드 애니메이션 시작 (블록 점유)
        /// </summary>
        private void StartFadeAnimation()
        {
            if (borderImage == null) return;

            isAnimating = true;
            currentAnimation = StartCoroutine(FadeAnimationCoroutine());
        }

        /// <summary>
        /// 펄스 애니메이션 코루틴
        /// </summary>
        private IEnumerator PulseAnimationCoroutine()
        {
            Vector3 originalScale = Vector3.one;
            Vector3 targetScale = Vector3.one * pulseScale;
            float halfDuration = pulseDuration * 0.5f;

            while (isAnimating)
            {
                // 확대
                float elapsed = 0f;
                while (elapsed < halfDuration && isAnimating)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / halfDuration;
                    t = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease in-out curve
                    hoverContainer.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
                    yield return null;
                }

                // 축소
                elapsed = 0f;
                while (elapsed < halfDuration && isAnimating)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / halfDuration;
                    t = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease in-out curve
                    hoverContainer.transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// 깜빡임 애니메이션 코루틴
        /// </summary>
        private IEnumerator BlinkAnimationCoroutine()
        {
            Color originalColor = borderImage.color;
            Color fadeColor = originalColor;
            fadeColor.a = 0.3f;
            float halfDuration = blinkDuration * 0.5f;

            while (isAnimating)
            {
                // 페이드 아웃
                float elapsed = 0f;
                while (elapsed < halfDuration && isAnimating)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / halfDuration;
                    borderImage.color = Color.Lerp(originalColor, fadeColor, t);
                    yield return null;
                }

                // 페이드 인
                elapsed = 0f;
                while (elapsed < halfDuration && isAnimating)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / halfDuration;
                    borderImage.color = Color.Lerp(fadeColor, originalColor, t);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// 페이드 애니메이션 코루틴
        /// </summary>
        private IEnumerator FadeAnimationCoroutine()
        {
            Color originalColor = borderImage.color;
            Color fadeColor = originalColor;
            fadeColor.a = 0.5f;
            float duration = pulseDuration * 0.7f;

            while (isAnimating)
            {
                // 페이드 아웃
                float elapsed = 0f;
                while (elapsed < duration && isAnimating)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    borderImage.color = Color.Lerp(originalColor, fadeColor, t);
                    yield return null;
                }

                // 페이드 인
                elapsed = 0f;
                while (elapsed < duration && isAnimating)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    borderImage.color = Color.Lerp(fadeColor, originalColor, t);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// 현재 애니메이션 정지
        /// </summary>
        private void StopCurrentAnimation()
        {
            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
                currentAnimation = null;
            }

            isAnimating = false;

            // 스케일과 알파값 초기화
            if (hoverContainer != null)
            {
                hoverContainer.transform.localScale = Vector3.one;
            }

            if (borderImage != null)
            {
                var color = borderImage.color;
                color.a = 1f;
                borderImage.color = color;
            }
        }

        /// <summary>
        /// 현재 호버 상태 반환
        /// </summary>
        public HoverState GetCurrentState()
        {
            return currentState;
        }

        /// <summary>
        /// 애니메이션 중인지 확인
        /// </summary>
        public bool IsAnimating()
        {
            return isAnimating;
        }

        private void OnDestroy()
        {
            StopCurrentAnimation();
        }

        // Inspector에서 테스트용
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            if (Application.isPlaying && currentState != HoverState.None)
            {
                SetHoverState(currentState);
            }
        }
    }
}