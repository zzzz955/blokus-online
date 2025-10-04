using UnityEditor;
using UnityEngine;
using TMPro;

public class TMPSwapper : EditorWindow
{
    TMP_FontAsset newFont;
    TMP_FontAsset fallbackFont;  // 선택사항
    bool replaceMaterialPreset = true; // 폰트 교체 후 폰트의 기본 머티리얼로 맞출지

    [MenuItem("Tools/TMP/Swap Font In Open Scenes")]
    static void Open() => GetWindow<TMPSwapper>("TMP Font Swapper");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Swap TMP_Text Fonts (Open Scenes)", EditorStyles.boldLabel);
        newFont = (TMP_FontAsset)EditorGUILayout.ObjectField("New Font", newFont, typeof(TMP_FontAsset), false);
        fallbackFont = (TMP_FontAsset)EditorGUILayout.ObjectField("(Optional) Fallback Font", fallbackFont, typeof(TMP_FontAsset), false);
        replaceMaterialPreset = EditorGUILayout.Toggle("Replace Material Preset", replaceMaterialPreset);

        using (new EditorGUI.DisabledScope(newFont == null))
        {
            if (GUILayout.Button("Swap In Open Scenes"))
                SwapInOpenScenes();
        }
    }

    void SwapInOpenScenes()
    {
        var texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        int count = 0;

        Undo.RegisterCompleteObjectUndo(texts, "Swap TMP Fonts");

        foreach (var t in texts)
        {
            // 씬/프리팹 모드 외 기타 에디터 리소스 제외
            if (!IsEditableObject(t)) continue;

            t.font = newFont;

            if (replaceMaterialPreset && newFont != null && newFont.material != null)
                t.fontMaterial = newFont.material; // 기존 커스텀 프리셋을 쓰고 있었다면 이 줄은 끄세요.

            if (fallbackFont != null && !t.font.fallbackFontAssetTable.Contains(fallbackFont))
                t.font.fallbackFontAssetTable.Add(fallbackFont);

            EditorUtility.SetDirty(t);
            count++;
        }

        Debug.Log($"[TMP] Swapped font on {count} TMP_Text objects.");
    }

    bool IsEditableObject(Object o)
    {
        // Project Settings → Editor → Enter Play Mode Options 등에 따라 추가 필터링 가능
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) return true; // 프리팹 모드도 허용
        return AssetDatabase.GetAssetPath(o) == string.Empty; // 씬 오브젝트만
    }
}
