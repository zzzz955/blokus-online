using UnityEngine;

namespace App.Audio
{
    /// <summary>
    /// SFX 오디오 클립 설정용 ScriptableObject
    /// Inspector에서 SFX 타입별 AudioClip 할당
    /// </summary>
    [CreateAssetMenu(fileName = "SFXData", menuName = "Blokus/Audio/SFX Data", order = 2)]
    public class SFXData : ScriptableObject
    {
        [System.Serializable]
        public class SFXEntry
        {
            [Header("SFX 설정")]
            public SFXType type;
            public AudioClip clip;

            [Header("재생 설정")]
            [Range(0f, 1f)] public float volume = 1f;

            [Header("설명 (선택사항)")]
            [TextArea(2, 3)]
            public string description;
        }

        [Header("SFX 목록")]
        [Tooltip("각 SFX 타입에 AudioClip 할당")]
        public SFXEntry[] sfxEntries;

        // ========================================
        // Public API
        // ========================================

        /// <summary>
        /// SFXType에 해당하는 SFXEntry 찾기
        /// </summary>
        public SFXEntry GetEntry(SFXType type)
        {
            if (sfxEntries == null)
                return null;

            foreach (var entry in sfxEntries)
            {
                if (entry.type == type)
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// SFXType에 해당하는 AudioClip 찾기
        /// </summary>
        public AudioClip GetClip(SFXType type)
        {
            var entry = GetEntry(type);
            return entry?.clip;
        }

        /// <summary>
        /// SFXType에 해당하는 볼륨 값 찾기
        /// </summary>
        public float GetVolume(SFXType type)
        {
            var entry = GetEntry(type);
            return entry?.volume ?? 1f;
        }

        /// <summary>
        /// 설정 유효성 검사
        /// </summary>
        public bool Validate()
        {
            if (sfxEntries == null || sfxEntries.Length == 0)
            {
                Debug.LogWarning("[SFXData] SFX 엔트리가 없습니다.");
                return false;
            }

            bool hasErrors = false;

            foreach (var entry in sfxEntries)
            {
                if (entry.type == SFXType.None)
                {
                    Debug.LogWarning($"[SFXData] SFX 타입이 None으로 설정되어 있습니다: {entry.description}");
                    hasErrors = true;
                }

                if (entry.clip == null)
                {
                    Debug.LogWarning($"[SFXData] AudioClip이 없습니다: {entry.type}");
                    hasErrors = true;
                }
            }

            return !hasErrors;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector에서 유효성 검사 버튼
        /// </summary>
        [ContextMenu("유효성 검사")]
        private void ValidateInEditor()
        {
            if (Validate())
            {
                Debug.Log("[SFXData] 유효성 검사 통과 ✓");
            }
            else
            {
                Debug.LogError("[SFXData] 유효성 검사 실패 ✗");
            }
        }

        /// <summary>
        /// Inspector에서 SFX 정보 출력
        /// </summary>
        [ContextMenu("SFX 정보 출력")]
        private void PrintSFXInfo()
        {
            Debug.Log("=== SFX Data 정보 ===");
            Debug.Log($"총 SFX 개수: {sfxEntries?.Length ?? 0}");

            if (sfxEntries != null)
            {
                foreach (var entry in sfxEntries)
                {
                    Debug.Log($"  - {entry.type}: {(entry.clip != null ? entry.clip.name : "없음")} (Volume: {entry.volume})");
                }
            }
        }
#endif
    }
}
