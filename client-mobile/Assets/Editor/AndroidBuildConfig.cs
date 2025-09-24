#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;

public static class AndroidBuildConfig
{
    [MenuItem("Blokus/Android/Apply Recommended Settings")]
    public static void Apply()
    {
        // 1) Android로 빌드타겟 전환
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        // 2) 스크립팅 백엔드: IL2CPP
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

        // 3) 아키텍처: ARM64 (+ ARMv7 옵션)
        //    ARM64 필수 (S24 Ultra 등 64-bit only)
        try
        {
            var archType = typeof(PlayerSettings.Android).Assembly.GetType("UnityEditor.PlayerSettings+AndroidArchitecture");
            var prop = typeof(PlayerSettings.Android).GetProperty("targetArchitectures");
            if (archType != null && prop != null)
            {
                var arm64 = Enum.Parse(archType, "ARM64");
                object value = arm64;

                // 필요 시 ARMv7도 함께 포함하려면 아래 줄 주석 해제
                var armv7 = Enum.Parse(archType, "ARMv7");
                value = (int)arm64 | (int)armv7;

                prop.SetValue(null, value, null);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("TargetArchitectures 설정 실패(무시 가능): " + e.Message);
        }

        // 4) min/target SDK
        //    35가 API에 있으면 35, 없으면 Highest/Auto
        try
        {
            var enumType = typeof(PlayerSettings.Android).Assembly.GetType("UnityEditor.AndroidSdkVersions");
            var minProp  = typeof(PlayerSettings.Android).GetProperty("minSdkVersion");
            var tgtProp  = typeof(PlayerSettings.Android).GetProperty("targetSdkVersion");

            var min26 = Enum.Parse(enumType, "AndroidApiLevel26");
            minProp.SetValue(null, min26, null);

            object target = null;
            // 우선 35 시도
            var names = Enum.GetNames(enumType);
            var name35 = names.FirstOrDefault(n => n.Contains("35") || n.Contains("Android15"));
            if (!string.IsNullOrEmpty(name35))
                target = Enum.Parse(enumType, name35);
            else
            {
                // 34 또는 HighestInstalled/Auto 로 대체
                var name34 = names.FirstOrDefault(n => n.Contains("34") || n.Contains("Android14"));
                if (!string.IsNullOrEmpty(name34)) target = Enum.Parse(enumType, name34);
                else
                {
                    var highest = names.FirstOrDefault(n => n.Contains("Highest") || n.Contains("Auto"));
                    target = Enum.Parse(enumType, highest);
                }
            }
            tgtProp.SetValue(null, target, null);
        }
        catch (Exception e)
        {
            Debug.LogWarning("SDK Version 설정 실패(무시 가능): " + e.Message);
        }

        // 5) 커스텀 Gradle 템플릿 활성 (에디터 버전에 따라 없을 수 있어 try-catch)
        TryEnableTemplateToggle("useCustomGradlePropertiesTemplate", true);
        TryEnableTemplateToggle("useCustomMainGradleTemplate", true);
        TryEnableTemplateToggle("useCustomLauncherGradleTemplate", true);
        TryEnableTemplateToggle("useCustomBaseGradleTemplate", true);

        Debug.Log(" Blokus Android 권장 설정 적용 완료");
    }

    private static void TryEnableTemplateToggle(string propName, bool on)
    {
        try
        {
            var p = typeof(PlayerSettings.Android).GetProperty(propName, BindingFlags.Public | BindingFlags.Static);
            if (p != null && p.CanWrite)
            {
                p.SetValue(null, on, null);
                Debug.Log($" - {propName} = {on}");
            }
        }
        catch { /* 일부 버전에 없음 */ }
    }
}
#endif
