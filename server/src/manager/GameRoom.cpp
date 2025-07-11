#include "manager/GameRoom.h"
#include "core/Session.h"
#include <spdlog/spdlog.h>
#include <algorithm>

namespace Blokus {
    namespace Server {

        // ========================================
        // GameRoom 클래스 구현
        // ========================================

        GameRoom::GameRoom(int roomId, const std::string& roomName, const std::string& hostId)
            : m_roomId(roomId)
            , m_roomName(roomName)
            , m_hostId(hostId)
            , m_state(RoomState::Waiting)
            , m_gameLogic(std::make_unique<Blokus::Common::GameLogic>())
            , m_lastActivity(std::chrono::steady_clock::now())
        {
            m_players.reserve(Blokus::Common::MAX_PLAYERS);
            spdlog::info("GameRoom 생성: ID={}, Name={}, Host={}", m_roomId, m_roomName, m_hostId);
        }

        GameRoom::~GameRoom() {
            spdlog::info("GameRoom 소멸: ID={}", m_roomId);
        }

        bool GameRoom::addPlayer(SessionPtr client, const std::string& userId, const std::string& username) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            // 방이 가득 찬지 확인
            if (m_players.size() >= Blokus::Common::MAX_PLAYERS) {
                spdlog::warn("방 {} 플레이어 추가 실패: 방이 가득참", m_roomId);
                return false;
            }

            // 이미 참여한 플레이어인지 확인
            auto it = std::find_if(m_players.begin(), m_players.end(),
                [&userId](const PlayerInfo& player) {
                    return player.userId == userId;
                });

            if (it != m_players.end()) {
                spdlog::warn("방 {} 플레이어 추가 실패: 이미 참여한 플레이어 {}", m_roomId, userId);
                return false;
            }

            // 게임 중인지 확인
            if (m_state == RoomState::Playing) {
                spdlog::warn("방 {} 플레이어 추가 실패: 게임 진행 중", m_roomId);
                return false;
            }

            // 새 플레이어 추가
            PlayerInfo newPlayer;
            newPlayer.userId = userId;
            newPlayer.username = username;
            newPlayer.session = client;
            newPlayer.isReady = false;
            newPlayer.isAI = false;

            // 색상 할당 (첫 번째 빈 색상 찾기)
            std::array<bool, 4> colorUsed = { false, false, false, false };
            for (const auto& player : m_players) {
                if (player.color != Blokus::Common::PlayerColor::None) {
                    int colorIndex = static_cast<int>(player.color) - 1;
                    if (colorIndex >= 0 && colorIndex < 4) {
                        colorUsed[colorIndex] = true;
                    }
                }
            }

            // 첫 번째 사용되지 않은 색상 할당
            for (int i = 0; i < 4; ++i) {
                if (!colorUsed[i]) {
                    newPlayer.color = static_cast<Blokus::Common::PlayerColor>(i + 1);
                    break;
                }
            }

            m_players.push_back(newPlayer);
            m_lastActivity = std::chrono::steady_clock::now();

            spdlog::info("방 {} 플레이어 추가: {} (색상: {})",
                m_roomId, username, static_cast<int>(newPlayer.color));

            // 방에 있는 모든 플레이어에게 알림
            broadcastPlayerJoined(username);

            return true;
        }

        bool GameRoom::removePlayer(const std::string& userId) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            auto it = std::find_if(m_players.begin(), m_players.end(),
                [&userId](const PlayerInfo& player) {
                    return player.userId == userId;
                });

            if (it == m_players.end()) {
                spdlog::warn("방 {} 플레이어 제거 실패: 플레이어 {} 없음", m_roomId, userId);
                return false;
            }

            std::string username = it->username;
            bool wasHost = (userId == m_hostId);

            m_players.erase(it);
            m_lastActivity = std::chrono::steady_clock::now();

            spdlog::info("방 {} 플레이어 제거: {}", m_roomId, username);

            // 호스트가 나갔다면 새 호스트 지정
            if (wasHost && !m_players.empty()) {
                m_hostId = m_players[0].userId;
                spdlog::info("방 {} 새 호스트: {}", m_roomId, m_players[0].username);
                broadcastNewHost(m_players[0].username);
            }

            // 방에 있는 모든 플레이어에게 알림
            broadcastPlayerLeft(username);

            // 게임 중이었다면 게임 종료
            if (m_state == RoomState::Playing) {
                endGame();
            }

            return true;
        }

        bool GameRoom::setPlayerReady(const std::string& userId, bool ready) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            auto it = std::find_if(m_players.begin(), m_players.end(),
                [&userId](PlayerInfo& player) {
                    return player.userId == userId;
                });

            if (it == m_players.end()) {
                return false;
            }

            it->isReady = ready;
            m_lastActivity = std::chrono::steady_clock::now();

            spdlog::info("방 {} 플레이어 {} 준비 상태: {}",
                m_roomId, it->username, ready ? "준비됨" : "대기중");

            // 준비 상태 변경 알림
            broadcastPlayerReady(it->username, ready);

            return true;
        }

        size_t GameRoom::getPlayerCount() const {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return m_players.size();
        }

        bool GameRoom::isFull() const {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return m_players.size() >= Blokus::Common::MAX_PLAYERS;
        }

        bool GameRoom::startGame() {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            // 최소 인원 확인
            if (m_players.size() < Blokus::Common::MIN_PLAYERS_TO_START) {
                spdlog::warn("방 {} 게임 시작 실패: 최소 인원 부족 ({}/{})",
                    m_roomId, m_players.size(), Blokus::Common::MIN_PLAYERS_TO_START);
                return false;
            }

            // 모든 플레이어가 준비되었는지 확인 (호스트 제외)
            for (const auto& player : m_players) {
                if (player.userId != m_hostId && !player.isReady && !player.isAI) {
                    spdlog::warn("방 {} 게임 시작 실패: 플레이어 {} 미준비", m_roomId, player.username);
                    return false;
                }
            }

            // 이미 게임이 진행 중인지 확인
            if (m_state == RoomState::Playing) {
                spdlog::warn("방 {} 게임 시작 실패: 이미 진행 중", m_roomId);
                return false;
            }

            // 게임 로직 초기화
            m_gameLogic->clearBoard();
            m_gameLogic->setCurrentPlayer(Blokus::Common::PlayerColor::Blue); // 첫 번째 플레이어부터 시작

            // 게임 상태 변경
            m_state = RoomState::Playing;
            m_gameStartTime = std::chrono::steady_clock::now();
            m_lastActivity = std::chrono::steady_clock::now();

            spdlog::info("방 {} 게임 시작: {} 플레이어", m_roomId, m_players.size());

            // 모든 플레이어에게 게임 시작 알림
            broadcastGameStart();

            return true;
        }

        bool GameRoom::endGame() {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            if (m_state != RoomState::Playing) {
                return false;
            }

            m_state = RoomState::Waiting;
            m_lastActivity = std::chrono::steady_clock::now();

            // 모든 플레이어 준비 상태 초기화
            for (auto& player : m_players) {
                player.isReady = false;
            }

            spdlog::info("방 {} 게임 종료", m_roomId);

            // 게임 종료 알림
            broadcastGameEnd();

            return true;
        }

        void GameRoom::resetGame() {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            m_gameLogic->clearBoard();
            m_gameLogic->setCurrentPlayer(Blokus::Common::PlayerColor::Blue);
            m_state = RoomState::Waiting;

            for (auto& player : m_players) {
                player.isReady = false;
            }

            spdlog::info("방 {} 게임 리셋", m_roomId);
        }

        void GameRoom::broadcastMessage(const std::string& message, const std::string& excludeUserId) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            for (const auto& player : m_players) {
                if (player.userId != excludeUserId && player.session) {
                    try {
                        player.session->sendMessage(message);
                    }
                    catch (const std::exception& e) {
                        spdlog::error("방 {} 메시지 전송 실패 (플레이어: {}): {}",
                            m_roomId, player.username, e.what());
                    }
                }
            }
        }

        void GameRoom::sendToPlayer(const std::string& userId, const std::string& message) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            auto it = std::find_if(m_players.begin(), m_players.end(),
                [&userId](const PlayerInfo& player) {
                    return player.userId == userId;
                });

            if (it != m_players.end() && it->session) {
                try {
                    it->session->sendMessage(message);
                }
                catch (const std::exception& e) {
                    spdlog::error("방 {} 개별 메시지 전송 실패 (플레이어: {}): {}",
                        m_roomId, it->username, e.what());
                }
            }
        }

        bool GameRoom::makeMove(const std::string& userId, const Blokus::Common::BlockPlacement& move) {
            std::lock_guard<std::mutex> lock(m_playersMutex);

            if (m_state != RoomState::Playing) {
                return false;
            }

            // 현재 턴인 플레이어 확인
            auto currentPlayer = getCurrentPlayer();
            auto it = std::find_if(m_players.begin(), m_players.end(),
                [&userId](const PlayerInfo& player) {
                    return player.userId == userId;
                });

            if (it == m_players.end() || it->color != currentPlayer) {
                return false;
            }

            // 게임 로직으로 이동 검증 및 적용
            if (!m_gameLogic->canPlaceBlock(move)) {
                return false;
            }

            if (!m_gameLogic->placeBlock(move)) {
                return false;
            }

            m_lastActivity = std::chrono::steady_clock::now();

            // 모든 플레이어에게 이동 알림
            broadcastMove(userId, move);

            // 게임 종료 확인
            if (m_gameLogic->isGameFinished()) {
                endGame();
            }
            else {
                // 다음 턴으로 - 간단한 턴 순환 구현
                auto currentColor = getCurrentPlayer();
                Blokus::Common::PlayerColor nextColor;

                switch (currentColor) {
                case Blokus::Common::PlayerColor::Blue:
                    nextColor = Blokus::Common::PlayerColor::Yellow;
                    break;
                case Blokus::Common::PlayerColor::Yellow:
                    nextColor = Blokus::Common::PlayerColor::Red;
                    break;
                case Blokus::Common::PlayerColor::Red:
                    nextColor = Blokus::Common::PlayerColor::Green;
                    break;
                case Blokus::Common::PlayerColor::Green:
                    nextColor = Blokus::Common::PlayerColor::Blue;
                    break;
                default:
                    nextColor = Blokus::Common::PlayerColor::Blue;
                    break;
                }

                m_gameLogic->setCurrentPlayer(nextColor);
                broadcastTurnChange();
            }

            return true;
        }

        Blokus::Common::PlayerColor GameRoom::getCurrentPlayer() const {
            return m_gameLogic->getCurrentPlayer();
        }

        bool GameRoom::isGameFinished() const {
            return m_gameLogic->isGameFinished();
        }

        bool GameRoom::canJoin() const {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return m_state == RoomState::Waiting && m_players.size() < Blokus::Common::MAX_PLAYERS;
        }

        bool GameRoom::isEmpty() const {
            std::lock_guard<std::mutex> lock(m_playersMutex);
            return m_players.empty();
        }

        // 브로드캐스트 헬퍼 함수들
        void GameRoom::broadcastPlayerJoined(const std::string& username) {
            broadcastMessage("PLAYER_JOINED:" + username);
        }

        void GameRoom::broadcastPlayerLeft(const std::string& username) {
            broadcastMessage("PLAYER_LEFT:" + username);
        }

        void GameRoom::broadcastPlayerReady(const std::string& username, bool ready) {
            broadcastMessage("PLAYER_READY:" + username + ":" + (ready ? "1" : "0"));
        }

        void GameRoom::broadcastNewHost(const std::string& username) {
            broadcastMessage("NEW_HOST:" + username);
        }

        void GameRoom::broadcastGameStart() {
            broadcastMessage("GAME_START");
        }

        void GameRoom::broadcastGameEnd() {
            broadcastMessage("GAME_END");
        }

        void GameRoom::broadcastMove(const std::string& userId, const Blokus::Common::BlockPlacement& move) {
            // 간단한 형태로 이동 정보 전송 (실제로는 protobuf 사용)
            std::string moveMsg = "PLAYER_MOVE:" + userId + ":" +
                std::to_string(move.position.first) + "," + std::to_string(move.position.second);
            broadcastMessage(moveMsg);
        }

        void GameRoom::broadcastTurnChange() {
            auto currentPlayer = getCurrentPlayer();
            broadcastMessage("TURN_CHANGE:" + std::to_string(static_cast<int>(currentPlayer)));
        }

    } // namespace Server
} // namespace Blokus