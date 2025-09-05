using UnityEngine;
using UnityEngine.UI;

namespace App.UI
{
    /// <summary>
    /// 탭 버튼 컨트롤러
    /// TabGroup과 연동하여 탭 전환 처리
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TabButton : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        
        private Button button;
        private TabGroup tabGroup;
        
        private void Awake()
        {
            button = GetComponent<Button>();
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
        }
        
        private void Start()
        {
            button.onClick.AddListener(OnButtonClicked);
        }
        
        /// <summary>
        /// 탭 그룹 설정
        /// </summary>
        public void SetTabGroup(TabGroup group)
        {
            tabGroup = group;
        }
        
        /// <summary>
        /// 버튼 클릭 처리
        /// </summary>
        private void OnButtonClicked()
        {
            if (tabGroup != null)
            {
                tabGroup.SelectTab(this);
            }
        }
        
        /// <summary>
        /// 버튼 색상 설정
        /// </summary>
        public void SetColor(Color color)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }
        }
        
        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveAllListeners();
        }
    }
}