using System;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace App.Network
{
    /// <summary>
    /// Google Play Games 인증 제공자
    /// Android 디바이스에서 Google Play Games를 통한 인증을 처리합니다.
    /// </summary>
    public class GooglePlayGamesAuthProvider : IAuthenticationProvider
    {
        public async Task<AuthResult> AuthenticateAsync()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AuthResult>();

            try
            {
                // Google Play Games 로그인 시도
                PlayGamesPlatform.Instance.Authenticate((status) =>
                {
                    if (status == SignInStatus.Success)
                    {
                        Debug.Log("[GooglePlayGamesAuthProvider] Authentication successful");
                        RequestServerAuthCode(tcs);
                    }
                    else
                    {
                        Debug.LogError($"[GooglePlayGamesAuthProvider] Authentication failed: {status}");
                        tcs.SetResult(new AuthResult
                        {
                            Success = false,
                            ErrorMessage = $"Google Play Games authentication failed: {status}"
                        });
                    }
                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GooglePlayGamesAuthProvider] Exception: {ex.Message}");
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
            #else
            await Task.CompletedTask;
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "Google Play Games is only available on Android devices"
            };
            #endif
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        private void RequestServerAuthCode(TaskCompletionSource<AuthResult> tcs)
        {
            PlayGamesPlatform.Instance.RequestServerSideAccess(
                forceRefreshToken: false,
                code =>
                {
                    if (!string.IsNullOrEmpty(code))
                    {
                        Debug.Log("[GooglePlayGamesAuthProvider] Server auth code received");
                        tcs.SetResult(new AuthResult
                        {
                            Success = true,
                            AuthCode = code
                        });
                    }
                    else
                    {
                        Debug.LogError("[GooglePlayGamesAuthProvider] Failed to get server auth code");
                        tcs.SetResult(new AuthResult
                        {
                            Success = false,
                            ErrorMessage = "Failed to retrieve server auth code"
                        });
                    }
                }
            );
        }
        #endif

        public string GetProviderName()
        {
            return "GooglePlayGames";
        }

        public bool IsAvailable()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            return true;
            #else
            return false;
            #endif
        }
    }
}