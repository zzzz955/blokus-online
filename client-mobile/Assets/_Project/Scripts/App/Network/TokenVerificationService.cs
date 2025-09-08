using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace App.Network
{
    /// <summary>
    /// 토큰 검증 및 갱신 서비스
    /// TCP 서버 연결 전 모바일 클라이언트의 사전 토큰 검증을 담당
    /// </summary>
    public class TokenVerificationService
    {
        private const string TOKEN_VERIFICATION_ENDPOINT = "/auth/mobile/tcp-token";
        private readonly string baseUrl;
        
        public TokenVerificationService(string authServerBaseUrl)
        {
            // EnvironmentModeManager를 통한 동적 URL 설정
            if (App.Config.EnvironmentModeManager.Instance != null)
            {
                baseUrl = App.Config.EnvironmentModeManager.Instance.GetAuthServerUrl();
                Debug.Log($"[TokenVerificationService] Environment-based URL: {baseUrl}");
            }
            else
            {
                // Fallback: 전달받은 URL 또는 배포 서버 URL 사용
                baseUrl = authServerBaseUrl?.TrimEnd('/') ?? "https://blokus-online.mooo.com";
                Debug.LogWarning($"[TokenVerificationService] EnvironmentModeManager not found, using fallback URL: {baseUrl}");
            }
        }
        
        /// <summary>
        /// TCP 연결을 위한 토큰 검증 및 갱신
        /// </summary>
        /// <param name="currentAccessToken">현재 Access Token</param>
        /// <param name="clientId">OIDC 클라이언트 ID</param>
        /// <returns>토큰 검증 결과</returns>
        public async Task<TokenVerificationResult> VerifyTokenForTcpConnection(string currentAccessToken, string clientId)
        {
            if (string.IsNullOrEmpty(currentAccessToken))
            {
                Debug.LogError("[TokenVerificationService] Access token이 비어있습니다");
                return new TokenVerificationResult
                {
                    Valid = false,
                    Error = "MISSING_TOKEN",
                    Message = "Access token is required"
                };
            }

            if (string.IsNullOrEmpty(clientId))
            {
                Debug.LogError("[TokenVerificationService] Client ID가 비어있습니다");
                return new TokenVerificationResult
                {
                    Valid = false,
                    Error = "MISSING_CLIENT_ID", 
                    Message = "Client ID is required"
                };
            }

            try
            {
                Debug.Log($"[TokenVerificationService] 토큰 검증 요청 시작: {baseUrl}{TOKEN_VERIFICATION_ENDPOINT}");
                
                var requestData = new TokenVerificationRequest
                {
                    ClientId = clientId,
                    AccessToken = currentAccessToken
                };

                string jsonData = JsonConvert.SerializeObject(requestData);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

                using (UnityWebRequest request = new UnityWebRequest($"{baseUrl}{TOKEN_VERIFICATION_ENDPOINT}", "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    
                    // Headers 설정
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {currentAccessToken}");
                    request.SetRequestHeader("User-Agent", $"BlokusUnityClient/{Application.version}");
                    
                    // 타임아웃 설정 (모바일 환경 고려)
                    request.timeout = 15; // 15초
                    
                    var operation = request.SendWebRequest();
                    
                    // 비동기 대기
                    while (!operation.isDone)
                    {
                        await Task.Delay(50);
                    }
                    
                    // 응답 처리
                    return ProcessVerificationResponse(request);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TokenVerificationService] 토큰 검증 중 예외 발생: {ex.Message}");
                return new TokenVerificationResult
                {
                    Valid = false,
                    Error = "NETWORK_ERROR",
                    Message = $"Network request failed: {ex.Message}"
                };
            }
        }
        
        /// <summary>
        /// HTTP 응답 처리
        /// </summary>
        private TokenVerificationResult ProcessVerificationResponse(UnityWebRequest request)
        {
            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"[TokenVerificationService] 토큰 검증 응답: {responseText}");
                    
                    var response = JsonConvert.DeserializeObject<TokenVerificationResponse>(responseText);
                    
                    if (response != null && response.Valid)
                    {
                        Debug.Log($"[TokenVerificationService] 토큰 검증 성공 (갱신됨: {response.Refreshed})");
                        return new TokenVerificationResult
                        {
                            Valid = true,
                            AccessToken = response.AccessToken,
                            ExpiresIn = response.ExpiresIn,
                            Refreshed = response.Refreshed
                        };
                    }
                    else
                    {
                        Debug.LogWarning($"[TokenVerificationService] 토큰 검증 실패: {response?.Error ?? "Unknown error"}");
                        return new TokenVerificationResult
                        {
                            Valid = false,
                            Error = response?.Error ?? "UNKNOWN_ERROR",
                            Message = response?.Message ?? "Token verification failed"
                        };
                    }
                }
                else
                {
                    // HTTP 에러 처리
                    string errorMessage = $"HTTP {request.responseCode}: {request.error}";
                    string responseBody = request.downloadHandler?.text ?? "No response body";
                    
                    Debug.LogError($"[TokenVerificationService] HTTP 에러: {errorMessage}");
                    Debug.LogError($"[TokenVerificationService] 응답 내용: {responseBody}");
                    
                    // 응답 본문에서 에러 정보 추출 시도
                    try
                    {
                        var errorResponse = JsonConvert.DeserializeObject<TokenVerificationResponse>(responseBody);
                        if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.Error))
                        {
                            return new TokenVerificationResult
                            {
                                Valid = false,
                                Error = errorResponse.Error,
                                Message = errorResponse.Message ?? errorMessage
                            };
                        }
                    }
                    catch
                    {
                        // JSON 파싱 실패 시 기본 에러 메시지 사용
                    }
                    
                    return new TokenVerificationResult
                    {
                        Valid = false,
                        Error = "HTTP_ERROR",
                        Message = errorMessage
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TokenVerificationService] 응답 처리 중 예외: {ex.Message}");
                return new TokenVerificationResult
                {
                    Valid = false,
                    Error = "RESPONSE_PROCESSING_ERROR",
                    Message = $"Failed to process response: {ex.Message}"
                };
            }
        }
    }
    
    /// <summary>
    /// 토큰 검증 요청 데이터
    /// </summary>
    [Serializable]
    public class TokenVerificationRequest
    {
        [JsonProperty("client_id")]
        public string ClientId { get; set; }
        
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
    }
    
    /// <summary>
    /// 토큰 검증 응답 데이터 (서버 응답 형식)
    /// </summary>
    [Serializable]
    public class TokenVerificationResponse
    {
        [JsonProperty("valid")]
        public bool Valid { get; set; }
        
        [JsonProperty("accessToken")]
        public string AccessToken { get; set; }
        
        [JsonProperty("expiresIn")]
        public int ExpiresIn { get; set; }
        
        [JsonProperty("refreshed")]
        public bool Refreshed { get; set; }
        
        [JsonProperty("error")]
        public string Error { get; set; }
        
        [JsonProperty("message")]
        public string Message { get; set; }
    }
    
    /// <summary>
    /// 토큰 검증 결과 (클라이언트 사용)
    /// </summary>
    public class TokenVerificationResult
    {
        public bool Valid { get; set; }
        public string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
        public bool Refreshed { get; set; }
        public string Error { get; set; }
        public string Message { get; set; }
        
        /// <summary>
        /// 사용자 친화적 에러 메시지 반환
        /// </summary>
        public string GetUserFriendlyMessage()
        {
            if (Valid) return "인증 성공";
            
            return Error switch
            {
                "ACCESS_TOKEN_REQUIRED" => "로그인이 필요합니다",
                "INVALID_TOKEN" => "인증 정보가 유효하지 않습니다",
                "REFRESH_TOKEN_EXPIRED" => "다시 로그인해주세요",
                "INVALID_CLIENT" => "앱 설정 오류입니다",
                "NETWORK_ERROR" => "네트워크 연결을 확인해주세요",
                "HTTP_ERROR" => "서버 연결에 실패했습니다",
                _ => Message ?? "알 수 없는 오류가 발생했습니다"
            };
        }
    }
}