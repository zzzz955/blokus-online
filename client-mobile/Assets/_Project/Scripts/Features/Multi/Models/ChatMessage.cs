using System;

namespace Features.Multi.Models
{
    /// <summary>
    /// 채팅 메시지 모델
    /// </summary>
    [Serializable]
    public class ChatMessage
    {
        public string username;      // 서버 통신용 ID
        public string displayName;   // 멀티플레이어 UI 표시용 이름
        public string message;
        public DateTime timestamp;
        
        public ChatMessage()
        {
            timestamp = DateTime.Now;
        }
        
        public ChatMessage(string username, string message, string displayName = null)
        {
            this.username = username;
            this.displayName = displayName ?? username;
            this.message = message;
            this.timestamp = DateTime.Now;
        }
        
        public ChatMessage(string username, string message, DateTime timestamp, string displayName = null)
        {
            this.username = username;
            this.displayName = displayName ?? username;
            this.message = message;
            this.timestamp = timestamp;
        }
    }
}