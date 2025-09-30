using System;
using System.Threading.Tasks;
using UnityEngine;

namespace App.Network
{
    /// <summary>
    /// Unity 에디터 테스트용 인증 제공자
    /// 에디터에서 Google Play Games 없이 인증 흐름을 테스트할 수 있습니다.
    /// </summary>
    public class EditorAuthProvider : IAuthenticationProvider
    {
        public async Task<AuthResult> AuthenticateAsync()
        {
            #if UNITY_EDITOR
            Debug.Log("[EditorAuthProvider] Simulating authentication...");

            // 실제 네트워크 지연 시뮬레이션
            await Task.Delay(500);

            // 테스트용 Auth Code 생성
            string testAuthCode = "editor_test_code_" + Guid.NewGuid().ToString();

            Debug.Log($"[EditorAuthProvider] Authentication successful (test code: {testAuthCode})");

            return new AuthResult
            {
                Success = true,
                AuthCode = testAuthCode
            };
            #else
            await Task.CompletedTask;
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "EditorAuthProvider is only available in Unity Editor"
            };
            #endif
        }

        public string GetProviderName()
        {
            return "Editor (Test)";
        }

        public bool IsAvailable()
        {
            #if UNITY_EDITOR
            return true;
            #else
            return false;
            #endif
        }
    }
}