using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace App.Audio
{
    /// <summary>
    /// 전역 버튼 사운드 자동 적용 시스템
    /// 씬의 모든 Button에 Hover/Click 사운드를 자동으로 추가
    /// 모달 Open/Close 버튼은 예외 처리하여 중복 재생 방지
    /// </summary>
    public class GlobalButtonSoundPlayer : MonoBehaviour
    {
        // Singleton
        public static GlobalButtonSoundPlayer Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private bool autoDetectButtons = true;
        [Tooltip("씬 로드 시 자동으로 버튼 탐지 및 사운드 추가")]

        [SerializeField] private bool verboseLog = false;
        [Tooltip("디버그 로그 출력")]

        [Header("스마트 감지")]
        [SerializeField] private bool autoDetectModalButtons = true;
        [Tooltip("버튼의 부모 계층에서 ModalSoundPlayer를 찾아 자동으로 제외")]

        // 등록된 버튼 추적
        private HashSet<Button> registeredButtons = new HashSet<Button>();

        // ========================================
        // Unity Lifecycle
        // ========================================

        void Awake()
        {
            // Singleton 패턴
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);

                if (verboseLog)
                    Debug.Log("[GlobalButtonSoundPlayer] 초기화 완료 (DontDestroyOnLoad)");
            }
            else
            {
                if (verboseLog)
                    Debug.Log("[GlobalButtonSoundPlayer] 중복 인스턴스 제거");
                Destroy(gameObject);
            }
        }

        void Start()
        {
            if (autoDetectButtons)
            {
                DetectAndRegisterAllButtons();
            }

            // 씬 로드 이벤트 구독
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        // ========================================
        // Scene Management
        // ========================================

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (autoDetectButtons)
            {
                // 씬이 로드되면 새 버튼들을 탐지
                if (verboseLog)
                    Debug.Log($"[GlobalButtonSoundPlayer] 새 씬 로드됨: {scene.name}, 버튼 재탐지");

                // 등록 목록 초기화 (이전 씬의 버튼들은 파괴됨)
                registeredButtons.Clear();

                // 새 버튼들 탐지
                DetectAndRegisterAllButtons();
            }
        }

        // ========================================
        // Button Detection & Registration
        // ========================================

        /// <summary>
        /// 현재 씬의 모든 버튼을 찾아서 사운드 추가
        /// </summary>
        private void DetectAndRegisterAllButtons()
        {
            // 모든 Button 컴포넌트 찾기 (비활성화된 것 포함)
            Button[] allButtons = FindObjectsOfType<Button>(true);

            int registered = 0;
            foreach (var button in allButtons)
            {
                if (RegisterButton(button))
                {
                    registered++;
                }
            }

            if (verboseLog)
                Debug.Log($"[GlobalButtonSoundPlayer] {registered}개 버튼에 사운드 추가 완료");
        }

        /// <summary>
        /// 특정 버튼에 사운드 추가 (Public API)
        /// 동적으로 생성된 버튼에 사용
        /// </summary>
        public bool RegisterButton(Button button)
        {
            if (button == null)
                return false;

            // 이미 등록된 버튼은 스킵
            if (registeredButtons.Contains(button))
                return false;

            // 스마트 감지: 모달 버튼 자동 제외
            if (autoDetectModalButtons && IsModalButton(button))
            {
                if (verboseLog)
                    Debug.Log($"[GlobalButtonSoundPlayer] 모달 버튼 자동 제외: {button.name}");
                return false;
            }

            // EventTrigger가 이미 있으면 수동으로 설정된 것으로 간주하고 스킵
            var existingTrigger = button.GetComponent<EventTrigger>();
            if (existingTrigger != null && HasPointerEvents(existingTrigger))
            {
                if (verboseLog)
                    Debug.Log($"[GlobalButtonSoundPlayer] 버튼 스킵 (이미 EventTrigger 존재): {button.name}");
                return false;
            }

            // 버튼에 사운드 이벤트 추가
            AddButtonSounds(button);
            registeredButtons.Add(button);

            return true;
        }

        /// <summary>
        /// 버튼이 모달 내부에 있는지 확인
        /// 부모 계층에서 ModalSoundPlayer를 찾아 자동으로 판단
        /// </summary>
        private bool IsModalButton(Button button)
        {
            // 버튼의 부모 계층을 올라가면서 ModalSoundPlayer 찾기
            Transform current = button.transform;
            while (current != null)
            {
                if (current.GetComponent<ModalSoundPlayer>() != null)
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// EventTrigger에 이미 Pointer 이벤트가 있는지 확인
        /// </summary>
        private bool HasPointerEvents(EventTrigger trigger)
        {
            foreach (var entry in trigger.triggers)
            {
                if (entry.eventID == EventTriggerType.PointerEnter ||
                    entry.eventID == EventTriggerType.PointerClick)
                {
                    return true;
                }
            }
            return false;
        }

        // ========================================
        // Sound Event Setup
        // ========================================

        /// <summary>
        /// 버튼에 Hover/Click 사운드 이벤트 추가
        /// </summary>
        private void AddButtonSounds(Button button)
        {
            // EventTrigger 컴포넌트 추가 (없으면)
            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            // PointerEnter (Hover) 이벤트 추가
            EventTrigger.Entry hoverEntry = new EventTrigger.Entry();
            hoverEntry.eventID = EventTriggerType.PointerEnter;
            hoverEntry.callback.AddListener((data) => { OnButtonHover(); });
            trigger.triggers.Add(hoverEntry);

            // PointerClick 이벤트 추가
            EventTrigger.Entry clickEntry = new EventTrigger.Entry();
            clickEntry.eventID = EventTriggerType.PointerClick;
            clickEntry.callback.AddListener((data) => { OnButtonClick(); });
            trigger.triggers.Add(clickEntry);

            if (verboseLog)
                Debug.Log($"[GlobalButtonSoundPlayer] 버튼 사운드 추가: {button.name}");
        }

        // ========================================
        // Sound Playback
        // ========================================

        private void OnButtonHover()
        {
            AudioManager.Instance?.PlaySFX(SFXType.ButtonHover);
        }

        private void OnButtonClick()
        {
            AudioManager.Instance?.PlaySFX(SFXType.ButtonClick);
        }

        // ========================================
        // Public API
        // ========================================

        /// <summary>
        /// 특정 버튼을 등록 해제 (사운드 제거하지 않음, 추적만 해제)
        /// </summary>
        public void UnregisterButton(Button button)
        {
            registeredButtons.Remove(button);
        }

        /// <summary>
        /// 현재 등록된 버튼 수
        /// </summary>
        public int RegisteredButtonCount => registeredButtons.Count;

#if UNITY_EDITOR
        /// <summary>
        /// Inspector 테스트: 버튼 재탐지
        /// </summary>
        [ContextMenu("버튼 재탐지")]
        private void TestRedetectButtons()
        {
            registeredButtons.Clear();
            DetectAndRegisterAllButtons();
        }

        /// <summary>
        /// Inspector 테스트: 등록된 버튼 정보 출력
        /// </summary>
        [ContextMenu("등록된 버튼 정보")]
        private void PrintRegisteredButtons()
        {
            Debug.Log($"=== 등록된 버튼: {registeredButtons.Count}개 ===");
            foreach (var button in registeredButtons)
            {
                if (button != null)
                    Debug.Log($"  - {button.name} (Scene: {button.gameObject.scene.name})");
            }
        }
#endif
    }
}
