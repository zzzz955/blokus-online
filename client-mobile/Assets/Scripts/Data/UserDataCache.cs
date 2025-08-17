using System;
using System.Collections.Generic;
using System.Linq;
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
        
        // 중복 요청 방지
        private bool isBatchProgressLoading = false;
        private bool isStageMetadataLoading = false;
        
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
                
                // 루트 GameObject로 이동 (DontDestroyOnLoad 적용을 위해)
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                
                LoadCacheFromDisk();
                SetupHttpApiEventHandlers();
                Debug.Log("UserDataCache 초기화 완료 - DontDestroyOnLoad 적용됨");
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
            httpClient.OnStageProgressReceived += OnStageProgressReceived;
            httpClient.OnStageCompleteResponse += OnStageCompleteResponse;
            
            Debug.Log("[UserDataCache] HTTP API 이벤트 핸들러 설정 완료");
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
                httpClient.OnStageProgressReceived -= OnStageProgressReceived;
                httpClient.OnStageCompleteResponse -= OnStageCompleteResponse;
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
            
            Debug.Log($"인증 토큰 설정: {username}");
            
            // HTTP API 토큰 설정
            if (!string.IsNullOrEmpty(token) && HttpApiClient.Instance != null)
            {
                HttpApiClient.Instance.SetAuthToken(token, GetUserIdFromUsername(username));
            }
            
            OnLoginStatusChanged?.Invoke();
        }
        
        /// <summary>
        /// 🔥 새로운 메서드: 프로필 정보 설정 (상세 사용자 정보)
        /// </summary>
        public void SetUserProfile(UserInfo userInfo)
        {
            currentUser = userInfo;
            
            Debug.Log($"사용자 프로필 설정: {userInfo.username} (최대 스테이지: {userInfo.maxStageCompleted})");
            
            SaveUserDataToDisk();
            OnUserDataUpdated?.Invoke(currentUser);
            
            // 🔥 추가: 프로필 설정 후 자동으로 초기 데이터 로드
            LoadInitialDataFromServer();
        }
        
        /// <summary>
        /// 사용자 로그인 처리 (기존 호환성 유지)
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
                
                // 로그인 후 자동으로 데이터 로드
                LoadInitialDataFromServer();
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
        /// 로그인 후 서버로부터 초기 데이터 로드
        /// </summary>
        private void LoadInitialDataFromServer()
        {
            if (HttpApiClient.Instance != null)
            {
                Debug.Log("[UserDataCache] 로그인 후 서버 데이터 로드 시작");
                
                // 1. 스테이지 메타데이터 로드 (중복 방지)
                if (!isStageMetadataLoading)
                {
                    isStageMetadataLoading = true;
                    HttpApiClient.Instance.GetStageMetadata();
                    Debug.Log("[UserDataCache] 스테이지 메타데이터 요청 (중복 방지됨)");
                }
                else
                {
                    Debug.Log("[UserDataCache] 스테이지 메타데이터 요청 중복 방지 - 이미 로딩 중");
                }
                
                // 2. 사용자 진행도 일괄 로드 (중복 방지)
                if (!isBatchProgressLoading)
                {
                    isBatchProgressLoading = true;
                    HttpApiClient.Instance.GetBatchProgress();
                    Debug.Log("[UserDataCache] 일괄 진행도 요청 (중복 방지됨)");
                }
                else
                {
                    Debug.Log("[UserDataCache] 일괄 진행도 요청 중복 방지 - 이미 로딩 중");
                }
                
                // 3. 사용자 프로필 로드 제거 - 로그인 시 이미 AuthUserData로 받음 (중복 호출 방지)
                Debug.Log("[UserDataCache] 사용자 프로필은 로그인 시 이미 수신됨 - 중복 요청 방지");
                
                Debug.Log("[UserDataCache] 서버 데이터 로드 요청 완료");
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
            
            Debug.Log($"[UserDataCache] 스테이지 진행도 설정: {progress.stageNumber} (완료: {progress.isCompleted}, 별: {progress.starsEarned})");
            Debug.Log($"[UserDataCache] 현재 캐시 크기: {stageProgressCache.Count}개");
            Debug.Log($"[UserDataCache] OnStageProgressUpdated 이벤트 발생 - 구독자 수: {OnStageProgressUpdated?.GetInvocationList()?.Length ?? 0}");
            
            OnStageProgressUpdated?.Invoke(progress);
        }
        
        /// <summary>
        /// 스테이지 진행도 가져오기
        /// </summary>
        public NetworkUserStageProgress GetStageProgress(int stageNumber)
        {
            Debug.Log($"[UserDataCache] 스테이지 {stageNumber} 진행도 요청 - 현재 캐시 크기: {stageProgressCache.Count}개");
            
            // 캐시 내용 디버깅 (처음 몇 개만)
            if (stageProgressCache.Count > 0)
            {
                var keys = string.Join(", ", stageProgressCache.Keys.Take(5));
                Debug.Log($"[UserDataCache] 캐시된 스테이지들: {keys}{(stageProgressCache.Count > 5 ? "..." : "")}");
            }
            
            if (stageProgressCache.TryGetValue(stageNumber, out NetworkUserStageProgress progress))
            {
                Debug.Log($"[UserDataCache] 스테이지 {stageNumber} 진행도 캐시에서 반환: 완료={progress.isCompleted}, 별={progress.starsEarned}");
                return progress;
            }
            
            // 진행도가 없으면 null 반환 (기본값 대신)
            // UI에서 null 체크를 통해 데이터가 아직 로드되지 않았음을 알 수 있음
            Debug.Log($"[UserDataCache] 스테이지 {stageNumber} 진행도 캐시에 없음 - null 반환");
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
            
            Debug.Log($"일괄 스테이지 진행도 설정: {progressList.Count}개");
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
        
        /// <summary>
        /// 사용자 정보에서 최대 완료 스테이지 번호 반환 (서버 데이터 기반)
        /// </summary>
        public int GetMaxStageCompleted()
        {
            if (currentUser != null)
            {
                Debug.Log($"[UserDataCache] 서버 기반 최대 완료 스테이지: {currentUser.maxStageCompleted}");
                return currentUser.maxStageCompleted;
            }
            
            Debug.Log("[UserDataCache] 사용자 정보 없음 - 기본값 0 반환");
            return 0;
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
            // 중복 요청 방지 플래그 초기화
            isStageMetadataLoading = false;
            
            stageMetadataCache = metadata;
            
            // 메타데이터 검증 및 로깅
            if (metadata != null)
            {
                int newFormatCount = 0;
                int legacyFormatCount = 0;
                
                foreach (var stage in metadata)
                {
                    if (stage.HasInitialBoardState)
                    {
                        var boardData = stage.GetBoardData();
                        if (stage.ibs.HasBoardData)
                        {
                            newFormatCount++;
                            Debug.Log($"[UserDataCache] 스테이지 {stage.n}: 새로운 INTEGER[] 형식 ({boardData.Length}개 위치)");
                        }
                        else
                        {
                            legacyFormatCount++;
                            Debug.Log($"[UserDataCache] 스테이지 {stage.n}: 레거시 형식 ({boardData.Length}개 위치)");
                        }
                    }
                }
                
                Debug.Log($"[UserDataCache] 스테이지 메타데이터 설정 완료: {metadata.Length}개 총 (INTEGER[] {newFormatCount}개, Empty {legacyFormatCount}개) (중복 방지 플래그 초기화됨)");
            }
            else
            {
                Debug.Log($"[UserDataCache] 스테이지 메타데이터 설정 - 데이터 없음 (중복 방지 플래그 초기화됨)");
            }
            
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
                
                Debug.Log($"[UserDataCache] ✅ 일괄 진행도 캐시 완료 - 총 {progressArray.Length}개 처리됨");
            }
            else
            {
                Debug.LogWarning($"[UserDataCache] ❌ 일괄 진행도 수신 - 데이터 없음 (중복 방지 플래그 초기화됨)");
            }
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