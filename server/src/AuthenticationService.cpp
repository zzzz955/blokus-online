﻿#include "AuthenticationService.h"
#include "DatabaseManager.h"
#include "ConfigManager.h"
#include <spdlog/spdlog.h>
#include <openssl/sha.h>
#include <openssl/rand.h>
#include <random>
#include <regex>
#include <iomanip>
#include <sstream>

namespace Blokus {
    namespace Server {

        // ========================================
        // 생성자 및 소멸자
        // ========================================

        AuthenticationService::AuthenticationService(std::shared_ptr<DatabaseManager> dbManager)
            : m_dbManager(dbManager)
        {
            spdlog::info("AuthenticationService 생성");
        }

        AuthenticationService::~AuthenticationService() {
            shutdown();
            spdlog::info("AuthenticationService 소멸");
        }

        // ========================================
        // 초기화 및 종료
        // ========================================

        bool AuthenticationService::initialize() {
            if (m_isInitialized.load()) {
                return true;
            }

            try {
                spdlog::info("AuthenticationService 초기화 시작");

                // 설정 로드
                m_sessionDuration = std::chrono::hours(ConfigManager::sessionTimeoutHours);
                m_minPasswordLength = 6; // 기본값 또는 설정에서 로드

                // OpenSSL 초기화 확인
                if (RAND_status() != 1) {
                    spdlog::warn("OpenSSL 랜덤 생성기 초기화 확인 필요");
                }

                m_isInitialized.store(true);
                spdlog::info("AuthenticationService 초기화 완료");
                return true;
            }
            catch (const std::exception& e) {
                spdlog::error("AuthenticationService 초기화 실패: {}", e.what());
                return false;
            }
        }

        void AuthenticationService::shutdown() {
            if (!m_isInitialized.load()) {
                return;
            }

            spdlog::info("AuthenticationService 종료 시작");

            // 활성 세션 정리
            {
                std::lock_guard<std::mutex> lock(m_sessionsMutex);
                m_activeSessions.clear();
            }

            m_isInitialized.store(false);
            spdlog::info("AuthenticationService 종료 완료");
        }

        // ========================================
        // 회원가입/로그인
        // ========================================

        RegisterResult AuthenticationService::registerUser(const std::string& username, const std::string& password) {
            try {
                // 입력 데이터 검증
                if (!validateUsername(username)) {
                    return RegisterResult(false, "잘못된 사용자명 형식입니다", "");
                }


                if (!validatePassword(password)) {
                    return RegisterResult(false,
                        "비밀번호는 " + std::to_string(m_minPasswordLength) + "자 이상이어야 합니다", "");
                }

                // DB 연결 확인
                if (!m_dbManager) {
                    spdlog::error("DatabaseManager가 초기화되지 않았습니다");
                    return RegisterResult(false, "서버 오류가 발생했습니다", "");
                }

                // 중복 확인 (DB에서 실제 확인)
                if (!m_dbManager->isUsernameAvailable(username)) {
                    spdlog::warn("회원가입 실패: 이미 사용 중인 사용자명 {}", username);
                    return RegisterResult(false, "이미 사용 중인 사용자명입니다", "");
                }


                // 비밀번호 해시화
                std::string salt = generateSalt();
                std::string hashedPassword = hashPassword(password, salt);

                spdlog::info("새 사용자 등록 시도: {}", username);

                // 데이터베이스에 저장
                if (m_dbManager->createUser(username, hashedPassword)) {
                    spdlog::info("새 사용자 등록 성공: {}", username);
                    return RegisterResult(true, "회원가입이 완료되었습니다", username);
                }
                else {
                    spdlog::error("데이터베이스 사용자 생성 실패: {}", username);
                    return RegisterResult(false, "사용자 등록에 실패했습니다", "");
                }
            }
            catch (const std::exception& e) {
                spdlog::error("사용자 등록 중 오류: {}", e.what());
                return RegisterResult(false, "서버 오류가 발생했습니다", "");
            }
        }

        AuthResult AuthenticationService::loginUser(const std::string& username, const std::string& password) {
            try {
                // 입력 데이터 검증
                if (username.empty() || password.empty()) {
                    return AuthResult(false, "사용자명과 비밀번호를 입력해주세요", "", "", "");
                }

                // DB 연결 확인
                if (!m_dbManager) {
                    spdlog::error("DatabaseManager가 초기화되지 않았습니다");
                    return AuthResult(false, "서버 오류가 발생했습니다", "", "", "");
                }

                spdlog::info("로그인 시도: {}", username);

                // 사용자 정보 조회
                auto userAccount = m_dbManager->getUserByUsername(username);
                if (!userAccount) {
                    spdlog::warn("로그인 실패: 사용자 없음 {}", username);
                    return AuthResult(false, "잘못된 사용자명 또는 비밀번호입니다", "", "", "");
                }

                // 비밀번호 검증
                if (!verifyPassword(password, userAccount->passwordHash)) {
                    spdlog::warn("로그인 실패: 비밀번호 불일치 {}", username);
                    return AuthResult(false, "잘못된 사용자명 또는 비밀번호입니다", "", "", "");
                }

                // 사용자 계정 활성화 상태 확인
                if (!userAccount->isActive) {
                    spdlog::warn("로그인 실패: 비활성화된 계정 {}", username);
                    return AuthResult(false, "비활성화된 계정입니다", "", "", "");
                }

                std::string userId = std::to_string(userAccount->userId);

                // 세션 토큰 생성
                std::string sessionToken = generateSessionToken();

                // 세션 저장
                if (!storeSession(sessionToken, userId, username)) {
                    return AuthResult(false, "세션 생성에 실패했습니다", "", "", "");
                }

                // 마지막 로그인 시간 업데이트
                m_dbManager->updateUserLastLogin(userAccount->userId);

                spdlog::info("로그인 성공: {} (ID: {})", username, userId);
                return AuthResult(true, "로그인되었습니다", userId, sessionToken, username);
            }
            catch (const std::exception& e) {
                spdlog::error("로그인 중 오류: {}", e.what());
                return AuthResult(false, "서버 오류가 발생했습니다", "", "", "");
            }
        }

        AuthResult AuthenticationService::loginGuest(const std::string& guestName) {
            try {
                // 게스트 사용자명 생성
                std::string username = guestName.empty() ? generateGuestUsername() : guestName;
                std::string userId = generateGuestUserId();

                // 게스트 사용자명 검증
                if (!validateUsername(username)) {
                    username = generateGuestUsername();
                }

                // 세션 토큰 생성
                std::string sessionToken = generateSessionToken();

                // 세션 저장
                if (!storeSession(sessionToken, userId, username)) {
                    return AuthResult(false, "게스트 세션 생성에 실패했습니다", "", "", "");
                }

                spdlog::info("게스트 로그인: {} (ID: {})", username, userId);
                return AuthResult(true, "게스트로 로그인되었습니다", userId, sessionToken, username);
            }
            catch (const std::exception& e) {
                spdlog::error("게스트 로그인 중 오류: {}", e.what());
                return AuthResult(false, "서버 오류가 발생했습니다", "", "", "");
            }
        }

        bool AuthenticationService::logoutUser(const std::string& sessionToken) {
            try {
                if (sessionToken.empty()) {
                    return false;
                }

                bool success = removeSession(sessionToken);
                if (success) {
                    spdlog::info("로그아웃 완료: {}", sessionToken.substr(0, 8) + "...");
                }
                return success;
            }
            catch (const std::exception& e) {
                spdlog::error("로그아웃 중 오류: {}", e.what());
                return false;
            }
        }

        // ========================================
        // 세션 관리
        // ========================================

        std::optional<SessionInfo> AuthenticationService::validateSession(const std::string& sessionToken) {
            try {
                if (sessionToken.empty()) {
                    return std::nullopt;
                }

                auto sessionInfo = getSessionInfo(sessionToken);
                if (!sessionInfo) {
                    return std::nullopt;
                }

                // 만료 확인
                auto now = std::chrono::system_clock::now();
                if (now > sessionInfo->expiresAt) {
                    removeSession(sessionToken);
                    return std::nullopt;
                }

                return sessionInfo;
            }
            catch (const std::exception& e) {
                spdlog::error("세션 검증 중 오류: {}", e.what());
                return std::nullopt;
            }
        }

        bool AuthenticationService::refreshSession(const std::string& sessionToken) {
            try {
                std::lock_guard<std::mutex> lock(m_sessionsMutex);

                auto it = m_activeSessions.find(sessionToken);
                if (it == m_activeSessions.end()) {
                    return false;
                }

                // 세션 만료 시간 연장
                it->second.expiresAt = getSessionExpireTime();

                spdlog::debug("세션 갱신: {}", sessionToken.substr(0, 8) + "...");
                return true;
            }
            catch (const std::exception& e) {
                spdlog::error("세션 갱신 중 오류: {}", e.what());
                return false;
            }
        }

        bool AuthenticationService::invalidateAllUserSessions(const std::string& userId) {
            try {
                std::lock_guard<std::mutex> lock(m_sessionsMutex);

                auto it = m_activeSessions.begin();
                int count = 0;

                while (it != m_activeSessions.end()) {
                    if (it->second.userId == userId) {
                        it = m_activeSessions.erase(it);
                        count++;
                    }
                    else {
                        ++it;
                    }
                }

                spdlog::info("사용자 {} 모든 세션 무효화: {}개", userId, count);
                return true;
            }
            catch (const std::exception& e) {
                spdlog::error("세션 무효화 중 오류: {}", e.what());
                return false;
            }
        }

        void AuthenticationService::cleanupExpiredSessions() {
            try {
                std::lock_guard<std::mutex> lock(m_sessionsMutex);

                auto now = std::chrono::system_clock::now();
                auto it = m_activeSessions.begin();
                int count = 0;

                while (it != m_activeSessions.end()) {
                    if (now > it->second.expiresAt) {
                        it = m_activeSessions.erase(it);
                        count++;
                    }
                    else {
                        ++it;
                    }
                }

                if (count > 0) {
                    spdlog::info("만료된 세션 정리: {}개", count);
                }
            }
            catch (const std::exception& e) {
                spdlog::error("세션 정리 중 오류: {}", e.what());
            }
        }

        // ========================================
        // 계정 관리
        // ========================================


        // ========================================
        // 검증 함수들
        // ========================================

        bool AuthenticationService::isUsernameAvailable(const std::string& username) {
            try {
                // TODO: 데이터베이스에서 중복 확인

                // 임시: 메모리 세션에서 확인
                std::lock_guard<std::mutex> lock(m_sessionsMutex);
                for (const auto& [token, session] : m_activeSessions) {
                    if (session.username == username) {
                        return false;
                    }
                }

                return true;
            }
            catch (const std::exception& e) {
                spdlog::error("사용자명 중복 확인 중 오류: {}", e.what());
                return false;
            }
        }


        bool AuthenticationService::validateUsername(const std::string& username) const {
            if (username.length() < m_minUsernameLength || username.length() > m_maxUsernameLength) {
                return false;
            }

            // 영문, 숫자, 언더스코어만 허용
            std::regex usernamePattern("^[a-zA-Z0-9_]+$");
            return std::regex_match(username, usernamePattern);
        }

        bool AuthenticationService::validateEmail(const std::string& email) const {
            if (email.empty() || email.length() > 100) {
                return false;
            }

            // 간단한 이메일 형식 검증
            std::regex emailPattern(R"([a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})");
            return std::regex_match(email, emailPattern);
        }

        bool AuthenticationService::validatePassword(const std::string& password) const {
            return password.length() >= m_minPasswordLength && password.length() <= 100;
        }

        // ========================================
        // 통계 및 정보
        // ========================================

        size_t AuthenticationService::getActiveSessionCount() const {
            std::lock_guard<std::mutex> lock(m_sessionsMutex);
            return m_activeSessions.size();
        }


        // ========================================
        // 내부 헬퍼 함수들
        // ========================================

        std::string AuthenticationService::hashPassword(const std::string& password, const std::string& salt) const {
            try {
                std::string saltedPassword = password + salt;

                unsigned char hash[SHA256_DIGEST_LENGTH];
                SHA256_CTX sha256;
                SHA256_Init(&sha256);
                SHA256_Update(&sha256, saltedPassword.c_str(), saltedPassword.length());
                SHA256_Final(hash, &sha256);

                std::stringstream ss;
                for (int i = 0; i < SHA256_DIGEST_LENGTH; i++) {
                    ss << std::hex << std::setw(2) << std::setfill('0') << static_cast<int>(hash[i]);
                }

                return salt + ":" + ss.str();
            }
            catch (const std::exception& e) {
                spdlog::error("비밀번호 해시 중 오류: {}", e.what());
                return "";
            }
        }

        std::string AuthenticationService::generateSalt() const {
            try {
                unsigned char salt[16];
                if (RAND_bytes(salt, sizeof(salt)) != 1) {
                    // Fallback to random_device
                    std::random_device rd;
                    std::mt19937 gen(rd());
                    std::uniform_int_distribution<> dis(0, 255);
                    for (int i = 0; i < 16; ++i) {
                        salt[i] = dis(gen);
                    }
                }

                std::stringstream ss;
                for (int i = 0; i < 16; ++i) {
                    ss << std::hex << std::setw(2) << std::setfill('0') << static_cast<int>(salt[i]);
                }
                return ss.str();
            }
            catch (const std::exception& e) {
                spdlog::error("솔트 생성 중 오류: {}", e.what());
                return "fallback_salt_12345678";
            }
        }

        bool AuthenticationService::verifyPassword(const std::string& password, const std::string& hash) const {
            try {
                size_t colonPos = hash.find(':');
                if (colonPos == std::string::npos) {
                    return false;
                }

                std::string salt = hash.substr(0, colonPos);
                std::string expectedHash = hash.substr(colonPos + 1);

                std::string computedHash = hashPassword(password, salt);
                size_t computedColonPos = computedHash.find(':');
                if (computedColonPos == std::string::npos) {
                    return false;
                }

                std::string computedHashPart = computedHash.substr(computedColonPos + 1);
                return expectedHash == computedHashPart;
            }
            catch (const std::exception& e) {
                spdlog::error("비밀번호 검증 중 오류: {}", e.what());
                return false;
            }
        }

        std::string AuthenticationService::generateSessionToken() const {
            try {
                unsigned char randomBytes[32];
                if (RAND_bytes(randomBytes, sizeof(randomBytes)) != 1) {
                    // Fallback
                    std::random_device rd;
                    std::mt19937 gen(rd());
                    std::uniform_int_distribution<> dis(0, 255);
                    for (int i = 0; i < 32; ++i) {
                        randomBytes[i] = dis(gen);
                    }
                }

                std::stringstream ss;
                for (int i = 0; i < 32; ++i) {
                    ss << std::hex << std::setw(2) << std::setfill('0') << static_cast<int>(randomBytes[i]);
                }

                return ss.str();
            }
            catch (const std::exception& e) {
                spdlog::error("세션 토큰 생성 중 오류: {}", e.what());
                auto now = std::chrono::system_clock::now();
                auto timestamp = now.time_since_epoch().count();
                return "fallback_" + std::to_string(timestamp);
            }
        }

        std::string AuthenticationService::generateResetToken() const {
            return "reset_" + generateSessionToken();
        }

        std::chrono::system_clock::time_point AuthenticationService::getSessionExpireTime() const {
            return std::chrono::system_clock::now() + m_sessionDuration;
        }

        std::string AuthenticationService::generateGuestUsername() {  // const 제거
            uint32_t guestNum = m_guestCounter.fetch_add(1, std::memory_order_relaxed);
            return "Guest" + std::to_string(guestNum);
        }

        std::string AuthenticationService::generateGuestUserId() const {
            return "guest_" + generateSessionToken().substr(0, 12);
        }

        bool AuthenticationService::storeSession(const std::string& token, const std::string& userId, const std::string& username) {
            try {
                std::lock_guard<std::mutex> lock(m_sessionsMutex);

                SessionInfo sessionInfo;
                sessionInfo.userId = userId;
                sessionInfo.username = username;
                sessionInfo.expiresAt = getSessionExpireTime();
                sessionInfo.isValid = true;

                m_activeSessions[token] = sessionInfo;

                spdlog::debug("세션 저장: {} -> {} ({})", token.substr(0, 8) + "...", username, userId);
                return true;
            }
            catch (const std::exception& e) {
                spdlog::error("세션 저장 중 오류: {}", e.what());
                return false;
            }
        }

        bool AuthenticationService::removeSession(const std::string& token) {
            try {
                std::lock_guard<std::mutex> lock(m_sessionsMutex);

                auto it = m_activeSessions.find(token);
                if (it != m_activeSessions.end()) {
                    spdlog::debug("세션 제거: {} ({})", token.substr(0, 8) + "...", it->second.username);
                    m_activeSessions.erase(it);
                    return true;
                }
                return false;
            }
            catch (const std::exception& e) {
                spdlog::error("세션 제거 중 오류: {}", e.what());
                return false;
            }
        }

        std::optional<SessionInfo> AuthenticationService::getSessionInfo(const std::string& token) const {
            try {
                std::lock_guard<std::mutex> lock(m_sessionsMutex);

                auto it = m_activeSessions.find(token);
                if (it != m_activeSessions.end()) {
                    return it->second;
                }
                return std::nullopt;
            }
            catch (const std::exception& e) {
                spdlog::error("세션 정보 조회 중 오류: {}", e.what());
                return std::nullopt;
            }
        }

        std::string AuthenticationService::normalizeUsername(const std::string& username) const {
            std::string normalized = username;

            // 앞뒤 공백 제거
            size_t start = normalized.find_first_not_of(" \t\n\r");
            size_t end = normalized.find_last_not_of(" \t\n\r");

            if (start == std::string::npos) {
                return "";
            }

            normalized = normalized.substr(start, end - start + 1);

            // 소문자로 변환 (선택사항)
            // std::transform(normalized.begin(), normalized.end(), normalized.begin(), ::tolower);

            return normalized;
        }

        std::string AuthenticationService::normalizeEmail(const std::string& email) const {
            std::string normalized = email;

            // 앞뒤 공백 제거
            size_t start = normalized.find_first_not_of(" \t\n\r");
            size_t end = normalized.find_last_not_of(" \t\n\r");

            if (start == std::string::npos) {
                return "";
            }

            normalized = normalized.substr(start, end - start + 1);

            // 소문자로 변환
            std::transform(normalized.begin(), normalized.end(), normalized.begin(), ::tolower);

            return normalized;
        }

    } // namespace Server
} // namespace Blokus