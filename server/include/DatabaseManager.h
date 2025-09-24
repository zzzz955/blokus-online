#pragma once
// DatabaseManager.h - 간단하고 명확한 인터페이스

#include <string>
#include <vector>
#include <memory>
#include <optional>
#include <cstdint>
#include "ServerTypes.h"  // UserSettings 구조체 사용

namespace Blokus {
    namespace Server {

        //  사용자 정보 구조체
        struct UserAccount {
            uint32_t userId;
            std::string username;
            std::string displayName;  // 사용자 표시명
            std::string passwordHash;
            int totalGames;
            int wins;
            int losses;
            int draws;
            int level;
            int experiencePoints;
            int totalScore;      // 누적 점수
            int bestScore;       // 최고 점수
            bool isActive;

            // 계산된 필드
            double getWinRate() const;
            double getAverageScore() const;
        };

        //  시스템 통계 구조체
        struct DatabaseStats {
            int totalUsers;
            int activeUsers;
            int onlineUsers;
            int totalGames;
            int totalStats;
        };

        //  전방 선언 (구현부 숨김)
        class ConnectionPool;

        //  DatabaseManager 클래스
        class DatabaseManager {
        public:
            DatabaseManager();
            ~DatabaseManager();

            // 초기화/정리
            bool initialize();
            void shutdown();

            // ========================================
            // 사용자 관리
            // ========================================

            // 사용자 조회
            std::optional<UserAccount> getUserByUsername(const std::string& username);
            std::optional<UserAccount> getUserByDisplayName(const std::string& displayName);
            std::optional<UserAccount> getUserById(uint32_t userId);

            // 사용자 생성/수정
            bool createUser(const std::string& username, const std::string& passwordHash);
            bool updateUserLastLogin(const std::string& username);
            bool updateUserLastLogin(uint32_t userId);
            bool setUserActive(uint32_t userId, bool active);

            // 인증
            std::optional<UserAccount> authenticateUser(const std::string& username, const std::string& passwordHash);
            bool isUsernameAvailable(const std::string& username);

            // ========================================
            // 게임 통계
            // ========================================

            bool updateGameStats(uint32_t userId, bool won, bool draw = false, int score = 0);
            
            // 게임 결과 저장 (멀티플레이어용)
            bool saveGameResults(const std::vector<uint32_t>& playerIds, 
                               const std::vector<int>& scores, 
                               const std::vector<bool>& isWinner,
                               bool isDraw = false);
            
            // 경험치 및 레벨 시스템
            bool updatePlayerExperience(uint32_t userId, int expGained);
            bool checkAndProcessLevelUp(uint32_t userId);
            int getRequiredExpForLevel(int level) const;
            int calculateExperienceGain(bool won, int score, bool completedGame) const;

            // ========================================
            // 조회 기능
            // ========================================

            DatabaseStats getStats();
            std::vector<UserAccount> getRanking(const std::string& orderBy = "rating", int limit = 100, int offset = 0);
            std::vector<std::string> getOnlineUsers();

            // ========================================
            // 사용자 설정 관리
            // ========================================

            // 사용자 설정 조회 (없으면 기본값으로 생성)
            std::optional<UserSettings> getUserSettings(const std::string& userId);
            
            // 사용자 설정 업데이트 (없으면 생성)
            bool updateUserSettings(const std::string& userId, const UserSettings& settings);
            
            // 사용자 설정 삭제 (사용자 삭제 시 호출)
            bool deleteUserSettings(const std::string& userId);

            // ========================================
            // 친구 시스템 (추후 확장)
            // ========================================

            bool sendFriendRequest(uint32_t requesterId, uint32_t addresseeId);
            bool acceptFriendRequest(uint32_t requesterId, uint32_t addresseeId);
            std::vector<std::string> getFriends(uint32_t userId);

            // ========================================
            // 테스트/관리 기능
            // ========================================

            bool insertDummyData();

        private:
            // Pimpl 패턴으로 구현부 숨김
            std::unique_ptr<ConnectionPool> dbPool_;
            bool isInitialized_;
        };

    } // namespace Server
} // namespace Blokus