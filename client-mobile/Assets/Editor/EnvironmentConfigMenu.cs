using UnityEditor;
using UnityEngine;
using System.IO;
using App.Config;

/// <summary>
/// Unity Editor 메뉴에서 환경변수 설정과 EnvironmentModeManager를 관리할 수 있는 툴
/// </summary>
public class EnvironmentConfigMenu
{
    [MenuItem("Tools/Environment Config/Copy .env to StreamingAssets")]
    public static void CopyEnvToStreamingAssets()
    {
        Debug.Log("🔧 수동으로 .env 파일을 StreamingAssets에 복사합니다...");
        
        try
        {
            // StreamingAssets 폴더 생성
            string streamingAssetsPath = Application.dataPath + "/StreamingAssets";
            if (!Directory.Exists(streamingAssetsPath))
            {
                Directory.CreateDirectory(streamingAssetsPath);
            }

            // 루트 .env 파일 경로
            string rootEnvPath = Path.Combine(Application.dataPath, "../../.env");
            rootEnvPath = Path.GetFullPath(rootEnvPath);
            
            string targetEnvPath = Path.Combine(streamingAssetsPath, ".env");

            if (File.Exists(rootEnvPath))
            {
                // 허용된 환경변수만 필터링하여 복사
                string[] allowedVars = {
                    "WEB_APP_URL",
                    "SERVER_PORT",
                    "NODE_ENV"
                };

                var lines = File.ReadAllLines(rootEnvPath);
                var filteredLines = new System.Collections.Generic.List<string>();
                
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    {
                        filteredLines.Add(line);
                        continue;
                    }
                    
                    foreach (string allowedVar in allowedVars)
                    {
                        if (line.StartsWith(allowedVar + "="))
                        {
                            filteredLines.Add(line);
                            Debug.Log($"  📝 복사된 변수: {allowedVar}");
                            break;
                        }
                    }
                }
                
                File.WriteAllLines(targetEnvPath, filteredLines);
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("성공", 
                    $".env 파일이 성공적으로 복사되었습니다.\n" +
                    $"소스: {rootEnvPath}\n" +
                    $"대상: {targetEnvPath}\n" +
                    $"복사된 변수: {filteredLines.Count}개", "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("오류", 
                    $"루트 .env 파일을 찾을 수 없습니다:\n{rootEnvPath}", "확인");
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("오류", $"환경변수 복사 중 오류가 발생했습니다:\n{e.Message}", "확인");
        }
    }

    [MenuItem("Tools/Environment Config/Show Current Config")]
    public static void ShowCurrentConfig()
    {
        Debug.Log("🔧 현재 환경변수 설정:");
        Debug.Log($"   WebServerUrl: {App.Config.EnvironmentConfig.WebServerUrl}");
        Debug.Log($"   ApiServerUrl: {App.Config.EnvironmentConfig.ApiServerUrl}");
        Debug.Log($"   OidcServerUrl: {App.Config.EnvironmentConfig.OidcServerUrl}");
        Debug.Log($"   TcpServerHost: {App.Config.EnvironmentConfig.TcpServerHost}:{App.Config.EnvironmentConfig.TcpServerPort}");
        Debug.Log($"   IsDevelopment: {App.Config.EnvironmentConfig.IsDevelopment}");
        
        var envManager = EnvironmentModeManager.Instance;
        string mode = envManager != null ? envManager.CurrentMode : "EnvironmentModeManager가 없음";
        
        string configSummary = $"현재 Unity 환경변수 설정:\n\n" +
                              $"• 환경 모드 매니저: {mode}\n" +
                              $"• 개발 환경: {App.Config.EnvironmentConfig.IsDevelopment}\n" +
                              $"• 웹 서버: {App.Config.EnvironmentConfig.WebServerUrl}\n" +
                              $"• API 서버: {App.Config.EnvironmentConfig.ApiServerUrl}\n" +
                              $"• OIDC 서버: {App.Config.EnvironmentConfig.OidcServerUrl}\n" +
                              $"• TCP 서버: {App.Config.EnvironmentConfig.TcpServerHost}:{App.Config.EnvironmentConfig.TcpServerPort}";
        
        EditorUtility.DisplayDialog("환경변수 설정", configSummary, "확인");
    }
    
    [MenuItem("Tools/Environment Config/Environment Mode Manager/Create Environment Mode Manager")]
    public static void CreateEnvironmentModeManager()
    {
        // 이미 존재하는지 확인
        var existing = Object.FindObjectOfType<EnvironmentModeManager>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("이미 존재함", 
                $"EnvironmentModeManager가 이미 존재합니다:\n{existing.gameObject.name}", "확인");
            Selection.activeGameObject = existing.gameObject;
            return;
        }
        
        // 새 GameObject 생성
        GameObject envManagerObj = new GameObject("[Environment] Environment Mode Manager");
        envManagerObj.AddComponent<EnvironmentModeManager>();
        
        // Inspector에서 선택
        Selection.activeGameObject = envManagerObj;
        
        Debug.Log("✅ EnvironmentModeManager가 생성되었습니다!");
        EditorUtility.DisplayDialog("생성 완료", 
            "EnvironmentModeManager가 생성되었습니다.\n\n" +
            "Inspector에서 dev/release 모드를 설정할 수 있습니다.", "확인");
    }
    
    [MenuItem("Tools/Environment Config/Environment Mode Manager/Set Development Mode")]
    public static void SetDevelopmentMode()
    {
        var envManager = EnvironmentModeManager.Instance;
        if (envManager != null)
        {
            envManager.SetDevelopmentMode();
            Debug.Log("🔧 Development 모드로 설정됨");
            EditorUtility.DisplayDialog("모드 변경", "Development 모드로 설정되었습니다.", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("오류", 
                "EnvironmentModeManager를 찾을 수 없습니다.\n" +
                "'Create Environment Mode Manager' 메뉴를 먼저 실행하세요.", "확인");
        }
    }
    
    [MenuItem("Tools/Environment Config/Environment Mode Manager/Set Release Mode")]
    public static void SetReleaseMode()
    {
        var envManager = EnvironmentModeManager.Instance;
        if (envManager != null)
        {
            envManager.SetReleaseMode();
            Debug.Log("🔧 Release 모드로 설정됨");
            EditorUtility.DisplayDialog("모드 변경", "Release 모드로 설정되었습니다.", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("오류", 
                "EnvironmentModeManager를 찾을 수 없습니다.\n" +
                "'Create Environment Mode Manager' 메뉴를 먼저 실행하세요.", "확인");
        }
    }
    
    [MenuItem("Tools/Environment Config/Environment Mode Manager/Toggle Mode")]
    public static void ToggleEnvironmentMode()
    {
        var envManager = EnvironmentModeManager.Instance;
        if (envManager != null)
        {
            envManager.ToggleDevelopmentMode();
            Debug.Log($"🔧 모드가 {envManager.CurrentMode}로 변경됨");
            EditorUtility.DisplayDialog("모드 토글", 
                $"모드가 {envManager.CurrentMode}로 변경되었습니다.", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("오류", 
                "EnvironmentModeManager를 찾을 수 없습니다.\n" +
                "'Create Environment Mode Manager' 메뉴를 먼저 실행하세요.", "확인");
        }
    }

    [MenuItem("Tools/Environment Config/Validate StreamingAssets .env")]
    public static void ValidateStreamingAssetsEnv()
    {
        string envPath = Path.Combine(Application.dataPath, "StreamingAssets", ".env");
        
        if (File.Exists(envPath))
        {
            string content = File.ReadAllText(envPath);
            Debug.Log($"📄 StreamingAssets .env 파일 내용:\n{content}");
            EditorUtility.DisplayDialog("StreamingAssets .env", $"파일 경로: {envPath}\n\n내용:\n{content}", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("파일 없음", 
                $"StreamingAssets에 .env 파일이 없습니다.\n{envPath}\n\n" +
                "'Copy .env to StreamingAssets' 메뉴를 먼저 실행하세요.", "확인");
        }
    }
}