using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Features.Single.Gameplay;
namespace Features.Single.UI.InGame{
    /// <summary>
    /// 게임 종료 확인 모달
    /// Exit 버튼 클릭 시 표시되는 확인 다이얼로그
    /// </summary>
    public class ExitConfirmModal : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button rejectButton;
        
        [Header("설정")]
        [SerializeField] private string confirmMessage = "";
        [SerializeField] private string mainSceneName = "MainScene";
        
        void Awake()
        {
            // 버튼 이벤트 연결
            if (acceptButton != null)
            {
                acceptButton.onClick.AddListener(OnAcceptClicked);
            }

            if (backgroundButton != null)
            {
                backgroundButton.onClick.AddListener(OnRejectClicked);
            }
            
            if (rejectButton != null)
            {
                rejectButton.onClick.AddListener(OnRejectClicked);
            }
            
            // 초기에는 모달 숨김
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }
            
            // 메시지 텍스트 설정
            if (messageText != null)
            {
                messageText.text = confirmMessage;
            }
        }
        
        void OnDestroy()
        {
            // 버튼 이벤트 해제
            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveListener(OnAcceptClicked);
            }
            
            if (rejectButton != null)
            {
                rejectButton.onClick.RemoveListener(OnRejectClicked);
            }
        }
        
        /// <summary>
        /// 모달 표시
        /// </summary>
        public void ShowModal()
        {
            if (modalPanel != null)
            {
                modalPanel.SetActive(true);
                Debug.Log("[ExitConfirmModal] 종료 확인 모달 표시");
            }
        }
        
        /// <summary>
        /// 모달 숨김
        /// </summary>
        public void HideModal()
        {
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
                Debug.Log("[ExitConfirmModal] 종료 확인 모달 숨김");
            }
        }
        
        /// <summary>
        /// 확인 버튼 클릭 - MainScene으로 이동 (5-Scene 아키텍처 지원)
        /// </summary>
        private void OnAcceptClicked()
        {
            Debug.Log("[ExitConfirmModal] 게임 종료 확인 - MainScene으로 이동");
            
            // 게임 진행도 저장 (중도 포기)
            if (SingleGameManager.Instance != null)
            {
                SingleGameManager.Instance.OnExitRequested();
            }
            
            // Exit으로 돌아온다는 플래그 설정
            PlayerPrefs.SetInt("ReturnedFromGame", 1);
            PlayerPrefs.Save();
            
            // 🔥 수정: SceneFlowController를 통한 proper Scene 전환
            if (App.Core.SceneFlowController.Instance != null)
            {
                Debug.Log("[ExitConfirmModal] SceneFlowController를 통해 MainScene으로 전환");
                App.Core.SceneFlowController.Instance.StartExitSingleToMain();
            }
            else
            {
                Debug.LogError("[ExitConfirmModal] SceneFlowController가 없습니다! 레거시 방식으로 전환");
                SceneManager.LoadScene(mainSceneName);
            }
        }
        
        /// <summary>
        /// 취소 버튼 클릭 - 모달 닫음
        /// </summary>
        private void OnRejectClicked()
        {
            Debug.Log("[ExitConfirmModal] 게임 종료 취소");
            HideModal();
        }
        
        /// <summary>
        /// Exit 버튼에서 호출할 공개 메서드
        /// </summary>
        public void OnExitButtonClicked()
        {
            ShowModal();
        }
    }
}