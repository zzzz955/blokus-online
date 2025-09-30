using UnityEngine;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace App.Core
{
    /// <summary>
    /// Google Play Games 플랫폼 초기화
    /// 게임 시작 시 자동으로 Google Play Games 서비스를 활성화합니다.
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
            PlayGamesClientConfiguration config = new PlayGamesClientConfiguration.Builder()
                .RequestServerAuthCode(false)  // 서버 인증 코드 요청
                .RequestEmail()                 // 이메일 요청
                .RequestIdToken()               // ID 토큰 요청
                .Build();

            PlayGamesPlatform.InitializeInstance(config);
            PlayGamesPlatform.Activate();

            Debug.Log("[PlayGamesInitializer] Google Play Games initialized and activated");
            #else
            Debug.Log("[PlayGamesInitializer] Google Play Games skipped (not Android device)");
            #endif
        }
    }
}