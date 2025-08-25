using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Unity 빌드 전처리기 - .env 파일을 StreamingAssets로 복사
/// </summary>
public class BuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("🔧 Build Preprocessor: .env 파일 처리 시작");
        
        try
        {
            // StreamingAssets 폴더 생성
            string streamingAssetsPath = Application.dataPath + "/StreamingAssets";
            if (!Directory.Exists(streamingAssetsPath))
            {
                Directory.CreateDirectory(streamingAssetsPath);
                Debug.Log($"✅ StreamingAssets 폴더 생성: {streamingAssetsPath}");
            }

            // 루트 .env 파일 경로 (client-mobile 상위 폴더)
            string rootEnvPath = Path.Combine(Application.dataPath, "../../.env");
            rootEnvPath = Path.GetFullPath(rootEnvPath); // 절대 경로로 변환
            
            // 대상 .env 파일 경로
            string targetEnvPath = Path.Combine(streamingAssetsPath, ".env");

            if (File.Exists(rootEnvPath))
            {
                // 보안을 위해 민감한 정보를 제외한 환경변수만 복사
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
                        filteredLines.Add(line); // 주석은 유지
                        continue;
                    }
                    
                    foreach (string allowedVar in allowedVars)
                    {
                        if (line.StartsWith(allowedVar + "="))
                        {
                            filteredLines.Add(line);
                            break;
                        }
                    }
                }
                
                File.WriteAllLines(targetEnvPath, filteredLines);
                Debug.Log($"✅ .env 파일 복사 완료: {rootEnvPath} → {targetEnvPath}");
                Debug.Log($"   필터링된 변수 수: {filteredLines.Count}개");
            }
            else
            {
                Debug.LogWarning($"⚠️ 루트 .env 파일을 찾을 수 없습니다: {rootEnvPath}");
                
                // 기본값으로 대체 .env 생성
                string defaultEnv = @"# Unity 빌드용 기본 환경변수
WEB_APP_URL=https://blokus-online.mooo.com
SERVER_PORT=9999
NODE_ENV=production";
                File.WriteAllText(targetEnvPath, defaultEnv);
                Debug.Log("✅ 기본 .env 파일 생성 완료");
            }

            // Unity에서 .meta 파일 생성을 위해 에셋 갱신
            AssetDatabase.Refresh();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Build Preprocessor 오류: {e.Message}");
            throw; // 빌드 실패로 처리
        }
    }
}