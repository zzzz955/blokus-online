using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace App.Utils
{
    /// <summary>
    /// 릴리즈 빌드에서 로그를 자동 제거하는 게임 로거
    /// Development Build에서만 로그가 출력됩니다.
    /// </summary>
    public static class GameLogger
    {
        [Conditional("DEVELOPMENT")]
        public static void Log(string message)
        {
            Debug.Log(message);
        }

        [Conditional("DEVELOPMENT")]
        public static void Log(string message, UnityEngine.Object context)
        {
            Debug.Log(message, context);
        }

        [Conditional("DEVELOPMENT")]
        public static void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        [Conditional("DEVELOPMENT")]
        public static void LogWarning(string message, UnityEngine.Object context)
        {
            Debug.LogWarning(message, context);
        }

        [Conditional("DEVELOPMENT")]
        public static void LogError(string message)
        {
            Debug.LogError(message);
        }

        [Conditional("DEVELOPMENT")]
        public static void LogError(string message, UnityEngine.Object context)
        {
            Debug.LogError(message, context);
        }

        [Conditional("DEVELOPMENT")]
        public static void LogException(System.Exception exception)
        {
            Debug.LogException(exception);
        }

        [Conditional("DEVELOPMENT")]
        public static void LogException(System.Exception exception, UnityEngine.Object context)
        {
            Debug.LogException(exception, context);
        }

        // 릴리즈에서도 유지되는 중요한 에러 로그 (크래시 수집용)
        public static void LogCriticalError(string message)
        {
            Debug.LogError($"[CRITICAL] {message}");
        }

        public static void LogCriticalException(System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}