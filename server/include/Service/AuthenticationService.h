#pragma once

#include "common/ServerTypes.h"
#include <string>
#include <memory>
#include <future>
#include <chrono>

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
        // ���� ���� Ŭ���� (DB ���)
        // ========================================
        class AuthenticationService {
        public:
            explicit AuthenticationService(std::shared_ptr<DatabaseManager> dbManager);
            ~AuthenticationService();

            // �ʱ�ȭ
            bool initialize();
            void shutdown();

            // ========================================
            // ȸ������/�α���
            // ========================================

            // ȸ������
            std::future<RegisterResult> registerUser(const std::string& username,
                const std::string& email,
                const std::string& password);

            // �α��� (���̵�/�н�����)
            std::future<AuthResult> loginUser(const std::string& username, const std::string& password);

            // �Խ�Ʈ �α��� (�ӽ� ����)
            std::future<AuthResult> loginGuest(const std::string& guestName);

            // �α׾ƿ�
            std::future<bool> logoutUser(const std::string& sessionToken);

            // ========================================
            // ���� ����
            // ========================================

            // ���� ����
            std::future<std::optional<uint32_t>> validateSession(const std::string& sessionToken);

            // ���� ����
            std::future<bool> refreshSession(const std::string& sessionToken);

            // ��� ���� ��ȿȭ (��й�ȣ ���� �� ��)
            std::future<bool> invalidateAllUserSessions(uint32_t userId);

            // ========================================
            // ���� ����
            // ========================================

            // ��й�ȣ ����
            std::future<bool> changePassword(uint32_t userId, const std::string& oldPassword,
                const std::string& newPassword);

            // ��й�ȣ �缳�� (�̸��� ���)
            std::future<bool> requestPasswordReset(const std::string& email);
            std::future<bool> resetPassword(const std::string& resetToken, const std::string& newPassword);

            // ���� ����
            std::future<bool> deleteAccount(uint32_t userId, const std::string& password);

            // ========================================
            // ���� �Լ���
            // ========================================

            // ����ڸ�/�̸��� �ߺ� Ȯ��
            std::future<bool> isUsernameAvailable(const std::string& username);
            std::future<bool> isEmailAvailable(const std::string& email);

            // �Է� ������ ����
            bool validateUsername(const std::string& username) const;
            bool validateEmail(const std::string& email) const;
            bool validatePassword(const std::string& password) const;

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
            std::string generateGuestUsername() const;

        private:
            std::shared_ptr<DatabaseManager> m_dbManager;

            // ����
            std::chrono::hours m_sessionDuration{ 24 };        // ���� ��ȿ �Ⱓ
            std::chrono::minutes m_resetTokenDuration{ 30 };   // ���� ��ū ��ȿ �Ⱓ
            int m_minPasswordLength{ 8 };                      // �ּ� ��й�ȣ ����
            int m_maxUsernameLength{ 20 };                     // �ִ� ����ڸ� ����

            std::atomic<bool> m_isInitialized{ false };
        };

    } // namespace Server
} // namespace Blokus