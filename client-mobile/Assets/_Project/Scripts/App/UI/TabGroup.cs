using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace App.UI
{
    /// <summary>
    /// 탭 그룹 컨트롤러
    /// 여러 탭 간의 전환을 관리
    /// </summary>
    public class TabGroup : MonoBehaviour
    {
        [SerializeField] private List<TabButton> tabButtons = new List<TabButton>();
        [SerializeField] private List<GameObject> tabContents = new List<GameObject>();
        [SerializeField] private TabButton activeTab;
        
        [Header("색상 설정")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = Color.cyan;
        
        public event System.Action<int> OnTabChanged;
        
        private void Start()
        {
            // 첫 번째 탭을 활성화
            if (tabButtons.Count > 0)
            {
                SelectTab(tabButtons[0]);
            }
        }
        
        /// <summary>
        /// 탭 버튼 등록
        /// </summary>
        public void RegisterTab(TabButton tabButton)
        {
            if (!tabButtons.Contains(tabButton))
            {
                tabButtons.Add(tabButton);
                tabButton.SetTabGroup(this);
            }
        }
        
        /// <summary>
        /// 탭 선택
        /// </summary>
        public void SelectTab(TabButton tabButton)
        {
            if (tabButton == null || !tabButtons.Contains(tabButton))
                return;
                
            activeTab = tabButton;
            int tabIndex = tabButtons.IndexOf(tabButton);
            
            // 모든 탭 버튼 색상 리셋
            foreach (TabButton button in tabButtons)
            {
                button.SetColor(normalColor);
            }
            
            // 선택된 탭 활성화
            tabButton.SetColor(selectedColor);
            
            // 탭 컨텐츠 전환
            for (int i = 0; i < tabContents.Count; i++)
            {
                if (tabContents[i] != null)
                {
                    tabContents[i].SetActive(i == tabIndex);
                }
            }
            
            // 이벤트 발생
            OnTabChanged?.Invoke(tabIndex);
        }
        
        /// <summary>
        /// 인덱스로 탭 선택
        /// </summary>
        public void SelectTab(int index)
        {
            if (index >= 0 && index < tabButtons.Count)
            {
                SelectTab(tabButtons[index]);
            }
        }
        
        /// <summary>
        /// 활성 탭 가져오기
        /// </summary>
        public TabButton GetActiveTab()
        {
            return activeTab;
        }
        
        /// <summary>
        /// 활성 탭 인덱스 가져오기
        /// </summary>
        public int GetActiveTabIndex()
        {
            return activeTab != null ? tabButtons.IndexOf(activeTab) : -1;
        }
    }
}