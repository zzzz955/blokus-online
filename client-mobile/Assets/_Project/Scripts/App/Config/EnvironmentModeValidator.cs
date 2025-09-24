using UnityEngine;
using App.Config;

namespace App.Config
{
    /// <summary>
    /// Environment Mode 설정이 올바르게 동작하는지 검증하는 테스트 컴포넌트
    /// 개발 중에만 사용하고, 배포 시에는 제거하거나 비활성화해야 합니다.
    /// </summary>
    public class EnvironmentModeValidator : MonoBehaviour
    {
        [Header("Validation Settings")]
        [SerializeField] 
        [Tooltip("시작 시 자동으로 검증을 실행할지 여부")]
        private bool validateOnStart = true;
        
        [SerializeField] 
        [Tooltip("검증 결과를 Console에 출력할지 여부")]
        private bool logValidationResults = true;
        
        [SerializeField] 
        [Tooltip("검증 중 오류가 발견되면 UI 다이얼로그로 알릴지 여부")]
        private bool showErrorDialog = true;
        
        void Start()
        {
            if (validateOnStart)
            {
                ValidateEnvironmentMode();
            }
        }
        
        /// <summary>
        /// 환경 모드 설정 검증 실행
        /// </summary>
        [ContextMenu("환경 모드 설정 검증")]
        public void ValidateEnvironmentMode()
        {
            if (logValidationResults)
            {
                Debug.Log("[EnvironmentModeValidator] 환경 모드 검증 시작...");
            }
            
            bool isValid = true;
            string errorMessage = "";
            
            try
            {
                // 1. EnvironmentModeManager 존재 확인
                var envManager = EnvironmentModeManager.Instance;
                if (envManager == null)
                {
                    errorMessage += " EnvironmentModeManager를 찾을 수 없습니다.\n";
                    isValid = false;
                }
                else
                {
                    if (logValidationResults)
                    {
                        Debug.Log($" EnvironmentModeManager 발견: {envManager.CurrentMode} 모드");
                    }
                    
                    // 2. 모드별 설정 검증
                    ValidateModeConfiguration(envManager);
                }
                
                // 3. EnvironmentConfig 연동 확인
                ValidateEnvironmentConfigIntegration();
                
                if (isValid)
                {
                    if (logValidationResults)
                    {
                        Debug.Log(" [EnvironmentModeValidator] 모든 검증 통과!");
                    }
                }
                else
                {
                    Debug.LogError($" [EnvironmentModeValidator] 검증 실패:\n{errorMessage}");
                    
                    if (showErrorDialog)
                    {
                        // Unity Editor에서만 다이얼로그 표시
                        #if UNITY_EDITOR
                        UnityEditor.EditorUtility.DisplayDialog("환경 모드 검증 실패", errorMessage, "확인");
                        #endif
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($" [EnvironmentModeValidator] 검증 중 예외 발생: {e.Message}");
            }
        }
        
        /// <summary>
        /// 현재 모드별 설정값 검증
        /// </summary>
        private void ValidateModeConfiguration(EnvironmentModeManager envManager)
        {
            if (logValidationResults)
            {
                Debug.Log($"[EnvironmentModeValidator] {envManager.CurrentMode} 모드 설정 확인:");
                Debug.Log($"  TCP Server: {envManager.GetTcpServerHost()}:{envManager.GetTcpServerPort()}");
                Debug.Log($"  API Server: {envManager.GetApiServerUrl()}");
                Debug.Log($"  Auth Server: {envManager.GetAuthServerUrl()}");
                Debug.Log($"  Web Server: {envManager.GetWebServerUrl()}");
            }
            
            if (envManager.IsDevelopmentMode)
            {
                // Dev 모드 검증
                ValidateDevModeSettings(envManager);
            }
            else
            {
                // Release 모드 검증
                ValidateReleaseModeSettings(envManager);
            }
        }
        
        /// <summary>
        /// Dev 모드 설정값 검증
        /// </summary>
        private void ValidateDevModeSettings(EnvironmentModeManager envManager)
        {
            // TCP 서버는 localhost여야 함
            if (envManager.GetTcpServerHost() != "localhost")
            {
                Debug.LogWarning($"⚠️ Dev 모드에서 TCP 서버가 localhost가 아닙니다: {envManager.GetTcpServerHost()}");
            }
            
            // API 서버는 localhost 기반이어야 함
            if (!envManager.GetApiServerUrl().Contains("localhost"))
            {
                Debug.LogWarning($"⚠️ Dev 모드에서 API 서버가 localhost가 아닙니다: {envManager.GetApiServerUrl()}");
            }
            
            // Auth 서버는 localhost 기반이어야 함
            if (!envManager.GetAuthServerUrl().Contains("localhost"))
            {
                Debug.LogWarning($"⚠️ Dev 모드에서 Auth 서버가 localhost가 아닙니다: {envManager.GetAuthServerUrl()}");
            }
            
            if (logValidationResults)
            {
                Debug.Log(" Dev 모드 설정 검증 완료");
            }
        }
        
        /// <summary>
        /// Release 모드 설정값 검증
        /// </summary>
        private void ValidateReleaseModeSettings(EnvironmentModeManager envManager)
        {
            // 배포 환경 서버는 localhost가 아니어야 함
            if (envManager.GetTcpServerHost() == "localhost")
            {
                Debug.LogWarning($"⚠️ Release 모드에서 TCP 서버가 localhost입니다: {envManager.GetTcpServerHost()}");
            }
            
            // HTTPS 사용 여부 확인 (선택적)
            if (!envManager.GetWebServerUrl().StartsWith("https://"))
            {
                Debug.LogWarning($"⚠️ Release 모드에서 웹 서버가 HTTPS가 아닙니다: {envManager.GetWebServerUrl()}");
            }
            
            if (logValidationResults)
            {
                Debug.Log(" Release 모드 설정 검증 완료");
            }
        }
        
        /// <summary>
        /// EnvironmentConfig와의 연동 확인
        /// </summary>
        private void ValidateEnvironmentConfigIntegration()
        {
            if (logValidationResults)
            {
                Debug.Log("[EnvironmentModeValidator] EnvironmentConfig 연동 확인:");
                Debug.Log($"  EnvironmentConfig.TcpServerHost: {EnvironmentConfig.TcpServerHost}");
                Debug.Log($"  EnvironmentConfig.TcpServerPort: {EnvironmentConfig.TcpServerPort}");
                Debug.Log($"  EnvironmentConfig.ApiServerUrl: {EnvironmentConfig.ApiServerUrl}");
                Debug.Log($"  EnvironmentConfig.OidcServerUrl: {EnvironmentConfig.OidcServerUrl}");
                Debug.Log($"  EnvironmentConfig.WebServerUrl: {EnvironmentConfig.WebServerUrl}");
            }
            
            var envManager = EnvironmentModeManager.Instance;
            if (envManager != null)
            {
                // EnvironmentConfig와 EnvironmentModeManager 값이 일치하는지 확인
                bool tcpHostMatch = EnvironmentConfig.TcpServerHost == envManager.GetTcpServerHost();
                bool tcpPortMatch = EnvironmentConfig.TcpServerPort == envManager.GetTcpServerPort();
                bool apiUrlMatch = EnvironmentConfig.ApiServerUrl == envManager.GetApiServerUrl();
                
                if (tcpHostMatch && tcpPortMatch && apiUrlMatch)
                {
                    if (logValidationResults)
                    {
                        Debug.Log(" EnvironmentConfig와 EnvironmentModeManager 설정이 일치합니다.");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠️ EnvironmentConfig와 EnvironmentModeManager 설정이 일치하지 않습니다.");
                    Debug.LogWarning($"  TCP Host Match: {tcpHostMatch}");
                    Debug.LogWarning($"  TCP Port Match: {tcpPortMatch}");
                    Debug.LogWarning($"  API URL Match: {apiUrlMatch}");
                }
            }
        }
        
        /// <summary>
        /// 모드 전환 테스트 (에디터 전용)
        /// </summary>
        [ContextMenu("모드 전환 테스트")]
        public void TestModeToggle()
        {
            #if UNITY_EDITOR
            var envManager = EnvironmentModeManager.Instance;
            if (envManager != null)
            {
                string beforeMode = envManager.CurrentMode;
                
                // 모드 전환
                envManager.ToggleDevelopmentMode();
                
                string afterMode = envManager.CurrentMode;
                
                Debug.Log($"[EnvironmentModeValidator] 모드 전환 테스트: {beforeMode} → {afterMode}");
                
                // 설정이 올바르게 변경되었는지 검증
                ValidateEnvironmentMode();
                
                // 다시 원래 모드로 복구
                envManager.ToggleDevelopmentMode();
                
                Debug.Log($"[EnvironmentModeValidator] 모드 복구: {afterMode} → {envManager.CurrentMode}");
            }
            else
            {
                Debug.LogError("[EnvironmentModeValidator] EnvironmentModeManager를 찾을 수 없어 모드 전환 테스트를 실행할 수 없습니다.");
            }
            #else
            Debug.LogWarning("[EnvironmentModeValidator] 모드 전환 테스트는 Unity Editor에서만 사용할 수 있습니다.");
            #endif
        }
        
        /// <summary>
        /// 전체 설정 요약 출력
        /// </summary>
        [ContextMenu("전체 설정 요약")]
        public void LogConfigSummary()
        {
            Debug.Log("=== Environment Mode Configuration Summary ===");
            
            var envManager = EnvironmentModeManager.Instance;
            if (envManager != null)
            {
                envManager.LogCurrentConfiguration();
            }
            else
            {
                Debug.LogWarning("EnvironmentModeManager가 없습니다.");
            }
            
            Debug.Log("=== EnvironmentConfig Values ===");
            Debug.Log($"TCP: {EnvironmentConfig.TcpServerHost}:{EnvironmentConfig.TcpServerPort}");
            Debug.Log($"API: {EnvironmentConfig.ApiServerUrl}");
            Debug.Log($"Auth: {EnvironmentConfig.OidcServerUrl}");
            Debug.Log($"Web: {EnvironmentConfig.WebServerUrl}");
            Debug.Log($"IsDevelopment: {EnvironmentConfig.IsDevelopment}");
            Debug.Log("===============================================");
        }
    }
}