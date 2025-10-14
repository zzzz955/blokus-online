#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace App.UI.Settings.Editor
{
    /// <summary>
    /// AccountContent UI를 자동으로 생성하는 Editor 스크립트
    /// 사용법: Unity Editor 메뉴 → Tools → Build AccountTab UI
    /// </summary>
    public class AccountTabBuilder
    {
        [MenuItem("Tools/Settings/Build AccountTab UI")]
        public static void BuildAccountTabUI()
        {
            // 현재 선택된 GameObject가 AccountContent인지 확인
            GameObject accountContent = Selection.activeGameObject;

            if (accountContent == null || accountContent.name != "AccountContent")
            {
                EditorUtility.DisplayDialog("Error",
                    "AccountContent GameObject를 선택하고 다시 시도하세요.\n\n" +
                    "1. Hierarchy에서 AccountContent 선택\n" +
                    "2. Tools → Settings → Build AccountTab UI 실행",
                    "OK");
                return;
            }

            // 기존 자식 삭제 확인
            if (accountContent.transform.childCount > 0)
            {
                bool confirm = EditorUtility.DisplayDialog("기존 UI 삭제",
                    "AccountContent에 기존 자식이 있습니다. 삭제하고 새로 생성할까요?",
                    "Yes", "No");

                if (!confirm)
                {
                    return;
                }

                // 기존 자식 삭제
                while (accountContent.transform.childCount > 0)
                {
                    GameObject.DestroyImmediate(accountContent.transform.GetChild(0).gameObject);
                }
            }

            // AccountTab 스크립트 확인/추가
            var accountTab = accountContent.GetComponent<AccountTab>();
            if (accountTab == null)
            {
                accountTab = accountContent.AddComponent<AccountTab>();
            }

            // UI 생성
            CreateAccountUI(accountContent.transform, accountTab);

            // 변경사항 저장
            EditorUtility.SetDirty(accountContent);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(accountContent.scene);

            EditorUtility.DisplayDialog("Success", "AccountTab UI가 성공적으로 생성되었습니다!", "OK");

            Debug.Log("[AccountTabBuilder] AccountTab UI 생성 완료");
        }

        private static void CreateAccountUI(Transform parent, AccountTab accountTab)
        {
            // 1. DisplayName Section
            var displayNameSection = CreateSection(parent, "DisplayNameSection", 0f);
            var displayNameText = CreateValueText(displayNameSection, "DisplayNameValue", "Display Name: -");

            // 2. UserID Section
            var userIdSection = CreateSection(parent, "UserIdSection", -60f);
            var userIdText = CreateValueText(userIdSection, "UserIdValue", "User ID: -");

            // 3. Username Section
            var usernameSection = CreateSection(parent, "UsernameSection", -120f);
            var usernameText = CreateValueText(usernameSection, "UsernameValue", "Username: -");

            // 4. Separator
            var separator = CreateSeparator(parent, -200f);

            // 5. Login Time Section
            var loginTimeSection = CreateSection(parent, "LoginTimeSection", -260f);
            var loginTimeText = CreateValueText(loginTimeSection, "LoginTimeValue", "Login Time: -");

            // 6. Session Status Section
            var sessionStatusSection = CreateSection(parent, "SessionStatusSection", -320f);
            var sessionStatusText = CreateValueText(sessionStatusSection, "SessionStatusValue", "Session Status: -");

            // AccountTab 스크립트에 참조 연결
            SerializedObject so = new SerializedObject(accountTab);
            so.FindProperty("displayNameText").objectReferenceValue = displayNameText;
            so.FindProperty("userIdText").objectReferenceValue = userIdText;
            so.FindProperty("usernameText").objectReferenceValue = usernameText;
            so.FindProperty("loginTimeText").objectReferenceValue = loginTimeText;
            so.FindProperty("sessionStatusText").objectReferenceValue = sessionStatusText;
            so.ApplyModifiedProperties();

            Debug.Log("[AccountTabBuilder] 모든 UI 요소 생성 및 연결 완료");
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
