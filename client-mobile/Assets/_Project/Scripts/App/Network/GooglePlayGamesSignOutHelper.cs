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
        /// </summary>
        public static void SignOut()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidLogger.LogAuth("=== GooglePlayGamesSignOutHelper.SignOut START ===");

                // Unity Activity 가져오기
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        // GamesSignInClient 가져오기
                        using (AndroidJavaClass playGames = new AndroidJavaClass("com.google.android.gms.games.PlayGames"))
                        {
                            AndroidJavaObject signInClient = playGames.CallStatic<AndroidJavaObject>("getGamesSignInClient", activity);

                            // signOut() 호출
                            AndroidJavaObject task = signInClient.Call<AndroidJavaObject>("signOut");

                            // Task 완료 대기 (비동기)
                            task.Call<AndroidJavaObject>("addOnCompleteListener", new SignOutListener());

                            AndroidLogger.LogAuth("✅ Google Play Games signOut initiated");
                        }
                    }
                }
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
