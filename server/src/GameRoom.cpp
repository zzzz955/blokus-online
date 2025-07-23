#include "GameRoom.h"
#include "Session.h"
#include "PlayerInfo.h"  // 🔥 새로 추가
#include <spdlog/spdlog.h>
#include <algorithm>
#include <sstream>

namespace Blokus {
    namespace Server {

        // ========================================
        // 생성자/소멸자
        // ========================================

        GameRoom::GameRoom(int roomId, const std::string& roomName, const std::string& hostId)
            : m_roomId(roomId)
            , m_roomName(roomName)
            , m_hostId(hostId)
            , m_state(RoomState::Waiting)
            , m_gameLogic(std::make_unique<Common::GameLogic>())
            , m_gameStateManager(std::make_unique<Common::GameStateManager>())
            , m_createdTime(std::chrono::steady_clock::now())
            , m_gameStartTime{}
            , m_lastActivity(std::chrono::steady_clock::now())
            , m_isPrivate(false)
            , m_password("")
            , m_maxPlayers(Common::MAX_PLAYERS)
        {
            m_players.reserve(Common::MAX_PLAYERS);
            spdlog::info("🏠 방 생성: ID={}, Name='{}', Host={}", m_roomId, m_roomName, m_hostId);
        }

        GameRoom::~GameRoom() {
            // 모든 플레이어에게 방 해체 알림
            broadcastMessage("ROOM_DISBANDED");
            spdlog::info("🏠 방 소멸: ID={}, Name='{}'", m_roomId, m_roomName);
        }

        // ========================================
        // 플레이어 관리 (PlayerInfo 클래스 사용)
        // ========================================

        bool GameRoom::addPlayer(SessionPtr session, const std::string& userId, const std::string& username) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            // 1. 방이 가득 찬지 확인
            if (m_players.size() >= m_maxPlayers) {
                spdlog::warn("❌ 방 {} 플레이어 추가 실패: 방이 가득참 ({}/{})",
                    m_roomId, m_players.size(), m_maxPlayers);
                return false;
            }

            // 2. 게임 중인 경우 참여 제한
            if (m_state == RoomState::Playing) {
                spdlog::warn("❌ 방 {} 플레이어 추가 실패: 게임 진행 중 (상태: {})",
                    m_roomId, static_cast<int>(m_state));
                return false;
            }

            // 3. 이미 참여한 플레이어인지 확인
            auto* existingPlayer = findPlayerById(m_players, userId);
            if (existingPlayer != nullptr) {
                spdlog::warn("❌ 방 {} 플레이어 추가 실패: 이미 참여한 플레이어 '{}'", m_roomId, userId);
                return false;
            }

            // 4. 🔥 새 PlayerInfo 객체 생성
            PlayerInfo newPlayer(session);
            // 호스트 설정 (첫 번째 플레이어가 호스트)
            if (m_players.empty() || userId == m_hostId) {
                newPlayer.setHost(true);
                m_hostId = userId;
            }

            // 색상 자동 배정
            assignPlayerColor(newPlayer);

            m_players.push_back(std::move(newPlayer));
            updateActivity();

            spdlog::info("✅ 방 {} 플레이어 추가: '{}' (현재: {}/{})",
                m_roomId, username, m_players.size(), m_maxPlayers);

            return true;
        }

        bool GameRoom::removePlayer(const std::string& userId) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            auto it = std::find_if(m_players.begin(), m_players.end(),
                [&userId](const PlayerInfo& player) {
                    return player.getUserId() == userId;
                });

            if (it == m_players.end()) {
                spdlog::warn("❌ 방 {} 플레이어 제거 실패: 플레이어 '{}' 없음", m_roomId, userId);
                return false;
            }

            std::string username = it->getUsername();
            bool wasHost = it->isHost();

            m_players.erase(it);
            updateActivity();

            spdlog::info("✅ 방 {} 플레이어 제거: '{}' (남은: {}명)", m_roomId, username, m_players.size());

            // 호스트가 나간 경우 새 호스트 선정
            if (wasHost && !m_players.empty()) {
                autoSelectNewHost();
            }

            // 방이 비었다면 게임 종료
            if (m_players.empty()) {
                spdlog::info("🏠 방 {} 모든 플레이어 퇴장으로 인한 게임 종료", m_roomId);
                m_state = RoomState::Disbanded;
                return true;
            }

            // 게임 중이었다면 게임 종료
            if (m_state == RoomState::Playing) {
                spdlog::info("🎮 방 {} 플레이어 이탈로 인한 게임 강제 종료", m_roomId);
                endGame();
            }

            return true;
        }

        bool GameRoom::hasPlayer(const std::string& userId) const {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return findPlayerById(m_players, userId) != nullptr;
        }

        PlayerInfo* GameRoom::getPlayer(const std::string& userId) {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return findPlayerById(m_players, userId);
        }

        const PlayerInfo* GameRoom::getPlayer(const std::string& userId) const {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return findPlayerById(m_players, userId);
        }

        // ========================================
        // 플레이어 상태 관리 (PlayerInfo 위임)
        // ========================================

        bool GameRoom::setPlayerReady(const std::string& userId, bool ready) {
            std::string username;
            bool actualReadyState;
            bool success = false;
            
            {
                std::lock_guard<std::mutex> lock(m_playersMutex);

                auto* player = findPlayerById(m_players, userId);
                if (!player) {
                    return false;
                }

                success = player->setReady(ready);
                if (success) {
                    updateActivity();
                    username = player->getUsername();
                    actualReadyState = player->isReady();
                }
            }

            // 락 해제 후 브로드캐스트 (데드락 방지)
            if (success) {
                broadcastPlayerReady(username, actualReadyState);
            }

            return success;
        }

        bool GameRoom::isPlayerReady(const std::string& userId) const {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            const auto* player = findPlayerById(m_players, userId);
            return player ? player->isReady() : false;
        }

        bool GameRoom::setPlayerColor(const std::string& userId, Common::PlayerColor color) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            // 색상이 이미 사용 중인지 확인
            if (isColorTaken(color)) {
                return false;
            }

            auto* player = findPlayerById(m_players, userId);
            if (!player) {
                return false;
            }

            bool success = player->setPlayerColor(color);
            if (success) {
                updateActivity();
            }

            return success;
        }

        // ========================================
        // 호스트 관리 (PlayerInfo 위임)
        // ========================================

        bool GameRoom::isHost(const std::string& userId) const {
            const auto* player = getPlayer(userId);
            return player ? player->isHost() : false;
        }

        bool GameRoom::transferHost(const std::string& newHostId) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            // 새 호스트가 방에 있는지 확인
            auto* newHost = findPlayerById(m_players, newHostId);
            if (!newHost) {
                return false;
            }

            // 기존 호스트 권한 제거
            auto* oldHost = findPlayerById(m_players, m_hostId);
            if (oldHost) {
                oldHost->setHost(false);
            }

            // 새 호스트 설정
            newHost->setHost(true);
            m_hostId = newHostId;
            updateActivity();

            spdlog::info("👑 방 {} 호스트 변경: '{}'", m_roomId, newHost->getUsername());
            return true;
        }

        void GameRoom::autoSelectNewHost() {
            if (m_players.empty()) {
                return;
            }

            // 첫 번째 플레이어를 호스트로 선정
            PlayerInfo& newHost = m_players.front();
            newHost.setHost(true);
            m_hostId = newHost.getUserId();
            spdlog::info("👑 방 {} 자동 호스트 선정: '{}'", m_roomId, newHost.getUsername());
        }

        // ========================================
        // 방 상태 정보
        // ========================================
        
        std::string GameRoom::getHostName() const {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            const auto* host = findPlayerById(m_players, m_hostId);
            return host ? host->getUsername() : "Unknown";
        }

        size_t GameRoom::getPlayerCount() const {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return m_players.size();
        }
        
        
        bool GameRoom::isFull() const {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return m_players.size() >= m_maxPlayers;
        }

        bool GameRoom::isEmpty() const {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return m_players.empty();
        }

        bool GameRoom::canStartGame() const {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            // 최소 인원 확인
            if (m_players.size() < Common::MIN_PLAYERS_TO_START) {
                return false;
            }

            // 대기 상태가 아니면 시작 불가
            if (m_state != RoomState::Waiting) {
                return false;
            }

            // 모든 플레이어가 준비되었는지 확인
            return validateAllPlayersReady();
        }

        // ========================================
        // 게임 제어
        // ========================================

        bool GameRoom::startGame() {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            if (!validateGameCanStart()) {
                return false;
            }

            // 게임 상태 변경
            m_state = RoomState::Playing;
            m_gameStartTime = std::chrono::steady_clock::now();
            updateActivity();

            // 게임 로직 초기화
            m_gameLogic->clearBoard();
            assignColorsAutomatically();

            // 턴 순서 설정 (플레이어 색상 기준)
            std::vector<Common::PlayerColor> turnOrder;
            for (const auto& player : m_players) {
                if (player.getColor() != Common::PlayerColor::None) {
                    turnOrder.push_back(player.getColor());
                }
            }
            
            // 게임 상태 관리자 시작
            m_gameStateManager->startNewGame(turnOrder);

            spdlog::info("🎮 방 {} 게임 시작: {} 플레이어, 턴 순서 설정됨", m_roomId, m_players.size());
            return true;
        }

        bool GameRoom::endGame() {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            if (m_state != RoomState::Playing) {
                return false;
            }

            m_state = RoomState::Waiting;
            updateActivity();

            // 모든 플레이어 게임 상태 리셋
            for (auto& player : m_players) {
                player.resetForNewGame();
            }

            spdlog::info("🎮 방 {} 게임 종료", m_roomId);
            return true;
        }

        void GameRoom::resetGame() {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            m_gameLogic->clearBoard();
            m_gameStateManager->resetGame();
            m_state = RoomState::Waiting;

            for (auto& player : m_players) {
                player.resetForNewGame();
            }

            updateActivity();

            spdlog::info("🔄 방 {} 게임 리셋", m_roomId);
            broadcastMessage("GAME_RESET");
        }

        // ========================================
        // 메시지 전송 (PlayerInfo 위임)
        // ========================================

        void GameRoom::broadcastMessage(const std::string& message, const std::string& excludeUserId) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            for (const auto& player : m_players) {
                if (player.getUserId() != excludeUserId && player.isConnected()) {
                    try {
                        player.sendMessage(message);
                    }
                    catch (const std::exception& e) {
                        spdlog::error("❌ 방 {} 메시지 전송 실패 (플레이어: '{}'): {}",
                            m_roomId, player.getUsername(), e.what());
                    }
                }
            }
        }

        void GameRoom::sendToPlayer(const std::string& userId, const std::string& message) {
            auto* player = getPlayer(userId);
            if (player && player->isConnected()) {
                try {
                    player->sendMessage(message);
                }
                catch (const std::exception& e) {
                    spdlog::error("❌ 방 {} 개별 메시지 전송 실패 (플레이어: '{}'): {}",
                        m_roomId, player->getUsername(), e.what());
                }
            }
        }

        void GameRoom::sendToHost(const std::string& message) {
            sendToPlayer(m_hostId, message);
        }

        // ========================================
        // 방 정보 생성
        // ========================================

        Common::RoomInfo GameRoom::getRoomInfo() const {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            Common::RoomInfo info;
            info.roomId = m_roomId;
            info.roomName = m_roomName;
            info.currentPlayers = static_cast<int>(m_players.size());
            info.maxPlayers = m_maxPlayers;
            info.isPrivate = m_isPrivate;
            info.isPlaying = (m_state == RoomState::Playing);
            info.gameMode = "클래식";

            // 호스트 이름 찾기
            const auto* host = findHostPlayer(m_players);
            info.hostName = host ? host->getUsername() : "알 수 없음";

            return info;
        }

        std::vector<PlayerInfo> GameRoom::getPlayerList() const {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return m_players; // 복사본 반환
        }

        // ========================================
        // 유틸리티
        // ========================================

        void GameRoom::updateActivity() {
            m_lastActivity = std::chrono::steady_clock::now();
        }

        bool GameRoom::isInactive(std::chrono::minutes threshold) const {
            auto now = std::chrono::steady_clock::now();
            auto elapsed = std::chrono::duration_cast<std::chrono::minutes>(now - m_lastActivity);
            return elapsed >= threshold;
        }

        // ========================================
        // 색상 관리
        // ========================================

        Common::PlayerColor GameRoom::getAvailableColor() const {
            return getNextAvailableColor();
        }

        bool GameRoom::isColorTaken(Common::PlayerColor color) const {
            if (color == Common::PlayerColor::None) {
                return false;
            }

            return std::any_of(m_players.begin(), m_players.end(),
                [color](const PlayerInfo& player) {
                    return player.getColor() == color;
                });
        }

        void GameRoom::assignColorsAutomatically() {
            const std::array<Common::PlayerColor, 4> colors = {
                Common::PlayerColor::Blue,
                Common::PlayerColor::Yellow,
                Common::PlayerColor::Red,
                Common::PlayerColor::Green
            };

            for (size_t i = 0; i < m_players.size() && i < colors.size(); ++i) {
                m_players[i].setPlayerColor(colors[i]);
            }
        }

        // ========================================
        // 브로드캐스트 함수들
        // ========================================

        void GameRoom::broadcastPlayerJoined(const std::string& username) {
            // 구조화된 메시지와 시스템 메시지 모두 전송
            broadcastMessage("PLAYER_JOINED:" + username);
            
            std::ostringstream oss;
            oss << username << "님이 입장하셨습니다. 현재 인원 : " << getPlayerCount() << "명";
            broadcastMessage("SYSTEM:" + oss.str());
        }

        void GameRoom::broadcastPlayerLeft(const std::string& username) {
            // 구조화된 메시지와 시스템 메시지 모두 전송
            broadcastMessage("PLAYER_LEFT:" + username);
            
            std::ostringstream oss;
            oss << username << "님이 퇴장하셨습니다. 현재 인원 : " << getPlayerCount() << "명";
            broadcastMessage("SYSTEM:" + oss.str());
        }

        void GameRoom::broadcastPlayerReady(const std::string& username, bool ready) {
            std::ostringstream oss;
            oss << "PLAYER_READY:" << username << ":" << (ready ? "1" : "0");
            broadcastMessage(oss.str());
        }

        void GameRoom::broadcastHostChanged(const std::string& newHostName) {
            // 구조화된 메시지와 시스템 메시지 모두 전송
            broadcastMessage("HOST_CHANGED:" + newHostName);
            
            std::ostringstream oss;
            oss << newHostName << "님이 방장이 되셨습니다";
            broadcastMessage("SYSTEM:" + oss.str());
        }

        void GameRoom::broadcastGameStart() {
            // 구조화된 메시지 전송
            broadcastMessage("GAME_STARTED");
            
            std::ostringstream oss;
            oss << "게임이 시작되었습니다. 현재 인원 : " << getPlayerCount() << "명";
            broadcastMessage("SYSTEM:" + oss.str());

            // 플레이어 정보도 함께 전송
            for (const auto& player : m_players) {
                oss << ":" << player.getUsername() << "," << static_cast<int>(player.getColor());
            }

            broadcastMessage(oss.str());
        }

        void GameRoom::broadcastGameEnd() {
            // 구조화된 메시지와 시스템 메시지 모두 전송
            broadcastMessage("GAME_ENDED");
            broadcastMessage("SYSTEM:게임이 종료되었습니다.");
        }

        void GameRoom::broadcastGameState() {
            std::ostringstream oss;
            oss << "게임 종료";
            broadcastMessage(oss.str());
        }

        // ========================================
        // 내부 유틸리티 함수들
        // ========================================

        void GameRoom::assignPlayerColor(PlayerInfo& player) {
            Common::PlayerColor color = getNextAvailableColor();
            player.setPlayerColor(color);
        }

        Common::PlayerColor GameRoom::getNextAvailableColor() const {
            const std::array<Common::PlayerColor, 4> colors = {
                Common::PlayerColor::Blue,
                Common::PlayerColor::Yellow,
                Common::PlayerColor::Red,
                Common::PlayerColor::Green
            };

            for (const auto& color : colors) {
                if (!isColorTaken(color)) {
                    return color;
                }
            }

            return Common::PlayerColor::None;
        }

        // 검증 함수들
        bool GameRoom::validatePlayerCount() const {
            return m_players.size() >= Common::MIN_PLAYERS_TO_START &&
                m_players.size() <= m_maxPlayers;
        }

        bool GameRoom::validateAllPlayersReady() const {
            for (const auto& player : m_players) {
                // 호스트가 아닌 플레이어는 반드시 준비되어야 함
                if (!player.isHost() && !player.isReady()) {
                    return false;
                }
            }
            return true;
        }

        bool GameRoom::validateGameCanStart() const {
            // 상태 확인
            if (m_state != RoomState::Waiting) {
                spdlog::warn("❌ 방 {} 게임 시작 실패: 잘못된 상태 ({})",
                    m_roomId, static_cast<int>(m_state));
                return false;
            }

            // 플레이어 수 확인
            if (!validatePlayerCount()) {
                spdlog::warn("❌ 방 {} 게임 시작 실패: 플레이어 수 부족 ({}/{})",
                    m_roomId, m_players.size(), Common::MIN_PLAYERS_TO_START);
                return false;
            }

            // 준비 상태 확인
            if (!validateAllPlayersReady()) {
                spdlog::warn("❌ 방 {} 게임 시작 실패: 일부 플레이어 미준비", m_roomId);
                return false;
            }

            return true;
        }

        // 정리 함수들
        void GameRoom::cleanupDisconnectedPlayers() {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            auto it = m_players.begin();
            while (it != m_players.end()) {
                if (it->needsCleanup()) {
                    spdlog::info("🧹 방 {} 연결 끊어진 플레이어 정리: '{}'",
                        m_roomId, it->getUsername());
                    broadcastPlayerLeft(it->getUsername());
                    it = m_players.erase(it);
                }
                else {
                    ++it;
                }
            }

            // 모든 플레이어가 연결 끊어진 경우
            if (m_players.empty()) {
                m_state = RoomState::Disbanded;
            }
        }

        void GameRoom::resetPlayerStates() {
            for (auto& player : m_players) {
                player.resetForNewGame();
            }
        }

        // ========================================
        // 턴 관리 메서드
        // ========================================

        bool GameRoom::handleBlockPlacement(const std::string& userId, const Common::BlockPlacement& placement) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            // 게임이 진행 중인지 확인
            if (m_state != RoomState::Playing) {
                spdlog::warn("❌ 블록 배치 실패: 게임이 진행 중이 아님 (방 {})", m_roomId);
                return false;
            }

            // 플레이어 찾기
            auto* player = getPlayer(userId);
            if (!player) {
                spdlog::warn("❌ 블록 배치 실패: 플레이어를 찾을 수 없음 (방 {}, 사용자 {})", m_roomId, userId);
                return false;
            }

            // 플레이어 턴 확인
            if (!isPlayerTurn(userId)) {
                spdlog::warn("❌ 블록 배치 실패: 플레이어 턴이 아님 (방 {}, 사용자 {})", m_roomId, userId);
                return false;
            }

            // 플레이어 색상과 배치 색상 일치 확인
            if (player->getColor() != placement.player) {
                spdlog::warn("❌ 블록 배치 실패: 색상 불일치 (방 {}, 사용자 {}, 플레이어 색상: {}, 배치 색상: {})", 
                    m_roomId, userId, static_cast<int>(player->getColor()), static_cast<int>(placement.player));
                return false;
            }

            // 블록 배치 시도
            if (!m_gameLogic->canPlaceBlock(placement)) {
                spdlog::warn("❌ 블록 배치 실패: 게임 규칙 위반 (방 {}, 사용자 {})", m_roomId, userId);
                return false;
            }

            if (!m_gameLogic->placeBlock(placement)) {
                spdlog::warn("❌ 블록 배치 실패: 블록 배치 불가 (방 {}, 사용자 {})", m_roomId, userId);
                return false;
            }

            // 성공적으로 배치됨
            spdlog::info("✅ 블록 배치 성공 (방 {}, 사용자 {}, 블록 타입: {})", 
                m_roomId, userId, static_cast<int>(placement.type));

            // 다음 턴으로 전환
            m_gameStateManager->nextTurn();

            // 게임 종료 조건 확인
            if (m_gameStateManager->getGameState() == Common::GameState::Finished) {
                endGame();
            }

            return true;
        }

        bool GameRoom::skipPlayerTurn(const std::string& userId) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            // 게임이 진행 중인지 확인
            if (m_state != RoomState::Playing) {
                return false;
            }

            // 플레이어 턴 확인
            if (!isPlayerTurn(userId)) {
                return false;
            }

            // 턴 스킵
            spdlog::info("⏭️ 턴 스킵 (방 {}, 사용자 {})", m_roomId, userId);
            m_gameStateManager->skipTurn();

            // 게임 종료 조건 확인
            if (m_gameStateManager->getGameState() == Common::GameState::Finished) {
                endGame();
            }

            return true;
        }

        bool GameRoom::isPlayerTurn(const std::string& userId) const {
            if (m_state != RoomState::Playing) {
                return false;
            }

            const auto* player = getPlayer(userId);
            if (!player) {
                return false;
            }

            return player->getColor() == m_gameStateManager->getCurrentPlayer();
        }

        Common::PlayerColor GameRoom::getCurrentPlayer() const {
            return m_gameStateManager->getCurrentPlayer();
        }

        std::vector<Common::PlayerColor> GameRoom::getTurnOrder() const {
            return m_gameStateManager->getTurnOrder();
        }

    } // namespace Server
} // namespace Blokus