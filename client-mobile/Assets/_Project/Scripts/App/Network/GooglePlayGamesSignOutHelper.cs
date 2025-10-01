using UnityEngine;
using App.Logging;

namespace App.Network
{
    /// <summary>
    /// Google Play Games 로그아웃 헬퍼
    /// Unity Plugin v2에서 SignOut이 제거되어 Android Native API를 직접 호출합니다.
    /// </summary>
    public static class GooglePlayGamesSignOutHelper
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject gamesSignInClient;
        #endif

        /// <summary>
        /// Google Play Games 로그아웃
        /// GPGS v2에서는 signOut이 제거되었으므로, 로컬 세션만 클리어합니다.
        /// 실제 Google 계정 로그아웃은 사용자가 기기 설정에서 수행해야 합니다.
        /// </summary>
        public static void SignOut()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidLogger.LogAuth("=== GooglePlayGamesSignOutHelper.SignOut START ===");
                AndroidLogger.LogAuth("⚠️ GPGS v2 does not support signOut");
                AndroidLogger.LogAuth("Clearing local session data only");

                // GPGS v2에서는 signOut이 제거되었으므로
                // 로컬 토큰/세션 정보만 클리어
                // 실제 Google 계정 연결 해제는 불가능

                // PlayerPrefs나 다른 로컬 저장소에서 토큰 삭제
                UnityEngine.PlayerPrefs.DeleteKey("RefreshToken");
                UnityEngine.PlayerPrefs.DeleteKey("AccessToken");
                UnityEngine.PlayerPrefs.Save();

                AndroidLogger.LogAuth("✅ Local session cleared (GPGS account still connected)");
                AndroidLogger.LogAuth("ℹ️ User must sign out from device settings to fully disconnect");
            }
            catch (System.Exception ex)
            {
                AndroidLogger.LogError($"❌ SignOut failed: {ex.Message}");
                AndroidLogger.LogError($"StackTrace: {ex.StackTrace}");
            }
            #else
            Debug.Log("[GooglePlayGamesSignOutHelper] SignOut is only available on Android devices");
            #endif
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        private class SignOutListener : AndroidJavaProxy
        {
            public SignOutListener() : base("com.google.android.gms.tasks.OnCompleteListener") { }

            public void onComplete(AndroidJavaObject task)
            {
                bool isSuccessful = task.Call<bool>("isSuccessful");

                if (isSuccessful)
                {
                    AndroidLogger.LogAuth("✅ Google Play Games signOut completed successfully");
                }
                else
                {
                    AndroidJavaObject exception = task.Call<AndroidJavaObject>("getException");
                    string errorMessage = exception != null ? exception.Call<string>("getMessage") : "Unknown error";
                    AndroidLogger.LogError($"❌ SignOut failed: {errorMessage}");
                }
            }
        }
        #endif
    }
}
