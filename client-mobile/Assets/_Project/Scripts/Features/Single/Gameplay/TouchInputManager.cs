// Assets/Scripts/Game/TouchInputManager.cs
using UnityEngine;
namespace Features.Single.Gameplay{
    /// <summary>
    /// (비활성 버전) 스와이프/제스처 입력 제거.
    /// 조작은 ActionButtonPanel(회전/플립/배치)과 팔레트/보드 UI가 담당한다.
    /// </summary>
    public sealed class TouchInputManager : MonoBehaviour
    {
        [Tooltip("디버그/개발 중 임시로 키보드 단축키를 허용하려면 On으로 변경 (R/F/Space 등은 별도 구현 필요)")]
        [SerializeField] private bool enableDebugHotkeys = false;

        private void Update()
        {
            // 의도적으로 아무 것도 하지 않음.
            // (스와이프 회전/플립/언두 등 모든 제스처 비활성화)
            if (!enableDebugHotkeys) return;

            // 필요 시 여기에서만 임시 단축키 처리 (지금은 비워둠)
            // 예) if (Input.GetKeyDown(KeyCode.R)) { /* 회전 버튼과 동일 로직 호출 */ }
        }
    }
}
