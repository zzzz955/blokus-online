using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BlokusUnity.UI
{
    public class ModeSelectionPanel : BlokusUnity.UI.PanelBase
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button multiPlayerButton;
        [SerializeField] private Button backButton;
        
        protected override void Start()
        {
            base.Start();
            Debug.Log("ModeSelectionPanel 초기화 완료");
            
            // 인스펙터 할당 버튼 이벤트 연결
            SetupButtons();
        }
        
        /// <summary>
        /// 인스펙터에서 할당된 버튼들의 이벤트 연결
        /// </summary>
        private void SetupButtons()
        {
            if (singlePlayerButton != null)
            {
                singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);
                Debug.Log("싱글플레이 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogWarning("singlePlayerButton이 인스펙터에서 할당되지 않았습니다!");
            }
            
            if (multiPlayerButton != null)
            {
                multiPlayerButton.onClick.AddListener(OnMultiPlayerClicked);
                Debug.Log("멀티플레이 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogWarning("multiPlayerButton이 인스펙터에서 할당되지 않았습니다!");
            }
            
            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackButtonClicked);
                Debug.Log("뒤로가기 버튼 이벤트 연결 완료");
            }
            else
            {
                Debug.LogWarning("backButton이 인스펙터에서 할당되지 않았습니다!");
            }
            
            Debug.Log("ModeSelectionPanel 버튼 설정 완료");
        }
        
        public void OnSinglePlayerClicked()
        {
            Debug.Log("싱글플레이 버튼 클릭");
            UIManager.Instance?.OnSingleModeSelected();
        }
        
        public void OnMultiPlayerClicked()
        {
            Debug.Log("멀티플레이 버튼 클릭");
            UIManager.Instance?.OnMultiModeSelected();
        }
        
        public void OnBackButtonClicked()
        {
            Debug.Log("뒤로가기 버튼 클릭");
            UIManager.Instance?.OnBackToMenu();
        }
    }
}