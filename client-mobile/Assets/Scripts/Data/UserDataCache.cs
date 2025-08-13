using System;
using System.Collections.Generic;
using UnityEngine;
using BlokusUnity.Common;
using BlokusUnity.Network;
using NetworkStageData = BlokusUnity.Network.StageData;
using NetworkUserStageProgress = BlokusUnity.Network.UserStageProgress;
using UserInfo = BlokusUnity.Common.UserInfo;
using UserStageProgress = BlokusUnity.Common.UserStageProgress;

namespace BlokusUnity.Data
{
    /// <summary>
    /// 사용자 데이터 캐싱 시스템
    /// 로그인된 사용자의 정보, 통계, 스테이지 진행도를 메모리 및 영구 저장소에 관리
    /// </summary>
    public class UserDataCache : MonoBehaviour
    {
        [Header("캐시 설정")]
        [SerializeField] private bool enablePersistentCache = true;
        [SerializeField] private int maxCacheSize = 1000; // 최대 캐시된 스테이지 진행도 수
        
        // 싱글톤
        public static UserDataCache Instance { get; private set; }
        
        // 현재 로그인된 사용자 정보
        private UserInfo currentUser;
        private bool isLoggedIn = false;
        private string authToken;
        
        // 스테이지 진행도 캐시 (stageNumber -> UserStageProgress)
        private Dictionary<int, NetworkUserStageProgress> stageProgressCache = new Dictionary<int, NetworkUserStageProgress>();
        
        // 서버 스테이지 데이터 캐시 (stageNumber -> StageData)
        private Dictionary<int, NetworkStageData> stageDataCache = new Dictionary<int, NetworkStageData>();
        
        // 압축된 스테이지 메타데이터 캐시
        private HttpApiClient.CompactStageMetadata[] stageMetadataCache;
        
        // 이벤트
        public event System.Action<UserInfo> OnUserDataUpdated;
        public event System.Action<NetworkUserStageProgress> OnStageProgressUpdated;
        public event System.Action<NetworkStageData> OnStageDataUpdated;
        public event System.Action<HttpApiClient.CompactStageMetadata[]> OnStageMetadataUpdated;
        public event System.Action OnLoginStatusChanged;
        
        void Awake()
        {
            // 싱글톤 패턴
            if (Instance == null)
            {
                Instance = this;
                
                // 루트 GameObject인지 확인하고 DontDestroyOnLoad 적용
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
                else
                {
                    Debug.LogWarning("UserDataCache가 루트 GameObject가 아닙니다. DontDestroyOnLoad를 적용할 수 없습니다.");
                }
                
                LoadCacheFromDisk();
                Debug.Log("UserDataCache 초기화 완료");
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        void OnApplicationPause(bool pauseStatus)
        {
            // 앱이 백그라운드로 갈 때 캐시 저장
            if (pauseStatus && enablePersistentCache)
            {
                SaveCacheToDisk();
            }
        }
        
        void OnApplicationFocus(bool hasFocus)
        {
            // 포커스를 잃을 때 캐시 저장
            if (!hasFocus && enablePersistentCache)
            {
                SaveCacheToDisk();
            }
        }
        
        void OnDestroy()
        {
            if (enablePersistentCache)
            {
                SaveCacheToDisk();
            }
        }
        
        // ========================================
        // 사용자 인증 관리
        // ========================================
        
        /// <summary>
        /// 사용자 로그인 처리
        /// </summary>
        public void LoginUser(UserInfo userInfo, string token = null)
        {
            currentUser = userInfo;
            authToken = token;
            isLoggedIn = true;
            
            Debug.Log($"사용자 로그인: {userInfo.username} (레벨: {userInfo.level})");
            
            // HTTP API 토큰 설정
            if (!string.IsNullOrEmpty(token) && HttpApiClient.Instance != null)
            {
                // 사용자 ID는 userInfo에서 추출하거나 별도로 관리 필요
                HttpApiClient.Instance.SetAuthToken(token, GetUserIdFromUserInfo(userInfo));
            }
            
            SaveUserDataToDisk();
            OnUserDataUpdated?.Invoke(currentUser);
            OnLoginStatusChanged?.Invoke();
        }
        
        /// <summary>
        /// 사용자 로그아웃 처리
        /// </summary>
        public void LogoutUser()
        {
            Debug.Log($"사용자 로그아웃: {currentUser?.username}");
            
            currentUser = null;
            authToken = null;
            isLoggedIn = false;
            
            // 캐시 클리어 (또는 유지하도록 선택 가능)
            ClearCache();
            
            OnLoginStatusChanged?.Invoke();
        }
        
        /// <summary>
        /// 현재 로그인 상태 확인
        /// </summary>
        public bool IsLoggedIn()
        {
            return isLoggedIn && currentUser != null;
        }
        
        /// <summary>
        /// 현재 사용자 정보 반환
        /// </summary>
        public UserInfo GetCurrentUser()
        {
            return currentUser;
        }
        
        /// <summary>
        /// 현재 인증 토큰 반환
        /// </summary>
        public string GetAuthToken()
        {
            return authToken;
        }
        
        // ========================================
        // 사용자 데이터 관리
        // ========================================
        
        /// <summary>
        /// 사용자 정보 업데이트
        /// </summary>
        public void UpdateUserInfo(UserInfo userInfo)
        {
            if (!IsLoggedIn())
            {
                Debug.LogWarning("로그인되지 않은 상태에서 사용자 정보 업데이트 시도");
                return;
            }
            
            currentUser = userInfo;
            SaveUserDataToDisk();
            OnUserDataUpdated?.Invoke(currentUser);
            
            Debug.Log($"사용자 정보 업데이트: {userInfo.username} (레벨: {userInfo.level})");
        }
        
        // ========================================
        // 스테이지 진행도 관리
        // ========================================
        
        /// <summary>
        /// 스테이지 진행도 설정
        /// </summary>
        public void SetStageProgress(NetworkUserStageProgress progress)
        {
            stageProgressCache[progress.stageNumber] = progress;
            
            // 캐시 크기 제한
            if (stageProgressCache.Count > maxCacheSize)
            {
                RemoveOldestProgressEntries();
            }
            
            SaveProgressToDisk();
            OnStageProgressUpdated?.Invoke(progress);
            
            Debug.Log($"스테이지 진행도 설정: {progress.stageNumber} (완료: {progress.isCompleted}, 별: {progress.starsEarned})");
        }
        
        /// <summary>
        /// 스테이지 진행도 가져오기
        /// </summary>
        public NetworkUserStageProgress GetStageProgress(int stageNumber)
        {
            if (stageProgressCache.TryGetValue(stageNumber, out NetworkUserStageProgress progress))
            {
                return progress;
            }
            
            // 진행도가 없으면 기본값 반환
            return new NetworkUserStageProgress
            {
                stageNumber = stageNumber,
                isCompleted = false,
                starsEarned = 0,
                bestScore = 0,
                bestCompletionTime = 0,
                totalAttempts = 0,
                successfulAttempts = 0
            };
        }
        
        /// <summary>
        /// 여러 스테이지 진행도 일괄 설정
        /// </summary>
        public void SetBatchStageProgress(List<NetworkUserStageProgress> progressList)
        {
            foreach (var progress in progressList)
            {
                stageProgressCache[progress.stageNumber] = progress;
            }
            
            SaveProgressToDisk();
            
            Debug.Log($"일괄 스테이지 진행도 설정: {progressList.Count}개");
        }
        
        /// <summary>
        /// 최대 클리어 스테이지 번호 반환
        /// </summary>
        public int GetMaxClearedStage()
        {
            int maxStage = 0;
            foreach (var progress in stageProgressCache.Values)
            {
                if (progress.isCompleted && progress.stageNumber > maxStage)
                {
                    maxStage = progress.stageNumber;
                }
            }
            return maxStage;
        }
        
        // ========================================
        // 서버 스테이지 데이터 관리
        // ========================================
        
        /// <summary>
        /// 서버 스테이지 데이터 설정
        /// </summary>
        public void SetStageData(NetworkStageData stageData)
        {
            stageDataCache[stageData.stageNumber] = stageData;
            OnStageDataUpdated?.Invoke(stageData);
            
            Debug.Log($"서버 스테이지 데이터 설정: {stageData.stageNumber} - {stageData.stageName}");
        }
        
        /// <summary>
        /// 서버 스테이지 데이터 가져오기
        /// </summary>
        public NetworkStageData GetStageData(int stageNumber)
        {
            stageDataCache.TryGetValue(stageNumber, out NetworkStageData stageData);
            return stageData; // null일 수 있음
        }
        
        /// <summary>
        /// 스테이지 데이터가 캐시되어 있는지 확인
        /// </summary>
        public bool HasStageData(int stageNumber)
        {
            return stageDataCache.ContainsKey(stageNumber);
        }
        
        // ========================================
        // 캐시 관리
        // ========================================
        
        /// <summary>
        /// 전체 캐시 클리어
        /// </summary>
        public void ClearCache()
        {
            stageProgressCache.Clear();
            stageDataCache.Clear();
            
            if (enablePersistentCache)
            {
                PlayerPrefs.DeleteKey("UserDataCache_Progress");
                PlayerPrefs.DeleteKey("UserDataCache_StageData");
                PlayerPrefs.DeleteKey("UserDataCache_UserInfo");
                PlayerPrefs.Save();
            }
            
            Debug.Log("사용자 데이터 캐시 클리어됨");
        }
        
        /// <summary>
        /// 오래된 진행도 항목 제거 (LRU 방식)
        /// </summary>
        private void RemoveOldestProgressEntries()
        {
            // 간단한 구현: 가장 작은 스테이지 번호부터 제거
            List<int> sortedKeys = new List<int>(stageProgressCache.Keys);
            sortedKeys.Sort();
            
            int removeCount = stageProgressCache.Count - maxCacheSize + 10; // 여유분
            for (int i = 0; i < removeCount && i < sortedKeys.Count; i++)
            {
                stageProgressCache.Remove(sortedKeys[i]);
            }
        }
        
        // ========================================
        // 영구 저장소 관리
        // ========================================
        
        /// <summary>
        /// 디스크에서 캐시 로드
        /// </summary>
        private void LoadCacheFromDisk()
        {
            if (!enablePersistentCache)
                return;
            
            try
            {
                // 사용자 정보 로드
                string userInfoJson = PlayerPrefs.GetString("UserDataCache_UserInfo", "");
                if (!string.IsNullOrEmpty(userInfoJson))
                {
                    var userData = JsonUtility.FromJson<CachedUserData>(userInfoJson);
                    currentUser = userData.userInfo;
                    authToken = userData.authToken;
                    isLoggedIn = userData.isLoggedIn;
                    
                    Debug.Log($"캐시에서 사용자 정보 로드: {currentUser?.username}");
                }
                
                // 스테이지 진행도 로드
                string progressJson = PlayerPrefs.GetString("UserDataCache_Progress", "");
                if (!string.IsNullOrEmpty(progressJson))
                {
                    var progressData = JsonUtility.FromJson<CachedProgressData>(progressJson);
                    foreach (var progress in progressData.progressList)
                    {
                        stageProgressCache[progress.stageNumber] = progress;
                    }
                    
                    Debug.Log($"캐시에서 스테이지 진행도 로드: {stageProgressCache.Count}개");
                }
                
                // 스테이지 데이터는 서버에서 최신 정보를 가져오므로 캐시하지 않음
            }
            catch (Exception ex)
            {
                Debug.LogError($"캐시 로드 실패: {ex.Message}");
                ClearCache();
            }
        }
        
        /// <summary>
        /// 캐시를 디스크에 저장
        /// </summary>
        private void SaveCacheToDisk()
        {
            if (!enablePersistentCache)
                return;
            
            try
            {
                SaveUserDataToDisk();
                SaveProgressToDisk();
            }
            catch (Exception ex)
            {
                Debug.LogError($"캐시 저장 실패: {ex.Message}");
            }
        }
        
        private void SaveUserDataToDisk()
        {
            if (currentUser != null)
            {
                var userData = new CachedUserData
                {
                    userInfo = currentUser,
                    authToken = authToken,
                    isLoggedIn = isLoggedIn
                };
                
                string json = JsonUtility.ToJson(userData);
                PlayerPrefs.SetString("UserDataCache_UserInfo", json);
                PlayerPrefs.Save();
            }
        }
        
        private void SaveProgressToDisk()
        {
            if (stageProgressCache.Count > 0)
            {
                var progressData = new CachedProgressData
                {
                    progressList = new List<NetworkUserStageProgress>(stageProgressCache.Values)
                };
                
                string json = JsonUtility.ToJson(progressData);
                PlayerPrefs.SetString("UserDataCache_Progress", json);
                PlayerPrefs.Save();
            }
        }
        
        // ========================================
        // 스테이지 메타데이터 관리 (API 전용)
        // ========================================
        
        /// <summary>
        /// 스테이지 메타데이터 설정 (압축된 API 응답)
        /// </summary>
        public void SetStageMetadata(HttpApiClient.CompactStageMetadata[] metadata)
        {
            stageMetadataCache = metadata;
            OnStageMetadataUpdated?.Invoke(metadata);
            
            Debug.Log($"스테이지 메타데이터 설정: {metadata?.Length ?? 0}개");
        }
        
        /// <summary>
        /// 스테이지 메타데이터 가져오기
        /// </summary>
        public HttpApiClient.CompactStageMetadata[] GetStageMetadata()
        {
            return stageMetadataCache;
        }
        
        /// <summary>
        /// 특정 스테이지 메타데이터 가져오기
        /// </summary>
        public HttpApiClient.CompactStageMetadata? GetStageMetadata(int stageNumber)
        {
            if (stageMetadataCache != null)
            {
                foreach (var metadata in stageMetadataCache)
                {
                    if (metadata.n == stageNumber)
                    {
                        return metadata;
                    }
                }
            }
            return null;
        }
        
        // ========================================
        // 유틸리티 메서드
        // ========================================
        
        /// <summary>
        /// UserInfo에서 사용자 ID 추출 (임시 구현)
        /// </summary>
        private int GetUserIdFromUserInfo(UserInfo userInfo)
        {
            // UserInfo에 userId 필드가 없으므로 임시로 username 해시코드 사용
            // 실제로는 서버에서 userId를 별도로 제공해야 함
            return Mathf.Abs(userInfo.username.GetHashCode());
        }
        
        /// <summary>
        /// 캐시 상태 정보 반환
        /// </summary>
        public string GetCacheStatusInfo()
        {
            return $"로그인: {IsLoggedIn()}, " +
                   $"진행도: {stageProgressCache.Count}개, " +
                   $"스테이지데이터: {stageDataCache.Count}개";
        }
        
        // ========================================
        // 직렬화용 데이터 구조체
        // ========================================
        
        [System.Serializable]
        private class CachedUserData
        {
            public UserInfo userInfo;
            public string authToken;
            public bool isLoggedIn;
        }
        
        [System.Serializable]
        private class CachedProgressData
        {
            public List<NetworkUserStageProgress> progressList = new List<NetworkUserStageProgress>();
        }
    }
}