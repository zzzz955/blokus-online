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
        /// CRITICAL FIX: 계정 선택 UI 표시로 계정 전환 지원
        /// </summary>
        public async Task<AuthResult> AuthenticateAsync()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<AuthResult>();

            try
            {
                AndroidLogger.LogAuth("=== GooglePlayGamesAuthProvider.AuthenticateAsync START ===");
                AndroidLogger.LogAuth("Mode: Interactive - Account picker for login and account switching");

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

                // 현재 인증 상태 확인
                bool isAlreadyAuthenticated = instance.IsAuthenticated();
                AndroidLogger.LogAuth($"Current authentication status: {(isAlreadyAuthenticated ? "AUTHENTICATED" : "NOT AUTHENTICATED")}");

                if (isAlreadyAuthenticated)
                {
                    var localUser = instance.localUser;
                    AndroidLogger.LogAuth($"Already authenticated - User: {localUser?.userName ?? "NULL"} ({localUser?.id ?? "NULL"})");

                    // RefreshToken 삭제 (앱 서버 세션 해제)
                    // OPEN_ID 플래그는 유지 (GPGS SDK가 계정별로 관리)
                    PlayerPrefs.DeleteKey("RefreshToken");
                    PlayerPrefs.DeleteKey("AccessToken");
                    PlayerPrefs.Save();
                    AndroidLogger.LogAuth("✅ App session cleared - GPGS session maintained");
                }

                // ManuallyAuthenticate: 계정 선택 UI 표시
                AndroidLogger.LogAuth("Showing account picker for login/account switching");

                instance.ManuallyAuthenticate((success) =>
                {
                    AndroidLogger.LogAuth($"ManuallyAuthenticate callback received - Status: {success}");

                    if (success == GooglePlayGames.BasicApi.SignInStatus.Success)
                    {
                        AndroidLogger.LogAuth("✅ Authentication successful");

                        // Play Games Player ID 가져오기 (Web Client ID 불필요)
                        var localUser = instance.localUser;
                        string playerId = localUser?.id;
                        string playerName = localUser?.userName;

                        if (string.IsNullOrEmpty(playerId))
                        {
                            AndroidLogger.LogError("❌ Player ID is null or empty");
                            tcs.TrySetResult(new AuthResult
                            {
                                Success = false,
                                ErrorMessage = "Failed to get Player ID"
                            });
                            return;
                        }

                        AndroidLogger.LogAuth($"✅ Player ID: {playerId}");
                        AndroidLogger.LogAuth($"✅ Player Name: {playerName}");
                        AndroidLogger.LogAuth("🎮 Using Play Games Player ID for authentication (no OAuth required)");

                        // Player ID를 AuthCode 대신 전달
                        tcs.TrySetResult(new AuthResult
                        {
                            Success = true,
                            AuthCode = playerId  // Player ID를 AuthCode 필드에 전달
                        });
                    }
                    else
                    {
                        AndroidLogger.LogError($"❌ Authentication failed: {success}");
                        tcs.TrySetResult(new AuthResult
                        {
                            Success = false,
                            ErrorMessage = $"Authentication failed: {success}"
                        });
                    }
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
        /// CRITICAL: GPGS v2에서 추가 스코프(OPEN_ID, EMAIL, PROFILE) 명시적 요청 필수
        /// - 기본 스코프(games_lite, drive.appdata)만으로는 id_token을 받을 수 없음
        /// - 사용자 동의 필요 (SDK가 자동으로 동의 UI 표시)
        /// </summary>
        private void RequestServerAuthCodeWithThreadTimeout(
            TaskCompletionSource<AuthResult> tcs,
            Action onGranted,
            int timeoutMs = 20000)
        {
            AndroidLogger.LogAuth("Requesting server-side access with OAuth scopes: OPEN_ID, EMAIL, PROFILE");

            bool isCompleted = false;
            object lockObject = new object();

            // CRITICAL: 추가 스코프 명시 필수
            // 이 스코프들이 승인되어야 서버에서 id_token을 받을 수 있음
            var scopes = new System.Collections.Generic.List<GooglePlayGames.BasicApi.AuthScope>
            {
                GooglePlayGames.BasicApi.AuthScope.OPEN_ID,   // id_token 발급 필수
                GooglePlayGames.BasicApi.AuthScope.EMAIL,     // 이메일 정보
                GooglePlayGames.BasicApi.AuthScope.PROFILE    // 프로필 정보
            };

            AndroidLogger.LogAuth("📱 Initiating OAuth consent flow...");
            AndroidLogger.LogAuth("📱 If consent UI appears, user must accept scopes for login to succeed");
            AndroidLogger.LogAuth($"📱 Timeout: {timeoutMs}ms - waiting for user consent or callback");

            // GPGS v2 정식 시그니처: (bool forceRefreshToken, List<AuthScope> scopes, Action<AuthResponse> callback)
            PlayGamesPlatform.Instance.RequestServerSideAccess(
                forceRefreshToken: false,
                scopes: scopes,
                callback: authResponse =>
                {
                    lock (lockObject)
                    {
                        if (isCompleted)
                        {
                            AndroidLogger.LogAuth("⚠️ Callback received after completion (ignored)");
                            return;
                        }

                        AndroidLogger.LogAuth("✅ RequestServerSideAccess callback received");
                        AndroidLogger.LogAuth("📱 User completed OAuth consent flow (accepted or denied)");

                        string code = authResponse?.GetAuthCode();
                        if (string.IsNullOrEmpty(code))
                        {
                            AndroidLogger.LogError("❌ Server auth code is null or empty");
                            isCompleted = true;
                            tcs.TrySetResult(new AuthResult
                            {
                                Success = false,
                                ErrorMessage = "Empty server auth code"
                            });
                            return;
                        }

                        // 승인된 스코프 검사 및 로깅
                        var grantedScopes = authResponse.GetGrantedScopes();
                        AndroidLogger.LogAuth($"✅ Granted scopes count: {grantedScopes?.Count ?? 0}");

                        if (grantedScopes != null)
                        {
                            foreach (var s in grantedScopes)
                                AndroidLogger.LogAuth($"  - Scope: {s}");
                        }

                        // openid 포함 여부 확인 (중요: 서버에서 id_token 받으려면 필수)
                        bool hasOpenId  = grantedScopes?.Contains(GooglePlayGames.BasicApi.AuthScope.OPEN_ID) ?? false;
                        bool hasEmail   = grantedScopes?.Contains(GooglePlayGames.BasicApi.AuthScope.EMAIL)   ?? false;
                        bool hasProfile = grantedScopes?.Contains(GooglePlayGames.BasicApi.AuthScope.PROFILE) ?? false;

                        AndroidLogger.LogAuth($"OPEN_ID: {hasOpenId}, EMAIL: {hasEmail}, PROFILE: {hasProfile}");

                        if (!hasOpenId)
                        {
                            AndroidLogger.LogError("❌ OPEN_ID scope not granted - server will not receive id_token");
                            AndroidLogger.LogError("User denied consent or scope request failed");
                        }

                        AndroidLogger.LogAuth($"✅ Server auth code received (length: {code.Length})");
                        onGranted?.Invoke(); // 플래그 저장 콜백

                        isCompleted = true;
                        AndroidLogger.LogAuth("✅ Setting success result");
                        tcs.TrySetResult(new AuthResult
                        {
                            Success = true,
                            AuthCode = code
                        });
                    }
                });

            // 스레드 기반 타임아웃 (Unity 메인 루프 정지에도 동작)
            Task.Run(async () =>
            {
                await Task.Delay(timeoutMs);

                lock (lockObject)
                {
                    if (!isCompleted)
                    {
                        AndroidLogger.LogAuth($"⚠️ RequestServerSideAccess THREAD timeout ({timeoutMs}ms)");
                        AndroidLogger.LogAuth("❌ OAuth consent UI did not complete within timeout period");
                        AndroidLogger.LogAuth("Possible causes:");
                        AndroidLogger.LogAuth("  1. OAuth consent UI did not appear");
                        AndroidLogger.LogAuth("  2. Web Client ID not configured in games-ids.xml");
                        AndroidLogger.LogAuth("  3. Google Play Services outdated or incompatible");
                        AndroidLogger.LogAuth("  4. Network connectivity issues");
                        isCompleted = true;
                        tcs.TrySetResult(new AuthResult
                        {
                            Success = false,
                            ErrorMessage = "Server auth request timeout (try interactive sign-in)"
                        });
                    }
                    else
                    {
                        AndroidLogger.LogAuth("✅ Request completed before timeout - no action needed");
                    }
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