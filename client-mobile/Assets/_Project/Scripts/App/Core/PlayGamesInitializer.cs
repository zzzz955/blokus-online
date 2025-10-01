using UnityEngine;
using App.Logging;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
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

                // PlayGamesClientConfiguration으로 자동 로그인 비활성화
                var config = new PlayGamesClientConfiguration.Builder()
                    // 서버 인증 코드 요청 (백엔드 JWT 교환용)
                    .RequestServerAuthCode(forceRefresh: false)
                    // 이메일 접근 권한 요청
                    .RequestEmail()
                    // ID 토큰 요청
                    .RequestIdToken()
                    .Build();

                AndroidLogger.LogAuth("PlayGamesClientConfiguration created with Web Client ID");

                // PlayGamesPlatform 초기화 (자동 로그인 없음)
                PlayGamesPlatform.InitializeInstance(config);
                PlayGamesPlatform.Activate();

                AndroidLogger.LogAuth("✅ Google Play Games initialized (auto-signin disabled)");
                AndroidLogger.LogAuth("Authentication will only occur when user explicitly clicks login button");
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