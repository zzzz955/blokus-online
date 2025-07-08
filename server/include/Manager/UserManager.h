#pragma once

#include "ServerTypes.h"
#include <string>
#include <unordered_map>
#include <memory>
#include <mutex>

namespace Blokus {
    namespace Server {

        struct UserInfo {
            std::string userId;
            std::string username;
            std::string email;
            int rating;
            int gamesPlayed;
            int gamesWon;
            std::chrono::system_clock::time_point lastLogin;
            bool isOnline;
        };

        class UserManager {
        public:
            UserManager();
            ~UserManager();

            // ����� ����
            bool authenticateUser(const std::string& username, const std::string& password);
            bool registerUser(const std::string& username, const std::string& password, const std::string& email);

            // ����� ���� ����
            std::shared_ptr<UserInfo> getUserInfo(const std::string& userId);
            void updateUserRating(const std::string& userId, int newRating);
            void recordGameResult(const std::string& userId, bool won);

            // �¶��� ���� ����
            void setUserOnline(const std::string& userId, bool online);
            std::vector<std::string> getOnlineUsers();

        private:
            std::unordered_map<std::string, std::shared_ptr<UserInfo>> users_;
            std::unordered_map<std::string, std::string> usernameToId_;
            mutable std::mutex usersMutex_;

            // �����ͺ��̽� ���� (���߿� ����)
            void loadUsersFromDatabase();
            void saveUserToDatabase(const UserInfo& user);
        };

    } // namespace Server
} // namespace Blokus