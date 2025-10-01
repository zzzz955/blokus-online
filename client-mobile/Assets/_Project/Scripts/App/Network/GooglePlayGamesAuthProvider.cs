using System;
using System.Threading.Tasks;
using UnityEngine;
using App.Logging;
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
                AndroidLogger.LogAuth("=== GooglePlayGamesAuthProvider.AuthenticateAsync START ===");
                AndroidLogger.LogAuth("Checking PlayGamesPlatform availability...");

                // PlayGamesPlatform.Instance 접근 전 로깅
                var instance = PlayGamesPlatform.Instance;
                AndroidLogger.LogAuth($"PlayGamesPlatform.Instance: {(instance != null ? "NOT NULL" : "NULL")}");

                // Google Play Games 로그인 시도
                AndroidLogger.LogAuth("Calling PlayGamesPlatform.Instance.Authenticate()...");
                instance.Authenticate((status) =>
                {
                    AndroidLogger.LogAuth($"Authenticate callback received - Status: {status}");

                    if (status == SignInStatus.Success)
                    {
                        AndroidLogger.LogAuth("✅ Authentication successful");
                        RequestServerAuthCode(tcs);
                    }
                    else
                    {
                        // 상세한 에러 메시지
                        string errorMessage;
                        switch (status)
                        {
                            case SignInStatus.Canceled:
                                errorMessage = "사용자가 Google 로그인을 취소했습니다";
                                AndroidLogger.LogAuth($"⚠️ Authentication canceled by user");
                                break;
                            case SignInStatus.DeveloperError:
                                errorMessage = "Google Play Games 설정 오류입니다. 개발자에게 문의하세요";
                                AndroidLogger.LogError($"❌ Developer error - Check OAuth client ID configuration in Google Play Console");
                                break;
                            case SignInStatus.InternalError:
                                errorMessage = "Google Play Services 내부 오류입니다";
                                AndroidLogger.LogError($"❌ Internal error - Google Play Services issue");
                                break;
                            case SignInStatus.NotAuthenticated:
                                errorMessage = "Google 인증에 실패했습니다";
                                AndroidLogger.LogError($"❌ Not authenticated");
                                break;
                            case SignInStatus.NetworkError:
                                errorMessage = "네트워크 연결을 확인해주세요";
                                AndroidLogger.LogError($"❌ Network error");
                                break;
                            default:
                                errorMessage = $"Google Play Games 인증 실패: {status}";
                                AndroidLogger.LogError($"❌ Authentication failed: {status}");
                                break;
                        }

                        tcs.SetResult(new AuthResult
                        {
                            Success = false,
                            ErrorMessage = errorMessage
                        });
                    }
                });

                AndroidLogger.LogAuth("Authenticate call initiated, waiting for callback...");
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                AndroidLogger.LogError($"❌ EXCEPTION in AuthenticateAsync: {ex.GetType().Name}");
                AndroidLogger.LogError($"Exception Message: {ex.Message}");
                AndroidLogger.LogError($"StackTrace: {ex.StackTrace}");
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
            AndroidLogger.LogAuth("Requesting server-side access code...");

            PlayGamesPlatform.Instance.RequestServerSideAccess(
                forceRefreshToken: false,
                code =>
                {
                    AndroidLogger.LogAuth($"RequestServerSideAccess callback received");
                    AndroidLogger.LogAuth($"Code is null or empty: {string.IsNullOrEmpty(code)}");

                    if (!string.IsNullOrEmpty(code))
                    {
                        AndroidLogger.LogAuth($"✅ Server auth code received (length: {code.Length})");
                        tcs.SetResult(new AuthResult
                        {
                            Success = true,
                            AuthCode = code
                        });
                    }
                    else
                    {
                        AndroidLogger.LogError("❌ Failed to get server auth code");
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