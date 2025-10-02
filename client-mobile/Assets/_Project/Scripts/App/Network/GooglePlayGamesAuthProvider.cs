using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using App.Logging;
using App.Core;
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
        #if UNITY_ANDROID && !UNITY_EDITOR
        private const string OPENID_GRANTED_KEY = "pgs_openid_granted";
        #endif

        /// <summary>
        /// Silent sign-in: 이전에 로그인한 계정으로 자동 로그인 시도 (UI 없음)
        /// CRITICAL: 새로운 스코프 요청 금지 - Unity 메인 루프 정지 방지
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
                instance.Authenticate((success) =>
                {
                    AndroidLogger.LogAuth($"Silent sign-in callback received - Success: {success}");

                    if (success == GooglePlayGames.BasicApi.SignInStatus.Success)
                    {
                        AndroidLogger.LogAuth("✅ Silent sign-in successful");

                        // CRITICAL: 로컬 플래그 확인 - 이미 동의했는지 체크
                        bool openIdGranted = PlayerPrefs.GetInt(OPENID_GRANTED_KEY, 0) == 1;
                        AndroidLogger.LogAuth($"OPEN_ID granted flag: {openIdGranted}");

                        if (!openIdGranted)
                        {
                            // 아직 동의 안 함 → 스코프 요청 금지 (Unity 메인 루프 정지 방지)
                            AndroidLogger.LogAuth("⚠️ OPEN_ID 미동의 → 서버 코드 요청 스킵");
                            AndroidLogger.LogAuth("해결: 로그인 버튼 클릭 → Interactive sign-in → 동의 UI");
                            tcs.SetResult(new AuthResult
                            {
                                Success = false,
                                ErrorMessage = "Interactive consent required (OPEN_ID)"
                            });
                        }
                        else
                        {
                            // 이미 동의된 기기 → 안전하게 서버 코드 요청 가능
                            AndroidLogger.LogAuth("✅ OPEN_ID 사전 동의 확인 → 서버 코드 요청");
                            RequestServerAuthCodeWithThreadTimeout(tcs, onGranted: null);
                        }
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
        /// Interactive sign-in: 사용자가 명시적으로 버튼을 클릭했을 때 OPEN_ID 동의 UI 표시
        /// CRITICAL FIX: ManuallyAuthenticate 제거 - 이미 인증된 상태에서 UI 세션 종료 후 스코프 요청 시 블로킹 방지
        /// </summary>
        public async Task<AuthResult> AuthenticateAsync()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AuthResult>();

            try
            {
                AndroidLogger.LogAuth("=== GooglePlayGamesAuthProvider.AuthenticateAsync START ===");
                AndroidLogger.LogAuth("Mode: Interactive - Direct scope request (no ManuallyAuthenticate)");

                var instance = PlayGamesPlatform.Instance;
                if (instance == null)
                {
                    AndroidLogger.LogError("❌ PlayGamesPlatform.Instance is NULL - Platform not initialized");
                    return new AuthResult
                    {
                        Success = false,
                        ErrorMessage = "PlayGamesPlatform not initialized"
                    };
                }

                // 현재 인증 상태 확인 (로깅용)
                bool isAlreadyAuthenticated = instance.IsAuthenticated();
                AndroidLogger.LogAuth($"Current authentication status: {(isAlreadyAuthenticated ? "AUTHENTICATED" : "NOT AUTHENTICATED")}");

                if (isAlreadyAuthenticated)
                {
                    var localUser = instance.localUser;
                    AndroidLogger.LogAuth($"Already authenticated - User: {localUser?.userName ?? "NULL"} ({localUser?.id ?? "NULL"})");
                }

                // CRITICAL FIX: ManuallyAuthenticate 제거
                // 이유: Silent sign-in으로 이미 인증된 상태에서 ManuallyAuthenticate 호출 시
                //       즉시 Success 콜백 반환 (UI 세션 종료)
                //       → 그 후 RequestServerSideAccess로 동의 UI 표시 시도 → 컨텍스트 없어 블로킹
                //
                // 해결: RequestServerSideAccess를 직접 호출
                //       → SDK가 필요한 경우 자동으로 인증 + 동의 UI 통합 표시
                //       → 이미 인증된 경우 동의 UI만 표시
                AndroidLogger.LogAuth("Directly requesting server auth code with OPEN_ID scope");
                AndroidLogger.LogAuth("SDK will handle authentication + consent UI as needed");

                RequestServerAuthCodeWithThreadTimeout(tcs, onGranted: () =>
                {
                    // 스코프 동의 성공 → 로컬 플래그 저장
                    PlayerPrefs.SetInt(OPENID_GRANTED_KEY, 1);
                    PlayerPrefs.Save();
                    AndroidLogger.LogAuth("✅ OPEN_ID 동의 완료 → 플래그 저장");
                });

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
        /// <summary>
        /// 서버 코드 요청 with 스레드 기반 타임아웃
        /// Unity 메인 루프 정지에도 타임아웃 동작 보장
        /// CRITICAL: GPGS v2에서 OAuth 스코프 명시적 요청 필요
        /// </summary>
        private void RequestServerAuthCodeWithThreadTimeout(
            TaskCompletionSource<AuthResult> tcs,
            Action onGranted,
            int timeoutMs = 5000)
        {
            AndroidLogger.LogAuth("Requesting server-side access with OAuth scopes (openid, email, profile)...");

            var innerTcs = new TaskCompletionSource<AuthResult>();

            // CRITICAL: GPGS v2에서 OAuth 스코프 명시적 요청
            // AuthScope enum을 사용하여 openid, email, profile 스코프 요청
            var scopes = new System.Collections.Generic.List<GooglePlayGames.BasicApi.AuthScope>
            {
                GooglePlayGames.BasicApi.AuthScope.OPEN_ID,
                GooglePlayGames.BasicApi.AuthScope.EMAIL,
                GooglePlayGames.BasicApi.AuthScope.PROFILE
            };

            PlayGamesPlatform.Instance.RequestServerSideAccess(
                forceRefreshToken: false,
                scopes,
                authResponse =>
                {
                    AndroidLogger.LogAuth("RequestServerSideAccess callback received");

                    string code = authResponse?.GetAuthCode();
                    if (string.IsNullOrEmpty(code))
                    {
                        AndroidLogger.LogError("❌ Server auth code is null or empty");
                        innerTcs.TrySetResult(new AuthResult
                        {
                            Success = false,
                            ErrorMessage = "Empty server auth code"
                        });
                        return;
                    }

                    // 승인된 스코프 로깅
                    var grantedScopes = authResponse.GetGrantedScopes();
                    AndroidLogger.LogAuth($"✅ Granted scopes count: {grantedScopes?.Count ?? 0}");
                    if (grantedScopes != null)
                    {
                        foreach (var scope in grantedScopes)
                        {
                            AndroidLogger.LogAuth($"  - Scope: {scope}");
                        }
                    }

                    AndroidLogger.LogAuth($"✅ Server auth code received (length: {code.Length})");
                    onGranted?.Invoke(); // 플래그 저장 콜백
                    innerTcs.TrySetResult(new AuthResult
                    {
                        Success = true,
                        AuthCode = code
                    });
                });

            // 스레드 기반 타임아웃 (Unity 메인 루프 정지에도 동작)
            Task.Run(async () =>
            {
                var completed = await Task.WhenAny(innerTcs.Task, Task.Delay(timeoutMs));
                if (completed != innerTcs.Task)
                {
                    AndroidLogger.LogAuth($"⚠️ RequestServerSideAccess THREAD timeout ({timeoutMs}ms)");
                    AndroidLogger.LogAuth("Unity 메인 루프 정지 가능성 - 스레드 타임아웃 동작");
                    tcs.TrySetResult(new AuthResult
                    {
                        Success = false,
                        ErrorMessage = "Server auth request timeout (try interactive sign-in)"
                    });
                }
                else
                {
                    var result = await innerTcs.Task;
                    AndroidLogger.LogAuth($"RequestServerSideAccess completed: {result.Success}");
                    tcs.TrySetResult(result);
                }
            });
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