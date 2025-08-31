using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using App.Config; // ★ EnvironmentConfig 사용

namespace App.Network
{
    /// <summary>
    /// 릴리즈 빌드에서 로그를 서버로 전송하는 원격 로깅 시스템 + 스모크 테스트
    /// </summary>
    public static class RemoteLogger
    {
        // 건강 체크 URL은 Single-API 기준으로 통일: https://.../single-api/api/health
        private static string ServerHealthUrl => $"{EnvironmentConfig.ApiServerUrl}/health";

        private static readonly Queue<LogEntry> _logQueue = new Queue<LogEntry>();
        private static bool _isInitialized = false;
        private static MonoBehaviour _monoBehaviour;

        [Serializable]
        private class LogEntry
        {
            public string timestamp;
            public string level;
            public string message;
            public string stackTrace;
            public string category;

            public LogEntry(string level, string message, string stackTrace = "", string category = "Unity")
            {
                this.timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                this.level = level;
                this.message = message;
                this.stackTrace = stackTrace;
                this.category = category;
            }
        }

        [Serializable]
        private class LogBatch
        {
            public string deviceId;
            public string platform;
            public string appVersion;
            public LogEntry[] logs;

            public LogBatch(LogEntry[] logs)
            {
                this.deviceId = SystemInfo.deviceUniqueIdentifier;
                this.platform = Application.platform.ToString();
                this.appVersion = Application.version;
                this.logs = logs;
            }
        }

        /// <summary>
        /// 원격 로거 초기화
        /// </summary>
        public static void Initialize(MonoBehaviour monoBehaviour)
        {
            if (_isInitialized) return;

            _monoBehaviour = monoBehaviour;
            _isInitialized = true;

            Application.logMessageReceived += OnLogMessageReceived;

            Debug.Log($"[RemoteLogger] 원격 로깅 활성화 (Debug: {Debug.isDebugBuild})");
            Debug.Log($"[RemoteLogger] ServerHealthUrl: {ServerHealthUrl}");
            Debug.Log($"[RemoteLogger] ApiServerUrl: {EnvironmentConfig.ApiServerUrl}");
            Debug.Log($"[RemoteLogger] OidcServerUrl: {EnvironmentConfig.OidcServerUrl}");

            // 연결 스모크 테스트 실행
            _monoBehaviour.StartCoroutine(TestConnection());

            // 정기 전송 루틴 시작
            _monoBehaviour.StartCoroutine(SendLogsRoutine());
        }

        /// <summary>
        /// Unity 로그 훅
        /// </summary>
        private static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            if (!_isInitialized) return;

            // 네트워크/OIDC 관련 로그 위주로 수집 (필요 시 조건 조정)
            if (logString.Contains("[OidcAuth]") ||
                logString.Contains("OIDC") ||
                logString.Contains("UnityWebRequest") ||
                logString.Contains("SSL") ||
                logString.Contains("Certificate"))
            {
                string level = type switch
                {
                    LogType.Error => "ERROR",
                    LogType.Exception => "ERROR",
                    LogType.Warning => "WARN",
                    LogType.Log => "INFO",
                    _ => "DEBUG"
                };

                _logQueue.Enqueue(new LogEntry(level, logString, stackTrace, "Network/OIDC"));

                // 큐 크기 제한
                if (_logQueue.Count > 200) _logQueue.Dequeue();
            }
        }

        public static void LogInfo(string message, string category = "Unity")
        {
            if (!_isInitialized) return;
            _logQueue.Enqueue(new LogEntry("INFO", message, "", category));
            Debug.Log($"[RemoteLogger] {message}");
        }

        public static void LogError(string message, string category = "Unity")
        {
            if (!_isInitialized) return;
            _logQueue.Enqueue(new LogEntry("ERROR", message, "", category));
            Debug.LogError($"[RemoteLogger] {message}");
        }

        /// <summary>
        /// 연결 스모크 테스트
        /// </summary>
        private static IEnumerator TestConnection()
        {
            yield return NetworkSmokeTest();
        }

        /// <summary>
        /// 네트워크 연결 진단 SmokeTest (HTTPS 우선, 필요 지점만 HTTP 비교)
        /// </summary>
        private static IEnumerator NetworkSmokeTest()
        {
            Debug.Log("[SmokeTest] 🔍 네트워크 연결 진단 시작");

            // A) HTTPS 기본 동작
            using (var req = UnityWebRequest.Get("https://www.google.com/generate_204"))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                Debug.Log($"[SmokeTest] A(HTTPS 기본): {req.result}/{req.responseCode} {req.error}");
            }

            // B) 웹서버 루트 (프로덕션)
            using (var req = UnityWebRequest.Get(EnvironmentConfig.WebServerUrl))
            {
                req.timeout = 15;
                req.SetRequestHeader("User-Agent", "Unity-Client/2022.3");
                yield return req.SendWebRequest();
                Debug.Log($"[SmokeTest] B(웹서버 HTTPS): {req.result}/{req.responseCode} {req.error}");
                if (!string.IsNullOrEmpty(req.error))
                    Debug.LogError($"[SmokeTest] B 상세 에러: {req.error}");
            }

            // C) (비교용) HTTP 루트 접근 -> 보통 301 리다이렉트
            using (var req = UnityWebRequest.Get(EnvironmentConfig.WebServerUrl.Replace("https://", "http://")))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                Debug.Log($"[SmokeTest] C(웹서버 HTTP): {req.result}/{req.responseCode} {req.error}");
            }

            // D) Single-API 헬스 (프록시 경유): https://.../single-api/api/health
            using (var req = UnityWebRequest.Get(ServerHealthUrl))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                Debug.Log($"[SmokeTest] D(Single-API): {req.result}/{req.responseCode} {req.error}");
            }

            // E) OIDC 디스커버리: https://.../oidc/.well-known/openid-configuration
            using (var req = UnityWebRequest.Get($"{EnvironmentConfig.OidcServerUrl}/.well-known/openid-configuration"))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                Debug.Log($"[SmokeTest] E(OIDC 프록시): {req.result}/{req.responseCode} {req.error}");
            }

            // F) 웹 서버 헬스 엔드포인트(nginx): https://.../healthz
            using (var req = UnityWebRequest.Get($"{EnvironmentConfig.WebServerUrl}/healthz"))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                Debug.Log($"[SmokeTest] F(웹서버 헬스): {req.result}/{req.responseCode} {req.error}");
            }

            Debug.Log("[SmokeTest] ✅ 네트워크 진단 완료 - adb logcat으로 상세 로그 확인");
        }

        /// <summary>
        /// (레거시) POST 연결 테스트
        /// </summary>
        private static IEnumerator TestConnectionLegacy()
        {
            Debug.Log("[RemoteLogger] 서버 연결 테스트 시작...");

            LogEntry testLog = new LogEntry("INFO", "[연결테스트] Unity에서 서버 연결 테스트", "", "ConnectionTest");
            LogBatch testBatch = new LogBatch(new LogEntry[] { testLog });
            string json = JsonUtility.ToJson(testBatch);

            Debug.Log($"[RemoteLogger] 전송할 JSON: {json}");
            Debug.Log($"[RemoteLogger] 서버 URL: {ServerHealthUrl}");

            using (UnityWebRequest request = new UnityWebRequest(ServerHealthUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("User-Agent", "Unity-RemoteLogger-Test/1.0");
                request.timeout = 10;

                Debug.Log("[RemoteLogger] 서버로 연결 테스트 요청 전송 중...");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[RemoteLogger] 연결 테스트 성공! 응답: {request.downloadHandler.text}");
                    LogInfo("서버 연결 테스트 성공", "ConnectionTest");
                }
                else
                {
                    Debug.LogError($"[RemoteLogger] 연결 테스트 실패! 에러: {request.error}");
                    Debug.LogError($"[RemoteLogger] Response Code: {request.responseCode}");
                    LogError($"연결 테스트 실패: {request.error} (Code: {request.responseCode})", "ConnectionTest");
                }
            }
        }

        /// <summary>
        /// 정기적으로 로그 전송
        /// </summary>
        private static IEnumerator SendLogsRoutine()
        {
            while (_isInitialized)
            {
                yield return new WaitForSeconds(10f); // 10초마다
                if (_logQueue.Count > 0)
                {
                    yield return SendLogs();
                }
            }
        }

        /// <summary>
        /// 서버로 로그 전송
        /// </summary>
        private static IEnumerator SendLogs()
        {
            if (_logQueue.Count == 0) yield break;

            // 최대 20개의 로그만 한 번에 전송
            List<LogEntry> logsToSend = new List<LogEntry>();
            int count = Mathf.Min(_logQueue.Count, 20);

            for (int i = 0; i < count; i++)
            {
                if (_logQueue.Count > 0)
                    logsToSend.Add(_logQueue.Dequeue());
            }

            if (logsToSend.Count == 0) yield break;

            LogBatch batch = new LogBatch(logsToSend.ToArray());
            string json = JsonUtility.ToJson(batch);

            using (UnityWebRequest request = new UnityWebRequest(ServerHealthUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 5;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[RemoteLogger] {logsToSend.Count}개 로그 전송 완료");
                }
                // 실패해도 조용히 무시
            }
        }

        /// <summary>
        /// 즉시 모든 로그 전송
        /// </summary>
        public static void FlushLogs()
        {
            if (_monoBehaviour != null && _isInitialized)
            {
                _monoBehaviour.StartCoroutine(SendLogs());
            }
        }

        /// <summary>
        /// 정리
        /// </summary>
        public static void Cleanup()
        {
            if (_isInitialized)
            {
                Application.logMessageReceived -= OnLogMessageReceived;
                _isInitialized = false;
            }
        }
    }
}
