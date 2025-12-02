using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Features.Multi.UI
{
    /// <summary>
    /// 버전 불일치 모달
    /// 서버와 클라이언트 버전이 맞지 않을 때 표시되며 플레이스토어 업데이트 유도
    /// </summary>
    public class VersionMismatchModal : MonoBehaviour
    {
        [Header("UI 요소")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button updateButton;
        [SerializeField] private Button cancelButton;

        [Header("설정")]
        [SerializeField] private string playStorePackageName = "com.blokus.bloblo"; // 플레이스토어 패키지명

        private string downloadUrl;

        void Awake()
        {
            // 버튼 이벤트 연결
            if (updateButton != null)
            {
                updateButton.onClick.AddListener(OnUpdateButtonClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelButtonClicked);
            }

            // 기본 텍스트 설정
            if (titleText != null)
            {
                titleText.text = "업데이트 필요";
            }

            if (messageText != null)
            {
                messageText.text = "최신 버전으로 업데이트가 필요합니다.\n플레이스토어에서 최신 버전을 설치해주세요.";
            }
        }

        /// <summary>
        /// 모달 표시
        /// </summary>
        /// <param name="url">다운로드 URL (선택사항)</param>
        public void Show(string url = null)
        {
            downloadUrl = url;
            gameObject.SetActive(true);
            Debug.Log($"[VersionMismatchModal] 버전 불일치 모달 표시 - downloadUrl: {downloadUrl}");
        }

        /// <summary>
        /// 모달 숨김
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            downloadUrl = null;
        }

        /// <summary>
        /// 업데이트 버튼 클릭 처리
        /// </summary>
        private void OnUpdateButtonClicked()
        {
            Debug.Log("[VersionMismatchModal] 업데이트 버튼 클릭");

            // 플레이스토어로 이동
            OpenPlayStore();

            // 모달 숨김
            Hide();
        }

        /// <summary>
        /// 취소 버튼 클릭 처리
        /// </summary>
        private void OnCancelButtonClicked()
        {
            Debug.Log("[VersionMismatchModal] 취소 버튼 클릭");

            // 모달 숨김
            Hide();
        }

        /// <summary>
        /// 플레이스토어 앱 페이지 열기
        /// </summary>
        private void OpenPlayStore()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                // Android 플레이스토어 URL
                string playStoreUrl = $"market://details?id={playStorePackageName}";

                Debug.Log($"[VersionMismatchModal] 플레이스토어 열기: {playStoreUrl}");
                Application.OpenURL(playStoreUrl);
#elif UNITY_EDITOR
                // 에디터에서는 웹 플레이스토어 페이지 열기
                string webPlayStoreUrl = $"https://play.google.com/store/apps/details?id={playStorePackageName}";
                Debug.Log($"[VersionMismatchModal] 에디터: 웹 플레이스토어 열기 ({webPlayStoreUrl})");
                Application.OpenURL(webPlayStoreUrl);
#else
                Debug.LogWarning("[VersionMismatchModal] 플레이스토어는 Android 플랫폼에서만 지원됩니다.");
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VersionMismatchModal] 플레이스토어 열기 실패: {ex.Message}");

                // 실패 시 웹 플레이스토어 시도
                try
                {
                    string webPlayStoreUrl = $"https://play.google.com/store/apps/details?id={playStorePackageName}";
                    Debug.Log($"[VersionMismatchModal] 대체: 웹 플레이스토어 열기 ({webPlayStoreUrl})");
                    Application.OpenURL(webPlayStoreUrl);
                }
                catch (System.Exception ex2)
                {
                    Debug.LogError($"[VersionMismatchModal] 웹 플레이스토어 열기도 실패: {ex2.Message}");
                }
            }
        }

        void OnDestroy()
        {
            // 이벤트 구독 해제
            if (updateButton != null)
            {
                updateButton.onClick.RemoveListener(OnUpdateButtonClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelButtonClicked);
            }
        }
    }
}
