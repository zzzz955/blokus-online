using UnityEngine;
namespace Shared.UI{
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

            // Check animator status
            if (animator == null)
            {
                if (debugMode)
                    Debug.Log($"[PanelBase] {gameObject.name}: No Animator found - will use immediate show/hide fallback");
            }
            else
            {
                if (debugMode)
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
        /// Show panel with Show trigger animation or immediate fallback
        /// Migration Plan: 0.2s EaseOut transition or SetActive fallback
        /// </summary>
        public virtual void Show()
        {
            if (debugMode)
                Debug.Log($"[PanelBase] {gameObject.name}: Show() called");

            gameObject.SetActive(true);
            
            if (animator != null && HasAnimatorParameter(ShowTrigger))
            {
                animator.SetTrigger(ShowTrigger);
                if (debugMode)
                    Debug.Log($"[PanelBase] {gameObject.name}: Show animation triggered");
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[PanelBase] {gameObject.name}: Using immediate show (no animator or trigger)");
                // Immediate show fallback - no animation needed
            }
        }

        /// <summary>
        /// Hide panel with Hide trigger animation or immediate fallback
        /// Migration Plan: 0.2s EaseOut transition or SetActive fallback
        /// </summary>
        public virtual void Hide()
        {
            if (debugMode)
                Debug.Log($"[PanelBase] {gameObject.name}: Hide() called");

            if (animator != null && HasAnimatorParameter(HideTrigger))
            {
                animator.SetTrigger(HideTrigger);
                // Note: GameObject deactivation should be handled by animation event
                // or override in child class if immediate deactivation is needed
                if (debugMode)
                    Debug.Log($"[PanelBase] {gameObject.name}: Hide animation triggered");
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[PanelBase] {gameObject.name}: Using immediate hide (no animator or trigger)");
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

        /// <summary>
        /// Check if animator has a specific parameter/trigger
        /// </summary>
        private bool HasAnimatorParameter(string parameterName)
        {
            if (animator == null) return false;
            
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == parameterName)
                    return true;
            }
            return false;
        }
    }
}