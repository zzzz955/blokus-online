using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace App.Audio
{
    /// <summary>
    /// BGM 및 SFX 통합 관리 시스템
    /// SessionManager, UIManager 패턴을 따르는 싱글톤 구조
    /// DontDestroyOnLoad로 씬 전환 시에도 유지
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // Singleton
        public static AudioManager Instance { get; private set; }

        [Header("Audio Configuration")]
        [SerializeField] private BGMData bgmData;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource[] sfxSources;

        [Header("SFX Pool Settings")]
        [SerializeField] private int sfxPoolSize = 5;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // Audio Settings
        private AudioSettings audioSettings;

        // BGM State
        private BGMTrack currentBGMTrack = BGMTrack.None;
        private Coroutine fadeCoroutine;
        private bool isTransitioning = false;

        // SFX Pool
        private int currentSFXIndex = 0;

        /// <summary>
        /// 디버그 로그 출력 여부
        /// </summary>
        private bool IsDebugEnabled => debugMode && (Application.isEditor || Debug.isDebugBuild);

        // ========================================
        // Unity Lifecycle
        // ========================================

        void Awake()
        {
            // Singleton 패턴 (SessionManager, UIManager와 동일)
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);

                InitializeAudioSystem();

                if (IsDebugEnabled)
                    Debug.Log("[AudioManager] Initialized with DontDestroyOnLoad");
            }
            else
            {
                if (IsDebugEnabled)
                    Debug.Log("[AudioManager] Duplicate instance destroyed");
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 오디오 시스템 초기화
        /// </summary>
        private void InitializeAudioSystem()
        {
            // AudioSettings 초기화 및 로드
            audioSettings = new AudioSettings();
            audioSettings.Load();

            // AudioSource 초기화
            InitializeAudioSources();

            // BGMData 유효성 검사
            if (bgmData != null)
            {
                bgmData.Validate();
            }
            else
            {
                Debug.LogWarning("[AudioManager] BGMData가 설정되지 않았습니다. Inspector에서 BGMData를 할당하세요.");
            }

            if (IsDebugEnabled)
                Debug.Log("[AudioManager] 오디오 시스템 초기화 완료");
        }

        /// <summary>
        /// AudioSource 컴포넌트 초기화
        /// </summary>
        private void InitializeAudioSources()
        {
            // BGM AudioSource 생성
            if (bgmSource == null)
            {
                GameObject bgmObj = new GameObject("BGM_AudioSource");
                bgmObj.transform.SetParent(transform);
                bgmSource = bgmObj.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
            }

            // SFX AudioSource Pool 생성
            if (sfxSources == null || sfxSources.Length == 0)
            {
                sfxSources = new AudioSource[sfxPoolSize];
                for (int i = 0; i < sfxPoolSize; i++)
                {
                    GameObject sfxObj = new GameObject($"SFX_AudioSource_{i}");
                    sfxObj.transform.SetParent(transform);
                    sfxSources[i] = sfxObj.AddComponent<AudioSource>();
                    sfxSources[i].playOnAwake = false;
                    sfxSources[i].loop = false;
                }
            }

            ApplyVolumeSettings();

            if (IsDebugEnabled)
                Debug.Log($"[AudioManager] AudioSource 초기화 완료 - BGM: 1개, SFX Pool: {sfxSources.Length}개");
        }

        // ========================================
        // Public API - BGM Control
        // ========================================

        /// <summary>
        /// BGM 재생 (컨텍스트 기반)
        /// MainMenu, Lobby, Gameplay 중 하나 선택
        /// </summary>
        public void PlayBGM(BGMTrack track)
        {
            if (track == BGMTrack.None)
            {
                StopBGM();
                return;
            }

            // 동일한 BGM이 재생 중이면 무시
            if (currentBGMTrack == track && bgmSource.isPlaying && !isTransitioning)
            {
                if (IsDebugEnabled)
                    Debug.Log($"[AudioManager] 동일한 BGM 재생 중: {track}");
                return;
            }

            if (bgmData == null)
            {
                Debug.LogError("[AudioManager] BGMData가 설정되지 않았습니다.");
                return;
            }

            var bgmEntry = bgmData.GetEntry(track);
            if (bgmEntry == null || bgmEntry.clip == null)
            {
                Debug.LogWarning($"[AudioManager] BGM을 찾을 수 없습니다: {track}");
                return;
            }

            // 페이드 전환으로 BGM 변경
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }

            fadeCoroutine = StartCoroutine(TransitionBGM(bgmEntry, track));
        }

        /// <summary>
        /// BGM 정지
        /// </summary>
        public void StopBGM()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }

            fadeCoroutine = StartCoroutine(FadeOutAndStop());
        }

        /// <summary>
        /// BGM 일시정지
        /// </summary>
        public void PauseBGM()
        {
            if (bgmSource.isPlaying)
            {
                bgmSource.Pause();
                if (IsDebugEnabled)
                    Debug.Log("[AudioManager] BGM 일시정지");
            }
        }

        /// <summary>
        /// BGM 재개
        /// </summary>
        public void ResumeBGM()
        {
            if (!bgmSource.isPlaying)
            {
                bgmSource.UnPause();
                if (IsDebugEnabled)
                    Debug.Log("[AudioManager] BGM 재개");
            }
        }

        /// <summary>
        /// 현재 재생 중인 BGM 트랙
        /// </summary>
        public BGMTrack CurrentBGMTrack => currentBGMTrack;

        /// <summary>
        /// BGM 재생 중 여부
        /// </summary>
        public bool IsBGMPlaying => bgmSource != null && bgmSource.isPlaying;

        // ========================================
        // Public API - SFX Control
        // ========================================

        /// <summary>
        /// 효과음 재생
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] SFX AudioClip이 null입니다.");
                return;
            }

            // SFX Pool에서 사용 가능한 AudioSource 찾기
            AudioSource availableSource = GetAvailableSFXSource();
            if (availableSource != null)
            {
                float volume = audioSettings.EffectiveSFXVolume * volumeScale;
                availableSource.PlayOneShot(clip, volume);

                if (IsDebugEnabled)
                    Debug.Log($"[AudioManager] SFX 재생: {clip.name} (Volume: {volume:F2})");
            }
            else
            {
                Debug.LogWarning("[AudioManager] 사용 가능한 SFX AudioSource가 없습니다.");
            }
        }

        /// <summary>
        /// SFX Pool에서 사용 가능한 AudioSource 찾기
        /// </summary>
        private AudioSource GetAvailableSFXSource()
        {
            // Round-robin 방식으로 AudioSource 선택
            for (int i = 0; i < sfxSources.Length; i++)
            {
                int index = (currentSFXIndex + i) % sfxSources.Length;
                if (!sfxSources[index].isPlaying)
                {
                    currentSFXIndex = (index + 1) % sfxSources.Length;
                    return sfxSources[index];
                }
            }

            // 모든 AudioSource가 사용 중이면 첫 번째 AudioSource 재사용
            currentSFXIndex = 1 % sfxSources.Length;
            return sfxSources[0];
        }

        /// <summary>
        /// 모든 SFX 정지
        /// </summary>
        public void StopAllSFX()
        {
            foreach (var source in sfxSources)
            {
                if (source.isPlaying)
                {
                    source.Stop();
                }
            }

            if (IsDebugEnabled)
                Debug.Log("[AudioManager] 모든 SFX 정지");
        }

        // ========================================
        // Public API - Volume Control
        // ========================================

        /// <summary>
        /// 마스터 볼륨 설정 (0-1)
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            audioSettings.MasterVolume = volume;
            ApplyVolumeSettings();

            if (IsDebugEnabled)
                Debug.Log($"[AudioManager] 마스터 볼륨 설정: {volume:F2}");
        }

        /// <summary>
        /// BGM 볼륨 설정 (0-1)
        /// </summary>
        public void SetBGMVolume(float volume)
        {
            audioSettings.BGMVolume = volume;
            ApplyVolumeSettings();

            if (IsDebugEnabled)
                Debug.Log($"[AudioManager] BGM 볼륨 설정: {volume:F2}");
        }

        /// <summary>
        /// SFX 볼륨 설정 (0-1)
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            audioSettings.SFXVolume = volume;

            if (IsDebugEnabled)
                Debug.Log($"[AudioManager] SFX 볼륨 설정: {volume:F2}");
        }

        /// <summary>
        /// 마스터 음소거 토글
        /// </summary>
        public void ToggleMasterMute()
        {
            audioSettings.MasterMuted = !audioSettings.MasterMuted;
            ApplyVolumeSettings();

            if (IsDebugEnabled)
                Debug.Log($"[AudioManager] 마스터 음소거: {audioSettings.MasterMuted}");
        }

        /// <summary>
        /// BGM 음소거 토글
        /// </summary>
        public void ToggleBGMMute()
        {
            audioSettings.BGMMuted = !audioSettings.BGMMuted;
            ApplyVolumeSettings();

            if (IsDebugEnabled)
                Debug.Log($"[AudioManager] BGM 음소거: {audioSettings.BGMMuted}");
        }

        /// <summary>
        /// SFX 음소거 토글
        /// </summary>
        public void ToggleSFXMute()
        {
            audioSettings.SFXMuted = !audioSettings.SFXMuted;

            if (IsDebugEnabled)
                Debug.Log($"[AudioManager] SFX 음소거: {audioSettings.SFXMuted}");
        }

        // Getters
        public float GetMasterVolume() => audioSettings.MasterVolume;
        public float GetBGMVolume() => audioSettings.BGMVolume;
        public float GetSFXVolume() => audioSettings.SFXVolume;
        public bool IsMasterMuted() => audioSettings.MasterMuted;
        public bool IsBGMMuted() => audioSettings.BGMMuted;
        public bool IsSFXMuted() => audioSettings.SFXMuted;

        /// <summary>
        /// 오디오 설정 접근자 (고급 사용)
        /// </summary>
        public AudioSettings Settings => audioSettings;

        // ========================================
        // Private Methods - Volume Application
        // ========================================

        /// <summary>
        /// 볼륨 설정을 AudioSource에 적용
        /// </summary>
        private void ApplyVolumeSettings()
        {
            if (bgmSource != null)
            {
                bgmSource.volume = audioSettings.EffectiveBGMVolume;
            }
        }

        // ========================================
        // Private Methods - BGM Transition
        // ========================================

        /// <summary>
        /// BGM 전환 코루틴 (Fade In/Out)
        /// </summary>
        private IEnumerator TransitionBGM(BGMData.BGMEntry newBGM, BGMTrack newTrack)
        {
            isTransitioning = true;

            float fadeOutDuration = bgmData.fadeOutDuration;
            float fadeInDuration = bgmData.fadeInDuration;

            // 1. Fade Out (기존 BGM)
            if (bgmSource.isPlaying && bgmSource.clip != null)
            {
                float startVolume = bgmSource.volume;
                float elapsed = 0f;

                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeOutDuration;
                    bgmSource.volume = Mathf.Lerp(startVolume, 0f, t);
                    yield return null;
                }

                bgmSource.Stop();
            }

            // 2. BGM 전환
            bgmSource.clip = newBGM.clip;
            bgmSource.loop = newBGM.loop;
            bgmSource.volume = 0f;
            bgmSource.Play();

            currentBGMTrack = newTrack;

            // 3. Fade In (새 BGM)
            float targetVolume = newBGM.volume * audioSettings.EffectiveBGMVolume;
            float elapsed2 = 0f;

            while (elapsed2 < fadeInDuration)
            {
                elapsed2 += Time.deltaTime;
                float t = elapsed2 / fadeInDuration;
                bgmSource.volume = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }

            bgmSource.volume = targetVolume;

            isTransitioning = false;

            if (IsDebugEnabled)
                Debug.Log($"[AudioManager] BGM 전환 완료: {newTrack} ({newBGM.clip.name})");
        }

        /// <summary>
        /// BGM Fade Out 후 정지
        /// </summary>
        private IEnumerator FadeOutAndStop()
        {
            isTransitioning = true;

            if (bgmSource.isPlaying)
            {
                float fadeOutDuration = bgmData != null ? bgmData.fadeOutDuration : 1f;
                float startVolume = bgmSource.volume;
                float elapsed = 0f;

                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeOutDuration;
                    bgmSource.volume = Mathf.Lerp(startVolume, 0f, t);
                    yield return null;
                }

                bgmSource.Stop();
                bgmSource.volume = 0f;
            }

            currentBGMTrack = BGMTrack.None;
            isTransitioning = false;

            if (IsDebugEnabled)
                Debug.Log("[AudioManager] BGM 정지 완료");
        }

        // ========================================
        // Debug & Testing
        // ========================================

#if UNITY_EDITOR
        /// <summary>
        /// Inspector 테스트: MainMenu BGM 재생
        /// </summary>
        [ContextMenu("테스트: MainMenu BGM 재생")]
        private void TestPlayMainMenuBGM()
        {
            PlayBGM(BGMTrack.MainMenu);
        }

        /// <summary>
        /// Inspector 테스트: Lobby BGM 재생
        /// </summary>
        [ContextMenu("테스트: Lobby BGM 재생")]
        private void TestPlayLobbyBGM()
        {
            PlayBGM(BGMTrack.Lobby);
        }

        /// <summary>
        /// Inspector 테스트: Gameplay BGM 재생
        /// </summary>
        [ContextMenu("테스트: Gameplay BGM 재생")]
        private void TestPlayGameplayBGM()
        {
            PlayBGM(BGMTrack.Gameplay);
        }

        /// <summary>
        /// Inspector 테스트: BGM 정지
        /// </summary>
        [ContextMenu("테스트: BGM 정지")]
        private void TestStopBGM()
        {
            StopBGM();
        }

        /// <summary>
        /// Inspector 테스트: 볼륨 정보 출력
        /// </summary>
        [ContextMenu("디버그: 볼륨 정보 출력")]
        private void DebugPrintVolumeInfo()
        {
            Debug.Log("=== AudioManager 볼륨 정보 ===");
            Debug.Log($"Master Volume: {audioSettings.MasterVolume:F2} (Muted: {audioSettings.MasterMuted})");
            Debug.Log($"BGM Volume: {audioSettings.BGMVolume:F2} (Muted: {audioSettings.BGMMuted})");
            Debug.Log($"SFX Volume: {audioSettings.SFXVolume:F2} (Muted: {audioSettings.SFXMuted})");
            Debug.Log($"Effective BGM Volume: {audioSettings.EffectiveBGMVolume:F2}");
            Debug.Log($"Effective SFX Volume: {audioSettings.EffectiveSFXVolume:F2}");
            Debug.Log($"Current BGM Track: {currentBGMTrack}");
            Debug.Log($"BGM Playing: {IsBGMPlaying}");
        }
#endif
    }
}
