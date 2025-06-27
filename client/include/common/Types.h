#pragma once

#include <utility>
#include <vector>
#include <QString>
#include <QPoint>

namespace Blokus {

    // 전역 상수
    constexpr int BOARD_SIZE = 20;          // 클래식 모드
    constexpr int DUO_BOARD_SIZE = 14;      // 🆕 듀오 모드 보드 크기
    constexpr int MAX_PLAYERS = 4;          // 최대 플레이어 수
    constexpr int BLOCKS_PER_PLAYER = 21;   // 플레이어당 블록 수
    constexpr int DEFAULT_TURN_TIME = 30;   // 🔥 기본 턴 제한시간 (30초)

    // 보드 크기 결정 함수
    inline int getBoardSize(bool isDuoMode) {
        return isDuoMode ? DUO_BOARD_SIZE : BOARD_SIZE;
    }

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
        Single = 0,

        // 2칸 블록  
        Domino = 1,

        // 3칸 블록
        TrioLine = 2,
        TrioAngle = 3,

        // 4칸 블록
        Tetro_I=4,
        Tetro_O=5,
        Tetro_T=6,
        Tetro_L=7,
        Tetro_S=8,

        // 5칸 블록 (총 12개)
        Pento_F=9,
        Pento_I=10,
        Pento_L=11,
        Pento_N=12,
        Pento_P=13,
        Pento_T=14,
        Pento_U=15,
        Pento_V=16,
        Pento_W=17,
        Pento_X=18,
        Pento_Y=19,
        Pento_Z=20
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
        QString username;           // 사용자명
        int level;                  // 경험치 레벨 (게임 수에 따라 증가)
        int totalGames;             // 총 게임 수
        int wins;                   // 승리 수
        int losses;                 // 패배 수
        int averageScore;           // 평균 점수
        bool isOnline;              // 온라인 상태
        QString status;             // "로비", "게임중", "자리비움"

        UserInfo()
            : username(QString::fromUtf8("익명"))
            , level(1)
            , totalGames(0)
            , wins(0)
            , losses(0)
            , averageScore(0)
            , isOnline(true)
            , status(QString::fromUtf8("로비"))
        {
        }

        // 승률 계산
        double getWinRate() const {
            return totalGames > 0 ? (double)wins / totalGames * 100.0 : 0.0;
        }

        // 레벨 계산 (10게임당 1레벨)
        int calculateLevel() const {
            return (totalGames / 10) + 1;
        }
    };

    struct RoomInfo {
        int roomId;
        QString roomName;
        QString hostName;
        int currentPlayers;
        int maxPlayers;
        bool isPrivate;
        bool isPlaying;
        QString gameMode;

        RoomInfo()
            : roomId(0)
            , roomName(QString::fromUtf8("새 방"))
            , hostName(QString::fromUtf8("호스트"))
            , currentPlayers(1)
            , maxPlayers(4)
            , isPrivate(false)
            , isPlaying(false)
            , gameMode(QString::fromUtf8("클래식"))
        {
        }
    };

    // 플레이어 슬롯 (게임 룸용) - 기존 PlayerInfo를 대체
    struct PlayerSlot {
        PlayerColor color;          // 플레이어 색상
        QString username;           // 플레이어 이름 (name → username 변경)
        bool isAI;                  // AI 플레이어 여부
        int aiDifficulty;           // AI 난이도 (1-3)
        bool isHost;                // 🆕 호스트 여부
        bool isReady;               // 🆕 준비 상태
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
            return username.isEmpty() && !isAI;
        }

        QString getDisplayName() const {
            if (isEmpty()) {
                return QString::fromUtf8("빈 슬롯");
            }
            else if (isAI) {
                return QString::fromUtf8("AI (레벨 %1)").arg(aiDifficulty);
            }
            else {
                return username;
            }
        }

        // 🔄 기존 PlayerInfo의 isActive 대신 isEmpty() 사용
        bool isActive() const {
            return !isEmpty();
        }
    };

    // 게임 룸 정보 (게임 룸용)
    struct GameRoomInfo {
        int roomId;
        QString roomName;
        QString hostUsername;
        PlayerColor hostColor;
        int maxPlayers;
        QString gameMode;
        bool isPlaying;
        QList<PlayerSlot> playerSlots;  // PlayerInfo[] → PlayerSlot[] 변경

        GameRoomInfo()
            : roomId(0)
            , roomName(QString::fromUtf8("새 방"))
            , hostUsername("")
            , hostColor(PlayerColor::Blue)
            , maxPlayers(4)
            , gameMode(QString::fromUtf8("클래식 (4인, 20x20)"))
            , isPlaying(false)
        {
            // 4개 색상 슬롯 초기화
            for (int i = 0; i < 4; ++i)
                playerSlots.append(PlayerSlot());

            playerSlots[0].color = PlayerColor::Blue;
            playerSlots[1].color = PlayerColor::Yellow;
            playerSlots[2].color = PlayerColor::Red;
            playerSlots[3].color = PlayerColor::Green;
        }

        bool isDuoMode() const {
            return gameMode.contains(QString::fromUtf8("듀오")) || maxPlayers == 2;
        }

        int getCurrentPlayerCount() const {
            int count = 0;
            int slotsToCheck = isDuoMode() ? 2 : 4;

            for (int i = 0; i < slotsToCheck && i < playerSlots.size(); ++i) {
                if (!playerSlots[i].isEmpty()) count++;
            }
            return count;
        }

        PlayerColor getMyColor(const QString& username) const {
            for (const auto& slot : playerSlots) {
                if (slot.username == username) {
                    return slot.color;
                }
            }
            return PlayerColor::None;
        }

        bool isMyTurn(const QString& username, PlayerColor currentTurn) const {
            return getMyColor(username) == currentTurn;
        }

        QList<PlayerColor> getAvailableColors() const {
            if (isDuoMode()) {
                return { PlayerColor::Blue, PlayerColor::Yellow };
            }
            else {
                return { PlayerColor::Blue, PlayerColor::Yellow,
                        PlayerColor::Red, PlayerColor::Green };
            }
        }
    };

    // 게임 설정 구조체
    struct GameSettings {
        int playerCount;            // 플레이어 수 (2-4)
        int turnTimeLimit;          // 🔥 턴 제한시간 (초, 기본 30초)
        bool enableAI;              // AI 플레이어 허용
        int aiDifficulty;           // AI 난이도 (1-3)
        bool showHints;             // 힌트 표시 여부
        QString gameMode;           // 게임 모드 ("classic", "duo")
        bool recordStats;           // 통계 기록 여부 (기본 true)

        GameSettings()
            : playerCount(4)
            , turnTimeLimit(DEFAULT_TURN_TIME)  // 🔥 30초 기본값
            , enableAI(true)
            , aiDifficulty(2)
            , showHints(true)
            , gameMode("classic")
            , recordStats(true)         // 🔥 통계는 기록, 레이팅은 안함
        {
        }
    };

    // 유틸리티 함수들
    namespace Utils {

        // 위치 유효성 검사
        inline bool isPositionValid(const Position& pos, bool isDuoMode = false) {
            int boardSize = getBoardSize(isDuoMode);
            return pos.first >= 0 && pos.first < boardSize &&
                pos.second >= 0 && pos.second < boardSize;
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

        // 🔥 턴 시간 포맷팅 (30초 → "0:30")
        inline QString formatTurnTime(int seconds) {
            int minutes = seconds / 60;
            int remainingSeconds = seconds % 60;
            return QString("%1:%2").arg(minutes).arg(remainingSeconds, 2, 10, QChar('0'));
        }

        // 🔥 시간 초과 여부 확인
        inline bool isTurnTimeExpired(int remainingSeconds) {
            return remainingSeconds <= 0;
        }

    } // namespace Utils

} // namespace Blokus