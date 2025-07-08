#pragma once

#include "server/common/ServerTypes.h"
#include <string>
#include <memory>
#include <unordered_map>
#include <mutex>
#include <chrono>

namespace Blokus {
    namespace Server {

        // ========================================
        // ����� ���� ����ü (UserManager ��� ����)
        // ========================================
        struct UserInfo {
            std::string userId;
            std::string username;
            std::string email;
            std::string passwordHash;
            int rating;
            int gamesPlayed;
            int gamesWon;
            std::chrono::system_clock::time_point lastLogin;
            std::chrono::system_clock::time_point createdAt;
            bool isOnline;
            bool isVerified;

            UserInfo()
                : rating(1000)
                , gamesPlayed(0)
                , gamesWon(0)
                , isOnline(false)
                , isVerified(false)
            {
            }

            // �·� ���
            double getWinRate() const {
                return gamesPlayed > 0 ? static_cast<double>(gamesWon) / gamesPlayed * 100.0 : 0.0;
            }
        };

        // ========================================
        // ���� ���� Ŭ���� (UserManager ��� ����)
        // ========================================
        class AuthenticationService {
        public:
            AuthenticationService();
            ~AuthenticationService();

            // ���� ó��
            bool authenticate(const std::string& username, const std::string& password);
            std::string generateSessionToken(const std::string& userId);
            bool validateSessionToken(const std::string& token);
            bool refreshSessionToken(const std::string& oldToken, std::string& newToken);

            // ȸ������
            bool registerUser(const std::string& username, const std::string& password, const std::string& email);
            bool verifyEmail(const std::string& token);

            // ����� ���� ���� (UserManager ��� ����)
            std::shared_ptr<UserInfo> getUserInfo(const std::string& userId);
            std::shared_ptr<UserInfo> getUserByUsername(const std::string& username);
            bool updateUserInfo(const std::string& userId, const UserInfo& userInfo);
            void updateUserRating(const std::string& userId, int newRating);
            void recordGameResult(const std::string& userId, bool won);

            // �¶��� ���� ����
            void setUserOnline(const std::string& userId, bool online);
            std::vector<std::string> getOnlineUsers();
            bool isUserOnline(const std::string& userId) const;

            // ��й�ȣ ����
            bool changePassword(const std::string& userId, const std::string& oldPassword, const std::string& newPassword);
            bool resetPassword(const std::string& email);

            // ���� ����
            bool isSessionValid(const std::string& sessionToken) const;
            std::string getUserIdFromSession(const std::string& sessionToken) const;
            void invalidateSession(const std::string& sessionToken);
            void cleanupExpiredSessions();

        private:
            // ���� ���� ����ü
            struct SessionInfo {
                std::string userId;
                std::chrono::system_clock::time_point createdAt;
                std::chrono::system_clock::time_point expiresAt;
                std::string clientIP;
                bool isValid;

                SessionInfo() : isValid(true) {}
            };

            // ��й�ȣ ����
            std::string hashPassword(const std::string& password);
            bool verifyPassword(const std::string& password, const std::string& hash);
            std::string generateSalt();

            // ��ū ����
            std::string generateRandomToken(size_t length = 32);
            std::string generateJWTToken(const std::string& userId);
            bool validateJWTToken(const std::string& token, std::string& userId);

            // �����ͺ��̽� ���� (���߿� ����)
            void loadUsersFromDatabase();
            void saveUserToDatabase(const UserInfo& user);
            void saveSessionToDatabase(const std::string& token, const SessionInfo& session);

        private:
            // ����� ������ (�޸� ���, ���߿� DB�� ��ü)
            std::unordered_map<std::string, std::shared_ptr<UserInfo>> m_users;
            std::unordered_map<std::string, std::string> m_usernameToId;
            std::unordered_map<std::string, std::string> m_emailToId;
            mutable std::shared_mutex m_usersMutex;

            // ���� ����
            std::unordered_map<std::string, SessionInfo> m_sessions;
            mutable std::shared_mutex m_sessionsMutex;

            // ����
            std::chrono::hours m_sessionDuration{ 24 };
            std::string m_jwtSecret;
            size_t m_maxSessionsPerUser = 5;

            // ���
            std::atomic<int> m_totalUsers{ 0 };
            std::atomic<int> m_onlineUsers{ 0 };
        };

    } // namespace Server
} // namespace Blokus