#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace App.UI.Settings.Editor
{
    /// <summary>
    /// AboutContent UI를 자동으로 생성하는 Editor 스크립트
    /// 사용법: Unity Editor 메뉴 → Tools → Settings → Build AboutTab UI
    /// </summary>
    public class AboutTabBuilder
    {
        [MenuItem("Tools/Settings/Build AboutTab UI")]
        public static void BuildAboutTabUI()
        {
            // 현재 선택된 GameObject가 AboutContent인지 확인
            GameObject aboutContent = Selection.activeGameObject;

            if (aboutContent == null || aboutContent.name != "AboutContent")
            {
                EditorUtility.DisplayDialog("Error",
                    "AboutContent GameObject를 선택하고 다시 시도하세요.\n\n" +
                    "1. Hierarchy에서 AboutContent 선택\n" +
                    "2. Tools → Settings → Build AboutTab UI 실행",
                    "OK");
                return;
            }

            // 기존 자식 삭제 확인
            if (aboutContent.transform.childCount > 0)
            {
                bool confirm = EditorUtility.DisplayDialog("기존 UI 삭제",
                    "AboutContent에 기존 자식이 있습니다. 삭제하고 새로 생성할까요?",
                    "Yes", "No");

                if (!confirm)
                {
                    return;
                }

                // 기존 자식 삭제
                while (aboutContent.transform.childCount > 0)
                {
                    GameObject.DestroyImmediate(aboutContent.transform.GetChild(0).gameObject);
                }
            }

            // AboutTab 스크립트 확인/추가
            var aboutTab = aboutContent.GetComponent<AboutTab>();
            if (aboutTab == null)
            {
                aboutTab = aboutContent.AddComponent<AboutTab>();
            }

            // UI 생성
            CreateAboutUI(aboutContent.transform, aboutTab);

            // 변경사항 저장
            EditorUtility.SetDirty(aboutContent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(aboutContent.scene);

            EditorUtility.DisplayDialog("Success", "AboutTab UI가 성공적으로 생성되었습니다!", "OK");

            Debug.Log("[AboutTabBuilder] AboutTab UI 생성 완료");
        }

        private static void CreateAboutUI(Transform parent, AboutTab aboutTab)
        {
            // 1. App Name Section
            var appNameSection = CreateSection(parent, "AppNameSection", 0f);
            var appNameText = CreateValueText(appNameSection, "AppNameValue", "App Name: -");

            // 2. App Version Section
            var appVersionSection = CreateSection(parent, "AppVersionSection", -60f);
            var appVersionText = CreateValueText(appVersionSection, "AppVersionValue", "Version: -");

            // 3. Build Number Section
            var buildNumberSection = CreateSection(parent, "BuildNumberSection", -120f);
            var buildNumberText = CreateValueText(buildNumberSection, "BuildNumberValue", "Build: -");

            // 4. Separator
            var separator1 = CreateSeparator(parent, -200f);

            // 5. Unity Version Section
            var unityVersionSection = CreateSection(parent, "UnityVersionSection", -260f);
            var unityVersionText = CreateValueText(unityVersionSection, "UnityVersionValue", "Unity Version: -");

            // 6. Separator
            var separator2 = CreateSeparator(parent, -340f);

            // 7. Device Model Section
            var deviceModelSection = CreateSection(parent, "DeviceModelSection", -400f);
            var deviceModelText = CreateValueText(deviceModelSection, "DeviceModelValue", "Device: -");

            // 8. OS Version Section
            var osVersionSection = CreateSection(parent, "OSVersionSection", -460f);
            var osVersionText = CreateValueText(osVersionSection, "OSVersionValue", "OS: -");

            // AboutTab 스크립트에 참조 연결
            SerializedObject so = new SerializedObject(aboutTab);
            so.FindProperty("appNameText").objectReferenceValue = appNameText;
            so.FindProperty("appVersionText").objectReferenceValue = appVersionText;
            so.FindProperty("buildNumberText").objectReferenceValue = buildNumberText;
            so.FindProperty("unityVersionText").objectReferenceValue = unityVersionText;
            so.FindProperty("deviceModelText").objectReferenceValue = deviceModelText;
            so.FindProperty("osVersionText").objectReferenceValue = osVersionText;
            so.ApplyModifiedProperties();

            Debug.Log("[AboutTabBuilder] 모든 UI 요소 생성 및 연결 완료");
        }

        private static Transform CreateSection(Transform parent, string name, float yPos)
        {
            GameObject section = new GameObject(name);
            section.layer = parent.gameObject.layer;

            RectTransform rt = section.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, yPos);
            rt.sizeDelta = new Vector2(-40, 50);

            return section.transform;
        }

        private static TMP_Text CreateValueText(Transform parent, string name, string text)
        {
            GameObject textObj = new GameObject(name);
            textObj.layer = parent.gameObject.layer;

            RectTransform rt = textObj.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            TMP_Text tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.text = text;
            tmpText.fontSize = 18;
            tmpText.color = Color.white;
            tmpText.alignment = TextAlignmentOptions.Left;

            return tmpText;
        }

        private static GameObject CreateSeparator(Transform parent, float yPos)
        {
            GameObject separator = new GameObject("Separator");
            separator.layer = parent.gameObject.layer;

            RectTransform rt = separator.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, yPos);
            rt.sizeDelta = new Vector2(-40, 2);

            Image img = separator.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.2f); // 반투명 흰색

            return separator;
        }
    }
}
#endif
