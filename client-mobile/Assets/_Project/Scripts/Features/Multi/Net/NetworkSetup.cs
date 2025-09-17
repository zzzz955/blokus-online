using UnityEngine;
using Features.Multi.Net;
using App.Config;

namespace Features.Multi.Net
{
    /// <summary>
    /// 네트워크 매니저 초기 설정 및 서버 정보 구성
    /// AppPersistent 씬에서 자동으로 네트워크 시스템을 설정합니다
    /// EnvironmentConfig를 통해 dev/release 모드에 따른 서버 설정을 자동으로 적용합니다
    /// </summary>
    public class NetworkSetup : MonoBehaviour
    {
        [Header("서버 연결 설정")]
        [SerializeField] 
        [Tooltip("EnvironmentConfig를 통해 자동 설정됩니다. dev모드: localhost, release모드: 배포서버")]
        private bool useEnvironmentConfig = true;
        
        [Header("Manual Override (useEnvironmentConfig=false일 때만 사용)")]
        [SerializeField] private string manualServerHost = "blokus-online.mooo.com";
        [SerializeField] private int manualServerPort = 9999;
        
        [Header("연결 옵션")]
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
            
            // 서버 정보 결정
            string serverHost;
            int serverPort;
            
            if (useEnvironmentConfig)
            {
                // EnvironmentConfig를 통한 자동 설정
                serverHost = EnvironmentConfig.TcpServerHost;
                serverPort = EnvironmentConfig.TcpServerPort;
                
                if (enableDebugLogs)
                {
                    var envManager = EnvironmentModeManager.Instance;
                    string mode = envManager != null ? envManager.CurrentMode : "Unknown";
                    Debug.Log($"[NetworkSetup] EnvironmentConfig 사용 - {mode} Mode");
                }
            }
            else
            {
                // 수동 설정 사용
                serverHost = manualServerHost;
                serverPort = manualServerPort;
                
                if (enableDebugLogs)
                {
                    Debug.Log("[NetworkSetup] Manual 설정 사용");
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
            // NetworkClient에서 이미 로그를 출력하므로 여기서는 로그 생략
            // UI에서 에러 처리가 필요한 경우 이벤트 구독자가 처리
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
            
            // 현재 설정된 서버 정보 가져오기
            string serverHost = useEnvironmentConfig ? EnvironmentConfig.TcpServerHost : manualServerHost;
            int serverPort = useEnvironmentConfig ? EnvironmentConfig.TcpServerPort : manualServerPort;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkSetup] 진단 대상: {serverHost}:{serverPort}");
                
                if (useEnvironmentConfig)
                {
                    var envManager = EnvironmentModeManager.Instance;
                    if (envManager != null)
                    {
                        Debug.Log($"[NetworkSetup] 현재 환경 모드: {envManager.CurrentMode}");
                    }
                }
            }
            
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
        
        /// <summary>
        /// 현재 환경 설정 정보 출력 (디버그용)
        /// </summary>
        [ContextMenu("현재 환경 설정 확인")]
        private void LogCurrentEnvironmentConfig()
        {
            var envManager = EnvironmentModeManager.Instance;
            
            if (envManager != null)
            {
                envManager.LogCurrentConfiguration();
            }
            else
            {
                Debug.LogWarning("[NetworkSetup] EnvironmentModeManager를 찾을 수 없습니다. AppPersistent 씬에 설정하세요.");
            }
            
            Debug.Log($"[NetworkSetup] NetworkSetup 설정:");
            Debug.Log($"[NetworkSetup] - Use Environment Config: {useEnvironmentConfig}");
            
            if (useEnvironmentConfig)
            {
                Debug.Log($"[NetworkSetup] - TCP 서버: {EnvironmentConfig.TcpServerHost}:{EnvironmentConfig.TcpServerPort}");
                Debug.Log($"[NetworkSetup] - API 서버: {EnvironmentConfig.ApiServerUrl}");
                Debug.Log($"[NetworkSetup] - 인증 서버: {EnvironmentConfig.OidcServerUrl}");
                Debug.Log($"[NetworkSetup] - 웹 서버: {EnvironmentConfig.WebServerUrl}");
            }
            else
            {
                Debug.Log($"[NetworkSetup] - Manual TCP 서버: {manualServerHost}:{manualServerPort}");
            }
        }
    }
}