using UnityEngine;

namespace Utilities
{
    /// <summary>
    /// Audio Listener 중복 문제 자동 해결
    /// 씬에 하나의 Audio Listener만 남기고 나머지 제거
    /// </summary>
    public class AudioListenerManager : MonoBehaviour
    {
        void Start()
        {
            FixDuplicateAudioListeners();
        }
        
        /// <summary>
        /// 중복된 Audio Listener 제거
        /// </summary>
        [ContextMenu("Audio Listener 중복 제거")]
        public static void FixDuplicateAudioListeners()
        {
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
            
            if (listeners.Length <= 1)
            {
                Debug.Log($"[AudioListenerManager] Audio Listener 개수: {listeners.Length} (정상)");
                return;
            }
            
            Debug.LogWarning($"[AudioListenerManager] Audio Listener 중복 발견: {listeners.Length}개");
            
            // Main Camera의 Audio Listener를 우선적으로 보존
            AudioListener mainCameraListener = null;
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCameraListener = mainCamera.GetComponent<AudioListener>();
            }
            
            int removedCount = 0;
            
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                
                // Main Camera의 Listener가 있으면 나머지 제거
                if (mainCameraListener != null && listener != mainCameraListener)
                {
                    Debug.Log($"[AudioListenerManager] Audio Listener 제거: {listener.gameObject.name}");
                    DestroyImmediate(listener);
                    removedCount++;
                }
                // Main Camera Listener가 없으면 첫 번째만 보존
                else if (mainCameraListener == null && i > 0)
                {
                    Debug.Log($"[AudioListenerManager] Audio Listener 제거: {listener.gameObject.name}");
                    DestroyImmediate(listener);
                    removedCount++;
                }
            }
            
            Debug.Log($"[AudioListenerManager] ✅ Audio Listener 정리 완료: {removedCount}개 제거");
        }
    }
}