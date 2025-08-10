using UnityEngine;
using UnityEngine.UI;
using BlokusUnity.Game;

namespace BlokusUnity.UI
{
    /// <summary>
    /// 개별 스테이지 버튼 컴포넌트
    /// 스테이지 상태 (언락/잠금/완료)에 따라 시각적 표현 변경
    /// </summary>
    public class StageButton : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Button button;
        [SerializeField] private Text stageNumberText;
        [SerializeField] private Text starsText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image lockIcon;
        [SerializeField] private Image[] starIcons; // 3개의 별 아이콘
        
        [Header("상태별 색상")]
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
    if (button == null) button = GetComponent<Button>();
    if (backgroundImage == null) backgroundImage = GetComponent<Image>();

    // Text 또는 TMP_Text 자동 할당
    if (stageNumberText == null) {
        stageNumberText = GetComponentInChildren<Text>();
        if (stageNumberText == null) {
            var tmp = GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null) {
                // TMP만 있는 프리팹 지원: 필요시 래퍼 작성 or 별도 필드 추가
            }
        }
    }

    if (button != null) button.onClick.AddListener(OnButtonClicked);
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
            
            // 스테이지 번호 표시
            if (stageNumberText != null)
            {
                stageNumberText.text = stageNumber.ToString();
            }
            
            // 초기 상태: 잠김
            UpdateState(false, null);
        }
        
        /// <summary>
        /// 버튼 상태 업데이트
        /// </summary>
        /// <param name="unlocked">언락 여부</param>
        /// <param name="progress">진행도 정보 (null이면 플레이하지 않음)</param>
        public void UpdateState(bool unlocked, UserStageProgress progress)
        {
            isUnlocked = unlocked;
            isCompleted = progress?.isCompleted ?? false;
            starsEarned = progress?.starsEarned ?? 0;
            
            // 버튼 활성화/비활성화
            if (button != null)
            {
                button.interactable = isUnlocked;
            }
            
            // 배경색 변경
            UpdateBackgroundColor();
            
            // 별 아이콘 업데이트
            UpdateStarIcons();
            
            // 잠금 아이콘 표시/숨김
            UpdateLockIcon();
            
            // 점수/별 텍스트 업데이트
            UpdateTexts(progress);
        }
        
        /// <summary>
        /// 배경색 업데이트
        /// </summary>
        private void UpdateBackgroundColor()
        {
            if (backgroundImage == null) return;
            
            Color targetColor;
            
            if (!isUnlocked)
            {
                targetColor = lockedColor;
            }
            else if (isCompleted)
            {
                targetColor = starsEarned >= 3 ? perfectColor : completedColor;
            }
            else
            {
                targetColor = unlockedColor;
            }
            
            backgroundImage.color = targetColor;
        }
        
        /// <summary>
        /// 별 아이콘들 업데이트
        /// </summary>
        private void UpdateStarIcons()
        {
            if (starIcons == null || starIcons.Length == 0) return;
            
            for (int i = 0; i < starIcons.Length; i++)
            {
                if (starIcons[i] != null)
                {
                    // 획득한 별 개수만큼 활성화
                    bool shouldShow = isCompleted && (i < starsEarned);
                    starIcons[i].gameObject.SetActive(shouldShow);
                    
                    // 별 색상 조정 (선택사항)
                    if (shouldShow)
                    {
                        starIcons[i].color = Color.yellow;
                    }
                }
            }
        }
        
        /// <summary>
        /// 잠금 아이콘 업데이트
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
        private void UpdateTexts(UserStageProgress progress)
        {
            // 별 개수 텍스트 (별도 Text 컴포넌트가 있는 경우)
            if (starsText != null)
            {
                if (isCompleted)
                {
                    starsText.text = $"{starsEarned}/3 ⭐";
                }
                else if (isUnlocked)
                {
                    starsText.text = ""; // 빈 텍스트
                }
                else
                {
                    starsText.text = "🔒"; // 잠금 이모지
                }
            }
            
            // 스테이지 번호 텍스트 색상 조정
            if (stageNumberText != null)
            {
                stageNumberText.color = isUnlocked ? Color.black : Color.white;
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
            Debug.Log($"스테이지 {stageNumber} 버튼 클릭");
            
            // 간단한 스케일 애니메이션 (예시)
            StartCoroutine(ScaleAnimation());
        }
        
        /// <summary>
        /// 잠김 상태 클릭 효과
        /// </summary>
        private void PlayLockedClickEffect()
        {
            // TODO: 잠김 사운드, 흔들림 애니메이션 등
            Debug.Log($"스테이지 {stageNumber} 잠김 - 클릭 불가");
            
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
        
        // ========================================
        // 개발자 도구 (Inspector에서 테스트용)
        // ========================================
        
        [ContextMenu("Test Unlocked State")]
        public void TestUnlockedState()
        {
            UpdateState(true, null);
        }
        
        [ContextMenu("Test Completed State (3 Stars)")]
        public void TestCompletedState()
        {
            var testProgress = new UserStageProgress
            {
                stageNumber = stageNumber,
                isCompleted = true,
                starsEarned = 3,
                bestScore = 100
            };
            UpdateState(true, testProgress);
        }
        
        [ContextMenu("Test Locked State")]
        public void TestLockedState()
        {
            UpdateState(false, null);
        }
    }
}