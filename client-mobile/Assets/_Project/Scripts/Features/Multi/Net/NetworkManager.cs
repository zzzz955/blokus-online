using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Shared.Models;
using Features.Multi.Net;
using App.Network;
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
        [SerializeField] private float reconnectDelay = 1f;
        [SerializeField] private int maxReconnectAttempts = 3;
        
        [Header("하트비트 설정")]
        [SerializeField] private bool enableHeartbeat = false; // 모바일 클라이언트는 하트비트 불필요
        [SerializeField] private float heartbeatInterval = 30.0f;
        
        // 컴포넌트 참조
        private NetworkClient networkClient;
        private MessageHandler messageHandler;
        private TokenVerificationService tokenVerificationService;
        
        // 상태 관리
        private bool isInitialized;
        private int reconnectAttempts;
        private Coroutine heartbeatCoroutine;
        private bool isAuthenticated = false;
        private string lastUsedToken = null;
        
        // 현재 사용자 정보 (LobbyPanel에서 접근 가능)
        private UserInfo currentUserInfo = null;
        
        // 현재 방 정보 (GameRoomPanel에서 접근 가능)
        private RoomInfo currentRoomInfo = null;
        
        // 싱글톤 패턴
        public static NetworkManager Instance { get; private set; }
        
        // 현재 사용자 정보 접근 프로퍼티
        public UserInfo CurrentUserInfo => currentUserInfo;
        
        // 현재 방 정보 접근 프로퍼티
        public RoomInfo CurrentRoomInfo => currentRoomInfo;
        
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

        public event System.Action OnRoomJoined
        {
            add { if (messageHandler != null) messageHandler.OnRoomJoined += value; }
            remove { if (messageHandler != null) messageHandler.OnRoomJoined -= value; }
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

        public event System.Action<string, string, string> OnChatMessageReceived
        {
            add { if (messageHandler != null) messageHandler.OnChatMessageReceived += value; }
            remove { if (messageHandler != null) messageHandler.OnChatMessageReceived -= value; }
        }

        public event System.Action<System.Collections.Generic.List<UserInfo>> OnUserListUpdated
        {
            add { if (messageHandler != null) messageHandler.OnUserListUpdated += value; }
            remove { if (messageHandler != null) messageHandler.OnUserListUpdated -= value; }
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
            
            // 토큰 검증 서비스 초기화 (EnvironmentModeManager에서 자동으로 URL 설정)
            tokenVerificationService = new TokenVerificationService(null);
            
            // 연결 상태 변경 이벤트 구독
            networkClient.OnConnectionChanged += OnConnectionStatusChanged;
            networkClient.OnError += OnNetworkError;
            
            // 사용자 정보 업데이트 이벤트 구독
            if (messageHandler != null)
            {
                messageHandler.OnMyStatsUpdated += OnMyStatsUpdatedHandler;
                messageHandler.OnRoomCreated += OnRoomCreatedHandler;
                messageHandler.OnRoomJoined += OnRoomJoinedHandler;
            }
            
            isInitialized = true;
            Debug.Log("[NetworkManager] 초기화 완료 (토큰 검증 서비스 포함)");
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
            
            // 인증 상태 초기화
            isAuthenticated = false;
            lastUsedToken = null;
            
            Debug.Log("[NetworkManager] 서버 연결 해제됨 (인증 상태 초기화)");
        }
        
        /// <summary>
        /// 로그아웃 - 재연결 없이 서버 연결 해제
        /// </summary>
        public void LogoutFromServer()
        {
            if (!isInitialized)
                return;
                
            Debug.Log("[NetworkManager] 로그아웃 시작 - 자동 재연결 비활성화");
            
            // 자동 재연결 임시 비활성화
            bool originalAutoReconnect = autoReconnect;
            autoReconnect = false;
            reconnectAttempts = maxReconnectAttempts; // 재연결 시도 횟수를 최대로 설정
            
            StopHeartbeat();
            networkClient?.DisconnectFromServer();
            
            // 인증 상태 및 사용자 정보 초기화
            isAuthenticated = false;
            lastUsedToken = null;
            currentUserInfo = null;
            currentRoomInfo = null;
            
            Debug.Log("[NetworkManager] 로그아웃 완료 - 모든 세션 정보 정리됨");
            
            // 1초 후 자동 재연결 설정을 원래대로 복구 (다음 연결을 위해)
            StartCoroutine(RestoreAutoReconnectAfterLogout(originalAutoReconnect));
        }
        
        /// <summary>
        /// 로그아웃 후 자동 재연결 설정 복구
        /// </summary>
        private System.Collections.IEnumerator RestoreAutoReconnectAfterLogout(bool originalSetting)
        {
            yield return new UnityEngine.WaitForSeconds(2f);
            autoReconnect = originalSetting;
            reconnectAttempts = 0; // 재연결 시도 횟수 리셋
            Debug.Log($"[NetworkManager] 자동 재연결 설정 복구: {autoReconnect}");
        }
        
        /// <summary>
        /// 연결 상태 확인
        /// </summary>
        public bool IsConnected()
        {
            return isInitialized && networkClient != null && networkClient.IsConnected();
        }
        
        /// <summary>
        /// 인증 상태 확인
        /// </summary>
        public bool IsAuthenticated()
        {
            return isAuthenticated && IsConnected();
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
        /// JWT 로그인 요청 (기존 - 호환성 유지)
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
        /// 모바일 클라이언트용 개선된 연결 및 인증
        /// 사전 토큰 검증 → TCP 연결 → 간소화 인증 플로우
        /// </summary>
        /// <param name="currentAccessToken">현재 Access Token</param>
        /// <param name="clientId">OIDC 클라이언트 ID</param>
        /// <returns>연결 및 인증 성공 여부</returns>
        public async Task<bool> ConnectToMultiplayerAsync(string currentAccessToken, string clientId)
        {
            try
            {
                if (!isInitialized)
                {
                    Debug.LogError("[NetworkManager] NetworkManager가 초기화되지 않았습니다.");
                    return false;
                }

                if (string.IsNullOrEmpty(currentAccessToken))
                {
                    Debug.LogError("[NetworkManager] Access Token이 필요합니다.");
                    return false;
                }

                if (string.IsNullOrEmpty(clientId))
                {
                    Debug.LogError("[NetworkManager] Client ID가 필요합니다.");
                    return false;
                }

                Debug.Log("[NetworkManager] 모바일 멀티플레이어 연결 시작...");

                // Step 1: 토큰 사전 검증 및 갱신
                Debug.Log("[NetworkManager] Step 1: 토큰 사전 검증 중...");
                var verifyResult = await tokenVerificationService.VerifyTokenForTcpConnection(currentAccessToken, clientId);
                
                if (!verifyResult.Valid)
                {
                    Debug.LogError($"[NetworkManager] 토큰 검증 실패: {verifyResult.GetUserFriendlyMessage()}");
                    OnNetworkError($"인증 실패: {verifyResult.GetUserFriendlyMessage()}");
                    return false;
                }

                // 갱신된 토큰이 있으면 사용
                string validToken = verifyResult.AccessToken ?? currentAccessToken;
                
                if (verifyResult.Refreshed)
                {
                    Debug.Log("[NetworkManager] 토큰이 자동으로 갱신되었습니다.");
                    
                    // 갱신된 토큰을 SecureStorage에 저장
                    if (!string.IsNullOrEmpty(verifyResult.AccessToken))
                    {
                        App.Security.SecureStorage.StoreString(App.Security.TokenKeys.Access, verifyResult.AccessToken);
                        
                        // 만료 시간 업데이트 (1분 버퍼 포함)
                        if (verifyResult.ExpiresIn > 0)
                        {
                            var expiryTime = DateTime.UtcNow.AddSeconds(verifyResult.ExpiresIn - 60);
                            App.Security.SecureStorage.StoreString(App.Security.TokenKeys.Expiry, expiryTime.ToBinary().ToString());
                        }
                        
                        Debug.Log("[NetworkManager] 갱신된 토큰을 SecureStorage에 저장 완료");
                    }
                }

                Debug.Log($"[NetworkManager] 토큰 검증 성공 (만료: {verifyResult.ExpiresIn}초 후)");

                // Step 2: TCP 서버 연결
                Debug.Log("[NetworkManager] Step 2: TCP 서버 연결 중...");
                bool connected = await networkClient.ConnectToServerAsync();
                
                if (!connected)
                {
                    Debug.LogError("[NetworkManager] TCP 서버 연결 실패");
                    OnNetworkError("서버 연결에 실패했습니다.");
                    return false;
                }

                Debug.Log("[NetworkManager] TCP 서버 연결 성공");

                // Step 3: 모바일 클라이언트 인증 (중복 방지)
                Debug.Log("[NetworkManager] Step 3: 모바일 클라이언트 인증 중...");
                
                // 이미 같은 토큰으로 인증된 경우 건너뛰기
                if (isAuthenticated && validToken == lastUsedToken)
                {
                    Debug.Log("[NetworkManager] 이미 인증됨 - 중복 인증 방지");
                    return true;
                }
                
                bool authSuccess = await SendMobileJwtLoginAsync(validToken);
                
                if (!authSuccess)
                {
                    Debug.LogError("[NetworkManager] 모바일 클라이언트 인증 실패");
                    DisconnectFromServer();
                    OnNetworkError("모바일 클라이언트 인증에 실패했습니다.");
                    return false;
                }

                // 인증 성공 시 상태 업데이트
                isAuthenticated = true;
                lastUsedToken = validToken;
                
                Debug.Log("[NetworkManager] 모바일 멀티플레이어 연결 완료!");
                return true;

            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] 모바일 연결 중 예외 발생: {ex.Message}");
                DisconnectFromServer();
                OnNetworkError($"연결 중 오류가 발생했습니다: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 모바일 클라이언트 전용 JWT 로그인 (TCP 서버 간소화 인증) - 서버 응답 대기
        /// </summary>
        private async Task<bool> SendMobileJwtLoginAsync(string validatedToken)
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] TCP 서버에 연결되지 않았습니다.");
                return false;
            }
            
            // 인증 결과를 기다리기 위한 TaskCompletionSource
            var authCompletionSource = new TaskCompletionSource<bool>();
            System.Action<string> tempErrorHandler = null;
            System.Action<bool, string> tempAuthHandler = null;
            bool authCompleted = false;

            try
            {
                // 인증 성공/실패 응답 핸들러 등록
                tempAuthHandler = (success, message) => {
                    if (authCompleted) return; // 이미 완료된 경우 무시
                    
                    authCompleted = true;
                    Debug.Log($"[NetworkManager] 인증 응답 수신: {success} - {message}");
                    authCompletionSource.TrySetResult(success);
                };

                // 에러 응답 핸들러 등록 (인증 관련 에러만 처리)
                tempErrorHandler = (errorMessage) => {
                    if (authCompleted) return; // 이미 완료된 경우 무시
                    
                    if (errorMessage.Contains("인증 토큰이 유효하지 않습니다") || 
                        errorMessage.Contains("authentication") ||
                        errorMessage.Contains("토큰") ||
                        errorMessage.Contains("AUTH"))
                    {
                        authCompleted = true;
                        Debug.Log($"[NetworkManager] 인증 실패 감지: {errorMessage}");
                        authCompletionSource.TrySetResult(false);
                    }
                };

                // 이벤트 구독
                OnAuthResponse += tempAuthHandler;
                OnErrorReceived += tempErrorHandler;

                // 모바일 클라이언트 전용 인증 요청 전송
                bool sent = networkClient.SendProtocolMessage("auth", "mobile_jwt", validatedToken);
                
                if (!sent)
                {
                    return false;
                }

                Debug.Log("[NetworkManager] JWT 인증 요청 전송됨, 응답 대기 중...");

                // 서버 응답 대기 (최대 10초로 증가)
                var timeoutTask = Task.Delay(10000);
                var completedTask = await Task.WhenAny(authCompletionSource.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // 타임아웃 - 실제 응답을 받지 못함
                    authCompleted = true;
                    Debug.LogWarning("[NetworkManager] 인증 응답 타임아웃 - 네트워크 문제 가능성");
                    return false; // 타임아웃을 실패로 처리
                }

                // 실제 서버 응답 결과 반환
                bool result = authCompletionSource.Task.Result;
                Debug.Log($"[NetworkManager] 인증 최종 결과: {result}");
                return result;
            }
            finally
            {
                // 이벤트 핸들러 해제
                if (tempAuthHandler != null)
                {
                    OnAuthResponse -= tempAuthHandler;
                }
                if (tempErrorHandler != null)
                {
                    OnErrorReceived -= tempErrorHandler;
                }
            }
        }

        /// <summary>
        /// 모바일 클라이언트 전용 JWT 로그인 (TCP 서버 간소화 인증) - 동기 버전
        /// </summary>
        private bool SendMobileJwtLogin(string validatedToken)
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] TCP 서버에 연결되지 않았습니다.");
                return false;
            }
            
            // 모바일 클라이언트 전용 인증 요청 (기존 JWT 메시지와 구분)
            return networkClient.SendProtocolMessage("auth", "mobile_jwt", validatedToken);
        }

        /// <summary>
        /// 동기 버전 (Unity 코루틴/이벤트 핸들러용)
        /// </summary>
        public void ConnectToMultiplayer(string currentAccessToken, string clientId)
        {
            _ = ConnectToMultiplayerAsync(currentAccessToken, clientId);
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
        public bool CreateRoom(string roomName, bool isPrivate = false, string password = "")
        {
            if (!IsConnected())
            {
                Debug.LogWarning("[NetworkManager] 서버에 연결되지 않았습니다.");
                return false;
            }
            
            return networkClient.SendCreateRoomRequest(roomName, isPrivate, password);
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
            
            // MessageHandler를 통해 에러 이벤트 발생 (UI에서 구독 가능)
            messageHandler?.TriggerError(errorMessage);
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
        /// 사용자 정보 업데이트 핸들러 (내부용)
        /// </summary>
        private void OnMyStatsUpdatedHandler(UserInfo userInfo)
        {
            currentUserInfo = userInfo;
            Debug.Log($"[NetworkManager] 현재 사용자 정보 저장됨: {userInfo.displayName} [{userInfo.username}]");
        }

        /// <summary>
        /// 방 생성 핸들러 (내부용)
        /// </summary>
        private void OnRoomCreatedHandler(RoomInfo roomInfo)
        {
            currentRoomInfo = roomInfo;
            Debug.Log($"[NetworkManager] 현재 방 정보 저장됨 (생성): {roomInfo.roomName} [ID: {roomInfo.roomId}]");
        }

        /// <summary>
        /// 방 입장 핸들러 (내부용)
        /// </summary>
        private void OnRoomJoinedHandler()
        {
            // 방 입장 시 추가 방 정보를 서버에서 받을 수 있도록 요청
            Debug.Log($"[NetworkManager] 방 입장됨 - 방 정보 요청 필요");
            // TODO: 서버에서 방 정보를 받는 메시지 처리 추가 필요
        }

        /// <summary>
        /// NetworkManager 정리 (MultiCoreBootstrap에서 사용)
        /// </summary>
        public void Cleanup()
        {
            // 이벤트 구독 해제
            if (messageHandler != null)
            {
                messageHandler.OnMyStatsUpdated -= OnMyStatsUpdatedHandler;
                messageHandler.OnRoomCreated -= OnRoomCreatedHandler;
                messageHandler.OnRoomJoined -= OnRoomJoinedHandler;
            }
            
            DisconnectFromServer();
            currentUserInfo = null;
            currentRoomInfo = null;
        }
        
        // ========================================
        // Missing Events (Stub) - OnRoomJoined는 위에서 구현됨
        // ========================================
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
            if (networkClient != null && networkClient.IsConnected())
            {
                bool success = networkClient.SendMessage("lobby:list");
                if (success)
                {
                    Debug.Log("[NetworkManager] 온라인 사용자 목록 요청 전송");
                }
                else
                {
                    Debug.LogError("[NetworkManager] 온라인 사용자 목록 요청 실패");
                }
            }
            else
            {
                Debug.LogError("[NetworkManager] RequestOnlineUsers: 서버에 연결되지 않음");
            }
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
            if (string.IsNullOrEmpty(message?.Trim()))
            {
                Debug.LogWarning("[NetworkManager] SendChatMessage: 빈 메시지는 전송할 수 없습니다.");
                return;
            }

            if (networkClient != null && networkClient.IsConnected())
            {
                bool success = networkClient.SendChatMessage(message);
                if (success)
                {
                    Debug.Log($"[NetworkManager] SendChatMessage: {message}");
                }
                else
                {
                    Debug.LogError($"[NetworkManager] SendChatMessage 실패: {message}");
                }
            }
            else
            {
                Debug.LogError("[NetworkManager] SendChatMessage: 서버에 연결되지 않음");
            }
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
        
        // ========================================
        // 테스트 및 검증 메서드
        // ========================================
        
        /// <summary>
        /// 기본 통신 테스트 (연결, 인증, 로비 입장)
        /// </summary>
        public async Task<bool> TestBasicCommunication(string testToken = null)
        {
            Debug.Log("[NetworkManager] === 기본 통신 테스트 시작 ===");
            
            try
            {
                // Step 1: 연결 테스트
                Debug.Log("[NetworkManager] 1단계: 서버 연결 테스트");
                if (!IsConnected())
                {
                    await networkClient.ConnectToServerAsync();
                    await Task.Delay(1000); // 연결 대기
                }
                
                if (!IsConnected())
                {
                    Debug.LogError("[NetworkManager] 연결 테스트 실패");
                    return false;
                }
                Debug.Log("[NetworkManager] ✅ 서버 연결 성공");
                
                // Step 2: 인증 테스트 (JWT 토큰이 있는 경우)
                if (!string.IsNullOrEmpty(testToken))
                {
                    Debug.Log("[NetworkManager] 2단계: JWT 인증 테스트");
                    bool authResult = await SendMobileJwtLoginAsync(testToken);
                    if (!authResult)
                    {
                        Debug.LogWarning("[NetworkManager] ⚠️ JWT 인증 테스트 실패 (계속 진행)");
                    }
                    else
                    {
                        Debug.Log("[NetworkManager] ✅ JWT 인증 성공");
                    }
                }
                
                // Step 3: 게스트 로그인 테스트
                Debug.Log("[NetworkManager] 3단계: 게스트 로그인 테스트");
                bool guestResult = GuestLogin();
                if (!guestResult)
                {
                    Debug.LogWarning("[NetworkManager] ⚠️ 게스트 로그인 전송 실패");
                }
                else
                {
                    Debug.Log("[NetworkManager] ✅ 게스트 로그인 요청 전송됨");
                }
                
                await Task.Delay(2000); // 응답 대기
                
                // Step 4: 로비 입장 테스트
                Debug.Log("[NetworkManager] 4단계: 로비 입장 테스트");
                bool lobbyResult = EnterLobby();
                if (!lobbyResult)
                {
                    Debug.LogWarning("[NetworkManager] ⚠️ 로비 입장 요청 실패");
                }
                else
                {
                    Debug.Log("[NetworkManager] ✅ 로비 입장 요청 전송됨");
                }
                
                await Task.Delay(2000); // 응답 대기
                
                Debug.Log("[NetworkManager] === 기본 통신 테스트 완료 ===");
                
                return true;
                
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] 통신 테스트 중 예외: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 메시지 프로토콜 테스트 (서버 프로토콜 형식 확인)
        /// </summary>
        public void TestMessageProtocols()
        {
            Debug.Log("[NetworkManager] === 메시지 프로토콜 테스트 시작 ===");
            
            if (!IsConnected())
            {
                Debug.LogError("[NetworkManager] 서버에 연결되지 않았습니다.");
                return;
            }
            
            // 다양한 프로토콜 메시지 테스트
            Debug.Log("[NetworkManager] 하트비트 테스트...");
            networkClient.SendHeartbeat();
            
            Debug.Log("[NetworkManager] 로비 목록 요청 테스트...");
            networkClient.SendCleanTCPMessage("lobby", "list");
            
            Debug.Log("[NetworkManager] 방 목록 요청 테스트...");
            networkClient.SendCleanTCPMessage("room", "list");
            
            Debug.Log("[NetworkManager] === 메시지 프로토콜 테스트 완료 ===");
        }
    }
}