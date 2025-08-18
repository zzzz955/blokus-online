using UnityEngine;

namespace BlokusUnity.UI
{
    /// <summary>
    /// Base implementation for UI panels with Animator-based transitions
    /// Migration Plan: PanelBase는 Animator와 트리거(Show, Hide)를 강제
    /// 기본 전환 시간 0.2s, EaseOut
    /// </summary>
    public abstract class PanelBase : MonoBehaviour, IPanel
    {
        [Header("Panel Animation")]
        [SerializeField] protected Animator animator;
        
        [Header("Debug")]
        [SerializeField] protected bool debugMode = false;

        // Animation trigger names - standard for all panels
        private const string ShowTrigger = "Show";
        private const string HideTrigger = "Hide";

        protected virtual void Awake()
        {
            // Auto-find animator if not assigned
            if (animator == null)
                animator = GetComponent<Animator>();

            // Validate animator exists
            if (animator == null)
            {
                Debug.LogError($"[PanelBase] {gameObject.name}: Animator component is required but not found!", this);
            }
            else if (debugMode)
            {
                Debug.Log($"[PanelBase] {gameObject.name}: Animator found and initialized");
            }
        }

        protected virtual void Start()
        {
            // Ensure panel starts in correct state
            // Default to hidden unless explicitly shown
            if (!IsVisible)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Show panel with Show trigger animation
        /// Migration Plan: 0.2s EaseOut transition
        /// </summary>
        public virtual void Show()
        {
            if (debugMode)
                Debug.Log($"[PanelBase] {gameObject.name}: Show() called");

            gameObject.SetActive(true);
            
            if (animator != null)
            {
                animator.SetTrigger(ShowTrigger);
            }
            else
            {
                Debug.LogWarning($"[PanelBase] {gameObject.name}: No animator found for Show animation");
            }
        }

        /// <summary>
        /// Hide panel with Hide trigger animation
        /// Migration Plan: 0.2s EaseOut transition
        /// </summary>
        public virtual void Hide()
        {
            if (debugMode)
                Debug.Log($"[PanelBase] {gameObject.name}: Hide() called");

            if (animator != null)
            {
                animator.SetTrigger(HideTrigger);
                // Note: GameObject deactivation should be handled by animation event
                // or override in child class if immediate deactivation is needed
            }
            else
            {
                Debug.LogWarning($"[PanelBase] {gameObject.name}: No animator found for Hide animation");
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Check if panel is currently visible
        /// Migration Plan: 수락 기준 - 임의 패널 프리팹에 PanelBase 붙이면 추가 코드 없이 Show/Hide 동작
        /// </summary>
        public virtual bool IsVisible
        {
            get { return gameObject.activeInHierarchy; }
        }

        /// <summary>
        /// Animation event callback for when hide animation completes
        /// Call this from Animation Events to deactivate GameObject
        /// </summary>
        public virtual void OnHideAnimationComplete()
        {
            if (debugMode)
                Debug.Log($"[PanelBase] {gameObject.name}: Hide animation complete, deactivating GameObject");
                
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Animation event callback for when show animation completes
        /// Override in child classes if needed
        /// </summary>
        public virtual void OnShowAnimationComplete()
        {
            if (debugMode)
                Debug.Log($"[PanelBase] {gameObject.name}: Show animation complete");
        }

        /// <summary>
        /// Force immediate show without animation
        /// </summary>
        public virtual void ShowImmediate()
        {
            if (debugMode)
                Debug.Log($"[PanelBase] {gameObject.name}: ShowImmediate() called");

            gameObject.SetActive(true);
            
            if (animator != null)
            {
                // Force animator to Show state immediately
                animator.Play("Show", 0, 1.0f);
            }
        }

        /// <summary>
        /// Force immediate hide without animation
        /// </summary>
        public virtual void HideImmediate()
        {
            if (debugMode)
                Debug.Log($"[PanelBase] {gameObject.name}: HideImmediate() called");

            gameObject.SetActive(false);
        }
    }
}