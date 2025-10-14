using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace App.UI.Settings
{
    /// <summary>
    /// Settings 모달의 Tab 전환 시스템
    /// 각 Tab 버튼 클릭 시 해당 인덱스의 Content를 활성화
    /// </summary>
    public class TabGroup : MonoBehaviour
    {
        [Header("Tab System")]
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private GameObject[] tabContents;

        [Header("Text Color Settings")]
        [SerializeField] private Color activeColor = new Color(0.5f, 1f, 0f); // 형광 연두색
        [SerializeField] private Color normalColor = Color.white; // 흰색
        [SerializeField] private Color hoverColor = Color.yellow; // 노란색

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        private int currentTabIndex = 0;
        private TMP_Text[] tabTexts;

        void Awake()
        {
            // 배열 길이 검증
            if (tabButtons.Length != tabContents.Length)
            {
                Debug.LogError($"[TabGroup] 버튼 배열({tabButtons.Length})과 콘텐츠 배열({tabContents.Length}) 길이 불일치!");
                return;
            }

            // 각 버튼의 TMP_Text 컴포넌트 찾기
            tabTexts = new TMP_Text[tabButtons.Length];
            for (int i = 0; i < tabButtons.Length; i++)
            {
                tabTexts[i] = tabButtons[i].GetComponentInChildren<TMP_Text>();

                if (tabTexts[i] == null)
                {
                    Debug.LogError($"[TabGroup] Button '{tabButtons[i].name}'에 TMP_Text가 없습니다!");
                }
            }
        }

        void Start()
        {
            // 버튼 이벤트 연결
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i; // Closure 문제 방지
                tabButtons[i].onClick.AddListener(() => ShowTab(index));

                // Hover 이벤트 추가
                AddHoverEvents(tabButtons[i], index);
            }

            // 첫 번째 탭 활성화 (Audio)
            ShowTab(0);
        }

        void OnDestroy()
        {
            // 버튼 이벤트 해제
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.RemoveAllListeners();
            }
        }

        /// <summary>
        /// 지정된 인덱스의 탭 표시
        /// </summary>
        public void ShowTab(int index)
        {
            if (index < 0 || index >= tabContents.Length)
            {
                Debug.LogWarning($"[TabGroup] 잘못된 탭 인덱스: {index}");
                return;
            }

            currentTabIndex = index;

            // 모든 Content 비활성화
            for (int i = 0; i < tabContents.Length; i++)
            {
                tabContents[i].SetActive(i == index);
            }

            // 텍스트 색상 업데이트
            UpdateTabTextColors();

            if (debugMode)
            {
                Debug.Log($"[TabGroup] 탭 전환: {index} ({tabContents[index].name})");
            }
        }

        /// <summary>
        /// 탭 텍스트 색상 업데이트 (Active/Normal)
        /// </summary>
        private void UpdateTabTextColors()
        {
            for (int i = 0; i < tabTexts.Length; i++)
            {
                if (tabTexts[i] != null)
                {
                    tabTexts[i].color = (i == currentTabIndex) ? activeColor : normalColor;
                }
            }
        }

        /// <summary>
        /// Hover 이벤트 추가 (EventTrigger 사용)
        /// </summary>
        private void AddHoverEvents(Button button, int index)
        {
            // EventTrigger 컴포넌트 추가
            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }
            else
            {
                // 기존 트리거 초기화
                trigger.triggers.Clear();
            }

            // PointerEnter (호버 시작)
            EventTrigger.Entry entryEnter = new EventTrigger.Entry();
            entryEnter.eventID = EventTriggerType.PointerEnter;
            entryEnter.callback.AddListener((data) => OnTabHoverEnter(index));
            trigger.triggers.Add(entryEnter);

            // PointerExit (호버 종료)
            EventTrigger.Entry entryExit = new EventTrigger.Entry();
            entryExit.eventID = EventTriggerType.PointerExit;
            entryExit.callback.AddListener((data) => OnTabHoverExit(index));
            trigger.triggers.Add(entryExit);
        }

        /// <summary>
        /// 탭 호버 시작
        /// </summary>
        private void OnTabHoverEnter(int index)
        {
            // 현재 선택되지 않은 탭만 hover 색상 적용
            if (index != currentTabIndex && tabTexts[index] != null)
            {
                tabTexts[index].color = hoverColor;
            }
        }

        /// <summary>
        /// 탭 호버 종료
        /// </summary>
        private void OnTabHoverExit(int index)
        {
            // 원래 색상으로 복원
            if (index != currentTabIndex && tabTexts[index] != null)
            {
                tabTexts[index].color = normalColor;
            }
        }
    }
}
