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

        // 온라인 사용자 정보
        struct OnlineUser {
            uint32_t sessionId;                                    // 세션 ID
            uint32_t userId;                                       // 사용자 ID  
            std::string username;                                  // 사용자명
            Common::UserInfo userInfo;                            // 사용자 통계 정보
            uint32_t currentRoomId{ 0 };                          // 현재 방 ID
            std::chrono::steady_clock::time_point loginTime;      // 로그인 시간
            std::chrono::steady_clock::time_point lastActivity;   // 마지막 활동 시간

            OnlineUser() = default;
            OnlineUser(uint32_t sid, uint32_t uid, const std::string& name)
                : sessionId(sid), userId(uid), username(name)
                , loginTime(std::chrono::steady_clock::now())
                , lastActivity(std::chrono::steady_clock::now()) {
                userInfo.username = name;
            }
        };

        // 사용자 관리를 담당하는 클래스
        class UserManager {
        public:
            UserManager();
            ~UserManager();

            // 사용자 로그인/로그아웃
            bool loginUser(uint32_t sessionId, const std::string& username, const std::string& password);
            bool logoutUser(uint32_t sessionId);
            bool registerUser(const std::string& username, const std::string& password, const std::string& email);

            // 온라인 사용자 관리
            bool addOnlineUser(uint32_t sessionId, uint32_t userId, const std::string& username);
            bool removeOnlineUser(uint32_t sessionId);
            std::shared_ptr<OnlineUser> findOnlineUser(uint32_t sessionId);
            std::shared_ptr<OnlineUser> findOnlineUserByName(const std::string& username);

            // 사용자 상태 관리
            void updateUserActivity(uint32_t sessionId);
            void setUserRoom(uint32_t sessionId, uint32_t roomId);
            std::vector<uint32_t> getUsersInRoom(uint32_t roomId);

            // 사용자 목록 조회
            std::vector<Common::UserInfo> getOnlineUserList();
            std::vector<Common::UserInfo> getUserRanking(size_t limit = 10);
            size_t getOnlineUserCount() const;

            // 사용자 검색
            std::vector<std::shared_ptr<OnlineUser>> findUsersByPattern(const std::string& pattern);
            bool isUserOnline(const std::string& username);

            // 통계 업데이트
            bool updateUserStats(uint32_t userId, bool isWin, int score);
            bool updateUserGameResult(const std::string& username, bool isWin, int score);

            // 관리자 기능
            bool kickUser(const std::string& username, const std::string& reason = "");
            bool banUser(const std::string& username, int durationMinutes = 0); // 0 = 영구
            bool isUserBanned(const std::string& username);

            // 정리 작업
            void cleanupInactiveUsers();                           // 비활성 사용자 정리

        private:
            // 인증 관련 헬퍼
            bool validateCredentials(const std::string& username, const std::string& password);
            bool validateUsername(const std::string& username);
            bool validatePassword(const std::string& password);
            bool validateEmail(const std::string& email);
            std::string hashPassword(const std::string& password);

            // 데이터베이스 연동 (추후 구현)
            bool loadUserFromDB(const std::string& username, Common::UserInfo& userInfo);
            bool saveUserToDB(const Common::UserInfo& userInfo);
            bool createUserInDB(const std::string& username, const std::string& passwordHash, const std::string& email);

            // 내부 헬퍼 함수들
            uint32_t generateUserId();                            // 새 사용자 ID 생성
            void notifyUserListChanged();                         // 사용자 목록 변경 알림

        private:
            // 온라인 사용자 관리
            std::unordered_map<uint32_t, std::shared_ptr<OnlineUser>> m_onlineUsers; // 세션 ID -> 온라인 사용자
            std::unordered_map<std::string, uint32_t> m_usernameToSession; // 사용자명 -> 세션 ID
            mutable std::mutex m_usersMutex;                      // 사용자 맵 보호용 뮤텍스

            // 사용자 통계 캐시 (DB에서 로드된 정보)
            std::unordered_map<std::string, Common::UserInfo> m_userStatsCache;
            mutable std::mutex m_statsMutex;                      // 통계 캐시 보호용 뮤텍스

            // 밴 관리
            std::unordered_map<std::string, std::chrono::steady_clock::time_point> m_bannedUsers;
            mutable std::mutex m_banMutex;                        // 밴 리스트 보호용 뮤텍스

            // ID 생성기
            std::atomic<uint32_t> m_nextUserId{ 10001 };          // 다음 사용자 ID (10001부터 시작)

            // 통계 정보
            std::atomic<uint64_t> m_totalLogins{ 0 };             // 총 로그인 수
            std::atomic<uint64_t> m_totalRegistrations{ 0 };      // 총 회원가입 수
        };

    } // namespace Server
} // namespace Blokus