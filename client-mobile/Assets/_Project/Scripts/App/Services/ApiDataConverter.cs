using System;
using System.Collections.Generic;
using UnityEngine;
using App.Network;
using Features.Multi.Net;
using Features.Single.Core;
using Shared.Models;
using CommonUserStageProgress = Shared.Models.UserStageProgress;
using CommonUserInfo = Shared.Models.UserInfo;
namespace App.Services{
    /// <summary>
    /// API 데이터 변환 유틸리티
    /// 압축된 JSON 응답을 Unity에서 사용 가능한 데이터 구조로 변환
    /// </summary>
    public static class ApiDataConverter
    {
        private static Shared.Models.InitialBoardState ConvertInitialBoardState(HttpApiClient.InitialBoardStateApi ibs)
        {
            if (ibs == null) return null;

            var state = new Shared.Models.InitialBoardState();

            // Get unified board data using new INTEGER[] format
            var boardData = ibs.GetBoardData();
            
            if (boardData != null && boardData.Length > 0)
            {
                // Convert INTEGER[] format to placements
                // Format: color_index * 400 + (y * 20 + x)
                // Colors: 검정(0), 파랑(1), 노랑(2), 빨강(3), 초록(4)
                state.boardPositions = boardData;
                
                Debug.Log($"[ApiDataConverter] 새로운 INTEGER[] 형식으로 초기 보드 상태 변환: {boardData.Length}개 위치");
            }
            else
            {
                // Fallback to empty state
                state.boardPositions = new int[0];
                Debug.Log("[ApiDataConverter] 빈 초기 보드 상태로 설정");
            }

            return state;
        }
        
        /// <summary>
        /// 압축된 스테이지 메타데이터를 StageData로 변환
        /// </summary>
        public static Shared.Models.StageData ConvertCompactMetadata(HttpApiClient.CompactStageMetadata compact)
        {
            return new Shared.Models.StageData
            {
                stage_number = compact.n,
                stage_title = compact.t,
                difficulty = compact.d,
                optimal_score = compact.o,
                time_limit = compact.tl,              // null/0 허용 시 StageData 쪽 타입에 맞게 사용
                max_undo_count = (compact.muc >= 0 ? compact.muc : 3),
                available_blocks = compact.ab,
                initial_board_state = ConvertInitialBoardState(compact.ibs),
                hints = (compact.h != null && compact.h.Length > 0) ? string.Join("|", compact.h) : "",
                stage_description = compact.desc,
                thumbnail_url = compact.tu
            };
        }

        /// <summary>
        /// 압축된 사용자 진행도를 UserStageProgress로 변환
        /// </summary>
        public static CommonUserStageProgress ConvertCompactProgress(HttpApiClient.CompactUserProgress compact)
        {
            return new CommonUserStageProgress
            {
                stage_number = compact.n,
                is_completed = compact.c,
                stars_earned = compact.s,
                best_score = compact.bs,
                best_completion_time = compact.bt,
                total_attempts = compact.a,
                successful_attempts = 0, // 압축 버전에는 포함되지 않음
                first_played_at = null,
                first_completed_at = null,
                last_played_at = null
            };
        }

        /// <summary>
        /// API 응답의 AuthUserData를 UserInfo로 변환 (로그인 기본 정보만)
        /// </summary>
        public static CommonUserInfo ConvertAuthUserData(HttpApiClient.AuthUserData authData)
        {
            return new CommonUserInfo
            {
                username = authData.user.username,
                display_name = authData.user.display_name,
                level = authData.user.single_player_level, // 싱글플레이어 레벨 사용
                totalGames = 0, // 싱글플레이어에서는 미사용
                wins = 0, // 싱글플레이어에서는 미사용
                losses = 0, // 싱글플레이어에서는 미사용
                averageScore = 0, // 싱글플레이어에서는 미사용
                isOnline = true,
                status = "로비",
                maxStageCompleted = authData.user.max_stage_completed
            };
        }
        
        /// <summary>
        ///  새로운 메서드: UserProfile API 응답을 UserInfo로 변환
        /// </summary>
        public static CommonUserInfo ConvertUserProfile(HttpApiClient.UserProfile userProfile)
        {
            var convertedUserInfo = new CommonUserInfo
            {
                username = userProfile.username,
                level = userProfile.single_player_level,
                totalGames = userProfile.total_single_games,
                wins = 0, // UserProfile에는 승패 정보가 없으므로 기본값
                losses = 0,
                averageScore = userProfile.single_player_score, //  복원: 직접 사용
                isOnline = true,
                status = "로비",
                maxStageCompleted = userProfile.max_stage_completed //  핵심: 서버에서 받은 최대 클리어 스테이지
            };
            
            Debug.Log($"[ApiDataConverter] 프로필 변환 완료: {userProfile.username}, maxStageCompleted={userProfile.max_stage_completed}");
            
            return convertedUserInfo;
        }

        /// <summary>
        /// 별점 시스템 - 클라이언트에서 optimal_score 기반으로 계산
        /// </summary>
        public static int CalculateStars(int playerScore, int optimalScore)
        {
            if (optimalScore <= 0) return 0;

            float percentage = (float)playerScore / optimalScore;

            if (percentage >= 1f) return 3;      // 90% 이상: 3별
            if (percentage >= 0.9f) return 2;      // 70% 이상: 2별  
            if (percentage >= 0.8f) return 1;      // 50% 이상: 1별
            return 0;                               // 50% 미만: 0별
        }

        /// <summary>
        /// 언락 조건 확인 - 순차적 언락 시스템
        /// </summary>
        public static bool IsStageUnlocked(int stageNumber, int maxStageCompleted)
        {
            if (stageNumber == 1) return true; // 첫 번째 스테이지는 항상 언락
            return stageNumber <= maxStageCompleted + 1; // 바로 다음 스테이지까지만 언락
        }

        /// <summary>
        /// 진행률 계산
        /// </summary>
        public static float CalculateProgressPercentage(int completedStages, int totalStages)
        {
            if (totalStages <= 0) return 0f;
            return (float)completedStages / totalStages * 100f;
        }

        /// <summary>
        /// 압축된 응답 배열을 List로 변환
        /// </summary>
        public static List<T> ConvertCompactArray<TInput, T>(TInput[] compactArray, Func<TInput, T> converter)
        {
            var result = new List<T>();
            if (compactArray != null)
            {
                foreach (var item in compactArray)
                {
                    result.Add(converter(item));
                }
            }
            return result;
        }

        /// <summary>
        /// API 오류 메시지를 사용자 친화적 메시지로 변환
        /// </summary>
        public static string GetUserFriendlyErrorMessage(string apiError)
        {
            if (string.IsNullOrEmpty(apiError)) return "알 수 없는 오류가 발생했습니다.";

            return apiError.ToUpper() switch
            {
                "INVALID_CREDENTIALS" => "아이디 또는 비밀번호가 올바르지 않습니다.",
                "USER_NOT_FOUND" => "존재하지 않는 사용자입니다.",
                "STAGE_NOT_FOUND" => "스테이지를 찾을 수 없습니다.",
                "STAGE_LOCKED" => "이전 스테이지를 먼저 클리어해야 합니다.",
                "OAUTH_REDIRECT_REQUIRED" => "웹 브라우저에서 회원가입을 완료해주세요.",
                "VALIDATION_ERROR" => "입력 데이터가 올바르지 않습니다.",
                "INTERNAL_SERVER_ERROR" => "서버에서 오류가 발생했습니다. 잠시 후 다시 시도해주세요.",
                "MISSING_TOKEN" => "인증이 필요합니다. 다시 로그인해주세요.",
                "INVALID_TOKEN" => "인증 정보가 유효하지 않습니다. 다시 로그인해주세요.",
                _ => apiError
            };
        }

        /// <summary>
        /// 네트워크 연결 상태 확인
        /// </summary>
        public static bool IsNetworkAvailable()
        {
            return UnityEngine.Application.internetReachability != UnityEngine.NetworkReachability.NotReachable;
        }

        /// <summary>
        /// 디버그 정보 포함 로그 출력
        /// </summary>
        public static void LogApiResponse(string endpoint, bool success, string message, object data = null)
        {
            string logMessage = $"API [{endpoint}] {(success ? "SUCCESS" : "FAILED")}: {message}";

            if (data != null)
            {
                logMessage += $"\nData: {JsonUtility.ToJson(data, true)}";
            }

            if (success)
            {
                Debug.Log(logMessage);
            }
            else
            {
                Debug.LogWarning(logMessage);
            }
        }
    }
}