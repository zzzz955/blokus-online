using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace App.Config
{
    /// <summary>
    /// 환경변수 기반 설정 관리
    /// UNITY_EDITOR: localhost 하드코딩
    /// BUILD: 루트 .env 파일 참조
    /// </summary>
    public static class EnvironmentConfig
    {
        private static bool? _isDevelopment = null;
        private static Dictionary<string, string> _envVariables = null;
        private static bool _envLoaded = false;

        /// <summary>
        /// 개발 환경 여부 확인
        /// </summary>
        public static bool IsDevelopment
        {
            get
            {
                if (_isDevelopment == null)
                {
#if UNITY_EDITOR
                    _isDevelopment = true;
#elif DEVELOPMENT_BUILD
                    _isDevelopment = true;
#else
                    _isDevelopment = false;
#endif
                }
                return _isDevelopment.Value;
            }
        }

        /// <summary>
        /// .env 파일에서 환경변수 로드 (빌드용)
        /// </summary>
        private static void LoadEnvironmentVariables()
        {
            if (_envLoaded) return;
            
            _envVariables = new Dictionary<string, string>();
            
#if !UNITY_EDITOR
            try
            {
                // 빌드된 앱의 StreamingAssets에서 .env 파일 읽기
                string envPath = Path.Combine(Application.streamingAssetsPath, ".env");
                
                if (File.Exists(envPath))
                {
                    string[] lines = File.ReadAllLines(envPath);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                            continue;
                            
                        int equalIndex = line.IndexOf('=');
                        if (equalIndex > 0)
                        {
                            string key = line.Substring(0, equalIndex).Trim();
                            string value = line.Substring(equalIndex + 1).Trim();
                            _envVariables[key] = value;
                        }
                    }
                    Debug.Log($"✅ 환경변수 로드 완료: {_envVariables.Count}개 변수");
                }
                else
                {
                    Debug.LogWarning($"⚠️ .env 파일을 찾을 수 없습니다: {envPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ 환경변수 로드 실패: {e.Message}");
            }
#endif
            _envLoaded = true;
        }

        /// <summary>
        /// 환경변수 값 가져오기
        /// </summary>
        private static string GetEnvVariable(string key, string defaultValue = "")
        {
#if UNITY_EDITOR
            // 에디터에서는 하드코딩된 개발값 사용
            return defaultValue;
#else
            LoadEnvironmentVariables();
            return _envVariables.ContainsKey(key) ? _envVariables[key] : defaultValue;
#endif
        }

        /// <summary>
        /// 웹 서버 URL
        /// </summary>
        public static string WebServerUrl
        {
            get
            {
#if UNITY_EDITOR
                return "http://localhost:3000";
#else
                return GetEnvVariable("WEB_APP_URL", "https://blokus-online.mooo.com");
#endif
            }
        }

        /// <summary>
        /// API 서버 URL
        /// </summary>
        public static string ApiServerUrl
        {
            get
            {
#if UNITY_EDITOR
                return "http://localhost:8080/api";
#else
                // WEB_APP_URL 기반으로 API URL 생성
                string webUrl = GetEnvVariable("WEB_APP_URL", "https://blokus-online.mooo.com");
                return $"{webUrl}:8080/api";
#endif
            }
        }

        /// <summary>
        /// OIDC 서버 URL
        /// </summary>
        public static string OidcServerUrl
        {
            get
            {
#if UNITY_EDITOR
                return "http://localhost:9000";
#else
                // WEB_APP_URL 기반으로 OIDC URL 생성  
                string webUrl = GetEnvVariable("WEB_APP_URL", "https://blokus-online.mooo.com");
                return $"{webUrl}:9000";
#endif
            }
        }

        /// <summary>
        /// TCP 게임 서버 호스트
        /// </summary>
        public static string TcpServerHost
        {
            get
            {
#if UNITY_EDITOR
                return "localhost";
#else
                // WEB_APP_URL에서 도메인 추출
                string webUrl = GetEnvVariable("WEB_APP_URL", "https://blokus-online.mooo.com");
                if (webUrl.StartsWith("http://"))
                    return webUrl.Substring(7);
                if (webUrl.StartsWith("https://"))
                    return webUrl.Substring(8);
                return webUrl;
#endif
            }
        }

        /// <summary>
        /// TCP 게임 서버 포트
        /// </summary>
        public static int TcpServerPort
        {
            get
            {
#if UNITY_EDITOR
                return 9999;
#else
                string portStr = GetEnvVariable("SERVER_PORT", "9999");
                return int.TryParse(portStr, out int port) ? port : 9999;
#endif
            }
        }

        /// <summary>
        /// 디버그 로그 활성화 여부
        /// </summary>
        public static bool EnableDebugLog => IsDevelopment;

        /// <summary>
        /// 환경변수 디버그 정보 출력
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LogEnvironmentInfo()
        {
            Debug.Log($"🔧 Unity Environment Config:");
            Debug.Log($"   IsDevelopment: {IsDevelopment}");
            Debug.Log($"   WebServerUrl: {WebServerUrl}");
            Debug.Log($"   ApiServerUrl: {ApiServerUrl}");
            Debug.Log($"   OidcServerUrl: {OidcServerUrl}");
            Debug.Log($"   TcpServerHost: {TcpServerHost}:{TcpServerPort}");
        }
    }
}