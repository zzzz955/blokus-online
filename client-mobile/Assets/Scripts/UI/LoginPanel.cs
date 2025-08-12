using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BlokusUnity.UI
{
    public class LoginPanel : BaseUIPanel
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Button loginButton;
        [SerializeField] private Button guestButton;
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;
        
        protected override void Awake()
        {
            base.Awake();
            // LoginPanel은 게임의 첫 진입점이므로 시작시 활성화
            startActive = true;
            Debug.Log("LoginPanel startActive = true로 설정");
        }
        
        protected override void Start()
        {
            base.Start();
            Debug.Log("LoginPanel 초기화 완료");
            
            // 인스펙터 할당 버튼 이벤트 연결
            SetupButtons();
        }
        
        /// <summary>
        /// 인스펙터에서 할당된 버튼들의 이벤트 연결
        /// </summary>
        private void SetupButtons()
        {
            if (loginButton != null)
            {
                loginButton.onClick.AddListener(OnLoginButtonClicked);
                Debug.Log("로그인 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogWarning("loginButton이 인스펙터에서 할당되지 않았습니다!");
            }
            
            if (guestButton != null)
            {
                guestButton.onClick.AddListener(OnGuestLoginClicked);
                Debug.Log("게스트 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogWarning("guestButton이 인스펙터에서 할당되지 않았습니다!");
            }
            
            Debug.Log("LoginPanel 버튼 설정 완료");
        }
        
        public void OnLoginButtonClicked()
        {
            Debug.Log("로그인 버튼 클릭");
            UIManager.Instance?.OnLoginSuccess();
        }
        
        public void OnGuestLoginClicked()
        {
            Debug.Log("게스트 로그인 버튼 클릭");
            UIManager.Instance?.OnLoginSuccess();
        }
    }
}