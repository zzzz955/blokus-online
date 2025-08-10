using System;
using System.Collections.Generic;
using UnityEngine;
using BlokusUnity.Common;

namespace BlokusUnity.Network
{
    /// <summary>
    /// 네트워크 메시지 핸들러
    /// 서버로부터 수신된 메시지를 파싱하고 적절한 이벤트로 변환
    /// </summary>
    public class MessageHandler : MonoBehaviour
    {
        // ========================================
        // 이벤트 정의 (UI 시스템에서 구독)
        // ========================================
        
        // 인증 관련
        public event System.Action<bool, string> OnAuthResponse; // 성공여부, 메시지
        public event System.Action<UserInfo> OnMyStatsUpdated; // 내 통계 업데이트
        public event System.Action<UserInfo> OnUserStatsReceived; // 다른 사용자 통계
        
        // 로비 관련
        public event System.Action<List<RoomInfo>> OnRoomListUpdated; // 방 목록 업데이트
        public event System.Action<List<UserInfo>> OnUserListUpdated; // 사용자 목록 업데이트
        public event System.Action<RoomInfo> OnRoomCreated; // 방 생성됨
        public event System.Action<bool, string> OnJoinRoomResponse; // 방 참가 응답
        
        // 게임 관련
        public event System.Action<GameState> OnGameStateUpdated; // 게임 상태 업데이트
        public event System.Action<BlockPlacement> OnBlockPlaced; // 블록 배치됨
        public event System.Action<PlayerColor> OnTurnChanged; // 턴 변경
        public event System.Action<Dictionary<PlayerColor, int>> OnScoresUpdated; // 점수 업데이트
        public event System.Action<PlayerColor> OnGameEnded; // 게임 종료
        
        // 연결 관련
        public event System.Action<string> OnErrorReceived; // 에러 메시지
        public event System.Action OnHeartbeatReceived; // 하트비트 응답
        
        // 싱글플레이어 관련
        public event System.Action<StageData> OnStageDataReceived; // 스테이지 데이터 수신
        public event System.Action<UserStageProgress> OnStageProgressReceived; // 스테이지 진행도 수신
        public event System.Action<bool, string> OnStageCompleteResponse; // 스테이지 완료 응답
        public event System.Action<int> OnMaxStageUpdated; // 최대 스테이지 업데이트
        
        // 싱글톤 패턴
        public static MessageHandler Instance { get; private set; }
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // NetworkClient 메시지 수신 이벤트 구독
                if (NetworkClient.Instance != null)
                {
                    NetworkClient.Instance.OnMessageReceived += HandleMessage;
                }
                else
                {
                    Debug.LogWarning("NetworkClient가 아직 초기화되지 않음. Start에서 다시 시도.");
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void Start()
        {
            // NetworkClient가 늦게 초기화된 경우 대비
            if (NetworkClient.Instance != null)
            {
                NetworkClient.Instance.OnMessageReceived += HandleMessage;
            }
        }
        
        void OnDestroy()
        {
            // 이벤트 구독 해제
            if (NetworkClient.Instance != null)
            {
                NetworkClient.Instance.OnMessageReceived -= HandleMessage;
            }
        }
        
        // ========================================
        // 메시지 처리 메인 함수
        // ========================================
        
        /// <summary>
        /// 서버 메시지 처리 (C++과 동일한 ':' 구분자 프로토콜)
        /// </summary>
        private void HandleMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            
            try
            {
                // ':' 기준으로 메시지 파싱
                string[] parts = message.Split(':');
                if (parts.Length < 1)
                {
                    Debug.LogWarning($"잘못된 메시지 형식: {message}");
                    return;
                }
                
                string messageType = parts[0];
                Debug.Log($"메시지 타입: {messageType}");
                
                // 메시지 타입별 처리
                switch (messageType)
                {
                    // 인증 관련
                    case "AUTH_RESPONSE":
                        HandleAuthResponse(parts);
                        break;
                    case "MY_STATS_UPDATE":
                        HandleMyStatsUpdate(parts);
                        break;
                    case "USER_STATS_RESPONSE":
                        HandleUserStatsResponse(parts);
                        break;
                    
                    // 로비 관련
                    case "ROOM_LIST_UPDATE":
                        HandleRoomListUpdate(parts);
                        break;
                    case "USER_LIST_UPDATE":
                        HandleUserListUpdate(parts);
                        break;
                    case "ROOM_CREATED":
                        HandleRoomCreated(parts);
                        break;
                    case "JOIN_ROOM_RESPONSE":
                        HandleJoinRoomResponse(parts);
                        break;
                    
                    // 게임 관련
                    case "GAME_STATE_UPDATE":
                        HandleGameStateUpdate(parts);
                        break;
                    case "BLOCK_PLACED":
                        HandleBlockPlaced(parts);
                        break;
                    case "TURN_CHANGED":
                        HandleTurnChanged(parts);
                        break;
                    case "SCORES_UPDATE":
                        HandleScoresUpdate(parts);
                        break;
                    case "GAME_ENDED":
                        HandleGameEnded(parts);
                        break;
                    
                    // 시스템 관련
                    case "ERROR":
                        HandleError(parts);
                        break;
                    case "HEARTBEAT_RESPONSE":
                        HandleHeartbeatResponse(parts);
                        break;
                    
                    // 싱글플레이어 관련
                    case "STAGE_DATA_RESPONSE":
                        HandleStageDataResponse(parts);
                        break;
                    case "STAGE_PROGRESS_RESPONSE":
                        HandleStageProgressResponse(parts);
                        break;
                    case "STAGE_COMPLETE_RESPONSE":
                        HandleStageCompleteResponse(parts);
                        break;
                    case "MAX_STAGE_UPDATED":
                        HandleMaxStageUpdated(parts);
                        break;
                    
                    default:
                        Debug.LogWarning($"알 수 없는 메시지 타입: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"메시지 처리 중 오류: {ex.Message}");
                Debug.LogError($"문제가 된 메시지: {message}");
            }
        }
        
        // ========================================
        // 인증 메시지 핸들러들
        // ========================================
        
        /// <summary>
        /// 인증 응답 처리 - "AUTH_RESPONSE:SUCCESS/FAILURE:메시지"
        /// </summary>
        private void HandleAuthResponse(string[] parts)
        {
            if (parts.Length < 3)
            {
                Debug.LogError("AUTH_RESPONSE 메시지 형식 오류");
                return;
            }
            
            bool success = parts[1] == "SUCCESS";
            string message = parts[2];
            
            Debug.Log($"인증 응답: {(success ? "성공" : "실패")} - {message}");
            OnAuthResponse?.Invoke(success, message);
        }
        
        /// <summary>
        /// 내 통계 업데이트 - "MY_STATS_UPDATE:username:level:games:wins:losses:avgScore:totalScore:bestScore"
        /// </summary>
        private void HandleMyStatsUpdate(string[] parts)
        {
            if (parts.Length < 9)
            {
                Debug.LogError("MY_STATS_UPDATE 메시지 형식 오류");
                return;
            }
            
            try
            {
                UserInfo userInfo = new UserInfo
                {
                    username = parts[1],
                    level = int.Parse(parts[2]),
                    totalGames = int.Parse(parts[3]),
                    wins = int.Parse(parts[4]),
                    losses = int.Parse(parts[5]),
                    averageScore = int.Parse(parts[6]),
                    totalScore = int.Parse(parts[7]),
                    bestScore = int.Parse(parts[8]),
                    isOnline = true,
                    status = "온라인"
                };
                
                Debug.Log($"내 통계 업데이트: {userInfo.username} (레벨 {userInfo.level})");
                OnMyStatsUpdated?.Invoke(userInfo);
            }
            catch (Exception ex)
            {
                Debug.LogError($"MY_STATS_UPDATE 파싱 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 사용자 통계 응답 - "USER_STATS_RESPONSE:username:level:games:wins:losses:avgScore:totalScore:bestScore:online:status"
        /// </summary>
        private void HandleUserStatsResponse(string[] parts)
        {
            if (parts.Length < 11)
            {
                Debug.LogError("USER_STATS_RESPONSE 메시지 형식 오류");
                return;
            }
            
            try
            {
                UserInfo userInfo = new UserInfo
                {
                    username = parts[1],
                    level = int.Parse(parts[2]),
                    totalGames = int.Parse(parts[3]),
                    wins = int.Parse(parts[4]),
                    losses = int.Parse(parts[5]),
                    averageScore = int.Parse(parts[6]),
                    totalScore = int.Parse(parts[7]),
                    bestScore = int.Parse(parts[8]),
                    isOnline = bool.Parse(parts[9]),
                    status = parts[10]
                };
                
                Debug.Log($"사용자 통계 수신: {userInfo.username} (레벨 {userInfo.level})");
                OnUserStatsReceived?.Invoke(userInfo);
            }
            catch (Exception ex)
            {
                Debug.LogError($"USER_STATS_RESPONSE 파싱 오류: {ex.Message}");
            }
        }
        
        // ========================================
        // 로비 메시지 핸들러들
        // ========================================
        
        /// <summary>
        /// 방 목록 업데이트 - "ROOM_LIST_UPDATE:count:room1_data:room2_data:..."
        /// </summary>
        private void HandleRoomListUpdate(string[] parts)
        {
            if (parts.Length < 2)
            {
                Debug.LogError("ROOM_LIST_UPDATE 메시지 형식 오류");
                return;
            }
            
            try
            {
                int roomCount = int.Parse(parts[1]);
                List<RoomInfo> rooms = new List<RoomInfo>();
                
                for (int i = 0; i < roomCount; i++)
                {
                    if (parts.Length > i + 2)
                    {
                        // 방 데이터 파싱 (예: "roomId,roomName,playerCount,maxPlayers")
                        string[] roomData = parts[i + 2].Split(',');
                        if (roomData.Length >= 4)
                        {
                            RoomInfo room = new RoomInfo
                            {
                                roomId = int.Parse(roomData[0]),
                                roomName = roomData[1],
                                currentPlayers = int.Parse(roomData[2]),
                                maxPlayers = int.Parse(roomData[3]),
                                isGameStarted = roomData.Length > 4 && bool.Parse(roomData[4])
                            };
                            rooms.Add(room);
                        }
                    }
                }
                
                Debug.Log($"방 목록 업데이트: {rooms.Count}개 방");
                OnRoomListUpdated?.Invoke(rooms);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ROOM_LIST_UPDATE 파싱 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 사용자 목록 업데이트 (간단한 버전)
        /// </summary>
        private void HandleUserListUpdate(string[] parts)
        {
            // 필요에 따라 구현
            Debug.Log("사용자 목록 업데이트 수신");
            OnUserListUpdated?.Invoke(new List<UserInfo>());
        }
        
        /// <summary>
        /// 방 생성됨 - "ROOM_CREATED:roomId:roomName"
        /// </summary>
        private void HandleRoomCreated(string[] parts)
        {
            if (parts.Length < 3)
            {
                Debug.LogError("ROOM_CREATED 메시지 형식 오류");
                return;
            }
            
            try
            {
                RoomInfo room = new RoomInfo
                {
                    roomId = int.Parse(parts[1]),
                    roomName = parts[2],
                    currentPlayers = 1,
                    maxPlayers = 4,
                    isGameStarted = false
                };
                
                Debug.Log($"방 생성됨: {room.roomName} (ID: {room.roomId})");
                OnRoomCreated?.Invoke(room);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ROOM_CREATED 파싱 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 방 참가 응답 - "JOIN_ROOM_RESPONSE:SUCCESS/FAILURE:메시지"
        /// </summary>
        private void HandleJoinRoomResponse(string[] parts)
        {
            if (parts.Length < 3)
            {
                Debug.LogError("JOIN_ROOM_RESPONSE 메시지 형식 오류");
                return;
            }
            
            bool success = parts[1] == "SUCCESS";
            string message = parts[2];
            
            Debug.Log($"방 참가 응답: {(success ? "성공" : "실패")} - {message}");
            OnJoinRoomResponse?.Invoke(success, message);
        }
        
        // ========================================
        // 게임 메시지 핸들러들
        // ========================================
        
        /// <summary>
        /// 게임 상태 업데이트 (기본 구현)
        /// </summary>
        private void HandleGameStateUpdate(string[] parts)
        {
            Debug.Log("게임 상태 업데이트 수신");
            // GameState 파싱 로직 필요시 구현
        }
        
        /// <summary>
        /// 블록 배치됨 - "BLOCK_PLACED:blockType:row:col:rotation:flip:player"
        /// </summary>
        private void HandleBlockPlaced(string[] parts)
        {
            if (parts.Length < 7)
            {
                Debug.LogError("BLOCK_PLACED 메시지 형식 오류");
                return;
            }
            
            try
            {
                BlockPlacement placement = new BlockPlacement(
                    (BlockType)int.Parse(parts[1]),
                    new Position(int.Parse(parts[2]), int.Parse(parts[3])),
                    (Rotation)int.Parse(parts[4]),
                    (FlipState)int.Parse(parts[5]),
                    (PlayerColor)int.Parse(parts[6])
                );
                
                Debug.Log($"블록 배치됨: {placement.type} at ({placement.position.row}, {placement.position.col})");
                OnBlockPlaced?.Invoke(placement);
            }
            catch (Exception ex)
            {
                Debug.LogError($"BLOCK_PLACED 파싱 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 턴 변경 - "TURN_CHANGED:playerColor"
        /// </summary>
        private void HandleTurnChanged(string[] parts)
        {
            if (parts.Length < 2)
            {
                Debug.LogError("TURN_CHANGED 메시지 형식 오류");
                return;
            }
            
            try
            {
                PlayerColor newPlayer = (PlayerColor)int.Parse(parts[1]);
                Debug.Log($"턴 변경: {newPlayer}");
                OnTurnChanged?.Invoke(newPlayer);
            }
            catch (Exception ex)
            {
                Debug.LogError($"TURN_CHANGED 파싱 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 점수 업데이트 - "SCORES_UPDATE:player1:score1:player2:score2:..."
        /// </summary>
        private void HandleScoresUpdate(string[] parts)
        {
            try
            {
                Dictionary<PlayerColor, int> scores = new Dictionary<PlayerColor, int>();
                
                // 2개씩 묶어서 파싱 (플레이어:점수)
                for (int i = 1; i < parts.Length - 1; i += 2)
                {
                    PlayerColor player = (PlayerColor)int.Parse(parts[i]);
                    int score = int.Parse(parts[i + 1]);
                    scores[player] = score;
                }
                
                Debug.Log($"점수 업데이트: {scores.Count}명의 플레이어");
                OnScoresUpdated?.Invoke(scores);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SCORES_UPDATE 파싱 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 게임 종료 - "GAME_ENDED:winnerPlayer"
        /// </summary>
        private void HandleGameEnded(string[] parts)
        {
            if (parts.Length < 2)
            {
                Debug.LogError("GAME_ENDED 메시지 형식 오류");
                return;
            }
            
            try
            {
                PlayerColor winner = (PlayerColor)int.Parse(parts[1]);
                Debug.Log($"게임 종료: {winner} 승리");
                OnGameEnded?.Invoke(winner);
            }
            catch (Exception ex)
            {
                Debug.LogError($"GAME_ENDED 파싱 오류: {ex.Message}");
            }
        }
        
        // ========================================
        // 시스템 메시지 핸들러들
        // ========================================
        
        /// <summary>
        /// 에러 메시지 - "ERROR:에러내용"
        /// </summary>
        private void HandleError(string[] parts)
        {
            if (parts.Length < 2)
            {
                Debug.LogError("ERROR 메시지 형식 오류");
                return;
            }
            
            string errorMessage = string.Join(":", parts, 1, parts.Length - 1);
            Debug.LogError($"서버 에러: {errorMessage}");
            OnErrorReceived?.Invoke(errorMessage);
        }
        
        /// <summary>
        /// 하트비트 응답
        /// </summary>
        private void HandleHeartbeatResponse(string[] parts)
        {
            Debug.Log("하트비트 응답 수신");
            OnHeartbeatReceived?.Invoke();
        }
        
        // ========================================
        // 싱글플레이어 메시지 핸들러들
        // ========================================
        
        /// <summary>
        /// 스테이지 데이터 응답 - "STAGE_DATA_RESPONSE:stageNumber:stageName:difficulty:optimalScore:timeLimit:maxUndoCount:availableBlocks:initialBoardState:stageDescription"
        /// </summary>
        private void HandleStageDataResponse(string[] parts)
        {
            if (parts.Length < 9)
            {
                Debug.LogError("STAGE_DATA_RESPONSE 메시지 형식 오류");
                return;
            }
            
            try
            {
                StageData stageData = new StageData
                {
                    stageNumber = int.Parse(parts[1]),
                    stageName = parts[2],
                    difficulty = int.Parse(parts[3]),
                    optimalScore = int.Parse(parts[4]),
                    timeLimit = int.Parse(parts[5]) == 0 ? (int?)null : int.Parse(parts[5]),
                    maxUndoCount = int.Parse(parts[6])
                };
                
                // 사용 가능한 블록들 파싱 (콤마로 구분된 블록 ID)
                if (parts.Length > 7 && !string.IsNullOrEmpty(parts[7]))
                {
                    string[] blockIds = parts[7].Split(',');
                    stageData.availableBlocks = new List<BlokusUnity.Common.BlockType>();
                    
                    foreach (string blockId in blockIds)
                    {
                        if (int.TryParse(blockId, out int id))
                        {
                            stageData.availableBlocks.Add((BlokusUnity.Common.BlockType)id);
                        }
                    }
                }
                
                // 초기 보드 상태 (JSONB 데이터)
                if (parts.Length > 8 && !string.IsNullOrEmpty(parts[8]))
                {
                    stageData.initialBoardStateJson = parts[8];
                }
                
                // 스테이지 설명
                if (parts.Length > 9)
                {
                    stageData.stageDescription = parts[9];
                }
                
                Debug.Log($"스테이지 데이터 수신: {stageData.stageName} (난이도: {stageData.difficulty})");
                OnStageDataReceived?.Invoke(stageData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"STAGE_DATA_RESPONSE 파싱 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 스테이지 진행도 응답 - "STAGE_PROGRESS_RESPONSE:stageNumber:isCompleted:starsEarned:bestScore:bestTime:totalAttempts:successfulAttempts"
        /// </summary>
        private void HandleStageProgressResponse(string[] parts)
        {
            if (parts.Length < 8)
            {
                Debug.LogError("STAGE_PROGRESS_RESPONSE 메시지 형식 오류");
                return;
            }
            
            try
            {
                UserStageProgress progress = new UserStageProgress
                {
                    stageNumber = int.Parse(parts[1]),
                    isCompleted = bool.Parse(parts[2]),
                    starsEarned = int.Parse(parts[3]),
                    bestScore = int.Parse(parts[4]),
                    bestCompletionTime = int.Parse(parts[5]),
                    totalAttempts = int.Parse(parts[6]),
                    successfulAttempts = int.Parse(parts[7])
                };
                
                // 날짜 정보가 있다면 파싱 (추가 파라미터)
                if (parts.Length > 8 && !string.IsNullOrEmpty(parts[8]))
                {
                    progress.firstPlayedAt = System.DateTime.Parse(parts[8]);
                }
                if (parts.Length > 9 && !string.IsNullOrEmpty(parts[9]))
                {
                    progress.lastPlayedAt = System.DateTime.Parse(parts[9]);
                }
                
                Debug.Log($"스테이지 진행도 수신: {progress.stageNumber} (완료: {progress.isCompleted}, 별: {progress.starsEarned})");
                OnStageProgressReceived?.Invoke(progress);
            }
            catch (Exception ex)
            {
                Debug.LogError($"STAGE_PROGRESS_RESPONSE 파싱 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 스테이지 완료 응답 - "STAGE_COMPLETE_RESPONSE:SUCCESS/FAILURE:메시지"
        /// </summary>
        private void HandleStageCompleteResponse(string[] parts)
        {
            if (parts.Length < 3)
            {
                Debug.LogError("STAGE_COMPLETE_RESPONSE 메시지 형식 오류");
                return;
            }
            
            bool success = parts[1] == "SUCCESS";
            string message = parts[2];
            
            Debug.Log($"스테이지 완료 응답: {(success ? "성공" : "실패")} - {message}");
            OnStageCompleteResponse?.Invoke(success, message);
        }
        
        /// <summary>
        /// 최대 스테이지 업데이트 - "MAX_STAGE_UPDATED:maxStageCompleted"
        /// </summary>
        private void HandleMaxStageUpdated(string[] parts)
        {
            if (parts.Length < 2)
            {
                Debug.LogError("MAX_STAGE_UPDATED 메시지 형식 오류");
                return;
            }
            
            try
            {
                int maxStageCompleted = int.Parse(parts[1]);
                Debug.Log($"최대 스테이지 업데이트: {maxStageCompleted}");
                OnMaxStageUpdated?.Invoke(maxStageCompleted);
            }
            catch (Exception ex)
            {
                Debug.LogError($"MAX_STAGE_UPDATED 파싱 오류: {ex.Message}");
            }
        }
    }
    
    // ========================================
    // 데이터 구조체들
    // ========================================
    
    /// <summary>
    /// 사용자 정보 구조체
    /// </summary>
    [System.Serializable]
    public class UserInfo
    {
        public string username;
        public int level;
        public int totalGames;
        public int wins;
        public int losses;
        public int averageScore;
        public int totalScore;
        public int bestScore;
        public bool isOnline;
        public string status;
        
        /// <summary>
        /// 승률 계산
        /// </summary>
        public double GetWinRate()
        {
            if (totalGames <= 0) return 0.0;
            return (double)wins / totalGames * 100.0;
        }
    }
    
    /// <summary>
    /// 방 정보 구조체
    /// </summary>
    [System.Serializable]
    public class RoomInfo
    {
        public int roomId;
        public string roomName;
        public int currentPlayers;
        public int maxPlayers;
        public bool isGameStarted;
        
        /// <summary>
        /// 방이 가득 찼는지 확인
        /// </summary>
        public bool IsFull()
        {
            return currentPlayers >= maxPlayers;
        }
        
        /// <summary>
        /// 참가 가능한지 확인
        /// </summary>
        public bool CanJoin()
        {
            return !IsFull() && !isGameStarted;
        }
    }
    
    /// <summary>
    /// 스테이지 데이터 구조체 (네트워크 전용)
    /// </summary>
    [System.Serializable]
    public class StageData
    {
        public int stageNumber;
        public string stageName;
        public int difficulty;
        public int optimalScore;
        public int? timeLimit; // null이면 무제한
        public int maxUndoCount;
        public List<BlokusUnity.Common.BlockType> availableBlocks;
        public string initialBoardStateJson; // JSONB 데이터 문자열
        public string stageDescription;
        
        /// <summary>
        /// 난이도 텍스트 반환
        /// </summary>
        public string GetDifficultyText()
        {
            return difficulty switch
            {
                1 => "쉬움",
                2 => "보통", 
                3 => "어려움",
                4 => "매우 어려움",
                5 => "극한",
                _ => "알 수 없음"
            };
        }
        
        /// <summary>
        /// 제한시간 텍스트 반환
        /// </summary>
        public string GetTimeLimitText()
        {
            if (!timeLimit.HasValue || timeLimit.Value <= 0)
                return "무제한";
            
            int minutes = timeLimit.Value / 60;
            int seconds = timeLimit.Value % 60;
            
            if (minutes > 0)
                return $"{minutes}분 {seconds}초";
            else
                return $"{seconds}초";
        }
    }
    
    /// <summary>
    /// 사용자 스테이지 진행도 구조체 (네트워크 전용 - Game 네임스페이스와 분리)
    /// </summary>
    [System.Serializable]
    public class UserStageProgress
    {
        public int stageNumber;
        public bool isCompleted;
        public int starsEarned;
        public int bestScore;
        public int bestCompletionTime; // 초 단위
        public int totalAttempts;
        public int successfulAttempts;
        public System.DateTime firstPlayedAt;
        public System.DateTime lastPlayedAt;
        
        /// <summary>
        /// 성공률 계산
        /// </summary>
        public float GetSuccessRate()
        {
            if (totalAttempts <= 0) return 0f;
            return (float)successfulAttempts / totalAttempts * 100f;
        }
        
        /// <summary>
        /// 별 문자열 반환
        /// </summary>
        public string GetStarString()
        {
            return starsEarned switch
            {
                3 => "⭐⭐⭐",
                2 => "⭐⭐☆",
                1 => "⭐☆☆",
                _ => "☆☆☆"
            };
        }
    }
}