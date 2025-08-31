using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;

namespace App.Network
{
    /// <summary>
    /// 릴리즈 빌드에서 로그를 서버로 전송하는 원격 로깅 시스템
    /// </summary>
    public static class RemoteLogger
    {
        private static string _serverUrl = "http://blokus-online.mooo.com/api/health";
        private static Queue<LogEntry> _logQueue = new Queue<LogEntry>();
        private static bool _isInitialized = false;
        private static MonoBehaviour _monoBehaviour;
        
        [System.Serializable]
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
        
        [System.Serializable]
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
            
            // Unity 로그 이벤트 구독
            Application.logMessageReceived += OnLogMessageReceived;
            
            // 디버그/릴리즈 모든 빌드에서 활성화 (테스트용)
            Debug.Log("[RemoteLogger] 원격 로깅 시스템 활성화됨 (Debug: " + Debug.isDebugBuild + ")");
            
            // 연결 테스트 먼저 실행
            _monoBehaviour.StartCoroutine(TestConnection());
            
            // 정기적으로 로그 전송
            _monoBehaviour.StartCoroutine(SendLogsRoutine());
        }
        
        /// <summary>
        /// Unity 로그 메시지 받기
        /// </summary>
        private static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            if (!_isInitialized) return;
            
            // OIDC 관련 로그만 수집
            if (logString.Contains("[OidcAuth]") || 
                logString.Contains("OIDC") || 
                logString.Contains("UnityWebRequest") ||
                logString.Contains("SSL") ||
                logString.Contains("Certificate"))
            {
                string level = type switch
                {
                    LogType.Error => "ERROR",
                    LogType.Warning => "WARN",
                    LogType.Log => "INFO",
                    LogType.Exception => "ERROR",
                    _ => "DEBUG"
                };
                
                _logQueue.Enqueue(new LogEntry(level, logString, stackTrace, "OIDC"));
                
                // 큐가 너무 크면 오래된 로그 제거
                if (_logQueue.Count > 100)
                {
                    _logQueue.Dequeue();
                }
            }
        }
        
        /// <summary>
        /// 직접 로그 추가
        /// </summary>
        public static void LogInfo(string message, string category = "Unity")
        {
            if (!_isInitialized) return;
            
            _logQueue.Enqueue(new LogEntry("INFO", message, "", category));
            Debug.Log($"[RemoteLogger] {message}");
        }
        
        /// <summary>
        /// 직접 에러 로그 추가
        /// </summary>
        public static void LogError(string message, string category = "Unity")
        {
            if (!_isInitialized) return;
            
            _logQueue.Enqueue(new LogEntry("ERROR", message, "", category));
            Debug.LogError($"[RemoteLogger] {message}");
        }
        
        /// <summary>
        /// 종합 네트워크 연결 진단 테스트
        /// </summary>
        private static IEnumerator TestConnection()
        {
            yield return NetworkSmokeTest();
        }
        
        /// <summary>
        /// 네트워크 연결 진단 SmokeTest
        /// </summary>
        private static IEnumerator NetworkSmokeTest()
        {
            Debug.Log("[SmokeTest] 🔍 네트워크 연결 진단 시작");
            
            // 1) HTTPS 기본 동작 테스트
            using (var request = UnityWebRequest.Get("https://www.google.com/generate_204"))
            {
                yield return request.SendWebRequest();
                Debug.Log($"[SmokeTest] A(HTTPS 기본): {request.result}/{request.responseCode} {request.error}");
            }
            
            // 2) 프로덕션 웹 서버 헬스체크 (80 포트)
            using (var request = UnityWebRequest.Get("https://blokus-online.mooo.com"))
            {
                request.certificateHandler = new BypassCertificate();
                request.timeout = 10;
                yield return request.SendWebRequest();
                Debug.Log($"[SmokeTest] B(웹서버 HTTPS): {request.result}/{request.responseCode} {request.error}");
            }
            
            // 3) 프로덕션 웹 서버 HTTP (80 포트)
            using (var request = UnityWebRequest.Get("http://blokus-online.mooo.com"))
            {
                request.certificateHandler = new BypassCertificate();
                request.timeout = 10;
                yield return request.SendWebRequest();
                Debug.Log($"[SmokeTest] C(웹서버 HTTP): {request.result}/{request.responseCode} {request.error}");
            }
            
            // 4) Single API 서버 프록시 경로 테스트
            using (var request = UnityWebRequest.Get("https://blokus-online.mooo.com/single-api/health"))
            {
                request.certificateHandler = new BypassCertificate();
                request.timeout = 10;
                yield return request.SendWebRequest();
                Debug.Log($"[SmokeTest] D(Single-API): {request.result}/{request.responseCode} {request.error}");
            }
            
            // 5) OIDC 서버 프록시 경로 테스트  
            using (var request = UnityWebRequest.Get("https://blokus-online.mooo.com/oidc/.well-known/openid-configuration"))
            {
                request.certificateHandler = new BypassCertificate();
                request.timeout = 10;
                yield return request.SendWebRequest();
                Debug.Log($"[SmokeTest] E(OIDC 프록시): {request.result}/{request.responseCode} {request.error}");
            }
            
            // 6) 웹 서버 헬스체크 엔드포인트 (RemoteLogger와 동일)
            using (var request = UnityWebRequest.Get("https://blokus-online.mooo.com/api/health"))
            {
                request.certificateHandler = new BypassCertificate();
                request.timeout = 10;
                yield return request.SendWebRequest();
                Debug.Log($"[SmokeTest] F(RemoteLog 경로): {request.result}/{request.responseCode} {request.error}");
            }
            
            Debug.Log("[SmokeTest] ✅ 네트워크 진단 완료 - adb logcat으로 상세 로그 확인");
        }

        /// <summary>
        /// 기존 서버 연결 테스트 (호환성 유지)
        /// </summary>
        private static IEnumerator TestConnectionLegacy()
        {
            Debug.Log("[RemoteLogger] 서버 연결 테스트 시작...");
            
            // 간단한 테스트 로그 생성
            LogEntry testLog = new LogEntry("INFO", "[연결테스트] Unity에서 서버 연결 테스트", "", "ConnectionTest");
            LogBatch testBatch = new LogBatch(new LogEntry[] { testLog });
            string json = JsonUtility.ToJson(testBatch);
            
            Debug.Log($"[RemoteLogger] 전송할 JSON: {json}");
            Debug.Log($"[RemoteLogger] 서버 URL: {_serverUrl}");
            
            using (UnityWebRequest request = new UnityWebRequest(_serverUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("User-Agent", "Unity-RemoteLogger-Test/1.0");
                request.certificateHandler = new BypassCertificate();
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
                {
                    logsToSend.Add(_logQueue.Dequeue());
                }
            }
            
            if (logsToSend.Count == 0) yield break;
            
            LogBatch batch = new LogBatch(logsToSend.ToArray());
            string json = JsonUtility.ToJson(batch);
            
            using (UnityWebRequest request = new UnityWebRequest(_serverUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.certificateHandler = new BypassCertificate();
                request.timeout = 5;
                
                yield return request.SendWebRequest();
                
                // 전송 실패해도 조용히 무시
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[RemoteLogger] {logsToSend.Count}개 로그 전송 완료");
                }
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