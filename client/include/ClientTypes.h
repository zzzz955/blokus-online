#pragma once

// 🔥 Common 라이브러리의 모든 타입들을 가져옴
#include "Types.h"
#include "Block.h"
#include "GameLogic.h"
#include "Utils.h"

// Qt 관련 헤더들
#include <QString>
#include <QColor>
#include <QRect>
#include <QDateTime>

namespace Blokus {

    // ========================================
    // Common 네임스페이스의 타입들을 기본으로 가져오기 (명시적 별칭)
    // ========================================
    using PlayerColor = Common::PlayerColor;
    using BlockType = Common::BlockType;
    using Position = Common::Position;
    using PositionList = Common::PositionList;
    using Rotation = Common::Rotation;
    using FlipState = Common::FlipState;
    using GameState = Common::GameState;
    using TurnState = Common::TurnState;
    using BlockPlacement = Common::BlockPlacement;
    using GameSettings = Common::GameSettings;

    // 🔥 Block과 관련 클래스들도 가져오기 (서버와 동일한 로직)
    using Block = Common::Block;
    using GameLogic = Common::GameLogic;
    using GameStateManager = Common::GameStateManager;

    // ========================================
    // Qt 전용 확장 타입들 (Common에 없는 것들만)
    // ========================================

    // Qt 전용 ChatMessage (UI 전용)
    struct ChatMessage {
        QString username;
        QString message;
        QDateTime timestamp;
        enum Type { Normal, System, Whisper } type;

        ChatMessage()
            : username(QString::fromUtf8("시스템"))
            , message("")
            , timestamp(QDateTime::currentDateTime())
            , type(System) {
        }
    };

    // Qt 호환 UserInfo (Common::UserInfo를 Qt 문자열로 래핑)
    struct UserInfo {
        QString username;
        int level;
        int totalGames;
        int wins;
        int losses;
        int draws;
        int averageScore;
        int totalScore;      // 누적 점수
        int bestScore;       // 최고 점수
        bool isOnline;
        QString status;
        int experience;      // 현재 경험치
        int requiredExp;     // 다음 레벨까지 필요한 경험치
        int gamesPlayed;
        double winRate;

        // 기본 생성자
        UserInfo()
            : username(QString::fromUtf8("익명"))
            , level(1), totalGames(0), wins(0), losses(0), draws(0)
            , averageScore(0), totalScore(0), bestScore(0), isOnline(true)
            , status(QString::fromUtf8("로비"))
            , experience(0), requiredExp(100)
            , gamesPlayed(0), winRate(0.0) {
        }

        // Common::UserInfo에서 변환 (자동 변환)
        UserInfo(const Common::UserInfo& common)
            : username(QString::fromUtf8(common.username.c_str()))
            , level(common.level), totalGames(common.totalGames)
            , wins(common.wins), losses(common.losses), draws(0)
            , averageScore(common.averageScore), totalScore(0), bestScore(0), isOnline(common.isOnline)
            , status(QString::fromUtf8(common.status.c_str()))
            , experience(0), requiredExp(100)
            , gamesPlayed(common.totalGames), winRate(getWinRate()) {
        }

        // Common::UserInfo로 변환
        Common::UserInfo toCommon() const {
            Common::UserInfo common;
            common.username = username.toUtf8().toStdString();
            common.level = level;
            common.totalGames = totalGames;
            common.wins = wins;
            common.losses = losses;
            common.averageScore = averageScore;
            common.isOnline = isOnline;
            common.status = status.toUtf8().toStdString();
            return common;
        }

        // 기존 함수들 유지 (Common과 동일한 로직)
        double getWinRate() const {
            return totalGames > 0 ? static_cast<double>(wins) / totalGames * 100.0 : 0.0;
        }

        int calculateLevel() const {
            return (totalGames / 10) + 1;
        }
    };

    // Qt 호환 RoomInfo (Common::RoomInfo를 Qt 문자열로 래핑)
    struct RoomInfo {
        int roomId;
        QString roomName;
        QString hostName;
        int currentPlayers;
        int maxPlayers;
        bool isPrivate;
        bool isPlaying;
        QString gameMode;

        // 기본 생성자
        RoomInfo()
            : roomId(0), roomName(QString::fromUtf8("새 방"))
            , hostName(QString::fromUtf8("호스트")), currentPlayers(1)
            , maxPlayers(4), isPrivate(false), isPlaying(false)
            , gameMode(QString::fromUtf8("클래식")) {
        }

        // Common::RoomInfo에서 변환
        RoomInfo(const Common::RoomInfo& common)
            : roomId(common.roomId)
            , roomName(QString::fromUtf8(common.roomName.c_str()))
            , hostName(QString::fromUtf8(common.hostName.c_str()))
            , currentPlayers(common.currentPlayers)
            , maxPlayers(common.maxPlayers)
            , isPrivate(common.isPrivate)
            , isPlaying(common.isPlaying)
            , gameMode(QString::fromUtf8(common.gameMode.c_str())) {
        }

        // Common::RoomInfo로 변환
        Common::RoomInfo toCommon() const {
            Common::RoomInfo common;
            common.roomId = roomId;
            common.roomName = roomName.toUtf8().toStdString();
            common.hostName = hostName.toUtf8().toStdString();
            common.currentPlayers = currentPlayers;
            common.maxPlayers = maxPlayers;
            common.isPrivate = isPrivate;
            common.isPlaying = isPlaying;
            common.gameMode = gameMode.toUtf8().toStdString();
            return common;
        }
    };

    // ========================================
    // GameRoomInfo와 PlayerSlot도 Common 기반으로 수정
    // ========================================

    struct PlayerSlot {
        PlayerColor color;
        QString username;    // Qt 문자열 사용
        bool isHost;
        bool isReady;
        int score;
        int remainingBlocks;

        PlayerSlot()
            : color(PlayerColor::None), username("")
            , isHost(false), isReady(false), score(0)
            , remainingBlocks(Common::BLOCKS_PER_PLAYER) {
        }  // Common 상수 사용

// Common::PlayerSlot과 변환 (필요시)
        PlayerSlot(const Common::PlayerSlot& common)
            : color(common.color)
            , username(QString::fromUtf8(common.username.c_str()))
            , isHost(common.isHost), isReady(common.isReady)
            , score(common.score), remainingBlocks(common.remainingBlocks) {
        }

        // 기존 함수들 유지
        bool isEmpty() const { return username.isEmpty(); }

        QString getDisplayName() const {
            if (isEmpty()) return QString::fromUtf8("빈 슬롯");
            return username;
        }

        bool isActive() const { return !isEmpty(); }
    };

    struct GameRoomInfo {
        int roomId;
        QString roomName;
        QString hostUsername;
        PlayerColor hostColor;
        int maxPlayers;
        QString gameMode;
        bool isPlaying;
        std::array<PlayerSlot, 4> playerSlots;

        GameRoomInfo()
            : roomId(0), roomName(QString::fromUtf8("새 방"))
            , hostUsername(""), hostColor(PlayerColor::Blue)
            , maxPlayers(Common::MAX_PLAYERS)  // Common 상수 사용
            , gameMode(QString::fromUtf8("클래식")), isPlaying(false) {

            // Common에서 정의된 색상 순서 사용
            playerSlots[0].color = PlayerColor::Blue;
            playerSlots[1].color = PlayerColor::Yellow;
            playerSlots[2].color = PlayerColor::Red;
            playerSlots[3].color = PlayerColor::Green;
        }

        // Common::GameRoomInfo와 변환 (필요시 추가)

        // 기존 함수들 유지
        int getCurrentPlayerCount() const {
            int count = 0;
            for (const auto& slot : playerSlots) {
                if (!slot.isEmpty()) count++;
            }
            return count;
        }

        PlayerColor getMyColor(const QString& username) const {
            for (const auto& slot : playerSlots) {
                if (slot.username == username) return slot.color;
            }
            return PlayerColor::None;
        }

        bool isMyTurn(const QString& username, PlayerColor currentTurn) const {
            return getMyColor(username) == currentTurn;
        }
    };

    // ========================================
    // Utils 네임스페이스 (Qt 전용 함수들과 Common 함수들의 인라인 래퍼)
    // ========================================
    namespace Utils {
        // Qt-specific functions (구현은 ClientUtils.cpp에 있음)
        QString playerColorToString(PlayerColor color);
        QColor getPlayerColor(PlayerColor color);
        QString trim(const QString& str);
        bool isValidUsername(const QString& username);
        bool isValidRoomName(const QString& roomName);
        QString getBlockName(BlockType blockType);
        QString formatTurnTime(int seconds);
        bool isTurnTimeExpired(int remainingSeconds);
        
        // Common::Utils 함수들의 인라인 래퍼 (헤더에서 직접 구현)
        inline PlayerColor getNextPlayer(PlayerColor current) {
            return Common::Utils::getNextPlayer(current);
        }
        
        inline bool isPositionValid(const Position& pos, int boardSize = Common::BOARD_SIZE) {
            return Common::Utils::isPositionValid(pos, boardSize);
        }
        
        inline int manhattanDistance(const Position& a, const Position& b) {
            return Common::Utils::manhattanDistance(a, b);
        }
        
        inline bool isCornerAdjacent(const Position& pos1, const Position& pos2) {
            return Common::Utils::isCornerAdjacent(pos1, pos2);
        }
        
        inline bool isEdgeAdjacent(const Position& pos1, const Position& pos2) {
            return Common::Utils::isEdgeAdjacent(pos1, pos2);
        }
        
        inline int getBlockScore(BlockType blockType) {
            return Common::Utils::getBlockScore(blockType);
        }
    }

} // namespace Blokus