#include "GameRoom.h"
#include "Session.h"
#include "PlayerInfo.h"  // 🔥 새로 추가
#include "Block.h"       // BlockFactory를 위해 추가
#include <spdlog/spdlog.h>
#include <algorithm>
#include <sstream>
#include <ctime>

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
            , m_waitingForGameResultResponses(false)
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
            Common::PlayerColor playerColor = it->getColor();
            bool wasInGame = (m_state == RoomState::Playing);

            m_players.erase(it);
            updateActivity();

            spdlog::info("✅ 방 {} 플레이어 제거: '{}' (남은: {}명)", m_roomId, username, m_players.size());

            // 다른 플레이어들에게 나간 것을 알림 (뮤텍스 내에서 직접 브로드캐스트)
            broadcastMessageLocked("PLAYER_LEFT:" + username);
            std::ostringstream leftMsg;
            leftMsg << username << "님이 퇴장하셨습니다. 현재 인원 : " << m_players.size() << "명";
            broadcastMessageLocked("SYSTEM:" + leftMsg.str());

            // 호스트가 나간 경우 새 호스트 선정
            if (wasHost && !m_players.empty()) {
                autoSelectNewHost();
                // 새 호스트 알림 (뮤텍스 내에서 직접 브로드캐스트)
                std::string newHostName = "";
                for (const auto& player : m_players) {
                    if (player.isHost()) {
                        newHostName = player.getUsername();
                        break;
                    }
                }
                if (!newHostName.empty()) {
                    broadcastMessageLocked("HOST_CHANGED:" + newHostName);
                    std::ostringstream hostMsg;
                    hostMsg << newHostName << "님이 방장이 되셨습니다";
                    broadcastMessageLocked("SYSTEM:" + hostMsg.str());
                }
            }

            // 방이 비었다면 게임 종료
            if (m_players.empty()) {
                spdlog::info("🏠 방 {} 모든 플레이어 퇴장으로 인한 게임 종료", m_roomId);
                m_state = RoomState::Disbanded;
                return true;
            }

            // 게임 중이었다면 추가 처리
            if (wasInGame && m_gameStateManager && m_state == RoomState::Playing) {
                // 현재 턴 순서와 인덱스 저장
                std::vector<Common::PlayerColor> oldTurnOrder = m_gameStateManager->getTurnOrder();
                int oldPlayerIndex = m_gameStateManager->getCurrentPlayerIndex();
                bool wasCurrentPlayerTurn = (m_gameStateManager->getCurrentPlayer() == playerColor);
                
                // 남은 플레이어들로 턴 순서 재설정
                std::vector<Common::PlayerColor> remainingTurnOrder;
                for (const auto& player : m_players) {
                    if (player.getColor() != Common::PlayerColor::None) {
                        remainingTurnOrder.push_back(player.getColor());
                    }
                }
                
                spdlog::info("🔄 플레이어 이탈로 인한 턴 순서 재설정: {} -> {}명", 
                    static_cast<int>(playerColor), remainingTurnOrder.size());
                
                // 게임 상태 매니저에 새로운 턴 순서 설정
                if (!remainingTurnOrder.empty()) {
                    // 나간 플레이어가 현재 턴이었다면 다음 플레이어 찾기
                    Common::PlayerColor nextPlayer = Common::PlayerColor::None;
                    if (wasCurrentPlayerTurn) {
                        // 기존 턴 순서에서 나간 플레이어 다음의 플레이어 찾기
                        for (int i = oldPlayerIndex + 1; i < oldTurnOrder.size() + oldPlayerIndex + 1; ++i) {
                            Common::PlayerColor candidatePlayer = oldTurnOrder[i % oldTurnOrder.size()];
                            // 남은 플레이어 목록에 있는지 확인
                            if (std::find(remainingTurnOrder.begin(), remainingTurnOrder.end(), candidatePlayer) != remainingTurnOrder.end()) {
                                nextPlayer = candidatePlayer;
                                break;
                            }
                        }
                    } else {
                        // 나간 플레이어가 현재 턴이 아니었다면 현재 턴 유지
                        nextPlayer = m_gameStateManager->getCurrentPlayer();
                    }
                    
                    m_gameStateManager->setTurnOrder(remainingTurnOrder);
                    
                    // 다음 플레이어로 턴 설정
                    if (nextPlayer != Common::PlayerColor::None) {
                        // nextPlayer에 해당하는 인덱스 찾기
                        auto it = std::find(remainingTurnOrder.begin(), remainingTurnOrder.end(), nextPlayer);
                        if (it != remainingTurnOrder.end()) {
                            int newPlayerIndex = std::distance(remainingTurnOrder.begin(), it);
                            m_gameStateManager->setCurrentPlayerIndex(newPlayerIndex);
                            m_gameStateManager->getGameLogic().setCurrentPlayer(nextPlayer);
                            
                            // 턴 변경 브로드캐스트
                            if (wasCurrentPlayerTurn) {
                                spdlog::info("🔄 나간 플레이어의 턴이었음, 다음 플레이어로 전환: {} -> {}", 
                                    static_cast<int>(playerColor), static_cast<int>(nextPlayer));
                                broadcastTurnChangeLocked(nextPlayer);
                            }
                        }
                    }
                }
                
                // 최소 인원 체크 (2명 미만이면 게임 종료)
                if (m_players.size() < Common::MIN_PLAYERS_TO_START) {
                    spdlog::info("🏁 방 {} 최소 인원 미달로 게임 종료", m_roomId);
                    endGameLocked();
                } else {
                    // 게임 계속 진행 - 게임 상태 브로드캐스트 (뮤텍스 내에서 안전하게)
                    spdlog::info("🎮 방 {} 플레이어 이탈했지만 게임 계속 진행 (남은: {}명)", m_roomId, m_players.size());
                    broadcastGameStateLocked();
                }
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

            // 모든 플레이어의 세션 상태를 게임 중으로 업데이트
            for (auto& player : m_players) {
                if (player.getSession()) {
                    player.getSession()->setStateToInGame();
                }
            }

            // 게임 시작 브로드캐스트 (뮤텍스 내에서 안전하게)
            broadcastMessageLocked("GAME_STARTED");
            
            std::ostringstream startMsg;
            startMsg << "게임이 시작되었습니다. 현재 인원 : " << m_players.size() << "명";
            broadcastMessageLocked("SYSTEM:" + startMsg.str());

            // 플레이어 정보도 함께 전송
            std::ostringstream playerInfoMsg;
            playerInfoMsg << "GAME_PLAYER_INFO";
            for (const auto& player : m_players) {
                playerInfoMsg << ":" << player.getUsername() << "," << static_cast<int>(player.getColor());
            }
            broadcastMessageLocked(playerInfoMsg.str());
            
            // 초기 게임 상태 브로드캐스트 (뮤텍스 내에서 안전하게)
            if (m_state == RoomState::Playing) {
                // JSON 형태로 게임 상태 생성
                std::ostringstream gameStateJson;
                gameStateJson << "GAME_STATE_UPDATE:{";
                
                // 현재 턴 정보
                Common::PlayerColor currentPlayer = m_gameStateManager->getCurrentPlayer();
                gameStateJson << "\"currentPlayer\":" << static_cast<int>(currentPlayer) << ",";
                gameStateJson << "\"turnNumber\":" << m_gameStateManager->getTurnNumber() << ",";
                
                // 간단한 보드 상태 (초기는 빈 보드)
                gameStateJson << "\"boardState\":[], \"scores\":{}}";
                
                broadcastMessageLocked(gameStateJson.str());
            }

            // 게임 시작 후 첫 번째 플레이어가 블록을 배치할 수 없다면 자동 스킵 체크
            int autoSkipCount = 0;
            int maxAutoSkips = m_players.size(); // 최대 플레이어 수만큼만 스킵 허용
            bool shouldCheckAutoSkip = true;
            
            while (shouldCheckAutoSkip && m_state == RoomState::Playing && autoSkipCount < maxAutoSkips) {
                Common::PlayerColor checkPlayer = m_gameStateManager->getCurrentPlayer();
                
                if (m_gameLogic->canPlayerPlaceAnyBlock(checkPlayer)) {
                    // 현재 플레이어가 블록을 배치할 수 있으면 중단
                    shouldCheckAutoSkip = false;
                } else {
                    autoSkipCount++;
                    
                    // 현재 플레이어가 블록을 배치할 수 없으면 자동 스킵
                    std::string playerName = "";
                    for (const auto& player : m_players) {
                        if (player.getColor() == checkPlayer) {
                            playerName = player.getUsername();
                            break;
                        }
                    }
                    
                    spdlog::info("🔄 게임 시작 후 자동 턴 스킵 {}/{}: {} (색상 {})님이 더 이상 배치할 블록이 없음", 
                        autoSkipCount, maxAutoSkips, playerName, static_cast<int>(checkPlayer));
                    
                    // 자동 턴 스킵 알림 메시지
                    std::ostringstream skipMsg;
                    skipMsg << "SYSTEM:" << playerName << "님이 배치할 수 있는 블록이 없어 자동으로 턴이 넘어갑니다.";
                    broadcastMessageLocked(skipMsg.str());
                    
                    // 턴 넘기기
                    Common::PlayerColor prevPlayer = checkPlayer;
                    m_gameStateManager->nextTurn();
                    Common::PlayerColor nextPlayer = m_gameStateManager->getCurrentPlayer();
                    
                    spdlog::info("🔄 게임 시작 후 자동 턴 전환: {} -> {}", static_cast<int>(prevPlayer), static_cast<int>(nextPlayer));
                    
                    // 턴 변경 브로드캐스트
                    if (nextPlayer != prevPlayer) {
                        broadcastTurnChangeLocked(nextPlayer);
                    }
                    
                    // 게임 상태 브로드캐스트
                    broadcastGameStateLocked();
                    
                    // 모든 플레이어가 한 번씩 스킵되었으면 게임 종료
                    if (autoSkipCount >= maxAutoSkips) {
                        spdlog::info("🏁 게임 시작 직후 모든 활성 플레이어가 스킵됨, 게임 종료 (방 {})", m_roomId);
                        
                        // 최종 점수 계산
                        auto finalScores = m_gameLogic->calculateScores();
                        
                        // 승자 결정 (가장 높은 점수를 가진 플레이어)
                        Common::PlayerColor winner = Common::PlayerColor::None;
                        int highestScore = -1;
                        std::vector<Common::PlayerColor> winners; // 동점자 처리
                        
                        for (const auto& score : finalScores) {
                            // 실제 게임에 참여한 플레이어들만 고려
                            bool isActivePlayer = false;
                            for (const auto& player : m_players) {
                                if (player.getColor() == score.first) {
                                    isActivePlayer = true;
                                    break;
                                }
                            }
                            
                            if (isActivePlayer) {
                                if (score.second > highestScore) {
                                    highestScore = score.second;
                                    winners.clear();
                                    winners.push_back(score.first);
                                    winner = score.first;
                                } else if (score.second == highestScore) {
                                    winners.push_back(score.first);
                                }
                            }
                        }
                        
                        // 게임 결과 브로드캐스트
                        broadcastGameResultLocked(finalScores, winners);
                        shouldCheckAutoSkip = false;
                        break;
                    }
                }
            }

            spdlog::info("🎮 방 {} 게임 시작: {} 플레이어, 턴 순서 설정됨", m_roomId, m_players.size());
            return true;
        }

        bool GameRoom::endGame() {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return endGameLocked();
        }

        bool GameRoom::endGameLocked() {
            // 뮤텍스가 이미 잠겨있다고 가정하고 실행 (데드락 방지용)
            if (m_state != RoomState::Playing) {
                return false;
            }

            m_state = RoomState::Waiting;
            updateActivity();

            // 모든 플레이어 게임 상태 리셋
            for (auto& player : m_players) {
                player.resetForNewGame();
                // 세션 상태를 방에 있는 상태로 복원
                if (player.getSession()) {
                    player.getSession()->setStateToInRoom(m_roomId);
                }
            }

            // 게임 종료 후 플레이어 색상 재할당
            assignColorsAutomatically();

            // 게임 종료 브로드캐스트 (뮤텍스 내에서 안전하게)
            broadcastMessageLocked("GAME_ENDED");
            broadcastMessageLocked("SYSTEM:게임이 종료되었습니다.");

            // 게임 종료 후 방 정보 업데이트 브로드캐스트
            broadcastRoomInfoLocked();

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
            broadcastMessageLocked("GAME_RESET");
        }

        // ========================================
        // 메시지 전송 (PlayerInfo 위임)
        // ========================================

        void GameRoom::broadcastMessage(const std::string& message, const std::string& excludeUserId) {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            broadcastMessageLocked(message, excludeUserId);
        }

        void GameRoom::broadcastMessageLocked(const std::string& message, const std::string& excludeUserId) {
            // 뮤텍스가 이미 잠겨있다고 가정하고 실행 (데드락 방지용)
            spdlog::info("📢 브로드캐스트 시작: 방 {}, 메시지: '{}', 플레이어 수: {}", 
                m_roomId, message.substr(0, 50) + (message.length() > 50 ? "..." : ""), m_players.size());

            int sentCount = 0;
            for (const auto& player : m_players) {
                spdlog::debug("  플레이어 체크: {} (연결됨: {}, 제외여부: {})", 
                    player.getUsername(), player.isConnected(), player.getUserId() == excludeUserId);
                
                if (player.getUserId() != excludeUserId && player.isConnected()) {
                    try {
                        player.sendMessage(message);
                        sentCount++;
                        spdlog::debug("  ✅ 전송 성공: {}", player.getUsername());
                    }
                    catch (const std::exception& e) {
                        spdlog::error("❌ 방 {} 메시지 전송 실패 (플레이어: '{}'): {}",
                            m_roomId, player.getUsername(), e.what());
                    }
                }
            }
            
            spdlog::info("📢 브로드캐스트 완료: {}/{} 플레이어에게 전송", sentCount, m_players.size());
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
            std::lock_guard<std::mutex> lock(m_playersMutex);
            
            // 구조화된 메시지와 시스템 메시지 모두 전송
            broadcastMessageLocked("PLAYER_JOINED:" + username);
            
            std::ostringstream oss;
            oss << username << "님이 입장하셨습니다. 현재 인원 : " << m_players.size() << "명";
            broadcastMessageLocked("SYSTEM:" + oss.str());
        }

        void GameRoom::broadcastPlayerLeft(const std::string& username) {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            
            // 구조화된 메시지와 시스템 메시지 모두 전송
            broadcastMessageLocked("PLAYER_LEFT:" + username);
            
            std::ostringstream oss;
            oss << username << "님이 퇴장하셨습니다. 현재 인원 : " << m_players.size() << "명";
            broadcastMessageLocked("SYSTEM:" + oss.str());
        }

        void GameRoom::broadcastPlayerReady(const std::string& username, bool ready) {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            std::ostringstream oss;
            oss << "PLAYER_READY:" << username << ":" << (ready ? "1" : "0");
            broadcastMessageLocked(oss.str());
        }

        void GameRoom::broadcastHostChanged(const std::string& newHostName) {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            
            // 구조화된 메시지만 전송 (시스템 메시지는 호출하는 곳에서 처리)
            broadcastMessageLocked("HOST_CHANGED:" + newHostName);
        }


        void GameRoom::broadcastGameEnd() {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            
            // 구조화된 메시지만 전송 (시스템 메시지는 endGameLocked에서 처리)
            broadcastMessageLocked("GAME_ENDED");
        }

        void GameRoom::broadcastRoomInfoLocked() {
            // 뮤텍스가 이미 잠겨있다고 가정하고 실행 (데드락 방지용)
            
            // 호스트 이름 직접 찾기 (뮤텍스 데드락 방지)
            std::string hostName = "Unknown";
            const auto* host = findPlayerById(m_players, m_hostId);
            if (host) {
                hostName = host->getUsername();
            }
            
            // ROOM_INFO 메시지 생성
            std::ostringstream response;
            response << "ROOM_INFO:" << m_roomId << ":" << m_roomName
                     << ":" << hostName << ":" << m_players.size()
                     << ":" << m_maxPlayers << ":" << (m_isPrivate ? "1" : "0")
                     << ":" << (m_state == RoomState::Playing ? "1" : "0") << ":클래식";
            
            // 플레이어 데이터 추가 (userId,username,isHost,isReady,colorIndex)
            for (const auto& player : m_players) {
                response << ":" << player.getUserId() << "," << player.getUsername()
                         << "," << (player.isHost() ? "1" : "0") << "," << (player.isReady() ? "1" : "0")
                         << "," << static_cast<int>(player.getColor());
            }
            
            std::string roomInfoMessage = response.str();
            
            spdlog::info("📤 방 {} ROOM_INFO 브로드캐스트: {}", m_roomId, roomInfoMessage);
            
            // 방의 모든 플레이어에게 브로드캐스트
            broadcastMessageLocked(roomInfoMessage);
        }

        void GameRoom::broadcastGameState() {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            broadcastGameStateLocked();
        }

        void GameRoom::broadcastGameStateLocked() {
            // 뮤텍스가 이미 잠겨있다고 가정하고 실행 (데드락 방지용)
            
            if (m_state != RoomState::Playing) {
                return;
            }

            // JSON 형태로 게임 상태 생성
            std::ostringstream gameStateJson;
            gameStateJson << "GAME_STATE_UPDATE:{";
            
            // 현재 턴 정보
            Common::PlayerColor currentPlayer = m_gameStateManager->getCurrentPlayer();
            gameStateJson << "\"currentPlayer\":" << static_cast<int>(currentPlayer) << ",";
            gameStateJson << "\"turnNumber\":" << m_gameStateManager->getTurnNumber() << ",";
            
            // 보드 상태 (간단한 형태로)
            gameStateJson << "\"boardState\":[";
            for (int row = 0; row < Common::BOARD_SIZE; ++row) {
                if (row > 0) gameStateJson << ",";
                gameStateJson << "[";
                for (int col = 0; col < Common::BOARD_SIZE; ++col) {
                    if (col > 0) gameStateJson << ",";
                    Common::PlayerColor cellOwner = m_gameLogic->getBoardCell(row, col);
                    gameStateJson << static_cast<int>(cellOwner);
                }
                gameStateJson << "]";
            }
            gameStateJson << "],";
            
            // 플레이어 점수 정보
            auto scores = m_gameLogic->calculateScores();
            gameStateJson << "\"scores\":{";
            bool firstScore = true;
            for (const auto& score : scores) {
                if (!firstScore) gameStateJson << ",";
                gameStateJson << "\"" << static_cast<int>(score.first) << "\":" << score.second;
                firstScore = false;
            }
            gameStateJson << "},";
            
            // 플레이어 남은 블록 개수 정보
            gameStateJson << "\"remainingBlocks\":{";
            bool firstRemaining = true;
            for (const auto& player : m_players) {
                if (!firstRemaining) gameStateJson << ",";
                
                // 남은 블록 개수 계산 (전체 블록 수 - 사용된 블록 수)
                auto availableBlocks = m_gameLogic->getAvailableBlocks(player.getColor());
                int remainingCount = static_cast<int>(availableBlocks.size());
                
                gameStateJson << "\"" << static_cast<int>(player.getColor()) << "\":" << remainingCount;
                firstRemaining = false;
            }
            gameStateJson << "}";
            
            gameStateJson << "}";
            
            // 모든 플레이어에게 브로드캐스트
            std::string message = gameStateJson.str();
            broadcastMessageLocked(message);
            
            spdlog::info("🔄 게임 상태 브로드캐스트: 방 {}, 현재 턴: {}", 
                m_roomId, static_cast<int>(currentPlayer));
        }

        void GameRoom::broadcastBlockPlacement(const std::string& playerName, const Common::BlockPlacement& placement, int scoreGained) {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            broadcastBlockPlacementLocked(playerName, placement, scoreGained);
        }

        void GameRoom::broadcastBlockPlacementLocked(const std::string& playerName, const Common::BlockPlacement& placement, int scoreGained) {
            // 뮤텍스가 이미 잠겨있다고 가정하고 실행 (데드락 방지용)
            
            spdlog::info("📦 블록 배치 브로드캐스트 - 방 {}, 플레이어 수: {}", m_roomId, m_players.size());
            
            // 블록 배치 알림 메시지 생성
            std::ostringstream blockPlacementMsg;
            blockPlacementMsg << "BLOCK_PLACED:{"
                << "\"player\":\"" << playerName << "\","
                << "\"blockType\":" << static_cast<int>(placement.type) << ","
                << "\"position\":{\"row\":" << placement.position.first << ",\"col\":" << placement.position.second << "},"
                << "\"rotation\":" << static_cast<int>(placement.rotation) << ","
                << "\"flip\":" << static_cast<int>(placement.flip) << ","
                << "\"playerColor\":" << static_cast<int>(placement.player) << ","
                << "\"scoreGained\":" << scoreGained
                << "}";
            
            broadcastMessageLocked(blockPlacementMsg.str());
            
            // 시스템 메시지로도 알림
            std::ostringstream systemMsg;
            std::string blockName = Common::BlockFactory::getBlockName(placement.type);
            systemMsg << "SYSTEM:" << playerName << "님이 " << blockName << " 블록을 배치했습니다. (점수: +" << scoreGained << ")";
            broadcastMessageLocked(systemMsg.str());
            
            spdlog::info("📦 블록 배치 브로드캐스트: 방 {}, 플레이어 {}, 블록 타입 {}", 
                m_roomId, playerName, static_cast<int>(placement.type));
        }

        void GameRoom::broadcastTurnChange(Common::PlayerColor newPlayer) {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            broadcastTurnChangeLocked(newPlayer);
        }

        void GameRoom::broadcastTurnChangeLocked(Common::PlayerColor newPlayer) {
            // 뮤텍스가 이미 잠겨있다고 가정하고 실행 (데드락 방지용)
            
            // 새 플레이어 이름 찾기
            std::string newPlayerName = "";
            for (const auto& player : m_players) {
                if (player.getColor() == newPlayer) {
                    newPlayerName = player.getUsername();
                    break;
                }
            }
            
            // 플레이어를 찾지 못한 경우 오류 로깅
            if (newPlayerName.empty()) {
                spdlog::warn("⚠️ 턴 변경 실패: 플레이어 색상 {}에 해당하는 플레이어를 찾을 수 없음", static_cast<int>(newPlayer));
                return; // 빈 슬롯 메시지 방지
            }
            
            // 턴 변경 알림 메시지
            std::ostringstream turnChangeMsg;
            turnChangeMsg << "TURN_CHANGED:{"
                << "\"newPlayer\":\"" << newPlayerName << "\","
                << "\"playerColor\":" << static_cast<int>(newPlayer) << ","
                << "\"turnNumber\":" << m_gameStateManager->getTurnNumber()
                << "}";
            
            broadcastMessageLocked(turnChangeMsg.str());
            
            // 시스템 메시지
            std::ostringstream systemMsg;
            systemMsg << "SYSTEM:" << newPlayerName << "님의 턴입니다.";
            broadcastMessageLocked(systemMsg.str());
            
            spdlog::info("🔄 턴 변경 브로드캐스트: 방 {}, 새 플레이어 {} ({})", 
                m_roomId, newPlayerName, static_cast<int>(newPlayer));
        }

        void GameRoom::broadcastGameResultLocked(const std::map<Common::PlayerColor, int>& finalScores, 
                                               const std::vector<Common::PlayerColor>& winners) {
            // 뮤텍스가 이미 잠겨있다고 가정하고 실행 (데드락 방지용)
            
            // 게임 결과 JSON 메시지 생성
            std::ostringstream gameResultMsg;
            gameResultMsg << "GAME_RESULT:{";
            
            // 점수 정보
            gameResultMsg << "\"scores\":{";
            bool firstScore = true;
            for (const auto& score : finalScores) {
                if (!firstScore) gameResultMsg << ",";
                
                // 플레이어 이름 찾기
                std::string playerName = "";
                for (const auto& player : m_players) {
                    if (player.getColor() == score.first) {
                        playerName = player.getUsername();
                        break;
                    }
                }
                
                if (!playerName.empty()) {
                    gameResultMsg << "\"" << playerName << "\":" << score.second;
                    firstScore = false;
                }
            }
            gameResultMsg << "},";
            
            // 승자 정보
            gameResultMsg << "\"winners\":[";
            bool firstWinner = true;
            for (const auto& winnerColor : winners) {
                if (!firstWinner) gameResultMsg << ",";
                
                // 승자 이름 찾기
                std::string winnerName = "";
                for (const auto& player : m_players) {
                    if (player.getColor() == winnerColor) {
                        winnerName = player.getUsername();
                        break;
                    }
                }
                
                if (!winnerName.empty()) {
                    gameResultMsg << "\"" << winnerName << "\"";
                    firstWinner = false;
                }
            }
            gameResultMsg << "],";
            
            // 게임 타입 및 기타 정보
            gameResultMsg << "\"gameType\":\"블로커스\",";
            gameResultMsg << "\"roomId\":" << m_roomId << ",";
            gameResultMsg << "\"timestamp\":\"" << std::time(nullptr) << "\"";
            gameResultMsg << "}";
            
            // 게임 결과 브로드캐스트
            broadcastMessageLocked(gameResultMsg.str());
            
            // 시스템 메시지로도 결과 알림
            std::ostringstream systemMsg;
            if (winners.size() == 1) {
                std::string winnerName = "";
                for (const auto& player : m_players) {
                    if (player.getColor() == winners[0]) {
                        winnerName = player.getUsername();
                        break;
                    }
                }
                systemMsg << "SYSTEM:🎉 게임이 종료되었습니다! 승자: " << winnerName << "님!";
            } else if (winners.size() > 1) {
                systemMsg << "SYSTEM:🎉 게임이 종료되었습니다! 동점 승부입니다!";
            } else {
                systemMsg << "SYSTEM:🎉 게임이 종료되었습니다!";
            }
            broadcastMessageLocked(systemMsg.str());
            
            // 게임 결과 응답 대기 상태로 설정
            m_waitingForGameResultResponses = true;
            m_gameResultResponses.clear();
            m_playersToLeave.clear();
            
            spdlog::info("🏆 게임 결과 브로드캐스트: 방 {}, 승자 수: {}명, 응답 대기 시작", m_roomId, winners.size());
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

            // 플레이어 찾기 (뮤텍스 내에서 직접 검색)
            auto* player = findPlayerById(m_players, userId);
            if (!player) {
                spdlog::warn("❌ 블록 배치 실패: 플레이어를 찾을 수 없음 (방 {}, 사용자 {})", m_roomId, userId);
                return false;
            }

            // 플레이어 턴 확인 (직접 확인으로 데드락 방지)
            if (player->getColor() != m_gameStateManager->getCurrentPlayer()) {
                spdlog::warn("❌ 블록 배치 실패: 플레이어 턴이 아님 (방 {}, 사용자 {}, 현재 턴: {})", 
                    m_roomId, userId, static_cast<int>(m_gameStateManager->getCurrentPlayer()));
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

            // 블록 사용 상태 업데이트
            m_gameLogic->setPlayerBlockUsed(placement.player, placement.type);

            // 성공적으로 배치됨 - 점수 계산
            int scoreGained = Common::BlockFactory::getBlockScore(placement.type);
            
            spdlog::info("✅ 블록 배치 성공 (방 {}, 사용자 {}, 블록 타입: {}, 획득 점수: {})", 
                m_roomId, userId, static_cast<int>(placement.type), scoreGained);

            // 블록 배치 알림 브로드캐스트 (뮤텍스 내에서 안전하게)
            spdlog::info("🔄 블록 배치 브로드캐스트 시작: 방 {}", m_roomId);
            broadcastBlockPlacementLocked(player->getUsername(), placement, scoreGained);

            // 다음 턴으로 전환
            Common::PlayerColor previousPlayer = m_gameStateManager->getCurrentPlayer();
            spdlog::info("🔄 턴 전환 시작: {} -> ?", static_cast<int>(previousPlayer));
            m_gameStateManager->nextTurn();
            Common::PlayerColor newPlayer = m_gameStateManager->getCurrentPlayer();
            spdlog::info("🔄 턴 전환 완료: {} -> {}", static_cast<int>(previousPlayer), static_cast<int>(newPlayer));

            // 턴 변경 알림 브로드캐스트 (뮤텍스 내에서 안전하게)
            if (newPlayer != previousPlayer) {
                spdlog::info("🔄 턴 변경 브로드캐스트 시작");
                
                // 새 플레이어 이름 찾기
                std::string newPlayerName = "";
                for (const auto& p : m_players) {
                    if (p.getColor() == newPlayer) {
                        newPlayerName = p.getUsername();
                        break;
                    }
                }
                
                // 플레이어를 찾지 못한 경우 오류 로깅 후 스킵
                if (newPlayerName.empty()) {
                    spdlog::warn("⚠️ 턴 변경 실패: 플레이어 색상 {}에 해당하는 플레이어를 찾을 수 없음", static_cast<int>(newPlayer));
                } else {
                    // 턴 변경 알림 메시지
                    std::ostringstream turnChangeMsg;
                    turnChangeMsg << "TURN_CHANGED:{"
                        << "\"newPlayer\":\"" << newPlayerName << "\","
                        << "\"playerColor\":" << static_cast<int>(newPlayer) << ","
                        << "\"turnNumber\":" << m_gameStateManager->getTurnNumber()
                        << "}";
                    
                    broadcastMessageLocked(turnChangeMsg.str());
                    
                    // 시스템 메시지
                    std::ostringstream turnSystemMsg;
                    turnSystemMsg << "SYSTEM:" << newPlayerName << "님의 턴입니다.";
                    broadcastMessageLocked(turnSystemMsg.str());
                }
            }

            // 전체 게임 상태 브로드캐스트 (뮤텍스 내에서 안전하게)
            broadcastGameStateLocked();

            // 새 플레이어가 블록을 배치할 수 없다면 자동 턴 스킵 체크
            // 단, 뮤텍스를 이미 잡고 있으므로 별도 함수 호출하지 않고 직접 처리
            int autoSkipCount = 0;
            int maxAutoSkips = m_players.size(); // 최대 플레이어 수만큼만 스킵 허용
            bool shouldCheckAutoSkip = true;
            
            while (shouldCheckAutoSkip && m_state == RoomState::Playing && autoSkipCount < maxAutoSkips) {
                Common::PlayerColor checkPlayer = m_gameStateManager->getCurrentPlayer();
                
                if (m_gameLogic->canPlayerPlaceAnyBlock(checkPlayer)) {
                    // 현재 플레이어가 블록을 배치할 수 있으면 중단
                    shouldCheckAutoSkip = false;
                } else {
                    autoSkipCount++;
                    
                    // 현재 플레이어가 블록을 배치할 수 없으면 자동 스킵
                    std::string playerName = "";
                    for (const auto& player : m_players) {
                        if (player.getColor() == checkPlayer) {
                            playerName = player.getUsername();
                            break;
                        }
                    }
                    
                    spdlog::info("🔄 자동 턴 스킵 {}/{}: {} (색상 {})님이 더 이상 배치할 블록이 없음", 
                        autoSkipCount, maxAutoSkips, playerName, static_cast<int>(checkPlayer));
                    
                    // 자동 턴 스킵 알림 메시지
                    std::ostringstream skipMsg;
                    skipMsg << "SYSTEM:" << playerName << "님이 배치할 수 있는 블록이 없어 자동으로 턴이 넘어갑니다.";
                    broadcastMessageLocked(skipMsg.str());
                    
                    // 턴 넘기기
                    Common::PlayerColor prevPlayer = checkPlayer;
                    m_gameStateManager->nextTurn();
                    Common::PlayerColor nextPlayer = m_gameStateManager->getCurrentPlayer();
                    
                    spdlog::info("🔄 자동 턴 전환: {} -> {}", static_cast<int>(prevPlayer), static_cast<int>(nextPlayer));
                    
                    // 턴 변경 브로드캐스트
                    if (nextPlayer != prevPlayer) {
                        broadcastTurnChangeLocked(nextPlayer);
                    }
                    
                    // 게임 상태 브로드캐스트
                    broadcastGameStateLocked();
                    
                    // 모든 플레이어가 한 번씩 스킵되었으면 게임 종료
                    if (autoSkipCount >= maxAutoSkips) {
                        spdlog::info("🏁 모든 활성 플레이어가 스킵됨, 게임 종료 (방 {})", m_roomId);
                        shouldCheckAutoSkip = false;
                        break;
                    }
                }
            }
            
            // 모든 활성 플레이어가 스킵되었으면 게임 종료 처리
            if (autoSkipCount >= maxAutoSkips) {
                spdlog::info("🏁 게임 종료 조건 충족: 모든 활성 플레이어가 블록 배치 불가 (방 {})", m_roomId);
                
                // 최종 점수 계산
                auto finalScores = m_gameLogic->calculateScores();
                
                // 승자 결정 (가장 높은 점수를 가진 플레이어)
                Common::PlayerColor winner = Common::PlayerColor::None;
                int highestScore = -1;
                std::vector<Common::PlayerColor> winners; // 동점자 처리
                
                for (const auto& score : finalScores) {
                    // 실제 게임에 참여한 플레이어들만 고려
                    bool isActivePlayer = false;
                    for (const auto& player : m_players) {
                        if (player.getColor() == score.first) {
                            isActivePlayer = true;
                            break;
                        }
                    }
                    
                    if (isActivePlayer) {
                        if (score.second > highestScore) {
                            highestScore = score.second;
                            winners.clear();
                            winners.push_back(score.first);
                            winner = score.first;
                        } else if (score.second == highestScore) {
                            winners.push_back(score.first);
                        }
                    }
                }
                
                // 게임 결과 브로드캐스트
                broadcastGameResultLocked(finalScores, winners);
            }

            // 게임 종료 조건 확인: 모든 플레이어가 더 이상 블록을 배치할 수 없는 경우
            if (m_gameLogic->isGameFinished()) {
                spdlog::info("🏁 게임 종료 조건 충족: 모든 플레이어가 블록 배치 불가 (방 {})", m_roomId);
                
                // 최종 점수 계산
                auto finalScores = m_gameLogic->calculateScores();
                
                // 승자 결정 (가장 높은 점수를 가진 플레이어)
                Common::PlayerColor winner = Common::PlayerColor::None;
                int highestScore = -1;
                std::vector<Common::PlayerColor> winners; // 동점자 처리
                
                for (const auto& score : finalScores) {
                    if (score.second > highestScore) {
                        highestScore = score.second;
                        winners.clear();
                        winners.push_back(score.first);
                        winner = score.first;
                    } else if (score.second == highestScore) {
                        winners.push_back(score.first);
                    }
                }
                
                // 게임 결과 브로드캐스트
                broadcastGameResultLocked(finalScores, winners);
                
                // 게임 종료 처리는 플레이어 응답 후에 수행하므로 여기서는 하지 않음
            } else if (m_gameStateManager->getGameState() == Common::GameState::Finished) {
                endGameLocked();
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
            spdlog::info("⏭️ 수동 턴 스킵 (방 {}, 사용자 {})", m_roomId, userId);
            
            Common::PlayerColor previousPlayer = m_gameStateManager->getCurrentPlayer();
            m_gameStateManager->skipTurn();
            Common::PlayerColor newPlayer = m_gameStateManager->getCurrentPlayer();
            
            // 턴 변경 브로드캐스트
            if (newPlayer != previousPlayer) {
                broadcastTurnChangeLocked(newPlayer);
            }
            
            // 게임 상태 브로드캐스트
            broadcastGameStateLocked();
            
            // 자동 턴 스킵 체크 (새로운 플레이어도 블록을 배치할 수 없다면)
            int autoSkipCount = 0;
            int maxAutoSkips = m_players.size(); // 최대 플레이어 수만큼만 스킵 허용
            bool shouldCheckAutoSkip = true;
            
            while (shouldCheckAutoSkip && m_state == RoomState::Playing && autoSkipCount < maxAutoSkips) {
                Common::PlayerColor checkPlayer = m_gameStateManager->getCurrentPlayer();
                
                if (m_gameLogic->canPlayerPlaceAnyBlock(checkPlayer)) {
                    // 현재 플레이어가 블록을 배치할 수 있으면 중단
                    shouldCheckAutoSkip = false;
                } else {
                    autoSkipCount++;
                    
                    // 현재 플레이어가 블록을 배치할 수 없으면 자동 스킵
                    std::string playerName = "";
                    for (const auto& player : m_players) {
                        if (player.getColor() == checkPlayer) {
                            playerName = player.getUsername();
                            break;
                        }
                    }
                    
                    spdlog::info("🔄 수동 스킵 후 자동 턴 스킵 {}/{}: {} (색상 {})님이 더 이상 배치할 블록이 없음", 
                        autoSkipCount, maxAutoSkips, playerName, static_cast<int>(checkPlayer));
                    
                    // 자동 턴 스킵 알림 메시지
                    std::ostringstream skipMsg;
                    skipMsg << "SYSTEM:" << playerName << "님이 배치할 수 있는 블록이 없어 자동으로 턴이 넘어갑니다.";
                    broadcastMessageLocked(skipMsg.str());
                    
                    // 턴 넘기기
                    Common::PlayerColor prevPlayer = checkPlayer;
                    m_gameStateManager->nextTurn();
                    Common::PlayerColor nextPlayer = m_gameStateManager->getCurrentPlayer();
                    
                    spdlog::info("🔄 자동 턴 전환: {} -> {}", static_cast<int>(prevPlayer), static_cast<int>(nextPlayer));
                    
                    // 턴 변경 브로드캐스트
                    if (nextPlayer != prevPlayer) {
                        broadcastTurnChangeLocked(nextPlayer);
                    }
                    
                    // 게임 상태 브로드캐스트
                    broadcastGameStateLocked();
                    
                    // 모든 플레이어가 한 번씩 스킵되었으면 게임 종료
                    if (autoSkipCount >= maxAutoSkips) {
                        spdlog::info("🏁 수동 스킵 후 모든 활성 플레이어가 스킵됨, 게임 종료 (방 {})", m_roomId);
                        shouldCheckAutoSkip = false;
                        break;
                    }
                }
            }

            // 수동 스킵 후 모든 활성 플레이어가 스킵되었으면 게임 종료 처리
            if (autoSkipCount >= maxAutoSkips) {
                spdlog::info("🏁 수동 스킵 후 게임 종료 조건 충족: 모든 활성 플레이어가 블록 배치 불가 (방 {})", m_roomId);
                
                // 최종 점수 계산
                auto finalScores = m_gameLogic->calculateScores();
                
                // 승자 결정 (가장 높은 점수를 가진 플레이어)
                Common::PlayerColor winner = Common::PlayerColor::None;
                int highestScore = -1;
                std::vector<Common::PlayerColor> winners; // 동점자 처리
                
                for (const auto& score : finalScores) {
                    // 실제 게임에 참여한 플레이어들만 고려
                    bool isActivePlayer = false;
                    for (const auto& player : m_players) {
                        if (player.getColor() == score.first) {
                            isActivePlayer = true;
                            break;
                        }
                    }
                    
                    if (isActivePlayer) {
                        if (score.second > highestScore) {
                            highestScore = score.second;
                            winners.clear();
                            winners.push_back(score.first);
                            winner = score.first;
                        } else if (score.second == highestScore) {
                            winners.push_back(score.first);
                        }
                    }
                }
                
                // 게임 결과 브로드캐스트
                broadcastGameResultLocked(finalScores, winners);
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

        bool GameRoom::canCurrentPlayerMakeMove() const {
            if (m_state != RoomState::Playing) {
                return false;
            }
            
            Common::PlayerColor currentPlayer = m_gameStateManager->getCurrentPlayer();
            return m_gameLogic->canPlayerPlaceAnyBlock(currentPlayer);
        }

        // ========================================
        // 게임 결과 응답 처리 메서드들
        // ========================================

        bool GameRoom::handleGameResultResponse(const std::string& userId, const std::string& response) {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            
            // 게임 결과 응답 대기 중이 아니면 무시
            if (!m_waitingForGameResultResponses) {
                spdlog::warn("⚠️ 게임 결과 응답 처리 실패: 응답 대기 중이 아님 (방 {}, 사용자 {})", m_roomId, userId);
                return false;
            }
            
            // 플레이어 존재 확인
            auto* player = findPlayerById(m_players, userId);
            if (!player) {
                spdlog::warn("⚠️ 게임 결과 응답 처리 실패: 플레이어를 찾을 수 없음 (방 {}, 사용자 {})", m_roomId, userId);
                return false;
            }
            
            // 이미 응답한 플레이어인지 확인
            if (m_gameResultResponses.find(userId) != m_gameResultResponses.end()) {
                spdlog::warn("⚠️ 게임 결과 응답 처리 실패: 이미 응답한 플레이어 (방 {}, 사용자 {})", m_roomId, userId);
                return false;
            }
            
            // 응답 저장
            m_gameResultResponses[userId] = response;
            
            // LEAVE 응답인 경우 나가기 목록에 추가
            if (response == "LEAVE") {
                m_playersToLeave.insert(userId);
            }
            
            spdlog::info("📝 게임 결과 응답 처리: 방 {}, 사용자 {}, 응답 {}, 응답 수: {}/{}", 
                m_roomId, userId, response, m_gameResultResponses.size(), m_players.size());
            
            // 모든 플레이어가 응답했는지 확인
            if (allPlayersResponded()) {
                spdlog::info("✅ 모든 플레이어 응답 완료, 게임 결과 처리 시작 (방 {})", m_roomId);
                processGameResultResponses();
            }
            
            return true;
        }

        bool GameRoom::allPlayersResponded() const {
            // 뮤텍스가 이미 잠겨있다고 가정
            return m_gameResultResponses.size() == m_players.size();
        }

        void GameRoom::processGameResultResponses() {
            // 뮤텍스가 이미 잠겨있다고 가정하고 실행 (데드락 방지용)
            
            spdlog::info("🔄 게임 결과 응답 처리 시작: 방 {}, 나가기 선택: {}명, 남기 선택: {}명", 
                m_roomId, m_playersToLeave.size(), m_players.size() - m_playersToLeave.size());
            
            // 응답 대기 상태 해제
            m_waitingForGameResultResponses = false;
            
            // 방을 나가기로 선택한 플레이어들 제거
            std::vector<std::string> playersToRemove(m_playersToLeave.begin(), m_playersToLeave.end());
            for (const auto& userId : playersToRemove) {
                auto* player = findPlayerById(m_players, userId);
                if (player) {
                    std::string username = player->getUsername();
                    
                    // 플레이어에게 방 나가기 확인 메시지 전송
                    player->sendMessage("LEAVE_ROOM_CONFIRMED");
                    
                    // 플레이어 제거 (removePlayer는 이미 뮤텍스를 사용하므로 직접 제거)
                    auto it = std::find_if(m_players.begin(), m_players.end(),
                        [&userId](const PlayerInfo& p) { return p.getUserId() == userId; });
                    if (it != m_players.end()) {
                        m_players.erase(it);
                        
                        // 다른 플레이어들에게 알림
                        std::ostringstream leftMsg;
                        leftMsg << username << "님이 퇴장하셨습니다. 현재 인원 : " << m_players.size() << "명";
                        broadcastMessageLocked("PLAYER_LEFT:" + username);
                        broadcastMessageLocked("SYSTEM:" + leftMsg.str());
                        
                        spdlog::info("👋 게임 결과로 인한 플레이어 퇴장: 방 {}, 사용자 {} ({})", 
                            m_roomId, userId, username);
                    }
                }
            }
            
            // 새 호스트 선정 (필요한 경우)
            if (!m_players.empty() && m_playersToLeave.find(m_hostId) != m_playersToLeave.end()) {
                autoSelectNewHost();
                std::string newHostName = "";
                for (const auto& player : m_players) {
                    if (player.isHost()) {
                        newHostName = player.getUsername();
                        break;
                    }
                }
                if (!newHostName.empty()) {
                    broadcastMessageLocked("HOST_CHANGED:" + newHostName);
                    std::ostringstream hostMsg;
                    hostMsg << newHostName << "님이 방장이 되셨습니다";
                    broadcastMessageLocked("SYSTEM:" + hostMsg.str());
                }
            }
            
            // 방이 비었는지 확인
            if (m_players.empty()) {
                spdlog::info("🏠 방 {} 모든 플레이어 퇴장으로 인한 방 해체", m_roomId);
                m_state = RoomState::Disbanded;
                return;
            }
            
            // 게임 초기화 및 대기 상태로 전환
            spdlog::info("🔄 게임 초기화 및 대기 상태 전환: 방 {}", m_roomId);
            
            // 게임 상태 초기화
            m_gameLogic->clearBoard();
            m_gameStateManager->resetGame();
            m_state = RoomState::Waiting;
            
            // 남은 플레이어들 상태 초기화
            for (auto& player : m_players) {
                player.resetForNewGame();
                // 세션 상태를 방에 있는 상태로 설정
                if (player.getSession()) {
                    player.getSession()->setStateToInRoom(m_roomId);
                }
            }
            
            // 플레이어 색상 재할당
            assignColorsAutomatically();
            
            // 응답 데이터 정리
            m_gameResultResponses.clear();
            m_playersToLeave.clear();
            
            // 방 정보 업데이트 브로드캐스트
            broadcastRoomInfoLocked();
            
            // 게임 초기화 완료 메시지
            broadcastMessageLocked("SYSTEM:새로운 게임을 시작할 수 있습니다!");
            
            spdlog::info("✅ 게임 결과 처리 완료: 방 {}, 남은 플레이어 {}명", m_roomId, m_players.size());
        }

    } // namespace Server
} // namespace Blokus