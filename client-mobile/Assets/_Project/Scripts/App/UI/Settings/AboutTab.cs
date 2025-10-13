using UnityEngine;
using TMPro;

namespace App.UI.Settings
{
    /// <summary>
    /// About 정보 탭 (앱 버전, Unity 버전, 디바이스 정보 등)
    /// 읽기 전용 정보 표시
    /// </summary>
    public class AboutTab : MonoBehaviour
    {
        [Header("앱 정보")]
        [SerializeField] private TMP_Text appNameText;
        [SerializeField] private TMP_Text appVersionText;
        [SerializeField] private TMP_Text buildNumberText;

        [Header("Unity 정보")]
        [SerializeField] private TMP_Text unityVersionText;

        [Header("디바이스 정보")]
        [SerializeField] private TMP_Text deviceModelText;
        [SerializeField] private TMP_Text osVersionText;

        [Header("라벨 (옵션)")]
        [SerializeField] private string appNameLabel = "App Name";
        [SerializeField] private string appVersionLabel = "Version";
        [SerializeField] private string buildNumberLabel = "Build";
        [SerializeField] private string unityVersionLabel = "Unity Version";
        [SerializeField] private string deviceModelLabel = "Device";
        [SerializeField] private string osVersionLabel = "OS";

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        void OnEnable()
        {
            RefreshAboutInfo();
        }

        /// <summary>
        /// About 정보 새로고침
        /// </summary>
        public void RefreshAboutInfo()
        {
            // 앱 이름
            if (appNameText != null)
            {
                string appName = Application.productName;
                appNameText.text = $"{appNameLabel}: {appName}";
            }

            // 앱 버전
            if (appVersionText != null)
            {
                string version = Application.version;
                appVersionText.text = $"{appVersionLabel}: {version}";
            }

            // 빌드 번호
            if (buildNumberText != null)
            {
                string buildNumber = Application.buildGUID;
                // buildGUID가 너무 길면 앞 8자리만 표시
                if (buildNumber.Length > 8)
                {
                    buildNumber = buildNumber.Substring(0, 8);
                }
                buildNumberText.text = $"{buildNumberLabel}: {buildNumber}";
            }

            // Unity 버전
            if (unityVersionText != null)
            {
                string unityVersion = Application.unityVersion;
                unityVersionText.text = $"{unityVersionLabel}: {unityVersion}";
            }

            // 디바이스 모델
            if (deviceModelText != null)
            {
                string deviceModel = SystemInfo.deviceModel;
                if (string.IsNullOrEmpty(deviceModel))
                {
                    deviceModel = SystemInfo.deviceName;
                }
                deviceModelText.text = $"{deviceModelLabel}: {deviceModel}";
            }

            // OS 버전
            if (osVersionText != null)
            {
                string osVersion = SystemInfo.operatingSystem;
                osVersionText.text = $"{osVersionLabel}: {osVersion}";
            }

            if (debugMode)
            {
                Debug.Log("[AboutTab] About 정보 표시 완료");
            }
        }

        /// <summary>
        /// 앱 버전 복사 (클립보드)
        /// </summary>
        public void CopyAppVersion()
        {
            if (!string.IsNullOrEmpty(Application.version))
            {
                GUIUtility.systemCopyBuffer = Application.version;

                if (debugMode)
                {
                    Debug.Log($"[AboutTab] 앱 버전 복사됨: {Application.version}");
                }
            }
        }

        /// <summary>
        /// 전체 정보 복사 (클립보드) - 디버그/문의용
        /// </summary>
        public void CopyAllInfo()
        {
            string allInfo = $"App: {Application.productName} v{Application.version} ({Application.buildGUID.Substring(0, 8)})\n" +
                           $"Unity: {Application.unityVersion}\n" +
                           $"Device: {SystemInfo.deviceModel}\n" +
                           $"OS: {SystemInfo.operatingSystem}";

            GUIUtility.systemCopyBuffer = allInfo;

            if (debugMode)
            {
                Debug.Log($"[AboutTab] 전체 정보 복사됨:\n{allInfo}");
            }
        }
    }
}
