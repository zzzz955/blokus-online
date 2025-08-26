namespace Features.Multi.Net
{
// using System.Collections;
// using UnityEngine;
// using App.Network;
// using Shared.Models;

// namespace App.Network
// {
//     /// <summary>
//     /// 네트워크 매니저 - NetworkClient와 MessageHandler 통합 관리
//     /// UI 시스템에서 쉽게 접근할 수 있도록 하는 파사드 패턴
//     /// </summary>
//     public class NetworkManager : MonoBehaviour
//     {
//         [Header("자동 연결 설정")]
//         [SerializeField] private bool autoConnectOnStart = false;
//         [SerializeField] private bool autoReconnect = true;
//         [SerializeField] private float reconnectDelay = 3.0f;
//         [SerializeField] private int maxReconnectAttempts = 5;
        
//         [Header("하트비트 설정")]
//         [SerializeField] private bool enableHeartbeat = true;
//         [SerializeField] private float heartbeatInterval = 30.0f;
        
//         // 컴포넌트 참조
//         private NetworkClient networkClient;
//         private MessageHandler messageHandler;
        
//         // 상태 관리
//         private bool isInitialized;
//         private int reconnectAttempts;
//         private Coroutine heartbeatCoroutine;
        
//         // 싱글톤 패턴
//         public static NetworkManager Instance { get; private set; }
        
//         // 이벤트 (MessageHandler 이벤트를 래핑)
//         public event System.Action<bool> OnConnectionChanged
//         {
//             add { if (networkClient != null) networkClient.OnConnectionChanged += value; }
//             remove { if (networkClient != null) networkClient.OnConnectionChanged -= value; }
//         }
        
//         public event System.Action<bool, string> OnAuthResponse
//         {
//             add { if (messageHandler != null) messageHandler.OnAuthResponse += value; }
//             remove { if (messageHandler != null) messageHandler.OnAuthResponse -= value; }
//         }
        
//         public event System.Action<UserInfo> OnMyStatsUpdated
//         {
//             add { if (messageHandler != null) messageHandler.OnMyStatsUpdated += value; }
//             remove { if (messageHandler != null) messageHandler.OnMyStatsUpdated -= value; }
//         }
        
//         public event System.Action<UserInfo> OnUserStatsReceived
//         {
//             add { if (messageHandler != null) messageHandler.OnUserStatsReceived += value; }
//             remove { if (messageHandler != null) messageHandler.OnUserStatsReceived -= value; }
//         }
        
//         void Awake()
//         {
//             // 싱글톤 패턴
//             if (Instance == null)
//             {
//                 Instance = this;
//                 DontDestroyOnLoad(gameObject);
//                 InitializeComponents();
//             }
//             else
//             {
//                 Destroy(gameObject);
//             }
//         }
        
//         void Start()
//         {
//             if (autoConnectOnStart && isInitialized)
//             {
//                 ConnectToServer();
//             }
//         }
        
//         void OnDestroy()
//         {
//             StopHeartbeat();
//         }
        
//         // ========================================
//         // 초기화
//         // ========================================
        
//         /// <summary>
//         /// 네트워크 컴포넌트들 초기화
//         /// </summary>
//         private void InitializeComponents()
//         {
//             // NetworkClient 컴포넌트 확인/추가
//             networkClient = GetComponent<NetworkClient>();
//             if (networkClient == null)
//             {
//                 networkClient = gameObject.AddComponent<NetworkClient>();
//             }
            
//             // MessageHandler 컴포넌트 확인/추가
//             messageHandler = GetComponent<MessageHandler>();
//             if (messageHandler == null)
//             {
//                 messageHandler = gameObject.AddComponent<MessageHandler>();
//             }
            
//             // 연결 상태 변경 이벤트 구독
//             networkClient.OnConnectionChanged += OnConnectionStatusChanged;
//             networkClient.OnError += OnNetworkError;
            
//             isInitialized = true;
//             Debug.Log("NetworkManager 초기화 완료");
//         }
        
//         // ========================================
//         // 연결 관리
//         // ========================================
        
//         /// <summary>
//         /// 서버 연결
//         /// </summary>
//         public void ConnectToServer()
//         {
//             if (!isInitialized)
//             {
//                 Debug.LogError("NetworkManager가 초기화되지 않았습니다.");
//                 return;
//             }
            
//             if (IsConnected())
//             {
//                 Debug.LogWarning("이미 서버에 연결되어 있습니다.");
//                 return;
//             }
            
//             Debug.Log("서버 연결 시도...");
//             networkClient.ConnectToServer();
//         }
        
//         /// <summary>
//         /// 서버 연결 해제
//         /// </summary>
//         public void DisconnectFromServer()
//         {
//             if (!isInitialized)
//                 return;
            
//             StopHeartbeat();
//             networkClient?.DisconnectFromServer();
            
//             Debug.Log("서버 연결 해제됨");
//         }
        
//         /// <summary>
//         /// 연결 상태 확인
//         /// </summary>
//         public bool IsConnected()
//         {
//             return isInitialized && networkClient != null && networkClient.IsConnected();
//         }
        
//         /// <summary>
//         /// 서버 정보 설정
//         /// </summary>
//         public void SetServerInfo(string host, int port)
//         {
//             if (networkClient != null)
//             {
//                 networkClient.SetServerInfo(host, port);
//             }
//         }
        
//         // ========================================
//         // 메시지 전송 (편의 함수들)
//         // ========================================
        
//         /// <summary>
//         /// 로그인 요청
//         /// </summary>
//         public bool Login(string username, string password)
//         {
//             if (!IsConnected())
//             {
//                 Debug.LogWarning("서버에 연결되지 않았습니다.");
//                 return false;
//             }
            
//             return networkClient.SendLoginRequest(username, password);
//         }
        
//         /// <summary>
//         /// 회원가입 요청
//         /// </summary>
//         public bool Register(string username, string password)
//         {
//             if (!IsConnected())
//             {
//                 Debug.LogWarning("서버에 연결되지 않았습니다.");
//                 return false;
//             }
            
//             return networkClient.SendRegisterRequest(username, password);
//         }
        
//         /// <summary>
//         /// 게스트 로그인
//         /// </summary>
//         public bool GuestLogin()
//         {
//             if (!IsConnected())
//             {
//                 Debug.LogWarning("서버에 연결되지 않았습니다.");
//                 return false;
//             }
            
//             return networkClient.SendGuestLoginRequest();
//         }
        
//         /// <summary>
//         /// 사용자 통계 요청
//         /// </summary>
//         public bool RequestUserStats(string username)
//         {
//             if (!IsConnected())
//             {
//                 Debug.LogWarning("서버에 연결되지 않았습니다.");
//                 return false;
//             }
            
//             return networkClient.SendGetUserStatsRequest(username);
//         }
        
//         /// <summary>
//         /// 방 생성 요청
//         /// </summary>
//         public bool CreateRoom(string roomName, int maxPlayers = 4)
//         {
//             if (!IsConnected())
//             {
//                 Debug.LogWarning("서버에 연결되지 않았습니다.");
//                 return false;
//             }
            
//             return networkClient.SendCreateRoomRequest(roomName, maxPlayers);
//         }
        
//         /// <summary>
//         /// 방 참가 요청
//         /// </summary>
//         public bool JoinRoom(int roomId)
//         {
//             if (!IsConnected())
//             {
//                 Debug.LogWarning("서버에 연결되지 않았습니다.");
//                 return false;
//             }
            
//             return networkClient.SendJoinRoomRequest(roomId);
//         }
        
//         /// <summary>
//         /// 게임 시작 요청
//         /// </summary>
//         public bool StartGame()
//         {
//             if (!IsConnected())
//             {
//                 Debug.LogWarning("서버에 연결되지 않았습니다.");
//                 return false;
//             }
            
//             return networkClient.SendStartGameRequest();
//         }
        
//         /// <summary>
//         /// 블록 배치 요청
//         /// </summary>
//         public bool PlaceBlock(BlockPlacement placement)
//         {
//             if (!IsConnected())
//             {
//                 Debug.LogWarning("서버에 연결되지 않았습니다.");
//                 return false;
//             }
            
//             return networkClient.SendPlaceBlockRequest(placement);
//         }
        
//         // ========================================
//         // 이벤트 핸들러들
//         // ========================================
        
//         /// <summary>
//         /// 연결 상태 변경 처리
//         /// </summary>
//         private void OnConnectionStatusChanged(bool isConnected)
//         {
//             Debug.Log($"연결 상태 변경: {(isConnected ? "연결됨" : "연결 해제됨")}");
            
//             if (isConnected)
//             {
//                 // 연결 성공
//                 reconnectAttempts = 0;
//                 StartHeartbeat();
//             }
//             else
//             {
//                 // 연결 해제
//                 StopHeartbeat();
                
//                 // 자동 재연결 시도
//                 if (autoReconnect && reconnectAttempts < maxReconnectAttempts)
//                 {
//                     StartCoroutine(AttemptReconnect());
//                 }
//             }
//         }
        
//         /// <summary>
//         /// 네트워크 에러 처리
//         /// </summary>
//         private void OnNetworkError(string errorMessage)
//         {
//             Debug.LogError($"네트워크 에러: {errorMessage}");
            
//             // UI에서 에러 처리 필요시 이벤트 발생
//             // OnErrorReceived?.Invoke(errorMessage);
//         }
        
//         // ========================================
//         // 자동 재연결
//         // ========================================
        
//         /// <summary>
//         /// 재연결 시도
//         /// </summary>
//         private IEnumerator AttemptReconnect()
//         {
//             reconnectAttempts++;
//             Debug.Log($"재연결 시도 {reconnectAttempts}/{maxReconnectAttempts} ({reconnectDelay}초 후)");
            
//             yield return new WaitForSeconds(reconnectDelay);
            
//             if (!IsConnected())
//             {
//                 ConnectToServer();
//             }
//         }
        
//         // ========================================
//         // 하트비트 시스템
//         // ========================================
        
//         /// <summary>
//         /// 하트비트 시작
//         /// </summary>
//         private void StartHeartbeat()
//         {
//             if (!enableHeartbeat)
//                 return;
            
//             StopHeartbeat(); // 기존 코루틴 중지
//             heartbeatCoroutine = StartCoroutine(HeartbeatCoroutine());
//             Debug.Log("하트비트 시작");
//         }
        
//         /// <summary>
//         /// 하트비트 중지
//         /// </summary>
//         private void StopHeartbeat()
//         {
//             if (heartbeatCoroutine != null)
//             {
//                 StopCoroutine(heartbeatCoroutine);
//                 heartbeatCoroutine = null;
//                 Debug.Log("하트비트 중지");
//             }
//         }
        
//         /// <summary>
//         /// 하트비트 코루틴
//         /// </summary>
//         private IEnumerator HeartbeatCoroutine()
//         {
//             while (IsConnected())
//             {
//                 yield return new WaitForSeconds(heartbeatInterval);
                
//                 if (IsConnected())
//                 {
//                     networkClient.SendHeartbeat();
//                     Debug.Log("하트비트 전송");
//                 }
//             }
//         }
        
//         // ========================================
//         // 디버그 및 상태 확인
//         // ========================================
        
//         /// <summary>
//         /// 네트워크 상태 정보 반환
//         /// </summary>
//         public string GetStatusInfo()
//         {
//             if (!isInitialized)
//                 return "초기화되지 않음";
            
//             string status = IsConnected() ? "연결됨" : "연결 안됨";
//             string reconnectInfo = autoReconnect ? $" (재연결: {reconnectAttempts}/{maxReconnectAttempts})" : "";
            
//             return $"상태: {status}{reconnectInfo}";
//         }
        
//         /// <summary>
//         /// 수동 하트비트 전송 (디버그용)
//         /// </summary>
//         public void SendManualHeartbeat()
//         {
//             if (IsConnected())
//             {
//                 networkClient.SendHeartbeat();
//                 Debug.Log("수동 하트비트 전송");
//             }
//             else
//             {
//                 Debug.LogWarning("연결되지 않아 하트비트를 전송할 수 없습니다.");
//             }
//         }
        
//         /// <summary>
//         /// 설정 업데이트
//         /// </summary>
//         public void UpdateSettings(bool autoReconnectEnabled, float reconnectDelaySeconds, int maxAttempts)
//         {
//             autoReconnect = autoReconnectEnabled;
//             reconnectDelay = reconnectDelaySeconds;
//             maxReconnectAttempts = maxAttempts;
            
//             Debug.Log($"네트워크 설정 업데이트: 자동재연결={autoReconnect}, 딜레이={reconnectDelay}s, 최대시도={maxReconnectAttempts}");
//         }
        
//         /// <summary>
//         /// 하트비트 설정 업데이트
//         /// </summary>
//         public void UpdateHeartbeatSettings(bool enabled, float intervalSeconds)
//         {
//             bool wasEnabled = enableHeartbeat;
//             enableHeartbeat = enabled;
//             heartbeatInterval = intervalSeconds;
            
//             if (enabled && !wasEnabled && IsConnected())
//             {
//                 StartHeartbeat();
//             }
//             else if (!enabled && wasEnabled)
//             {
//                 StopHeartbeat();
//             }
            
//             Debug.Log($"하트비트 설정 업데이트: 활성화={enableHeartbeat}, 간격={heartbeatInterval}s");
//         }
//     }
// }
}
