using UnityEngine;

namespace BlokusUnity.UI.Messages
{
    /// <summary>
    /// 시스템 메시지 타입
    /// </summary>
    public enum MessageType
    {
        Toast,      // 자동으로 사라지는 알림 (3-5초)
        Alert,      // 사용자 확인이 필요한 경고/알림
        Loading,    // 로딩 진행 상태 표시
        Persistent  // 수동으로 닫을 때까지 유지되는 메시지
    }

    /// <summary>
    /// 메시지 우선순위
    /// </summary>
    public enum MessagePriority
    {
        Debug = 0,    // 개발용 메시지 (회색)
        Info = 1,     // 일반 정보 (파란색)
        Success = 2,  // 성공 메시지 (초록색)
        Warning = 3,  // 경고 메시지 (노란색)
        Error = 4,    // 오류 메시지 (빨간색)
        Critical = 5  // 치명적 오류 (진한 빨간색)
    }

    /// <summary>
    /// 시스템 메시지 데이터
    /// </summary>
    [System.Serializable]
    public struct MessageData
    {
        public string message;
        public MessageType type;
        public MessagePriority priority;
        public float duration; // Toast 메시지 지속 시간 (초)
        public string title;   // Alert용 제목 (선택사항)
        public System.Action onConfirm; // Alert 확인 콜백
        public System.Action onCancel;  // Alert 취소 콜백
        public Sprite icon;      // 메시지 아이콘 (선택사항)

        /// <summary>
        /// 간단한 Toast 메시지 생성
        /// </summary>
        public static MessageData CreateToast(string message, MessagePriority priority = MessagePriority.Info, float duration = 3f)
        {
            return new MessageData
            {
                message = message,
                type = MessageType.Toast,
                priority = priority,
                duration = duration,
                title = "",
                onConfirm = null,
                onCancel = null,
                icon = null
            };
        }

        /// <summary>
        /// Alert 메시지 생성
        /// </summary>
        public static MessageData CreateAlert(string message, string title = "", 
            System.Action onConfirm = null, System.Action onCancel = null, 
            MessagePriority priority = MessagePriority.Warning)
        {
            return new MessageData
            {
                message = message,
                type = MessageType.Alert,
                priority = priority,
                duration = 0f, // Alert은 지속시간 무관
                title = title,
                onConfirm = onConfirm,
                onCancel = onCancel,
                icon = null
            };
        }

        /// <summary>
        /// 로딩 메시지 생성
        /// </summary>
        public static MessageData CreateLoading(string message = "로딩 중...")
        {
            return new MessageData
            {
                message = message,
                type = MessageType.Loading,
                priority = MessagePriority.Info,
                duration = 0f, // 수동으로 종료
                title = "",
                onConfirm = null,
                onCancel = null,
                icon = null
            };
        }

        /// <summary>
        /// 우선순위에 따른 색상 반환
        /// </summary>
        public Color GetPriorityColor()
        {
            switch (priority)
            {
                case MessagePriority.Debug:
                    return new Color(0.7f, 0.7f, 0.7f, 1f); // 회색
                case MessagePriority.Info:
                    return new Color(0.2f, 0.6f, 1f, 1f); // 파란색
                case MessagePriority.Success:
                    return new Color(0.2f, 0.8f, 0.2f, 1f); // 초록색
                case MessagePriority.Warning:
                    return new Color(1f, 0.8f, 0.2f, 1f); // 노란색
                case MessagePriority.Error:
                    return new Color(1f, 0.3f, 0.3f, 1f); // 빨간색
                case MessagePriority.Critical:
                    return new Color(0.8f, 0.1f, 0.1f, 1f); // 진한 빨간색
                default:
                    return Color.white;
            }
        }
    }
}