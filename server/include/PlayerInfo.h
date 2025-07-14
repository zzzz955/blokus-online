#pragma once

#include "Session.h"
#include "Types.h"
#include <memory>
#include <chrono>
#include <string>
#include <nlohmann/json.hpp>

namespace Blokus::Server {

    // 전방 선언
    class Session;
    using SessionPtr = std::shared_ptr<Session>;

    /**
     * @brief 게임 방 내 플레이어 정보 클래스
     *
     * Session 포인터를 통해 사용자 기본 정보를 동적으로 가져오고,
     * 게임 방 전용 상태 정보만 직접 관리합니다.
     */
    class PlayerInfo {
    private:
        // 핵심 참조점
        SessionPtr session_;

        // 게임 방 전용 상태 정보
        Common::PlayerColor color_;
        bool isHost_;
        bool isReady_;
        bool isAI_;
        int aiDifficulty_;
        int score_;
        int remainingBlocks_;
        std::chrono::steady_clock::time_point lastActivity_;

    public:
        // ========================================
        // 생성자 및 소멸자
        // ========================================
        explicit PlayerInfo(SessionPtr session);
        PlayerInfo(const std::string& userId, const std::string& username, SessionPtr session);
        ~PlayerInfo() = default;

        // 복사/이동 생성자 (Session 포인터 때문에 명시적 정의)
        PlayerInfo(const PlayerInfo& other);
        PlayerInfo& operator=(const PlayerInfo& other);
        PlayerInfo(PlayerInfo&& other) noexcept;
        PlayerInfo& operator=(PlayerInfo&& other) noexcept;

        // ========================================
        // 세션 기반 정보 접근 (인라인 - 간단한 getter들)
        // ========================================

        /// @brief 사용자 ID 반환 (세션에서 동적으로 가져옴)
        std::string getUserId() const {
            return session_ ? session_->getUserId() : "";
        }

        /// @brief 사용자명 반환 (세션에서 동적으로 가져옴)
        std::string getUsername() const {
            return session_ ? session_->getUsername() : "";
        }

        /// @brief 연결 상태 확인
        bool isConnected() const {
            return session_ && session_->isActive();
        }

        /// @brief 세션 상태 반환
        ConnectionState getConnectionState() const {
            return session_ ? session_->getState() : ConnectionState::Connected;
        }

        /// @brief 현재 방 ID 반환
        int getCurrentRoomId() const {
            return session_ ? session_->getCurrentRoomId() : -1;
        }

        /// @brief 플레이어 정보 유효성 검사
        bool isValid() const {
            return session_ && session_->isActive() && !getUserId().empty();
        }

        /// @brief 정리가 필요한 플레이어인지 확인 (연결 끊김)
        bool needsCleanup() const {
            return !session_ || !session_->isActive();
        }

        // ========================================
        // 게임 상태 정보 접근 (인라인 - 간단한 getter들)
        // ========================================

        Common::PlayerColor getColor() const { return color_; }
        bool isHost() const { return isHost_; }
        bool isReady() const { return isReady_; }
        bool isAI() const { return isAI_; }
        int getAIDifficulty() const { return aiDifficulty_; }
        int getScore() const { return score_; }
        int getRemainingBlocks() const { return remainingBlocks_; }

        std::chrono::steady_clock::time_point getLastActivity() const {
            return lastActivity_;
        }

        // ========================================
        // 게임 상태 설정 (복잡한 로직 - 소스 파일에서 구현)
        // ========================================

        /// @brief 플레이어 색상 설정 (유효성 검사 포함)
        bool setPlayerColor(Common::PlayerColor color);

        /// @brief 준비 상태 설정 (호스트 특별 처리 포함)
        bool setReady(bool ready);

        /// @brief 호스트 상태 설정 (권한 체크 포함)
        void setHost(bool host);

        /// @brief AI 플레이어로 설정
        void setAI(bool isAI, int difficulty = 2);

        /// @brief 점수 업데이트 (게임 로직 포함)
        void updateScore(int newScore);
        void addScore(int points);

        /// @brief 블록 수 관리
        void setRemainingBlocks(int blocks);
        void useBlocks(int count);

        /// @brief 활동 시간 업데이트 (세션과 동기화)
        void updateActivity();

        // ========================================
        // 게임 로직 관련 (복잡한 계산 - 소스 파일에서 구현)
        // ========================================

        /// @brief 최종 점수 계산 (보너스/페널티 포함)
        int calculateFinalScore() const;

        /// @brief 게임 승리 조건 확인
        bool hasWon() const;

        /// @brief 새 게임을 위한 상태 리셋
        void resetForNewGame();

        /// @brief 플레이어가 게임을 계속할 수 있는지 확인
        bool canContinueGame() const;

        // ========================================
        // 메시지 전송 (세션 위임)
        // ========================================

        /// @brief 플레이어에게 메시지 전송
        void sendMessage(const std::string& message) const {
            if (session_) {
                session_->sendMessage(message);
            }
        }

        /// @brief 세션 포인터 직접 접근 (필요시)
        SessionPtr getSession() const { return session_; }

        /// @brief 세션 설정 (재연결 등에서 사용)
        void setSession(SessionPtr session);

        // ========================================
        // 직렬화 (JSON 변환 - 복잡한 로직)
        // ========================================

        /// @brief JSON으로 변환 (네트워크 전송용)
        nlohmann::json toJson() const;

        /// @brief JSON에서 복원 (세션은 별도 설정 필요)
        static PlayerInfo fromJson(const nlohmann::json& json, SessionPtr session = nullptr);

        /// @brief 게임 상태만 JSON으로 변환 (간단한 동기화용)
        nlohmann::json gameStateToJson() const;

        // ========================================
        // 디버깅 및 로깅
        // ========================================

        /// @brief 디버깅용 문자열 반환
        std::string toString() const;

        /// @brief 상세 정보 로깅
        void logPlayerInfo() const;

        // ========================================
        // 연산자 오버로딩
        // ========================================

        /// @brief 사용자 ID 기반 비교
        bool operator==(const PlayerInfo& other) const {
            return getUserId() == other.getUserId();
        }

        bool operator!=(const PlayerInfo& other) const {
            return !(*this == other);
        }

        /// @brief 사용자명 기반 정렬용
        bool operator<(const PlayerInfo& other) const {
            return getUsername() < other.getUsername();
        }
    };

    // ========================================
    // 편의 함수들
    // ========================================

    /// @brief 연결된 플레이어들만 필터링
    std::vector<PlayerInfo> filterConnectedPlayers(const std::vector<PlayerInfo>& players);

    /// @brief 준비된 플레이어들만 필터링  
    std::vector<PlayerInfo> filterReadyPlayers(const std::vector<PlayerInfo>& players);

    /// @brief 호스트 플레이어 찾기
    PlayerInfo* findHostPlayer(std::vector<PlayerInfo>& players);
    const PlayerInfo* findHostPlayer(const std::vector<PlayerInfo>& players);

    /// @brief 사용자 ID로 플레이어 찾기
    PlayerInfo* findPlayerById(std::vector<PlayerInfo>& players, const std::string& userId);
    const PlayerInfo* findPlayerById(const std::vector<PlayerInfo>& players, const std::string& userId);

} // namespace Blokus::Server