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
            // Google Play Games SDK v11.01+ 최신 API 사용
            PlayGamesPlatform.Activate();

            Debug.Log("[PlayGamesInitializer] Google Play Games activated");
            #else
            Debug.Log("[PlayGamesInitializer] Google Play Games skipped (not Android device)");
            #endif
        }
    }
}