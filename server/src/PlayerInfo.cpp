#include "PlayerInfo.h"
#include "Session.h"
#include <spdlog/spdlog.h>
#include <algorithm>
#include <sstream>

namespace Blokus::Server {

    // ========================================
    // 생성자 및 소멸자
    // ========================================

    PlayerInfo::PlayerInfo(SessionPtr session)
        : session_(session)
        , aiUserId_("")
        , aiUsername_("")
        , color_(Common::PlayerColor::None)
        , isHost_(false)
        , isReady_(false)
        , isAI_(false)
        , aiDifficulty_(2)
        , score_(0)
        , remainingBlocks_(Common::BLOCKS_PER_PLAYER)
        , lastActivity_(std::chrono::steady_clock::now())
    {
        if (!session_) {
            spdlog::warn("PlayerInfo created with null session");
        }
    }

    PlayerInfo::PlayerInfo(const std::string& userId, const std::string& username, SessionPtr session)
        : PlayerInfo(session)
    {
        // 세션이 있다면 검증, 없다면 경고만
        if (session_ && (session_->getUserId() != userId || session_->getUsername() != username)) {
            spdlog::warn("PlayerInfo: provided user info doesn't match session - User: {}/{}, Session: {}/{}",
                userId, username, session_->getUserId(), session_->getUsername());
        }
    }

    // 복사 생성자
    PlayerInfo::PlayerInfo(const PlayerInfo& other)
        : session_(other.session_)
        , aiUserId_(other.aiUserId_)
        , aiUsername_(other.aiUsername_)
        , color_(other.color_)
        , isHost_(other.isHost_)
        , isReady_(other.isReady_)
        , isAI_(other.isAI_)
        , aiDifficulty_(other.aiDifficulty_)
        , score_(other.score_)
        , remainingBlocks_(other.remainingBlocks_)
        , lastActivity_(other.lastActivity_)
    {
    }

    // 복사 대입 연산자
    PlayerInfo& PlayerInfo::operator=(const PlayerInfo& other) {
        if (this != &other) {
            session_ = other.session_;
            aiUserId_ = other.aiUserId_;
            aiUsername_ = other.aiUsername_;
            color_ = other.color_;
            isHost_ = other.isHost_;
            isReady_ = other.isReady_;
            isAI_ = other.isAI_;
            aiDifficulty_ = other.aiDifficulty_;
            score_ = other.score_;
            remainingBlocks_ = other.remainingBlocks_;
            lastActivity_ = other.lastActivity_;
        }
        return *this;
    }

    // 이동 생성자
    PlayerInfo::PlayerInfo(PlayerInfo&& other) noexcept
        : session_(std::move(other.session_))
        , aiUserId_(std::move(other.aiUserId_))
        , aiUsername_(std::move(other.aiUsername_))
        , color_(other.color_)
        , isHost_(other.isHost_)
        , isReady_(other.isReady_)
        , isAI_(other.isAI_)
        , aiDifficulty_(other.aiDifficulty_)
        , score_(other.score_)
        , remainingBlocks_(other.remainingBlocks_)
        , lastActivity_(other.lastActivity_)
    {
    }

    // 이동 대입 연산자
    PlayerInfo& PlayerInfo::operator=(PlayerInfo&& other) noexcept {
        if (this != &other) {
            session_ = std::move(other.session_);
            aiUserId_ = std::move(other.aiUserId_);
            aiUsername_ = std::move(other.aiUsername_);
            color_ = other.color_;
            isHost_ = other.isHost_;
            isReady_ = other.isReady_;
            isAI_ = other.isAI_;
            aiDifficulty_ = other.aiDifficulty_;
            score_ = other.score_;
            remainingBlocks_ = other.remainingBlocks_;
            lastActivity_ = other.lastActivity_;
        }
        return *this;
    }

    // ========================================
    // 게임 상태 설정
    // ========================================

    bool PlayerInfo::setPlayerColor(Common::PlayerColor color) {
        if (!isConnected() && !isAI()) {
            spdlog::warn("Cannot set color for disconnected player: {}", getUserId());
            return false;
        }

        if (color == Common::PlayerColor::None) {
            spdlog::warn("Invalid color assignment for player: {}", getUsername());
            return false;
        }

        color_ = color;
        updateActivity();

        spdlog::debug("Player '{}' color set to: {}", getUsername(), static_cast<int>(color));
        return true;
    }

    bool PlayerInfo::setReady(bool ready) {
        if (!isConnected() && !isAI()) {
            spdlog::warn("Cannot set ready state for disconnected player: {}", getUserId());
            return false;
        }

        // 호스트는 항상 준비 상태로 간주
        if (isHost_) {
            isReady_ = true;
            spdlog::debug("Host '{}' ready state is always true", getUsername());
        }
        else {
            isReady_ = ready;
            spdlog::debug("Player '{}' ready state set to: {}", getUsername(), ready);
        }

        updateActivity();
        return true;
    }

    void PlayerInfo::setHost(bool host) {
        isHost_ = host;

        // 호스트가 되면 자동으로 준비 상태
        if (host) {
            isReady_ = true;
            spdlog::info("Player '{}' is now the host", getUsername());
        }
        else {
            spdlog::debug("Player '{}' is no longer the host", getUsername());
        }

        updateActivity();
    }
    
    void PlayerInfo::setAIInfo(const std::string& userId, const std::string& username) {
        aiUserId_ = userId;
        aiUsername_ = username;
        spdlog::debug("AI 플레이어 정보 설정: ID='{}', 이름='{}'", userId, username);
        updateActivity();
    }

    void PlayerInfo::setAI(bool isAI, int difficulty) {
        isAI_ = isAI;

        if (isAI) {
            aiDifficulty_ = std::clamp(difficulty, 1, 5); // 1-5 범위로 제한
            isReady_ = true; // AI는 항상 준비됨
            spdlog::info("Player '{}' set to AI (difficulty: {})", getUsername(), aiDifficulty_);
        }
        else {
            aiDifficulty_ = 0;
            spdlog::debug("Player '{}' set to human player", getUsername());
        }

        updateActivity();
    }

    void PlayerInfo::updateScore(int newScore) {
        int oldScore = score_;
        score_ = std::max(0, newScore); // 음수 점수 방지

        spdlog::debug("Player '{}' score updated: {} -> {}", getUsername(), oldScore, score_);
        updateActivity();
    }

    void PlayerInfo::addScore(int points) {
        updateScore(score_ + points);
    }

    void PlayerInfo::setRemainingBlocks(int blocks) {
        remainingBlocks_ = std::max(0, blocks);
        spdlog::debug("Player '{}' remaining blocks: {}", getUsername(), remainingBlocks_);
        updateActivity();
    }

    void PlayerInfo::useBlocks(int count) {
        setRemainingBlocks(remainingBlocks_ - count);
    }

    void PlayerInfo::updateActivity() {
        lastActivity_ = std::chrono::steady_clock::now();

        // 세션의 활동 시간도 동기화
        if (session_) {
            session_->updateLastActivity();
        }
    }

    // ========================================
    // 게임 로직 관련
    // ========================================

    int PlayerInfo::calculateFinalScore() const {
        int finalScore = score_;

        // 보너스 점수 계산
        if (remainingBlocks_ == 0) {
            finalScore += 15; // 모든 블록 사용 보너스
            spdlog::debug("Player '{}' gets perfect game bonus (+15)", getUsername());
        }
        else if (remainingBlocks_ <= 3) {
            finalScore += 5; // 거의 완성 보너스
            spdlog::debug("Player '{}' gets near-perfect bonus (+5)", getUsername());
        }

        // 페널티 점수 계산 (남은 블록에 따른)
        int penalty = remainingBlocks_ * 1; // 블록당 1점씩 차감
        finalScore -= penalty;

        if (penalty > 0) {
            spdlog::debug("Player '{}' penalty for remaining blocks: -{}", getUsername(), penalty);
        }

        return std::max(0, finalScore);
    }

    bool PlayerInfo::hasWon() const {
        // 승리 조건: 모든 블록을 사용했거나 더 이상 놓을 수 없는 상태
        return remainingBlocks_ == 0;
    }

    void PlayerInfo::resetForNewGame() {
        color_ = Common::PlayerColor::None;
        isReady_ = isHost_; // 호스트만 자동으로 준비됨
        score_ = 0;
        remainingBlocks_ = Common::BLOCKS_PER_PLAYER;
        updateActivity();

        spdlog::debug("Player '{}' reset for new game", getUsername());
    }

    bool PlayerInfo::canContinueGame() const {
        // 연결되어 있고, 아직 블록이 남아있으면 계속 가능
        return isConnected() && remainingBlocks_ > 0;
    }

    void PlayerInfo::setSession(SessionPtr session) {
        if (session_) {
            spdlog::debug("Replacing session for player: {}", getUsername());
        }

        session_ = session;
        updateActivity();

        if (session_) {
            spdlog::info("Session set for player: {}", getUsername());
        }
        else {
            spdlog::warn("Session cleared for player: {}", getUserId());
        }
    }

    // ========================================
    // 직렬화
    // ========================================

    nlohmann::json PlayerInfo::toJson() const {
        nlohmann::json j;

        // 기본 정보 (세션에서 가져옴)
        j["userId"] = getUserId();
        j["username"] = getUsername();
        j["isConnected"] = isConnected();

        // 게임 상태
        j["color"] = static_cast<int>(color_);
        j["isHost"] = isHost_;
        j["isReady"] = isReady_;
        j["isAI"] = isAI_;
        j["aiDifficulty"] = aiDifficulty_;
        j["score"] = score_;
        j["remainingBlocks"] = remainingBlocks_;

        // 시간 정보
        auto timestamp = std::chrono::duration_cast<std::chrono::milliseconds>(
            lastActivity_.time_since_epoch()).count();
        j["lastActivity"] = timestamp;

        return j;
    }

    PlayerInfo PlayerInfo::fromJson(const nlohmann::json& json, SessionPtr session) {
        PlayerInfo player(session);

        try {
            // 게임 상태 복원
            if (json.contains("color")) {
                player.color_ = static_cast<Common::PlayerColor>(json["color"].get<int>());
            }
            if (json.contains("isHost")) {
                player.isHost_ = json["isHost"].get<bool>();
            }
            if (json.contains("isReady")) {
                player.isReady_ = json["isReady"].get<bool>();
            }
            if (json.contains("isAI")) {
                player.isAI_ = json["isAI"].get<bool>();
            }
            if (json.contains("aiDifficulty")) {
                player.aiDifficulty_ = json["aiDifficulty"].get<int>();
            }
            if (json.contains("score")) {
                player.score_ = json["score"].get<int>();
            }
            if (json.contains("remainingBlocks")) {
                player.remainingBlocks_ = json["remainingBlocks"].get<int>();
            }

            // 시간 정보 복원
            if (json.contains("lastActivity")) {
                auto timestamp = json["lastActivity"].get<long long>();
                player.lastActivity_ = std::chrono::steady_clock::time_point(
                    std::chrono::milliseconds(timestamp));
            }

        }
        catch (const std::exception& e) {
            spdlog::error("Error parsing PlayerInfo from JSON: {}", e.what());
        }

        return player;
    }

    nlohmann::json PlayerInfo::gameStateToJson() const {
        nlohmann::json j;
        j["color"] = static_cast<int>(color_);
        j["isHost"] = isHost_;
        j["isReady"] = isReady_;
        j["score"] = score_;
        j["remainingBlocks"] = remainingBlocks_;
        return j;
    }

    // ========================================
    // 디버깅 및 로깅
    // ========================================

    std::string PlayerInfo::toString() const {
        std::ostringstream oss;
        oss << "PlayerInfo{"
            << "userId='" << getUserId() << "'"
            << ", username='" << getUsername() << "'"
            << ", connected=" << (isConnected() ? "true" : "false")
            << ", color=" << static_cast<int>(color_)
            << ", host=" << (isHost_ ? "true" : "false")
            << ", ready=" << (isReady_ ? "true" : "false")
            << ", AI=" << (isAI_ ? "true" : "false")
            << ", score=" << score_
            << ", blocks=" << remainingBlocks_
            << "}";
        return oss.str();
    }

    void PlayerInfo::logPlayerInfo() const {
        spdlog::info("Player Info: {}", toString());
    }

    // ========================================
    // 편의 함수들
    // ========================================

    std::vector<PlayerInfo> filterConnectedPlayers(const std::vector<PlayerInfo>& players) {
        std::vector<PlayerInfo> connected;
        std::copy_if(players.begin(), players.end(), std::back_inserter(connected),
            [](const PlayerInfo& player) {
                return player.isConnected();
            });
        return connected;
    }

    std::vector<PlayerInfo> filterReadyPlayers(const std::vector<PlayerInfo>& players) {
        std::vector<PlayerInfo> ready;
        std::copy_if(players.begin(), players.end(), std::back_inserter(ready),
            [](const PlayerInfo& player) {
                return player.isReady() && player.isConnected();
            });
        return ready;
    }

    PlayerInfo* findHostPlayer(std::vector<PlayerInfo>& players) {
        auto it = std::find_if(players.begin(), players.end(),
            [](const PlayerInfo& player) {
                return player.isHost();
            });
        return (it != players.end()) ? &(*it) : nullptr;
    }

    const PlayerInfo* findHostPlayer(const std::vector<PlayerInfo>& players) {
        auto it = std::find_if(players.begin(), players.end(),
            [](const PlayerInfo& player) {
                return player.isHost();
            });
        return (it != players.end()) ? &(*it) : nullptr;
    }

    PlayerInfo* findPlayerById(std::vector<PlayerInfo>& players, const std::string& userId) {
        auto it = std::find_if(players.begin(), players.end(),
            [&userId](const PlayerInfo& player) {
                return player.getUserId() == userId;
            });
        return (it != players.end()) ? &(*it) : nullptr;
    }

    const PlayerInfo* findPlayerById(const std::vector<PlayerInfo>& players, const std::string& userId) {
        auto it = std::find_if(players.begin(), players.end(),
            [&userId](const PlayerInfo& player) {
                return player.getUserId() == userId;
            });
        return (it != players.end()) ? &(*it) : nullptr;
    }

} // namespace Blokus::Server