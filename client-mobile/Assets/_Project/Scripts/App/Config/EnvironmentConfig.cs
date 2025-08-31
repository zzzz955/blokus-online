using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace App.Config
{
    /// <summary>
    /// 환경변수 기반 설정 관리
    /// - 에디터: 기본적으로 프로덕션 서버(HTTPS)로 테스트
    /// - 빌드: StreamingAssets/.env 의 WEB_APP_URL 등을 사용(없으면 프로덕션 기본값)
    /// </summary>
    public static class EnvironmentConfig
    {
        // === 편의 스위치 ===
        // 에디터에서도 프로덕션 서버로 붙어서 통합 테스트 (권장)
        private const bool UseProdServerInEditor = true;

        // === 기본값 ===
        private const string DefaultProdBaseUrl = "https://blokus-online.mooo.com";

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        /// Android의 StreamingAssets는 File.* 접근이 제한될 수 있습니다.
        /// 그 경우 기본값을 사용하거나, Resources/TextAsset로 전환을 권장합니다.
        /// </summary>
        private static void LoadEnvironmentVariables()
        {
            if (_envLoaded) return;

            _envVariables = new Dictionary<string, string>();

#if !UNITY_EDITOR
            try
            {
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
        private static string GetEnv(string key, string defaultValue = "")
        {
#if UNITY_EDITOR
            // 에디터에서는 기본값 사용(프로덕션 테스트 기준)
            return defaultValue;
#else
            LoadEnvironmentVariables();
            return _envVariables != null && _envVariables.ContainsKey(key) ? _envVariables[key] : defaultValue;
#endif
        }

        private static string NormalizeBase(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return DefaultProdBaseUrl;
            url = url.Trim().TrimEnd('/');
            if (!url.StartsWith("http")) url = "https://" + url;
            return url;
        }

        /// <summary>
        /// 서비스 베이스 URL (스킴/도메인 기준)
        /// </summary>
        private static string BaseUrl
        {
            get
            {
#if UNITY_EDITOR
                return UseProdServerInEditor
                    ? DefaultProdBaseUrl
                    : "http://localhost:3000"; // 필요 시 로컬 웹앱 테스트
#else
                // 빌드: .env 의 WEB_APP_URL 우선
                var envUrl = GetEnv("WEB_APP_URL", DefaultProdBaseUrl);
                return NormalizeBase(envUrl);
#endif
            }
        }

        /// <summary>
        /// 웹 앱 URL (Next.js)
        /// </summary>
        public static string WebServerUrl => BaseUrl;

        /// <summary>
        /// Single API 서버 베이스 URL
        /// 업스트림이 /api/... 라우트를 쓰므로, 프록시 서브패스 + /api 로 맞춥니다.
        /// 결과: https://.../single-api/api
        /// </summary>
        public static string ApiServerUrl => $"{BaseUrl}/single-api/api";

        /// <summary>
        /// OIDC 서버 베이스 URL (서브패스)
        /// 결과: https://.../oidc
        /// </summary>
        public static string OidcServerUrl => $"{BaseUrl}/oidc";

        /// <summary>
        /// TCP 게임 서버 호스트
        /// </summary>
        public static string TcpServerHost
        {
            get
            {
#if UNITY_EDITOR
                return "localhost"; // 에디터 로컬 테스트
#else
                try
                {
                    var uri = new Uri(BaseUrl);
                    return uri.Host;
                }
                catch
                {
                    // BaseUrl이 비정상일 경우 대비
                    var webUrl = GetEnv("WEB_APP_URL", DefaultProdBaseUrl);
                    if (webUrl.StartsWith("http://")) return webUrl.Substring(7);
                    if (webUrl.StartsWith("https://")) return webUrl.Substring(8);
                    return webUrl;
                }
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
                string portStr = GetEnv("SERVER_PORT", "9999");
                return int.TryParse(portStr, out int port) ? port : 9999;
#endif
            }
        }

        /// <summary>
        /// 디버그 로그 활성화 여부
        /// </summary>
        public static bool EnableDebugLog => IsDevelopment;

        /// <summary>
        /// TLS 설정 및 환경변수 디버그 정보 출력
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LogEnvironmentInfo()
        {
            // UnityWebRequest는 UnityTLS를 주로 사용하지만,
            // .NET 스택을 쓰는 일부 코드 대비로 TLS1.2 최소 보장
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            System.Net.ServicePointManager.CheckCertificateRevocationList = false;
            System.Net.ServicePointManager.DefaultConnectionLimit = 10;

            Debug.Log("🔒 네트워크 스택 설정: TLS1.2");
            Debug.Log($"🔧 Unity Environment Config:");
            Debug.Log($"   IsDevelopment: {IsDevelopment}");
            Debug.Log($"   WebServerUrl: {WebServerUrl}");
            Debug.Log($"   ApiServerUrl: {ApiServerUrl}");
            Debug.Log($"   OidcServerUrl: {OidcServerUrl}");
            Debug.Log($"   TcpServerHost: {TcpServerHost}:{TcpServerPort}");
        }
    }
}
