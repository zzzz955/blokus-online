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
                AndroidLogger.LogAuth($"Web Client ID: {GameInfo.WebClientId}");
                AndroidLogger.LogAuth($"Web Client ID initialized: {GameInfo.WebClientIdInitialized()}");

                // 패키지 정보 로깅 (디버깅용)
                LogPackageSignatureInfo();

                // Web Client ID 필수 체크
                if (!GameInfo.WebClientIdInitialized())
                {
                    AndroidLogger.LogError("❌ Web Client ID not configured!");
                    AndroidLogger.LogError("Please set WebClientId in GameInfo.cs");
                    AndroidLogger.LogError("Create Web Application OAuth Client ID in Google Cloud Console");
                    return;
                }

                // GPGS v2: Platform 활성화만 필요
                // CRITICAL: Web Client ID는 Unity Editor의 "Window → Google Play Games → Setup" 에서 설정
                // OAuth 스코프는 RequestServerSideAccess() 호출 시 지정 (GooglePlayGamesAuthProvider.cs)
                AndroidLogger.LogAuth("Activating PlayGamesPlatform (GPGS v2)...");
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

        #if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// APK 서명 정보를 로그에 출력 (SHA-1 인증서 확인용)
        /// </summary>
        private void LogPackageSignatureInfo()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    string packageName = currentActivity.Call<string>("getPackageName");
                    AndroidLogger.LogAuth($"Package Name: {packageName}");

                    // GET_SIGNATURES = 0x00000040
                    using (var packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0x00000040))
                    {
                        AndroidJavaObject[] signatures = packageInfo.Get<AndroidJavaObject[]>("signatures");

                        if (signatures != null && signatures.Length > 0)
                        {
                            using (var signature = signatures[0])
                            {
                                byte[] byteArray = signature.Call<byte[]>("toByteArray");

                                using (var messageDigestClass = new AndroidJavaClass("java.security.MessageDigest"))
                                using (var messageDigest = messageDigestClass.CallStatic<AndroidJavaObject>("getInstance", "SHA1"))
                                {
                                    // update 메서드는 void 반환 (Call<void> 사용)
                                    messageDigest.Call("update", byteArray);
                                    byte[] digest = messageDigest.Call<byte[]>("digest");

                                    // Convert byte array to hex string
                                    string sha1 = System.BitConverter.ToString(digest).Replace("-", ":");
                                    AndroidLogger.LogAuth($"📱 APK Signature SHA-1: {sha1}");
                                    AndroidLogger.LogAuth("위 SHA-1이 Google Cloud Console의 Android OAuth 클라이언트에 등록되어 있어야 합니다");
                                    AndroidLogger.LogAuth("✅ 등록된 SHA-1 #1: 9B:80:F0:55:FF:CF:58:BD:6C:3D:BA:5B:53:11:88:85:F9:22:7C:1E (로컬 키스토어)");
                                    AndroidLogger.LogAuth("✅ 등록된 SHA-1 #2: A0:F0:4C:BF:8A:52:A7:AE:4F:66:5B:74:77:DF:3F:7E:BB:8A:9C:59 (플레이 콘솔)");

                                    // 매칭 여부 확인
                                    if (sha1 == "9B:80:F0:55:FF:CF:58:BD:6C:3D:BA:5B:53:11:88:85:F9:22:7C:1E")
                                    {
                                        AndroidLogger.LogAuth("✅ SHA-1 일치: 로컬 키스토어 (Android1 OAuth 클라이언트)");
                                    }
                                    else if (sha1 == "A0:F0:4C:BF:8A:52:A7:AE:4F:66:5B:74:77:DF:3F:7E:BB:8A:9C:59")
                                    {
                                        AndroidLogger.LogAuth("✅ SHA-1 일치: 플레이 콘솔 서명 (Android2 OAuth 클라이언트)");
                                    }
                                    else
                                    {
                                        AndroidLogger.LogError($"❌ SHA-1 불일치! 이 인증서는 Google Cloud Console에 등록되지 않았습니다");
                                        AndroidLogger.LogError("해결 방법: Google Cloud Console → APIs & Services → Credentials");
                                        AndroidLogger.LogError("Android OAuth 클라이언트에 위 SHA-1 추가 후 5-10분 대기");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                AndroidLogger.LogError($"❌ Failed to get package signature: {ex.Message}");
                AndroidLogger.LogError("SHA-1 자동 확인 실패 - 수동으로 확인하세요:");
                AndroidLogger.LogError("명령어: keytool -list -printcert -jarfile your_app.apk");
            }
        }
        #endif
    }
}