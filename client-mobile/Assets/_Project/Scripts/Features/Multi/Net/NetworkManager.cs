using System.Collections;
using UnityEngine;
using Shared.Models;
using Features.Multi.Net;
using MultiModels = Features.Multi.Models;

namespace Features.Multi.Net
{
    /// <summary>
    /// 네트워크 매니저 - NetworkClient와 MessageHandler 통합 관리
    /// UI 시스템에서 쉽게 접근할 수 있도록 하는 파사드 패턴
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        [Header("자동 연결 설정")]
        [SerializeField] private bool autoConnectOnStart = false;
        [SerializeField] private bool autoReconnect = true;
        [SerializeField] private float reconnectDelay = 3.0f;
        [SerializeField] private int maxReconnectAttempts = 5;
        
        [Header("하트비트 설정")]
        [SerializeField] private bool enableHeartbeat = true;
        [SerializeField] private float heartbeatInterval = 30.0f;
        
        // 컴포넌트 참조
        private NetworkClient networkClient;
        private MessageHandler messageHandler;
        
        // 상태 관리
        private bool isInitialized;
        private int reconnectAttempts;
        private Coroutine heartbeatCoroutine;
        
        // 싱글톤 패턴
        public static NetworkManager Instance { get; private set; }
        
        // 이벤트 (MessageHandler 이벤트를 래핑)
        public event System.Action<bool> OnConnectionChanged
        {
            add { if (networkClient != null) networkClient.OnConnectionChanged += value; }
            remove { if (networkClient != null) networkClient.OnConnectionChanged -= value; }
        }
        
        public event System.Action<bool, string> OnAuthResponse
        {
            add { if (messageHandler != null) messageHandler.OnAuthResponse += value; }
            remove { if (messageHandler != null) messageHandler.OnAuthResponse -= value; }
        }
        
        public event System.Action<UserInfo> OnMyStatsUpdated
        {
            add { if (messageHandler != null) messageHandler.OnMyStatsUpdated += value; }
            remove { if (messageHandler != null) messageHandler.OnMyStatsUpdated -= value; }
        }
        
        public event System.Action<UserInfo> OnUserStatsReceived
        {
            add { if (messageHandler != null) messageHandler.OnUserStatsReceived += value; }
            remove { if (messageHandler != null) messageHandler.OnUserStatsReceived -= value; }
        }
        
        // 로비 관련 이벤트
        public event System.Action<System.Collections.Generic.List<RoomInfo>> OnRoomListUpdated
        {
            add { if (messageHandler != null) messageHandler.OnRoomListUpdated += value; }
            remove { if (messageHandler != null) messageHandler.OnRoomListUpdated -= value; }
        }
        
        public event System.Action<RoomInfo> OnRoomCreated
        {
            add { if (messageHandler != null) messageHandler.OnRoomCreated += value; }
            remove { if (messageHandler != null) messageHandler.OnRoomCreated -= value; }
        }
        
        public event System.Action<bool, string> OnJoinRoomResponse
        {
            add { if (messageHandler != null) messageHandler.OnJoinRoomResponse += value; }
            remove { if (messageHandler != null) messageHandler.OnJoinRoomResponse -= value; }
        }
        
        // 게임 관련 이벤트
        public event System.Action<MultiModels.BlockPlacement> OnBlockPlaced
        {
            add { if (messageHandler != null) messageHandler.OnBlockPlaced += value; }
            remove { if (messageHandler != null) messageHandler.OnBlockPlaced -= value; }
        }
        
        public event System.Action<MultiModels.PlayerColor> OnTurnChanged
        {
            add { if (messageHandler != null) messageHandler.OnTurnChanged += value; }
            remove { if (messageHandler != null) messageHandler.OnTurnChanged -= value; }
        }
        
        public event System.Action<MultiModels.PlayerColor> OnGameEnded
        {
            add { if (messageHandler != null) messageHandler.OnGameEnded += value; }
            remove { if (messageHandler != null) messageHandler.OnGameEnded -= value; }
        }
        
        // 에러 관련 이벤트
        public event System.Action<string> OnErrorReceived
        {
            add { if (messageHandler != null) messageHandler.OnErrorReceived += value; }
            remove { if (messageHandler != null) messageHandler.OnErrorReceived -= value; }
        }
        
        void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeComponents();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void Start()
        {
            if (autoConnectOnStart && isInitialized)
            {
                ConnectToServer();
            }
        }
        
        void OnDestroy()
        {
            StopHeartbeat();
        }
        
        // ========================================
        // 초기화
        // ========================================
        
        /// <summary>
        /// 네트워크 컴포넌트들 초기화
        /// </summary>
        private void InitializeComponents()
        {
            // NetworkClient 컴포넌트 확인/추가
            networkClient = GetComponent<NetworkClient>();
            if (networkClient == null)
            {
                networkClient = gameObject.AddComponent<NetworkClient>();
            }
            
            // MessageHandler 컴포넌트 확인/추가
            messageHandler = GetComponent<MessageHandler>();
            if (messageHandler == null)
            {
                messageHandler = gameObject.AddComponent<MessageHandler>();
            }
            
            // 연결 상태 변경 이벤트 구독
            networkClient.OnConnectionChanged += OnConnectionStatusChanged;
            networkClient.OnError += OnNetworkError;
            
            isInitialized = true;
            Debug.Log("[NetworkManager] 초기화 완료");
        }
        
        // ========================================
        // 연결 관리
        // ========================================
        
        /// <summary>
        /// 서버 연결
        /// </summary>
        public async void ConnectToServer()
        {
            if (!isInitialized)
            {
                Debug.LogError("[NetworkManager] 초기화되지 않았습니다.");
                return;
            }
            
            if (IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 이미 서버에 연결되어 있습니다.");
                return;
            }
            
            Debug.Log("[NetworkManager] 서버 연결 시도...");
            await networkClient.ConnectToServerAsync();
        }
        
        /// <summary>
        /// 서버 연결 해제
        /// </summary>
        public void DisconnectFromServer()
        {
            if (!isInitialized)
                return;
            
            StopHeartbeat();
            networkClient?.DisconnectFromServer();
            
            Debug.Log("[NetworkManager] 서버 연결 해제됨");
        }
        
        /// <summary>
        /// 연결 상태 확인
        /// </summary>
        public bool IsConnected()
        {
            return isInitialized && networkClient != null && networkClient.IsConnected();
        }
        
        /// <summary>
        /// 서버 정보 설정
        /// </summary>
        public void SetServerInfo(string host, int port)
        {
            if (networkClient != null)
            {
                networkClient.SetServerInfo(host, port);
            }
        }
        
        // ========================================
        // 메시지 전송 (편의 함수들)
        // ========================================
        
        /// <summary>
        /// JWT 로그인 요청
        /// </summary>
        public bool JwtLogin(string token)
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendJwtLoginRequest(token);
        }
        
        /// <summary>
        /// 로그인 요청
        /// </summary>
        public bool Login(string username, string password)
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendLoginRequest(username, password);
        }
        
        /// <summary>
        /// 회원가입 요청
        /// </summary>
        public bool Register(string username, string password)
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendRegisterRequest(username, password);
        }
        
        /// <summary>
        /// 게스트 로그인
        /// </summary>
        public bool GuestLogin()
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendGuestLoginRequest();
        }
        
        /// <summary>
        /// 사용자 통계 요청
        /// </summary>
        public bool RequestUserStats(string username)
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendGetUserStatsRequest(username);
        }
        
        /// <summary>
        /// 로비 입장 요청
        /// </summary>
        public bool EnterLobby()
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendLobbyEnterRequest();
        }
        
        /// <summary>
        /// 방 생성 요청
        /// </summary>
        public bool CreateRoom(string roomName, int maxPlayers = 4)
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendCreateRoomRequest(roomName, maxPlayers);
        }
        
        /// <summary>
        /// 방 참가 요청
        /// </summary>
        public bool JoinRoom(int roomId)
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendJoinRoomRequest(roomId);
        }
        
        /// <summary>
        /// 방 나가기 요청
        /// </summary>
        public bool LeaveRoom()
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendLeaveRoomRequest();
        }
        
        /// <summary>
        /// 플레이어 준비 상태 설정
        /// </summary>
        public bool SetPlayerReady(bool isReady)
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendPlayerReadyRequest(isReady);
        }
        
        /// <summary>
        /// 게임 시작 요청
        /// </summary>
        public bool StartGame()
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendStartGameRequest();
        }
        
        /// <summary>
        /// 블록 배치 요청
        /// </summary>
        public bool PlaceBlock(MultiModels.BlockPlacement placement)
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendPlaceBlockRequest(placement);
        }
        
        // ========================================
        // 이벤트 핸들러들
        // ========================================
        
        /// <summary>
        /// 연결 상태 변경 처리
        /// </summary>
        private void OnConnectionStatusChanged(bool isConnected)
        {
            Debug.Log($"[NetworkManager] 연결 상태 변경: {(isConnected ? "연결됨" : "연결 해제됨")}");
            
            if (isConnected)
            {
                // 연결 성공
                reconnectAttempts = 0;
                StartHeartbeat();
            }
            else
            {
                // 연결 해제
                StopHeartbeat();
                
                // 자동 재연결 시도
                if (autoReconnect && reconnectAttempts < maxReconnectAttempts)
                {
                    StartCoroutine(AttemptReconnect());
                }
            }
        }
        
        /// <summary>
        /// 네트워크 에러 처리
        /// </summary>
        private void OnNetworkError(string errorMessage)
        {
            Debug.LogError($"[NetworkManager] 네트워크 에러: {errorMessage}");
            
            // UI에서 에러 처리 필요시 이벤트 발생
            // OnErrorReceived?.Invoke(errorMessage);
        }
        
        // ========================================
        // 자동 재연결
        // ========================================
        
        /// <summary>
        /// 재연결 시도
        /// </summary>
        private IEnumerator AttemptReconnect()
        {
            reconnectAttempts++;
            Debug.Log($"[NetworkManager] 재연결 시도 {reconnectAttempts}/{maxReconnectAttempts} ({reconnectDelay}초 후)");
            
            yield return new WaitForSeconds(reconnectDelay);
            
            if (!IsConnected())
            {
                ConnectToServer();
            }
        }
        
        // ========================================
        // 하트비트 시스템
        // ========================================
        
        /// <summary>
        /// 하트비트 시작
        /// </summary>
        private void StartHeartbeat()
        {
            if (!enableHeartbeat)
                return;
            
            StopHeartbeat(); // 기존 코루틴 중지
            heartbeatCoroutine = StartCoroutine(HeartbeatCoroutine());
            Debug.Log("[NetworkManager] 하트비트 시작");
        }
        
        /// <summary>
        /// 하트비트 중지
        /// </summary>
        private void StopHeartbeat()
        {
            if (heartbeatCoroutine != null)
            {
                StopCoroutine(heartbeatCoroutine);
                heartbeatCoroutine = null;
                Debug.Log("[NetworkManager] 하트비트 중지");
            }
        }
        
        /// <summary>
        /// 하트비트 코루틴
        /// </summary>
        private IEnumerator HeartbeatCoroutine()
        {
            while (IsConnected())
            {
                yield return new WaitForSeconds(heartbeatInterval);
                
                if (IsConnected())
                {
                    networkClient.SendHeartbeat();
                    Debug.Log("[NetworkManager] 하트비트 전송");
                }
            }
        }
        
        // ========================================
        // 디버그 및 상태 확인
        // ========================================
        
        /// <summary>
        /// 네트워크 상태 정보 반환
        /// </summary>
        public string GetStatusInfo()
        {
            if (!isInitialized)
                return "초기화되지 않음";
            
            string status = IsConnected() ? "연결됨" : "연결 안됨";
            string reconnectInfo = autoReconnect ? $" (재연결: {reconnectAttempts}/{maxReconnectAttempts})" : "";
            
            return $"상태: {status}{reconnectInfo}";
        }
        
        /// <summary>
        /// 수동 하트비트 전송 (디버그용)
        /// </summary>
        public void SendManualHeartbeat()
        {
            if (IsConnected())
            {
                networkClient.SendHeartbeat();
                Debug.Log("[NetworkManager] 수동 하트비트 전송");
            }
            else
            {
                Debug.LogWarning("[NetworkManager] 연결되지 않아 하트비트를 전송할 수 없습니다.");
            }
        }
        
        /// <summary>
        /// 설정 업데이트
        /// </summary>
        public void UpdateSettings(bool autoReconnectEnabled, float reconnectDelaySeconds, int maxAttempts)
        {
            autoReconnect = autoReconnectEnabled;
            reconnectDelay = reconnectDelaySeconds;
            maxReconnectAttempts = maxAttempts;
            
            Debug.Log($"[NetworkManager] 설정 업데이트: 자동재연결={autoReconnect}, 딜레이={reconnectDelay}s, 최대시도={maxReconnectAttempts}");
        }
        
        /// <summary>
        /// 하트비트 설정 업데이트
        /// </summary>
        public void UpdateHeartbeatSettings(bool enabled, float intervalSeconds)
        {
            bool wasEnabled = enableHeartbeat;
            enableHeartbeat = enabled;
            heartbeatInterval = intervalSeconds;
            
            if (enabled && !wasEnabled && IsConnected())
            {
                StartHeartbeat();
            }
            else if (!enabled && wasEnabled)
            {
                StopHeartbeat();
            }
            
            Debug.Log($"[NetworkManager] 하트비트 설정 업데이트: 활성화={enableHeartbeat}, 간격={heartbeatInterval}s");
        }
        
        /// <summary>
        /// 현재 서버 정보 반환
        /// </summary>
        public string GetServerInfo()
        {
            if (networkClient != null)
            {
                return networkClient.GetServerInfo();
            }
            return "NetworkClient 없음";
        }
        
        /// <summary>
        /// 네트워크 통계 정보 반환 (디버그용)
        /// </summary>
        public string GetNetworkStats()
        {
            if (networkClient != null)
            {
                return networkClient.GetNetworkStats();
            }
            return "NetworkClient 없음";
        }
        
        // ========================================
        // Missing Methods (MultiCoreBootstrap 호환)
        // ========================================
        
        /// <summary>
        /// NetworkManager 초기화 (MultiCoreBootstrap에서 사용)
        /// </summary>
        public void Initialize()
        {
            InitializeComponents();
        }
        
        /// <summary>
        /// NetworkManager 정리 (MultiCoreBootstrap에서 사용)
        /// </summary>
        public void Cleanup()
        {
            DisconnectFromServer();
        }
        
        // ========================================
        // Missing Events
        // ========================================
        
        public event System.Action<string> OnChatMessage;
        public event System.Action OnRoomJoined;
        public event System.Action OnRoomLeft;
        public event System.Action<RoomInfo> OnRoomInfoUpdated;
        public event System.Action<UserInfo> OnPlayerJoined;
        public event System.Action<int> OnPlayerLeft;
        public event System.Action<int, bool> OnPlayerReadyChanged;
        public event System.Action OnGameStarted;
        
        // ========================================
        // Missing Request Methods
        // ========================================
        
        /// <summary>
        /// 방 목록 요청
        /// </summary>
        public void RequestRoomList()
        {
            Debug.Log("[NetworkManager] RequestRoomList - Stub");
            // Stub: 서버에 방 목록 요청
        }
        
        /// <summary>
        /// 온라인 사용자 목록 요청
        /// </summary>
        public void RequestOnlineUsers()
        {
            Debug.Log("[NetworkManager] RequestOnlineUsers - Stub");
            // Stub: 서버에 온라인 사용자 목록 요청
        }
        
        /// <summary>
        /// 랭킹 정보 요청
        /// </summary>
        public void RequestRanking()
        {
            Debug.Log("[NetworkManager] RequestRanking - Stub");
            // Stub: 서버에 랭킹 정보 요청
        }
        
        /// <summary>
        /// 채팅 메시지 전송
        /// </summary>
        public void SendChatMessage(string message)
        {
            Debug.Log($"[NetworkManager] SendChatMessage: {message} - Stub");
            // Stub: 서버에 채팅 메시지 전송
        }
        
        /// <summary>
        /// 턴 패스
        /// </summary>
        public void PassTurn()
        {
            Debug.Log("[NetworkManager] PassTurn - Stub");
            // Stub: 서버에 턴 패스 요청
        }
        
        /// <summary>
        /// 플레이어 추방
        /// </summary>
        public void KickPlayer(int playerId)
        {
            Debug.Log($"[NetworkManager] KickPlayer: {playerId} - Stub");
            // Stub: 서버에 플레이어 추방 요청
        }
    }
}