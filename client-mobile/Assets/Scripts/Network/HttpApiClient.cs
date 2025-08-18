using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using BlokusUnity.Data;

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
        public string ApiBaseUrl => apiBaseUrl;
        [SerializeField] private int requestTimeoutSeconds = 10;
        
        // 인증 토큰
        private string authToken;
        private int currentUserId;
        
        // 오프라인 큐 (네트워크 복구시 재시도)
        private Queue<PendingRequest> offlineQueue = new Queue<PendingRequest>();
        private bool isOnline = true;
        
        // 싱글톤
        public static HttpApiClient Instance { get; private set; }
        
        // 이벤트 - API 응답용 StageData 사용
        public event System.Action<ApiStageData> OnStageDataReceived;
        public event System.Action<UserStageProgress> OnStageProgressReceived;
        public event System.Action<bool, string> OnStageCompleteResponse;
        public event System.Action<UserProfile> OnUserProfileReceived;
        public event System.Action<CompactStageMetadata[]> OnStageMetadataReceived;
        public event System.Action<CompactUserProgress[]> OnBatchProgressReceived;
        
        // 인증 이벤트 
        public event System.Action<bool, string, string> OnAuthResponse; // success, message, token
        public event System.Action<AuthUserData> OnUserInfoReceived;
        public event System.Action<string> OnOAuthRegisterRedirect;
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                
                // 루트 GameObject로 이동 (DontDestroyOnLoad 적용을 위해)
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                
                InitializeFromEnvironment();
                Debug.Log("HttpApiClient 초기화 완료 - DontDestroyOnLoad 적용됨");
            }
            else
            {
                Debug.Log("HttpApiClient 중복 인스턴스 제거");
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
        /// 인증 토큰 설정
        /// </summary>
        public void SetAuthToken(string token, int userId)
        {
            authToken = token;
            currentUserId = userId;
            isOnline = true;
            Debug.Log($"HTTP API 인증 설정 완료: User {userId}");
        }
        
        /// <summary>
        /// 인증 토큰 클리어
        /// </summary>
        public void ClearAuthToken()
        {
            authToken = null;
            currentUserId = 0;
            Debug.Log("HTTP API 인증 토큰 클리어됨");
        }
        
        /// <summary>
        /// 로그인 상태 확인
        /// </summary>
        public bool IsAuthenticated()
        {
            return !string.IsNullOrEmpty(authToken) && currentUserId > 0;
        }
        
        /// <summary>
        /// 현재 인증 토큰 반환
        /// </summary>
        public string GetAuthToken()
        {
            return authToken;
        }
        
        // ========================================
        // HTTP 요청 기본 메서드
        // ========================================
        
        /// <summary>
        /// GET 요청 - API 응답 구조에 맞게 업데이트
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
                        // API 응답은 { "success": true, "data": {...} } 구조
                        ApiResponse<T> apiResponse = JsonUtility.FromJson<ApiResponse<T>>(jsonResponse);
                        if (apiResponse.success)
                        {
                            onSuccess?.Invoke(apiResponse.data);
                        }
                        else
                        {
                            onError?.Invoke(apiResponse.message ?? "API 요청 실패");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"JSON 파싱 오류: {ex.Message}\nResponse: {jsonResponse}");
                        onError?.Invoke($"응답 파싱 실패: {ex.Message}");
                    }
                }
                else
                {
                    string errorMsg = GetUserFriendlyErrorMessage(request.responseCode, request.error);
                    Debug.LogError($"HTTP 오류: {request.error} (코드: {request.responseCode})");
                    
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
        /// POST 요청 - API 응답 구조에 맞게 업데이트
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
                            // API 응답은 { "success": true, "data": {...} } 구조
                            ApiResponse<T> apiResponse = JsonUtility.FromJson<ApiResponse<T>>(jsonResponse);
                            if (apiResponse.success)
                            {
                                onSuccess.Invoke(apiResponse.data);
                            }
                            else
                            {
                                onError?.Invoke(apiResponse.message ?? "API 요청 실패");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"JSON 파싱 오류: {ex.Message}\nResponse: {jsonResponse}");
                            onError?.Invoke($"응답 파싱 실패: {ex.Message}");
                        }
                    }
                }
                else
                {
                    string errorMsg = GetUserFriendlyErrorMessage(request.responseCode, request.error);
                    Debug.LogError($"HTTP 오류: {request.error} (코드: {request.responseCode})");
                    
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
        // 인증 API 메서드들
        // ========================================
        
        /// <summary>
        /// 로그인 요청 - 실제 API에 맞게 업데이트
        /// </summary>
        public void Login(string username, string password)
        {
            var loginData = new LoginRequest
            {
                username = username,
                password = password
            };
            
            StartCoroutine(SendPostRequest<AuthUserData>(
                "auth/login",
                loginData,
                response => {
                    // 성공시 토큰 저장 및 이벤트 발생
                    SetAuthToken(response.token, response.user.user_id);
                    OnAuthResponse?.Invoke(true, "로그인 성공", response.token);
                    
                    // 사용자 정보 이벤트 발생
                    OnUserInfoReceived?.Invoke(response);
                },
                error => OnAuthResponse?.Invoke(false, error, null)
            ));
        }
        
        /// <summary>
        /// 회원가입 요청 - OAuth 리다이렉트 방식
        /// </summary>
        public void Register()
        {
            var registerData = new RegisterRequest
            {
                app_callback = "blokus://auth/callback",
                user_agent = "Unity Mobile Client",
                device_id = SystemInfo.deviceUniqueIdentifier
            };
            
            StartCoroutine(SendPostRequest<OAuthRedirectData>(
                "auth/register",
                registerData,
                response => {
                    // OAuth 리다이렉트 URL 이벤트 발생
                    OnOAuthRegisterRedirect?.Invoke(response.redirect_url);
                },
                error => OnAuthResponse?.Invoke(false, error, null)
            ));
        }
        
        /// <summary>
        /// 게스트 로그인 요청 - 실제 API에 맞게 업데이트
        /// </summary>
        public void GuestLogin()
        {
            StartCoroutine(SendPostRequest<AuthUserData>(
                "auth/guest",
                new { }, // 빈 객체
                response => {
                    SetAuthToken(response.token, response.user.user_id);
                    OnAuthResponse?.Invoke(true, "게스트 로그인 성공", response.token);
                    OnUserInfoReceived?.Invoke(response);
                },
                error => OnAuthResponse?.Invoke(false, error, null)
            ));
        }
        
        /// <summary>
        /// 토큰 유효성 검증 - POST 요청으로 변경
        /// </summary>
        public void ValidateToken()
        {
            if (string.IsNullOrEmpty(authToken))
            {
                OnAuthResponse?.Invoke(false, "토큰이 없습니다.", null);
                return;
            }
            
            StartCoroutine(SendPostRequest<TokenValidationData>(
                "auth/validate",
                new { }, // 빈 객체, Authorization 헤더에서 토큰 확인
                response => {
                    if (response.valid)
                    {
                        OnAuthResponse?.Invoke(true, "토큰이 유효합니다.", authToken);
                    }
                    else
                    {
                        ClearAuthToken();
                        OnAuthResponse?.Invoke(false, "토큰이 만료되었습니다.", null);
                    }
                },
                error => {
                    ClearAuthToken();
                    OnAuthResponse?.Invoke(false, $"토큰 검증 실패: {error}", null);
                }
            ));
        }
        
        /// <summary>
        /// 로그아웃
        /// </summary>
        public void Logout()
        {
            if (!string.IsNullOrEmpty(authToken))
            {
                StartCoroutine(SendPostRequest<LogoutResponse>(
                    "auth/logout",
                    new { }, // 빈 객체
                    response => {
                        ClearAuthToken();
                        Debug.Log("로그아웃 완료");
                    },
                    error => {
                        // 로그아웃은 실패해도 로컬에서 토큰 클리어
                        ClearAuthToken();
                        Debug.LogWarning($"로그아웃 요청 실패: {error}");
                    }
                ));
            }
            else
            {
                ClearAuthToken();
            }
        }
        
        // ========================================
        // 싱글플레이어 API 메서드들
        // ========================================
        
        /// <summary>
        /// 스테이지 데이터 요청 - 실제 API 구조에 맞게 업데이트
        /// </summary>
        public void GetStageData(int stageNumber)
        {
            StartCoroutine(SendGetRequest<ApiStageData>(
                $"stages/{stageNumber}",
                response => OnStageDataReceived?.Invoke(response),
                error => Debug.LogWarning($"스테이지 {stageNumber} 데이터 요청 실패: {error}")
            ));
        }
        
        /// <summary>
        /// 사용자 스테이지 진행도 요청 - 실제 API 구조에 맞게 업데이트
        /// </summary>
        public void GetStageProgress(int stageNumber)
        {
            StartCoroutine(SendGetRequest<UserStageProgress>(
                $"stages/{stageNumber}/progress",
                response => OnStageProgressReceived?.Invoke(response),
                error => Debug.LogWarning($"스테이지 {stageNumber} 진행도 요청 실패: {error}")
            ));
        }
        
        /// <summary>
        /// 일괄 진행도 요청 - 실제 API 구조에 맞게 업데이트
        /// </summary>
        public void GetBatchProgress()
        {
            StartCoroutine(SendGetRequest<BatchProgressData>(
                "user/progress/batch",
                response => {
                    OnBatchProgressReceived?.Invoke(response.progress);
                },
                error => Debug.LogWarning($"일괄 진행도 요청 실패: {error}")
            ));
        }
        
        /// <summary>
        /// 스테이지 메타데이터 일괄 요청
        /// </summary>
        public void GetStageMetadata()
        {
            Debug.Log("[HttpApiClient] 스테이지 메타데이터 요청 시작");
            StartCoroutine(SendGetRequest<MetadataSyncResponse>(
                "user/sync/metadata",
                response => {
                    Debug.Log($"[HttpApiClient] 스테이지 메타데이터 수신: {response.stages?.Length ?? 0}개");
                    
                    // MetadataSyncResponse의 stages를 CompactStageMetadata로 변환
                    if (response.stages != null && response.stages.Length > 0)
                    {
                        var compactStages = ConvertToCompactMetadata(response.stages);
                        
                        // UserDataCache에 직접 저장
                        if (UserDataCache.Instance != null)
                        {
                            UserDataCache.Instance.SetStageMetadata(compactStages);
                            Debug.Log($"[HttpApiClient] 메타데이터 캐시에 저장 완료: {compactStages.Length}개");
                        }
                        
                        // 이벤트도 발생 (기존 구독자들을 위해)
                        OnStageMetadataReceived?.Invoke(compactStages);
                    }
                    else
                    {
                        Debug.LogWarning("[HttpApiClient] 수신된 스테이지 데이터가 없습니다");
                    }
                },
                error => Debug.LogWarning($"[HttpApiClient] 스테이지 메타데이터 요청 실패: {error}")
            ));
        }
        
        /// <summary>
        /// 스테이지 완료 보고 - 실제 API 구조에 맞게 업데이트
        /// </summary>
        public void CompleteStage(int stageNumber, int score, int completionTime, bool completed)
        {
            var requestData = new StageCompleteRequest
            {
                stage_number = stageNumber,
                score = score,
                completion_time = completionTime,
                completed = completed
            };
            
            StartCoroutine(SendPostRequest<StageCompleteData>(
                "stages/complete",
                requestData,
                response => {
                    string message = completed ? 
                        $"스테이지 {stageNumber} 완료! {response.stars_earned}별 획득" :
                        $"스테이지 {stageNumber} 시도 기록됨";
                    OnStageCompleteResponse?.Invoke(true, message);
                },
                error => {
                    Debug.LogWarning($"스테이지 완료 보고 실패: {error}");
                    OnStageCompleteResponse?.Invoke(false, error);
                }
            ));
        }
        
        /// <summary>
        /// 사용자 프로필 요청 - 실제 API 구조에 맞게 업데이트
        /// </summary>
        public void GetUserProfile()
        {
            StartCoroutine(SendGetRequest<UserProfile>(
                "user/profile",
                response => OnUserProfileReceived?.Invoke(response),
                error => Debug.LogWarning($"사용자 프로필 요청 실패: {error}")
            ));
        }
        
        /// <summary>
        /// 사용자 상세 통계 요청
        /// </summary>
        public void GetUserStats()
        {
            StartCoroutine(SendGetRequest<UserProfile>(
                "user/profile",
                response => OnUserProfileReceived?.Invoke(response),
                error => Debug.LogWarning($"사용자 프로필 요청 실패: {error}")
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
        // 데이터 구조체들 - 실제 API 응답 구조에 맞게 업데이트
        // ========================================
        
        // API 응답 기본 구조
        [System.Serializable]
        public class ApiResponse<T>
        {
            public bool success;
            public string message;
            public T data;
            public string error;
        }
        
        // 인증 관련 데이터 구조체들
        [System.Serializable]
        public class LoginRequest
        {
            public string username;
            public string password;
        }
        
        [System.Serializable]
        public class RegisterRequest
        {
            public string app_callback;
            public string user_agent;
            public string device_id;
        }
        
        [System.Serializable]
        public class AuthUserData
        {
            public UserData user;
            public string token;
            public string expires_in;
        }
        
        [System.Serializable]
        public class UserData
        {
            public int user_id;
            public string username;
            public int level;
            public int experience_points;
            public int single_player_level;
            public int max_stage_completed;
            public UserStatsData stats;
        }
        
        [System.Serializable]
        public class UserStatsData
        {
            public int total_games;
            public int wins;
            public int losses;
            public int total_score;
            public int best_score;
            public int win_rate;
        }
        
        [System.Serializable]
        public class OAuthRedirectData
        {
            public string redirect_url;
            public string registration_type;
            public OAuthInstructions instructions;
            public string[] flow_steps;
        }
        
        [System.Serializable]
        public class OAuthInstructions
        {
            public string ko;
            public string en;
        }
        
        [System.Serializable]
        public class TokenValidationData
        {
            public bool valid;
            public string username;
            public int user_id;
            public string issued_at;
            public string expires_at;
            public int remaining_time;
        }
        
        // 스테이지 관련 데이터 구조체들 - API 응답 구조에 맞춤
        [System.Serializable]
        public class ApiStageData
        {
            public int stage_number;
            public string title;
            public int difficulty;
            public int optimal_score;
            public int? time_limit;
            public int max_undo_count;
            public int[] available_blocks;
            public InitialBoardStateApi initial_board_state;
            public string[] hints;
            public string stage_description;
            public bool is_featured;
            public string thumbnail_url;  // DB stages 테이블의 thumbnail_url 필드
            
            /// <summary>
            /// Get unified board data from initial_board_state
            /// </summary>
            public int[] GetBoardData()
            {
                return initial_board_state?.GetBoardData() ?? new int[0];
            }
            
            /// <summary>
            /// Check if this stage has initial board state
            /// </summary>
            public bool HasInitialBoardState => initial_board_state != null && GetBoardData().Length > 0;
        }
        
        [System.Serializable]
        public class InitialBoardStateApi
        {
            // INTEGER[] format from database migration
            // Format: color_index * 400 + (row * 20 + col)
            // Colors: 검정(0), 파랑(1), 노랑(2), 빨강(3), 초록(4)
            public int[] boardPositions;
            
            /// <summary>
            /// Get board data in INTEGER[] format
            /// </summary>
            public int[] GetBoardData()
            {
                return boardPositions ?? new int[0];
            }
            
            /// <summary>
            /// Check if this has any board data
            /// </summary>
            public bool HasBoardData => boardPositions != null && boardPositions.Length > 0;
        }
        
        [System.Serializable]
        public class StageMetadataResponse
        {
            public CompactStageMetadata[] stages;
            public int total_count;
            public string last_updated;
        }

        [System.Serializable]
        public class CompactStageMetadata
        {
            public int n;           // stage_number
            public string t;        // title
            public int d;           // difficulty
            public int o;           // optimal_score
            public int tl;          // time_limit
            public string tu;       // thumbnail_url
            public string desc;     // description
            public int[] ab;        // available_blocks
            public int muc;         // max_undo_count
            public InitialBoardStateApi ibs;        // initial_board_state
            public string[] h;      // hints
            
            /// <summary>
            /// Get unified board data in new INTEGER[] format
            /// Priority: new format > legacy format > empty array
            /// </summary>
            public int[] GetBoardData()
            {
                if (ibs != null)
                {
                    return ibs.GetBoardData();
                }
                return new int[0];
            }
            
            /// <summary>
            /// Check if this stage has any initial board state
            /// </summary>
            public bool HasInitialBoardState => ibs != null && ibs.HasBoardData;
        }
        
        [System.Serializable]
        public class StageCompleteRequest
        {
            public int stage_number;
            public int score;
            public int completion_time;
            public bool completed;
        }
        
        [System.Serializable]
        public class StageCompleteData
        {
            public bool success;
            public int stars_earned;
            public bool is_new_best;
            public bool level_up;
            public string message;
        }
        
        // 사용자 관련 데이터 구조체들
        [System.Serializable]
        public class UserProfile
        {
            public string username;
            public int single_player_level;
            public int max_stage_completed;
            public int total_single_games;
            public int single_player_score; // 🔥 복원: DB가 bigint이므로 int로 복원
        }
        
        [System.Serializable]
        public class UserStats
        {
            public string username;
            public int single_player_level;
            public int max_stage_completed;
            public int total_single_games;
            public int single_player_score;
            public int total_stages_played;
            public int stages_completed;
            public int perfect_stages;
            public int average_score;
            public int completion_rate;
            public int success_rate;
            public int total_attempts;
            public int successful_attempts;
        }
        
        [System.Serializable]
        public class BatchProgressData
        {
            public CompactUserProgress[] progress;
            public CurrentStatus current_status;
            public int total_count;
            public string last_updated;
        }
        
        [System.Serializable]
        public class CompactUserProgress
        {
            public int n;    // stage_number
            public bool c;   // is_completed
            public int s;    // stars_earned
            public int bs;   // best_score
            public int bt;   // best_completion_time
            public int a;    // total_attempts
        }
        
        [System.Serializable]
        public class CurrentStatus
        {
            public int max_stage_completed;
            public int single_player_level;
            public int total_stars;
            public int completion_count;
        }
        
        [System.Serializable]
        public class LogoutResponse
        {
            public string message;
        }
        
        /// <summary>
        /// 사용자 친화적인 오류 메시지 생성
        /// </summary>
        private string GetUserFriendlyErrorMessage(long responseCode, string error)
        {
            switch (responseCode)
            {
                case 400: return "잘못된 요청입니다.";
                case 401: return "로그인이 필요합니다.";
                case 403: return "접근이 거부되었습니다.";
                case 404: return "요청한 정보를 찾을 수 없습니다.";
                case 500: return "서버 오류가 발생했습니다.";
                case 502: return "서버 연결에 실패했습니다.";
                case 503: return "서버가 일시적으로 사용할 수 없습니다.";
                default: return $"네트워크 오류가 발생했습니다. ({responseCode})";
            }
        }
        
        /// <summary>
        /// 스테이지 목록 메타데이터 요청
        /// </summary>
        public void GetStageList()
        {
            StartCoroutine(SendGetRequest<CompactStageMetadata[]>(
                "stages",
                response => {
                    Debug.Log($"스테이지 목록 수신: {response.Length}개");
                    OnStageListReceived?.Invoke(response);
                },
                error => Debug.LogError($"스테이지 목록 로드 실패: {error}")
            ));
        }
        
        /// <summary>
        /// 특정 스테이지의 사용자 진행도 요청
        /// </summary>
        public void GetUserProgress(int stageNumber)
        {
            StartCoroutine(SendGetRequest<CompactUserProgress>(
                $"user/progress/{stageNumber}",
                response => {
                    Debug.Log($"스테이지 {stageNumber} 진행도 수신");
                    OnUserProgressReceived?.Invoke(response);
                },
                error => Debug.LogError($"스테이지 진행도 로드 실패: {error}")
            ));
        }
        
        // 이벤트 추가
        public event System.Action<CompactStageMetadata[]> OnStageListReceived;
        public event System.Action<CompactUserProgress> OnUserProgressReceived;

        // ========================================
        // 새로운 캐싱 전략 API 메서드들
        // ========================================

        /// <summary>
        /// 라이트 동기화 - 프로필 요약 + 버전 정보
        /// </summary>
        public void GetLightSync(System.Action<bool, BlokusUnity.Data.LightSyncResponse, string> onComplete)
        {
            StartCoroutine(SendGetRequest<BlokusUnity.Data.LightSyncResponse>(
                "user/sync/light",
                response => {
                    Debug.Log($"라이트 동기화 성공: 버전 {response.user_profile.progress_version}");
                    onComplete?.Invoke(true, response, null);
                },
                error => {
                    Debug.LogError($"라이트 동기화 실패: {error}");
                    onComplete?.Invoke(false, null, error);
                }
            ));
        }

        /// <summary>
        /// 전체 진행도 동기화
        /// </summary>
        public void GetProgressSync(System.Action<bool, BlokusUnity.Data.ProgressSyncResponse, string> onComplete, 
            int fromStage = 1, int toStage = 1000)
        {
            string endpoint = $"user/sync/progress?from_stage={fromStage}&to_stage={toStage}";
            
            StartCoroutine(SendGetRequest<BlokusUnity.Data.ProgressSyncResponse>(
                endpoint,
                response => {
                    Debug.Log($"진행도 동기화 성공: {response.progress_data.Length}개 스테이지");
                    onComplete?.Invoke(true, response, null);
                },
                error => {
                    Debug.LogError($"진행도 동기화 실패: {error}");
                    onComplete?.Invoke(false, null, error);
                }
            ));
        }

        /// <summary>
        /// 메타데이터 동기화
        /// </summary>
        public void GetMetadataSync(System.Action<bool, BlokusUnity.Data.MetadataSyncResponse, string> onComplete, 
            string clientVersion = "")
        {
            string endpoint = "user/sync/metadata";
            if (!string.IsNullOrEmpty(clientVersion))
            {
                endpoint += $"?version={UnityWebRequest.EscapeURL(clientVersion)}";
            }
            
            StartCoroutine(SendGetRequest<BlokusUnity.Data.MetadataSyncResponse>(
                endpoint,
                response => {
                    Debug.Log($"메타데이터 동기화 성공: {(response.not_modified ? "변경없음" : response.stages.Length + "개 스테이지")}");
                    onComplete?.Invoke(true, response, null);
                },
                error => {
                    Debug.LogError($"메타데이터 동기화 실패: {error}");
                    onComplete?.Invoke(false, null, error);
                }
            ));
        }

        /// <summary>
        /// 스테이지 완료 보고 (응답에 최신 진행도 포함)
        /// </summary>
        public void CompleteStageWithSync(int stageNumber, int score, int completionTime, bool calculateStars,
            System.Action<bool, BlokusUnity.Data.CompleteStageResponse, string> onComplete)
        {
            var requestData = new
            {
                score = score,
                completion_time = completionTime,
                stars_earned = calculateStars ? CalculateStars(score, 100) : 0 // 임시로 100을 optimal_score로 사용
            };

            StartCoroutine(SendPostRequest<BlokusUnity.Data.CompleteStageResponse>(
                $"stages/{stageNumber}/complete",
                requestData,
                response => {
                    Debug.Log($"스테이지 {stageNumber} 완료 보고 성공");
                    onComplete?.Invoke(true, response, null);
                },
                error => {
                    Debug.LogError($"스테이지 완료 보고 실패: {error}");
                    onComplete?.Invoke(false, null, error);
                }
            ));
        }

        /// <summary>
        /// 별점 계산 헬퍼 메서드
        /// </summary>
        private int CalculateStars(int score, int optimalScore)
        {
            if (optimalScore <= 0) return 0;
            
            float percentage = (float)score / optimalScore;
            
            if (percentage >= 0.9f) return 3;      // 90% 이상
            else if (percentage >= 0.7f) return 2; // 70% 이상
            else if (percentage >= 0.5f) return 1; // 50% 이상
            else return 0;
        }

        /// <summary>
        /// 건강성 체크 (연결 상태 확인용)
        /// </summary>
        public void CheckHealth(System.Action<bool> onComplete)
        {
            StartCoroutine(CheckHealthCoroutine(onComplete));
        }

        private IEnumerator CheckHealthCoroutine(System.Action<bool> onComplete)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{apiBaseUrl}/health"))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();
                
                bool isHealthy = request.result == UnityWebRequest.Result.Success;
                onComplete?.Invoke(isHealthy);
            }
        }

        /// <summary>
        /// 일괄 스테이지 진행도 요청 (기존 메서드 활용)
        /// </summary>
        public void GetBatchProgress(System.Action<bool, BatchProgressData, string> onComplete)
        {
            StartCoroutine(SendGetRequest<BatchProgressData>(
                "user/progress/batch",
                response => {
                    Debug.Log($"일괄 진행도 수신: {response.total_count}개");
                    onComplete?.Invoke(true, response, null);
                },
                error => {
                    Debug.LogError($"일괄 진행도 로드 실패: {error}");
                    onComplete?.Invoke(false, null, error);
                }
            ));
        }

        /// <summary>
        /// 네트워크 상태 확인
        /// </summary>
        public bool IsOnline()
        {
            return isOnline;
        }

        /// <summary>
        /// 오프라인 큐 크기 반환
        /// </summary>
        public int GetOfflineQueueSize()
        {
            return offlineQueue.Count;
        }

        /// <summary>
        /// 강제 오프라인 큐 처리
        /// </summary>
        public void FlushOfflineQueue()
        {
            if (isOnline)
            {
                ProcessOfflineQueue();
            }
        }

        // ========================================
        // 새로운 API 응답 구조체들 (서버 실제 응답 구조에 맞춤)
        // ========================================
        
        [System.Serializable]
        public class MetadataSyncResponse
        {
            public string metadata_version;
            public MetadataStage[] stages;
            public int total_count;
            public string sync_completed_at;
            public bool not_modified; // 304 응답시에만 존재
        }
        
        [System.Serializable]
        public class MetadataStage
        {
            public int stage_id;
            public int stage_number;
            public int difficulty;
            public int optimal_score;
            public int? time_limit;
            public int max_undo_count;
            public string description;
            public string[] hints;
            public int[] available_blocks;
            public bool is_featured;
            public string thumbnail_url;
            public int[] initial_board_state;
        }
        
        /// <summary>
        /// MetadataStage 배열을 CompactStageMetadata 배열로 변환
        /// </summary>
        private CompactStageMetadata[] ConvertToCompactMetadata(MetadataStage[] serverStages)
        {
            if (serverStages == null) return new CompactStageMetadata[0];
            
            var compactStages = new CompactStageMetadata[serverStages.Length];
            for (int i = 0; i < serverStages.Length; i++)
            {
                var server = serverStages[i];
                compactStages[i] = new CompactStageMetadata
                {
                    n = server.stage_number,
                    t = server.stage_number.ToString(), // title은 stage_number를 문자열로
                    d = server.difficulty,
                    o = server.optimal_score,
                    tl = server.time_limit ?? 0,
                    tu = server.thumbnail_url,
                    desc = server.description,
                    ab = server.available_blocks ?? new int[0], // 서버에서 제공하는 available_blocks 사용
                    muc = server.max_undo_count,
                    ibs = server.initial_board_state != null && server.initial_board_state.Length > 0 
                        ? new InitialBoardStateApi { boardPositions = server.initial_board_state }
                        : null,
                    h = server.hints ?? new string[0]
                };
            }
            
            Debug.Log($"[HttpApiClient] 메타데이터 변환 완료: {compactStages.Length}개");
            return compactStages;
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