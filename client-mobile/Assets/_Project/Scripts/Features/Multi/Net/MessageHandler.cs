using System;
using System.Collections.Generic;
using UnityEngine;
using Shared.Models;
using Features.Multi.Net;
using MultiModels = Features.Multi.Models;

namespace Features.Multi.Net
{
    /// <summary>
    /// 플레이어 데이터 구조체 (ROOM_INFO에서 파싱용)
    /// </summary>
    [System.Serializable]
    public struct PlayerData
    {
        public int playerId;
        public string username;
        public string displayName;
        public bool isHost;
        public bool isReady;
        public int colorSlot;
    }

    /// <summary>
    /// 턴 변경 정보 구조체 (TURN_CHANGED JSON에서 파싱용)
    /// </summary>
    [System.Serializable]
    public struct TurnChangeInfo
    {
        public string newPlayer;
        public int playerColor;
        public int turnNumber;
        public int turnTimeSeconds;
        public int remainingTimeSeconds;
        public bool previousTurnTimedOut;
    }
    
    /// <summary>
    /// 게임 상태 정보 구조체 (GAME_STATE_UPDATE JSON에서 파싱용)
    /// </summary>
    [System.Serializable]
    public struct GameStateData
    {
        public int currentPlayer;
        public int turnNumber;
        public int[] boardState; // 1차원 배열로 수신 (400개 요소 = 20x20)
        public object scores; // 점수 정보 (빈 객체이거나 플레이어별 점수)
        public object remainingBlocks; // 남은 블록 개수 (플레이어별)
        
        /// <summary>
        /// 1차원 배열을 20x20 2차원 배열로 변환
        /// </summary>
        public int[,] GetBoardState2D()
        {
            const int BOARD_SIZE = 20;
            var result = new int[BOARD_SIZE, BOARD_SIZE];
            
            if (boardState != null && boardState.Length == BOARD_SIZE * BOARD_SIZE)
            {
                for (int i = 0; i < boardState.Length; i++)
                {
                    int row = i / BOARD_SIZE;
                    int col = i % BOARD_SIZE;
                    result[row, col] = boardState[i];
                }
            }
            
            return result;
        }
    }

    /// <summary>
    /// 게임 결과 데이터 구조체 (새로운 GAME_RESULT 메시지 형식)
    /// </summary>
    [System.Serializable]
    public class GameResultData
    {
        // 공통 정보 (모든 클라이언트)
        public System.Collections.Generic.Dictionary<string, int> scores;
        public string[] winners;
        public string gameType;
        public int roomId;
        public string timestamp;

        // 개인별 정보 (각 플레이어마다 다름)
        public int myRank;         // 내 순위
        public int myScore;        // 내 점수
        public int expGained;      // 획득 경험치
        public bool levelUp;       // 레벨업 여부
        public int newLevel;       // 현재/새 레벨
        public int gameTime;       // 게임 진행 시간 (초)

        public GameResultData()
        {
            scores = new System.Collections.Generic.Dictionary<string, int>();
            winners = new string[0];
            gameType = "";
            roomId = 0;
            timestamp = "";
            myRank = 0;
            myScore = 0;
            expGained = 0;
            levelUp = false;
            newLevel = 1;
            gameTime = 0;
        }
    }

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
        public event System.Action OnRoomJoined; // 방 참가됨 (GameRoomPanel로 전환용)
        public event System.Action OnRoomLeft; // 방 나가기 완료 (LobbyPanel로 전환용)
        public event System.Action<RoomInfo, List<PlayerData>> OnRoomInfoUpdated; // 방 정보 및 플레이어 데이터 업데이트
        
        // 게임 관련
        public event System.Action OnGameStarted; // 게임 시작됨
        public event System.Action<GameStateData> OnGameStateUpdate; // 게임 상태 업데이트
        public event System.Action<MultiModels.BlockPlacement> OnBlockPlaced; // 블록 배치됨
        public event System.Action<TurnChangeInfo> OnTurnChanged; // 턴 변경 (상세 정보)
        public event System.Action<Dictionary<MultiModels.PlayerColor, int>> OnScoresUpdated; // 점수 업데이트
        public event System.Action<MultiModels.PlayerColor> OnGameEnded; // 게임 종료
        
        // 연결 관련
        public event System.Action<string> OnErrorReceived; // 에러 메시지
        public event System.Action OnHeartbeatReceived; // 하트비트 응답
        
        // 채팅 관련
        public event System.Action<string, string, string> OnChatMessageReceived; // username, displayName, message
        
        // 플레이어 상태 관련
        public event System.Action<string> OnPlayerJoined; // 플레이어 입장 (username)
        public event System.Action<string> OnPlayerLeft; // 플레이어 퇴장 (username)
        public event System.Action<string, bool> OnPlayerReadyChanged; // 플레이어 준비 상태 (username, isReady)
        public event System.Action OnPlayerSystemJoined; // 시스템 메시지 기반 플레이어 입장 감지
        
        // AFK 관련 이벤트
        public event System.Action OnAfkVerifyReceived; // AFK 검증 요청 수신
        public event System.Action OnAfkUnblockSuccess; // AFK 해제 성공
        public event System.Action<string> OnAfkStatusReset; // AFK 상태 리셋 (username)

        // 게임 결과 관련 이벤트
        public event System.Action<GameResultData> OnGameResultReceived; // 새로운 GAME_RESULT 데이터 수신
        
        // 싱글플레이어 관련 (현재 HTTP API로 대체됨)
        // public event System.Action<StageData> OnStageDataReceived; // TCP에서 HTTP API로 이동
        // public event System.Action<UserStageProgress> OnStageProgressReceived; // TCP에서 HTTP API로 이동
        // public event System.Action<bool, string> OnStageCompleteResponse; // TCP에서 HTTP API로 이동
        // public event System.Action<int> OnMaxStageUpdated; // TCP에서 HTTP API로 이동
        
        // 싱글톤 패턴
        public static MessageHandler Instance { get; private set; }
        
        // 중복 구독 방지
        private bool isSubscribedToNetworkClient = false;

        // 방 입장 상태 추적 (중복 OnRoomJoined 방지)
        private bool hasJoinedRoom = false;
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // NetworkClient 메시지 수신 이벤트 구독
                if (NetworkClient.Instance != null && !isSubscribedToNetworkClient)
                {
                    NetworkClient.Instance.OnMessageReceived += HandleMessage;
                    isSubscribedToNetworkClient = true;
                    Debug.Log("[MessageHandler] NetworkClient에 메시지 핸들러 구독 (Awake)");
                }
                else if (isSubscribedToNetworkClient)
                {
                    Debug.Log("[MessageHandler] 이미 NetworkClient에 구독됨 (Awake)");
                }
                else
                {
                    Debug.LogWarning("[MessageHandler] NetworkClient가 아직 초기화되지 않음. Start에서 다시 시도.");
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
            if (NetworkClient.Instance != null && !isSubscribedToNetworkClient)
            {
                NetworkClient.Instance.OnMessageReceived += HandleMessage;
                isSubscribedToNetworkClient = true;
                Debug.Log("[MessageHandler] NetworkClient에 메시지 핸들러 구독 완료 (Start)");
            }
            else if (isSubscribedToNetworkClient)
            {
                Debug.Log("[MessageHandler] 이미 NetworkClient에 구독됨 (Start)");
            }
        }
        
        void OnDestroy()
        {
            // 이벤트 구독 해제
            if (NetworkClient.Instance != null && isSubscribedToNetworkClient)
            {
                NetworkClient.Instance.OnMessageReceived -= HandleMessage;
                isSubscribedToNetworkClient = false;
                Debug.Log("[MessageHandler] NetworkClient 구독 해제");
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
            
            // Unity 메인스레드에서 처리하도록 디스패칭
            UnityMainThreadDispatcher.Enqueue(() => HandleMessageInternal(message));
        }
        
        /// <summary>
        /// 실제 메시지 처리 로직 (Unity 메인스레드에서 실행)
        /// 데스크톱 클라이언트와 동일한 프로토콜 처리
        /// </summary>
        private void HandleMessageInternal(string message)
        {
            try
            {
                // ':' 기준으로 메시지 파싱
                string[] parts = message.Split(':');
                if (parts.Length < 1)
                {
                    Debug.LogWarning($"[MessageHandler] 잘못된 메시지 형식: {message}");
                    return;
                }
                
                string messageType = parts[0];
                Debug.Log($"[MessageHandler] 메시지 타입: {messageType}");
                
                // 서버 프로토콜에 맞는 메시지 타입별 처리
                switch (messageType)
                {
                    // 인증 관련 (서버 응답)
                    case "AUTH_SUCCESS":
                        HandleAuthSuccess(parts);
                        break;
                    case "REGISTER_SUCCESS":
                        HandleRegisterSuccess(parts);
                        break;
                    case "LOGOUT_SUCCESS":
                        HandleLogoutSuccess(parts);
                        break;
                    
                    // 로비 관련 (서버 응답)
                    case "LOBBY_ENTER_SUCCESS":
                        HandleLobbyEnterSuccess(parts);
                        break;
                    case "LOBBY_LEAVE_SUCCESS":
                        HandleLobbyLeaveSuccess(parts);
                        break;
                    case "LOBBY_USER_LIST":
                        HandleLobbyUserList(parts);
                        break;
                    case "LOBBY_USER_JOINED":
                        HandleLobbyUserJoined(parts);
                        break;
                    case "LOBBY_USER_LEFT":
                        HandleLobbyUserLeft(parts);
                        break;
                    
                    // 방 관련 (서버 응답)
                    case "ROOM_LIST":
                        HandleRoomList(parts);
                        break;
                    case "ROOM_CREATED":
                        HandleRoomCreated(parts);
                        break;
                    case "ROOM_JOIN_SUCCESS":
                        HandleRoomJoinSuccess(parts);
                        break;
                    case "ROOM_LEFT":
                        HandleRoomLeft(parts);
                        break;
                    case "ROOM_INFO":
                        HandleRoomInfo(parts);
                        break;
                    
                    // 플레이어 관련 (서버 응답)
                    case "PLAYER_JOINED":
                        HandlePlayerJoined(parts);
                        break;
                    case "PLAYER_LEFT":
                        HandlePlayerLeft(parts);
                        break;
                    case "PLAYER_READY":
                        HandlePlayerReady(parts);
                        break;
                    case "HOST_CHANGED":
                        HandleHostChanged(parts);
                        break;
                    
                    // 게임 관련 (서버 응답)
                    case "GAME_STARTED":
                        HandleGameStarted(parts);
                        break;
                    case "GAME_PLAYER_INFO":
                        HandleGamePlayerInfo(parts);
                        break;
                    case "GAME_STATE_UPDATE":
                        HandleGameStateUpdate(parts);
                        break;
                    case "BLOCK_PLACED":
                        HandleBlockPlaced(parts);
                        break;
                    case "TURN_CHANGED":
                        HandleTurnChanged(parts);
                        break;
                    case "TURN_TIMEOUT":
                        HandleTurnTimeout(parts);
                        break;
                    case "GAME_ENDED":
                        HandleGameEnded(parts);
                        break;
                    case "GAME_MOVE_SUCCESS":
                        HandleGameMoveSuccess(parts);
                        break;
                    case "GAME_RESULT":
                        HandleGameResult(parts);
                        break;
                    case "GAME_RESET":
                        HandleGameReset(parts);
                        break;
                    
                    // 채팅 관련
                    case "CHAT":
                        HandleChat(parts);
                        break;
                    case "CHAT_SUCCESS":
                        HandleChatSuccess(parts);
                        break;
                    case "SYSTEM":
                        HandleSystemMessage(parts);
                        break;
                    
                    // 사용자 정보 관련
                    case "USER_STATS_RESPONSE":
                        HandleUserStatsResponse(parts);
                        break;
                    case "MY_STATS_UPDATE":
                        HandleMyStatsUpdate(parts);
                        break;
                    
                    // AFK 관련
                    case "AFK_VERIFY":
                        HandleAfkVerify(parts);
                        break;
                    case "AFK_MODE_ACTIVATED":
                        HandleAfkModeActivated(parts);
                        break;
                    case "AFK_UNBLOCK_SUCCESS":
                        HandleAfkUnblockSuccess(parts);
                        break;
                    case "AFK_STATUS_RESET":
                        HandleAfkStatusReset(parts);
                        break;
                    
                    // 버전 체크
                    case "version":
                        HandleVersionCheck(parts);
                        break;
                    
                    // 에러 처리
                    case "ERROR":
                        HandleError(parts);
                        break;
                    
                    // 하트비트/핑
                    case "pong":
                        HandlePong(parts);
                        break;
                    
                    default:
                        Debug.LogWarning($"[MessageHandler] 알 수 없는 메시지 타입: {messageType}");
                        Debug.LogWarning($"[MessageHandler] 전체 메시지: {message}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MessageHandler] 메시지 처리 중 오류: {ex.Message}");
                Debug.LogError($"[MessageHandler] 문제가 된 메시지: {message}");
            }
        }
        
        // ========================================
        // 인증 메시지 핸들러들 (서버 프로토콜 기준)
        // ========================================
        
        /// <summary>
        /// 인증 성공 처리 - "AUTH_SUCCESS:username:sessionToken:displayName:level:totalGames:wins:losses:totalScore:bestScore:experiencePoints"
        /// </summary>
        private void HandleAuthSuccess(string[] parts)
        {
            try
            {
                if (parts.Length >= 10)
                {
                    string username = parts[1];
                    string sessionToken = parts[2];
                    string displayName = parts[3];
                    int level = int.Parse(parts[4]);
                    int totalGames = int.Parse(parts[5]);
                    int wins = int.Parse(parts[6]);
                    int losses = int.Parse(parts[7]);
                    int totalScore = int.Parse(parts[8]);
                    int bestScore = int.Parse(parts[9]);
                    int experiencePoints = parts.Length > 10 ? int.Parse(parts[10]) : 0;
                    
                    // UserInfo 객체 생성
                    UserInfo userInfo = new UserInfo
                    {
                        username = username,
                        displayName = displayName,
                        level = level,
                        totalGames = totalGames,
                        wins = wins,
                        losses = losses,
                        totalScore = totalScore,
                        bestScore = bestScore,
                        isOnline = true,
                        status = "온라인"
                    };
                    
                    Debug.Log($"[MessageHandler] 인증 성공: {username} (세션토큰: {sessionToken.Substring(0, Math.Min(10, sessionToken.Length))}...)");
                    
                    // 인증 성공과 사용자 정보를 동시에 전달
                    OnAuthResponse?.Invoke(true, $"로그인 성공: {displayName}");
                    OnMyStatsUpdated?.Invoke(userInfo);
                }
                else if (parts.Length >= 3)
                {
                    // 기본 형태 지원
                    string username = parts[1];
                    string sessionToken = parts[2];
                    
                    Debug.Log($"[MessageHandler] 인증 성공 (기본형태): {username}");
                    
                    OnAuthResponse?.Invoke(true, $"로그인 성공: {username}");
                }
                else
                {
                    Debug.LogError("[MessageHandler] AUTH_SUCCESS 메시지 형식 오류");
                    OnAuthResponse?.Invoke(false, "인증 응답 형식이 올바르지 않습니다.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MessageHandler] AUTH_SUCCESS 파싱 오류: {ex.Message}");
                OnAuthResponse?.Invoke(false, "인증 정보 처리 중 오류가 발생했습니다.");
            }
        }
        
        /// <summary>
        /// 회원가입 성공 처리 - "REGISTER_SUCCESS:username"
        /// </summary>
        private void HandleRegisterSuccess(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string username = parts[1];
                Debug.Log($"[MessageHandler] 회원가입 성공: {username}");
                OnAuthResponse?.Invoke(true, "회원가입이 완료되었습니다. 로그인해주세요.");
            }
            else
            {
                Debug.Log("[MessageHandler] 회원가입 성공");
                OnAuthResponse?.Invoke(true, "회원가입이 완료되었습니다.");
            }
        }
        
        /// <summary>
        /// 로그아웃 성공 처리 - "LOGOUT_SUCCESS"
        /// </summary>
        private void HandleLogoutSuccess(string[] parts)
        {
            Debug.Log("[MessageHandler] 로그아웃 성공");
            OnAuthResponse?.Invoke(true, "로그아웃되었습니다.");
        }
        
        // ========================================
        // 로비 메시지 핸들러들 (서버 프로토콜 기준)
        // ========================================
        
        /// <summary>
        /// 로비 입장 성공 - "LOBBY_ENTER_SUCCESS"
        /// </summary>
        private void HandleLobbyEnterSuccess(string[] parts)
        {
            Debug.Log("[MessageHandler] 로비 입장 성공");
            // 로비 UI로 전환하는 이벤트 발생 가능
        }
        
        /// <summary>
        /// 로비 나가기 성공 - "LOBBY_LEAVE_SUCCESS"
        /// </summary>
        private void HandleLobbyLeaveSuccess(string[] parts)
        {
            Debug.Log("[MessageHandler] 로비 나가기 성공");
        }
        
        /// <summary>
        /// 로비 사용자 목록 - "LOBBY_USER_LIST:count:user1,displayName1,level1,status1:user2,displayName2,level2,status2..."
        /// </summary>
        private void HandleLobbyUserList(string[] parts)
        {
            if (parts.Length < 2)
            {
                Debug.LogError("[MessageHandler] LOBBY_USER_LIST 메시지 형식 오류");
                return;
            }
            
            try
            {
                int userCount = int.Parse(parts[1]);
                List<UserInfo> users = new List<UserInfo>();
                
                Debug.Log($"[MessageHandler] 로비 사용자 목록 수신: 총 {userCount}명, 파트 개수: {parts.Length}");
                
                for (int i = 2; i < parts.Length; ++i)
                {
                    if (!string.IsNullOrEmpty(parts[i]))
                    {
                        string[] userInfo = parts[i].Split(',');
                        if (userInfo.Length >= 4)
                        {
                            UserInfo user = new UserInfo
                            {
                                username = userInfo[0],
                                displayName = userInfo[1],
                                level = int.Parse(userInfo[2]),
                                status = userInfo[3],
                                isOnline = true,
                                totalGames = 0, // 기본값
                                wins = 0,
                                losses = 0,
                                totalScore = 0,
                                bestScore = 0
                            };
                            
                            users.Add(user);
                            Debug.Log($"[MessageHandler] 사용자 추가: {user.displayName} [{user.username}] (레벨: {user.level}, 상태: {user.status})");
                        }
                        else if (userInfo.Length >= 3)
                        {
                            // 구버전 호환성
                            UserInfo user = new UserInfo
                            {
                                username = userInfo[0],
                                displayName = userInfo[0], // displayName 없음
                                level = int.Parse(userInfo[1]),
                                status = userInfo[2],
                                isOnline = true,
                                totalGames = 0,
                                wins = 0,
                                losses = 0,
                                totalScore = 0,
                                bestScore = 0
                            };
                            
                            users.Add(user);
                        }
                    }
                }
                
                Debug.Log($"[MessageHandler] 최종 사용자 목록: {users.Count}명");
                OnUserListUpdated?.Invoke(users);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MessageHandler] LOBBY_USER_LIST 파싱 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 로비 사용자 입장 - "LOBBY_USER_JOINED:username"
        /// </summary>
        private void HandleLobbyUserJoined(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string username = parts[1];
                Debug.Log($"[MessageHandler] 로비에 사용자 입장: {username}");
                // 필요시 UI 업데이트
            }
        }
        
        /// <summary>
        /// 로비 사용자 퇴장 - "LOBBY_USER_LEFT:username"
        /// </summary>
        private void HandleLobbyUserLeft(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string username = parts[1];
                Debug.Log($"[MessageHandler] 로비에서 사용자 퇴장: {username}");
                // 필요시 UI 업데이트
            }
        }
        
        // ========================================
        // 방 관련 메시지 핸들러들 (서버 프로토콜 기준)
        // ========================================
        
        /// <summary>
        /// 방 목록 - "ROOM_LIST:roomCount:room1_data:room2_data:..."
        /// </summary>
        private void HandleRoomList(string[] parts)
        {
            if (parts.Length < 2)
            {
                Debug.LogError("[MessageHandler] ROOM_LIST 메시지 형식 오류");
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
                        // 방 데이터 파싱 (예: "roomId,roomName,host,currentPlayers,maxPlayers,isPrivate,isGameStarted")
                        string[] roomData = parts[i + 2].Split(',');
                        if (roomData.Length >= 4)
                        {
                            RoomInfo room = new RoomInfo
                            {
                                roomId = int.Parse(roomData[0]),
                                roomName = roomData[1],
                                hostName = roomData.Length > 2 ? roomData[2] : "호스트",
                                currentPlayers = int.Parse(roomData.Length > 3 ? roomData[3] : roomData[2]),
                                maxPlayers = int.Parse(roomData.Length > 4 ? roomData[4] : roomData[3]),
                                isPrivate = roomData.Length > 5 && roomData[5] == "1",
                                isGameStarted = roomData.Length > 6 && roomData[6] == "1"
                            };
                            rooms.Add(room);
                        }
                    }
                }
                
                Debug.Log($"[MessageHandler] 방 목록 업데이트: {rooms.Count}개 방");
                OnRoomListUpdated?.Invoke(rooms);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MessageHandler] ROOM_LIST 파싱 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 방 생성됨 - "ROOM_CREATED:roomId:roomName"
        /// </summary>
        private void HandleRoomCreated(string[] parts)
        {
            if (parts.Length < 3)
            {
                Debug.LogError("[MessageHandler] ROOM_CREATED 메시지 형식 오류");
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
                
                Debug.Log($"[MessageHandler] 방 생성됨: {room.roomName} (ID: {room.roomId})");
                OnRoomCreated?.Invoke(room);
                hasJoinedRoom = true; // 방 생성 시 자동 입장 상태 설정
                OnRoomJoined?.Invoke(); // 방 생성자는 자동으로 방에 입장함
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MessageHandler] ROOM_CREATED 파싱 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 방 참가 성공 - "ROOM_JOIN_SUCCESS:roomId:roomName"
        /// </summary>
        private void HandleRoomJoinSuccess(string[] parts)
        {
            if (parts.Length < 3)
            {
                Debug.LogError("[MessageHandler] ROOM_JOIN_SUCCESS 메시지 형식 오류");
                return;
            }
            
            try
            {
                int roomId = int.Parse(parts[1]);
                string roomName = parts[2];
                
                Debug.Log($"[MessageHandler] 방 참가 성공: {roomName} (ID: {roomId})");
                OnJoinRoomResponse?.Invoke(true, $"방 '{roomName}'에 참가했습니다.");
                hasJoinedRoom = true; // 방 입장 상태 설정
                OnRoomJoined?.Invoke(); // GameRoomPanel로 전환 트리거
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MessageHandler] ROOM_JOIN_SUCCESS 파싱 오류: {ex.Message}");
                OnJoinRoomResponse?.Invoke(false, "방 참가 정보 처리 중 오류가 발생했습니다.");
            }
        }
        
        /// <summary>
        /// 방 나가기 - "ROOM_LEFT" 또는 "LEAVE_ROOM_CONFIRMED"
        /// </summary>
        private void HandleRoomLeft(string[] parts)
        {
            Debug.Log("[MessageHandler] 방 나가기 성공");

            // 방 입장 상태 초기화
            hasJoinedRoom = false;

            // 로비로 돌아가는 이벤트 발생
            OnRoomLeft?.Invoke();
            Debug.Log("[MessageHandler] OnRoomLeft 이벤트 발생");
        }
        
        /// <summary>
        /// 방 정보 - "ROOM_INFO:방ID:방이름:호스트:현재인원:최대인원:비공개:게임중:게임모드:플레이어데이터..."
        /// </summary>
        private void HandleRoomInfo(string[] parts)
        {
            if (parts.Length < 9)
            {
                Debug.LogError("[MessageHandler] ROOM_INFO 메시지 형식 오류");
                return;
            }
            
            try
            {
                Debug.Log($"[MessageHandler] 방 정보 수신: {string.Join(":", parts)}");
                Debug.Log($"[MessageHandler] 총 parts 개수: {parts.Length}");
                
                // 방 기본 정보 파싱
                int roomId = int.Parse(parts[1]);
                string roomName = parts[2];
                string hostName = parts[3];
                int currentPlayers = int.Parse(parts[4]);
                int maxPlayers = int.Parse(parts[5]);
                bool isPrivate = parts[6] == "1";
                bool isGameStarted = parts[7] == "1";
                string gameMode = parts[8];

                Debug.Log($"[MessageHandler] 로비 사용자 목록 수신: 총 {currentPlayers}명, 파트 개수: {parts.Length}");

                // 방 정보 업데이트
                var roomInfo = new RoomInfo
                {
                    roomId = roomId,
                    roomName = roomName,
                    hostName = hostName,
                    currentPlayers = currentPlayers,
                    maxPlayers = maxPlayers,
                    isPrivate = isPrivate,
                    isGameStarted = isGameStarted,
                    gameMode = gameMode
                };

                // 방 정보 업데이트 이벤트 발생 (나중에 플레이어 데이터와 함께 전달)

                // 플레이어 데이터 파싱 (9번째 인덱스부터)
                var playerDataList = new List<PlayerData>();
                for (int i = 9; i < parts.Length; i++)
                {
                    string playerData = parts[i];
                    Debug.Log($"[MessageHandler] parts[{i}] 플레이어 데이터: '{playerData}'");
                    var playerParts = playerData.Split(',');
                    Debug.Log($"[MessageHandler] 플레이어 파트 개수: {playerParts.Length}");
                    
                    if (playerParts.Length >= 6)
                    {
                        var player = new PlayerData
                        {
                            playerId = int.Parse(playerParts[0]),
                            username = playerParts[1],
                            displayName = playerParts[2],
                            isHost = playerParts[3] == "1",
                            isReady = playerParts[4] == "1",
                            colorSlot = int.Parse(playerParts[5])
                        };
                        playerDataList.Add(player);
                        Debug.Log($"[MessageHandler] 사용자 추가: {player.displayName} [{player.username}] (레벨: {player.playerId}, 상태: 호스트={player.isHost}, 레디={player.isReady}, 색상슬롯={player.colorSlot})");
                    }
                    else
                    {
                        Debug.LogWarning($"[MessageHandler] 플레이어 데이터 파트 부족: {playerParts.Length}개 (최소 6개 필요)");
                    }
                }

                Debug.Log($"[MessageHandler] 최종 사용자 목록: {playerDataList.Count}명");
                
                // 현재 사용자가 방에 있는지 확인 (방 입장 시 GameRoomPanel 활성화용)
                bool currentUserInRoom = false;
                var networkManager = FindObjectOfType<Features.Multi.Net.NetworkManager>();
                string currentUsername = networkManager?.CurrentUserInfo?.username;
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    foreach (var player in playerDataList)
                    {
                        if (player.username == currentUsername)
                        {
                            currentUserInRoom = true;
                            break;
                        }
                    }
                }
                
                // 방 정보 및 플레이어 데이터 업데이트 이벤트 발생
                OnRoomInfoUpdated?.Invoke(roomInfo, playerDataList);
                
                // 현재 사용자가 방에 있고 아직 입장 상태가 설정되지 않은 경우에만 GameRoomPanel 활성화
                if (currentUserInRoom && !hasJoinedRoom)
                {
                    Debug.Log($"[MessageHandler] ROOM_INFO에서 현재 사용자 확인됨 - GameRoomPanel 활성화 트리거");
                    hasJoinedRoom = true; // 방 입장 상태 설정
                    OnRoomJoined?.Invoke();
                }
                else if (currentUserInRoom && hasJoinedRoom)
                {
                    Debug.Log($"[MessageHandler] ROOM_INFO에서 현재 사용자 확인됨 - 이미 입장한 상태이므로 OnRoomJoined 중복 호출 방지");
                }

                Debug.Log($"[MessageHandler] 방 정보 파싱 완료: {roomName}, 플레이어 {playerDataList.Count}명");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MessageHandler] ROOM_INFO 파싱 오류: {ex.Message}");
            }
        }
        
        // ========================================
        // 플레이어 관련 메시지 핸들러들
        // ========================================
        
        /// <summary>
        /// 플레이어 입장 - "PLAYER_JOINED:username" 또는 "PLAYER_JOINED:username:displayName"
        /// </summary>
        private void HandlePlayerJoined(string[] parts)
        {
            Debug.Log($"[MessageHandler] HandlePlayerJoined 호출됨 - parts 개수: {parts.Length}");
            for (int i = 0; i < parts.Length; i++)
            {
                Debug.Log($"[MessageHandler] parts[{i}]: '{parts[i]}'");
            }
            
            if (parts.Length >= 2)
            {
                string username = parts[1];
                string displayName = parts.Length >= 3 ? parts[2] : username;
                Debug.Log($"[MessageHandler] 플레이어 입장 처리: {displayName} [{username}]");
                
                // 이벤트 발생
                OnPlayerJoined?.Invoke(username);
                Debug.Log($"[MessageHandler] OnPlayerJoined 이벤트 발생: {username}");
            }
            else
            {
                Debug.LogWarning($"[MessageHandler] PLAYER_JOINED 메시지 형식 오류 - parts 개수: {parts.Length}");
            }
        }
        
        /// <summary>
        /// 플레이어 퇴장 - "PLAYER_LEFT:username" 또는 "PLAYER_LEFT:username:displayName"
        /// </summary>
        private void HandlePlayerLeft(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string username = parts[1];
                string displayName = parts.Length >= 3 ? parts[2] : username;
                Debug.Log($"[MessageHandler] 플레이어 퇴장: {displayName} [{username}]");
                
                // 이벤트 발생
                OnPlayerLeft?.Invoke(username);
            }
        }
        
        /// <summary>
        /// 플레이어 준비 상태 - "PLAYER_READY:username:ready" 또는 "PLAYER_READY:ready"
        /// </summary>
        private void HandlePlayerReady(string[] parts)
        {
            if (parts.Length >= 2)
            {
                if (parts.Length >= 3)
                {
                    // "PLAYER_READY:username:ready" 형태
                    string username = parts[1];
                    bool ready = parts[2] == "1" || parts[2].ToLower() == "true";
                    Debug.Log($"[MessageHandler] 플레이어 준비 상태: {username} - {(ready ? "준비완료" : "대기중")}");
                    
                    // 이벤트 발생
                    OnPlayerReadyChanged?.Invoke(username, ready);
                }
                else
                {
                    // "PLAYER_READY:ready" 형태 (본인 상태)
                    bool ready = parts[1] == "1" || parts[1].ToLower() == "true";
                    Debug.Log($"[MessageHandler] 내 준비 상태 확인: {(ready ? "준비완료" : "대기중")}");
                }
            }
        }
        
        /// <summary>
        /// 방장 변경 - "HOST_CHANGED:newHost" 또는 "HOST_CHANGED:newHost:displayName"
        /// </summary>
        private void HandleHostChanged(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string newHost = parts[1];
                string displayName = parts.Length >= 3 ? parts[2] : newHost;
                Debug.Log($"[MessageHandler] 방장 변경: {displayName} [{newHost}]");
                // 필요시 UI에 방장 표시 업데이트
            }
        }
        
        // ========================================
        // 게임 관련 메시지 핸들러들 (서버 프로토콜 기준)
        // ========================================
        
        /// <summary>
        /// 게임 시작 - "GAME_STARTED"
        /// </summary>
        private void HandleGameStarted(string[] parts)
        {
            Debug.Log("[MessageHandler] 게임 시작됨");
            
            // GameRoomPanel에서 게임 시작 상태를 인식할 수 있도록 OnGameStarted 이벤트 발생
            OnGameStarted?.Invoke();
        }
        
        /// <summary>
        /// 게임 플레이어 정보 - "GAME_PLAYER_INFO:username1,colorSlot1:username2,colorSlot2..."
        /// </summary>
        private void HandleGamePlayerInfo(string[] parts)
        {
            try
            {
                Debug.Log($"[MessageHandler] 게임 플레이어 정보 수신: {string.Join(":", parts)}");
                
                if (parts.Length < 2)
                {
                    Debug.LogWarning("[MessageHandler] GAME_PLAYER_INFO 메시지 형식 오류: 플레이어 정보가 없음");
                    return;
                }
                
                // parts[1] 이후가 "username1,colorSlot1:username2,colorSlot2" 형태
                for (int i = 1; i < parts.Length; i++)
                {
                    string playerInfo = parts[i];
                    if (string.IsNullOrEmpty(playerInfo)) continue;
                    
                    // "username,colorSlot" 형태로 파싱
                    string[] playerData = playerInfo.Split(',');
                    if (playerData.Length == 2)
                    {
                        string username = playerData[0];
                        if (int.TryParse(playerData[1], out int colorSlot))
                        {
                            Debug.Log($"[MessageHandler] 플레이어 정보 파싱: {username} → 색상 슬롯 {colorSlot}");
                            
                            // 현재 사용자와 비교해서 내 색상 슬롯 확인
                            var networkManager = FindObjectOfType<Features.Multi.Net.NetworkManager>();
                            var currentUser = networkManager?.CurrentUserInfo;
                            if (currentUser != null && currentUser.username == username)
                            {
                                Debug.Log($"[MessageHandler] 내 색상 슬롯 확인: {colorSlot}");
                                // 추후 필요시 색상 정보 업데이트 로직 추가 가능
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[MessageHandler] 색상 슬롯 파싱 실패: {playerData[1]}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[MessageHandler] 플레이어 정보 형식 오류: {playerInfo}");
                    }
                }
                
                Debug.Log("[MessageHandler] GAME_PLAYER_INFO 처리 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MessageHandler] GAME_PLAYER_INFO 처리 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 게임 상태 업데이트 - "GAME_STATE_UPDATE:jsonData"
        /// </summary>
        private void HandleGameStateUpdate(string[] parts)
        {
            try
            {
                if (parts.Length >= 2)
                {
                    string jsonData = string.Join(":", parts, 1, parts.Length - 1);
                    Debug.Log($"[MessageHandler] 게임 상태 업데이트: {jsonData}");
                    
                    // JSON 파싱 - Unity JsonUtility로 기본 필드 파싱
                    GameStateData gameState = JsonUtility.FromJson<GameStateData>(jsonData);
                    
                    // Newtonsoft.Json으로 object 필드들을 수동으로 파싱
                    try
                    {
                        var fullData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonData);
                        
                        // scores 필드 파싱
                        if (fullData.ContainsKey("scores") && fullData["scores"] != null)
                        {
                            gameState.scores = fullData["scores"];
                            Debug.Log($"[MessageHandler] scores 파싱 성공: {Newtonsoft.Json.JsonConvert.SerializeObject(gameState.scores)}");
                        }
                        else
                        {
                            Debug.Log("[MessageHandler] scores 필드 없음 또는 null");
                        }
                        
                        // remainingBlocks 필드 파싱
                        if (fullData.ContainsKey("remainingBlocks") && fullData["remainingBlocks"] != null)
                        {
                            gameState.remainingBlocks = fullData["remainingBlocks"];
                            Debug.Log($"[MessageHandler] remainingBlocks 파싱 성공: {Newtonsoft.Json.JsonConvert.SerializeObject(gameState.remainingBlocks)}");
                        }
                        else
                        {
                            Debug.Log("[MessageHandler] remainingBlocks 필드 없음 또는 null");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[MessageHandler] object 필드 파싱 실패: {ex.Message}");
                    }
                    
                    // struct는 null이 될 수 없으므로, 파싱 성공 여부를 다른 방식으로 확인
                    if (!string.IsNullOrEmpty(jsonData))
                    {
                        Debug.Log($"[MessageHandler] 게임 상태 파싱 완료: currentPlayer={gameState.currentPlayer}, turnNumber={gameState.turnNumber}, boardState 크기={gameState.boardState?.Length ?? 0}");
                        
                        // boardState 배열 유효성 확인
                        if (gameState.boardState != null && gameState.boardState.Length > 0)
                        {
                            Debug.Log($"[MessageHandler] boardState 1차원 배열 수신: {gameState.boardState.Length}개 요소");
                        }
                        
                        // GameRoomPanel에 게임 상태 업데이트 알림
                        OnGameStateUpdate?.Invoke(gameState);
                    }
                    else
                    {
                        Debug.LogWarning("[MessageHandler] GAME_STATE_UPDATE JSON 파싱 실패");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MessageHandler] GAME_STATE_UPDATE 처리 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 블록 배치됨 - "BLOCK_PLACED:jsonData"
        /// </summary>
        private void HandleBlockPlaced(string[] parts)
        {
            try
            {
                if (parts.Length >= 2)
                {
                    string jsonData = string.Join(":", parts, 1, parts.Length - 1);
                    Debug.Log($"[MessageHandler] 블록 배치됨: {jsonData}");
                    
                    // 서버 JSON을 파싱
                    BlockPlacedData placementData = JsonUtility.FromJson<BlockPlacedData>(jsonData);
                    
                    if (placementData != null)
                    {
                        Debug.Log($"[MessageHandler] 블록 배치 파싱 완료: Player={placementData.player}, " +
                                  $"BlockType={placementData.blockType}, Position=({placementData.position.row},{placementData.position.col}), " +
                                  $"PlayerColor={placementData.playerColor}");
                        
                        // placedCells가 있는지 확인 (개선된 동기화)
                        if (placementData.placedCells != null && placementData.placedCells.Length > 0)
                        {
                            Debug.Log($"[MessageHandler] 📦 서버에서 배치된 셀 좌표 수신: {placementData.placedCells.Length}개");
                            
                            // 서버에서 전송한 실제 배치 좌표를 사용 (정확한 동기화)
                            var occupiedCells = new List<Vector2Int>();
                            foreach (var cell in placementData.placedCells)
                            {
                                occupiedCells.Add(new Vector2Int(cell.col, cell.row)); // col=x축, row=y축
                            }
                            
                            // 서버 데이터로 직접 생성 (계산 없이)
                            var multiPlacement = new MultiModels.BlockPlacement(
                                placementData.playerColor - 1, // 서버는 1-4, 클라이언트는 0-3
                                (MultiModels.BlockType)placementData.blockType,
                                new Vector2Int(placementData.position.col, placementData.position.row), // col=x축, row=y축
                                placementData.rotation,
                                placementData.flip != 0,
                                occupiedCells // 서버에서 계산된 정확한 좌표 사용
                            );
                            
                            Debug.Log($"[MessageHandler] ✅ 서버 좌표 직접 사용: {multiPlacement.blockType} at ({multiPlacement.position.x},{multiPlacement.position.y}), 점유셀={multiPlacement.occupiedCells.Count}개");
                            OnBlockPlaced?.Invoke(multiPlacement);
                        }
                        else
                        {
                            // 하위 호환성: placedCells가 없으면 기존 방식 사용
                            Debug.Log($"[MessageHandler] ⚠️ placedCells 없음 - 기존 계산 방식 사용");
                            
                            // MultiModels.BlockPlacement 형태로 변환하여 이벤트 발생
                            // 서버에서 row=y좌표, col=x좌표로 응답하므로 Unity Vector2Int(x,y)로 변환
                            var multiPlacement = new MultiModels.BlockPlacement(
                                placementData.playerColor - 1, // 서버는 1-4, 클라이언트는 0-3
                                (MultiModels.BlockType)placementData.blockType,
                                new Vector2Int(placementData.position.col, placementData.position.row), // col=x축, row=y축
                                placementData.rotation,
                                placementData.flip != 0
                            );
                            
                            // 점유된 셀 자동 계산 - 블록 타입, 위치, 회전, 뒤집기 정보로 계산됨
                            Debug.Log($"[MessageHandler] 블록 배치 점유셀 계산: {multiPlacement.blockType} at ({multiPlacement.position.x},{multiPlacement.position.y}), 점유셀={multiPlacement.occupiedCells.Count}개");
                            OnBlockPlaced?.Invoke(multiPlacement);
                        }
                        
                        Debug.Log("[MessageHandler] OnBlockPlaced 이벤트 발생 완료");
                    }
                    else
                    {
                        Debug.LogError("[MessageHandler] BLOCK_PLACED JSON 파싱 실패");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MessageHandler] BLOCK_PLACED 처리 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 턴 변경 - "TURN_CHANGED:jsonData"
        /// 서버 형식: TURN_CHANGED:{"newPlayer":"username","playerColor":int,"turnNumber":int,"turnTimeSeconds":int,"remainingTimeSeconds":int,"previousTurnTimedOut":boolean}
        /// </summary>
        private void HandleTurnChanged(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string jsonData = string.Join(":", parts, 1, parts.Length - 1);
                Debug.Log($"[MessageHandler] 턴 변경 JSON 수신: {jsonData}");
                
                try
                {
                    // Unity JsonUtility를 사용한 JSON 파싱
                    TurnChangeInfo turnInfo = JsonUtility.FromJson<TurnChangeInfo>(jsonData);
                    
                    Debug.Log($"[MessageHandler] 턴 변경 파싱 완료: 플레이어={turnInfo.newPlayer}, " +
                             $"색상={turnInfo.playerColor}, 턴={turnInfo.turnNumber}, " +
                             $"제한시간={turnInfo.turnTimeSeconds}초, 남은시간={turnInfo.remainingTimeSeconds}초, " +
                             $"이전턴타임아웃={turnInfo.previousTurnTimedOut}");
                    
                    // 상세 정보를 포함한 이벤트 발생
                    OnTurnChanged?.Invoke(turnInfo);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MessageHandler] TURN_CHANGED JSON 파싱 실패: {ex.Message}");
                    Debug.LogError($"[MessageHandler] 문제가 된 JSON: {jsonData}");
                    
                    // 파싱 실패 시 기본값으로 폴백하지 않고 에러 로그만 출력
                    // UI에서는 이전 상태를 유지하도록 함
                }
            }
            else
            {
                Debug.LogWarning("[MessageHandler] TURN_CHANGED 메시지에 JSON 데이터가 없습니다.");
            }
        }
        
        /// <summary>
        /// 턴 타임아웃 - "TURN_TIMEOUT:jsonData"
        /// </summary>
        private void HandleTurnTimeout(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string jsonData = string.Join(":", parts, 1, parts.Length - 1);
                Debug.Log($"[MessageHandler] 턴 타임아웃: {jsonData}");
                // JSON 파싱하여 타임아웃 플레이어 정보 처리
            }
        }
        
        /// <summary>
        /// 게임 종료 - "GAME_ENDED"
        /// </summary>
        private void HandleGameEnded(string[] parts)
        {
            try
            {
                Debug.Log("[MessageHandler] 게임 종료 메시지 수신");
                
                // 게임 종료 이벤트 발생
                OnGameEnded?.Invoke(MultiModels.PlayerColor.None); // 승자 정보는 GAME_RESULT에서 처리
                Debug.Log("[MessageHandler] OnGameEnded 이벤트 발생 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MessageHandler] GAME_ENDED 처리 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 게임 이동 성공 - "GAME_MOVE_SUCCESS" 또는 "GAME_MOVE_SUCCESS:jsonData"
        /// </summary>
        private void HandleGameMoveSuccess(string[] parts)
        {
            try
            {
                if (parts.Length >= 2)
                {
                    // JSON 데이터가 있는 경우
                    string jsonData = string.Join(":", parts, 1, parts.Length - 1);
                    Debug.Log($"[MessageHandler] 게임 이동 성공 (데이터 포함): {jsonData}");
                    
                    // TODO: 필요시 JSON 파싱하여 상세 정보 처리
                    // 현재는 확인용 로그만 출력
                }
                else
                {
                    // 단순 성공 메시지
                    Debug.Log("[MessageHandler] 게임 이동 성공");
                }
                
                // 성공 확인 로그 (UI 피드백이 필요하다면 이벤트 추가 가능)
                Debug.Log("[MessageHandler] 블록 배치 서버 확인 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MessageHandler] GAME_MOVE_SUCCESS 처리 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 게임 결과 - "GAME_RESULT:jsonData"
        /// </summary>
        private void HandleGameResult(string[] parts)
        {
            try
            {
                if (parts.Length >= 2)
                {
                    string resultJson = string.Join(":", parts, 1, parts.Length - 1);
                    Debug.Log($"[MessageHandler] 게임 결과 수신: {resultJson}");

                    // JSON 파싱하여 GameResultData 생성
                    try
                    {
                        GameResultData gameResult = JsonUtility.FromJson<GameResultData>(resultJson);

                        // 파싱이 성공하면 scores Dictionary 수동 설정
                        if (gameResult != null)
                        {
                            // Unity JsonUtility는 Dictionary를 지원하지 않으므로 수동 파싱
                            gameResult.scores = ParseScoresDictionary(resultJson);

                            Debug.Log($"[MessageHandler] 새로운 GAME_RESULT 데이터 파싱 성공: 순위={gameResult.myRank}, 점수={gameResult.myScore}, 경험치={gameResult.expGained}");
                            Debug.Log($"[MessageHandler] scores Dictionary 파싱 완료: {gameResult.scores?.Count ?? 0}개 플레이어");

                            OnGameResultReceived?.Invoke(gameResult);
                        }
                        else
                        {
                            Debug.LogWarning("[MessageHandler] GAME_RESULT JSON 파싱 결과가 null입니다");
                        }
                    }
                    catch (System.Exception parseEx)
                    {
                        // 파싱에 실패하면 명확한 에러 메시지 표시 (fallback 제거)
                        Debug.LogError($"[MessageHandler] GAME_RESULT JSON 파싱 실패: {parseEx.Message}");
                        Debug.LogError($"[MessageHandler] 실패한 JSON 데이터: {resultJson}");

                        // 에러 메시지를 표시할 GameResultData 생성
                        GameResultData errorResult = new GameResultData();
                        errorResult.scores = new System.Collections.Generic.Dictionary<string, int>();
                        errorResult.gameType = "파싱 실패";
                        errorResult.myRank = 0;
                        errorResult.myScore = 0;
                        errorResult.expGained = 0;
                        errorResult.levelUp = false;
                        errorResult.newLevel = 1;

                        Debug.LogWarning("[MessageHandler] 게임 결과 파싱에 실패했습니다. 빈 결과로 모달을 표시합니다.");
                        OnGameResultReceived?.Invoke(errorResult);
                    }

                    // GAME_RESULT 처리 완료 - OnGameEnded는 이미 GAME_ENDED에서 호출되었으므로 중복 호출 제거
                    Debug.Log("[MessageHandler] GAME_RESULT 처리 완료 (OnGameEnded 중복 호출 방지)");
                }
                else
                {
                    Debug.LogWarning("[MessageHandler] GAME_RESULT 메시지에 결과 데이터가 없습니다.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MessageHandler] GAME_RESULT 처리 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 게임 리셋 - "GAME_RESET"
        /// </summary>
        private void HandleGameReset(string[] parts)
        {
            Debug.Log("[MessageHandler] 게임 리셋");
            // 게임 상태 초기화
        }
        
        // ========================================
        // 채팅 및 시스템 메시지 핸들러들
        // ========================================
        
        /// <summary>
        /// 채팅 메시지 - "CHAT:username:displayName:message" 또는 "CHAT:username:message"
        /// </summary>
        private void HandleChat(string[] parts)
        {
            if (parts.Length >= 3)
            {
                string username = parts[1];
                string displayName = "";
                string message;
                
                if (parts.Length >= 4)
                {
                    // 새로운 형식: CHAT:username:displayName:message
                    displayName = parts[2];
                    message = string.Join(":", parts, 3, parts.Length - 3);
                    Debug.Log($"[MessageHandler] 채팅 메시지 (새 형식): {displayName} [{username}]: {message}");
                }
                else
                {
                    // 기존 형식: CHAT:username:message
                    displayName = username; // displayName이 없으면 username 사용
                    message = string.Join(":", parts, 2, parts.Length - 2);
                    Debug.Log($"[MessageHandler] 채팅 메시지: {username}: {message}");
                }

                // 채팅 메시지 이벤트 발생
                OnChatMessageReceived?.Invoke(username, displayName, message);
            }
        }
        
        /// <summary>
        /// 시스템 메시지 - "SYSTEM:message"
        /// </summary>
        private void HandleSystemMessage(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string systemMessage = string.Join(":", parts, 1, parts.Length - 1);
                Debug.Log($"[MessageHandler] 시스템 메시지: {systemMessage}");
                
                // 플레이어 입장 메시지 감지하여 이벤트 발생
                if (systemMessage.Contains("입장하셨습니다"))
                {
                    Debug.Log($"[MessageHandler] 플레이어 입장 시스템 메시지 감지 - ROOM_INFO 트리거 필요");
                    OnPlayerSystemJoined?.Invoke();
                }
            }
        }
        
        /// <summary>
        /// 사용자 통계 응답 - "USER_STATS_RESPONSE:jsonData"
        /// </summary>
        private void HandleUserStatsResponse(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string statsJson = string.Join(":", parts, 1, parts.Length - 1);
                Debug.Log($"[MessageHandler] 사용자 통계 응답: {statsJson}");
                // TODO: JSON 파싱 후 OnUserStatsReceived 이벤트 발생
            }
        }
        
        /// <summary>
        /// 내 통계 업데이트 - "MY_STATS_UPDATE:jsonData"
        /// </summary>
        private void HandleMyStatsUpdate(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string statsJson = string.Join(":", parts, 1, parts.Length - 1);
                Debug.Log($"[MessageHandler] 내 통계 업데이트: {statsJson}");

                try
                {
                    // JSON을 전용 구조체로 파싱
                    StatsUpdateData statsData = JsonUtility.FromJson<StatsUpdateData>(statsJson);
                    if (statsData != null)
                    {
                        // UserInfo 객체 생성 및 이벤트 발생
                        UserInfo userInfo = new UserInfo
                        {
                            username = statsData.username ?? "",
                            displayName = statsData.displayName ?? "",
                            level = statsData.level,
                            totalGames = statsData.totalGames,
                            wins = statsData.wins,
                            losses = statsData.losses,
                            averageScore = (int)statsData.averageScore, // float를 int로 변환
                            totalScore = statsData.totalScore,
                            bestScore = statsData.bestScore,
                            isOnline = true,
                            status = statsData.status ?? "로비"
                        };

                        Debug.Log($"[MessageHandler] 사용자 정보 파싱 완료: {userInfo.displayName} [{userInfo.username}] - 레벨 {userInfo.level}, 총게임 {userInfo.totalGames}, 승률 {statsData.winRate}%");
                        OnMyStatsUpdated?.Invoke(userInfo);
                    }
                    else
                    {
                        Debug.LogError("[MessageHandler] JSON 파싱 결과가 null입니다");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[MessageHandler] 내 통계 업데이트 파싱 오류: {e.Message}");
                    Debug.LogError($"[MessageHandler] JSON 내용: {statsJson}");
                }
            }
        }
        
        /// <summary>
        /// AFK 검증 요청 - "AFK_VERIFY"
        /// </summary>
        private void HandleAfkVerify(string[] parts)
        {
            Debug.Log("[MessageHandler] AFK 검증 요청 수신");
            OnAfkVerifyReceived?.Invoke();
        }
        
        /// <summary>
        /// AFK 모드 활성화 - "AFK_MODE_ACTIVATED:jsonData"
        /// </summary>
        private void HandleAfkModeActivated(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string jsonData = string.Join(":", parts, 1, parts.Length - 1);
                Debug.Log($"[MessageHandler] AFK 모드 활성화: {jsonData}");

                // AFK 모달 표시를 위해 AFK_VERIFY 이벤트와 동일하게 처리
                Debug.Log("[MessageHandler] AFK_MODE_ACTIVATED로 인한 AFK 모달 표시 트리거");
                OnAfkVerifyReceived?.Invoke();
            }
            else
            {
                Debug.Log("[MessageHandler] AFK 모드 활성화");
                // 데이터가 없어도 AFK 모달은 표시해야 함
                OnAfkVerifyReceived?.Invoke();
            }
        }
        
        /// <summary>
        /// AFK 해제 성공 - "AFK_UNBLOCK_SUCCESS"
        /// </summary>
        private void HandleAfkUnblockSuccess(string[] parts)
        {
            Debug.Log("[MessageHandler] AFK 해제 성공");
            OnAfkUnblockSuccess?.Invoke();
        }
        
        /// <summary>
        /// AFK 상태 리셋 - "AFK_STATUS_RESET:username"
        /// </summary>
        private void HandleAfkStatusReset(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string username = parts[1];
                Debug.Log($"[MessageHandler] AFK 상태 리셋: {username}");
                OnAfkStatusReset?.Invoke(username);
            }
        }
        
        /// <summary>
        /// 버전 체크 응답 - "version:ok" 또는 "version:mismatch:downloadUrl"
        /// </summary>
        private void HandleVersionCheck(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string status = parts[1];
                if (status == "ok")
                {
                    Debug.Log("[MessageHandler] 버전 호환성 확인 완료");
                }
                else if (status == "mismatch" && parts.Length >= 3)
                {
                    string downloadUrl = parts[2];
                    Debug.LogWarning($"[MessageHandler] 버전 불일치 - 다운로드 URL: {downloadUrl}");
                    OnErrorReceived?.Invoke($"클라이언트 업데이트가 필요합니다. 다운로드 URL: {downloadUrl}");
                }
            }
        }
        
        /// <summary>
        /// 에러 메시지 - "ERROR:에러내용"
        /// </summary>
        private void HandleError(string[] parts)
        {
            if (parts.Length < 2)
            {
                Debug.LogError("[MessageHandler] ERROR 메시지 형식 오류");
                return;
            }
            
            string errorMessage = string.Join(":", parts, 1, parts.Length - 1);
            Debug.LogError($"[MessageHandler] 서버 에러: {errorMessage}");
            
            OnErrorReceived?.Invoke(errorMessage);
        }
        
        /// <summary>
        /// Pong 응답 - "pong"
        /// </summary>
        private void HandlePong(string[] parts)
        {
            Debug.Log("[MessageHandler] Pong 응답 수신");
            OnHeartbeatReceived?.Invoke();
        }

        /// <summary>
        /// 채팅 성공 응답 - "CHAT_SUCCESS"
        /// </summary>
        private void HandleChatSuccess(string[] parts)
        {
            Debug.Log("[MessageHandler] 채팅 메시지 전송 성공");
            // 채팅 성공은 단순히 로그만 출력하고 특별한 처리는 하지 않음
        }
        
        /// <summary>
        /// JSON 문자열에서 scores Dictionary를 수동 파싱 (Unity JsonUtility Dictionary 미지원 해결)
        /// </summary>
        /// <param name="jsonString">GAME_RESULT JSON 문자열</param>
        /// <returns>파싱된 scores Dictionary</returns>
        private System.Collections.Generic.Dictionary<string, int> ParseScoresDictionary(string jsonString)
        {
            var scores = new System.Collections.Generic.Dictionary<string, int>();

            try
            {
                // "scores":{...} 부분 찾기
                int scoresIndex = jsonString.IndexOf("\"scores\":");
                if (scoresIndex == -1)
                {
                    Debug.LogWarning("[MessageHandler] JSON에서 'scores' 필드를 찾을 수 없습니다");
                    return scores;
                }

                // scores 객체 시작점 찾기
                int openBraceIndex = jsonString.IndexOf('{', scoresIndex);
                if (openBraceIndex == -1)
                {
                    Debug.LogWarning("[MessageHandler] scores 객체 시작을 찾을 수 없습니다");
                    return scores;
                }

                // scores 객체 끝점 찾기 (중첩된 {} 고려)
                int braceCount = 0;
                int closeBraceIndex = -1;
                for (int i = openBraceIndex; i < jsonString.Length; i++)
                {
                    if (jsonString[i] == '{') braceCount++;
                    else if (jsonString[i] == '}')
                    {
                        braceCount--;
                        if (braceCount == 0)
                        {
                            closeBraceIndex = i;
                            break;
                        }
                    }
                }

                if (closeBraceIndex == -1)
                {
                    Debug.LogWarning("[MessageHandler] scores 객체 끝을 찾을 수 없습니다");
                    return scores;
                }

                // scores 객체 문자열 추출
                string scoresJson = jsonString.Substring(openBraceIndex, closeBraceIndex - openBraceIndex + 1);

                // 간단한 파싱: {"key1":value1,"key2":value2,...} 형태
                scoresJson = scoresJson.Trim('{', '}');
                if (string.IsNullOrEmpty(scoresJson))
                {
                    Debug.Log("[MessageHandler] scores 객체가 비어있습니다");
                    return scores;
                }

                // 키-값 쌍 분리
                string[] pairs = scoresJson.Split(',');
                foreach (string pair in pairs)
                {
                    string[] keyValue = pair.Split(':');
                    if (keyValue.Length == 2)
                    {
                        string key = keyValue[0].Trim().Trim('"');
                        if (int.TryParse(keyValue[1].Trim(), out int value))
                        {
                            scores[key] = value;
                        }
                    }
                }

                Debug.Log($"[MessageHandler] scores Dictionary 파싱 성공: {scores.Count}개 플레이어");
                foreach (var kvp in scores)
                {
                    Debug.Log($"[MessageHandler] - {kvp.Key}: {kvp.Value}점");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MessageHandler] scores Dictionary 파싱 실패: {ex.Message}");
            }

            return scores;
        }

        // ========================================
        // Public 메서드들 (외부에서 직접 호출)
        // ========================================

        /// <summary>
        /// 외부에서 에러를 직접 전달할 때 사용 (NetworkManager용)
        /// </summary>
        public void TriggerError(string errorMessage)
        {
            // NetworkClient에서 이미 로그를 출력하므로 여기서는 로그 생략
            OnErrorReceived?.Invoke(errorMessage);
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
        public string displayName;  // 사용자 별명 (UI에서 표시용)
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
    /// MY_STATS_UPDATE JSON 파싱용 구조체
    /// </summary>
    [System.Serializable]
    public class StatsUpdateData
    {
        public string username;
        public string displayName;
        public int level;
        public int totalGames;
        public int wins;
        public int losses;
        public int draws;
        public int currentExp;
        public int requiredExp;
        public float winRate;
        public float averageScore;
        public int totalScore;
        public int bestScore;
        public string status;
    }

    /// <summary>
    /// 방 정보 구조체
    /// </summary>
    [System.Serializable]
    public class RoomInfo
    {
        public int roomId;
        public string roomName;
        public string hostName = "호스트";  // UI 호환성을 위해 추가
        public int currentPlayers;
        public int maxPlayers;
        public bool isGameStarted;
        public bool isPrivate = false;     // UI 호환성을 위해 추가
        public string gameMode = "클래식"; // UI 호환성을 위해 추가
        
        /// <summary>
        /// Shared.Models.RoomInfo와 호환성을 위한 isPlaying 속성
        /// </summary>
        public bool isPlaying
        {
            get { return isGameStarted; }
            set { isGameStarted = value; }
        }
        
        /// <summary>
        /// Shared.Models.RoomInfo로부터 암시적 변환
        /// </summary>
        public static implicit operator RoomInfo(Shared.Models.RoomInfo sharedRoom)
        {
            return new RoomInfo
            {
                roomId = sharedRoom.roomId,
                roomName = sharedRoom.roomName,
                hostName = sharedRoom.hostName,
                currentPlayers = sharedRoom.currentPlayers,
                maxPlayers = sharedRoom.maxPlayers,
                isGameStarted = sharedRoom.isPlaying,
                isPrivate = sharedRoom.isPrivate,
                gameMode = sharedRoom.gameMode
            };
        }
        
        /// <summary>
        /// Shared.Models.RoomInfo로 암시적 변환
        /// </summary>
        public static implicit operator Shared.Models.RoomInfo(RoomInfo netRoom)
        {
            return new Shared.Models.RoomInfo
            {
                roomId = netRoom.roomId,
                roomName = netRoom.roomName,
                hostName = netRoom.hostName,
                currentPlayers = netRoom.currentPlayers,
                maxPlayers = netRoom.maxPlayers,
                isPlaying = netRoom.isGameStarted,
                isPrivate = netRoom.isPrivate,
                gameMode = netRoom.gameMode
            };
        }
        
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
    /// 서버 BLOCK_PLACED 메시지의 JSON 파싱용 데이터 구조
    /// </summary>
    [System.Serializable]
    public class BlockPlacedData
    {
        public string player;           // 플레이어 이름
        public int blockType;          // 블록 타입 (서버 enum)
        public BlockPosition position; // 위치 정보
        public int rotation;           // 회전
        public int flip;              // 플립 (0 또는 1)
        public int playerColor;       // 플레이어 색상 (1-4)
        public int scoreGained;       // 획득 점수
        public BlockPosition[] placedCells; // 실제 배치된 셀들의 좌표 (개선된 동기화)
    }
    
    /// <summary>
    /// 블록 위치 정보
    /// </summary>
    [System.Serializable]
    public class BlockPosition
    {
        public int row;
        public int col;
    }
}