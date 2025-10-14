#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using App.Audio;

namespace App.Audio.Editor
{
    /// <summary>
    /// SFXData 자동 설정을 위한 에디터 스크립트
    ///
    /// 사용법:
    /// 1. Unity 에디터에서 메뉴: Tools > Blokus > Setup SFX Data
    /// 2. 또는 SFXData 에셋을 선택하고 Inspector에서 "Auto Setup SFX Clips" 버튼 클릭
    /// </summary>
    public class SFXDataSetupEditor
    {
        // ========================================
        // Menu Item
        // ========================================

        [MenuItem("Tools/Blokus/Setup SFX Data")]
        private static void SetupSFXDataFromMenu()
        {
            // SFXData 에셋 찾기
            string[] guids = AssetDatabase.FindAssets("t:SFXData");

            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "SFXData 없음",
                    "SFXData 에셋을 먼저 생성해주세요.\n(우클릭 > Create > Blokus > Audio > SFX Data)",
                    "확인"
                );
                return;
            }

            // 첫 번째 SFXData 사용
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            SFXData sfxData = AssetDatabase.LoadAssetAtPath<SFXData>(path);

            if (sfxData != null)
            {
                AutoSetupSFXClips(sfxData);
            }
        }

        // ========================================
        // Custom Inspector
        // ========================================

        [CustomEditor(typeof(SFXData))]
        public class SFXDataInspector : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("자동 설정", EditorStyles.boldLabel);

                if (GUILayout.Button("Auto Setup SFX Clips", GUILayout.Height(30)))
                {
                    SFXData sfxData = (SFXData)target;
                    AutoSetupSFXClips(sfxData);
                }

                EditorGUILayout.HelpBox(
                    "이 버튼을 클릭하면 Resources/Audio/SFX 폴더에서 자동으로 오디오 클립을 찾아 할당합니다.\n\n" +
                    "파일명 규칙:\n" +
                    "- button_hovered\n" +
                    "- button_clicked\n" +
                    "- modal_open\n" +
                    "- modal_close\n" +
                    "- turn_change\n" +
                    "- count_down\n" +
                    "- time_out\n" +
                    "- stage_clear\n" +
                    "- stage_fail",
                    MessageType.Info
                );
            }
        }

        // ========================================
        // Auto Setup Logic
        // ========================================

        private static void AutoSetupSFXClips(SFXData sfxData)
        {
            Debug.Log("[SFXDataSetup] 자동 설정 시작...");

            // SFX 폴더 경로들 (우선순위 순)
            string[] searchPaths = new string[]
            {
                "Assets/_Project/Audio/SFX",
                "Assets/_Project/Resources/Audio/SFX",
                "Assets/Resources/Audio/SFX",
                "Assets/Audio/SFX"
            };

            // 파일명 매핑 (SFXType -> 파일명)
            var fileNameMap = new System.Collections.Generic.Dictionary<SFXType, string[]>
            {
                { SFXType.ButtonHover, new[] { "button_hovered", "button_hover", "btn_hover" } },
                { SFXType.ButtonClick, new[] { "button_clicked", "button_click", "btn_click" } },
                { SFXType.ModalOpen, new[] { "modal_open", "modal_opened", "popup_open" } },
                { SFXType.ModalClose, new[] { "modal_close", "modal_closed", "popup_close" } },
                { SFXType.BlockPlace, new[] { "placed_block", "block_place", "place_block" } },
                { SFXType.TurnChange, new[] { "turn_change", "turn_changed", "turn_switch" } },
                { SFXType.CountDown, new[] { "count_down", "countdown", "timer_tick" } },
                { SFXType.TimeOut, new[] { "time_out", "timeout", "timer_end" } },
                { SFXType.StageClear, new[] { "stage_clear", "victory", "win", "success" } },
                { SFXType.StageFail, new[] { "stage_fail", "defeat", "lose", "fail" } }
            };

            // 권장 볼륨 설정
            var volumeMap = new System.Collections.Generic.Dictionary<SFXType, float>
            {
                { SFXType.ButtonHover, 0.4f },
                { SFXType.ButtonClick, 0.7f },
                { SFXType.ModalOpen, 0.6f },
                { SFXType.ModalClose, 0.6f },
                { SFXType.BlockPlace, 0.75f },
                { SFXType.TurnChange, 0.7f },
                { SFXType.CountDown, 0.8f },
                { SFXType.TimeOut, 0.8f },
                { SFXType.StageClear, 0.9f },
                { SFXType.StageFail, 0.9f }
            };

            // 모든 오디오 클립 검색
            string[] allAudioGUIDs = AssetDatabase.FindAssets("t:AudioClip");
            var audioClips = allAudioGUIDs
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => searchPaths.Any(sp => path.StartsWith(sp)))
                .Select(path => new { Path = path, Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path) })
                .Where(x => x.Clip != null)
                .ToList();

            Debug.Log($"[SFXDataSetup] {audioClips.Count}개의 오디오 클립 발견");

            // SFXEntry 배열 초기화 (9개)
            var entries = new System.Collections.Generic.List<SFXData.SFXEntry>();

            int foundCount = 0;

            foreach (var kvp in fileNameMap)
            {
                SFXType type = kvp.Key;
                string[] possibleNames = kvp.Value;

                // 매칭되는 클립 찾기
                AudioClip foundClip = null;
                foreach (var name in possibleNames)
                {
                    foundClip = audioClips
                        .FirstOrDefault(x => x.Clip.name.ToLower().Contains(name.ToLower()))
                        ?.Clip;

                    if (foundClip != null)
                        break;
                }

                // Entry 생성
                var entry = new SFXData.SFXEntry
                {
                    type = type,
                    clip = foundClip,
                    volume = volumeMap.ContainsKey(type) ? volumeMap[type] : 1f,
                    description = GetDescription(type)
                };

                entries.Add(entry);

                if (foundClip != null)
                {
                    Debug.Log($"[SFXDataSetup] ✓ {type}: {foundClip.name}");
                    foundCount++;
                }
                else
                {
                    Debug.LogWarning($"[SFXDataSetup] ✗ {type}: 클립을 찾을 수 없음 (파일명: {string.Join(", ", possibleNames)})");
                }
            }

            // SFXData에 적용
            SerializedObject serializedObject = new SerializedObject(sfxData);
            SerializedProperty entriesProperty = serializedObject.FindProperty("sfxEntries");

            entriesProperty.ClearArray();
            for (int i = 0; i < entries.Count; i++)
            {
                entriesProperty.InsertArrayElementAtIndex(i);
                SerializedProperty entryProp = entriesProperty.GetArrayElementAtIndex(i);

                entryProp.FindPropertyRelative("type").enumValueIndex = (int)entries[i].type;
                entryProp.FindPropertyRelative("clip").objectReferenceValue = entries[i].clip;
                entryProp.FindPropertyRelative("volume").floatValue = entries[i].volume;
                entryProp.FindPropertyRelative("description").stringValue = entries[i].description;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(sfxData);
            AssetDatabase.SaveAssets();

            // 결과 다이얼로그
            string message = $"자동 설정 완료!\n\n발견: {foundCount} / {entries.Count}\n\n";

            if (foundCount < entries.Count)
            {
                message += "일부 클립을 찾지 못했습니다.\n수동으로 할당해주세요.";
            }

            EditorUtility.DisplayDialog(
                "SFX Data 자동 설정",
                message,
                "확인"
            );

            Debug.Log($"[SFXDataSetup] 자동 설정 완료: {foundCount}/{entries.Count}");
        }

        // ========================================
        // Helper Methods
        // ========================================

        private static string GetDescription(SFXType type)
        {
            return type switch
            {
                SFXType.ButtonHover => "버튼 호버링 시 재생 (전역)",
                SFXType.ButtonClick => "버튼 클릭 시 재생 (전역, 모달 버튼 자동 제외)",
                SFXType.ModalOpen => "모달 열릴 때 재생",
                SFXType.ModalClose => "모달 닫힐 때 재생",
                SFXType.BlockPlace => "블록 배치 시 재생 (싱글/멀티)",
                SFXType.TurnChange => "턴 변경 시 재생 (멀티플레이)",
                SFXType.CountDown => "5초 이하 카운트다운 (멀티플레이, 내 턴)",
                SFXType.TimeOut => "시간 초과 시 재생 (멀티플레이, 내 턴)",
                SFXType.StageClear => "스테이지 클리어 / 우승 (1등)",
                SFXType.StageFail => "스테이지 실패 / 패배 (2등 이하)",
                _ => ""
            };
        }
    }
}
#endif
