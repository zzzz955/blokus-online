using UnityEngine;

namespace App.Network
{
    /// <summary>
    /// Unity 모바일 앱의 Deep Link 설정을 위한 헬퍼 클래스
    /// Android: Intent Filter를 통한 Custom URL Scheme 처리
    /// iOS: URL Types를 통한 Custom URL Scheme 처리
    /// </summary>
    public static class DeepLinkConfiguration
    {
        /// <summary>
        /// Deep Link URL Scheme
        /// </summary>
        public const string URL_SCHEME = "blokus";
        
        /// <summary>
        /// OIDC Callback Deep Link
        /// </summary>
        public const string OIDC_CALLBACK_URL = "blokus://auth/callback";
        
        /// <summary>
        /// Deep Link 설정 가이드를 로그로 출력
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void LogDeepLinkConfiguration()
        {
            Debug.Log("=== Blokus Deep Link Configuration ===");
            Debug.Log($"URL Scheme: {URL_SCHEME}");
            Debug.Log($"OIDC Callback: {OIDC_CALLBACK_URL}");
            
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("Android Deep Link Setup Required:");
            Debug.Log("1. Add to AndroidManifest.xml in <activity android:name=\"com.unity3d.player.UnityPlayerActivity\">:");
            Debug.Log("   <intent-filter>");
            Debug.Log($"     <action android:name=\"android.intent.action.VIEW\" />");
            Debug.Log($"     <category android:name=\"android.intent.category.DEFAULT\" />");
            Debug.Log($"     <category android:name=\"android.intent.category.BROWSABLE\" />");
            Debug.Log($"     <data android:scheme=\"{URL_SCHEME}\" />");
            Debug.Log("   </intent-filter>");
            
#elif UNITY_IOS && !UNITY_EDITOR
            Debug.Log("iOS Deep Link Setup Required:");
            Debug.Log("1. Add to Info.plist in CFBundleURLTypes array:");
            Debug.Log("   <dict>");
            Debug.Log("     <key>CFBundleURLName</key>");
            Debug.Log("     <string>blokus-auth</string>");
            Debug.Log("     <key>CFBundleURLSchemes</key>");
            Debug.Log("     <array>");
            Debug.Log($"       <string>{URL_SCHEME}</string>");
            Debug.Log("     </array>");
            Debug.Log("   </dict>");
#endif
            
            Debug.Log("==========================================");
        }
        
        /// <summary>
        /// Deep Link URL 검증
        /// </summary>
        /// <param name="url">검증할 URL</param>
        /// <returns>유효한 Deep Link인지 여부</returns>
        public static bool IsValidDeepLink(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;
                
            return url.StartsWith($"{URL_SCHEME}://");
        }
        
        /// <summary>
        /// OIDC Callback URL 검증
        /// </summary>
        /// <param name="url">검증할 URL</param>
        /// <returns>OIDC Callback URL인지 여부</returns>
        public static bool IsOidcCallback(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;
                
            return url.StartsWith($"{URL_SCHEME}://auth/callback");
        }
    }
}