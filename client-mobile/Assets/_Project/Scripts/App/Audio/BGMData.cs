using UnityEngine;

namespace App.Audio
{
    /// <summary>
    /// BGM 오디오 클립 설정용 ScriptableObject
    /// Inspector에서 BGM 트랙별 AudioClip 할당
    /// </summary>
    [CreateAssetMenu(fileName = "BGMData", menuName = "Blokus/Audio/BGM Data", order = 1)]
    public class BGMData : ScriptableObject
    {
        [System.Serializable]
        public class BGMEntry
        {
            [Header("BGM 설정")]
            public BGMTrack track;
            public AudioClip clip;

            [Header("재생 설정")]
            [Range(0f, 1f)] public float volume = 0.7f;
            public bool loop = true;

            [Header("설명 (선택사항)")]
            [TextArea(2, 3)]
            public string description;
        }

        [Header("BGM 트랙 목록")]
        [Tooltip("각 BGM 트랙에 AudioClip 할당")]
        public BGMEntry[] bgmEntries;

        [Header("페이드 전환 설정")]
        [Range(0.1f, 5f)]
        [Tooltip("BGM Fade In 시간 (초)")]
        public float fadeInDuration = 1.5f;

        [Range(0.1f, 5f)]
        [Tooltip("BGM Fade Out 시간 (초)")]
        public float fadeOutDuration = 1f;

        [Header("크로스페이드 설정")]
        [Tooltip("BGM 전환 시 크로스페이드 사용")]
        public bool useCrossFade = true;

        // ========================================
        // Public API
        // ========================================

        /// <summary>
        /// BGMTrack에 해당하는 BGMEntry 찾기
        /// </summary>
        public BGMEntry GetEntry(BGMTrack track)
        {
            if (bgmEntries == null)
                return null;

            foreach (var entry in bgmEntries)
            {
                if (entry.track == track)
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// 설정 유효성 검사
        /// </summary>
        public bool Validate()
        {
            if (bgmEntries == null || bgmEntries.Length == 0)
            {
                Debug.LogWarning("[BGMData] BGM 엔트리가 없습니다.");
                return false;
            }

            bool hasErrors = false;

            foreach (var entry in bgmEntries)
            {
                if (entry.track == BGMTrack.None)
                {
                    Debug.LogWarning($"[BGMData] BGM 트랙이 None으로 설정되어 있습니다: {entry.description}");
                    hasErrors = true;
                }

                if (entry.clip == null)
                {
                    Debug.LogWarning($"[BGMData] AudioClip이 없습니다: {entry.track}");
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
                Debug.Log("[BGMData] 유효성 검사 통과 ✓");
            }
            else
            {
                Debug.LogError("[BGMData] 유효성 검사 실패 ✗");
            }
        }

        /// <summary>
        /// Inspector에서 BGM 정보 출력
        /// </summary>
        [ContextMenu("BGM 정보 출력")]
        private void PrintBGMInfo()
        {
            Debug.Log("=== BGM Data 정보 ===");
            Debug.Log($"총 BGM 트랙 수: {bgmEntries?.Length ?? 0}");
            Debug.Log($"Fade In 시간: {fadeInDuration}초");
            Debug.Log($"Fade Out 시간: {fadeOutDuration}초");
            Debug.Log($"크로스페이드: {useCrossFade}");

            if (bgmEntries != null)
            {
                foreach (var entry in bgmEntries)
                {
                    Debug.Log($"  - {entry.track}: {(entry.clip != null ? entry.clip.name : "없음")} (Volume: {entry.volume})");
                }
            }
        }
#endif
    }
}
