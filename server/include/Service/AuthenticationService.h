#pragma once

#include "common/ServerTypes.h"
#include <string>
#include <memory>
#include <future>
#include <chrono>
#include <optional>
#include <atomic>

namespace Blokus {
    namespace Server {

        // 전방 선언
        class DatabaseManager;

        // ========================================
        // 인증 결과 구조체
        // ========================================
        struct AuthResult {
            bool success;
            std::string message;
            std::string userId;
            std::string sessionToken;
            std::string username;

            AuthResult(bool s = false, const std::string& msg = "",
                const std::string& id = "", const std::string& token = "",
                const std::string& user = "")
                : success(s), message(msg), userId(id), sessionToken(token), username(user) {
            }
        };

        struct RegisterResult {
            bool success;
            std::string message;
            std::string userId;

            RegisterResult(bool s = false, const std::string& msg = "", const std::string& id = "")
                : success(s), message(msg), userId(id) {
            }
        };

        struct SessionInfo {
            std::string userId;
            std::string username;
            std::chrono::system_clock::time_point expiresAt;
            bool isValid;

            SessionInfo() : isValid(false) {}
        };

        // ========================================
        // 인증 서비스 클래스
        // ========================================
        class AuthenticationService {
        public:
            explicit AuthenticationService(std::shared_ptr<DatabaseManager> dbManager = nullptr);
            ~AuthenticationService();

            // 초기화
            bool initialize();
            void shutdown();

            // ========================================
            // 회원가입/로그인 (동기 버전 - 단순화)
            // ========================================

            // 회원가입
            RegisterResult registerUser(const std::string& username, const std::string& password);

            // 로그인 (아이디/패스워드)
            AuthResult loginUser(const std::string& username, const std::string& password);

            // 게스트 로그인 (임시 계정)
            AuthResult loginGuest(const std::string& guestName = "");

            // 로그아웃
            bool logoutUser(const std::string& sessionToken);

            // ========================================
            // 세션 관리
            // ========================================

            // 세션 검증
            std::optional<SessionInfo> validateSession(const std::string& sessionToken);

            // 세션 갱신
            bool refreshSession(const std::string& sessionToken);

            // 모든 세션 무효화 (비밀번호 변경 시 등)
            bool invalidateAllUserSessions(const std::string& userId);

            // 만료된 세션 정리
            void cleanupExpiredSessions();

            // ========================================
            // 계정 관리
            // ========================================

            // 비밀번호 변경
            bool changePassword(const std::string& userId, const std::string& oldPassword,
                const std::string& newPassword);

            // 비밀번호 재설정 (이메일 기반)
            bool requestPasswordReset(const std::string& email);
            bool resetPassword(const std::string& resetToken, const std::string& newPassword);

            // 계정 삭제
            bool deleteAccount(const std::string& userId, const std::string& password);

            // ========================================
            // 검증 함수들
            // ========================================

            // 사용자명/이메일 중복 확인
            bool isUsernameAvailable(const std::string& username);
            bool isEmailAvailable(const std::string& email);

            // 입력 데이터 검증
            bool validateUsername(const std::string& username) const;
            bool validateEmail(const std::string& email) const;
            bool validatePassword(const std::string& password) const;

            // ========================================
            // 통계 및 정보
            // ========================================
            size_t getActiveSessionCount() const;
            std::chrono::system_clock::time_point getLastLoginTime(const std::string& userId) const;

        private:
            // 암호화/해시
            std::string hashPassword(const std::string& password, const std::string& salt = "") const;
            std::string generateSalt() const;
            bool verifyPassword(const std::string& password, const std::string& hash) const;

            // 세션 토큰 생성
            std::string generateSessionToken() const;
            std::string generateResetToken() const;

            // 세션 만료 시간 계산
            std::chrono::system_clock::time_point getSessionExpireTime() const;

            // 게스트 계정 관리
            std::string generateGuestUsername();
            std::string generateGuestUserId() const;

            // 내부 헬퍼 함수들
            bool storeSession(const std::string& token, const std::string& userId, const std::string& username);
            bool removeSession(const std::string& token);
            std::optional<SessionInfo> getSessionInfo(const std::string& token) const;

            // 입력 데이터 정규화
            std::string normalizeUsername(const std::string& username) const;
            std::string normalizeEmail(const std::string& email) const;

        private:
            std::shared_ptr<DatabaseManager> m_dbManager;

            // 세션 저장소 (메모리 기반 - 단순화)
            mutable std::mutex m_sessionsMutex;
            std::unordered_map<std::string, SessionInfo> m_activeSessions;

            // 설정
            std::chrono::hours m_sessionDuration{ 24 };        // 세션 유효 기간
            std::chrono::minutes m_resetTokenDuration{ 30 };   // 리셋 토큰 유효 기간
            int m_minPasswordLength{ 6 };                      // 최소 비밀번호 길이
            int m_maxUsernameLength{ 20 };                     // 최대 사용자명 길이
            int m_minUsernameLength{ 3 };                      // 최소 사용자명 길이

            std::atomic<bool> m_isInitialized{ false };
            std::atomic<uint32_t> m_guestCounter{ 1000 };      // 게스트 번호 카운터
        };

    } // namespace Server
} // namespace Blokus