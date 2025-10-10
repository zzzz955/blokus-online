using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Features.Single.Core;
namespace Features.Single.UI.StageSelect{
    /// <summary>
    /// 개별 스테이지 버튼 컴포넌트
    /// 스테이지 상태 (언락/잠금/완료)에 따라 시각적 표현 변경
    /// </summary>
    public class StageButton : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI stageNumberText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image lockIcon;
        [SerializeField] private Image[] starIcons; // 3개의 별 아이콘 (프리팹에서 할당)
        
        [Header("별 스프라이트")]
        [SerializeField] private Sprite activeStar; // 활성화된 별 이미지
        [SerializeField] private Sprite inactiveStar; // 비활성화된 별 이미지
        
        [Header("별 색상 설정 (Fallback)")]
        [SerializeField] private Color activeStarColor = Color.yellow;
        [SerializeField] private Color inactiveStarColor = Color.gray;
        
        [Header("상태별 스프라이트")]
        [SerializeField] private Sprite unlockedSprite; // 해금 상태 이미지
        [SerializeField] private Sprite lockedSprite; // 잠김 상태 이미지
        [SerializeField] private Sprite completedSprite; // 완료 상태 이미지
        [SerializeField] private Sprite perfectSprite; // 3별 완료 상태 이미지
        
        [Header("상태별 색상 (Fallback)")]
        [SerializeField] private Color unlockedColor = Color.white;
        [SerializeField] private Color completedColor = Color.green;
        [SerializeField] private Color lockedColor = Color.gray;
        [SerializeField] private Color perfectColor = Color.yellow; // 3별 완료시
        
        // 스테이지 정보
        private int stageNumber;
        private System.Action<int> onClickCallback;
        
        // 상태 정보
        private bool isUnlocked;
        private bool isCompleted;
        private int starsEarned;
        
        public int StageNumber => stageNumber;
        
void Awake()
{
    if (button != null)
    {
        button.onClick.AddListener(OnButtonClicked);
        // // debug.Log("Button onClick 리스너 등록 완료");
    }
    else
    {
        // debug.LogError($"StageButton {gameObject.name}: Button 컴포넌트가 할당되지 않았습니다!");
    }
    
    // stageNumberText 기본값 설정 (초기 표시용)
    if (stageNumberText != null)
    {
        if (stageNumberText.text == "New Text" || string.IsNullOrEmpty(stageNumberText.text))
        {
            stageNumberText.text = "?";
            // // debug.Log("stageNumberText 기본값 설정: ?");
        }
    }
}

        
        /// <summary>
        /// 스테이지 버튼 초기화
        /// </summary>
        /// <param name="stageNum">스테이지 번호</param>
        /// <param name="clickCallback">클릭시 호출될 콜백</param>
        public void Initialize(int stageNum, System.Action<int> clickCallback)
        {            
            stageNumber = stageNum;
            onClickCallback = clickCallback;
            
            // 스테이지 번호 표시 (null 체크)
            if (stageNumberText != null)
            {
                string newText = stageNumber.ToString();
                stageNumberText.text = newText;
            }
        }
        
        /// <summary>
        /// 버튼 상태 업데이트
        /// </summary>
        /// <param name="unlocked">언락 여부</param>
        /// <param name="progress">진행도 정보 (null이면 플레이하지 않음)</param>
        public void UpdateState(bool unlocked, Features.Single.Core.UserStageProgress progress)
        {
            isUnlocked = unlocked;
            isCompleted = progress?.isCompleted ?? false;
            starsEarned = progress?.starsEarned ?? 0;
            
            // 버튼 활성화/비활성화
            if (button != null)
            {
                button.interactable = isUnlocked;
            }
            
            // 배경 이미지 변경
            UpdateBackgroundImage();
            
            // 별 아이콘 업데이트
            UpdateStarIcons();
            
            // 잠금 아이콘 표시/숨김
            UpdateLockIcon();
            
            // 점수/별 텍스트 업데이트
            UpdateTexts(progress);
        }
        
        /// <summary>
        /// 배경 이미지 업데이트 (스프라이트 우선, 색상 폴백)
        /// </summary>
        private void UpdateBackgroundImage()
        {
            if (backgroundImage == null) 
            {
                // debug.LogWarning($"StageButton {stageNumber}: backgroundImage가 할당되지 않았습니다!");
                return;
            }
            
            Sprite targetSprite = null;
            Color targetColor = Color.white;
            
            // 상태에 따른 스프라이트 선택
            if (!isUnlocked)
            {
                targetSprite = lockedSprite;
                targetColor = lockedColor;
            }
            else if (isCompleted)
            {
                if (starsEarned >= 3)
                {
                    targetSprite = perfectSprite;
                    targetColor = perfectColor;
                }
                else
                {
                    targetSprite = completedSprite;
                    targetColor = completedColor;
                }
            }
            else
            {
                targetSprite = unlockedSprite;
                targetColor = unlockedColor;
            }
            
            // 스프라이트가 있으면 스프라이트 사용, 없으면 색상만 변경
            if (targetSprite != null)
            {
                backgroundImage.sprite = targetSprite;
                backgroundImage.color = Color.white; // 스프라이트 사용시 색상 취소
                // debug.Log($"스테이지 {stageNumber}: 스프라이트 설정 - {targetSprite.name}");
            }
            else
            {
                // 스프라이트가 없으면 색상만 변경 (Fallback)
                backgroundImage.color = targetColor;
                // debug.Log($"스테이지 {stageNumber}: 색상 설정 - {targetColor}");
            }
        }
        
        /// <summary>
        /// 별 아이콘들 업데이트 (스프라이트 우선, 색상 폴백)
        /// </summary>
        private void UpdateStarIcons()
        {
            if (starIcons == null || starIcons.Length == 0)
            {
                Debug.LogWarning($"[StageButton {stageNumber}] starIcons 배열이 비어있습니다!");
                return;
            }

            // 스테이지가 잠겨있으면 모든 별 아이콘을 비활성화
            if (!isUnlocked)
            {
                for (int i = 0; i < starIcons.Length; i++)
                {
                    if (starIcons[i] != null)
                    {
                        starIcons[i].gameObject.SetActive(false);
                    }
                }
                return;
            }

            // 언락된 스테이지의 경우 별 아이콘 상태 업데이트
            for (int i = 0; i < starIcons.Length; i++)
            {
                if (starIcons[i] != null)
                {
                    // 모든 별 아이콘을 표시 (활성화/비활성화 상태로)
                    starIcons[i].gameObject.SetActive(true);

                    // 획득한 별 개수에 따른 스프라이트 및 색상 설정
                    bool shouldActivate = isCompleted && (i < starsEarned);

                    if (shouldActivate)
                    {
                        // 활성화된 별
                        if (activeStar != null)
                        {
                            starIcons[i].sprite = activeStar;
                            starIcons[i].color = Color.white; // 스프라이트 사용시 색상 취소
                        }
                        else
                        {
                            // 스프라이트가 없으면 색상만 변경
                            starIcons[i].color = activeStarColor;
                        }
                    }
                    else
                    {
                        // 비활성화된 별
                        if (inactiveStar != null)
                        {
                            starIcons[i].sprite = inactiveStar;
                            starIcons[i].color = Color.white; // 스프라이트 사용시 색상 취소
                        }
                        else
                        {
                            // 스프라이트가 없으면 색상만 변경
                            starIcons[i].color = inactiveStarColor;
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[StageButton {stageNumber}] starIcons[{i}]가 null입니다!");
                }
            }
        }
        
        /// <summary>
        /// 잠금 아이콘 업데이트 (선택적 사용)
        /// </summary>
        private void UpdateLockIcon()
        {
            if (lockIcon != null)
            {
                lockIcon.gameObject.SetActive(!isUnlocked);
            }
        }
        
        /// <summary>
        /// 텍스트 정보 업데이트
        /// </summary>
        private void UpdateTexts(Features.Single.Core.UserStageProgress progress)
        {
            // 스테이지 번호 텍스트 색상 조정
            if (stageNumberText != null)
            {
                stageNumberText.color = isUnlocked ? Color.black : Color.white;
                // debug.Log($"스테이지 {stageNumber}: 텍스트 색상 설정 - {(isUnlocked ? "Black (Unlocked)" : "White (Locked)")}");
            }
        }
        
        /// <summary>
        /// 버튼 클릭 처리
        /// </summary>
        private void OnButtonClicked()
        {
            if (isUnlocked)
            {
                // 클릭 효과 (선택사항)
                PlayClickEffect();
                
                // 콜백 호출
                onClickCallback?.Invoke(stageNumber);
            }
            else
            {
                // 잠김 상태 클릭 효과 (선택사항)
                PlayLockedClickEffect();
            }
        }
        
        // ========================================
        // 시각적 효과들 (기본 구현)
        // ========================================
        
        /// <summary>
        /// 클릭 효과
        /// </summary>
        private void PlayClickEffect()
        {
            // TODO: 사운드 재생, 스케일 애니메이션 등
            // debug.Log($"스테이지 {stageNumber} 버튼 클릭");
            
            // 간단한 스케일 애니메이션 (예시)
            StartCoroutine(ScaleAnimation());
        }
        
        /// <summary>
        /// 잠김 상태 클릭 효과
        /// </summary>
        private void PlayLockedClickEffect()
        {
            // TODO: 잠김 사운드, 흔들림 애니메이션 등
            // debug.Log($"스테이지 {stageNumber} 잠김 - 클릭 불가");
            
            // 간단한 흔들림 애니메이션 (예시)
            StartCoroutine(ShakeAnimation());
        }
        
        /// <summary>
        /// 스케일 애니메이션 (클릭시)
        /// </summary>
        private System.Collections.IEnumerator ScaleAnimation()
        {
            Vector3 originalScale = transform.localScale;
            Vector3 targetScale = originalScale * 1.1f;
            
            // 확대
            float elapsedTime = 0f;
            float duration = 0.1f;
            
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
                yield return null;
            }
            
            // 축소
            elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
                yield return null;
            }
            
            transform.localScale = originalScale;
        }
        
        /// <summary>
        /// 흔들림 애니메이션 (잠김 클릭시)
        /// </summary>
        private System.Collections.IEnumerator ShakeAnimation()
        {
            Vector3 originalPosition = transform.localPosition;
            float shakeIntensity = 5f;
            float duration = 0.3f;
            float elapsedTime = 0f;
            
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float strength = Mathf.Lerp(shakeIntensity, 0f, elapsedTime / duration);
                
                Vector3 randomOffset = new Vector3(
                    Random.Range(-strength, strength),
                    Random.Range(-strength, strength),
                    0f
                );
                
                transform.localPosition = originalPosition + randomOffset;
                yield return null;
            }
            
            transform.localPosition = originalPosition;
        }
    }
}