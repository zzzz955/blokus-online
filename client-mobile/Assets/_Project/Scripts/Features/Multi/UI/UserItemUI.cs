using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Features.Multi.Net;

namespace Features.Multi.UI
{
    /// <summary>
    /// 사용자 목록 아이템 UI (Stub 구현)
    /// 로비에서 접속한 사용자를 표시하는 아이템
    /// </summary>
    public class UserItemUI : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private TextMeshProUGUI userNameText;
        [SerializeField] private Image statusIcon;
        [SerializeField] private Button clickButton;
        
        [Header("상태 색상")]
        [SerializeField] private Color onlineColor = Color.green;
        [SerializeField] private Color awayColor = Color.yellow;
        [SerializeField] private Color offlineColor = Color.gray;
        
        private UserInfo userData;
        
        public event System.Action<UserInfo> OnUserClicked;
        
        void Start()
        {
            SetupEventHandlers();
        }
        
        /// <summary>
        /// 이벤트 핸들러 설정
        /// </summary>
        private void SetupEventHandlers()
        {
            if (clickButton != null)
            {
                clickButton.onClick.AddListener(OnItemClicked);
            }
        }
        
        /// <summary>
        /// 사용자 데이터 설정
        /// </summary>
        public void SetupUser(UserInfo user)
        {
            userData = user;
            UpdateUI();
        }
        
        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (userData == null) return;
            
            // 사용자 이름 설정 (멀티플레이어에서는 displayName만 사용)
            if (userNameText != null)
                userNameText.text = userData.displayName ?? "Unknown";
            
            // 상태 아이콘 설정 (Stub)
            if (statusIcon != null)
            {
                statusIcon.color = onlineColor; // 기본적으로 온라인 상태
            }
        }
        
        /// <summary>
        /// 아이템 클릭 처리
        /// </summary>
        private void OnItemClicked()
        {
            if (userData != null)
            {
                Debug.Log($"[UserItemUI] 사용자 클릭: {userData.displayName}");
                OnUserClicked?.Invoke(userData);
            }
        }
        
        /// <summary>
        /// 상태 업데이트 (Stub)
        /// </summary>
        public void UpdateStatus(bool isOnline)
        {
            if (statusIcon != null)
            {
                statusIcon.color = isOnline ? onlineColor : offlineColor;
            }
        }
        
        private void OnDestroy()
        {
            if (clickButton != null)
                clickButton.onClick.RemoveAllListeners();
        }
    }
}