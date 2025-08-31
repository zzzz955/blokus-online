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
        private static string _serverUrl = "https://blokus-online.mooo.com/api/health";
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
            
            // 릴리즈 빌드에서만 활성화
            if (!Debug.isDebugBuild)
            {
                Debug.Log("[RemoteLogger] 원격 로깅 시스템 활성화됨");
                
                // 정기적으로 로그 전송
                _monoBehaviour.StartCoroutine(SendLogsRoutine());
            }
        }
        
        /// <summary>
        /// Unity 로그 메시지 받기
        /// </summary>
        private static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            if (!_isInitialized || Debug.isDebugBuild) return;
            
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