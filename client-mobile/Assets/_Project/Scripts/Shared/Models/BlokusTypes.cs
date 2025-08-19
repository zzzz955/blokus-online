using System;
using System.Collections.Generic;
using UnityEngine;
using Features.Multi.Net;
using Features.Single.Core;
namespace Shared.Models{
    // ========================================
    // 기본 상수 정의 (Types.h 포팅)
    // ========================================

    public static class GameConstants
    {
        public const int BOARD_SIZE = 20;              // 클래식 모드 (고정)
        public const int MAX_PLAYERS = 4;              // 최대 플레이어 수
        public const int BLOCKS_PER_PLAYER = 21;       // 플레이어당 블록 수
        public const int DEFAULT_TURN_TIME = 30;       // 기본 턴 제한시간 (30초)

        // 서버 관련 상수
        public const int MAX_CONCURRENT_USERS = 1000;
        public const ushort DEFAULT_SERVER_PORT = 7777;

        // 게임 관련 상수
        public const int MIN_PLAYERS_TO_START = 2;
        public const int MAX_ROOM_NAME_LENGTH = 50;
        public const int MAX_USERNAME_LENGTH = 20;
        public const int MIN_USERNAME_LENGTH = 3;
    }

    // ========================================
    // 기본 타입 정의 (C# 적응)
    // ========================================

    /// <summary>
    /// 보드 위치 (행, 열) - Unity Vector2Int 사용
    /// </summary>
    [System.Serializable]
    public struct Position : IEquatable<Position>
    {
        public int row;
        public int col;

        public Position(int row, int col)
        {
            this.row = row;
            this.col = col;
        }

        public Vector2Int ToVector2Int() => new Vector2Int(col, row);
        public static Position FromVector2Int(Vector2Int vec) => new Position(vec.y, vec.x);

        public bool Equals(Position other) => row == other.row && col == other.col;
        public override bool Equals(object obj) => obj is Position pos && Equals(pos);
        public override int GetHashCode() => HashCode.Combine(row, col);
        public static bool operator ==(Position a, Position b) => a.Equals(b);
        public static bool operator !=(Position a, Position b) => !a.Equals(b);
        public override string ToString() => $"({row},{col})";
    }

    // ========================================
    // 열거형 정의
    // ========================================

    /// <summary>
    /// 플레이어 색상 열거형
    /// </summary>
    public enum PlayerColor : byte
    {
        None = 0,     // 빈 칸
        Blue = 1,     // 파랑 (플레이어 1)
        Yellow = 2,   // 노랑 (플레이어 2)  
        Red = 3,      // 빨강 (플레이어 3)
        Green = 4,    // 초록 (플레이어 4)
        Obstacle = 5  // 장애물 (어두운 회색)
    }

    /// <summary>
    /// 블록 타입 (직관적이고 일관성 있는 명명)
    /// </summary>
    public enum BlockType : byte
    {
        // 1칸 블록
        Single = 1,

        // 2칸 블록  
        Domino = 2,

        // 3칸 블록
        TrioLine = 3,       // 3일자
        TrioAngle = 4,      // 3꺾임

        // 4칸 블록 (테트로미노)
        Tetro_I = 5,        // 4일자
        Tetro_O = 6,        // 정사각형
        Tetro_T = 7,        // T자
        Tetro_L = 8,        // L자
        Tetro_S = 9,        // S자 (Z자)

        // 5칸 블록 (펜토미노)
        Pento_F = 10,       // F자
        Pento_I = 11,       // 5일자
        Pento_L = 12,       // 5L자
        Pento_N = 13,       // N자
        Pento_P = 14,       // P자
        Pento_T = 15,       // 5T자
        Pento_U = 16,       // U자
        Pento_V = 17,       // V자
        Pento_W = 18,       // W자
        Pento_X = 19,       // X자
        Pento_Y = 20,       // Y자
        Pento_Z = 21        // 5Z자
    }

    /// <summary>
    /// 블록 회전 상태
    /// </summary>
    public enum Rotation : byte
    {
        Degree_0 = 0,   // 0도
        Degree_90 = 1,  // 90도 시계방향
        Degree_180 = 2, // 180도
        Degree_270 = 3  // 270도 시계방향
    }

    /// <summary>
    /// 블록 뒤집기 상태
    /// </summary>
    public enum FlipState : byte
    {
        Normal = 0,     // 정상
        Horizontal = 1, // 수평 뒤집기
        Vertical = 2,   // 수직 뒤집기
        Both = 3        // 양쪽 뒤집기
    }

    /// <summary>
    /// 게임 상태
    /// </summary>
    public enum GameState : byte
    {
        Waiting,     // 대기 중
        Playing,     // 게임 중
        Finished,    // 게임 종료
        Paused       // 일시정지
    }

    /// <summary>
    /// 턴 상태 (서버용)
    /// </summary>
    public enum TurnState : byte
    {
        WaitingForMove,    // 이동 대기
        PlacingBlock,      // 블록 배치 중
        TurnComplete,      // 턴 완료
        Skipped           // 턴 건너뜀
    }

    // ========================================
    // 구조체 정의
    // ========================================

    /// <summary>
    /// 블록 배치 정보
    /// </summary>
    [System.Serializable]
    public struct BlockPlacement
    {
        public BlockType type;             // 블록 타입
        public Position position;          // 보드 위치 (행, 열)
        public Rotation rotation;          // 회전 상태
        public FlipState flip;             // 뒤집기 상태
        public PlayerColor player;         // 소유 플레이어

        public BlockPlacement(BlockType type, Position position, PlayerColor player) 
            : this(type, position, Rotation.Degree_0, FlipState.Normal, player) { }

        public BlockPlacement(BlockType type, Position position, Rotation rotation, FlipState flip, PlayerColor player)
        {
            this.type = type;
            this.position = position;
            this.rotation = rotation;
            this.flip = flip;
            this.player = player;
        }

        public static BlockPlacement Default => new BlockPlacement(BlockType.Single, new Position(0, 0), PlayerColor.None);
    }

    /// <summary>
    /// 게임 설정
    /// </summary>
    [System.Serializable]
    public struct GameSettings
    {
        public int turnTimeLimit;          // 턴 제한시간 (초)
        public bool allowSpectators;       // 관전 허용
        public string gameMode;            // "클래식", "듀얼" 등

        public static GameSettings Default => new GameSettings
        {
            turnTimeLimit = GameConstants.DEFAULT_TURN_TIME,
            allowSpectators = true,
            gameMode = "클래식"
        };
    }

    // ========================================
    // 사용자 정보 구조체
    // ========================================

    /// <summary>
    /// 사용자 정보
    /// </summary>
    [System.Serializable]
    public class UserInfo
    {
        public string username = "익명";       // 사용자명
        public int level = 1;                  // 경험치 레벨 (게임 수에 따라 증가)
        public int totalGames = 0;             // 총 게임 수
        public int wins = 0;                   // 승리 수
        public int losses = 0;                 // 패배 수
        public int averageScore = 0;           // 평균 점수
        public bool isOnline = true;           // 온라인 상태
        public string status = "로비";         // "로비", "게임중", "자리비움"
        
        // 싱글플레이어 진행도
        public int maxStageCompleted = 0;      // 최대 클리어한 스테이지 번호

        /// <summary>
        /// 승률 계산
        /// </summary>
        public double GetWinRate()
        {
            return totalGames > 0 ? (double)wins / totalGames * 100.0 : 0.0;
        }

        /// <summary>
        /// 레벨 계산 (10게임당 1레벨)
        /// </summary>
        public int CalculateLevel()
        {
            return (totalGames / 10) + 1;
        }
    }

    /// <summary>
    /// 방 정보 구조체
    /// </summary>
    [System.Serializable]
    public class RoomInfo
    {
        public int roomId = 0;
        public string roomName = "새 방";
        public string hostName = "호스트";
        public int currentPlayers = 1;
        public int maxPlayers = 4;
        public bool isPrivate = false;
        public bool isPlaying = false;
        public string gameMode = "클래식";
    }

    /// <summary>
    /// 플레이어 슬롯 (게임 룸용)
    /// </summary>
    [System.Serializable]
    public class PlayerSlot
    {
        public PlayerColor color = PlayerColor.None;          // 플레이어 색상
        public string username = "";                          // 플레이어 이름
        public bool isHost = false;                           // 호스트 여부
        public bool isReady = false;                          // 준비 상태
        public int score = 0;                                 // 현재 점수
        public int remainingBlocks = GameConstants.BLOCKS_PER_PLAYER; // 남은 블록 수
    }

    /// <summary>
    /// 게임 세션 관리용
    /// </summary>
    [System.Serializable]
    public class GameSession
    {
        public int roomId = 0;
        public GameState state = GameState.Waiting;
        public PlayerSlot[] players = new PlayerSlot[GameConstants.MAX_PLAYERS];
        public int currentPlayerIndex = 0;
        public int turnNumber = 1;
        public DateTime startTime;
        public DateTime lastMoveTime;
        public GameSettings settings = GameSettings.Default;

        public GameSession() 
        {
            for (int i = 0; i < players.Length; i++)
                players[i] = new PlayerSlot();
        }

        public GameSession(int roomId) : this()
        {
            this.roomId = roomId;
        }

        /// <summary>
        /// 현재 플레이어 색상 가져오기
        /// </summary>
        public PlayerColor GetCurrentPlayerColor()
        {
            if (currentPlayerIndex >= 0 && currentPlayerIndex < GameConstants.MAX_PLAYERS)
                return players[currentPlayerIndex].color;
            return PlayerColor.None;
        }

        /// <summary>
        /// 지정된 색상의 플레이어 턴인지 확인
        /// </summary>
        public bool IsPlayerTurn(PlayerColor color)
        {
            return GetCurrentPlayerColor() == color;
        }

        /// <summary>
        /// 다음 턴으로 진행
        /// </summary>
        public void NextTurn()
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % GameConstants.MAX_PLAYERS;
            if (currentPlayerIndex == 0)
                turnNumber++;
        }

        /// <summary>
        /// 게임 시작 가능한지 확인
        /// </summary>
        public bool CanStartGame()
        {
            int activePlayers = 0;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].color != PlayerColor.None && !string.IsNullOrEmpty(players[i].username))
                    activePlayers++;
            }
            return activePlayers >= GameConstants.MIN_PLAYERS_TO_START;
        }
    }

    // ========================================
    // 유틸리티 함수들
    // ========================================

    /// <summary>
    /// 플레이어 색상 관련 유틸리티
    /// </summary>
    public static class PlayerColorUtility
    {
        /// <summary>
        /// 플레이어 색상을 문자열로 변환
        /// </summary>
        public static string ToString(PlayerColor color)
        {
            return color switch
            {
                PlayerColor.Blue => "Blue",
                PlayerColor.Yellow => "Yellow",
                PlayerColor.Red => "Red",
                PlayerColor.Green => "Green",
                PlayerColor.Obstacle => "Obstacle",
                PlayerColor.None => "None",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// 문자열을 플레이어 색상으로 변환
        /// </summary>
        public static PlayerColor FromString(string str)
        {
            return str switch
            {
                "Blue" => PlayerColor.Blue,
                "Yellow" => PlayerColor.Yellow,
                "Red" => PlayerColor.Red,
                "Green" => PlayerColor.Green,
                "Obstacle" => PlayerColor.Obstacle,
                _ => PlayerColor.None
            };
        }

        /// <summary>
        /// 플레이어 색상을 Unity Color로 변환
        /// </summary>
        public static Color ToUnityColor(PlayerColor color)
        {
            return color switch
            {
                PlayerColor.Blue => Color.blue,
                PlayerColor.Yellow => Color.yellow,
                PlayerColor.Red => Color.red,
                PlayerColor.Green => Color.green,
                PlayerColor.Obstacle => new Color(0.3f, 0.3f, 0.3f, 1f), // 어두운 회색
                _ => Color.gray
            };
        }
    }

    /// <summary>
    /// 게임 상태 관련 유틸리티
    /// </summary>
    public static class GameStateUtility
    {
        /// <summary>
        /// 게임 상태를 문자열로 변환
        /// </summary>
        public static string ToString(GameState state)
        {
            return state switch
            {
                GameState.Waiting => "Waiting",
                GameState.Playing => "Playing",
                GameState.Finished => "Finished",
                GameState.Paused => "Paused",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// 문자열을 게임 상태로 변환
        /// </summary>
        public static GameState FromString(string str)
        {
            return str switch
            {
                "Waiting" => GameState.Waiting,
                "Playing" => GameState.Playing,
                "Finished" => GameState.Finished,
                "Paused" => GameState.Paused,
                _ => GameState.Waiting
            };
        }
    }

    /// <summary>
    /// 검증 함수들
    /// </summary>
    public static class ValidationUtility
    {
        /// <summary>
        /// 유효한 사용자명인지 확인
        /// </summary>
        public static bool IsValidUsername(string username)
        {
            return !string.IsNullOrEmpty(username) && 
                   username.Length >= GameConstants.MIN_USERNAME_LENGTH &&
                   username.Length <= GameConstants.MAX_USERNAME_LENGTH;
        }

        /// <summary>
        /// 유효한 방 이름인지 확인
        /// </summary>
        public static bool IsValidRoomName(string roomName)
        {
            return !string.IsNullOrEmpty(roomName) && 
                   roomName.Length <= GameConstants.MAX_ROOM_NAME_LENGTH;
        }

        /// <summary>
        /// 유효한 보드 위치인지 확인
        /// </summary>
        public static bool IsValidPosition(Position pos)
        {
            return pos.row >= 0 && pos.row < GameConstants.BOARD_SIZE &&
                   pos.col >= 0 && pos.col < GameConstants.BOARD_SIZE;
        }
    }
    
    /// <summary>
    /// 사용자 스테이지 진행도 - API 응답 구조에 맞게 업데이트
    /// </summary>
    [System.Serializable]
    public struct UserStageProgress
    {
        public int stage_number;
        public bool is_completed;
        public int stars_earned;
        public int best_score;
        public int best_completion_time;
        public int total_attempts;
        public int successful_attempts;
        public string first_played_at;
        public string first_completed_at;
        public string last_played_at;
        
        // Unity에서 사용하기 위한 편의 프로퍼티
        public int stageNumber => stage_number;
        public bool isCompleted => is_completed;
        public int starsEarned => stars_earned;
        public int bestScore => best_score;
        public int bestTime => best_completion_time;
        public int totalAttempts => total_attempts;
        public int successfulAttempts => successful_attempts;
    }
}