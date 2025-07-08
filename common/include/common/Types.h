#pragma once

#include <utility>
#include <vector>
#include <array>
#include <string>
#include <cstdint>
#include <chrono>

namespace Blokus {
    namespace Common {

        // ========================================
        // 기본 상수 정의
        // ========================================

        constexpr int BOARD_SIZE = 20;              // 클래식 모드 (고정)
        constexpr int MAX_PLAYERS = 4;              // 최대 플레이어 수
        constexpr int BLOCKS_PER_PLAYER = 21;       // 플레이어당 블록 수
        constexpr int DEFAULT_TURN_TIME = 30;       // 기본 턴 제한시간 (30초)

        // 서버 관련 상수
        constexpr int MAX_CONCURRENT_USERS = 1000;
        constexpr uint16_t DEFAULT_SERVER_PORT = 7777;

        // 게임 관련 상수
        constexpr int MIN_PLAYERS_TO_START = 2;
        constexpr int MAX_ROOM_NAME_LENGTH = 50;
        constexpr int MAX_USERNAME_LENGTH = 20;
        constexpr int MIN_USERNAME_LENGTH = 3;

        // ========================================
        // 기본 타입 정의
        // ========================================

        // 위치 타입 정의 (행, 열)
        using Position = std::pair<int, int>;

        // 위치 벡터 타입 (블록 모양 정의용)
        using PositionList = std::vector<Position>;

        // ========================================
        // 열거형 정의
        // ========================================

        // 플레이어 색상 열거형
        enum class PlayerColor : uint8_t {
            None = 0,   // 빈 칸
            Blue = 1,   // 파랑 (플레이어 1)
            Yellow = 2, // 노랑 (플레이어 2)  
            Red = 3,    // 빨강 (플레이어 3)
            Green = 4   // 초록 (플레이어 4)
        };

        // 블록 타입 (직관적이고 일관성 있는 명명)
        enum class BlockType : uint8_t {
            // 1칸 블록
            Single = 1,

            // 2칸 블록  
            Domino = 2,

            // 3칸 블록
            TrioLine = 3,       // 3일자
            TrioAngle = 4,      // 3꺾임

            // 4칸 블록 (테트로미노)
            Tetro_I = 5,        // 4일자
            Tetro_O = 6,        // 정사각형
            Tetro_T = 7,        // T자
            Tetro_L = 8,        // L자
            Tetro_S = 9,        // S자 (Z자)

            // 5칸 블록 (펜토미노)
            Pento_F = 10,       // F자
            Pento_I = 11,       // 5일자
            Pento_L = 12,       // 5L자
            Pento_N = 13,       // N자
            Pento_P = 14,       // P자
            Pento_T = 15,       // 5T자
            Pento_U = 16,       // U자
            Pento_V = 17,       // V자
            Pento_W = 18,       // W자
            Pento_X = 19,       // X자
            Pento_Y = 20,       // Y자
            Pento_Z = 21        // 5Z자
        };

        // 블록 회전 상태
        enum class Rotation : uint8_t {
            Degree_0 = 0,   // 0도
            Degree_90 = 1,  // 90도 시계방향
            Degree_180 = 2, // 180도
            Degree_270 = 3  // 270도 시계방향
        };

        // 블록 뒤집기 상태
        enum class FlipState : uint8_t {
            Normal = 0,     // 정상
            Horizontal = 1, // 수평 뒤집기
            Vertical = 2,   // 수직 뒤집기
            Both = 3        // 양쪽 뒤집기
        };

        // 게임 상태
        enum class GameState : uint8_t {
            Waiting,     // 대기 중
            Playing,     // 게임 중
            Finished,    // 게임 종료
            Paused       // 일시정지
        };

        // 턴 상태 (서버용)
        enum class TurnState : uint8_t {
            WaitingForMove,    // 이동 대기
            PlacingBlock,      // 블록 배치 중
            TurnComplete,      // 턴 완료
            Skipped           // 턴 건너뜀
        };

        // ========================================
        // 구조체 정의
        // ========================================

        // 블록 배치 정보
        struct BlockPlacement {
            BlockType type;             // 블록 타입
            Position position;          // 보드 위치 (행, 열)
            Rotation rotation;          // 회전 상태
            FlipState flip;             // 뒤집기 상태
            PlayerColor player;         // 소유 플레이어

            // 기본 생성자
            BlockPlacement()
                : type(BlockType::Single)
                , position(0, 0)
                , rotation(Rotation::Degree_0)
                , flip(FlipState::Normal)
                , player(PlayerColor::None)
            {
            }

            // 매개변수 생성자 (기본 값들)
            BlockPlacement(BlockType t, Position pos, PlayerColor p)
                : type(t)
                , position(pos)
                , rotation(Rotation::Degree_0)
                , flip(FlipState::Normal)
                , player(p)
            {
            }

            // 완전한 매개변수 생성자
            BlockPlacement(BlockType t, Position pos, Rotation rot, FlipState f, PlayerColor p)
                : type(t)
                , position(pos)
                , rotation(rot)
                , flip(f)
                , player(p)
            {
            }
        };

        // 게임 설정
        struct GameSettings {
            int turnTimeLimit;          // 턴 제한시간 (초)
            bool allowSpectators;       // 관전 허용
            std::string gameMode;       // "클래식", "듀얼" 등

            GameSettings()
                : turnTimeLimit(DEFAULT_TURN_TIME)
                , allowSpectators(true)
                , gameMode("클래식")
            {
            }
        };

        // ========================================
        // 사용자 정보 구조체
        // ========================================

        struct UserInfo {
            std::string username;       // 사용자명
            int level;                  // 경험치 레벨 (게임 수에 따라 증가)
            int totalGames;             // 총 게임 수
            int wins;                   // 승리 수
            int losses;                 // 패배 수
            int averageScore;           // 평균 점수
            bool isOnline;              // 온라인 상태
            std::string status;         // "로비", "게임중", "자리비움"

            UserInfo()
                : username("익명")
                , level(1)
                , totalGames(0)
                , wins(0)
                , losses(0)
                , averageScore(0)
                , isOnline(true)
                , status("로비")
            {
            }

            // 승률 계산
            double getWinRate() const {
                return totalGames > 0 ? static_cast<double>(wins) / totalGames * 100.0 : 0.0;
            }

            // 레벨 계산 (10게임당 1레벨)
            int calculateLevel() const {
                return (totalGames / 10) + 1;
            }
        };

        // 방 정보 구조체
        struct RoomInfo {
            int roomId;
            std::string roomName;
            std::string hostName;
            int currentPlayers;
            int maxPlayers;
            bool isPrivate;
            bool isPlaying;
            std::string gameMode;

            RoomInfo()
                : roomId(0)
                , roomName("새 방")
                , hostName("호스트")
                , currentPlayers(1)
                , maxPlayers(4)
                , isPrivate(false)
                , isPlaying(false)
                , gameMode("클래식")
            {
            }
        };

        // 플레이어 슬롯 (게임 룸용)
        struct PlayerSlot {
            PlayerColor color;          // 플레이어 색상
            std::string username;       // 플레이어 이름
            bool isAI;                  // AI 플레이어 여부
            int aiDifficulty;           // AI 난이도 (1-3)
            bool isHost;                // 호스트 여부
            bool isReady;               // 준비 상태
            int score;                  // 현재 점수
            int remainingBlocks;        // 남은 블록 수

            PlayerSlot()
                : color(PlayerColor::None)
                , username("")
                , isAI(false)
                , aiDifficulty(2)
                , isHost(false)
                , isReady(false)
                , score(0)
                , remainingBlocks(BLOCKS_PER_PLAYER)
            {
            }
        };

        // ========================================
        // 서버용 확장 구조체
        // ========================================

        // 게임 세션 관리용 (서버 전용)
        struct GameSession {
            int roomId = 0;
            GameState state = GameState::Waiting;
            std::array<PlayerSlot, MAX_PLAYERS> players;
            int currentPlayerIndex = 0;
            int turnNumber = 1;
            std::chrono::system_clock::time_point startTime;
            std::chrono::system_clock::time_point lastMoveTime;

            // 게임 설정
            GameSettings settings;

            GameSession() = default;
            explicit GameSession(int roomId_) : roomId(roomId_) {}

            // 유틸리티 함수
            PlayerColor getCurrentPlayerColor() const {
                if (currentPlayerIndex >= 0 && currentPlayerIndex < MAX_PLAYERS) {
                    return players[currentPlayerIndex].color;
                }
                return PlayerColor::None;
            }

            bool isPlayerTurn(PlayerColor color) const {
                return getCurrentPlayerColor() == color;
            }

            void nextTurn() {
                currentPlayerIndex = (currentPlayerIndex + 1) % MAX_PLAYERS;
                if (currentPlayerIndex == 0) {
                    turnNumber++;
                }
            }

            bool canStartGame() const {
                int activePlayers = 0;
                for (const auto& slot : players) {
                    if (slot.color != PlayerColor::None && !slot.username.empty()) {
                        activePlayers++;
                    }
                }
                return activePlayers >= MIN_PLAYERS_TO_START;
            }
        };

        // ========================================
        // 유틸리티 함수들
        // ========================================

        // 문자열 변환 함수들
        inline std::string playerColorToString(PlayerColor color) {
            switch (color) {
            case PlayerColor::Blue: return "Blue";
            case PlayerColor::Yellow: return "Yellow";
            case PlayerColor::Red: return "Red";
            case PlayerColor::Green: return "Green";
            case PlayerColor::None: return "None";
            default: return "Unknown";
            }
        }

        inline std::string gameStateToString(GameState state) {
            switch (state) {
            case GameState::Waiting: return "Waiting";
            case GameState::Playing: return "Playing";
            case GameState::Finished: return "Finished";
            case GameState::Paused: return "Paused";
            default: return "Unknown";
            }
        }

        inline PlayerColor stringToPlayerColor(const std::string& str) {
            if (str == "Blue") return PlayerColor::Blue;
            if (str == "Yellow") return PlayerColor::Yellow;
            if (str == "Red") return PlayerColor::Red;
            if (str == "Green") return PlayerColor::Green;
            return PlayerColor::None;
        }

        inline GameState stringToGameState(const std::string& str) {
            if (str == "Waiting") return GameState::Waiting;
            if (str == "Playing") return GameState::Playing;
            if (str == "Finished") return GameState::Finished;
            if (str == "Paused") return GameState::Paused;
            return GameState::Waiting;
        }

        // 검증 함수들
        inline bool isValidUsername(const std::string& username) {
            return username.length() >= MIN_USERNAME_LENGTH &&
                username.length() <= MAX_USERNAME_LENGTH &&
                !username.empty();
        }

        inline bool isValidRoomName(const std::string& roomName) {
            return !roomName.empty() && roomName.length() <= MAX_ROOM_NAME_LENGTH;
        }

        inline bool isValidPosition(const Position& pos) {
            return pos.first >= 0 && pos.first < BOARD_SIZE &&
                pos.second >= 0 && pos.second < BOARD_SIZE;
        }

    } // namespace Common
} // namespace Blokus