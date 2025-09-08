using UnityEngine;

namespace App.Config
{
    /// <summary>
    /// 환경 모드 관리자 - Unity Editor에서 dev/release 모드를 구분하는 GameObject 컴포넌트
    /// AppPersistent 씬의 GameObject에 할당하여 환경별 서버 설정을 관리합니다.
    /// </summary>
    public class EnvironmentModeManager : MonoBehaviour
    {
        [Header("Environment Settings")]
        [SerializeField] 
        [Tooltip("개발 모드 활성화 여부. true = localhost 서버, false = 배포 환경 서버")]
        private bool isDevelopmentMode = true;
        
        [Header("Development Server Settings (localhost)")]
        [SerializeField] 
        [Tooltip("TCP 게임 서버 포트")]
        private int devTcpPort = 9999;
        
        [SerializeField] 
        [Tooltip("싱글 플레이 API 서버 포트")]
        private int devApiPort = 8080;
        
        [SerializeField] 
        [Tooltip("인증 서버 포트")]
        private int devAuthPort = 9000;
        
        [SerializeField] 
        [Tooltip("웹 서버 포트 (썸네일용)")]
        private int devWebPort = 3000;
        
        [Header("Release Server Settings")]
        [SerializeField] 
        [Tooltip("배포 환경 기본 URL")]
        private string releaseBaseUrl = "https://blokus-online.mooo.com";
        
        [Header("Debug")]
        [SerializeField] 
        [Tooltip("디버그 로그 출력 여부")]
        private bool enableDebugLogs = true;
        
        // 싱글톤 인스턴스
        private static EnvironmentModeManager _instance;
        
        /// <summary>
        /// 싱글톤 인스턴스
        /// </summary>
        public static EnvironmentModeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<EnvironmentModeManager>();
                    
                    if (_instance == null)
                    {
                        Debug.LogWarning("[EnvironmentModeManager] Instance not found in scene. Using default release configuration.");
                        // 기본값으로 release 모드 설정 반환
                        return null;
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// 현재 개발 모드 여부
        /// </summary>
        public bool IsDevelopmentMode => isDevelopmentMode;
        
        /// <summary>
        /// 현재 모드 문자열 (디버그용)
        /// </summary>
        public string CurrentMode => isDevelopmentMode ? "Development" : "Release";
        
        void Awake()
        {
            // 싱글톤 설정
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[EnvironmentModeManager] Duplicate instance found. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[EnvironmentModeManager] Initialized in {CurrentMode} mode");
                LogCurrentConfiguration();
            }
        }
        
        /// <summary>
        /// TCP 서버 호스트 가져오기
        /// </summary>
        public string GetTcpServerHost()
        {
            return isDevelopmentMode ? "localhost" : GetReleaseHost();
        }
        
        /// <summary>
        /// TCP 서버 포트 가져오기
        /// </summary>
        public int GetTcpServerPort()
        {
            return isDevelopmentMode ? devTcpPort : 9999; // Release default port
        }
        
        /// <summary>
        /// API 서버 베이스 URL 가져오기
        /// </summary>
        public string GetApiServerUrl()
        {
            if (isDevelopmentMode)
            {
                return $"http://localhost:{devApiPort}/api";
            }
            else
            {
                return $"{releaseBaseUrl}/single-api/api";
            }
        }
        
        /// <summary>
        /// 인증 서버 URL 가져오기
        /// </summary>
        public string GetAuthServerUrl()
        {
            if (isDevelopmentMode)
            {
                return $"http://localhost:{devAuthPort}";
            }
            else
            {
                return $"{releaseBaseUrl}/oidc";
            }
        }
        
        /// <summary>
        /// 웹 서버 URL 가져오기 (썸네일용)
        /// </summary>
        public string GetWebServerUrl()
        {
            if (isDevelopmentMode)
            {
                return $"http://localhost:{devWebPort}";
            }
            else
            {
                return releaseBaseUrl;
            }
        }
        
        /// <summary>
        /// Release 환경의 호스트 추출
        /// </summary>
        private string GetReleaseHost()
        {
            try
            {
                var uri = new System.Uri(releaseBaseUrl);
                return uri.Host;
            }
            catch
            {
                // URL 파싱 실패시 기본값
                return "blokus-online.mooo.com";
            }
        }
        
        /// <summary>
        /// 현재 설정 정보 로그 출력
        /// </summary>
        public void LogCurrentConfiguration()
        {
            if (!enableDebugLogs) return;
            
            Debug.Log($"[EnvironmentModeManager] === {CurrentMode} Mode Configuration ===");
            Debug.Log($"[EnvironmentModeManager] TCP Server: {GetTcpServerHost()}:{GetTcpServerPort()}");
            Debug.Log($"[EnvironmentModeManager] API Server: {GetApiServerUrl()}");
            Debug.Log($"[EnvironmentModeManager] Auth Server: {GetAuthServerUrl()}");
            Debug.Log($"[EnvironmentModeManager] Web Server: {GetWebServerUrl()}");
            Debug.Log($"[EnvironmentModeManager] ================================================");
        }
        
        /// <summary>
        /// 런타임에 개발 모드 토글 (에디터 전용)
        /// </summary>
        [ContextMenu("Toggle Development Mode")]
        public void ToggleDevelopmentMode()
        {
            #if UNITY_EDITOR
            isDevelopmentMode = !isDevelopmentMode;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[EnvironmentModeManager] Mode switched to: {CurrentMode}");
                LogCurrentConfiguration();
            }
            #else
            Debug.LogWarning("[EnvironmentModeManager] Development mode toggle is only available in Unity Editor");
            #endif
        }
        
        /// <summary>
        /// 개발 모드로 설정 (에디터 전용)
        /// </summary>
        [ContextMenu("Set Development Mode")]
        public void SetDevelopmentMode()
        {
            #if UNITY_EDITOR
            isDevelopmentMode = true;
            LogCurrentConfiguration();
            #endif
        }
        
        /// <summary>
        /// 릴리즈 모드로 설정 (에디터 전용)
        /// </summary>
        [ContextMenu("Set Release Mode")]
        public void SetReleaseMode()
        {
            #if UNITY_EDITOR
            isDevelopmentMode = false;
            LogCurrentConfiguration();
            #endif
        }
        
        /// <summary>
        /// 연결 테스트 (디버그용)
        /// </summary>
        [ContextMenu("Test Current Configuration")]
        public void TestCurrentConfiguration()
        {
            LogCurrentConfiguration();
            
            // 간단한 연결 테스트 시뮬레이션
            Debug.Log($"[EnvironmentModeManager] Testing {CurrentMode} configuration...");
            Debug.Log($"[EnvironmentModeManager] Would connect to TCP: {GetTcpServerHost()}:{GetTcpServerPort()}");
            Debug.Log($"[EnvironmentModeManager] Would use API: {GetApiServerUrl()}");
        }
        
        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        // 에디터 전용: Inspector에서 값 변경시 실시간 로그
        #if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying && enableDebugLogs)
            {
                // Inspector에서 값이 변경될 때마다 로그 출력
                UnityEditor.EditorApplication.delayCall += () => 
                {
                    if (this != null) LogCurrentConfiguration();
                };
            }
        }
        #endif
    }
}