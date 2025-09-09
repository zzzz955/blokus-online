using System;
using System.Collections.Generic;
using UnityEngine;
using Shared.Models;
using Features.Multi.Net;
using MultiModels = Features.Multi.Models;

namespace Features.Multi.Net
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
        public event System.Action OnRoomJoined; // 방 참가됨 (GameRoomPanel로 전환용)
        public event System.Action OnRoomLeft; // 방 나가기 완료 (LobbyPanel로 전환용)
        
        // 게임 관련
        // public event System.Action<GameState> OnGameStateUpdated; // 멀티플레이어에서 사용 예정
        public event System.Action<MultiModels.BlockPlacement> OnBlockPlaced; // 블록 배치됨
        public event System.Action<MultiModels.PlayerColor> OnTurnChanged; // 턴 변경
        public event System.Action<Dictionary<MultiModels.PlayerColor, int>> OnScoresUpdated; // 점수 업데이트
        public event System.Action<MultiModels.PlayerColor> OnGameEnded; // 게임 종료
        
        // 연결 관련
        public event System.Action<string> OnErrorReceived; // 에러 메시지
        public event System.Action OnHeartbeatReceived; // 하트비트 응답
        
        // 채팅 관련
        public event System.Action<string, string, string> OnChatMessageReceived; // username, displayName, message
        
        // 싱글플레이어 관련 (현재 HTTP API로 대체됨)
        // public event System.Action<StageData> OnStageDataReceived; // TCP에서 HTTP API로 이동
        // public event System.Action<UserStageProgress> OnStageProgressReceived; // TCP에서 HTTP API로 이동
        // public event System.Action<bool, string> OnStageCompleteResponse; // TCP에서 HTTP API로 이동
        // public event System.Action<int> OnMaxStageUpdated; // TCP에서 HTTP API로 이동
        
        // 싱글톤 패턴
        public static MessageHandler Instance { get; private set; }
        
        // 중복 구독 방지
        private bool isSubscribedToNetworkClient = false;
        
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
            
            // 로비로 돌아가는 이벤트 발생
            OnRoomLeft?.Invoke();
            Debug.Log("[MessageHandler] OnRoomLeft 이벤트 발생");
        }
        
        /// <summary>
        /// 방 정보 - "ROOM_INFO:방ID:방이름:호스트:현재인원:최대인원:비공개:게임중:게임모드:플레이어데이터..."
        /// </summary>
        private void HandleRoomInfo(string[] parts)
        {
            if (parts.Length < 8)
            {
                Debug.LogError("[MessageHandler] ROOM_INFO 메시지 형식 오류");
                return;
            }
            
            try
            {
                // 방 기본 정보 파싱
                Debug.Log($"[MessageHandler] 방 정보 수신: {string.Join(":", parts)}");
                // 필요시 방 상세 정보를 UI에 전달
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
            if (parts.Length >= 2)
            {
                string username = parts[1];
                string displayName = parts.Length >= 3 ? parts[2] : username;
                Debug.Log($"[MessageHandler] 플레이어 입장: {displayName} [{username}]");
                // 필요시 UI에 플레이어 추가
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
                // 필요시 UI에서 플레이어 제거
            }
        }
        
        /// <summary>
        /// 플레이어 준비 상태 - "PLAYER_READY:username:ready"
        /// </summary>
        private void HandlePlayerReady(string[] parts)
        {
            if (parts.Length >= 3)
            {
                string username = parts[1];
                bool ready = parts[2] == "1" || parts[2].ToLower() == "true";
                Debug.Log($"[MessageHandler] 플레이어 준비 상태: {username} - {(ready ? "준비완료" : "대기중")}");
                // 필요시 UI에 준비 상태 표시
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
            // 게임 UI로 전환
        }
        
        /// <summary>
        /// 게임 상태 업데이트 - "GAME_STATE_UPDATE:jsonData"
        /// </summary>
        private void HandleGameStateUpdate(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string jsonData = string.Join(":", parts, 1, parts.Length - 1);
                Debug.Log($"[MessageHandler] 게임 상태 업데이트: {jsonData}");
                // JSON 파싱하여 게임 상태 업데이트
            }
        }
        
        /// <summary>
        /// 블록 배치됨 - "BLOCK_PLACED:jsonData"
        /// </summary>
        private void HandleBlockPlaced(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string jsonData = string.Join(":", parts, 1, parts.Length - 1);
                Debug.Log($"[MessageHandler] 블록 배치됨: {jsonData}");
                // JSON 파싱하여 블록 배치 정보 처리
                // TODO: JSON 파싱 후 OnBlockPlaced 이벤트 발생
            }
        }
        
        /// <summary>
        /// 턴 변경 - "TURN_CHANGED:jsonData"
        /// </summary>
        private void HandleTurnChanged(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string jsonData = string.Join(":", parts, 1, parts.Length - 1);
                Debug.Log($"[MessageHandler] 턴 변경: {jsonData}");
                // JSON 파싱하여 턴 정보 처리
                // TODO: JSON 파싱 후 OnTurnChanged 이벤트 발생
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
            Debug.Log("[MessageHandler] 게임 종료");
            // OnGameEnded?.Invoke(winner); // TODO: 승자 정보가 있다면 파싱
        }
        
        /// <summary>
        /// 게임 결과 - "GAME_RESULT:jsonData"
        /// </summary>
        private void HandleGameResult(string[] parts)
        {
            if (parts.Length >= 2)
            {
                string resultJson = string.Join(":", parts, 1, parts.Length - 1);
                Debug.Log($"[MessageHandler] 게임 결과: {resultJson}");
                // JSON 파싱하여 결과 처리
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
                // TODO: JSON 파싱 후 OnMyStatsUpdated 이벤트 발생
            }
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
            }
        }
        
        /// <summary>
        /// AFK 해제 성공 - "AFK_UNBLOCK_SUCCESS"
        /// </summary>
        private void HandleAfkUnblockSuccess(string[] parts)
        {
            Debug.Log("[MessageHandler] AFK 해제 성공");
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
        
        // ========================================
        // Public 메서드들 (외부에서 직접 호출)
        // ========================================
        
        /// <summary>
        /// 외부에서 에러를 직접 전달할 때 사용 (NetworkManager용)
        /// </summary>
        public void TriggerError(string errorMessage)
        {
            Debug.LogError($"[MessageHandler] 외부 에러 전달: {errorMessage}");
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
}