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

        // ���� ����
        class DatabaseManager;

        // ========================================
        // ���� ��� ����ü
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
        // ���� ���� Ŭ����
        // ========================================
        class AuthenticationService {
        public:
            explicit AuthenticationService(std::shared_ptr<DatabaseManager> dbManager = nullptr);
            ~AuthenticationService();

            // �ʱ�ȭ
            bool initialize();
            void shutdown();

            // ========================================
            // ȸ������/�α��� (���� ���� - �ܼ�ȭ)
            // ========================================

            // ȸ������
            RegisterResult registerUser(const std::string& username, const std::string& password);

            // �α��� (���̵�/�н�����)
            AuthResult loginUser(const std::string& username, const std::string& password);

            // �Խ�Ʈ �α��� (�ӽ� ����)
            AuthResult loginGuest(const std::string& guestName = "");

            // �α׾ƿ�
            bool logoutUser(const std::string& sessionToken);

            // ========================================
            // ���� ����
            // ========================================

            // ���� ����
            std::optional<SessionInfo> validateSession(const std::string& sessionToken);

            // ���� ����
            bool refreshSession(const std::string& sessionToken);

            // ��� ���� ��ȿȭ (��й�ȣ ���� �� ��)
            bool invalidateAllUserSessions(const std::string& userId);

            // ����� ���� ����
            void cleanupExpiredSessions();

            // ========================================
            // ���� ����
            // ========================================

            // ��й�ȣ ����
            bool changePassword(const std::string& userId, const std::string& oldPassword,
                const std::string& newPassword);

            // ��й�ȣ �缳�� (�̸��� ���)
            bool requestPasswordReset(const std::string& email);
            bool resetPassword(const std::string& resetToken, const std::string& newPassword);

            // ���� ����
            bool deleteAccount(const std::string& userId, const std::string& password);

            // ========================================
            // ���� �Լ���
            // ========================================

            // ����ڸ�/�̸��� �ߺ� Ȯ��
            bool isUsernameAvailable(const std::string& username);
            bool isEmailAvailable(const std::string& email);

            // �Է� ������ ����
            bool validateUsername(const std::string& username) const;
            bool validateEmail(const std::string& email) const;
            bool validatePassword(const std::string& password) const;

            // ========================================
            // ��� �� ����
            // ========================================
            size_t getActiveSessionCount() const;
            std::chrono::system_clock::time_point getLastLoginTime(const std::string& userId) const;

        private:
            // ��ȣȭ/�ؽ�
            std::string hashPassword(const std::string& password, const std::string& salt = "") const;
            std::string generateSalt() const;
            bool verifyPassword(const std::string& password, const std::string& hash) const;

            // ���� ��ū ����
            std::string generateSessionToken() const;
            std::string generateResetToken() const;

            // ���� ���� �ð� ���
            std::chrono::system_clock::time_point getSessionExpireTime() const;

            // �Խ�Ʈ ���� ����
            std::string generateGuestUsername();
            std::string generateGuestUserId() const;

            // ���� ���� �Լ���
            bool storeSession(const std::string& token, const std::string& userId, const std::string& username);
            bool removeSession(const std::string& token);
            std::optional<SessionInfo> getSessionInfo(const std::string& token) const;

            // �Է� ������ ����ȭ
            std::string normalizeUsername(const std::string& username) const;
            std::string normalizeEmail(const std::string& email) const;

        private:
            std::shared_ptr<DatabaseManager> m_dbManager;

            // ���� ����� (�޸� ��� - �ܼ�ȭ)
            mutable std::mutex m_sessionsMutex;
            std::unordered_map<std::string, SessionInfo> m_activeSessions;

            // ����
            std::chrono::hours m_sessionDuration{ 24 };        // ���� ��ȿ �Ⱓ
            std::chrono::minutes m_resetTokenDuration{ 30 };   // ���� ��ū ��ȿ �Ⱓ
            int m_minPasswordLength{ 6 };                      // �ּ� ��й�ȣ ����
            int m_maxUsernameLength{ 20 };                     // �ִ� ����ڸ� ����
            int m_minUsernameLength{ 3 };                      // �ּ� ����ڸ� ����

            std::atomic<bool> m_isInitialized{ false };
            std::atomic<uint32_t> m_guestCounter{ 1000 };      // �Խ�Ʈ ��ȣ ī����
        };

    } // namespace Server
} // namespace Blokus