using UnityEngine;
using App.Logging;
#if UNITY_ANDROID
using GooglePlayGames;
#endif

namespace App.Core
{
    /// <summary>
    /// Google Play Games 플랫폼 초기화
    /// 게임 시작 시 Google Play Games 서비스를 설정하되, 자동 로그인은 하지 않습니다.
    /// 실제 인증은 사용자가 명시적으로 로그인 버튼을 클릭할 때만 수행됩니다.
    /// </summary>
    public class PlayGamesInitializer : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            InitializeGooglePlayGames();
        }

        private void InitializeGooglePlayGames()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            AndroidLogger.LogAuth("=== PlayGamesInitializer START ===");

            try
            {
                AndroidLogger.LogAuth($"Application ID: {GameInfo.ApplicationId}");
                AndroidLogger.LogAuth($"Web Client ID initialized: {GameInfo.WebClientIdInitialized()}");

                // Web Client ID 필수 체크
                if (!GameInfo.WebClientIdInitialized())
                {
                    AndroidLogger.LogError("❌ Web Client ID not configured!");
                    AndroidLogger.LogError("Please set WebClientId in GameInfo.cs");
                    AndroidLogger.LogError("Create Web Application OAuth Client ID in Google Cloud Console");
                    return;
                }

                // PlayGamesPlatform.Activate() 호출:
                // Silent sign-in을 위해 Platform을 활성화합니다.
                // 이 시점에서는 UI를 보여주지 않으며, 이전에 로그인한 계정이 있을 경우에만 자동 로그인됩니다.
                AndroidLogger.LogAuth("Activating PlayGamesPlatform for silent sign-in...");
                PlayGamesPlatform.Activate();
                AndroidLogger.LogAuth("✅ PlayGamesPlatform activated");

                AndroidLogger.LogAuth("✅ Google Play Games v2 initialized");
                AndroidLogger.LogAuth("Silent sign-in will be attempted at app start");
                AndroidLogger.LogAuth("Interactive sign-in requires explicit user action (button click)");
            }
            catch (System.Exception ex)
            {
                AndroidLogger.LogError($"❌ PlayGamesInitializer failed: {ex.Message}");
                AndroidLogger.LogError($"StackTrace: {ex.StackTrace}");
            }
            #else
            Debug.Log("[PlayGamesInitializer] Google Play Games skipped (not Android device)");
            #endif
        }
    }
}