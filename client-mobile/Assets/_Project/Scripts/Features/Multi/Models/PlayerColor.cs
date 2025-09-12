using UnityEngine;

namespace Features.Multi.Models
{
    /// <summary>
    /// 블로커스 플레이어 색상
    /// 4가지 고정 색상으로 플레이어를 구분
    /// </summary>
    public enum PlayerColor
    {
        None = -1,
        Blue = 0,   // 파랑 - playerId 0 (서버 playerColor 1)
        Yellow = 1, // 노랑 - playerId 1 (서버 playerColor 2)
        Red = 2,    // 빨강 - playerId 2 (서버 playerColor 3)
        Green = 3   // 초록 - playerId 3 (서버 playerColor 4)
    }
    
    /// <summary>
    /// PlayerColor 유틸리티 클래스
    /// </summary>
    public static class PlayerColorExtensions
    {
        private static readonly Color[] Colors = {
            Color.red,      // Red
            Color.blue,     // Blue
            Color.yellow,   // Yellow
            Color.green     // Green
        };
        
        private static readonly string[] Names = {
            "빨강",   // Red
            "파랑",   // Blue
            "노랑",   // Yellow
            "초록"    // Green
        };
        
        /// <summary>
        /// PlayerColor를 Unity Color로 변환
        /// </summary>
        public static Color ToUnityColor(this PlayerColor playerColor)
        {
            if (playerColor == PlayerColor.None || (int)playerColor < 0 || (int)playerColor >= Colors.Length)
            {
                return Color.gray; // 기본값
            }
            
            return Colors[(int)playerColor];
        }
        
        /// <summary>
        /// PlayerColor를 한글 이름으로 변환
        /// </summary>
        public static string ToKoreanName(this PlayerColor playerColor)
        {
            if (playerColor == PlayerColor.None || (int)playerColor < 0 || (int)playerColor >= Names.Length)
            {
                return "없음";
            }
            
            return Names[(int)playerColor];
        }
        
        /// <summary>
        /// 플레이어 ID를 PlayerColor로 변환
        /// </summary>
        public static PlayerColor FromPlayerId(int playerId)
        {
            if (playerId < 0 || playerId >= 4)
            {
                return PlayerColor.None;
            }
            
            return (PlayerColor)playerId;
        }
        
        /// <summary>
        /// 모든 유효한 플레이어 색상 가져오기
        /// </summary>
        public static PlayerColor[] GetAllValidColors()
        {
            return new PlayerColor[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Yellow, PlayerColor.Green };
        }
    }
}