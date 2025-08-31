using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using App.Services;
using App.Utils;
using App.Config;
using App.Core;
using Features.Single.Core;
using Shared.Models;
using MessagePriority = Shared.UI.MessagePriority;
namespace App.Network{
    /// <summary>
    /// HTTP API 클라이언트 - 싱글플레이어 전용
    /// TCP 대신 HTTP REST API 사용으로 리소스 효율성 극대화
    /// </summary>

    /// <summary>
    /// 스테이지 데이터 구조체 (네트워크 전용)
    /// </summary>
    [System.Serializable]
    public class StageData
    {
        public int stageNumber;
        public string stageName;
        public int difficulty;
        public int optimalScore;
        public int? timeLimit; // null이면 무제한
        public int maxUndoCount;
        public List<Shared.Models.BlockType> availableBlocks;
        public string initialBoardStateJson; // JSONB 데이터 문자열
        public string stageDescription;
        public string thumbnail_url; // 썸네일 이미지 URL
    }

    /// <summary>
    /// 사용자 스테이지 진행도 구조체 (네트워크 전용 - Game 네임스페이스와 분리)
    /// </summary>
    [System.Serializable]
    public class UserStageProgress
    {
        public int stageNumber;
        public bool isCompleted;
        public int starsEarned;
        public int bestScore;
        public int bestCompletionTime; // 초 단위
        public int totalAttempts;
        public int successfulAttempts;
        public System.DateTime firstPlayedAt;
        public System.DateTime lastPlayedAt;
    }

    public class HttpApiClient : MonoBehaviour
    {
        public string ApiBaseUrl => GetApiBaseUrl();
        [Header("API 서버 설정")]
        [SerializeField] private int requestTimeoutSeconds = 10;

        /// <summary>
        /// 환경별 API URL 반환 (EnvironmentConfig 사용)
        /// </summary>
        private string GetApiBaseUrl()
        {
            return EnvironmentConfig.ApiServerUrl;
        }

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
        public event System.Action<CurrentStatus> OnCurrentStatusReceived; // 🔥 추가: current_status 전달용

        // 인증 이벤트 
        public event System.Action<bool, string, string> OnAuthResponse; // success, message, token
        public event System.Action<AuthUserData> OnUserInfoReceived;
        public event System.Action<string> OnOAuthRegisterRedirect;
        public event System.Action OnLogoutComplete; // 🔥 추가: 로그아웃 전용 이벤트
        public event System.Action<bool, string> OnAutoLoginComplete; // 🔥 추가: 자동 로그인 완료 이벤트

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

        void Start()
        {
            // 🔥 제거: DelayedAutoLogin는 AppPersistent에서 호출하도록 변경
            // SessionManager 초기화는 이제 AppPersistent/SceneFlowController에서 처리
        }

        // ========================================
        // 환경 설정
        // ========================================

        private void InitializeFromEnvironment()
        {
            Debug.Log($"[HttpApiClient] 현재 환경: {(Application.isEditor ? "에디터" : Application.isMobilePlatform ? "모바일" : "데스크톱")}");
            Debug.Log($"[HttpApiClient] 사용할 API URL: {ApiBaseUrl}");
        }

        /// <summary>
        /// 인증 토큰 설정
        /// </summary>
        public void SetAuthToken(string token, int userId)
        {
            authToken = token;
            currentUserId = userId;
            isOnline = true;
            Debug.Log($"SetAuthToken Instance: {this.GetHashCode()}");
            Debug.Log($"HTTP API 인증 설정 완료: User {userId}");
            Debug.Log($"저장 후 authToken 상태: {authToken?.Substring(0, 20)}...");
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
            string url = $"{ApiBaseUrl}/{endpoint}";
            
            Debug.Log($"GET요청 Instance: {this.GetHashCode()}");
            Debug.Log($"GET요청 authToken: {(string.IsNullOrEmpty(authToken) ? "NULL" : "EXISTS")}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                // 인증 헤더 추가
                if (!string.IsNullOrEmpty(authToken))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    Debug.Log($"Authorization 헤더 추가됨: Bearer {authToken.Substring(0, 20)}...");
                }
                else
                {
                    Debug.LogWarning($"인증 토큰이 없음 - 인스턴스: {this.GetHashCode()}");
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
                    // 401 에러 시 자동 토큰 갱신 시도
                    if (request.responseCode == 401)
                    {
                        Debug.LogWarning($"[HttpApiClient] 401 Unauthorized - 자동 토큰 갱신 시도: {endpoint}");
                        yield return StartCoroutine(HandleUnauthorizedWithRefresh<T>(endpoint, null, "GET", onSuccess, onError));
                        yield break;
                    }

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
            string url = $"{ApiBaseUrl}/{endpoint}";
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
                    Debug.Log($"Authorization 헤더 추가됨: Bearer {authToken.Substring(0, 20)}...");
                }
                else
                {
                    Debug.LogWarning("인증 토큰이 없음 - Authorization 헤더 없이 요청");
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
                    // 401 에러 시 자동 토큰 갱신 시도
                    if (request.responseCode == 401)
                    {
                        Debug.LogWarning($"[HttpApiClient] 401 Unauthorized - 자동 토큰 갱신 시도: {endpoint}");
                        yield return StartCoroutine(HandleUnauthorizedWithRefresh<T>(endpoint, requestData, "POST", onSuccess, onError));
                        yield break;
                    }

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

            StartCoroutine(SendPostRequest<LoginResponseData>(
                "auth/login",
                loginData,
                response =>
                {
                    Debug.Log($"로그인 응답 파싱 성공: access_token={response?.access_token?.Substring(0, 20)}..., user_id={response?.user?.user_id}");
                    Debug.Log($"토큰 null 체크: access_token={!string.IsNullOrEmpty(response?.access_token)}, user={response?.user != null}");
                    
                    // null 체크 후 토큰 저장
                    if (!string.IsNullOrEmpty(response?.access_token) && response?.user != null)
                    {
                        Debug.Log($"SetAuthToken 호출 시작: token={response.access_token.Substring(0, 20)}..., userId={response.user.user_id}");
                        SetAuthToken(response.access_token, response.user.user_id);
                        
                        // 🔥 수정: refresh token을 SecureStorage에 저장
                        if (!string.IsNullOrEmpty(response.refresh_token))
                        {
                            App.Security.SecureStorage.StoreString("blokus_refresh_token", response.refresh_token);
                            App.Security.SecureStorage.StoreString("blokus_user_id", response.user.user_id.ToString());
                            App.Security.SecureStorage.StoreString("blokus_username", response.user.username ?? "");
                        }

                        // SessionManager에 access token만 저장 (refresh_token은 SecureStorage에서 관리)
                        if (App.Core.SessionManager.Instance != null)
                        {
                            App.Core.SessionManager.Instance.SetTokens(
                                response.access_token, 
                                "", // refresh_token은 SecureStorage에서 관리
                                response.user.user_id
                            );
                        }
                        
                        OnAuthResponse?.Invoke(true, "로그인 성공", response.access_token);

                        // 서버에서 제공하지 않는 필드들 기본값 설정
                        if (string.IsNullOrEmpty(response.user.display_name))
                            response.user.display_name = response.user.username;
                        
                        // 사용자 정보 이벤트 발생 (AuthUserData 형식으로 변환)
                        var authData = new AuthUserData
                        {
                            token = response.access_token,
                            user = response.user
                        };
                        OnUserInfoReceived?.Invoke(authData);
                    }
                    else
                    {
                        Debug.LogError($"로그인 응답에서 토큰 또는 사용자 정보가 없음: access_token={response?.access_token != null}, user={response?.user != null}");
                        OnAuthResponse?.Invoke(false, "로그인 응답 오류", null);
                    }
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
                response =>
                {
                    // OAuth 리다이렉트 URL 이벤트 발생
                    OnOAuthRegisterRedirect?.Invoke(response.redirect_url);
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
                response =>
                {
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
                error =>
                {
                    ClearAuthToken();
                    OnAuthResponse?.Invoke(false, $"토큰 검증 실패: {error}", null);
                }
            ));
        }

        /// <summary>
        /// Refresh Token을 사용한 자동 로그인
        /// </summary>
        public void RefreshToken(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                OnAuthResponse?.Invoke(false, "Refresh token이 없습니다.", null);
                return;
            }

            var refreshData = new RefreshTokenRequest
            {
                refresh_token = refreshToken,
                client_id = "unity-mobile-client"
            };

            StartCoroutine(SendPostRequest<LoginResponseData>(
                "auth/refresh",
                refreshData,
                response =>
                {
                    Debug.Log($"토큰 갱신 성공: access_token={response?.access_token?.Substring(0, 20)}..., user_id={response?.user?.user_id}");
                    
                    if (!string.IsNullOrEmpty(response?.access_token) && response?.user != null)
                    {
                        // 새로운 토큰으로 업데이트
                        SetAuthToken(response.access_token, response.user.user_id);
                        
                        // SessionManager에 새로운 토큰들 저장 (새로운 refresh_token 포함)
                        if (App.Core.SessionManager.Instance != null)
                        {
                            App.Core.SessionManager.Instance.SetTokens(
                                response.access_token, 
                                response.refresh_token, 
                                response.user.user_id
                            );
                        }

                        OnAuthResponse?.Invoke(true, "토큰 갱신 성공", response.access_token);

                        // 사용자 정보 이벤트 발생
                        var authData = new AuthUserData
                        {
                            token = response.access_token,
                            user = response.user
                        };
                        OnUserInfoReceived?.Invoke(authData);
                    }
                    else
                    {
                        Debug.LogError("토큰 갱신 응답에서 토큰 또는 사용자 정보가 없음");
                        OnAuthResponse?.Invoke(false, "토큰 갱신 응답 오류", null);
                    }
                },
                error => 
                {
                    Debug.LogError($"토큰 갱신 실패: {error}");
                    OnAuthResponse?.Invoke(false, $"토큰 갱신 실패: {error}", null);
                }
            ));
        }

        /// <summary>
        /// SecureStorage에서 refresh token을 가져와 자동 로그인 시도
        /// </summary>
        public void ValidateRefreshTokenFromStorage()
        {
            Debug.Log("[HttpApiClient] SecureStorage에서 refresh token 자동 로그인 시도");

            // 1. Single-API refresh token 확인
            string savedRefreshToken = App.Security.SecureStorage.GetString("blokus_refresh_token");
            
            // 2. OIDC refresh token도 확인
            var oidcAuthenticator = AppBootstrap.GetGlobalOidcAuthenticator();
            string oidcRefreshToken = oidcAuthenticator?.GetRefreshToken();
            
            if (!string.IsNullOrEmpty(savedRefreshToken))
            {
                Debug.Log("[HttpApiClient] 저장된 Single-API refresh token으로 자동 로그인 시도");
                TryRefreshSingleApiToken(savedRefreshToken);
                return;
            }
            
            if (!string.IsNullOrEmpty(oidcRefreshToken))
            {
                Debug.Log("[HttpApiClient] 저장된 OIDC refresh token으로 자동 로그인 시도");
                TryRefreshOidcToken(oidcRefreshToken);
                return;
            }
            
            Debug.Log("[HttpApiClient] 저장된 refresh token이 없음 - 자동 로그인 실패");
            OnAutoLoginComplete?.Invoke(false, "저장된 refresh token이 없음");
        }
        
        /// <summary>
        /// 저장된 refresh token으로 OIDC 서버에서 자동 로그인 (통합 메서드)
        /// </summary>
        private void TryRefreshSingleApiToken(string refreshToken)
        {
            Debug.Log("[HttpApiClient] OIDC 서버로 refresh token 자동 로그인 시도");
            
            // 모든 refresh token은 OIDC 서버로 요청 (포트 9000)
            StartCoroutine(RefreshOidcTokenCoroutine(null, refreshToken));
        }
        
        /// <summary>
        /// OIDC refresh token으로 자동 로그인
        /// </summary>
        private void TryRefreshOidcToken(string refreshToken)
        {
            Debug.Log("[HttpApiClient] OIDC refresh token으로 자동 로그인 시도");
            
            var oidcAuthenticator = AppBootstrap.GetGlobalOidcAuthenticator();
            if (oidcAuthenticator == null)
            {
                Debug.LogError("[HttpApiClient] OidcAuthenticator를 찾을 수 없음");
                OnAutoLoginComplete?.Invoke(false, "OIDC 인증 시스템 오류");
                return;
            }
            
            // OIDC 서버에서 토큰 갱신
            StartCoroutine(RefreshOidcTokenCoroutine(oidcAuthenticator, refreshToken));
        }
        
        /// <summary>
        /// OIDC 토큰 갱신 코루틴 - OIDC 서버의 직접 API 사용
        /// </summary>
        private IEnumerator RefreshOidcTokenCoroutine(OidcAuthenticator oidcAuth, string refreshToken)
        {
            Debug.Log("[HttpApiClient] OIDC 토큰 갱신 시도 - 직접 API 사용");
            
            var refreshData = new RefreshTokenRequest
            {
                refresh_token = refreshToken,
                client_id = "unity-mobile-client"
            };

            // OIDC 서버의 직접 API 엔드포인트 사용
            string oidcUrl = EnvironmentConfig.OidcServerUrl; // http://localhost:9000
            string endpoint = "api/auth/refresh";
            string fullUrl = $"{oidcUrl}/{endpoint}";
            string jsonData = JsonUtility.ToJson(refreshData);

            using (UnityWebRequest request = new UnityWebRequest(fullUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.certificateHandler = new BypassCertificate();

                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = requestTimeoutSeconds;

                Debug.Log($"[HttpApiClient] OIDC 토큰 갱신 요청: {fullUrl}, 데이터: {jsonData}");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    Debug.Log($"[HttpApiClient] OIDC 토큰 갱신 응답: {jsonResponse}");

                    try
                    {
                        LoginResponseData response = JsonUtility.FromJson<LoginResponseData>(jsonResponse);
                        
                        if (!string.IsNullOrEmpty(response?.access_token) && response?.user != null)
                        {
                            Debug.Log("[HttpApiClient] OIDC 토큰 갱신 성공 - 새로운 토큰 받음");
                            
                            // HttpApiClient에 새로운 토큰 설정
                            SetAuthToken(response.access_token, response.user.user_id);
                            
                            // SecureStorage에 새로운 refresh token 저장
                            if (!string.IsNullOrEmpty(response.refresh_token))
                            {
                                App.Security.SecureStorage.StoreString("blokus_refresh_token", response.refresh_token);
                                App.Security.SecureStorage.StoreString("blokus_user_id", response.user.user_id.ToString());
                                App.Security.SecureStorage.StoreString("blokus_username", response.user.username ?? "");
                            }
                            
                            // SessionManager에 토큰 설정
                            if (App.Core.SessionManager.Instance != null)
                            {
                                App.Core.SessionManager.Instance.SetTokens(
                                    response.access_token, 
                                    "", // refresh_token은 SecureStorage에서 관리
                                    response.user.user_id
                                );
                            }

                            OnAutoLoginComplete?.Invoke(true, "OIDC 토큰 갱신 성공");
                        }
                        else
                        {
                            Debug.LogWarning("[HttpApiClient] OIDC 토큰 갱신 응답이 불완전함");
                            OnAutoLoginComplete?.Invoke(false, "OIDC 토큰 갱신 응답 오류");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[HttpApiClient] OIDC 토큰 갱신 응답 파싱 오류: {ex.Message}\n응답: {jsonResponse}");
                        OnAutoLoginComplete?.Invoke(false, "OIDC 토큰 갱신 응답 파싱 실패");
                    }
                }
                else
                {
                    Debug.LogWarning($"[HttpApiClient] OIDC 토큰 갱신 실패: {request.error} (코드: {request.responseCode})");
                    
                    // 갱신 실패시 OIDC refresh token을 SecureStorage에서 제거
                    // (OIDC refresh token은 OIDC Authenticator가 관리하므로 여기서는 제거하지 않음)
                    
                    OnAutoLoginComplete?.Invoke(false, $"OIDC 토큰 갱신 실패: {request.error}");
                }
            }
        }

        /// <summary>
        /// 저장된 refresh token으로 자동 로그인 시도 (기존 메서드 - 호환성 유지)
        /// </summary>
        public void TryAutoLoginWithRefreshToken()
        {
            Debug.LogWarning("[HttpApiClient] TryAutoLoginWithRefreshToken is deprecated, use ValidateRefreshTokenFromStorage instead");
            ValidateRefreshTokenFromStorage();
        }

        /// <summary>
        /// 401 에러 시 자동 토큰 갱신 및 재시도
        /// </summary>
        private IEnumerator HandleUnauthorizedWithRefresh<T>(string endpoint, object requestData, string method, 
            System.Action<T> onSuccess, System.Action<string> onError = null)
        {
            if (App.Core.SessionManager.Instance == null || !App.Core.SessionManager.Instance.HasRefreshToken())
            {
                Debug.LogWarning("[HttpApiClient] Refresh token이 없어서 401 자동 처리 불가능");
                onError?.Invoke("로그인이 만료되었습니다. 다시 로그인해주세요.");
                yield break;
            }

            Debug.Log("[HttpApiClient] 401 에러 - refresh token으로 자동 갱신 시도");
            
            bool refreshSuccess = false;
            
            // 토큰 갱신 완료 이벤트 구독
            System.Action<bool, string, string> onRefreshComplete = (success, message, token) =>
            {
                refreshSuccess = success;
            };
            
            OnAuthResponse += onRefreshComplete;
            
            // Refresh token으로 토큰 갱신 시도
            string savedRefreshToken = App.Core.SessionManager.Instance.GetRefreshToken();
            RefreshToken(savedRefreshToken);
            
            // 갱신 완료까지 대기 (최대 10초)
            float timeout = 10f;
            float elapsed = 0f;
            
            while (elapsed < timeout && !refreshSuccess)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            OnAuthResponse -= onRefreshComplete;
            
            if (!refreshSuccess)
            {
                Debug.LogError("[HttpApiClient] 토큰 갱신 실패 - 로그인 필요");
                onError?.Invoke("로그인이 만료되었습니다. 다시 로그인해주세요.");
                yield break;
            }
            
            Debug.Log("[HttpApiClient] 토큰 갱신 성공 - 원래 요청 재시도");
            
            // 원래 요청 재시도
            if (method == "GET")
            {
                yield return StartCoroutine(SendGetRequest<T>(endpoint, onSuccess, onError));
            }
            else if (method == "POST")
            {
                yield return StartCoroutine(SendPostRequest<T>(endpoint, requestData, onSuccess, onError));
            }
        }

        /// <summary>
        /// 로그아웃 (클라이언트 측 토큰 클리어만 수행)
        /// </summary>
        public void Logout()
        {
            // 🔥 수정: 서버에 logout 엔드포인트가 없으므로 클라이언트에서만 토큰 클리어
            Debug.Log("[HttpApiClient] 로그아웃 - 로컬 토큰 클리어 시작");
            
            ClearAuthToken();
            
            // 🔥 수정: OnAuthResponse 대신 OnLogoutComplete 이벤트 사용
            OnLogoutComplete?.Invoke();
            
            Debug.Log("[HttpApiClient] 로그아웃 완료 - 토큰 클리어됨");
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
                response =>
                {
                    OnBatchProgressReceived?.Invoke(response.progress);
                    // 🔥 추가: current_status도 전달
                    if (response.current_status != null)
                    {
                        Debug.Log($"[HttpApiClient] 🔥 OnCurrentStatusReceived 이벤트 발생! max_stage_completed={response.current_status.max_stage_completed}");
                        OnCurrentStatusReceived?.Invoke(response.current_status);
                    }
                    else
                    {
                        Debug.LogWarning("[HttpApiClient] ⚠️ response.current_status가 null입니다!");
                    }
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
                response =>
                {
                    Debug.Log($"[HttpApiClient] 스테이지 메타데이터 수신: {response.stages?.Length ?? 0}개");

                    // MetadataSyncResponse의 stages를 CompactStageMetadata로 변환
                    if (response.stages != null && response.stages.Length > 0)
                    {
                        var compactStages = ConvertToCompactMetadata(response.stages);

                        // Features.Single.Core.UserDataCache에 직접 저장
                        if (Features.Single.Core.UserDataCache.Instance != null)
                        {
                            Features.Single.Core.UserDataCache.Instance.SetStageMetadata(compactStages);
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
                response =>
                {
                    string message = completed ?
                        $"스테이지 {stageNumber} 완료! {response.stars_earned}별 획득" :
                        $"스테이지 {stageNumber} 시도 기록됨";
                    OnStageCompleteResponse?.Invoke(true, message);
                },
                error =>
                {
                    Debug.LogWarning($"스테이지 완료 보고 실패: {error}");
                    OnStageCompleteResponse?.Invoke(false, error);
                }
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
                using (UnityWebRequest ping = UnityWebRequest.Get($"{ApiBaseUrl}/health"))
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
        public class RefreshTokenRequest
        {
            public string refresh_token;
            public string client_id;
        }

        [System.Serializable]
        public class AuthUserData
        {
            public UserData user;
            public string token;
            public string expires_in;
        }

        [System.Serializable]
        public class LoginResponseData
        {
            public string access_token;
            public string refresh_token;
            public string token_type;
            public int expires_in;
            public UserData user;
        }

        [System.Serializable]
        public class UserData
        {
            public int user_id;
            public string username;
            public string email;
            public string display_name;
            public int single_player_level;
            public int max_stage_completed;
            public long single_player_score;
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
            public string display_name;
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
                response =>
                {
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
                response =>
                {
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
        public void GetLightSync(System.Action<bool, App.Services.LightSyncResponse, string> onComplete)
        {
            StartCoroutine(SendGetRequest<App.Services.LightSyncResponse>(
                "user/sync/light",
                response =>
                {
                    Debug.Log($"라이트 동기화 성공: 버전 {response.user_profile.progress_version}");
                    onComplete?.Invoke(true, response, null);
                },
                error =>
                {
                    Debug.LogError($"라이트 동기화 실패: {error}");
                    onComplete?.Invoke(false, null, error);
                }
            ));
        }

        /// <summary>
        /// 전체 진행도 동기화
        /// </summary>
        public void GetProgressSync(System.Action<bool, App.Services.ProgressSyncResponse, string> onComplete,
            int fromStage = 1, int toStage = 1000)
        {
            string endpoint = $"user/sync/progress?from_stage={fromStage}&to_stage={toStage}";

            StartCoroutine(SendGetRequest<App.Services.ProgressSyncResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"진행도 동기화 성공: {response.progress_data.Length}개 스테이지");
                    onComplete?.Invoke(true, response, null);
                },
                error =>
                {
                    Debug.LogError($"진행도 동기화 실패: {error}");
                    onComplete?.Invoke(false, null, error);
                }
            ));
        }

        /// <summary>
        /// 메타데이터 동기화
        /// </summary>
        public void GetMetadataSync(System.Action<bool, App.Services.MetadataSyncResponse, string> onComplete,
            string clientVersion = "")
        {
            string endpoint = "user/sync/metadata";
            if (!string.IsNullOrEmpty(clientVersion))
            {
                endpoint += $"?version={UnityWebRequest.EscapeURL(clientVersion)}";
            }

            StartCoroutine(SendGetRequest<App.Services.MetadataSyncResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"메타데이터 동기화 성공: {(response.not_modified ? "변경없음" : response.stages.Length + "개 스테이지")}");
                    onComplete?.Invoke(true, response, null);
                },
                error =>
                {
                    Debug.LogError($"메타데이터 동기화 실패: {error}");
                    onComplete?.Invoke(false, null, error);
                }
            ));
        }

        /// <summary>
        /// 스테이지 완료 보고 (응답에 최신 진행도 포함)
        /// </summary>
        public void CompleteStageWithSync(int stageNumber, int score, int completionTime, bool calculateStars,
            System.Action<bool, App.Services.CompleteStageResponse, string> onComplete)
        {
            var requestData = new
            {
                score = score,
                completion_time = completionTime,
                stars_earned = calculateStars ? CalculateStars(score, 100) : 0 // 임시로 100을 optimal_score로 사용
            };

            StartCoroutine(SendPostRequest<App.Services.CompleteStageResponse>(
                $"stages/{stageNumber}/complete",
                requestData,
                response =>
                {
                    Debug.Log($"스테이지 {stageNumber} 완료 보고 성공");
                    onComplete?.Invoke(true, response, null);
                },
                error =>
                {
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
            using (UnityWebRequest request = UnityWebRequest.Get($"{ApiBaseUrl}/health"))
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
                response =>
                {
                    Debug.Log($"일괄 진행도 수신: {response.total_count}개");
                    onComplete?.Invoke(true, response, null);
                },
                error =>
                {
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