using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace BlokusUnity.Network
{
    /// <summary>
    /// HTTP API 클라이언트 - 싱글플레이어 전용
    /// TCP 대신 HTTP REST API 사용으로 리소스 효율성 극대화
    /// </summary>
    public class HttpApiClient : MonoBehaviour
    {
        [Header("API 서버 설정")]
        [SerializeField] private string apiBaseUrl = "http://localhost:8080/api";
        [SerializeField] private int requestTimeoutSeconds = 10;
        
        // 인증 토큰
        private string authToken;
        private int currentUserId;
        
        // 오프라인 큐 (네트워크 복구시 재시도)
        private Queue<PendingRequest> offlineQueue = new Queue<PendingRequest>();
        private bool isOnline = true;
        
        // 싱글톤
        public static HttpApiClient Instance { get; private set; }
        
        // 이벤트
        public event System.Action<StageData> OnStageDataReceived;
        public event System.Action<UserStageProgress> OnStageProgressReceived;
        public event System.Action<bool, string> OnStageCompleteResponse;
        public event System.Action<UserStats> OnUserStatsReceived;
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeFromEnvironment();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        // ========================================
        // 환경 설정
        // ========================================
        
        private void InitializeFromEnvironment()
        {
            string envApiUrl = Environment.GetEnvironmentVariable("BLOKUS_API_URL");
            if (!string.IsNullOrEmpty(envApiUrl))
            {
                apiBaseUrl = envApiUrl;
                Debug.Log($"API URL 환경변수 설정: {apiBaseUrl}");
            }
        }
        
        /// <summary>
        /// 인증 토큰 설정 (TCP 로그인 성공시 호출)
        /// </summary>
        public void SetAuthToken(string token, int userId)
        {
            authToken = token;
            currentUserId = userId;
            Debug.Log($"HTTP API 인증 설정 완료: User {userId}");
        }
        
        // ========================================
        // HTTP 요청 기본 메서드
        // ========================================
        
        /// <summary>
        /// GET 요청
        /// </summary>
        private IEnumerator SendGetRequest<T>(string endpoint, System.Action<T> onSuccess, System.Action<string> onError = null)
        {
            string url = $"{apiBaseUrl}/{endpoint}";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                // 인증 헤더 추가
                if (!string.IsNullOrEmpty(authToken))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {authToken}");
                }
                
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = requestTimeoutSeconds;
                
                Debug.Log($"HTTP GET: {url}");
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"HTTP 응답: {jsonResponse}");
                    
                    try
                    {
                        T data = JsonUtility.FromJson<T>(jsonResponse);
                        onSuccess?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"JSON 파싱 오류: {ex.Message}");
                        onError?.Invoke($"응답 파싱 실패: {ex.Message}");
                    }
                }
                else
                {
                    string errorMsg = $"HTTP 오류: {request.error} (코드: {request.responseCode})";
                    Debug.LogError(errorMsg);
                    
                    // 오프라인 처리
                    if (request.result == UnityWebRequest.Result.ConnectionError)
                    {
                        HandleOfflineMode();
                    }
                    
                    onError?.Invoke(errorMsg);
                }
            }
        }
        
        /// <summary>
        /// POST 요청
        /// </summary>
        private IEnumerator SendPostRequest<T>(string endpoint, object requestData, System.Action<T> onSuccess, System.Action<string> onError = null)
        {
            string url = $"{apiBaseUrl}/{endpoint}";
            string jsonData = JsonUtility.ToJson(requestData);
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                
                // 헤더 설정
                if (!string.IsNullOrEmpty(authToken))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {authToken}");
                }
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = requestTimeoutSeconds;
                
                Debug.Log($"HTTP POST: {url}, 데이터: {jsonData}");
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"HTTP 응답: {jsonResponse}");
                    
                    if (onSuccess != null)
                    {
                        try
                        {
                            T data = JsonUtility.FromJson<T>(jsonResponse);
                            onSuccess.Invoke(data);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"JSON 파싱 오류: {ex.Message}");
                            onError?.Invoke($"응답 파싱 실패: {ex.Message}");
                        }
                    }
                }
                else
                {
                    string errorMsg = $"HTTP 오류: {request.error} (코드: {request.responseCode})";
                    Debug.LogError(errorMsg);
                    
                    // 오프라인 처리
                    if (request.result == UnityWebRequest.Result.ConnectionError)
                    {
                        EnqueueOfflineRequest(endpoint, requestData, "POST");
                    }
                    
                    onError?.Invoke(errorMsg);
                }
            }
        }
        
        // ========================================
        // 싱글플레이어 API 메서드들
        // ========================================
        
        /// <summary>
        /// 스테이지 데이터 요청
        /// </summary>
        public void GetStageData(int stageNumber)
        {
            StartCoroutine(SendGetRequest<StageDataResponse>(
                $"stages/{stageNumber}",
                response => OnStageDataReceived?.Invoke(response.data),
                error => Debug.LogWarning($"스테이지 {stageNumber} 데이터 요청 실패: {error}")
            ));
        }
        
        /// <summary>
        /// 사용자 스테이지 진행도 요청
        /// </summary>
        public void GetUserProgress(int stageNumber)
        {
            StartCoroutine(SendGetRequest<ProgressResponse>(
                $"users/{currentUserId}/progress/{stageNumber}",
                response => OnStageProgressReceived?.Invoke(response.progress),
                error => Debug.LogWarning($"진행도 요청 실패: {error}")
            ));
        }
        
        /// <summary>
        /// 일괄 진행도 요청
        /// </summary>
        public void GetBatchProgress(int startStage, int endStage)
        {
            StartCoroutine(SendGetRequest<BatchProgressResponse>(
                $"users/{currentUserId}/progress?start={startStage}&end={endStage}",
                response => {
                    foreach (var progress in response.progressList)
                    {
                        OnStageProgressReceived?.Invoke(progress);
                    }
                },
                error => Debug.LogWarning($"일괄 진행도 요청 실패: {error}")
            ));
        }
        
        /// <summary>
        /// 스테이지 완료 보고
        /// </summary>
        public void CompleteStage(int stageNumber, int stars, int score, int completionTime)
        {
            var requestData = new StageCompleteRequest
            {
                stageNumber = stageNumber,
                stars = stars,
                score = score,
                completionTime = completionTime,
                completedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
            
            StartCoroutine(SendPostRequest<StageCompleteResponse>(
                $"stages/{stageNumber}/complete",
                requestData,
                response => OnStageCompleteResponse?.Invoke(response.success, response.message),
                error => {
                    Debug.LogWarning($"스테이지 완료 보고 실패: {error}");
                    OnStageCompleteResponse?.Invoke(false, error);
                }
            ));
        }
        
        /// <summary>
        /// 사용자 통계 요청
        /// </summary>
        public void GetUserStats()
        {
            StartCoroutine(SendGetRequest<UserStatsResponse>(
                $"users/{currentUserId}/stats",
                response => OnUserStatsReceived?.Invoke(response.stats),
                error => Debug.LogWarning($"사용자 통계 요청 실패: {error}")
            ));
        }
        
        // ========================================
        // 오프라인 지원
        // ========================================
        
        private void HandleOfflineMode()
        {
            if (isOnline)
            {
                isOnline = false;
                Debug.LogWarning("오프라인 모드로 전환");
                
                // 주기적으로 연결 복구 시도
                StartCoroutine(CheckConnectionRecovery());
            }
        }
        
        private IEnumerator CheckConnectionRecovery()
        {
            while (!isOnline)
            {
                yield return new WaitForSeconds(30f); // 30초마다 확인
                
                // 간단한 핑 테스트
                using (UnityWebRequest ping = UnityWebRequest.Get($"{apiBaseUrl}/health"))
                {
                    ping.timeout = 5;
                    yield return ping.SendWebRequest();
                    
                    if (ping.result == UnityWebRequest.Result.Success)
                    {
                        isOnline = true;
                        Debug.Log("네트워크 연결 복구됨");
                        ProcessOfflineQueue();
                    }
                }
            }
        }
        
        private void EnqueueOfflineRequest(string endpoint, object data, string method)
        {
            offlineQueue.Enqueue(new PendingRequest
            {
                endpoint = endpoint,
                data = data,
                method = method,
                timestamp = DateTime.Now
            });
        }
        
        private void ProcessOfflineQueue()
        {
            Debug.Log($"오프라인 큐 처리: {offlineQueue.Count}개 요청");
            
            while (offlineQueue.Count > 0)
            {
                var request = offlineQueue.Dequeue();
                
                // 5분 이상 된 요청은 폐기
                if ((DateTime.Now - request.timestamp).TotalMinutes > 5)
                {
                    Debug.LogWarning($"만료된 요청 폐기: {request.endpoint}");
                    continue;
                }
                
                // 재시도
                if (request.method == "POST")
                {
                    StartCoroutine(SendPostRequest<object>(request.endpoint, request.data, null));
                }
            }
        }
        
        // ========================================
        // 데이터 구조체들
        // ========================================
        
        [System.Serializable]
        public class StageDataResponse
        {
            public StageData data;
        }
        
        [System.Serializable]
        public class ProgressResponse
        {
            public UserStageProgress progress;
        }
        
        [System.Serializable]
        public class BatchProgressResponse
        {
            public UserStageProgress[] progressList;
        }
        
        [System.Serializable]
        public class StageCompleteRequest
        {
            public int stageNumber;
            public int stars;
            public int score;
            public int completionTime;
            public string completedAt;
        }
        
        [System.Serializable]
        public class StageCompleteResponse
        {
            public bool success;
            public string message;
            public int newMaxStage;
        }
        
        [System.Serializable]
        public class UserStatsResponse
        {
            public UserStats stats;
        }
        
        [System.Serializable]
        public class UserStats
        {
            public int singlePlayerLevel;
            public int maxStageCompleted;
            public int totalSingleGames;
            public long singlePlayerScore;
        }
        
        private class PendingRequest
        {
            public string endpoint;
            public object data;
            public string method;
            public DateTime timestamp;
        }
    }
}