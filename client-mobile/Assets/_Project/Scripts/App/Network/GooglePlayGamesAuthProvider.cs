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
    /// Player ID 기반 인증 방식 사용 (OAuth 불필요)
    /// </summary>
    public class GooglePlayGamesAuthProvider : IAuthenticationProvider
    {
        /// <summary>
        /// Silent sign-in: 이전에 로그인한 계정으로 자동 로그인 시도 (UI 없음)
        /// Player ID 기반 인증 사용 (OAuth 불필요)
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

                        // Play Games Player ID 가져오기 (OAuth 불필요)
                        var localUser = instance.localUser;
                        string playerId = localUser?.id;
                        string playerName = localUser?.userName;

                        if (string.IsNullOrEmpty(playerId))
                        {
                            AndroidLogger.LogError("❌ Player ID is null or empty");
                            tcs.SetResult(new AuthResult
                            {
                                Success = false,
                                ErrorMessage = "Failed to get Player ID"
                            });
                            return;
                        }

                        AndroidLogger.LogAuth($"✅ Player ID: {playerId}");
                        AndroidLogger.LogAuth($"✅ Player Name: {playerName}");
                        AndroidLogger.LogAuth("🎮 Using Play Games Player ID for silent authentication (no OAuth required)");

                        // Player ID와 Player Name을 JSON 형태로 전달
                        var authData = new System.Collections.Generic.Dictionary<string, string>
                        {
                            { "player_id", playerId },
                            { "player_name", playerName }
                        };
                        string authJson = Newtonsoft.Json.JsonConvert.SerializeObject(authData);

                        AndroidLogger.LogAuth($"🔍 DEBUG - Serialized JSON: {authJson}");
                        AndroidLogger.LogAuth($"🔍 DEBUG - JSON length: {authJson?.Length ?? 0}");

                        tcs.SetResult(new AuthResult
                        {
                            Success = true,
                            AuthCode = authJson  // JSON 형태로 전달
                        });
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

                        // Player ID와 Player Name을 JSON 형태로 전달 (Dictionary 사용)
                        var authData = new System.Collections.Generic.Dictionary<string, string>
                        {
                            { "player_id", playerId },
                            { "player_name", playerName }
                        };
                        string authJson = Newtonsoft.Json.JsonConvert.SerializeObject(authData);

                        AndroidLogger.LogAuth($"🔍 DEBUG - Serialized JSON: {authJson}");
                        AndroidLogger.LogAuth($"🔍 DEBUG - JSON length: {authJson?.Length ?? 0}");

                        tcs.TrySetResult(new AuthResult
                        {
                            Success = true,
                            AuthCode = authJson  // JSON 형태로 전달
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