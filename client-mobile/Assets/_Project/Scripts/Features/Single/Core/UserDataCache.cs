using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using App.Network;
using App.Services;
using Features.Multi.Net;
using Shared.Models;
using NetworkStageData = App.Network.StageData;
using NetworkUserStageProgress = App.Network.UserStageProgress;
using UserInfo = Shared.Models.UserInfo;
using UserStageProgress = Shared.Models.UserStageProgress;
namespace Features.Single.Core
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

        // 중복 요청 방지
        private bool isBatchProgressLoading = false;

        // 🔥 추가: 초기 동기화 상태 추적
        private bool metadataReceived = false;
        private bool progressBatchReceived = false;
        private bool currentStatusReceived = false;

        // 이벤트
        public event System.Action<UserInfo> OnUserDataUpdated;
        public event System.Action<NetworkUserStageProgress> OnStageProgressUpdated;
        public event System.Action<NetworkStageData> OnStageDataUpdated;
        public event System.Action<HttpApiClient.CompactStageMetadata[]> OnStageMetadataUpdated;
        public event System.Action OnLoginStatusChanged;

        // Migration Plan: Initialization state
        private bool isInitialized = false;
        private int cachedMaxStageCompleted = 0; // 진행도 캐시 기반
        public int MaxStageCompleted
        {
            get
            {
                int fromProfile = currentUser != null ? currentUser.maxStageCompleted : 0;
                return Mathf.Max(fromProfile, cachedMaxStageCompleted);
            }
        }

        /// <summary>
        /// 🔥 추가: 초기 동기화 완료 여부 확인
        /// 메타데이터, 진행도 배치, 현재 상태가 모두 수신되었을 때만 true
        /// </summary>
        public bool IsInitialSyncCompleted
        {
            get
            {
                return metadataReceived && progressBatchReceived && currentStatusReceived;
            }
        }

        /// <summary>
        /// 🔥 추가: 초기 동기화 완료까지 대기하는 코루틴
        /// </summary>
        public System.Collections.IEnumerator WaitUntilSynced(float timeoutSeconds = 15f)
        {
            float elapsed = 0f;
            while (!IsInitialSyncCompleted && elapsed < timeoutSeconds)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (elapsed >= timeoutSeconds)
            {
                Debug.LogWarning($"[UserDataCache] WaitUntilSynced 타임아웃 (metadata={metadataReceived}, batch={progressBatchReceived}, status={currentStatusReceived})");
            }
            else
            {
                Debug.Log($"[UserDataCache] 초기 동기화 완료 (elapsed={elapsed:F1}s)");
            }
        }

        private void RecomputeAndCacheMaxStageCompleted()
        {
            int computed = 0;
            foreach (var kv in stageProgressCache)
            {
                var p = kv.Value;
                // 🔥 수정: isCompleted AND starsEarned >= 1 조건으로 변경 (GameEndResult 규칙 준수)
                if (p != null && p.isCompleted && p.starsEarned >= 1 && kv.Key > computed)
                    computed = kv.Key;
            }

            if (computed > cachedMaxStageCompleted)
            {
                cachedMaxStageCompleted = computed;
                Debug.Log($"[UserDataCache] cachedMaxStageCompleted 갱신: {cachedMaxStageCompleted}");
            }

            // 서버 프로필보다 크면 프로필도 상향 보정(로컬 표시/언락용)
            if (currentUser != null && computed > currentUser.maxStageCompleted)
            {
                currentUser.maxStageCompleted = computed;
                SaveUserDataToDisk();
                OnUserDataUpdated?.Invoke(currentUser);
                Debug.Log($"[UserDataCache] currentUser.maxStageCompleted 상향 반영: {currentUser.maxStageCompleted}");
            }
        }

        void Awake()
        {
            // Migration Plan: Remove DontDestroyOnLoad - SingleCore scene management
            if (Instance == null)
            {
                Instance = this;
                LoadCacheFromDisk();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Initialize UserDataCache (Migration Plan)
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            SetupHttpApiEventHandlers();
            
            // 🔥 추가: 이미 로그인된 사용자가 있는지 확인하고 동기화
            CheckExistingLoginAndSync();
            
            isInitialized = true;

            Debug.Log("[UserDataCache] Initialized for SingleCore");
        }

        /// <summary>
        /// 🔥 추가: 기존 로그인 상태 확인 및 동기화
        /// </summary>
        private void CheckExistingLoginAndSync()
        {
            // SessionManager가 이미 로그인 상태인지 확인
            if (App.Core.SessionManager.Instance != null && App.Core.SessionManager.Instance.IsLoggedIn)
            {
                Debug.Log("[UserDataCache] 기존 로그인 상태 감지 - 사용자 정보 동기화 시작");
                
                // HttpApiClient가 준비될 때까지 대기한 후 동기화
                StartCoroutine(DelayedSyncWithExistingLogin());
            }
        }

        /// <summary>
        /// 🔥 추가: 기존 로그인 상태와 지연 동기화
        /// </summary>
        private System.Collections.IEnumerator DelayedSyncWithExistingLogin()
        {
            // HttpApiClient가 준비될 때까지 대기
            while (App.Network.HttpApiClient.Instance == null)
            {
                yield return new WaitForSeconds(0.1f);
            }

            // SessionManager에서 사용자 정보 가져와서 동기화
            if (App.Core.SessionManager.Instance != null && App.Core.SessionManager.Instance.IsLoggedIn)
            {
                Debug.Log("[UserDataCache] SessionManager로부터 사용자 정보 동기화");
                
                // SessionManager의 사용자 정보로 UserDataCache 동기화
                SyncWithSessionManager();
            }
        }

        /// <summary>
        /// 🔥 추가: SessionManager와 동기화
        /// </summary>
        private void SyncWithSessionManager()
        {
            var sessionManager = App.Core.SessionManager.Instance;
            if (sessionManager == null || !sessionManager.IsLoggedIn)
            {
                Debug.LogWarning("[UserDataCache] SessionManager 로그인 상태가 아님");
                return;
            }

            // UserInfo 생성 및 설정
            var userInfo = new UserInfo
            {
                username = sessionManager.CachedId,
                display_name = sessionManager.DisplayName
            };

            // 로그인 상태 및 토큰 설정
            isLoggedIn = true;
            authToken = sessionManager.AuthToken;
            currentUser = userInfo;

            Debug.Log($"[UserDataCache] SessionManager 동기화 완료 - User: {userInfo.username}");
            
            // 이벤트 발생
            OnUserDataUpdated?.Invoke(userInfo);
            OnLoginStatusChanged?.Invoke();
        }

        /// <summary>
        /// Check if initialized (Migration Plan)
        /// </summary>
        public bool IsInitialized => isInitialized;

        /// <summary>
        /// Cleanup for scene unload (Migration Plan)
        /// </summary>
        public void Cleanup()
        {
            if (enablePersistentCache)
            {
                SaveCacheToDisk();
            }

            CleanupHttpApiEventHandlers();
            isInitialized = false;

            Debug.Log("[UserDataCache] Cleaned up for scene unload");
        }

        /// <summary>
        /// Sync with server (Migration Plan)
        /// </summary>
        public void SyncWithServer()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[UserDataCache] SyncWithServer called but not initialized");
                return;
            }

            LoadInitialDataFromServer();
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

            CleanupHttpApiEventHandlers();
        }

        /// <summary>
        /// HTTP API 이벤트 핸들러 설정
        /// </summary>
        private void SetupHttpApiEventHandlers()
        {
            // HttpApiClient가 늦게 초기화될 수 있으므로 재시도
            StartCoroutine(SetupHttpApiEventHandlersCoroutine());
        }

        private System.Collections.IEnumerator SetupHttpApiEventHandlersCoroutine()
        {
            // HttpApiClient 인스턴스가 준비될 때까지 대기
            while (HttpApiClient.Instance == null)
            {
                yield return new WaitForSeconds(0.1f);
            }

            var httpClient = HttpApiClient.Instance;

            // 진행도 업데이트 이벤트 구독
            httpClient.OnBatchProgressReceived += OnBatchProgressReceived;
            httpClient.OnCurrentStatusReceived += OnCurrentStatusReceived; // 🔥 추가: current_status 구독
            httpClient.OnStageProgressReceived += OnStageProgressReceived;
            httpClient.OnStageCompleteResponse += OnStageCompleteResponse;

            // 🔥 수정: 사용자 프로필 업데이트 이벤트 구독 추가
            httpClient.OnUserProfileReceived += OnUserProfileReceived;
            
            // 🔥 추가: 로그인 시 사용자 정보 수신 이벤트 구독
            httpClient.OnUserInfoReceived += OnUserInfoReceived;

        }

        /// <summary>
        /// HTTP API 이벤트 핸들러 정리
        /// </summary>
        private void CleanupHttpApiEventHandlers()
        {
            if (HttpApiClient.Instance != null)
            {
                var httpClient = HttpApiClient.Instance;
                httpClient.OnBatchProgressReceived -= OnBatchProgressReceived;
                httpClient.OnCurrentStatusReceived -= OnCurrentStatusReceived; // 🔥 추가: 구독 해제
                httpClient.OnStageProgressReceived -= OnStageProgressReceived;
                httpClient.OnStageCompleteResponse -= OnStageCompleteResponse;

                // 🔥 수정: 사용자 프로필 업데이트 이벤트 구독 해제 추가
                httpClient.OnUserProfileReceived -= OnUserProfileReceived;
                
                // 🔥 추가: 로그인 시 사용자 정보 수신 이벤트 구독 해제
                httpClient.OnUserInfoReceived -= OnUserInfoReceived;
            }
        }

        // ========================================
        // 사용자 인증 관리
        // ========================================

        /// <summary>
        /// 🔥 새로운 메서드: 인증 토큰만 설정 (순수 로그인)
        /// </summary>
        public void SetAuthToken(string token, string username)
        {
            authToken = token;
            isLoggedIn = true;


            // HTTP API 토큰 설정
            if (!string.IsNullOrEmpty(token) && HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.SetAuthToken(token, GetUserIdFromUsername(username));
            }

            OnLoginStatusChanged?.Invoke();

            // Shared.Models.CacheManager 동기화 트리거
            if (CacheManager.Instance != null)
            {
                Debug.Log("[UserDataCache] 토큰 설정 후 Shared.Models.CacheManager 동기화 트리거");
                CacheManager.Instance.ForceFullSync();
            }
        }

        /// <summary>
        /// 🔥 새로운 메서드: 프로필 정보 설정 (상세 사용자 정보)
        /// </summary>
        public void SetUserProfile(UserInfo userInfo)
        {
            Debug.Log($"[UserDataCache] SetUserProfile 호출 - 현재 사용자: {currentUser?.username ?? "null"}, 새 사용자: {userInfo.username}");
            Debug.Log($"[UserDataCache] 현재 maxStageCompleted: {currentUser?.maxStageCompleted ?? -1}, 새 maxStageCompleted: {userInfo.maxStageCompleted}");

            bool isMaxStageChanged = currentUser?.maxStageCompleted != userInfo.maxStageCompleted;
            bool isFirstLogin = currentUser == null;

            Debug.Log($"[UserDataCache] isFirstLogin: {isFirstLogin}, isMaxStageChanged: {isMaxStageChanged}");

            currentUser = userInfo;


            SaveUserDataToDisk();
            OnUserDataUpdated?.Invoke(currentUser);

            // 🔥 추가: max_stage_completed 변경시 스테이지 버튼 새로고침 트리거
            if (isMaxStageChanged)
            {
                OnUserDataUpdated?.Invoke(currentUser); // 추가 이벤트 발생으로 UI 새로고침 촉진
            }

            // 🔥 수정: 프로필 설정 후 자동으로 초기 데이터 로드 (첫 로그인시 또는 진행도 변경시 또는 메타데이터 없음)
            bool hasNoMetadata = stageMetadataCache == null || stageMetadataCache.Length == 0;
            Debug.Log($"[UserDataCache] SetUserProfile 조건 확인 - isFirstLogin: {isFirstLogin}, isMaxStageChanged: {isMaxStageChanged}, hasNoMetadata: {hasNoMetadata}");

            if (isMaxStageChanged || isFirstLogin || hasNoMetadata)
            {
                Debug.Log($"[UserDataCache] 초기 데이터 로드 시작 - isFirstLogin: {isFirstLogin}, isMaxStageChanged: {isMaxStageChanged}, hasNoMetadata: {hasNoMetadata}");
                LoadInitialDataFromServer();

                // Shared.Models.CacheManager 동기화 트리거
                if (CacheManager.Instance != null)
                {
                    Debug.Log("[UserDataCache] 프로필 설정 후 Shared.Models.CacheManager 동기화 트리거");
                    CacheManager.Instance.ForceFullSync();
                }
            }
        }

        /// <summary>
        /// 사용자 로그인 처리 (기존 호환성 유지)
        /// </summary>
        public void LoginUser(UserInfo userInfo, string token = null)
        {
            currentUser = userInfo;
            authToken = token;
            isLoggedIn = true;


            // HTTP API 토큰 설정
            if (!string.IsNullOrEmpty(token) && HttpApiClient.Instance != null)
            {
                // 사용자 ID는 userInfo에서 추출하거나 별도로 관리 필요
                HttpApiClient.Instance.SetAuthToken(token, GetUserIdFromUserInfo(userInfo));

                // 로그인 후 자동으로 데이터 로드
                LoadInitialDataFromServer();
            }

            SaveUserDataToDisk();
            OnUserDataUpdated?.Invoke(currentUser);
            OnLoginStatusChanged?.Invoke();

            // Shared.Models.CacheManager 동기화 트리거
            if (CacheManager.Instance != null)
            {
                Debug.Log("[UserDataCache] 로그인 후 Shared.Models.CacheManager 동기화 트리거");
                CacheManager.Instance.ForceFullSync();
            }
        }

        /// <summary>
        /// 사용자 로그아웃 처리
        /// </summary>
        public void LogoutUser()
        {

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
        /// 현재 사용자 ID 반환
        /// </summary>
        public string GetCurrentUserId()
        {
            return currentUser?.username;
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
        /// 로그인 후 서버로부터 초기 데이터 로드
        /// </summary>
        private void LoadInitialDataFromServer()
        {
            if (HttpApiClient.Instance != null)
            {
                Debug.Log("[UserDataCache] 초기 서버 데이터 로드 시작");

                // 1. 스테이지 메타데이터 로드
                HttpApiClient.Instance.GetStageMetadata();
                Debug.Log("[UserDataCache] 스테이지 메타데이터 요청 전송");

                // 2. 사용자 진행도 일괄 로드 (중복 방지)
                if (!isBatchProgressLoading)
                {
                    isBatchProgressLoading = true;
                    HttpApiClient.Instance.GetBatchProgress();
                    Debug.Log("[UserDataCache] 일괄 진행도 요청 전송");
                }
                else
                {
                    Debug.Log("[UserDataCache] 일괄 진행도 로딩 중복 방지");
                }

                // 3. 사용자 프로필 로드 제거 - 로그인 시 이미 AuthUserData로 받음 (중복 호출 방지)

            }
            else
            {
                Debug.LogWarning("[UserDataCache] HttpApiClient가 null이어서 데이터 로드 실패");
            }
        }

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
            RecomputeAndCacheMaxStageCompleted();

            OnStageProgressUpdated?.Invoke(progress);
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

            // 진행도가 없으면 null 반환 (기본값 대신)
            // UI에서 null 체크를 통해 데이터가 아직 로드되지 않았음을 알 수 있음
            return null;
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

        }

        /// <summary>
        /// 최대 클리어 스테이지 번호 반환 (캐시된 개별 진행도 기반)
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

            // 🔥 핵심 수정: cachedMaxStageCompleted도 초기화!
            cachedMaxStageCompleted = 0;
            Debug.Log("[UserDataCache] cachedMaxStageCompleted 초기화됨: 0");

            // 🔥 추가: 동기화 상태 초기화
            ResetSyncState();

            if (enablePersistentCache)
            {
                PlayerPrefs.DeleteKey("UserDataCache_Progress");
                PlayerPrefs.DeleteKey("UserDataCache_StageData");
                PlayerPrefs.DeleteKey("UserDataCache_UserInfo");
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 🔥 추가: 동기화 상태 초기화 (사용자 전환 시 호출)
        /// </summary>
        private void ResetSyncState()
        {
            metadataReceived = false;
            progressBatchReceived = false;
            currentStatusReceived = false;
            Debug.Log("[UserDataCache] 동기화 상태 초기화됨");
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

            // 메타데이터 검증 및 로깅
            if (metadata != null)
            {
                Debug.Log($"[UserDataCache] 🔥 스테이지 메타데이터 설정 완료: {metadata.Length}개 (타임아웃 방지)");

                // 간략한 메타데이터 로깅
                for (int i = 0; i < Math.Min(5, metadata.Length); i++)
                {
                    var stage = metadata[i];
                    Debug.Log($"[UserDataCache] 메타데이터 샘플: 스테이지 {stage.n}, 난이도={stage.d}, 목표점수={stage.o}");
                }

                if (metadata.Length > 5)
                {
                    Debug.Log($"[UserDataCache] ... 및 {metadata.Length - 5}개 더");
                }
            }
            else
            {
                Debug.LogWarning($"[UserDataCache] 스테이지 메타데이터 설정 - 데이터 없음");
            }

            // 🔥 추가: 메타데이터 수신 플래그 설정
            metadataReceived = true;
            Debug.Log("[UserDataCache] 메타데이터 동기화 완료");

            OnStageMetadataUpdated?.Invoke(metadata);
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
        public HttpApiClient.CompactStageMetadata GetStageMetadata(int stageNumber)
        {
            if (stageMetadataCache != null)
            {
                foreach (var metadata in stageMetadataCache)
                {
                    if (metadata.n == stageNumber)
                    {
                        // 디버그 정보 추가
                        if (metadata.HasInitialBoardState)
                        {
                            var boardData = metadata.GetBoardData();
                            string formatType = metadata.ibs.HasBoardData ? "INTEGER[]" : "Empty";
                            Debug.Log($"[UserDataCache] 스테이지 {stageNumber} 메타데이터 반환: {formatType} 형식, {boardData.Length}개 위치");
                        }

                        return metadata;
                    }
                }
            }

            Debug.LogWarning($"[UserDataCache] 스테이지 {stageNumber} 메타데이터를 찾을 수 없음");
            return default(HttpApiClient.CompactStageMetadata);
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
        /// 🔥 추가: Username에서 사용자 ID 추출 (임시 구현)
        /// </summary>
        private int GetUserIdFromUsername(string username)
        {
            // 임시로 username 해시코드 사용
            return Mathf.Abs(username.GetHashCode());
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
        // HTTP API 이벤트 핸들러
        // ========================================

        /// <summary>
        /// 일괄 진행도 수신 처리
        /// </summary>
        private void OnBatchProgressReceived(HttpApiClient.CompactUserProgress[] progressArray)
        {
            Debug.Log($"[UserDataCache] 📥 OnBatchProgressReceived 호출됨!");

            // 중복 요청 방지 플래그 초기화
            isBatchProgressLoading = false;

            if (progressArray != null && progressArray.Length > 0)
            {
                Debug.Log($"[UserDataCache] 일괄 진행도 수신: {progressArray.Length}개 (중복 방지 플래그 초기화됨)");

                foreach (var compactProgress in progressArray)
                {
                    Debug.Log($"[UserDataCache] 처리 중: 스테이지 {compactProgress.n} (완료={compactProgress.c}, 별={compactProgress.s})");

                    var networkProgress = new NetworkUserStageProgress
                    {
                        stageNumber = compactProgress.n,
                        isCompleted = compactProgress.c,
                        starsEarned = compactProgress.s,
                        bestScore = compactProgress.bs,
                        bestCompletionTime = compactProgress.bt,
                        totalAttempts = compactProgress.a,
                        successfulAttempts = compactProgress.c ? compactProgress.a : 0, // 추정값
                        lastPlayedAt = System.DateTime.Now
                    };

                    Debug.Log($"[UserDataCache] API 스테이지 진행도 업데이트: {compactProgress.n} (별: {compactProgress.s})");
                    SetStageProgress(networkProgress);
                }
                RecomputeAndCacheMaxStageCompleted();
                
                // 🔥 추가: 진행도 배치 수신 플래그 설정
                progressBatchReceived = true;
                Debug.Log("[UserDataCache] 진행도 배치 동기화 완료");
                
                Debug.Log($"[UserDataCache] ✅ 일괄 진행도 캐시 완료 - 총 {progressArray.Length}개 처리됨");
            }
            else
            {
                Debug.LogWarning($"[UserDataCache] ❌ 일괄 진행도 수신 - 데이터 없음 (중복 방지 플래그 초기화됨)");
            }
        }

        /// <summary>
        /// 🔥 추가: 서버 current_status 수신 처리 (max_stage_completed 동기화)
        /// </summary>
        private void OnCurrentStatusReceived(App.Network.HttpApiClient.CurrentStatus currentStatus)
        {
            Debug.Log($"[UserDataCache] 📥 OnCurrentStatusReceived 호출됨! max_stage_completed={currentStatus.max_stage_completed}");

            // StageProgressManager에 max_stage_completed 설정
            var stageProgressManager = Features.Single.Core.StageProgressManager.Instance;
            if (stageProgressManager != null)
            {
                stageProgressManager.SetMaxStageCompleted(currentStatus.max_stage_completed);
                Debug.Log($"[UserDataCache] StageProgressManager에 서버 max_stage_completed={currentStatus.max_stage_completed} 설정 완료");
            }
            else
            {
                Debug.LogWarning("[UserDataCache] StageProgressManager.Instance가 null - max_stage_completed 설정 실패");
            }

            // currentUser 정보도 업데이트
            if (currentUser != null)
            {
                currentUser.maxStageCompleted = currentStatus.max_stage_completed;
                OnUserDataUpdated?.Invoke(currentUser);
                Debug.Log($"[UserDataCache] currentUser.maxStageCompleted을 {currentStatus.max_stage_completed}로 업데이트");
            }

            // 🔥 추가: 현재 상태 수신 플래그 설정
            currentStatusReceived = true;
            Debug.Log("[UserDataCache] 현재 상태 동기화 완료");
        }

        /// <summary>
        /// 개별 진행도 수신 처리
        /// </summary>
        private void OnStageProgressReceived(NetworkUserStageProgress progress)
        {
            if (progress != null)
            {
                Debug.Log($"[UserDataCache] 스테이지 {progress.stageNumber} 진행도 수신");
                SetStageProgress(progress);
            }
        }

        /// <summary>
        /// 스테이지 완료 응답 처리
        /// </summary>
        private void OnStageCompleteResponse(bool success, string message)
        {
            if (success)
            {
                Debug.Log($"[UserDataCache] 스테이지 완료 성공: {message}");
                // 진행도 새로고침 요청 (중복 방지)
                if (HttpApiClient.Instance != null && !isBatchProgressLoading)
                {
                    isBatchProgressLoading = true;
                    HttpApiClient.Instance.GetBatchProgress();
                    Debug.Log($"[UserDataCache] 스테이지 완료 후 진행도 새로고침 요청 (중복 방지됨)");
                }
                else if (isBatchProgressLoading)
                {
                    Debug.Log($"[UserDataCache] 스테이지 완료 후 진행도 새로고침 중복 방지 - 이미 로딩 중");
                }
            }
            else
            {
                Debug.LogWarning($"[UserDataCache] 스테이지 완료 실패: {message}");
            }
        }

        /// <summary>
        /// 🔥 추가: 사용자 프로필 수신 처리 (HTTP API → UserDataCache 연동)
        /// </summary>
        private void OnUserProfileReceived(HttpApiClient.UserProfile apiProfile)
        {
            if (apiProfile != null)
            {
                Debug.Log($"[UserDataCache] 📥 OnUserProfileReceived 호출됨!");
                Debug.Log($"[UserDataCache] API 프로필 데이터: username={apiProfile.username}, max_stage_completed={apiProfile.max_stage_completed}");

                // HttpApiClient.UserProfile을 UserInfo로 변환
                var userInfo = new UserInfo
                {
                    username = apiProfile.username,
                    display_name = apiProfile.display_name,
                    level = apiProfile.single_player_level,
                    maxStageCompleted = apiProfile.max_stage_completed,
                    totalGames = apiProfile.total_single_games,
                    averageScore = apiProfile.single_player_score
                };

                Debug.Log($"[UserDataCache] UserInfo 변환 완료: username={userInfo.username}, maxStageCompleted={userInfo.maxStageCompleted}");

                // 프로필 정보 설정 (기존 SetUserProfile 재사용)
                SetUserProfile(userInfo);
                cachedMaxStageCompleted = Mathf.Max(cachedMaxStageCompleted, userInfo.maxStageCompleted);

                Debug.Log($"[UserDataCache] ✅ 사용자 프로필 업데이트 완료 - max_stage_completed={userInfo.maxStageCompleted}");
            }
            else
            {
                Debug.LogWarning($"[UserDataCache] ❌ OnUserProfileReceived - apiProfile이 null입니다");
            }
        }

        /// <summary>
        /// 🔥 추가: 로그인 시 사용자 정보 수신 처리
        /// </summary>
        private void OnUserInfoReceived(App.Network.HttpApiClient.AuthUserData authData)
        {
            if (authData != null && authData.user != null)
            {
                Debug.Log($"[UserDataCache] 📥 OnUserInfoReceived 호출됨!");
                Debug.Log($"[UserDataCache] 로그인 사용자 데이터: username={authData.user.username}, user_id={authData.user.user_id}, max_stage_completed={authData.user.max_stage_completed}");

                // AuthUserData.user를 UserInfo로 변환 (실제 필드명 사용)
                var userInfo = new UserInfo
                {
                    username = authData.user.username,
                    display_name = authData.user.display_name,
                    level = authData.user.single_player_level,
                    maxStageCompleted = authData.user.max_stage_completed,
                    totalGames = 0, // 기본값
                    wins = 0, // 기본값
                    losses = 0, // 기본값
                    averageScore = 0, // 기본값
                    isOnline = true,
                    status = "로비"
                };

                // UserDataCache에 사용자 정보 및 토큰 직접 설정
                currentUser = userInfo;
                authToken = authData.token;
                isLoggedIn = true;

                // 🔥 추가: StageProgressManager에 max_stage_completed 동기화
                var stageProgressManager = Features.Single.Core.StageProgressManager.Instance;
                if (stageProgressManager != null)
                {
                    stageProgressManager.SetMaxStageCompleted(userInfo.maxStageCompleted);
                    Debug.Log($"[UserDataCache] StageProgressManager에 max_stage_completed={userInfo.maxStageCompleted} 설정 완료");
                }

                // 데이터 저장 및 이벤트 발생
                SaveUserDataToDisk();
                OnUserDataUpdated?.Invoke(currentUser);

                Debug.Log($"[UserDataCache] ✅ 로그인 사용자 정보 설정 완료 - username={userInfo.username}, max_stage_completed={userInfo.maxStageCompleted}");
            }
            else
            {
                Debug.LogWarning($"[UserDataCache] ❌ OnUserInfoReceived - authData 또는 user가 null입니다");
            }
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