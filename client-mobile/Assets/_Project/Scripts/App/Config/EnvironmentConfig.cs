using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace App.Config
{
    /// <summary>
    /// 환경변수 기반 설정 관리
    /// - 에디터: EnvironmentModeManager를 통한 dev/release 모드 분기 처리
    /// - 빌드: StreamingAssets/.env 의 WEB_APP_URL 등을 사용(없으면 프로덕션 기본값)
    /// </summary>
    public static class EnvironmentConfig
    {
        // === 편의 스위치 ===
        // EnvironmentModeManager가 없을 경우 폴백용 설정
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
                    Debug.Log($" 환경변수 로드 완료: {_envVariables.Count}개 변수");
                }
                else
                {
                    Debug.LogWarning($"⚠️ .env 파일을 찾을 수 없습니다: {envPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($" 환경변수 로드 실패: {e.Message}");
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
                // EnvironmentModeManager를 통한 모드 확인
                var envManager = EnvironmentModeManager.Instance;
                if (envManager != null)
                {
                    return envManager.GetWebServerUrl();
                }
                
                // 폴백: EnvironmentModeManager가 없을 경우 기존 로직 사용
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
        public static string WebServerUrl
        {
            get
            {
#if UNITY_EDITOR
                return BaseUrl;
#else
                // 릴리즈 빌드: 하드코딩된 프로덕션 서버 사용
                return "https://blokus-online.mooo.com";
#endif
            }
        }

        /// <summary>
        /// Single API 서버 베이스 URL
        /// 업스트림이 /api/... 라우트를 쓰므로, 프록시 서브패스 + /api 로 맞춥니다.
        /// 결과: https://.../single-api/api (release) 또는 http://localhost:8080/api (dev)
        /// </summary>
        public static string ApiServerUrl
        {
            get
            {
#if UNITY_EDITOR
                // EnvironmentModeManager를 통한 모드 확인
                var envManager = EnvironmentModeManager.Instance;
                if (envManager != null)
                {
                    return envManager.GetApiServerUrl();
                }
                // 폴백
                return $"{BaseUrl}/single-api/api";
#else
                // 릴리즈 빌드: 하드코딩된 프로덕션 서버 사용
                return "https://blokus-online.mooo.com/single-api/api";
#endif
            }
        }

        /// <summary>
        /// OIDC 서버 베이스 URL (서브패스)
        /// 결과: https://.../oidc (release) 또는 http://localhost:9000 (dev)
        /// </summary>
        public static string OidcServerUrl
        {
            get
            {
#if UNITY_EDITOR
                // EnvironmentModeManager를 통한 모드 확인
                var envManager = EnvironmentModeManager.Instance;
                if (envManager != null)
                {
                    return envManager.GetAuthServerUrl();
                }
                // 폴백
                return $"{BaseUrl}/oidc";
#else
                // 릴리즈 빌드: 하드코딩된 프로덕션 서버 사용
                return "https://blokus-online.mooo.com/oidc";
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
                // EnvironmentModeManager를 통한 모드 확인
                var envManager = EnvironmentModeManager.Instance;
                if (envManager != null)
                {
                    return envManager.GetTcpServerHost();
                }

                // 폴백: EnvironmentModeManager가 없을 경우 기존 로직 사용
                return "localhost"; // 에디터 로컬 테스트
#else
                // 릴리즈 빌드: 데스크탑 클라이언트와 동일하게 하드코딩된 프로덕션 서버 사용
                return "blokus-online.mooo.com";
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
                // EnvironmentModeManager를 통한 모드 확인
                var envManager = EnvironmentModeManager.Instance;
                if (envManager != null)
                {
                    return envManager.GetTcpServerPort();
                }

                // 폴백: EnvironmentModeManager가 없을 경우 기존 로직 사용
                return 9999;
#else
                // 릴리즈 빌드: 데스크탑 클라이언트와 동일하게 하드코딩된 포트 사용
                return 9999;
#endif
            }
        }
    }
}
