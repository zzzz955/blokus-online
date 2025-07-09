#pragma once

#include "common/ServerTypes.h"
#include <string>
#include <memory>
#include <future>
#include <chrono>

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
            uint32_t userId;
            std::string sessionToken;

            AuthResult(bool s = false, const std::string& msg = "", uint32_t id = 0, const std::string& token = "")
                : success(s), message(msg), userId(id), sessionToken(token) {
            }
        };

        struct RegisterResult {
            bool success;
            std::string message;
            uint32_t userId;

            RegisterResult(bool s = false, const std::string& msg = "", uint32_t id = 0)
                : success(s), message(msg), userId(id) {
            }
        };

        // ========================================
        // 인증 서비스 클래스 (DB 기반)
        // ========================================
        class AuthenticationService {
        public:
            explicit AuthenticationService(std::shared_ptr<DatabaseManager> dbManager);
            ~AuthenticationService();

            // 초기화
            bool initialize();
            void shutdown();

            // ========================================
            // 회원가입/로그인
            // ========================================

            // 회원가입
            std::future<RegisterResult> registerUser(const std::string& username,
                const std::string& email,
                const std::string& password);

            // 로그인 (아이디/패스워드)
            std::future<AuthResult> loginUser(const std::string& username, const std::string& password);

            // 게스트 로그인 (임시 계정)
            std::future<AuthResult> loginGuest(const std::string& guestName);

            // 로그아웃
            std::future<bool> logoutUser(const std::string& sessionToken);

            // ========================================
            // 세션 관리
            // ========================================

            // 세션 검증
            std::future<std::optional<uint32_t>> validateSession(const std::string& sessionToken);

            // 세션 갱신
            std::future<bool> refreshSession(const std::string& sessionToken);

            // 모든 세션 무효화 (비밀번호 변경 시 등)
            std::future<bool> invalidateAllUserSessions(uint32_t userId);

            // ========================================
            // 계정 관리
            // ========================================

            // 비밀번호 변경
            std::future<bool> changePassword(uint32_t userId, const std::string& oldPassword,
                const std::string& newPassword);

            // 비밀번호 재설정 (이메일 기반)
            std::future<bool> requestPasswordReset(const std::string& email);
            std::future<bool> resetPassword(const std::string& resetToken, const std::string& newPassword);

            // 계정 삭제
            std::future<bool> deleteAccount(uint32_t userId, const std::string& password);

            // ========================================
            // 검증 함수들
            // ========================================

            // 사용자명/이메일 중복 확인
            std::future<bool> isUsernameAvailable(const std::string& username);
            std::future<bool> isEmailAvailable(const std::string& email);

            // 입력 데이터 검증
            bool validateUsername(const std::string& username) const;
            bool validateEmail(const std::string& email) const;
            bool validatePassword(const std::string& password) const;

        private:
            // 암호화/해싱
            std::string hashPassword(const std::string& password, const std::string& salt = "") const;
            std::string generateSalt() const;
            bool verifyPassword(const std::string& password, const std::string& hash) const;

            // 세션 토큰 생성
            std::string generateSessionToken() const;
            std::string generateResetToken() const;

            // 세션 만료 시간 계산
            std::chrono::system_clock::time_point getSessionExpireTime() const;

            // 게스트 계정 관리
            std::string generateGuestUsername() const;

        private:
            std::shared_ptr<DatabaseManager> m_dbManager;

            // 설정
            std::chrono::hours m_sessionDuration{ 24 };        // 세션 유효 기간
            std::chrono::minutes m_resetTokenDuration{ 30 };   // 리셋 토큰 유효 기간
            int m_minPasswordLength{ 8 };                      // 최소 비밀번호 길이
            int m_maxUsernameLength{ 20 };                     // 최대 사용자명 길이

            std::atomic<bool> m_isInitialized{ false };
        };

    } // namespace Server
} // namespace Blokus