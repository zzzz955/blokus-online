using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Features.Single.Gameplay;
using System;
using App.Core;

namespace Shared.UI{
    /// <summary>
    /// 범용 확인 모달
    /// 로그아웃, 방 나가기, 게임 종료 등 다양한 확인 다이얼로그에 재사용 가능
    /// 기존 ExitConfirmModal에서 리팩토링됨
    /// </summary>
    public class ConfirmModal : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button rejectButton;
        
        [Header("기본 설정")]
        [SerializeField] private string defaultTitle = "확인";
        [SerializeField] private string defaultMessage = "정말로 실행하시겠습니까?";
        [SerializeField] private string acceptButtonText = "확인";
        [SerializeField] private string rejectButtonText = "취소";
        
        // 콜백 이벤트
        private Action onAcceptCallback;
        private Action onRejectCallback;
        
        void Awake()
        {
            // 버튼 이벤트 연결
            if (acceptButton != null)
            {
                acceptButton.onClick.AddListener(OnAcceptClicked);
                
                // 버튼 텍스트 설정
                var acceptText = acceptButton.GetComponentInChildren<TextMeshProUGUI>();
                if (acceptText != null)
                    acceptText.text = acceptButtonText;
            }

            if (backgroundButton != null)
            {
                backgroundButton.onClick.AddListener(OnRejectClicked);
            }
            
            if (rejectButton != null)
            {
                rejectButton.onClick.AddListener(OnRejectClicked);
                
                // 버튼 텍스트 설정
                var rejectText = rejectButton.GetComponentInChildren<TextMeshProUGUI>();
                if (rejectText != null)
                    rejectText.text = rejectButtonText;
            }
            
            // 초기에는 모달 숨김
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
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
        /// 모달 표시 (기본 메시지)
        /// </summary>
        public void ShowModal()
        {
            ShowModal(defaultTitle, defaultMessage, null, null);
        }
        
        /// <summary>
        /// 모달 표시 (커스텀 메시지와 콜백)
        /// </summary>
        public void ShowModal(string title, string message, Action onAccept, Action onReject = null)
        {
            Debug.Log($"[ConfirmModal] ShowModal 호출됨 - Title: {title}");
            Debug.Log($"[ConfirmModal] modalPanel null 여부: {modalPanel == null}");
            
            // 텍스트 설정
            if (titleText != null)
                titleText.text = title;
            else
                Debug.LogWarning("[ConfirmModal] titleText가 null입니다!");
                
            if (messageText != null)
                messageText.text = message;
            else
                Debug.LogWarning("[ConfirmModal] messageText가 null입니다!");
            
            // 콜백 설정
            onAcceptCallback = onAccept;
            onRejectCallback = onReject;
            
            // ConfirmModal GameObject 자체도 활성화
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
                Debug.Log($"[ConfirmModal] ConfirmModal GameObject 활성화됨");
            }
            
            // 모달 표시
            if (modalPanel != null)
            {
                modalPanel.SetActive(true);
                Debug.Log($"[ConfirmModal] 모달 표시 완료: {title}");
                Debug.Log($"[ConfirmModal] modalPanel.activeSelf: {modalPanel.activeSelf}");
                Debug.Log($"[ConfirmModal] ConfirmModal GameObject.activeSelf: {gameObject.activeSelf}");
                
                // 추가 디버그 정보
                var rectTransform = modalPanel.GetComponent<RectTransform>();
                var canvasGroup = modalPanel.GetComponent<CanvasGroup>();
                var canvas = modalPanel.GetComponentInParent<Canvas>();
                
                Debug.Log($"[ConfirmModal] modalPanel 위치: {rectTransform?.anchoredPosition}");
                Debug.Log($"[ConfirmModal] modalPanel 크기: {rectTransform?.sizeDelta}");
                if (canvasGroup != null)
                    Debug.Log($"[ConfirmModal] CanvasGroup alpha: {canvasGroup.alpha}");
                else
                    Debug.Log("[ConfirmModal] CanvasGroup 컴포넌트 없음");
                Debug.Log($"[ConfirmModal] Canvas sortingOrder: {canvas?.sortingOrder}");
                Debug.Log($"[ConfirmModal] 부모 Canvas 이름: {canvas?.name}");
            }
            else
            {
                Debug.LogError("[ConfirmModal] modalPanel이 null입니다! Inspector에서 Modal Panel을 할당해주세요.");
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
                Debug.Log("[ConfirmModal] 모달 숨김");
            }
            
            // ConfirmModal GameObject도 비활성화 (선택사항 - 필요에 따라)
            // gameObject.SetActive(false);
            
            // 콜백 초기화
            onAcceptCallback = null;
            onRejectCallback = null;
        }
        
        /// <summary>
        /// 확인 버튼 클릭
        /// </summary>
        private void OnAcceptClicked()
        {
            Debug.Log("[ConfirmModal] 확인 버튼 클릭");
            
            // 콜백 실행 (콜백이 없으면 기존 게임 종료 로직 실행)
            if (onAcceptCallback != null)
            {
                onAcceptCallback?.Invoke();
            }
            else
            {
                // 기존 게임 종료 로직 (하위 호환성)
                Debug.Log("[ConfirmModal] 기본 게임 종료 로직 실행");
                
                // 게임 진행도 저장 (중도 포기)
                if (SingleGameManager.Instance != null)
                {
                    SingleGameManager.Instance.OnExitRequested();
                }
                
                // Exit으로 돌아온다는 플래그 설정
                PlayerPrefs.SetInt("ReturnedFromGame", 1);
                PlayerPrefs.Save();
                
                // SceneFlowController를 통한 Scene 전환
                if (SceneFlowController.Instance != null)
                {
                    Debug.Log("[ConfirmModal] SceneFlowController를 통해 MainScene으로 전환");
                    SceneFlowController.Instance.StartExitSingleToMain();
                }
                else
                {
                    Debug.LogError("[ConfirmModal] SceneFlowController가 없습니다! 레거시 방식으로 전환");
                    SceneManager.LoadScene("MainScene");
                }
            }
            
            // 모달 숨김
            HideModal();
        }
        
        /// <summary>
        /// 취소 버튼 클릭
        /// </summary>
        private void OnRejectClicked()
        {
            Debug.Log("[ConfirmModal] 취소 버튼 클릭");
            
            // 콜백 실행
            onRejectCallback?.Invoke();
            
            // 모달 숨김
            HideModal();
        }
        
        /// <summary>
        /// Exit 버튼에서 호출할 공개 메서드 (하위 호환성)
        /// </summary>
        public void OnExitButtonClicked()
        {
            ShowModal();
        }
        
        /// <summary>
        /// ConfirmationModal 호환성을 위한 Show 메서드
        /// </summary>
        public void Show(string title, string message, Action onAcceptCallback = null, Action onRejectCallback = null)
        {
            ShowModal(title, message, onAcceptCallback, onRejectCallback);
        }
        
        /// <summary>
        /// ConfirmationModal 호환성을 위한 Hide 메서드
        /// </summary>
        public void Hide()
        {
            HideModal();
        }
        
        /// <summary>
        /// Undo 확인 모달을 위한 편의 메서드
        /// </summary>
        public void ShowUndoConfirmation(Action onConfirm, Action onCancel = null)
        {
            ShowModal(
                "실행 취소",
                "마지막 블록 배치를 되돌리시겠습니까?\n블록이 팔레트로 돌아가고 점수가 감소합니다.",
                onConfirm,
                onCancel
            );
        }
        
        /// <summary>
        /// 게임 종료 확인 모달을 위한 편의 메서드
        /// </summary>
        public void ShowExitConfirmation(Action onConfirm, Action onCancel = null)
        {
            ShowModal(
                "게임 종료",
                "게임을 종료하고 스테이지 목록으로 돌아가시겠습니까?\n현재 진행상황이 저장됩니다.",
                onConfirm,
                onCancel
            );
        }
        
        /// <summary>
        /// Android 뒤로가기 버튼 처리
        /// </summary>
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && modalPanel != null && modalPanel.activeSelf)
            {
                OnRejectClicked();
            }
        }
    }
}