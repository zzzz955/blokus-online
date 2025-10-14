using UnityEngine;

namespace App.Audio
{
    /// <summary>
    /// 모달 Open/Close 사운드 재생 컴포넌트
    /// 모달 GameObject에 부착하여 OnEnable/OnDisable 시 자동 재생
    /// 버튼 클릭 사운드와 중복되지 않도록 설계됨
    ///
    /// 사용법:
    /// 1. 모달 GameObject에 이 컴포넌트 추가
    /// 2. playOnEnable = true (Open 사운드)
    /// 3. playOnDisable = true (Close 사운드)
    /// 4. 모달을 여는 버튼에 "ModalButton" 태그 추가 (GlobalButtonSoundPlayer가 스킵)
    /// </summary>
    public class ModalSoundPlayer : MonoBehaviour
    {
        [Header("재생 설정")]
        [SerializeField] private bool playOnEnable = true;
        [Tooltip("모달이 활성화될 때 Open 사운드 재생")]

        [SerializeField] private bool playOnDisable = true;
        [Tooltip("모달이 비활성화될 때 Close 사운드 재생")]

        [Header("재생 지연 (초)")]
        [SerializeField] private float openDelay = 0f;
        [Tooltip("Open 사운드 재생 지연 (애니메이션과 동기화)")]

        [SerializeField] private float closeDelay = 0f;
        [Tooltip("Close 사운드 재생 지연 (애니메이션과 동기화)")]

        [Header("디버그")]
        [SerializeField] private bool verboseLog = false;

        // ========================================
        // Unity Lifecycle
        // ========================================

        void OnEnable()
        {
            if (playOnEnable)
            {
                if (openDelay > 0f)
                {
                    Invoke(nameof(PlayOpenSound), openDelay);
                }
                else
                {
                    PlayOpenSound();
                }
            }
        }

        void OnDisable()
        {
            if (playOnDisable)
            {
                // OnDisable에서는 Invoke 사용 불가능 (GameObject가 비활성화됨)
                // 즉시 재생하거나, 부모 코루틴 사용 필요
                if (closeDelay > 0f)
                {
                    // 지연 재생은 불가능하므로 즉시 재생
                    if (verboseLog)
                        Debug.LogWarning($"[ModalSoundPlayer] OnDisable에서는 지연 재생 불가능, 즉시 재생: {gameObject.name}");
                }
                PlayCloseSound();
            }
        }

        // ========================================
        // Sound Playback
        // ========================================

        private void PlayOpenSound()
        {
            AudioManager.Instance?.PlaySFX(SFXType.ModalOpen);

            if (verboseLog)
                Debug.Log($"[ModalSoundPlayer] Modal Open 사운드 재생: {gameObject.name}");
        }

        private void PlayCloseSound()
        {
            AudioManager.Instance?.PlaySFX(SFXType.ModalClose);

            if (verboseLog)
                Debug.Log($"[ModalSoundPlayer] Modal Close 사운드 재생: {gameObject.name}");
        }

        // ========================================
        // Public API
        // ========================================

        /// <summary>
        /// Open 사운드를 수동으로 재생
        /// </summary>
        public void PlayOpen()
        {
            PlayOpenSound();
        }

        /// <summary>
        /// Close 사운드를 수동으로 재생
        /// </summary>
        public void PlayClose()
        {
            PlayCloseSound();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector 테스트: Open 사운드 재생
        /// </summary>
        [ContextMenu("테스트: Open 사운드")]
        private void TestPlayOpen()
        {
            PlayOpenSound();
        }

        /// <summary>
        /// Inspector 테스트: Close 사운드 재생
        /// </summary>
        [ContextMenu("테스트: Close 사운드")]
        private void TestPlayClose()
        {
            PlayCloseSound();
        }
#endif
    }
}
