using UnityEngine;

namespace App.Audio
{
    /// <summary>
    /// 오디오 설정 관리 (볼륨, 음소거)
    /// PlayerPrefs를 통한 영속성 지원
    /// </summary>
    [System.Serializable]
    public class AudioSettings
    {
        // PlayerPrefs 키
        private const string MASTER_VOLUME_KEY = "blokus_audio_master_volume";
        private const string BGM_VOLUME_KEY = "blokus_audio_bgm_volume";
        private const string SFX_VOLUME_KEY = "blokus_audio_sfx_volume";
        private const string MASTER_MUTE_KEY = "blokus_audio_master_mute";
        private const string BGM_MUTE_KEY = "blokus_audio_bgm_mute";
        private const string SFX_MUTE_KEY = "blokus_audio_sfx_mute";

        // 볼륨 설정 (0-1)
        [SerializeField] private float masterVolume = 1f;
        [SerializeField] private float bgmVolume = 0.7f;
        [SerializeField] private float sfxVolume = 1f;

        // 음소거 설정
        [SerializeField] private bool masterMuted = false;
        [SerializeField] private bool bgmMuted = false;
        [SerializeField] private bool sfxMuted = false;

        // ========================================
        // Properties
        // ========================================

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                Save();
            }
        }

        public float BGMVolume
        {
            get => bgmVolume;
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                Save();
            }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                Save();
            }
        }

        public bool MasterMuted
        {
            get => masterMuted;
            set
            {
                masterMuted = value;
                Save();
            }
        }

        public bool BGMMuted
        {
            get => bgmMuted;
            set
            {
                bgmMuted = value;
                Save();
            }
        }

        public bool SFXMuted
        {
            get => sfxMuted;
            set
            {
                sfxMuted = value;
                Save();
            }
        }

        // ========================================
        // Computed Properties
        // ========================================

        /// <summary>
        /// 최종 BGM 볼륨 (Master * BGM, 음소거 고려)
        /// </summary>
        public float EffectiveBGMVolume
        {
            get
            {
                if (masterMuted || bgmMuted)
                    return 0f;
                return masterVolume * bgmVolume;
            }
        }

        /// <summary>
        /// 최종 SFX 볼륨 (Master * SFX, 음소거 고려)
        /// </summary>
        public float EffectiveSFXVolume
        {
            get
            {
                if (masterMuted || sfxMuted)
                    return 0f;
                return masterVolume * sfxVolume;
            }
        }

        // ========================================
        // Persistence
        // ========================================

        /// <summary>
        /// PlayerPrefs에서 설정 로드
        /// </summary>
        public void Load()
        {
            masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 1f);
            bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 0.7f);
            sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);

            masterMuted = PlayerPrefs.GetInt(MASTER_MUTE_KEY, 0) == 1;
            bgmMuted = PlayerPrefs.GetInt(BGM_MUTE_KEY, 0) == 1;
            sfxMuted = PlayerPrefs.GetInt(SFX_MUTE_KEY, 0) == 1;

            Debug.Log($"[AudioSettings] 설정 로드 완료 - Master: {masterVolume:F2}, BGM: {bgmVolume:F2}, SFX: {sfxVolume:F2}");
        }

        /// <summary>
        /// PlayerPrefs에 설정 저장
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, masterVolume);
            PlayerPrefs.SetFloat(BGM_VOLUME_KEY, bgmVolume);
            PlayerPrefs.SetFloat(SFX_VOLUME_KEY, sfxVolume);

            PlayerPrefs.SetInt(MASTER_MUTE_KEY, masterMuted ? 1 : 0);
            PlayerPrefs.SetInt(BGM_MUTE_KEY, bgmMuted ? 1 : 0);
            PlayerPrefs.SetInt(SFX_MUTE_KEY, sfxMuted ? 1 : 0);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// 설정 초기화 (기본값으로 리셋)
        /// </summary>
        public void Reset()
        {
            masterVolume = 1f;
            bgmVolume = 0.7f;
            sfxVolume = 1f;

            masterMuted = false;
            bgmMuted = false;
            sfxMuted = false;

            Save();
            Debug.Log("[AudioSettings] 설정 초기화 완료");
        }
    }
}
