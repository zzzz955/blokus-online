using UnityEngine;
using UnityEngine.UI;

namespace App.UI.Settings
{
    /// <summary>
    /// 게임 설정 모달 (Audio, Gameplay, Account, About 탭 포함)
    /// Settings 버튼 클릭 시 표시되는 Overlay 모달
    /// </summary>
    public class SettingsModal : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backgroundButton; // 배경 클릭으로 닫기 (옵션)

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        private void Awake()
        {
            // 닫기 버튼 이벤트 연결
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HideModal);
            }
            else
            {
                Debug.LogWarning("[SettingsModal] closeButton이 Inspector에서 할당되지 않았습니다!");
            }

            // 배경 클릭으로 닫기 (옵션)
            if (backgroundButton != null)
            {
                backgroundButton.onClick.AddListener(HideModal);
            }

            // 초기에는 모달 숨김
            if (modalPanel != null)
            {
                modalPanel.SetActive(false);
            }
            else
            {
                Debug.LogError("[SettingsModal] modalPanel이 Inspector에서 할당되지 않았습니다!");
            }
        }

        private void OnDestroy()
        {
            // 버튼 이벤트 해제
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HideModal);
            }

            if (backgroundButton != null)
            {
                backgroundButton.onClick.RemoveListener(HideModal);
            }
        }

        private void Update()
        {
            // ESC/Android Back 버튼으로 모달 닫기
            if (modalPanel != null && modalPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                HideModal();
            }
        }

        /// <summary>
        /// 모달 표시
        /// </summary>
        public void ShowModal()
        {
            // SettingsModal GameObject 자체를 활성화
            gameObject.SetActive(true);

            if (modalPanel != null)
            {
                // 부모 계층구조 활성화 보장
                EnsureParentHierarchyActive();

                modalPanel.SetActive(true);

                // 모달을 최상단에 표시
                EnsureModalOnTop();
            }

            if (debugMode)
            {
                Debug.Log("[SettingsModal] Settings 모달 표시");
            }
        }

        /// <summary>
        /// 모달 숨김
        /// </summary>
        public void HideModal()
        {
            // SettingsModal GameObject 자체를 비활성화 (Background 포함)
            gameObject.SetActive(false);

            if (debugMode)
            {
                Debug.Log("[SettingsModal] Settings 모달 숨김");
            }
        }

        /// <summary>
        /// 부모 계층구조 전체 활성화 보장
        /// </summary>
        private void EnsureParentHierarchyActive()
        {
            if (modalPanel == null) return;

            Transform current = modalPanel.transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                    if (debugMode)
                    {
                        Debug.Log($"[SettingsModal] 부모 오브젝트 활성화: {current.name}");
                    }
                }
                current = current.parent;
            }

            // 자기 자신도 활성화
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 모달을 최상단에 표시되도록 보장
        /// </summary>
        private void EnsureModalOnTop()
        {
            if (modalPanel == null) return;

            // Transform을 최상단으로 이동
            modalPanel.transform.SetAsLastSibling();

            // Canvas override sorting 설정
            var canvas = modalPanel.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = modalPanel.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000; // 최상단 표시

            // GraphicRaycaster 추가 (터치 이벤트 처리)
            var raycaster = modalPanel.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
            {
                modalPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
        }
    }
}
