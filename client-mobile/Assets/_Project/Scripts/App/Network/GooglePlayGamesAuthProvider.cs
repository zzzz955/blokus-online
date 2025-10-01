using System;
using System.Threading.Tasks;
using UnityEngine;
using App.Logging;
#if UNITY_ANDROID
using GooglePlayGames;
#endif

namespace App.Network
{
    /// <summary>
    /// Google Play Games 인증 제공자
    /// Android 디바이스에서 Google Play Games를 통한 인증을 처리합니다.
    /// </summary>
    public class GooglePlayGamesAuthProvider : IAuthenticationProvider
    {
        /// <summary>
        /// Silent sign-in: 이전에 로그인한 계정으로 자동 로그인 시도 (UI 없음)
        /// </summary>
        public async Task<AuthResult> AuthenticateSilentAsync()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AuthResult>();

            try
            {
                AndroidLogger.LogAuth("=== GooglePlayGamesAuthProvider.AuthenticateSilentAsync START ===");
                AndroidLogger.LogAuth("Attempting silent sign-in (no UI)...");

                var instance = PlayGamesPlatform.Instance;
                if (instance == null)
                {
                    AndroidLogger.LogError("❌ PlayGamesPlatform.Instance is NULL");
                    return new AuthResult
                    {
                        Success = false,
                        ErrorMessage = "PlayGamesPlatform not initialized"
                    };
                }

                // Authenticate() 호출: Silent sign-in 시도
                // 성공하면 RequestServerAuthCode 호출, 실패하면 조용히 실패 처리
                instance.Authenticate((success) =>
                {
                    AndroidLogger.LogAuth($"Silent sign-in callback received - Success: {success}");

                    if (success == GooglePlayGames.BasicApi.SignInStatus.Success)
                    {
                        AndroidLogger.LogAuth("✅ Silent sign-in successful");
                        RequestServerAuthCode(tcs);
                    }
                    else
                    {
                        // Silent sign-in 실패는 정상적인 상황 (이전 로그인 없음)
                        AndroidLogger.LogAuth($"Silent sign-in failed (expected if no previous login): {success}");
                        tcs.SetResult(new AuthResult
                        {
                            Success = false,
                            ErrorMessage = $"Silent sign-in failed: {success}"
                        });
                    }
                });

                AndroidLogger.LogAuth("Silent sign-in initiated, waiting for callback...");
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                AndroidLogger.LogError($"❌ EXCEPTION in AuthenticateSilentAsync: {ex.GetType().Name}");
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

        /// <summary>
        /// Interactive sign-in: 사용자가 명시적으로 버튼을 클릭했을 때 계정 선택 UI 표시
        /// </summary>
        public async Task<AuthResult> AuthenticateAsync()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AuthResult>();

            try
            {
                AndroidLogger.LogAuth("=== GooglePlayGamesAuthProvider.AuthenticateAsync START ===");
                AndroidLogger.LogAuth("Mode: Interactive (UI will be shown)");

                AndroidLogger.LogAuth("Checking PlayGamesPlatform availability...");
                var instance = PlayGamesPlatform.Instance;
                AndroidLogger.LogAuth($"PlayGamesPlatform.Instance: {(instance != null ? "NOT NULL" : "NULL")}");

                if (instance == null)
                {
                    AndroidLogger.LogError("❌ PlayGamesPlatform.Instance is NULL - Platform not initialized");
                    return new AuthResult
                    {
                        Success = false,
                        ErrorMessage = "PlayGamesPlatform not initialized"
                    };
                }

                // 현재 인증 상태 확인
                bool isAlreadyAuthenticated = instance.IsAuthenticated();
                AndroidLogger.LogAuth($"Current authentication status: {(isAlreadyAuthenticated ? "AUTHENTICATED" : "NOT AUTHENTICATED")}");

                if (isAlreadyAuthenticated)
                {
                    AndroidLogger.LogAuth("Already authenticated, getting local user info...");
                    var localUser = instance.localUser;
                    AndroidLogger.LogAuth($"Local user ID: {localUser?.id ?? "NULL"}");
                    AndroidLogger.LogAuth($"Local user name: {localUser?.userName ?? "NULL"}");
                }

                // ManuallyAuthenticate() 사용: 사용자 명시적 액션으로 인증 시작 (UI 표시)
                AndroidLogger.LogAuth("Calling PlayGamesPlatform.Instance.ManuallyAuthenticate()...");
                AndroidLogger.LogAuth("Account selection UI should appear now...");

                instance.ManuallyAuthenticate((success) =>
                {
                    AndroidLogger.LogAuth($"ManuallyAuthenticate callback received");
                    AndroidLogger.LogAuth($"SignInStatus enum value: {success}");
                    AndroidLogger.LogAuth($"SignInStatus string: {success.ToString()}");

                    if (success == GooglePlayGames.BasicApi.SignInStatus.Success)
                    {
                        AndroidLogger.LogAuth("✅ Authentication successful");

                        // 인증 성공 후 사용자 정보 확인
                        var localUser = instance.localUser;
                        AndroidLogger.LogAuth($"Authenticated user ID: {localUser?.id ?? "NULL"}");
                        AndroidLogger.LogAuth($"Authenticated user name: {localUser?.userName ?? "NULL"}");
                        AndroidLogger.LogAuth($"Is authenticated: {instance.IsAuthenticated()}");

                        RequestServerAuthCode(tcs);
                    }
                    else
                    {
                        // 상세한 에러 메시지
                        string errorMessage;
                        switch (success)
                        {
                            case GooglePlayGames.BasicApi.SignInStatus.Canceled:
                                errorMessage = "사용자가 Google 로그인을 취소했습니다";
                                AndroidLogger.LogAuth($"⚠️ Authentication canceled by user");
                                AndroidLogger.LogAuth("User pressed back button or dismissed account selection dialog");
                                break;
                            case GooglePlayGames.BasicApi.SignInStatus.InternalError:
                                errorMessage = "Google Play Services 내부 오류입니다";
                                AndroidLogger.LogError($"❌ Internal error - Google Play Services issue");
                                AndroidLogger.LogError("Possible causes: outdated Play Services, configuration error, or network issue");
                                break;
                            default:
                                errorMessage = $"Google Play Games 인증 실패: {success}";
                                AndroidLogger.LogError($"❌ Authentication failed with status: {success}");
                                AndroidLogger.LogError($"Status code: {(int)success}");
                                AndroidLogger.LogError("Check: OAuth Client ID, SHA-1 certificate, package name in Google Cloud Console");
                                break;
                        }

                        tcs.SetResult(new AuthResult
                        {
                            Success = false,
                            ErrorMessage = errorMessage
                        });
                    }
                });

                AndroidLogger.LogAuth("ManuallyAuthenticate call initiated, waiting for callback...");
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