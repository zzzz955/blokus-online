#pragma once

#include "ServerTypes.h"
#include "JwtVerifier.h"
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
        // 인증 정보 메시지
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
        // 인증 서비스 관련 클래스
        // ========================================
        class AuthenticationService {
        public:
            explicit AuthenticationService(std::shared_ptr<DatabaseManager> dbManager = nullptr);
            ~AuthenticationService();

            // 초기화 및 종료
            bool initialize();
            void shutdown();

            // 계정 생성
            RegisterResult registerUser(const std::string& username, const std::string& password);

            // ID/PW 로그인
            AuthResult loginUser(const std::string& username, const std::string& password);

            // 게스트 로그인(미사용)
            AuthResult loginGuest(const std::string& guestName = "");

            // JWT 로그인
            AuthResult loginWithJwt(const std::string& jwtToken);

            // 모바일 클라이언트 전용 간소화된 인증 (사전 검증된 토큰)
            AuthResult authenticateMobileClient(const std::string& accessToken);

            // 로그아웃
            bool logoutUser(const std::string& sessionToken);

            // 세션 유효 확인
            std::optional<SessionInfo> validateSession(const std::string& sessionToken);

            // 세션 갱신
            bool refreshSession(const std::string& sessionToken);

            // 세션 무효화
            bool invalidateAllUserSessions(const std::string& userId);

            // 만료 세션 제거
            void cleanupExpiredSessions();

            // 유저 이름 사용 가능여부 확인
            bool isUsernameAvailable(const std::string& username);

            // 정규식 검증
            bool validateUsername(const std::string& username) const;
            bool validateEmail(const std::string& email) const;
            bool validatePassword(const std::string& password) const;

            size_t getActiveSessionCount() const;

        private:
            // 비밀번호 암호화/검증
            std::string hashPassword(const std::string& password) const;
            std::string generateSalt() const;
            bool verifyPassword(const std::string& password, const std::string& hash) const;

            // 세션 토큰 생성 및 초기화
            std::string generateSessionToken() const;
            std::string generateResetToken() const;

            // 세션 만료 시간 반환
            std::chrono::system_clock::time_point getSessionExpireTime() const;

            // 게스트 관련(미사용)
            std::string generateGuestUsername();
            std::string generateGuestUserId() const;

            // 세션 생성 및 제거
            bool storeSession(const std::string& token, const std::string& userId, const std::string& username);
            bool removeSession(const std::string& token);
            std::optional<SessionInfo> getSessionInfo(const std::string& token) const;

            // 이름/이메일 검증용
            std::string normalizeUsername(const std::string& username) const;
            std::string normalizeEmail(const std::string& email) const;

        private:
            std::shared_ptr<DatabaseManager> m_dbManager;
            std::unique_ptr<JwtVerifier> m_jwtVerifier;

            // 세션 뮤텍스
            mutable std::mutex m_sessionsMutex;
            std::unordered_map<std::string, SessionInfo> m_activeSessions;

            // 세션 관리용
            std::chrono::hours m_sessionDuration{ 24 };
            std::chrono::minutes m_resetTokenDuration{ 30 };
            int m_minPasswordLength{ 6 };
            int m_maxUsernameLength{ 20 };
            int m_minUsernameLength{ 3 };

            std::atomic<bool> m_isInitialized{ false };
            std::atomic<uint32_t> m_guestCounter{ 1000 };
        };

    } // namespace Server
} // namespace Blokus