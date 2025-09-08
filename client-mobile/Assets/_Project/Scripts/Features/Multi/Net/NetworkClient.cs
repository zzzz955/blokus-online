using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Shared.Models;
using MultiModels = Features.Multi.Models;

namespace Features.Multi.Net
{

    /// <summary>
    /// Unity 블로쿠스 네트워크 클라이언트
    /// C++/Qt 클라이언트와 동일한 TCP 소켓 + 커스텀 문자열 프로토콜 사용
    /// </summary>
    public class NetworkClient : MonoBehaviour
    {
        [Header("서버 연결 설정")]
        [SerializeField] private string serverHost = "localhost";
        [SerializeField] private int serverPort = 9999;
        [SerializeField] private int connectionTimeoutMs = 15000; // 15초로 증가
        
        // 연결 상태
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private StreamReader streamReader;
        private StreamWriter streamWriter;
        private bool isConnected;
        private bool isConnecting;
        
        // 스레딩
        private Thread receiveThread;
        private CancellationTokenSource cancellationTokenSource;
        
        // 메시지 큐 (메인 스레드에서 처리)
        private readonly Queue<string> incomingMessages = new Queue<string>();
        private readonly object messageLock = new object();
        
        // 싱글톤 패턴
        public static NetworkClient Instance { get; private set; }
        
        // 이벤트
        public event System.Action<bool> OnConnectionChanged; // 연결 상태 변경
        public event System.Action<string> OnMessageReceived; // 메시지 수신
        public event System.Action<string> OnError; // 에러 발생
        
        void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeFromEnvironment();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void Start()
        {            
            // 자동 연결은 하지 않음 - 명시적으로 ConnectToServer() 호출 필요
            // UnityMainThreadDispatcher가 없으면 생성
            if (UnityMainThreadDispatcher.Instance == null)
            {
                var dispatcherObj = new GameObject("UnityMainThreadDispatcher");
                dispatcherObj.AddComponent<UnityMainThreadDispatcher>();
            }
        }
        
        void Update()
        {
            // 메인 스레드에서 메시지 처리
            ProcessIncomingMessages();
        }
        
        void OnApplicationPause(bool pauseStatus)
        {
            // 앱이 백그라운드로 갈 때 연결 유지 처리
            if (pauseStatus)
            {
                Debug.Log("[NetworkClient] 앱 백그라운드 진입 - 연결 유지");
            }
            else
            {
                Debug.Log("[NetworkClient] 앱 포그라운드 복귀 - 연결 확인");
                if (isConnected && !IsSocketConnected())
                {
                    Debug.LogWarning("[NetworkClient] 서버 연결 끊어짐 - 재연결 시도");
                    _ = ReconnectAsync();
                }
            }
        }
        
        void OnDestroy()
        {
            DisconnectFromServer();
        }
        
//         // ========================================
//         // 환경 설정
//         // ========================================
        
        /// <summary>
        /// EnvironmentConfig에서 서버 정보 로드
        /// </summary>
        private void InitializeFromEnvironment()
        {
            // 기본값 사용 (NetworkSetup에서 SetServerInfo로 오버라이드 가능)
            // EnvironmentConfig 의존성 제거됨
            
            Debug.Log($"[NetworkClient] TCP 서버 연결 정보: {serverHost}:{serverPort}");
        }
        
        /// <summary>
        /// 서버 연결 정보 설정
        /// </summary>
        public void SetServerInfo(string host, int port)
        {
            serverHost = host;
            serverPort = port;
            Debug.Log($"[NetworkClient] 서버 정보 변경: {serverHost}:{serverPort}");
        }
        
//         // ========================================
//         // 연결 관리
//         // ========================================
        
        /// <summary>
        /// 서버에 연결
        /// </summary>
        public async Task<bool> ConnectToServerAsync()
        {
            if (isConnected)
            {
                Debug.LogWarning("[NetworkClient] 이미 서버에 연결되어 있습니다.");
                return true;
            }
            
            if (isConnecting)
            {
                Debug.LogWarning("[NetworkClient] 연결 시도 중입니다.");
                return false;
            }
            
            isConnecting = true;
            
            try
            {
                Debug.Log($"[NetworkClient] 서버 연결 시도: {serverHost}:{serverPort}");
                
                // 모바일 플랫폼에서 네트워크 상태 확인
                if (Application.isMobilePlatform)
                {
                    if (Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        throw new System.Exception("인터넷 연결이 없습니다. 네트워크를 확인해주세요.");
                    }
                    Debug.Log($"[NetworkClient] 모바일 네트워크 상태: {Application.internetReachability}");
                }
                
                tcpClient = new TcpClient();
                cancellationTokenSource = new CancellationTokenSource();
                
                // 모바일에서 연결 타임아웃 더 길게 설정
                int actualTimeout = Application.isMobilePlatform ? connectionTimeoutMs * 2 : connectionTimeoutMs;
                Debug.Log($"[NetworkClient] 연결 타임아웃: {actualTimeout}ms (모바일: {Application.isMobilePlatform})");
                
                // 연결 타임아웃 설정
                var connectTask = tcpClient.ConnectAsync(serverHost, serverPort);
                var timeoutTask = Task.Delay(actualTimeout);
                
                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    throw new TimeoutException($"서버 연결 타임아웃 ({actualTimeout}ms)");
                }
                
                await connectTask; // 실제 연결 완료 확인
                
                // 스트림 설정
                networkStream = tcpClient.GetStream();
                streamReader = new StreamReader(networkStream, Encoding.UTF8);
                streamWriter = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };
                
                isConnected = true;
                isConnecting = false;
                
                // 수신 스레드 시작
                receiveThread = new Thread(ReceiveMessagesThread) { IsBackground = true };
                receiveThread.Start();
                
                Debug.Log("[NetworkClient] 서버 연결 성공!");
                
                // 메인스레드에서 연결 이벤트 발생
                UnityMainThreadDispatcher.Enqueue(() => OnConnectionChanged?.Invoke(true));
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkClient] 서버 연결 실패: {ex.Message}");
                
                isConnected = false;
                isConnecting = false;
                CleanupConnection();
                
                // 메인스레드에서 에러 이벤트 발생
                UnityMainThreadDispatcher.Enqueue(() => {
                    OnError?.Invoke($"연결 실패: {ex.Message}");
                    OnConnectionChanged?.Invoke(false);
                });
                
                return false;
            }
        }
        
        /// <summary>
        /// 동기 버전 (Unity 메인 스레드용)
        /// </summary>
        public void ConnectToServer()
        {
            _ = ConnectToServerAsync();
        }
        
        /// <summary>
        /// 서버 연결 해제
        /// </summary>
        public void DisconnectFromServer()
        {
            if (!isConnected && tcpClient == null)
            {
                return;
            }
            
            Debug.Log("[NetworkClient] 서버 연결 해제");
            
            isConnected = false;
            cancellationTokenSource?.Cancel();
            
            CleanupConnection();
            
            // 메인스레드에서 연결 해제 이벤트 발생
            UnityMainThreadDispatcher.Enqueue(() => OnConnectionChanged?.Invoke(false));
        }
        
        /// <summary>
        /// 자동 재연결
        /// </summary>
        public async Task<bool> ReconnectAsync()
        {
            Debug.Log("[NetworkClient] 서버 재연결 시도");
            
            DisconnectFromServer();
            await Task.Delay(1000); // 1초 대기
            
            return await ConnectToServerAsync();
        }
        
//         // ========================================
//         // 메시지 송수신
//         // ========================================
        
        /// <summary>
        /// 서버로 메시지 전송
        /// </summary>
        public new bool SendMessage(string message)
        {
            if (!isConnected || streamWriter == null)
            {
                Debug.LogWarning("[NetworkClient] 서버에 연결되지 않음");
                return false;
            }
            
            try
            {
                // UTF-8 BOM 없이 메시지 전송 (서버 파싱 에러 방지)
                byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message + "\n");
                networkStream.Write(messageBytes, 0, messageBytes.Length);
                networkStream.Flush();
                Debug.Log($"[NetworkClient] 메시지 전송: {message}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkClient] 메시지 전송 실패: {ex.Message}");
                
                // 메인스레드에서 에러 이벤트 발생
                UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke($"전송 실패: {ex.Message}"));
                
                // 연결 상태 확인 및 재연결 시도
                if (!IsSocketConnected())
                {
                    _ = ReconnectAsync();
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// 커스텀 프로토콜 메시지 전송 (C++ 클라이언트와 동일한 형식)
        /// </summary>
        public bool SendProtocolMessage(string messageType, params string[] parameters)
        {
            List<string> messageParts = new List<string> { messageType };
            messageParts.AddRange(parameters);
            
            string fullMessage = string.Join(":", messageParts);
            return SendMessage(fullMessage);
        }
        
        /// <summary>
        /// 깨끗한 TCP 메시지 전송 (불필요한 제어 문자 없이)
        /// </summary>
        public bool SendCleanTCPMessage(string messageType, params string[] parameters)
        {
            if (!isConnected || streamWriter == null)
            {
                Debug.LogWarning("[NetworkClient] 서버에 연결되지 않음");
                return false;
            }
            
            try
            {
                List<string> messageParts = new List<string> { messageType };
                messageParts.AddRange(parameters);
                
                string message = string.Join(":", messageParts);
                
                // WriteLine 대신 Write + 단일 \n 사용 (불필요한 \r 제거)
                streamWriter.Write(message + "\n");
                streamWriter.Flush(); // 즉시 전송 보장
                
                Debug.Log($"[NetworkClient] 깨끗한 메시지 전송: {message}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkClient] 깨끗한 메시지 전송 실패: {ex.Message}");
                
                // 메인스레드에서 에러 이벤트 발생
                UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke($"전송 실패: {ex.Message}"));
                
                // 연결 상태 확인 및 재연결 시도
                if (!IsSocketConnected())
                {
                    _ = ReconnectAsync();
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// 메시지 수신 스레드
        /// </summary>
        private void ReceiveMessagesThread()
        {
            try
            {
                while (isConnected && !cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (streamReader != null && tcpClient.Available > 0)
                    {
                        string message = streamReader.ReadLine();
                        if (!string.IsNullOrEmpty(message))
                        {
                            // 메시지를 큐에 추가 (메인 스레드에서 처리)
                            lock (messageLock)
                            {
                                incomingMessages.Enqueue(message);
                            }
                        }
                    }
                    
                    Thread.Sleep(10); // CPU 사용률 최적화
                }
            }
            catch (Exception ex)
            {
                if (isConnected) // 정상 종료가 아닌 경우만 에러 로그
                {
                    Debug.LogError($"[NetworkClient] 메시지 수신 에러: {ex.Message}");
                    
                    // 메인 스레드에서 재연결 시도
                    UnityMainThreadDispatcher.Enqueue(() => 
                    {
                        OnError?.Invoke($"수신 에러: {ex.Message}");
                        _ = ReconnectAsync();
                    });
                }
            }
        }
        
        /// <summary>
        /// 메인 스레드에서 수신된 메시지 처리
        /// </summary>
        private void ProcessIncomingMessages()
        {
            lock (messageLock)
            {
                while (incomingMessages.Count > 0)
                {
                    string message = incomingMessages.Dequeue();
                    Debug.Log($"[NetworkClient] 메시지 수신: {message}");
                    OnMessageReceived?.Invoke(message);
                }
            }
        }
        
//         // ========================================
//         // 연결 상태 확인
//         // ========================================
        
        /// <summary>
        /// 연결 상태 반환
        /// </summary>
        public bool IsConnected()
        {
            return isConnected && IsSocketConnected();
        }
        
        /// <summary>
        /// 소켓 연결 상태 확인
        /// </summary>
        private bool IsSocketConnected()
        {
            if (tcpClient == null || !tcpClient.Connected)
                return false;
            
            try
            {
                // Poll을 사용하여 실제 연결 상태 확인
                return !(tcpClient.Client.Poll(1, System.Net.Sockets.SelectMode.SelectRead) && tcpClient.Client.Available == 0);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 연결 정리
        /// </summary>
        private void CleanupConnection()
        {
            try
            {
                receiveThread?.Join(1000); // 1초 대기
                
                streamWriter?.Close();
                streamReader?.Close();
                networkStream?.Close();
                tcpClient?.Close();
                
                streamWriter = null;
                streamReader = null;
                networkStream = null;
                tcpClient = null;
                
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkClient] 연결 정리 중 오류: {ex.Message}");
            }
        }
        
        // ========================================
        // 게임별 메시지 전송 함수들 (C++ 클라이언트와 동일)
        // ========================================
        
        /// <summary>
        /// JWT 로그인 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendJwtLoginRequest(string token)
        {
            // 서버에서 예상하는 형식: auth:JWT토큰
            return SendCleanTCPMessage("auth", token);
        }
        
        /// <summary>
        /// 로그인 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendLoginRequest(string username, string password)
        {
            // 서버에서 예상하는 형식: auth:username:password
            return SendCleanTCPMessage("auth", username, password);
        }
        
        /// <summary>
        /// 회원가입 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendRegisterRequest(string username, string password)
        {
            // 서버에서 예상하는 형식: register:username:email:password (이메일은 빈값)
            return SendCleanTCPMessage("register", username, "", password);
        }
        
        /// <summary>
        /// 게스트 로그인 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendGuestLoginRequest()
        {
            // 서버에서 예상하는 형식: guest
            return SendCleanTCPMessage("guest");
        }
        
        /// <summary>
        /// 버전 체크 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendVersionCheckRequest(string clientVersion)
        {
            // 서버에서 예상하는 형식: version:check:clientVersion
            return SendCleanTCPMessage("version", "check", clientVersion);
        }
        
        /// <summary>
        /// 사용자 통계 정보 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendGetUserStatsRequest(string username)
        {
            // 서버에서 예상하는 형식: user:stats:username
            return SendCleanTCPMessage("user", "stats", username);
        }
        
        /// <summary>
        /// 로비 입장 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendLobbyEnterRequest()
        {
            // 서버에서 예상하는 형식: lobby:enter
            return SendCleanTCPMessage("lobby", "enter");
        }
        
        /// <summary>
        /// 로비 나가기 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendLobbyLeaveRequest()
        {
            // 서버에서 예상하는 형식: lobby:leave
            return SendCleanTCPMessage("lobby", "leave");
        }
        
        /// <summary>
        /// 로비 사용자 목록 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendLobbyListRequest()
        {
            // 서버에서 예상하는 형식: lobby:list
            return SendCleanTCPMessage("lobby", "list");
        }
        
        /// <summary>
        /// 방 목록 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendRoomListRequest()
        {
            // 서버에서 예상하는 형식: room:list
            return SendCleanTCPMessage("room", "list");
        }
        
        /// <summary>
        /// 방 생성 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendCreateRoomRequest(string roomName, bool isPrivate = false, string password = "")
        {
            // 서버에서 예상하는 형식: room:create:name:private[:password]
            if (isPrivate && !string.IsNullOrEmpty(password))
            {
                return SendCleanTCPMessage("room", "create", roomName, "1", password);
            }
            else
            {
                return SendCleanTCPMessage("room", "create", roomName, isPrivate ? "1" : "0");
            }
        }
        
        /// <summary>
        /// 방 참가 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendJoinRoomRequest(int roomId, string password = "")
        {
            // 서버에서 예상하는 형식: room:join:roomId[:password]
            if (!string.IsNullOrEmpty(password))
            {
                return SendCleanTCPMessage("room", "join", roomId.ToString(), password);
            }
            else
            {
                return SendCleanTCPMessage("room", "join", roomId.ToString());
            }
        }
        
        /// <summary>
        /// 방 나가기 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendLeaveRoomRequest()
        {
            // 서버에서 예상하는 형식: room:leave
            return SendCleanTCPMessage("room", "leave");
        }
        
        /// <summary>
        /// 플레이어 준비 상태 설정 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendPlayerReadyRequest(bool isReady)
        {
            // 서버에서 예상하는 형식: room:ready:0/1
            return SendCleanTCPMessage("room", "ready", isReady ? "1" : "0");
        }
        
        /// <summary>
        /// 게임 시작 요청 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendStartGameRequest()
        {
            // 서버에서 예상하는 형식: room:start
            return SendCleanTCPMessage("room", "start");
        }
        
        /// <summary>
        /// 채팅 메시지 전송 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendChatMessage(string message)
        {
            // 서버에서 예상하는 형식: chat:message
            return SendCleanTCPMessage("chat", message);
        }
        
        /// <summary>
        /// 핑 메시지 전송 (서버 프로토콜에 맞춤)
        /// </summary>
        public bool SendPing()
        {
            // 서버에서 예상하는 형식: ping
            return SendCleanTCPMessage("ping");
        }
        
        /// <summary>
        /// 블록 배치 요청
        /// </summary>
        public bool SendPlaceBlockRequest(MultiModels.BlockPlacement placement)
        {
            return SendProtocolMessage("PLACE_BLOCK_REQUEST", 
                ((int)placement.blockType).ToString(),
                placement.position.x.ToString(),
                placement.position.y.ToString(),
                placement.rotation.ToString(),
                placement.isFlipped.ToString(),
                placement.playerId.ToString()
            );
        }
        /// <summary>
        /// 하트비트 전송
        /// </summary>
        public bool SendHeartbeat()
        {
            return SendProtocolMessage("HEARTBEAT");
        }
        
        /// <summary>
        /// 최대 클리어 스테이지 업데이트
        /// </summary>
        public bool SendUpdateMaxStageRequest(int maxStageCompleted)
        {
            return SendProtocolMessage("UPDATE_MAX_STAGE", maxStageCompleted.ToString());
        }
        
        /// <summary>
        /// 사용자 싱글플레이어 통계 요청
        /// </summary>
        public bool SendGetSinglePlayerStatsRequest()
        {
            return SendProtocolMessage("GET_SINGLE_STATS_REQUEST");
        }
        
        /// <summary>
        /// 범위 스테이지 진행도 요청 (여러 스테이지 한번에)
        /// </summary>
        public bool SendBatchStageProgressRequest(int startStage, int endStage)
        {
            return SendProtocolMessage("BATCH_STAGE_PROGRESS_REQUEST", 
                startStage.ToString(), 
                endStage.ToString()
            );
        }
        
        // ========================================
        // 네트워크 상태 정보 메서드들
        // ========================================
        
        /// <summary>
        /// 서버 정보 반환
        /// </summary>
        public string GetServerInfo()
        {
            if (isConnected)
            {
                return $"서버: {serverHost}:{serverPort} (연결됨)";
            }
            else
            {
                return $"서버: {serverHost}:{serverPort} (연결 안됨)";
            }
        }
        
        /// <summary>
        /// 네트워크 통계 정보 반환
        /// </summary>
        public string GetNetworkStats()
        {
            if (isConnected)
            {
                return $"연결 상태: 활성\n서버: {serverHost}:{serverPort}\n수신 스레드: {(receiveThread?.IsAlive == true ? "실행 중" : "중지됨")}";
            }
            else
            {
                return $"연결 상태: 비활성\n서버: {serverHost}:{serverPort}\n마지막 연결 시도: {(isConnecting ? "진행 중" : "없음")}";
            }
        }
    }
}
