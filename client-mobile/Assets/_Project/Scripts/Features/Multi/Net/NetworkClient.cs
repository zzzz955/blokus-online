using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Shared.Models;
using MultiModels = Features.Multi.Models;
using App.UI;
using App.Logging;

namespace Features.Multi.Net
{

    /// <summary>
    /// Unity 블로블로 네트워크 클라이언트
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

                // 안드로이드 파일 로깅 시스템 초기화
#if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.Initialize();
                App.Logging.AndroidLogger.LogInfo("NetworkClient Awake - 안드로이드 릴리즈 빌드");
#endif

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
        /// 포트별 연결 테스트로 방화벽 차단 여부 확인
        /// </summary>
        private async Task TestPortConnectivity(string host)
        {
            try
            {
                SystemMessageManager.ShowToast("🔍 포트 연결 테스트 시작...", Shared.UI.MessagePriority.Info, 2f);

                // 443 포트 테스트 (HTTPS - 일반적으로 열려있음)
                bool port443Success = await TestSinglePort(host, 443, 5000);

                // 9999 포트 테스트 (게임 서버)
                bool port9999Success = await TestSinglePort(host, 9999, 5000);

                // 결과 분석 및 토스트 표시
                if (port443Success && !port9999Success)
                {
                    SystemMessageManager.ShowToast("🚫 포트 9999 차단됨 (방화벽/보안SW)", Shared.UI.MessagePriority.Error, 5f);
                    UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke("[포트테스트] 443:✅ 9999:❌ → 방화벽/보안SW에서 9999 포트 차단"));
                }
                else if (!port443Success && !port9999Success)
                {
                    SystemMessageManager.ShowToast("🌐 전체 네트워크 연결 문제", Shared.UI.MessagePriority.Error, 5f);
                    UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke("[포트테스트] 443:❌ 9999:❌ → 전체 네트워크 연결 문제"));
                }
                else if (port443Success && port9999Success)
                {
                    SystemMessageManager.ShowToast("✅ 포트 연결 정상 (다른 원인)", Shared.UI.MessagePriority.Warning, 3f);
                    UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke("[포트테스트] 443:✅ 9999:✅ → 포트는 정상, 다른 원인 확인 필요"));
                }
                else
                {
                    SystemMessageManager.ShowToast("⚠️ 443 차단됨 (특이한 환경)", Shared.UI.MessagePriority.Warning, 3f);
                    UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke("[포트테스트] 443:❌ 9999:✅ → 특이한 네트워크 환경"));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkClient] 포트 테스트 실패: {ex.Message}");
                SystemMessageManager.ShowToast($"⚠️ 포트 테스트 실패: {ex.GetType().Name}", Shared.UI.MessagePriority.Warning, 3f);
            }
        }

        /// <summary>
        /// 단일 포트 연결 테스트
        /// </summary>
        private async Task<bool> TestSinglePort(string host, int port, int timeoutMs)
        {
            try
            {
                using (var testClient = new TcpClient())
                {
                    var connectTask = testClient.ConnectAsync(host, port);
                    var timeoutTask = Task.Delay(timeoutMs);

                    if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                    {
                        return false; // 타임아웃
                    }

                    await connectTask;
                    Debug.Log($"[NetworkClient] 포트 테스트 성공: {host}:{port}");
                    return true;
                }
            }
            catch (SocketException se)
            {
                Debug.LogWarning($"[NetworkClient] 포트 테스트 실패: {host}:{port} - {se.SocketErrorCode}");
                UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke($"[포트테스트] {host}:{port} - {se.SocketErrorCode}"));
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkClient] 포트 테스트 실패: {host}:{port} - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 서버에 연결
        /// </summary>
        public async Task<bool> ConnectToServerAsync()
        {
#if !UNITY_EDITOR
    if (serverHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        serverHost == "127.0.0.1" || serverHost == "::1")
    {
        // 배포용 기본 호스트로 강제(원하는 값으로 변경)
        serverHost = "blokus-online.mooo.com";
        serverPort = serverPort <= 0 ? 9999 : serverPort;
        Debug.LogWarning($"[NetworkClient] serverHost가 localhost여서 배포 호스트로 강제: {serverHost}:{serverPort}");
    }
#endif

            // 실제 TCP 연결 상태 확인
            if (isConnected && tcpClient != null && tcpClient.Connected)
            {
                Debug.LogWarning("[NetworkClient] 이미 서버에 연결되어 있습니다.");
                return true;
            }

            // 연결 상태 플래그가 true이지만 실제 TCP 연결이 끊어진 경우 정리
            if (isConnected && (tcpClient == null || !tcpClient.Connected))
            {
                Debug.LogWarning("[NetworkClient] 연결 상태 불일치 감지 - 연결 정리 후 재시도");
                CleanupConnection();
            }

            if (isConnecting)
            {
                Debug.LogWarning("[NetworkClient] 연결 시도 중입니다.");
                return false;
            }

            isConnecting = true;

            try
            {
                // 안드로이드 파일 로깅 시스템 초기화 (가장 먼저)
#if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.Initialize();
                App.Logging.AndroidLogger.LogConnection("=== NetworkClient 연결 시도 시작 ===");
                App.Logging.AndroidLogger.LogConnection($"Unity 버전: {Application.unityVersion}");
                App.Logging.AndroidLogger.LogConnection($"플랫폼: {Application.platform}");
                App.Logging.AndroidLogger.LogConnection($"네트워크 도달성: {Application.internetReachability}");
#endif

                Debug.Log($"[NetworkClient] 서버 연결 시도: {serverHost}:{serverPort}");

                // 안드로이드 파일 로깅
#if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogConnection($"서버 연결 시도: {serverHost}:{serverPort}");
                App.Logging.AndroidLogger.LogConnection($"연결 상태 - isConnected: {isConnected}, isConnecting: {isConnecting}");
#endif

                // 모바일 플랫폼에서 네트워크 상태 확인 (소프트 체크)
                if (Application.isMobilePlatform)
                {
                    string reachabilityStatus = Application.internetReachability.ToString();
                    if (Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        Debug.LogWarning("[NetworkClient] Reachability=NotReachable 이지만 연결 시도는 진행합니다 (에뮬레이터/일부 단말 오탐 방지).");
                    }
                    else
                    {
                        Debug.Log($"[NetworkClient] 모바일 네트워크 상태: {Application.internetReachability}");
                    }
                }

                cancellationTokenSource = new CancellationTokenSource();

                // 모바일에서 연결 타임아웃 더 길게 설정
                int actualTimeout = Application.isMobilePlatform ? connectionTimeoutMs * 2 : connectionTimeoutMs;
                Debug.Log($"[NetworkClient] 연결 타임아웃: {actualTimeout}ms (모바일: {Application.isMobilePlatform})");

                // DNS 해결 및 IPv4 우선 연결 시도
                Debug.Log($"[NetworkClient] DNS 해결 시작: {serverHost}");
                SystemMessageManager.ShowToast($"🔍 DNS 해결 중...", Shared.UI.MessagePriority.Info, 2f);

                var addresses = await Dns.GetHostAddressesAsync(serverHost);

                // IPv4를 우선으로 정렬 (AddressFamily.InterNetwork = IPv4)
                var orderedAddresses = addresses.OrderBy(ip => ip.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).ToArray();
                Debug.Log($"[NetworkClient] 해결된 주소 ({orderedAddresses.Length}개): {string.Join(", ", orderedAddresses.Select(ip => ip.ToString()))}");

                Exception lastException = null;
                bool connected = false;

                // 각 주소에 대해 연결 시도
                foreach (var address in orderedAddresses)
                {
                    try
                    {
                        Debug.Log($"[NetworkClient] 연결 시도: {address} ({address.AddressFamily})");

                        tcpClient = new TcpClient(address.AddressFamily);
                        var connectTask = tcpClient.ConnectAsync(address, serverPort);
                        var timeoutTask = Task.Delay(actualTimeout);

                        if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                        {
                            tcpClient?.Close();
                            throw new TimeoutException($"연결 타임아웃: {address}:{serverPort} ({actualTimeout}ms)");
                        }

                        await connectTask; // 실제 연결 완료 확인
                        Debug.Log($"[NetworkClient] 연결 성공: {address}:{serverPort}");

                        // 릴리즈 디버깅: 연결 성공 토스트 표시
                        SystemMessageManager.ShowToast($"✅ 서버 연결 성공!", Shared.UI.MessagePriority.Success, 3f);

                        connected = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        Debug.LogWarning($"[NetworkClient] 연결 실패: {address}:{serverPort} - {ex.Message}");

                        // 릴리즈 디버깅: SocketException 코드 표시로 정확한 원인 파악
                        string errorInfo;
                        if (ex is SocketException se)
                        {
                            errorInfo = $"❌ {(address.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6")} 실패: {se.SocketErrorCode}";
                            UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke($"[TCP] {address}:{serverPort} 실패 - {se.SocketErrorCode}/{se.ErrorCode}"));
                        }
                        else
                        {
                            errorInfo = $"❌ {(address.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6")} 실패: {ex.GetType().Name}";
                            UnityMainThreadDispatcher.Enqueue(() => OnError?.Invoke($"[TCP] {address}:{serverPort} 실패 - {ex.GetType().Name}: {ex.Message}"));
                        }

                        SystemMessageManager.ShowToast(errorInfo, Shared.UI.MessagePriority.Warning, 3f);

                        tcpClient?.Close();
                        tcpClient = null;
                    }
                }

                if (!connected)
                {
                    // 릴리즈 디버깅: 모든 주소 연결 실패 시 포트 테스트 실행
                    SystemMessageManager.ShowToast($"💥 모든 연결 시도 실패", Shared.UI.MessagePriority.Error, 3f);

                    // 포트 연결 테스트로 방화벽 차단 여부 확인
                    await TestPortConnectivity(serverHost);

                    throw new Exception($"모든 주소 연결 실패: {serverHost}:{serverPort} (마지막 오류: {lastException?.Message})");
                }

                // 즉시 메시지 전송을 위한 소켓 최적화 설정
                tcpClient.NoDelay = true; // TCP Nagle 알고리즘 비활성화
                tcpClient.ReceiveBufferSize = 4096; // 수신 버퍼 크기 최적화
                tcpClient.SendBufferSize = 4096; // 전송 버퍼 크기 최적화
                Debug.Log("[NetworkClient] TCP 소켓 최적화 설정 완료 (NoDelay=true, Buffer=4KB)");

                // 스트림 설정
                networkStream = tcpClient.GetStream();
                streamReader = new StreamReader(networkStream, Encoding.UTF8);
                streamWriter = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };

                isConnected = true;
                isConnecting = false;

                // 안드로이드 파일 로깅 - 연결 완료
#if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogConnection($"TCP 연결 및 스트림 설정 완료");
                App.Logging.AndroidLogger.LogConnection($"최종 연결 상태 - isConnected: {isConnected}, TCP Connected: {tcpClient?.Connected}");
                App.Logging.AndroidLogger.LogConnection($"스트림 상태 - Reader: {streamReader != null}, Writer: {streamWriter != null}, NetworkStream: {networkStream != null}");
#endif

                // 수신 스레드 시작
                receiveThread = new Thread(ReceiveMessagesThread) { IsBackground = true };
                receiveThread.Start();

                Debug.Log("[NetworkClient] 서버 연결 성공!");

                // 안드로이드 파일 로깅 - 수신 스레드 시작
#if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogConnection("=== TCP 연결 및 수신 스레드 시작 완료 ===");
                App.Logging.AndroidLogger.LogConnection($"연결된 주소: {tcpClient.Client.RemoteEndPoint}");
                App.Logging.AndroidLogger.LogConnection($"로컬 주소: {tcpClient.Client.LocalEndPoint}");
                App.Logging.AndroidLogger.LogConnection($"스트림 상태 - Reader: {streamReader != null}, Writer: {streamWriter != null}, AutoFlush: {streamWriter?.AutoFlush}");
                App.Logging.AndroidLogger.LogConnection($"TCP 클라이언트 상태 - Connected: {tcpClient?.Connected}, Available: {tcpClient?.Available}");
                App.Logging.AndroidLogger.LogConnection($"수신 스레드 상태 - IsBackground: {receiveThread?.IsBackground}, IsAlive: {receiveThread?.IsAlive}");
#endif

                // 메인스레드에서 연결 이벤트 발생
                UnityMainThreadDispatcher.Enqueue(() => {
#if UNITY_ANDROID && !UNITY_EDITOR
                    App.Logging.AndroidLogger.LogConnection("메인 스레드에서 OnConnectionChanged 이벤트 발생");
#endif
                    OnConnectionChanged?.Invoke(true);
                });

                return true;
            }
            catch (Exception ex)
            {
                // 상세한 예외 정보 로그 출력 (Android 디버깅용)
                Debug.LogError($"[NetworkClient] 서버 연결 실패:");
                Debug.LogError($"[NetworkClient] - 서버: {serverHost}:{serverPort}");
                Debug.LogError($"[NetworkClient] - 예외 타입: {ex.GetType().Name}");
                Debug.LogError($"[NetworkClient] - 메시지: {ex.Message}");
                Debug.LogError($"[NetworkClient] - 스택트레이스: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.LogError($"[NetworkClient] - 내부 예외: {ex.InnerException.Message}");
                }

                // 안드로이드 파일 로깅 - 연결 실패
#if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogError($"=== TCP 연결 실패 ===");
                App.Logging.AndroidLogger.LogError($"서버: {serverHost}:{serverPort}");
                App.Logging.AndroidLogger.LogError($"예외 타입: {ex.GetType().Name}");
                App.Logging.AndroidLogger.LogError($"메시지: {ex.Message}");
                if (ex.InnerException != null)
                {
                    App.Logging.AndroidLogger.LogError($"내부 예외: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
                App.Logging.AndroidLogger.LogError($"연결 상태 정리 - isConnected: {isConnected}, isConnecting: {isConnecting}");
#endif

                // 릴리즈 디버깅: 일반적인 연결 실패 정보 토스트 표시
                string generalError = $"🚫 서버 연결 실패\n예외: {ex.GetType().Name}";
                if (ex.InnerException != null)
                {
                    generalError += $"\n내부 예외: {ex.InnerException.GetType().Name}";
                }
                SystemMessageManager.ShowToast(generalError, Shared.UI.MessagePriority.Error, 5f);

                isConnected = false;
                isConnecting = false;
                CleanupConnection();

                // 메인스레드에서 에러 이벤트 발생
                UnityMainThreadDispatcher.Enqueue(() =>
                {
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

                // 안드로이드 파일 로깅
#if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogNetwork($"메시지 전송 실패 - 연결 상태: isConnected={isConnected}, streamWriter={streamWriter != null}");
#endif
                return false;
            }

            try
            {
                List<string> messageParts = new List<string> { messageType };
                messageParts.AddRange(parameters);

                string message = string.Join(":", messageParts);

                // 안드로이드 파일 로깅 - 전송 전
#if UNITY_ANDROID && !UNITY_EDITOR
                if (messageType == "auth")
                {
                    App.Logging.AndroidLogger.LogNetwork($"TCP 메시지 전송 시도: {messageType}:[JWT토큰] (길이: {message.Length})");
                }
                else
                {
                    App.Logging.AndroidLogger.LogNetwork($"TCP 메시지 전송 시도: {message}");
                }
                App.Logging.AndroidLogger.LogNetwork($"연결 상태 재확인 - TCP Connected: {tcpClient?.Connected}, Stream CanWrite: {networkStream?.CanWrite}");
#endif

                // WriteLine 대신 Write + 단일 \n 사용 (불필요한 \r 제거)
                streamWriter.Write(message + "\n");
                streamWriter.Flush(); // 즉시 전송 보장

                // ping 메시지가 아닌 경우에만 로그 출력
                if (messageType != "ping")
                {
                    Debug.Log($"[NetworkClient] 깨끗한 메시지 전송: {message}");
                }

                // 안드로이드 파일 로깅 - 전송 성공
#if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogNetwork($"TCP 메시지 전송 성공: {messageType}");
                App.Logging.AndroidLogger.LogNetwork($"전송 후 연결 상태 - TCP Connected: {tcpClient?.Connected}");
#endif

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkClient] 깨끗한 메시지 전송 실패: {ex.Message}");

                // 안드로이드 파일 로깅 - 전송 실패
#if UNITY_ANDROID && !UNITY_EDITOR
                App.Logging.AndroidLogger.LogNetwork($"TCP 메시지 전송 실패: {ex.GetType().Name} - {ex.Message}");
                App.Logging.AndroidLogger.LogNetwork($"실패 시 연결 상태 - TCP Connected: {tcpClient?.Connected}, isConnected: {isConnected}");
#endif

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
                    try
                    {
                        // 블로킹 읽기로 즉시 메시지 처리 (Available 체크 제거)
                        if (streamReader != null)
                        {
                            string message = streamReader.ReadLine();
                            if (!string.IsNullOrEmpty(message))
                            {
                                // 안드로이드 파일 로깅 - 메시지 수신
#if UNITY_ANDROID && !UNITY_EDITOR
                                App.Logging.AndroidLogger.LogNetwork($"TCP 메시지 수신: {message}");
                                App.Logging.AndroidLogger.LogConnection($"수신 후 연결 상태 - isConnected: {isConnected}, TCP Connected: {tcpClient?.Connected}");
#endif

                                // 메시지를 큐에 추가 (메인 스레드에서 처리)
                                lock (messageLock)
                                {
                                    incomingMessages.Enqueue(message);
                                }

                                // 즉시 메인 스레드에서 처리하도록 알림
                                UnityMainThreadDispatcher.Enqueue(ProcessIncomingMessages);
                            }
                        }
                    }
                    catch (IOException ioEx)
                    {
                        // 연결이 끊어진 경우
                        if (isConnected)
                        {
                            Debug.LogWarning($"[NetworkClient] 연결 끊어짐: {ioEx.Message}");

                            // 안드로이드 파일 로깅 - 연결 끊어짐
#if UNITY_ANDROID && !UNITY_EDITOR
                            App.Logging.AndroidLogger.LogConnection($"연결 끊어짐 감지: {ioEx.GetType().Name} - {ioEx.Message}");
                            App.Logging.AndroidLogger.LogConnection($"끊어지기 전 상태 - isConnected: {isConnected}, TCP Connected: {tcpClient?.Connected}");
#endif

                            // 연결 상태 정리 및 이벤트 발생
                            CleanupConnection();
                            UnityMainThreadDispatcher.Enqueue(() => OnConnectionChanged?.Invoke(false));

                            break;
                        }
                    }

                    // CPU 사용률 최적화를 위한 짧은 대기 (1ms로 단축)
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                if (isConnected) // 정상 종료가 아닌 경우만 에러 로그
                {
                    Debug.LogError($"[NetworkClient] 메시지 수신 에러: {ex.Message}");

                    // 연결 상태 정리
                    CleanupConnection();

                    // 메인 스레드에서 연결 해제 이벤트 및 에러 이벤트 발생
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnConnectionChanged?.Invoke(false);
                        OnError?.Invoke($"수신 에러: {ex.Message}");
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

                    // 안드로이드 파일 로깅 - 메시지 처리
#if UNITY_ANDROID && !UNITY_EDITOR
                    App.Logging.AndroidLogger.LogNetwork($"메인 스레드에서 메시지 처리: {message}");

                    // 인증 관련 메시지는 상세 로깅
                    if (message.Contains("auth") || message.Contains("login") || message.Contains("success") || message.Contains("error"))
                    {
                        App.Logging.AndroidLogger.LogAuth($"인증 관련 응답 수신: {message}");
                    }
#endif

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
            // 안드로이드 파일 로깅 - JWT 인증 시작
#if UNITY_ANDROID && !UNITY_EDITOR
            App.Logging.AndroidLogger.LogAuth("=== JWT 로그인 요청 시작 ===");
            App.Logging.AndroidLogger.LogAuth($"JWT 토큰 존재 여부: {!string.IsNullOrEmpty(token)}");
            App.Logging.AndroidLogger.LogAuth($"JWT 토큰 길이: {token?.Length ?? 0}");
            App.Logging.AndroidLogger.LogAuth($"연결 상태 확인 - isConnected: {isConnected}");
            App.Logging.AndroidLogger.LogAuth($"TCP 클라이언트 상태: {tcpClient?.Connected}");
            App.Logging.AndroidLogger.LogAuth($"스트림 상태 - Writer: {streamWriter != null}, Reader: {streamReader != null}");

            if (!string.IsNullOrEmpty(token) && token.Length > 50)
            {
                App.Logging.AndroidLogger.LogAuth($"JWT 토큰 시작: {token.Substring(0, 50)}...");
            }
#endif

            // 서버에서 예상하는 형식: auth:JWT토큰
            bool result = SendCleanTCPMessage("auth", token);

            // 안드로이드 파일 로깅 - 결과
#if UNITY_ANDROID && !UNITY_EDITOR
            App.Logging.AndroidLogger.LogAuth($"JWT 전송 결과: {result}");
            App.Logging.AndroidLogger.LogAuth("=== JWT 로그인 요청 완료 ===");
#endif

            return result;
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
        /// 블록 배치 요청 (서버 프로토콜: game:move:blockType:col:row:rotation:flip)
        /// </summary>
        public bool SendPlaceBlockRequest(MultiModels.BlockPlacement placement)
        {
            // 서버가 기대하는 형식: game:move:11:17:0:0:0
            // blockType:col:row:rotation:flip (flip: 0=Normal, 1=Flipped)
            int flipValue = placement.isFlipped ? 1 : 0;

            return SendProtocolMessage("game:move",
                ((int)placement.blockType).ToString(),
                placement.position.x.ToString(),        // x좌표 = col (서버에서 열)
                placement.position.y.ToString(),        // y좌표 = row (서버에서 행)
                placement.rotation.ToString(),
                flipValue.ToString()
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
