﻿#pragma once

#include <utility>
#include <vector>
#include <array>
#include <string>
#include <cstdint>

namespace Blokus {
    namespace Common {
        // 전역 상수
        constexpr int BOARD_SIZE = 20;              // 클래식 모드 (고정)
        constexpr int MAX_PLAYERS = 4;              // 최대 플레이어 수
        constexpr int BLOCKS_PER_PLAYER = 21;       // 플레이어당 블록 수
        constexpr int DEFAULT_TURN_TIME = 30;       // 기본 턴 제한시간 (30초)

        // 위치 타입 정의 (행, 열)
        using Position = std::pair<int, int>;

        // 위치 벡터 타입 (블록 모양 정의용)
        using PositionList = std::vector<Position>;

        // 플레이어 색상 열거형
        enum class PlayerColor : uint8_t {
            None = 0,   // 빈 칸
            Blue = 1,   // 파랑 (플레이어 1)
            Yellow = 2, // 노랑 (플레이어 2)  
            Red = 3,    // 빨강 (플레이어 3)
            Green = 4   // 초록 (플레이어 4)
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

        // 턴 상태
        enum class TurnState : uint8_t {
            Waiting,     // 대기 중
            Thinking,    // 생각 중
            Placing,     // 블록 배치 중
            Confirming,  // 확인 중
            Finished     // 턴 완료
        };

        // 블록 타입 (폴리오미노 종류)
        enum class BlockType : uint8_t {
            // 1칸 블록
            Single = 0,

            // 2칸 블록  
            Domino = 1,

            // 3칸 블록
            TrioLine = 2,
            TrioAngle = 3,

            // 4칸 블록
            Tetro_I = 4,
            Tetro_O = 5,
            Tetro_T = 6,
            Tetro_L = 7,
            Tetro_S = 8,

            // 5칸 블록 (총 12개)
            Pento_F = 9,
            Pento_I = 10,
            Pento_L = 11,
            Pento_N = 12,
            Pento_P = 13,
            Pento_T = 14,
            Pento_U = 15,
            Pento_V = 16,
            Pento_W = 17,
            Pento_X = 18,
            Pento_Y = 19,
            Pento_Z = 20
        };

        // 블록 배치 정보 구조체
        struct BlockPlacement {
            BlockType type;         // 블록 타입
            Position position;      // 배치 위치 (기준점)
            Rotation rotation;      // 회전 상태
            FlipState flip;         // 뒤집기 상태
            PlayerColor player;     // 소유 플레이어

            // 기본 생성자
            BlockPlacement()
                : type(BlockType::Single)
                , position({ 0, 0 })
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

        // 사용자 정보 구조체  
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

            bool isEmpty() const {
                return username.empty() && !isAI;
            }

            std::string getDisplayName() const {
                if (isEmpty()) {
                    return "빈 슬롯";
                }
                else if (isAI) {
                    return "AI (레벨 " + std::to_string(aiDifficulty) + ")";
                }
                else {
                    return username;
                }
            }

            bool isActive() const {
                return !isEmpty();
            }
        };

        // 게임 룸 정보 (게임 룸용)
        struct GameRoomInfo {
            int roomId;
            std::string roomName;
            std::string hostUsername;
            PlayerColor hostColor;
            int maxPlayers;
            std::string gameMode;
            bool isPlaying;
            std::array<PlayerSlot, 4> playerSlots;

            GameRoomInfo()
                : roomId(0)
                , roomName("새 방")
                , hostUsername("")
                , hostColor(PlayerColor::Blue)
                , maxPlayers(4)
                , gameMode("클래식")
                , isPlaying(false)
            {
                // 4개 색상 슬롯 초기화
                playerSlots[0].color = PlayerColor::Blue;
                playerSlots[1].color = PlayerColor::Yellow;
                playerSlots[2].color = PlayerColor::Red;
                playerSlots[3].color = PlayerColor::Green;
            }

            int getCurrentPlayerCount() const {
                int count = 0;
                for (const auto& slot : playerSlots) {
                    if (!slot.isEmpty()) count++;
                }
                return count;
            }

            PlayerColor getMyColor(const std::string& username) const {
                for (const auto& slot : playerSlots) {
                    if (slot.username == username) {
                        return slot.color;
                    }
                }
                return PlayerColor::None;
            }

            bool isMyTurn(const std::string& username, PlayerColor currentTurn) const {
                return getMyColor(username) == currentTurn;
            }
        };

        // 게임 설정 구조체
        struct GameSettings {
            int playerCount;            // 플레이어 수 (2-4)
            int turnTimeLimit;          // 턴 제한시간 (초, 기본 30초)
            bool enableAI;              // AI 플레이어 허용
            int aiDifficulty;           // AI 난이도 (1-3)
            bool showHints;             // 힌트 표시 여부
            bool recordStats;           // 통계 기록 여부

            GameSettings()
                : playerCount(4)
                , turnTimeLimit(DEFAULT_TURN_TIME)
                , enableAI(true)
                , aiDifficulty(2)
                , showHints(true)
                , recordStats(true)
            {
            }
        };

    } // namespace Common
} // namespace Blokus