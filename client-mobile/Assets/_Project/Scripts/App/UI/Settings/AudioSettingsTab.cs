using UnityEngine;
using UnityEngine.UI;
using TMPro;
using App.Audio;

namespace App.UI.Settings
{
    /// <summary>
    /// Audio 설정 탭 (BGM/SFX Mute 및 Volume 제어)
    /// AudioManager와 직접 연동
    /// </summary>
    public class AudioSettingsTab : MonoBehaviour
    {
        [Header("BGM Controls")]
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Toggle bgmMuteToggle;
        [SerializeField] private TMP_Text bgmVolumeText; // 백분율 표시 (옵션)

        [Header("SFX Controls")]
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle sfxMuteToggle;
        [SerializeField] private TMP_Text sfxVolumeText; // 백분율 표시 (옵션)

        [Header("Master Controls (옵션)")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Toggle masterMuteToggle;
        [SerializeField] private TMP_Text masterVolumeText;

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        private bool isInitializing = false; // 초기화 중 이벤트 무시

        void OnEnable()
        {
            // AudioManager 존재 확인
            if (AudioManager.Instance == null)
            {
                Debug.LogError("[AudioSettingsTab] AudioManager.Instance가 null입니다! MainScene에 AudioManager가 있는지 확인하세요.");
                return;
            }

            // UI 초기화 (AudioManager 현재 값으로)
            InitializeUI();

            // 이벤트 연결
            RegisterEvents();
        }

        void OnDisable()
        {
            // 이벤트 해제
            UnregisterEvents();
        }

        /// <summary>
        /// AudioManager의 현재 값으로 UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            isInitializing = true;

            if (AudioManager.Instance == null) return;

            // BGM 설정 로드
            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.value = AudioManager.Instance.GetBGMVolume();
                UpdateVolumeText(bgmVolumeText, bgmVolumeSlider.value);
            }

            if (bgmMuteToggle != null)
            {
                bgmMuteToggle.isOn = AudioManager.Instance.IsBGMMuted();
            }

            // SFX 설정 로드
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = AudioManager.Instance.GetSFXVolume();
                UpdateVolumeText(sfxVolumeText, sfxVolumeSlider.value);
            }

            if (sfxMuteToggle != null)
            {
                sfxMuteToggle.isOn = AudioManager.Instance.IsSFXMuted();
            }

            // Master 설정 로드 (옵션)
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = AudioManager.Instance.GetMasterVolume();
                UpdateVolumeText(masterVolumeText, masterVolumeSlider.value);
            }

            if (masterMuteToggle != null)
            {
                masterMuteToggle.isOn = AudioManager.Instance.IsMasterMuted();
            }

            isInitializing = false;

            if (debugMode)
            {
                Debug.Log("[AudioSettingsTab] UI 초기화 완료");
            }
        }

        /// <summary>
        /// UI 이벤트 연결
        /// </summary>
        private void RegisterEvents()
        {
            // BGM
            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
            }

            if (bgmMuteToggle != null)
            {
                bgmMuteToggle.onValueChanged.AddListener(OnBGMMuteToggled);
            }

            // SFX
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            }

            if (sfxMuteToggle != null)
            {
                sfxMuteToggle.onValueChanged.AddListener(OnSFXMuteToggled);
            }

            // Master (옵션)
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }

            if (masterMuteToggle != null)
            {
                masterMuteToggle.onValueChanged.AddListener(OnMasterMuteToggled);
            }
        }

        /// <summary>
        /// UI 이벤트 해제
        /// </summary>
        private void UnregisterEvents()
        {
            // BGM
            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.onValueChanged.RemoveListener(OnBGMVolumeChanged);
            }

            if (bgmMuteToggle != null)
            {
                bgmMuteToggle.onValueChanged.RemoveListener(OnBGMMuteToggled);
            }

            // SFX
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
            }

            if (sfxMuteToggle != null)
            {
                sfxMuteToggle.onValueChanged.RemoveListener(OnSFXMuteToggled);
            }

            // Master (옵션)
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            }

            if (masterMuteToggle != null)
            {
                masterMuteToggle.onValueChanged.RemoveListener(OnMasterMuteToggled);
            }
        }

        // ========================================
        // BGM 이벤트 핸들러
        // ========================================

        private void OnBGMVolumeChanged(float value)
        {
            if (isInitializing || AudioManager.Instance == null) return;

            AudioManager.Instance.SetBGMVolume(value);
            UpdateVolumeText(bgmVolumeText, value);

            if (debugMode)
            {
                Debug.Log($"[AudioSettingsTab] BGM Volume: {value:F2}");
            }
        }

        private void OnBGMMuteToggled(bool isMuted)
        {
            if (isInitializing || AudioManager.Instance == null) return;

            // AudioManager의 현재 상태와 다를 때만 토글
            if (AudioManager.Instance.IsBGMMuted() != isMuted)
            {
                AudioManager.Instance.ToggleBGMMute();
            }

            if (debugMode)
            {
                Debug.Log($"[AudioSettingsTab] BGM Mute: {isMuted}");
            }
        }

        // ========================================
        // SFX 이벤트 핸들러
        // ========================================

        private void OnSFXVolumeChanged(float value)
        {
            if (isInitializing || AudioManager.Instance == null) return;

            AudioManager.Instance.SetSFXVolume(value);
            UpdateVolumeText(sfxVolumeText, value);

            if (debugMode)
            {
                Debug.Log($"[AudioSettingsTab] SFX Volume: {value:F2}");
            }
        }

        private void OnSFXMuteToggled(bool isMuted)
        {
            if (isInitializing || AudioManager.Instance == null) return;

            // AudioManager의 현재 상태와 다를 때만 토글
            if (AudioManager.Instance.IsSFXMuted() != isMuted)
            {
                AudioManager.Instance.ToggleSFXMute();
            }

            if (debugMode)
            {
                Debug.Log($"[AudioSettingsTab] SFX Mute: {isMuted}");
            }
        }

        // ========================================
        // Master 이벤트 핸들러 (옵션)
        // ========================================

        private void OnMasterVolumeChanged(float value)
        {
            if (isInitializing || AudioManager.Instance == null) return;

            AudioManager.Instance.SetMasterVolume(value);
            UpdateVolumeText(masterVolumeText, value);

            if (debugMode)
            {
                Debug.Log($"[AudioSettingsTab] Master Volume: {value:F2}");
            }
        }

        private void OnMasterMuteToggled(bool isMuted)
        {
            if (isInitializing || AudioManager.Instance == null) return;

            // AudioManager의 현재 상태와 다를 때만 토글
            if (AudioManager.Instance.IsMasterMuted() != isMuted)
            {
                AudioManager.Instance.ToggleMasterMute();
            }

            if (debugMode)
            {
                Debug.Log($"[AudioSettingsTab] Master Mute: {isMuted}");
            }
        }

        // ========================================
        // Helper Methods
        // ========================================

        /// <summary>
        /// 볼륨 텍스트 업데이트 (백분율 표시)
        /// </summary>
        private void UpdateVolumeText(TMP_Text text, float value)
        {
            if (text != null)
            {
                text.text = $"{Mathf.RoundToInt(value * 100)}%";
            }
        }
    }
}
