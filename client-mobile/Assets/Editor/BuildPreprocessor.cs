using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        try
        {
            var streamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets");
            if (!Directory.Exists(streamingAssetsPath)) Directory.CreateDirectory(streamingAssetsPath);

            var rootEnvPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.env"));
            var targetEnvPath = Path.Combine(streamingAssetsPath, ".env");

            if (File.Exists(rootEnvPath))
            {
                var allowed = new[] { "WEB_APP_URL", "SERVER_PORT", "NODE_ENV" };
                var filtered = new System.Collections.Generic.List<string>();
                foreach (var line in File.ReadAllLines(rootEnvPath))
                {
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) { filtered.Add(line); continue; }
                    foreach (var v in allowed) if (line.StartsWith(v + "=")) { filtered.Add(line); break; }
                }
                File.WriteAllLines(targetEnvPath, filtered);
            }
            else
            {
                var defaultEnv = "WEB_APP_URL=https://blokus-online.mooo.com\nSERVER_PORT=9999\nNODE_ENV=production";
                File.WriteAllText(targetEnvPath, defaultEnv);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(e.Message);
            throw;
        }
    }
}
