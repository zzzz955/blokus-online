using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace App.UI
{
    /// <summary>
    /// MainScene에서 게임 종료 확인을 위한 모달
    /// Android 뒤로가기 버튼 클릭 시 표시
    /// </summary>
    public class GameExitModal : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button acceptButton;  // 게임 종료
        [SerializeField] private Button backgroundButton;  // 게임 종료
        [SerializeField] private Button rejectButton;  // 취소

        [Header("설정")]
        [SerializeField] private string confirmMessage = "게임을 종료하시겠습니까?";

        private void Awake()
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

        private void OnDestroy()
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
                //  핵심 수정: 부모 계층부터 모두 활성화
                EnsureParentHierarchyActive();
                
                modalPanel.SetActive(true);
                Debug.Log("[GameExitModal] 게임 종료 확인 모달 표시");
                
                //  디버깅: 모달 상태 확인
                Debug.Log($"[GameExitModal] modalPanel active: {modalPanel.activeSelf}");
                Debug.Log($"[GameExitModal] modalPanel activeInHierarchy: {modalPanel.activeInHierarchy}");
                Debug.Log($"[GameExitModal] modalPanel position: {modalPanel.transform.position}");
                Debug.Log($"[GameExitModal] modalPanel scale: {modalPanel.transform.localScale}");
                
                // Canvas 계층구조 확인
                var canvas = modalPanel.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    Debug.Log($"[GameExitModal] Canvas found: {canvas.name}, sortingOrder: {canvas.sortingOrder}, renderMode: {canvas.renderMode}");
                }
                else
                {
                    Debug.LogWarning("[GameExitModal] Canvas not found in parent hierarchy!");
                }
                
                //  강제 최상단 이동 시도
                EnsureModalOnTop();
                
                //  최종 상태 재확인
                Debug.Log($"[GameExitModal] 최종 상태 - active: {modalPanel.activeSelf}, activeInHierarchy: {modalPanel.activeInHierarchy}");
            }
            else
            {
                Debug.LogError("[GameExitModal] modalPanel is null! Inspector에서 modalPanel이 할당되지 않았습니다.");
                
                //  자동 복구 시도: GameExitModal이라는 이름의 GameObject 찾기
                var foundModal = GameObject.Find("GameExitModal");
                if (foundModal != null)
                {
                    modalPanel = foundModal;
                    Debug.Log($"[GameExitModal] 자동으로 발견된 modalPanel: {foundModal.name}");
                    ShowModal(); // 재귀 호출로 다시 시도
                }
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
                Debug.Log("[GameExitModal] 게임 종료 확인 모달 숨김");
            }
        }

        /// <summary>
        /// Accept 버튼 클릭 - 게임 종료
        /// </summary>
        private void OnAcceptClicked()
        {
            Debug.Log("[GameExitModal] 게임 종료 확인");
            
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Reject 버튼 클릭 - 모달 닫음
        /// </summary>
        private void OnRejectClicked()
        {
            Debug.Log("[GameExitModal] 게임 종료 취소");
            HideModal();
        }

        /// <summary>
        ///  추가: 부모 계층구조 전체 활성화 보장
        /// </summary>
        private void EnsureParentHierarchyActive()
        {
            if (modalPanel == null) return;

            // 부모 계층을 따라 올라가면서 모든 GameObject 활성화
            Transform current = modalPanel.transform;
            System.Collections.Generic.List<string> activatedObjects = new System.Collections.Generic.List<string>();

            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                    activatedObjects.Add(current.name);
                    Debug.Log($"[GameExitModal] 부모 오브젝트 활성화: {current.name}");
                }
                current = current.parent;
            }

            if (activatedObjects.Count > 0)
            {
                Debug.Log($"[GameExitModal] 총 {activatedObjects.Count}개 부모 오브젝트를 활성화했습니다: {string.Join(", ", activatedObjects)}");
            }
            else
            {
                Debug.Log("[GameExitModal] 모든 부모 오브젝트가 이미 활성화되어 있습니다.");
            }

            // 자기 자신도 확실히 활성화 (혹시라도 비활성 상태일 경우)
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                Debug.Log($"[GameExitModal] GameExitModal 스크립트 GameObject도 활성화: {gameObject.name}");
            }
        }

        /// <summary>
        ///  수정: 모달을 최상단에 표시되도록 보장 (크기/위치 조정 제거)
        /// </summary>
        private void EnsureModalOnTop()
        {
            if (modalPanel == null) return;

            try
            {
                // 1. Transform을 최상단으로 이동
                modalPanel.transform.SetAsLastSibling();
                Debug.Log("[GameExitModal] 모달을 Transform 계층에서 최상단으로 이동");

                // 2. Canvas가 있다면 sortingOrder를 높게 설정
                var canvas = modalPanel.GetComponent<Canvas>();
                if (canvas == null)
                {
                    // Canvas가 없다면 추가
                    canvas = modalPanel.AddComponent<Canvas>();
                    canvas.overrideSorting = true;
                    Debug.Log("[GameExitModal] Canvas 컴포넌트 추가됨");
                }

                // 높은 sortingOrder로 설정
                canvas.overrideSorting = true;
                canvas.sortingOrder = 1000; // 매우 높은 값으로 설정
                Debug.Log($"[GameExitModal] Canvas sortingOrder를 {canvas.sortingOrder}로 설정");

                // 3. GraphicRaycaster가 없다면 추가 (터치 이벤트 처리용)
                var raycaster = modalPanel.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (raycaster == null)
                {
                    raycaster = modalPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                    Debug.Log("[GameExitModal] GraphicRaycaster 컴포넌트 추가됨");
                }

                //  크기/위치 조정 코드 제거 - Unity Inspector에서 설정한 기본값 사용
                Debug.Log("[GameExitModal] Unity Inspector 설정값 사용 - 크기/위치 조정하지 않음");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameExitModal] EnsureModalOnTop 실행 중 오류: {ex.Message}");
            }
        }
    }
}