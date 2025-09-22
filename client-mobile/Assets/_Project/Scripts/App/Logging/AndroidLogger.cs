using System;
using System.IO;
using UnityEngine;

namespace App.Logging
{
    /// <summary>
    /// 안드로이드 릴리즈 빌드용 파일 로깅 시스템
    /// persistentDataPath를 사용하여 앱 전용 저장소에 로그 기록
    /// App.Logging.AndroidLogger 클래스로 네임스페이스 충돌 방지
    /// </summary>
    public static class AndroidLogger
    {
        private static string logFilePath;
        private static bool initialized = false;
        private static readonly object lockObject = new object();

        /// <summary>
        /// 로거 초기화
        /// </summary>
        public static void Initialize()
        {
            if (initialized) return;

            try
            {
                // 안드로이드 앱 전용 저장소 경로: /storage/emulated/0/Android/data/com.yourcompany.yourapp/files/
                string logDirectory = Path.Combine(Application.persistentDataPath, "Logs");

                // 로그 디렉토리 생성
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // 로그 파일 경로 (날짜별로 분리)
                string fileName = $"blokus_client_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
                logFilePath = Path.Combine(logDirectory, fileName);

                // 초기화 완료 로그
                WriteToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INIT] Debug Logger 초기화 완료");
                WriteToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INIT] 로그 파일: {logFilePath}");
                WriteToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INIT] Unity 버전: {Application.unityVersion}");
                WriteToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INIT] 플랫폼: {Application.platform}");
                WriteToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INIT] 디바이스: {SystemInfo.deviceModel}");
                WriteToFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INIT] 안드로이드 버전: {SystemInfo.operatingSystem}");

                initialized = true;
                UnityEngine.Debug.Log($"[App.Debug] 초기화 완료: {logFilePath}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[App.Debug] 초기화 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 정보 로그 기록
        /// </summary>
        public static void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// 경고 로그 기록
        /// </summary>
        public static void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        /// <summary>
        /// 에러 로그 기록
        /// </summary>
        public static void LogError(string message)
        {
            WriteLog("ERROR", message);
        }

        /// <summary>
        /// 네트워크 전용 로그 (TCP 연결, JWT 인증 등)
        /// </summary>
        public static void LogNetwork(string message)
        {
            WriteLog("NET", message);
        }

        /// <summary>
        /// JWT 인증 전용 로그
        /// </summary>
        public static void LogAuth(string message)
        {
            WriteLog("AUTH", message);
        }

        /// <summary>
        /// 연결 상태 전용 로그
        /// </summary>
        public static void LogConnection(string message)
        {
            WriteLog("CONN", message);
        }

        /// <summary>
        /// 레벨별 로그 기록
        /// </summary>
        private static void WriteLog(string level, string message)
        {
            if (!initialized)
            {
                Initialize();
            }

            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                WriteToFile(logEntry);

                // Unity Console에도 출력 (릴리즈에서는 보이지 않지만 에디터 테스트용)
                UnityEngine.Debug.Log($"[FileLog] {logEntry}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[App.Debug] 로그 기록 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 파일에 직접 쓰기 (스레드 안전)
        /// </summary>
        private static void WriteToFile(string content)
        {
            if (string.IsNullOrEmpty(logFilePath)) return;

            lock (lockObject)
            {
                try
                {
                    using (var writer = new StreamWriter(logFilePath, true))
                    {
                        writer.WriteLine(content);
                        writer.Flush();
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[App.Debug] 파일 쓰기 실패: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 현재 로그 파일 경로 반환
        /// </summary>
        public static string GetLogFilePath()
        {
            return logFilePath ?? "로그 파일이 초기화되지 않음";
        }

        /// <summary>
        /// 로그 파일들을 ZIP으로 압축하여 공유 가능한 형태로 만들기
        /// </summary>
        public static string ExportLogs()
        {
            try
            {
                string logDirectory = Path.Combine(Application.persistentDataPath, "Logs");
                if (!Directory.Exists(logDirectory))
                {
                    return "로그 디렉토리가 존재하지 않습니다.";
                }

                string[] logFiles = Directory.GetFiles(logDirectory, "*.log");
                if (logFiles.Length == 0)
                {
                    return "로그 파일이 존재하지 않습니다.";
                }

                // 가장 최근 로그 파일의 내용 반환 (간단한 구현)
                string latestLog = logFiles[logFiles.Length - 1];
                string content = File.ReadAllText(latestLog);

                LogInfo($"로그 내보내기 완료: {latestLog} ({content.Length} chars)");
                return content;
            }
            catch (Exception ex)
            {
                LogError($"로그 내보내기 실패: {ex.Message}");
                return $"로그 내보내기 실패: {ex.Message}";
            }
        }

        /// <summary>
        /// 오래된 로그 파일 정리 (7일 이상)
        /// </summary>
        public static void CleanupOldLogs()
        {
            try
            {
                string logDirectory = Path.Combine(Application.persistentDataPath, "Logs");
                if (!Directory.Exists(logDirectory)) return;

                string[] logFiles = Directory.GetFiles(logDirectory, "*.log");
                int deletedCount = 0;

                foreach (string file in logFiles)
                {
                    FileInfo fileInfo = new FileInfo(file);
                    if (DateTime.Now.Subtract(fileInfo.CreationTime).TotalDays > 7)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }

                if (deletedCount > 0)
                {
                    LogInfo($"오래된 로그 파일 {deletedCount}개 삭제 완료");
                }
            }
            catch (Exception ex)
            {
                LogError($"로그 파일 정리 실패: {ex.Message}");
            }
        }

        // ========================================
        // 기존 App.Debug 호환성을 위한 메서드들
        // ========================================

        /// <summary>
        /// 기존 App.Debug.Log 호환성
        /// </summary>
        public static void Log(string message)
        {
            LogInfo(message);
        }

        /// <summary>
        /// 기존 App.Debug.LogException 호환성
        /// </summary>
        public static void LogException(Exception ex)
        {
            LogError($"Exception: {ex.GetType().Name} - {ex.Message}\nStackTrace: {ex.StackTrace}");
        }

        /// <summary>
        /// 기존 App.Debug.isDebugBuild 호환성
        /// </summary>
        public static bool isDebugBuild
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return UnityEngine.Debug.isDebugBuild;
#endif
            }
        }
    }
}