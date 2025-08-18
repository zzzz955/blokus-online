// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Net.Sockets;
// using System.Text;
// using System.Threading;
// using System.Threading.Tasks;
// using UnityEngine;
// using BlokusUnity.Common;

// namespace BlokusUnity.Network
// {
//     /// <summary>
//     /// Unity 블로쿠스 네트워크 클라이언트
//     /// C++/Qt 클라이언트와 동일한 TCP 소켓 + 커스텀 문자열 프로토콜 사용
//     /// </summary>
//     public class NetworkClient : MonoBehaviour
//     {
//         [Header("서버 연결 설정")]
//         [SerializeField] private string serverHost = "localhost";
//         [SerializeField] private int serverPort = 9999;
//         [SerializeField] private int connectionTimeoutMs = 5000;
//         // heartbeatIntervalMs 필드 제거됨 (사용되지 않음)
        
//         // 연결 상태
//         private TcpClient tcpClient;
//         private NetworkStream networkStream;
//         private StreamReader streamReader;
//         private StreamWriter streamWriter;
//         private bool isConnected;
//         private bool isConnecting;
        
//         // 스레딩
//         private Thread receiveThread;
//         private CancellationTokenSource cancellationTokenSource;
        
//         // 메시지 큐 (메인 스레드에서 처리)
//         private readonly Queue<string> incomingMessages = new Queue<string>();
//         private readonly object messageLock = new object();
        
//         // 싱글톤 패턴
//         public static NetworkClient Instance { get; private set; }
        
//         // 이벤트
//         public event System.Action<bool> OnConnectionChanged; // 연결 상태 변경
//         public event System.Action<string> OnMessageReceived; // 메시지 수신
//         public event System.Action<string> OnError; // 에러 발생
        
//         void Awake()
//         {
//             // 싱글톤 패턴
//             if (Instance == null)
//             {
//                 Instance = this;
//                 DontDestroyOnLoad(gameObject);
//                 InitializeFromEnvironment();
//             }
//             else
//             {
//                 Destroy(gameObject);
//             }
//         }
        
//         void Start()
//         {
//             // 자동 연결은 하지 않음 - 명시적으로 ConnectToServer() 호출 필요
//         }
        
//         void Update()
//         {
//             // 메인 스레드에서 메시지 처리
//             ProcessIncomingMessages();
//         }
        
//         void OnApplicationPause(bool pauseStatus)
//         {
//             // 앱이 백그라운드로 갈 때 연결 유지 처리
//             if (pauseStatus)
//             {
//                 Debug.Log("앱 백그라운드 진입 - 연결 유지");
//             }
//             else
//             {
//                 Debug.Log("앱 포그라운드 복귀 - 연결 확인");
//                 if (isConnected && !IsSocketConnected())
//                 {
//                     Debug.LogWarning("서버 연결 끊어짐 - 재연결 시도");
//                     _ = ReconnectAsync();
//                 }
//             }
//         }
        
//         void OnDestroy()
//         {
//             DisconnectFromServer();
//         }
        
//         // ========================================
//         // 환경 설정
//         // ========================================
        
//         /// <summary>
//         /// 환경변수에서 서버 정보 로드
//         /// </summary>
//         private void InitializeFromEnvironment()
//         {
//             string envHost = Environment.GetEnvironmentVariable("BLOKUS_SERVER_HOST");
//             string envPort = Environment.GetEnvironmentVariable("BLOKUS_SERVER_PORT");
            
//             if (!string.IsNullOrEmpty(envHost))
//             {
//                 serverHost = envHost;
//                 Debug.Log($"환경변수에서 서버 호스트 설정: {serverHost}");
//             }
            
//             if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out int port))
//             {
//                 serverPort = port;
//                 Debug.Log($"환경변수에서 서버 포트 설정: {serverPort}");
//             }
            
//             Debug.Log($"서버 연결 정보: {serverHost}:{serverPort}");
//         }
        
//         /// <summary>
//         /// 서버 연결 정보 설정
//         /// </summary>
//         public void SetServerInfo(string host, int port)
//         {
//             serverHost = host;
//             serverPort = port;
//             Debug.Log($"서버 정보 변경: {serverHost}:{serverPort}");
//         }
        
//         // ========================================
//         // 연결 관리
//         // ========================================
        
//         /// <summary>
//         /// 서버에 연결
//         /// </summary>
//         public async Task<bool> ConnectToServerAsync()
//         {
//             if (isConnected)
//             {
//                 Debug.LogWarning("이미 서버에 연결되어 있습니다.");
//                 return true;
//             }
            
//             if (isConnecting)
//             {
//                 Debug.LogWarning("연결 시도 중입니다.");
//                 return false;
//             }
            
//             isConnecting = true;
            
//             try
//             {
//                 Debug.Log($"서버 연결 시도: {serverHost}:{serverPort}");
                
//                 tcpClient = new TcpClient();
//                 cancellationTokenSource = new CancellationTokenSource();
                
//                 // 연결 타임아웃 설정
//                 var connectTask = tcpClient.ConnectAsync(serverHost, serverPort);
//                 var timeoutTask = Task.Delay(connectionTimeoutMs);
                
//                 if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
//                 {
//                     throw new TimeoutException($"서버 연결 타임아웃 ({connectionTimeoutMs}ms)");
//                 }
                
//                 await connectTask; // 실제 연결 완료 확인
                
//                 // 스트림 설정
//                 networkStream = tcpClient.GetStream();
//                 streamReader = new StreamReader(networkStream, Encoding.UTF8);
//                 streamWriter = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };
                
//                 isConnected = true;
//                 isConnecting = false;
                
//                 // 수신 스레드 시작
//                 receiveThread = new Thread(ReceiveMessagesThread) { IsBackground = true };
//                 receiveThread.Start();
                
//                 Debug.Log("서버 연결 성공!");
//                 OnConnectionChanged?.Invoke(true);
                
//                 return true;
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"서버 연결 실패: {ex.Message}");
//                 OnError?.Invoke($"연결 실패: {ex.Message}");
                
//                 isConnected = false;
//                 isConnecting = false;
//                 CleanupConnection();
                
//                 OnConnectionChanged?.Invoke(false);
//                 return false;
//             }
//         }
        
//         /// <summary>
//         /// 동기 버전 (Unity 메인 스레드용)
//         /// </summary>
//         public void ConnectToServer()
//         {
//             _ = ConnectToServerAsync();
//         }
        
//         /// <summary>
//         /// 서버 연결 해제
//         /// </summary>
//         public void DisconnectFromServer()
//         {
//             if (!isConnected && tcpClient == null)
//             {
//                 return;
//             }
            
//             Debug.Log("서버 연결 해제");
            
//             isConnected = false;
//             cancellationTokenSource?.Cancel();
            
//             CleanupConnection();
            
//             OnConnectionChanged?.Invoke(false);
//         }
        
//         /// <summary>
//         /// 자동 재연결
//         /// </summary>
//         public async Task<bool> ReconnectAsync()
//         {
//             Debug.Log("서버 재연결 시도");
            
//             DisconnectFromServer();
//             await Task.Delay(1000); // 1초 대기
            
//             return await ConnectToServerAsync();
//         }
        
//         // ========================================
//         // 메시지 송수신
//         // ========================================
        
//         /// <summary>
//         /// 서버로 메시지 전송
//         /// </summary>
//         public new bool SendMessage(string message)
//         {
//             if (!isConnected || streamWriter == null)
//             {
//                 Debug.LogWarning("서버에 연결되지 않음");
//                 return false;
//             }
            
//             try
//             {
//                 streamWriter.WriteLine(message);
//                 Debug.Log($"메시지 전송: {message}");
//                 return true;
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"메시지 전송 실패: {ex.Message}");
//                 OnError?.Invoke($"전송 실패: {ex.Message}");
                
//                 // 연결 상태 확인 및 재연결 시도
//                 if (!IsSocketConnected())
//                 {
//                     _ = ReconnectAsync();
//                 }
                
//                 return false;
//             }
//         }
        
//         /// <summary>
//         /// 커스텀 프로토콜 메시지 전송 (C++ 클라이언트와 동일한 형식)
//         /// </summary>
//         public bool SendProtocolMessage(string messageType, params string[] parameters)
//         {
//             List<string> messageParts = new List<string> { messageType };
//             messageParts.AddRange(parameters);
            
//             string fullMessage = string.Join(":", messageParts);
//             return SendMessage(fullMessage);
//         }
        
//         /// <summary>
//         /// 메시지 수신 스레드
//         /// </summary>
//         private void ReceiveMessagesThread()
//         {
//             try
//             {
//                 while (isConnected && !cancellationTokenSource.Token.IsCancellationRequested)
//                 {
//                     if (streamReader != null && tcpClient.Available > 0)
//                     {
//                         string message = streamReader.ReadLine();
//                         if (!string.IsNullOrEmpty(message))
//                         {
//                             // 메시지를 큐에 추가 (메인 스레드에서 처리)
//                             lock (messageLock)
//                             {
//                                 incomingMessages.Enqueue(message);
//                             }
//                         }
//                     }
                    
//                     Thread.Sleep(10); // CPU 사용률 최적화
//                 }
//             }
//             catch (Exception ex)
//             {
//                 if (isConnected) // 정상 종료가 아닌 경우만 에러 로그
//                 {
//                     Debug.LogError($"메시지 수신 에러: {ex.Message}");
                    
//                     // 메인 스레드에서 재연결 시도
//                     UnityMainThreadDispatcher.Enqueue(() => 
//                     {
//                         OnError?.Invoke($"수신 에러: {ex.Message}");
//                         _ = ReconnectAsync();
//                     });
//                 }
//             }
//         }
        
//         /// <summary>
//         /// 메인 스레드에서 수신된 메시지 처리
//         /// </summary>
//         private void ProcessIncomingMessages()
//         {
//             lock (messageLock)
//             {
//                 while (incomingMessages.Count > 0)
//                 {
//                     string message = incomingMessages.Dequeue();
//                     Debug.Log($"메시지 수신: {message}");
//                     OnMessageReceived?.Invoke(message);
//                 }
//             }
//         }
        
//         // ========================================
//         // 연결 상태 확인
//         // ========================================
        
//         /// <summary>
//         /// 연결 상태 반환
//         /// </summary>
//         public bool IsConnected()
//         {
//             return isConnected && IsSocketConnected();
//         }
        
//         /// <summary>
//         /// 소켓 연결 상태 확인
//         /// </summary>
//         private bool IsSocketConnected()
//         {
//             if (tcpClient == null || !tcpClient.Connected)
//                 return false;
            
//             try
//             {
//                 // Poll을 사용하여 실제 연결 상태 확인
//                 return !(tcpClient.Client.Poll(1, SelectMode.SelectRead) && tcpClient.Client.Available == 0);
//             }
//             catch
//             {
//                 return false;
//             }
//         }
        
//         /// <summary>
//         /// 연결 정리
//         /// </summary>
//         private void CleanupConnection()
//         {
//             try
//             {
//                 receiveThread?.Join(1000); // 1초 대기
                
//                 streamWriter?.Close();
//                 streamReader?.Close();
//                 networkStream?.Close();
//                 tcpClient?.Close();
                
//                 streamWriter = null;
//                 streamReader = null;
//                 networkStream = null;
//                 tcpClient = null;
                
//                 cancellationTokenSource?.Dispose();
//                 cancellationTokenSource = null;
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"연결 정리 중 오류: {ex.Message}");
//             }
//         }
        
//         // ========================================
//         // 게임별 메시지 전송 함수들 (C++ 클라이언트와 동일)
//         // ========================================
        
//         /// <summary>
//         /// 로그인 요청
//         /// </summary>
//         public bool SendLoginRequest(string username, string password)
//         {
//             return SendProtocolMessage("AUTH_REQUEST", "LOGIN", username, password);
//         }
        
//         /// <summary>
//         /// 회원가입 요청
//         /// </summary>
//         public bool SendRegisterRequest(string username, string password)
//         {
//             return SendProtocolMessage("AUTH_REQUEST", "REGISTER", username, password);
//         }
        
//         /// <summary>
//         /// 게스트 로그인 요청
//         /// </summary>
//         public bool SendGuestLoginRequest()
//         {
//             string guestName = $"Guest_{UnityEngine.Random.Range(1000, 9999)}";
//             return SendProtocolMessage("AUTH_REQUEST", "GUEST", guestName);
//         }
        
//         /// <summary>
//         /// 사용자 통계 정보 요청
//         /// </summary>
//         public bool SendGetUserStatsRequest(string username)
//         {
//             return SendProtocolMessage("GET_USER_STATS_REQUEST", username);
//         }
        
//         /// <summary>
//         /// 방 생성 요청
//         /// </summary>
//         public bool SendCreateRoomRequest(string roomName, int maxPlayers = 4)
//         {
//             return SendProtocolMessage("CREATE_ROOM_REQUEST", roomName, maxPlayers.ToString());
//         }
        
//         /// <summary>
//         /// 방 참가 요청
//         /// </summary>
//         public bool SendJoinRoomRequest(int roomId)
//         {
//             return SendProtocolMessage("JOIN_ROOM_REQUEST", roomId.ToString());
//         }
        
//         /// <summary>
//         /// 게임 시작 요청
//         /// </summary>
//         public bool SendStartGameRequest()
//         {
//             return SendProtocolMessage("START_GAME_REQUEST");
//         }
        
//         /// <summary>
//         /// 블록 배치 요청
//         /// </summary>
//         public bool SendPlaceBlockRequest(BlockPlacement placement)
//         {
//             return SendProtocolMessage("PLACE_BLOCK_REQUEST", 
//                 ((int)placement.type).ToString(),
//                 placement.position.row.ToString(),
//                 placement.position.col.ToString(),
//                 ((int)placement.rotation).ToString(),
//                 ((int)placement.flip).ToString(),
//                 ((int)placement.player).ToString()
//             );
//         }
        
//         /// <summary>
//         /// 하트비트 전송
//         /// </summary>
//         public bool SendHeartbeat()
//         {
//             return SendProtocolMessage("HEARTBEAT");
//         }
        
//         /// <summary>
//         /// 최대 클리어 스테이지 업데이트
//         /// </summary>
//         public bool SendUpdateMaxStageRequest(int maxStageCompleted)
//         {
//             return SendProtocolMessage("UPDATE_MAX_STAGE", maxStageCompleted.ToString());
//         }
        
//         /// <summary>
//         /// 사용자 싱글플레이어 통계 요청
//         /// </summary>
//         public bool SendGetSinglePlayerStatsRequest()
//         {
//             return SendProtocolMessage("GET_SINGLE_STATS_REQUEST");
//         }
        
//         /// <summary>
//         /// 범위 스테이지 진행도 요청 (여러 스테이지 한번에)
//         /// </summary>
//         public bool SendBatchStageProgressRequest(int startStage, int endStage)
//         {
//             return SendProtocolMessage("BATCH_STAGE_PROGRESS_REQUEST", 
//                 startStage.ToString(), 
//                 endStage.ToString()
//             );
//         }
//     }
    
//     /// <summary>
//     /// Unity 메인 스레드 디스패처 (간단한 구현)
//     /// </summary>
//     public static class UnityMainThreadDispatcher
//     {
//         private static readonly Queue<System.Action> actionQueue = new Queue<System.Action>();
//         private static readonly object queueLock = new object();
        
//         static UnityMainThreadDispatcher()
//         {
//             // NetworkClient에서 Update 시 처리하도록 함
//         }
        
//         public static void Enqueue(System.Action action)
//         {
//             lock (queueLock)
//             {
//                 actionQueue.Enqueue(action);
//             }
//         }
        
//         public static void ProcessQueue()
//         {
//             lock (queueLock)
//             {
//                 while (actionQueue.Count > 0)
//                 {
//                     try
//                     {
//                         actionQueue.Dequeue().Invoke();
//                     }
//                     catch (Exception ex)
//                     {
//                         Debug.LogError($"메인 스레드 액션 실행 에러: {ex.Message}");
//                     }
//                 }
//             }
//         }
//     }
// }