using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
namespace App.UI{
    /// <summary>
    /// Input locker to disable UI interactions during loading
    /// Migration Plan: InputLocker.Enable() / Disable() → EventSystem 및 GraphicRaycaster 비활성
    /// </summary>
    public class InputLocker : MonoBehaviour
    {
        // Singleton pattern
        public static InputLocker Instance { get; private set; }
        
        // State tracking
        private bool isLocked = false;
        private List<GraphicRaycaster> disabledRaycasters = new List<GraphicRaycaster>();
        private EventSystem eventSystem;
        
        void Awake()
        {
            // Singleton pattern with DontDestroyOnLoad
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                
                Initialize();
                Debug.Log("InputLocker initialized with DontDestroyOnLoad");
            }
            else
            {
                Debug.Log("InputLocker duplicate instance destroyed");
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            // Find EventSystem
            eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogWarning("InputLocker: No EventSystem found in scene");
            }
        }

        /// <summary>
        /// Enable input locking - disable EventSystem and all GraphicRaycasters
        /// Migration Plan: 로딩 중 UI 입력 완전 차단
        /// </summary>
        public static void Enable()
        {
            if (Instance == null)
            {
                Debug.LogError("InputLocker.Enable() called but Instance is null!");
                return;
            }
            
            Instance.EnableInternal();
        }

        /// <summary>
        /// Disable input locking - restore EventSystem and GraphicRaycasters
        /// Migration Plan: 전환 전후 깜빡임/입력 누수 없음
        /// </summary>
        public static void Disable()
        {
            if (Instance == null)
            {
                Debug.LogWarning("InputLocker.Disable() called but Instance is null");
                return;
            }
            
            Instance.DisableInternal();
        }

        private void EnableInternal()
        {
            if (isLocked) return;
            
            isLocked = true;
            disabledRaycasters.Clear();
            
            // Disable EventSystem
            if (eventSystem != null)
            {
                eventSystem.enabled = false;
                Debug.Log("InputLocker: EventSystem disabled");
            }
            else
            {
                // Try to find EventSystem again if it wasn't found before
                eventSystem = FindObjectOfType<EventSystem>();
                if (eventSystem != null)
                {
                    eventSystem.enabled = false;
                    Debug.Log("InputLocker: EventSystem found and disabled");
                }
            }
            
            // Disable all active GraphicRaycasters
            GraphicRaycaster[] raycasters = FindObjectsOfType<GraphicRaycaster>();
            foreach (GraphicRaycaster raycaster in raycasters)
            {
                if (raycaster.enabled)
                {
                    raycaster.enabled = false;
                    disabledRaycasters.Add(raycaster);
                }
            }
            
            Debug.Log($"InputLocker: Disabled {disabledRaycasters.Count} GraphicRaycasters");
        }

        private void DisableInternal()
        {
            if (!isLocked) return;
            
            isLocked = false;
            
            // Re-enable EventSystem
            if (eventSystem != null)
            {
                eventSystem.enabled = true;
                Debug.Log("InputLocker: EventSystem re-enabled");
            }
            else
            {
                // Try to find EventSystem if it's null
                eventSystem = FindObjectOfType<EventSystem>();
                if (eventSystem != null)
                {
                    eventSystem.enabled = true;
                    Debug.Log("InputLocker: EventSystem found and re-enabled");
                }
            }
            
            // Re-enable previously disabled GraphicRaycasters
            foreach (GraphicRaycaster raycaster in disabledRaycasters)
            {
                if (raycaster != null) // Check for null in case object was destroyed
                {
                    raycaster.enabled = true;
                }
            }
            
            Debug.Log($"InputLocker: Re-enabled {disabledRaycasters.Count} GraphicRaycasters");
            disabledRaycasters.Clear();
        }

        /// <summary>
        /// Check if input is currently locked
        /// </summary>
        public static bool IsLocked
        {
            get { return Instance != null && Instance.isLocked; }
        }

        /// <summary>
        /// Force refresh EventSystem reference (call if EventSystem changes)
        /// </summary>
        public static void RefreshEventSystem()
        {
            if (Instance != null)
            {
                Instance.eventSystem = FindObjectOfType<EventSystem>();
                Debug.Log($"InputLocker: EventSystem refreshed - {(Instance.eventSystem != null ? "Found" : "Not found")}");
            }
        }

        void OnDestroy()
        {
            // Ensure input is unlocked when destroyed
            if (isLocked)
            {
                DisableInternal();
            }
        }
    }
}