#pragma once

#include <memory>
#include <unordered_map>
#include <unordered_set>
#include <string>
#include <vector>
#include <mutex>
#include <atomic>

#include "common/Types.h"

namespace Blokus {
    namespace Server {

        // �¶��� ����� ����
        struct OnlineUser {
            uint32_t sessionId;                                    // ���� ID
            uint32_t userId;                                       // ����� ID  
            std::string username;                                  // ����ڸ�
            Common::UserInfo userInfo;                            // ����� ��� ����
            uint32_t currentRoomId{ 0 };                          // ���� �� ID
            std::chrono::steady_clock::time_point loginTime;      // �α��� �ð�
            std::chrono::steady_clock::time_point lastActivity;   // ������ Ȱ�� �ð�

            OnlineUser() = default;
            OnlineUser(uint32_t sid, uint32_t uid, const std::string& name)
                : sessionId(sid), userId(uid), username(name)
                , loginTime(std::chrono::steady_clock::now())
                , lastActivity(std::chrono::steady_clock::now()) {
                userInfo.username = name;
            }
        };

        // ����� ������ ����ϴ� Ŭ����
        class UserManager {
        public:
            UserManager();
            ~UserManager();

            // ����� �α���/�α׾ƿ�
            bool loginUser(uint32_t sessionId, const std::string& username, const std::string& password);
            bool logoutUser(uint32_t sessionId);
            bool registerUser(const std::string& username, const std::string& password, const std::string& email);

            // �¶��� ����� ����
            bool addOnlineUser(uint32_t sessionId, uint32_t userId, const std::string& username);
            bool removeOnlineUser(uint32_t sessionId);
            std::shared_ptr<OnlineUser> findOnlineUser(uint32_t sessionId);
            std::shared_ptr<OnlineUser> findOnlineUserByName(const std::string& username);

            // ����� ���� ����
            void updateUserActivity(uint32_t sessionId);
            void setUserRoom(uint32_t sessionId, uint32_t roomId);
            std::vector<uint32_t> getUsersInRoom(uint32_t roomId);

            // ����� ��� ��ȸ
            std::vector<Common::UserInfo> getOnlineUserList();
            std::vector<Common::UserInfo> getUserRanking(size_t limit = 10);
            size_t getOnlineUserCount() const;

            // ����� �˻�
            std::vector<std::shared_ptr<OnlineUser>> findUsersByPattern(const std::string& pattern);
            bool isUserOnline(const std::string& username);

            // ��� ������Ʈ
            bool updateUserStats(uint32_t userId, bool isWin, int score);
            bool updateUserGameResult(const std::string& username, bool isWin, int score);

            // ������ ���
            bool kickUser(const std::string& username, const std::string& reason = "");
            bool banUser(const std::string& username, int durationMinutes = 0); // 0 = ����
            bool isUserBanned(const std::string& username);

            // ���� �۾�
            void cleanupInactiveUsers();                           // ��Ȱ�� ����� ����

        private:
            // ���� ���� ����
            bool validateCredentials(const std::string& username, const std::string& password);
            bool validateUsername(const std::string& username);
            bool validatePassword(const std::string& password);
            bool validateEmail(const std::string& email);
            std::string hashPassword(const std::string& password);

            // �����ͺ��̽� ���� (���� ����)
            bool loadUserFromDB(const std::string& username, Common::UserInfo& userInfo);
            bool saveUserToDB(const Common::UserInfo& userInfo);
            bool createUserInDB(const std::string& username, const std::string& passwordHash, const std::string& email);

            // ���� ���� �Լ���
            uint32_t generateUserId();                            // �� ����� ID ����
            void notifyUserListChanged();                         // ����� ��� ���� �˸�

        private:
            // �¶��� ����� ����
            std::unordered_map<uint32_t, std::shared_ptr<OnlineUser>> m_onlineUsers; // ���� ID -> �¶��� �����
            std::unordered_map<std::string, uint32_t> m_usernameToSession; // ����ڸ� -> ���� ID
            mutable std::mutex m_usersMutex;                      // ����� �� ��ȣ�� ���ؽ�

            // ����� ��� ĳ�� (DB���� �ε�� ����)
            std::unordered_map<std::string, Common::UserInfo> m_userStatsCache;
            mutable std::mutex m_statsMutex;                      // ��� ĳ�� ��ȣ�� ���ؽ�

            // �� ����
            std::unordered_map<std::string, std::chrono::steady_clock::time_point> m_bannedUsers;
            mutable std::mutex m_banMutex;                        // �� ����Ʈ ��ȣ�� ���ؽ�

            // ID ������
            std::atomic<uint32_t> m_nextUserId{ 10001 };          // ���� ����� ID (10001���� ����)

            // ��� ����
            std::atomic<uint64_t> m_totalLogins{ 0 };             // �� �α��� ��
            std::atomic<uint64_t> m_totalRegistrations{ 0 };      // �� ȸ������ ��
        };

    } // namespace Server
} // namespace Blokus