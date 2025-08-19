using UnityEngine;
using Shared.Models;
using App.Core;
using App.UI;
namespace Features.Multi{
    /// <summary>
    /// MultiGameplayScene stub implementation
    /// Migration Plan: 멀티플레이어 기능은 나중에 구현하되, 스켈레톤만 제공하여 씬 로딩 오류 방지
    /// </summary>
    public class MultiGameplaySceneStub : MonoBehaviour
    {
        [Header("Stub Configuration")]
        [SerializeField] private bool showStubMessage = true;
        [SerializeField] private float autoReturnDelay = 3f;

        private bool hasShownMessage = false;

        void Start()
        {
            InitializeStub();
        }

        /// <summary>
        /// 스텁 초기화
        /// </summary>
        private void InitializeStub()
        {
            Debug.Log("[MultiGameplaySceneStub] MultiGameplayScene loaded - showing stub message");

            if (showStubMessage && !hasShownMessage)
            {
                ShowStubMessage();
                hasShownMessage = true;

                // 자동으로 메인 씬으로 돌아가기
                if (autoReturnDelay > 0)
                {
                    Invoke(nameof(ReturnToMainScene), autoReturnDelay);
                }
            }
        }

        /// <summary>
        /// 스텁 메시지 표시
        /// </summary>
        private void ShowStubMessage()
        {
            if (SystemMessageManager.Instance != null)
            {
                SystemMessageManager.ShowToast("멀티플레이어 기능은 현재 개발 중입니다.");
            }
            else
            {
                Debug.Log("[MultiGameplaySceneStub] 멀티플레이어 기능은 현재 개발 중입니다.");
            }
        }

        /// <summary>
        /// 메인 씬으로 돌아가기 (5-Scene 아키텍처 지원)
        /// </summary>
        private void ReturnToMainScene()
        {
            Debug.Log("[MultiGameplaySceneStub] Returning to MainScene");

            // 🔥 수정: SceneFlowController를 통한 proper Scene 전환
            if (SceneFlowController.Instance != null)
            {
                Debug.Log("[MultiGameplaySceneStub] SceneFlowController를 통해 MainScene으로 전환");
                SceneFlowController.Instance.StartExitMultiToMain();
            }
            else
            {
                Debug.LogError("[MultiGameplaySceneStub] SceneFlowController가 없습니다! 레거시 방식으로 전환");
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
            }
        }

        /// <summary>
        /// 수동으로 메인 씬으로 돌아가기 (UI 버튼용)
        /// </summary>
        public void OnReturnButtonClicked()
        {
            CancelInvoke(nameof(ReturnToMainScene)); // 자동 돌아가기 취소
            ReturnToMainScene();
        }

        void OnDestroy()
        {
            CancelInvoke(); // 모든 Invoke 정리
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 스텁 테스트
        /// </summary>
        [ContextMenu("Test Stub Message")]
        private void TestStubMessage()
        {
            ShowStubMessage();
        }

        [ContextMenu("Test Return to Main")]
        private void TestReturnToMain()
        {
            ReturnToMainScene();
        }
#endif
    }
}