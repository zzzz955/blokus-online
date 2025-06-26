#pragma once

#include <utility>
#include <vector>
#include <QString>
#include <QPoint>

namespace Blokus {

    // 전역 상수
    constexpr int BOARD_SIZE = 20;          // 20x20 게임 보드
    constexpr int MAX_PLAYERS = 4;          // 최대 플레이어 수
    constexpr int BLOCKS_PER_PLAYER = 21;   // 플레이어당 블록 수

    // 위치 타입 정의 (행, 열)
    using Position = std::pair<int, int>;

    // 위치 벡터 타입 (블록 모양 정의용)
    using PositionList = std::vector<Position>;

    // 플레이어 색상 열거형
    enum class PlayerColor {
        None = 0,   // 빈 칸
        Blue = 1,   // 파랑 (플레이어 1)
        Yellow = 2, // 노랑 (플레이어 2)  
        Red = 3,    // 빨강 (플레이어 3)
        Green = 4   // 초록 (플레이어 4)
    };

    // 블록 회전 상태
    enum class Rotation {
        Degree_0 = 0,   // 0도
        Degree_90 = 1,  // 90도 시계방향
        Degree_180 = 2, // 180도
        Degree_270 = 3  // 270도 시계방향
    };

    // 블록 뒤집기 상태  
    enum class FlipState {
        Normal = 0,     // 정상
        Horizontal = 1, // 수평 뒤집기
        Vertical = 2,   // 수직 뒤집기
        Both = 3        // 양쪽 뒤집기
    };

    // 게임 상태
    enum class GameState {
        Waiting,     // 대기 중
        Playing,     // 게임 중
        Finished,    // 게임 종료
        Paused       // 일시정지
    };

    // 턴 상태
    enum class TurnState {
        Waiting,     // 대기 중
        Thinking,    // 생각 중
        Placing,     // 블록 배치 중
        Confirming,  // 확인 중
        Finished     // 턴 완료
    };

    // 블록 타입 (폴리오미노 종류)
    enum class BlockType {
        // 1칸 블록
        Single = 0,         // ■

        // 2칸 블록  
        Domino = 1,         // ■■

        // 3칸 블록
        TrioLine = 2,       // ■■■
        TrioAngle = 3,      // ■■
        //  ■

// 4칸 블록
Tetro_I = 4,        // ■■■■
Tetro_O = 5,        // ■■
// ■■
Tetro_T = 6,        // ■■■
//  ■
Tetro_L = 7,        // ■■■
// ■
Tetro_S = 8,        // ■■
//  ■■

// 5칸 블록 (총 12개)
Pento_F = 9,        //  ■■
// ■■
//  ■
Pento_I = 10,       // ■■■■■
Pento_L = 11,       // ■■■■
// ■
Pento_N = 12,       // ■■■
//   ■■
Pento_P = 13,       // ■■
// ■■
// ■
Pento_T = 14,       // ■■■
//  ■
//  ■
Pento_U = 15,       // ■ ■
// ■■■
Pento_V = 16,       // ■
// ■
// ■■■
Pento_W = 17,       // ■
// ■■
//  ■■
Pento_X = 18,       //  ■
// ■■■
//  ■
Pento_Y = 19,       // ■■■■
//  ■
Pento_Z = 20        // ■■
//  ■
//  ■■
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

    // 플레이어 정보 구조체
    struct PlayerInfo {
        PlayerColor color;          // 플레이어 색상
        QString name;               // 플레이어 이름
        int score;                  // 현재 점수
        int remainingBlocks;        // 남은 블록 수
        bool isAI;                  // AI 플레이어 여부
        bool isActive;              // 활성 상태

        PlayerInfo()
            : color(PlayerColor::None)
            , name(QString::fromUtf8("플레이어"))
            , score(0)
            , remainingBlocks(BLOCKS_PER_PLAYER)
            , isAI(false)
            , isActive(true)
        {
        }
    };

    // 게임 설정 구조체
    struct GameSettings {
        int playerCount;            // 플레이어 수 (2-4)
        int timeLimit;              // 턴 제한시간 (초, 0=무제한)
        bool enableAI;              // AI 플레이어 허용
        int aiDifficulty;           // AI 난이도 (1-3)
        bool showHints;             // 힌트 표시 여부

        GameSettings()
            : playerCount(4)
            , timeLimit(0)
            , enableAI(true)
            , aiDifficulty(2)
            , showHints(true)
        {
        }
    };

    // 유틸리티 함수들
    namespace Utils {

        // 위치 유효성 검사
        inline bool isPositionValid(const Position& pos) {
            return pos.first >= 0 && pos.first < BOARD_SIZE &&
                pos.second >= 0 && pos.second < BOARD_SIZE;
        }

        // 두 위치 사이의 거리 계산 (맨하탄 거리)
        inline int manhattanDistance(const Position& a, const Position& b) {
            return std::abs(a.first - b.first) + std::abs(a.second - b.second);
        }

        // PlayerColor를 문자열로 변환
        inline QString playerColorToString(PlayerColor color) {
            switch (color) {
            case PlayerColor::Blue: return QString::fromUtf8("파랑");
            case PlayerColor::Yellow: return QString::fromUtf8("노랑");
            case PlayerColor::Red: return QString::fromUtf8("빨강");
            case PlayerColor::Green: return QString::fromUtf8("초록");
            default: return QString::fromUtf8("없음");
            }
        }

        // 다음 플레이어 색상 반환
        inline PlayerColor getNextPlayer(PlayerColor current) {
            switch (current) {
            case PlayerColor::Blue: return PlayerColor::Yellow;
            case PlayerColor::Yellow: return PlayerColor::Red;
            case PlayerColor::Red: return PlayerColor::Green;
            case PlayerColor::Green: return PlayerColor::Blue;
            default: return PlayerColor::Blue;
            }
        }
    } // namespace Utils

} // namespace Blokus