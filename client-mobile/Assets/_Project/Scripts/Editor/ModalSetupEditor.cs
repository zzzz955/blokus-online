#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Linq;
using App.Audio;

namespace App.Audio.Editor
{
    /// <summary>
    /// 모달에 ModalSoundPlayer 자동 추가 에디터 스크립트
    ///
    /// 사용법:
    /// Unity 에디터 메뉴: Tools > Blokus > Setup Modal Sounds
    /// </summary>
    public class ModalSetupEditor
    {
        [MenuItem("Tools/Blokus/Setup Modal Sounds")]
        private static void SetupModalSounds()
        {
            // 현재 열린 씬에서 모달 GameObject 찾기
            var allGameObjects = GameObject.FindObjectsOfType<GameObject>(true);

            // "Modal"이 이름에 포함된 GameObject 필터링
            var modalCandidates = allGameObjects
                .Where(go => go.name.Contains("Modal") || go.name.Contains("Popup"))
                .ToList();

            if (modalCandidates.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "모달 없음",
                    "현재 씬에서 'Modal' 또는 'Popup'이 이름에 포함된 GameObject를 찾을 수 없습니다.",
                    "확인"
                );
                return;
            }

            int addedCount = 0;
            int skippedCount = 0;
            System.Text.StringBuilder report = new System.Text.StringBuilder();

            foreach (var modal in modalCandidates)
            {
                // 이미 ModalSoundPlayer가 있는지 확인
                if (modal.GetComponent<ModalSoundPlayer>() != null)
                {
                    skippedCount++;
                    report.AppendLine($"⊖ {modal.name} (이미 설정됨)");
                    continue;
                }

                // ModalSoundPlayer 추가
                var modalSoundPlayer = modal.AddComponent<ModalSoundPlayer>();

                // 기본 설정
                SerializedObject so = new SerializedObject(modalSoundPlayer);
                so.FindProperty("playOnEnable").boolValue = true;
                so.FindProperty("playOnDisable").boolValue = true;
                so.FindProperty("openDelay").floatValue = 0f;
                so.FindProperty("closeDelay").floatValue = 0f;
                so.FindProperty("verboseLog").boolValue = false;
                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(modal);
                addedCount++;
                report.AppendLine($"✓ {modal.name}");
            }

            // 씬 저장
            if (addedCount > 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
                );
            }

            // 결과 다이얼로그
            string message = $"모달 사운드 설정 완료!\n\n";
            message += $"추가됨: {addedCount}개\n";
            message += $"건너뜀: {skippedCount}개\n\n";
            message += "=== 세부 내역 ===\n";
            message += report.ToString();

            EditorUtility.DisplayDialog(
                "모달 사운드 설정",
                message,
                "확인"
            );

            Debug.Log($"[ModalSetup] 완료: 추가 {addedCount}개, 건너뜀 {skippedCount}개");
            Debug.Log($"[ModalSetup]\n{report}");
        }

        // ========================================
        // 추가 메뉴: 특정 GameObject에만 적용
        // ========================================

        [MenuItem("GameObject/Blokus/Add Modal Sound Player", false, 10)]
        private static void AddModalSoundPlayerToSelected()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "선택 없음",
                    "GameObject를 먼저 선택해주세요.",
                    "확인"
                );
                return;
            }

            // 이미 있는지 확인
            if (selected.GetComponent<ModalSoundPlayer>() != null)
            {
                EditorUtility.DisplayDialog(
                    "이미 존재",
                    $"{selected.name}에는 이미 ModalSoundPlayer가 있습니다.",
                    "확인"
                );
                return;
            }

            // 추가
            var modalSoundPlayer = selected.AddComponent<ModalSoundPlayer>();

            // 기본 설정
            SerializedObject so = new SerializedObject(modalSoundPlayer);
            so.FindProperty("playOnEnable").boolValue = true;
            so.FindProperty("playOnDisable").boolValue = true;
            so.FindProperty("openDelay").floatValue = 0f;
            so.FindProperty("closeDelay").floatValue = 0f;
            so.FindProperty("verboseLog").boolValue = false;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(selected);

            Debug.Log($"[ModalSetup] ModalSoundPlayer 추가됨: {selected.name}");

            EditorUtility.DisplayDialog(
                "추가 완료",
                $"{selected.name}에 ModalSoundPlayer가 추가되었습니다.",
                "확인"
            );
        }

        // ========================================
        // 추가 메뉴: 제거
        // ========================================

        [MenuItem("GameObject/Blokus/Remove Modal Sound Player", false, 11)]
        private static void RemoveModalSoundPlayerFromSelected()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "선택 없음",
                    "GameObject를 먼저 선택해주세요.",
                    "확인"
                );
                return;
            }

            var modalSoundPlayer = selected.GetComponent<ModalSoundPlayer>();
            if (modalSoundPlayer == null)
            {
                EditorUtility.DisplayDialog(
                    "컴포넌트 없음",
                    $"{selected.name}에는 ModalSoundPlayer가 없습니다.",
                    "확인"
                );
                return;
            }

            UnityEngine.Object.DestroyImmediate(modalSoundPlayer);
            EditorUtility.SetDirty(selected);

            Debug.Log($"[ModalSetup] ModalSoundPlayer 제거됨: {selected.name}");

            EditorUtility.DisplayDialog(
                "제거 완료",
                $"{selected.name}에서 ModalSoundPlayer가 제거되었습니다.",
                "확인"
            );
        }
    }
}
#endif
