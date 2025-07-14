#include "GameRoom.h"
#include "Session.h"
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
		// 플레이어 관리
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
			auto it = std::find_if(m_players.begin(), m_players.end(),
				[&userId](const PlayerInfo& player) {
					return player.userId == userId;
				});

			if (it != m_players.end()) {
				spdlog::warn("❌ 방 {} 플레이어 추가 실패: 이미 참여한 플레이어 '{}'", m_roomId, userId);
				return false;
			}

			// 4. 새 플레이어 추가
			PlayerInfo newPlayer(userId, username, session);

			// 호스트 설정 (첫 번째 플레이어가 호스트)
			if (m_players.empty() || userId == m_hostId) {
				newPlayer.isHost = true;
				m_hostId = userId;
			}

			// 색상 자동 배정
			assignPlayerColor(newPlayer);

			m_players.push_back(std::move(newPlayer));
			updateActivity();

			spdlog::info("✅ 방 {} 플레이어 추가: '{}' (현재: {}/{})",
				m_roomId, username, m_players.size(), m_maxPlayers);

			// 브로드캐스트는 호출하는 쪽에서 처리 (데드락 방지)
			return true;
		}

		bool GameRoom::removePlayer(const std::string& userId) {
			std::lock_guard<std::mutex> lock(m_playersMutex);

			auto it = std::find_if(m_players.begin(), m_players.end(),
				[&userId](const PlayerInfo& player) {
					return player.userId == userId;
				});

			if (it == m_players.end()) {
				spdlog::warn("❌ 방 {} 플레이어 제거 실패: 플레이어 '{}' 없음", m_roomId, userId);
				return false;
			}

			std::string username = it->username;
			bool wasHost = it->isHost;

			m_players.erase(it);
			updateActivity();

			spdlog::info("✅ 방 {} 플레이어 제거: '{}' (남은: {}명)", m_roomId, username, m_players.size());

			// 브로드캐스트는 호출하는 쪽에서 처리 (데드락 방지)
			// broadcastPlayerLeft(username);

			// 호스트가 나간 경우 새 호스트 선정
			if (wasHost && !m_players.empty()) {
				autoSelectNewHost();
				// 호스트 변경 브로드캐스트도 호출하는 쪽에서 처리
			}

			// 방이 비었다면 게임 종료
			if (m_players.empty()) {
				spdlog::info("🏠 방 {} 모든 플레이어 퇴장으로 인한 게임 종료", m_roomId);
				m_state = RoomState::Disbanded;
				return true; // 방 매니저에서 이 방을 제거해야 함
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

			return std::any_of(m_players.begin(), m_players.end(),
				[&userId](const PlayerInfo& player) {
					return player.userId == userId;
				});
		}

		PlayerInfo* GameRoom::getPlayer(const std::string& userId) {
			std::lock_guard<std::mutex> lock(m_playersMutex);

			auto it = std::find_if(m_players.begin(), m_players.end(),
				[&userId](const PlayerInfo& player) {
					return player.userId == userId;
				});

			return (it != m_players.end()) ? &(*it) : nullptr;
		}

		const PlayerInfo* GameRoom::getPlayer(const std::string& userId) const {
			std::lock_guard<std::mutex> lock(m_playersMutex);

			auto it = std::find_if(m_players.begin(), m_players.end(),
				[&userId](const PlayerInfo& player) {
					return player.userId == userId;
				});

			return (it != m_players.end()) ? &(*it) : nullptr;
		}

		// ========================================
		// 플레이어 상태 관리
		// ========================================

		bool GameRoom::setPlayerReady(const std::string& userId, bool ready) {
			std::lock_guard<std::mutex> lock(m_playersMutex);

			auto it = std::find_if(m_players.begin(), m_players.end(),
				[&userId](const PlayerInfo& player) {
					return player.userId == userId;
				});

			if (it == m_players.end()) {
				return false;
			}

			// 호스트는 항상 준비 상태로 간주
			if (it->isHost) {
				it->isReady = true;
				spdlog::debug("🎮 방 {} 호스트 '{}' 준비 상태는 항상 true", m_roomId, it->username);
			}
			else {
				it->isReady = ready;
				spdlog::info("🎮 방 {} 플레이어 '{}' 준비 상태: {}",
					m_roomId, it->username, ready ? "준비됨" : "대기중");
			}

			updateActivity();
			broadcastPlayerReady(it->username, it->isReady);

			return true;
		}

		bool GameRoom::isPlayerReady(const std::string& userId) const {
			std::lock_guard<std::mutex> lock(m_playersMutex);

			auto it = std::find_if(m_players.begin(), m_players.end(),
				[&userId](const PlayerInfo& player) {
					return player.userId == userId;
				});

			return (it != m_players.end()) ? it->isReady : false;
		}

		bool GameRoom::setPlayerColor(const std::string& userId, Common::PlayerColor color) {
			std::lock_guard<std::mutex> lock(m_playersMutex);

			// 색상이 이미 사용 중인지 확인
			if (isColorTaken(color)) {
				return false;
			}

			auto it = std::find_if(m_players.begin(), m_players.end(),
				[&userId](const PlayerInfo& player) {
					return player.userId == userId;
				});

			if (it == m_players.end()) {
				return false;
			}

			it->color = color;
			updateActivity();

			return true;
		}

		// ========================================
		// 호스트 관리
		// ========================================

		bool GameRoom::isHost(const std::string& userId) const {
			return userId == m_hostId;
		}

		bool GameRoom::transferHost(const std::string& newHostId) {
			std::lock_guard<std::mutex> lock(m_playersMutex);

			// 새 호스트가 방에 있는지 확인
			auto newHostIt = std::find_if(m_players.begin(), m_players.end(),
				[&newHostId](const PlayerInfo& player) {
					return player.userId == newHostId;
				});

			if (newHostIt == m_players.end()) {
				return false;
			}

			// 기존 호스트 권한 제거
			auto oldHostIt = std::find_if(m_players.begin(), m_players.end(),
				[this](const PlayerInfo& player) {
					return player.userId == m_hostId;
				});

			if (oldHostIt != m_players.end()) {
				oldHostIt->isHost = false;
			}

			// 새 호스트 설정
			newHostIt->isHost = true;
			newHostIt->isReady = true; // 호스트는 항상 준비됨
			m_hostId = newHostId;
			updateActivity();

			spdlog::info("👑 방 {} 호스트 변경: '{}'", m_roomId, newHostIt->username);
			// 브로드캐스트는 호출하는 쪽에서 처리 (데드락 방지)
			// broadcastHostChanged(newHostIt->username);

			return true;
		}

		void GameRoom::autoSelectNewHost() {
			if (m_players.empty()) {
				return;
			}

			// 첫 번째 플레이어를 새 호스트로 선정
			m_players[0].isHost = true;
			m_players[0].isReady = true;
			m_hostId = m_players[0].userId;

			spdlog::info("👑 방 {} 자동 호스트 선정: '{}'", m_roomId, m_players[0].username);
			// 브로드캐스트는 호출하는 쪽에서 처리 (데드락 방지)
			// broadcastHostChanged(m_players[0].username);
		}

		// ========================================
		// 방 상태 정보
		// ========================================

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
			m_state = RoomState::Waiting;
			m_gameStartTime = std::chrono::steady_clock::now();
			updateActivity();

			// 게임 로직 초기화
			m_gameLogic->clearBoard();
			assignColorsAutomatically();

			// 게임 시작
			m_state = RoomState::Playing;

			spdlog::info("🎮 방 {} 게임 시작: {} 플레이어", m_roomId, m_players.size());
			// 브로드캐스트는 호출하는 쪽에서 처리 (데드락 방지)
			// broadcastGameStart();

			return true;
		}

		bool GameRoom::endGame() {
			std::lock_guard<std::mutex> lock(m_playersMutex);

			if (m_state != RoomState::Playing) {
				return false;
			}

			m_state = RoomState::Waiting;
			updateActivity();

			// 모든 플레이어 준비 상태 초기화
			resetPlayerStates();

			spdlog::info("🎮 방 {} 게임 종료", m_roomId);

			// 브로드캐스트는 호출하는 쪽에서 처리 (데드락 방지)
			// broadcastGameEnd();

			return true;
		}

		void GameRoom::resetGame() {
			std::lock_guard<std::mutex> lock(m_playersMutex);

			m_gameLogic->clearBoard();
			m_state = RoomState::Waiting;
			resetPlayerStates();
			updateActivity();

			spdlog::info("🔄 방 {} 게임 리셋", m_roomId);
			broadcastMessage("GAME_RESET");
		}

		// ========================================
		// 메시지 전송
		// ========================================

		void GameRoom::broadcastMessage(const std::string& message, const std::string& excludeUserId) {
			std::lock_guard<std::mutex> lock(m_playersMutex);

			for (const auto& player : m_players) {
				if (player.userId != excludeUserId && player.session) {
					try {
						player.session->sendMessage(message);
					}
					catch (const std::exception& e) {
						spdlog::error("❌ 방 {} 메시지 전송 실패 (플레이어: '{}'): {}",
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
					spdlog::error("❌ 방 {} 개별 메시지 전송 실패 (플레이어: '{}'): {}",
						m_roomId, it->username, e.what());
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
			auto hostIt = std::find_if(m_players.begin(), m_players.end(),
				[this](const PlayerInfo& player) {
					return player.userId == m_hostId;
				});

			if (hostIt != m_players.end()) {
				info.hostName = hostIt->username;
			}
			else {
				info.hostName = "알 수 없음";
			}

			return info;
		}

		std::vector<PlayerInfo> GameRoom::getPlayerList() const {
			std::lock_guard<std::mutex> lock(m_playersMutex);
			return m_players;
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
					return player.color == color;
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
				m_players[i].color = colors[i];
			}
		}

		// ========================================
		// 브로드캐스트 함수들 (public으로 변경됨 - 데드락 방지)
		// ========================================

		void GameRoom::broadcastPlayerJoined(const std::string& username) {
			std::ostringstream oss;
			oss << username << "님이 입장하셨습니다. 현재 인원 : " << getPlayerCount() << "명";
			broadcastMessage(oss.str());
		}

		void GameRoom::broadcastPlayerLeft(const std::string& username) {
			std::ostringstream oss;
			oss << username << "님이 퇴장하셨습니다. 현재 인원 : " << getPlayerCount() << "명";
			broadcastMessage(oss.str());
		}

		void GameRoom::broadcastPlayerReady(const std::string& username, bool ready) {
			std::ostringstream oss;
			oss << "PLAYER_READY:" << username << ":" << (ready ? "1" : "0");
			broadcastMessage(oss.str());
		}

		void GameRoom::broadcastHostChanged(const std::string& newHostName) {
			std::ostringstream oss;
			oss << newHostName << "님이 방장이 되셨습니다";
			broadcastMessage(oss.str());
		}

		void GameRoom::broadcastGameStart() {
			std::ostringstream oss;
			oss << "게임이 시작되었습니다. 현재 인원 : " << getPlayerCount() << "명";

			// 플레이어 정보도 함께 전송
			for (const auto& player : m_players) {
				oss << ":" << player.username << "," << static_cast<int>(player.color);
			}

			broadcastMessage(oss.str());
		}

		void GameRoom::broadcastGameEnd() {
			broadcastMessage("GAME_ENDED");
		}

		void GameRoom::broadcastGameState() {
			std::ostringstream oss;
			oss << "게임 종료";
			broadcastMessage(oss.str());
		}

		// ========================================
		// 내부 유틸리티 함수들 (브로드캐스트 제외)
		// ========================================
		void GameRoom::assignPlayerColor(PlayerInfo& player) {
			player.color = getNextAvailableColor();
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
				if (!player.isHost && !player.isReady && !player.isAI) {
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
				if (!it->session || !it->session->isConnected()) {
					spdlog::info("🧹 방 {} 연결 끊어진 플레이어 정리: '{}'", m_roomId, it->username);
					broadcastPlayerLeft(it->username);
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
				if (!player.isHost) {
					player.isReady = false;
				}
				player.score = 0;
				player.remainingBlocks = Common::BLOCKS_PER_PLAYER;
			}
		}

	} // namespace Server
} // namespace Blokus