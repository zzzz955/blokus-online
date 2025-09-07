using UnityEngine;
using Features.Multi.Net;

namespace Features.Multi.Net
{
    /// <summary>
    /// 네트워크 매니저 초기 설정 및 서버 정보 구성
    /// AppPersistent 씬에서 자동으로 네트워크 시스템을 설정합니다
    /// </summary>
    public class NetworkSetup : MonoBehaviour
    {
        [Header("서버 연결 설정")]
        [SerializeField] private string serverHost = "blokus-online.mooo.com";
        [SerializeField] private int serverPort = 9999;
        [SerializeField] private bool connectOnStart = false;
        
        [Header("디버그")]
        [SerializeField] private bool enableDebugLogs = true;
        
        void Start()
        {
            SetupNetworkManager();
        }
        
        /// <summary>
        /// NetworkManager 설정 및 초기화
        /// </summary>
        private void SetupNetworkManager()
        {
            // NetworkManager가 없으면 생성
            if (NetworkManager.Instance == null)
            {
                GameObject networkObj = new GameObject("NetworkManager");
                networkObj.AddComponent<NetworkManager>();
                
                if (enableDebugLogs)
                {
                    Debug.Log("[NetworkSetup] NetworkManager 자동 생성됨");
                }
            }
            
            // 서버 정보 설정
            NetworkManager.Instance.SetServerInfo(serverHost, serverPort);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkSetup] 서버 정보 설정: {serverHost}:{serverPort}");
            }
            
            // 연결 상태 이벤트 구독
            NetworkManager.Instance.OnConnectionChanged += OnConnectionChanged;
            NetworkManager.Instance.OnAuthResponse += OnAuthResponse;
            NetworkManager.Instance.OnErrorReceived += OnErrorReceived;
            
            // 자동 연결 (개발용)
            if (connectOnStart)
            {
                NetworkManager.Instance.ConnectToServer();
                Debug.Log("[NetworkSetup] 자동 서버 연결 시작");
            }
        }
        
        /// <summary>
        /// 연결 상태 변경 이벤트 핸들러
        /// </summary>
        private void OnConnectionChanged(bool isConnected)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkSetup] 연결 상태 변경: {(isConnected ? "연결됨" : "끊어짐")}");
            }
        }
        
        /// <summary>
        /// 인증 응답 이벤트 핸들러
        /// </summary>
        private void OnAuthResponse(bool success, string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkSetup] 인증 응답: {(success ? "성공" : "실패")} - {message}");
            }
        }
        
        /// <summary>
        /// 네트워크 에러 이벤트 핸들러
        /// </summary>
        private void OnErrorReceived(string error)
        {
            if (enableDebugLogs)
            {
                Debug.LogError($"[NetworkSetup] 네트워크 에러: {error}");
            }
        }
        
        void OnDestroy()
        {
            // 이벤트 구독 해제
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnConnectionChanged -= OnConnectionChanged;
                NetworkManager.Instance.OnAuthResponse -= OnAuthResponse;
                NetworkManager.Instance.OnErrorReceived -= OnErrorReceived;
            }
        }
        
        // 에디터에서 서버 연결 테스트
        [ContextMenu("서버 연결 테스트")]
        private void TestConnection()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.ConnectToServer();
            }
            else
            {
                Debug.LogError("NetworkManager가 없습니다!");
            }
        }
        
        [ContextMenu("서버 연결 해제")]
        private void TestDisconnection()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.DisconnectFromServer();
            }
        }
        
        [ContextMenu("네트워크 상태 확인")]
        private void CheckNetworkStatus()
        {
            if (NetworkManager.Instance != null)
            {
                Debug.Log($"[NetworkSetup] {NetworkManager.Instance.GetStatusInfo()}");
                Debug.Log($"[NetworkSetup] {NetworkManager.Instance.GetNetworkStats()}");
            }
        }
        
        [ContextMenu("서버 연결 진단 실행")]
        private async void RunConnectionDiagnostics()
        {
            Debug.Log("[NetworkSetup] 서버 연결 진단 시작...");
            
            bool result = await NetworkDiagnostics.DiagnoseConnection(serverHost, serverPort);
            
            if (result)
            {
                Debug.Log("[NetworkSetup] ✅ 서버 연결 진단 성공! 연결 시도를 진행하세요.");
            }
            else
            {
                Debug.LogError("[NetworkSetup] ❌ 서버 연결 진단 실패! 네트워크나 서버 상태를 확인하세요.");
            }
        }
    }
}