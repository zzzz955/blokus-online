#pragma once

// 🔥 Common 라이브러리의 모든 타입들을 가져옴
#include "common/Types.h"
#include "common/Block.h"
#include "common/GameLogic.h"
#include "common/Utils.h"

// Qt 관련 헤더들
#include <QString>
#include <QColor>
#include <QRect>
#include <QDateTime>

namespace Blokus {

    // ========================================
    // Common 네임스페이스의 타입들을 기본으로 가져오기
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
    //using BlockFactory = Common::BlockFactory;
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
        int averageScore;
        bool isOnline;
        QString status;

        // 기본 생성자
        UserInfo()
            : username(QString::fromUtf8("익명"))
            , level(1), totalGames(0), wins(0), losses(0)
            , averageScore(0), isOnline(true)
            , status(QString::fromUtf8("로비")) {
        }

        // Common::UserInfo에서 변환 (자동 변환)
        UserInfo(const Common::UserInfo& common)
            : username(QString::fromUtf8(common.username.c_str()))
            , level(common.level), totalGames(common.totalGames)
            , wins(common.wins), losses(common.losses)
            , averageScore(common.averageScore), isOnline(common.isOnline)
            , status(QString::fromUtf8(common.status.c_str())) {
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
        bool isAI;
        int aiDifficulty;
        bool isHost;
        bool isReady;
        int score;
        int remainingBlocks;

        PlayerSlot()
            : color(PlayerColor::None), username("")
            , isAI(false), aiDifficulty(2), isHost(false)
            , isReady(false), score(0)
            , remainingBlocks(Common::BLOCKS_PER_PLAYER) {
        }  // Common 상수 사용

// Common::PlayerSlot과 변환 (필요시)
        PlayerSlot(const Common::PlayerSlot& common)
            : color(common.color)
            , username(QString::fromUtf8(common.username.c_str()))
            , isAI(common.isAI), aiDifficulty(common.aiDifficulty)
            , isHost(common.isHost), isReady(common.isReady)
            , score(common.score), remainingBlocks(common.remainingBlocks) {
        }

        // 기존 함수들 유지
        bool isEmpty() const { return username.isEmpty() && !isAI; }

        QString getDisplayName() const {
            if (isEmpty()) return QString::fromUtf8("빈 슬롯");
            if (isAI) return QString::fromUtf8("AI (레벨 %1)").arg(aiDifficulty);
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
    // Utils 네임스페이스 (Common::Utils를 Qt로 래핑)
    // ========================================
    namespace Utils {
        QString playerColorToString(PlayerColor color);
        QColor getPlayerColor(PlayerColor color);
        PlayerColor getNextPlayer(PlayerColor current);
        bool isPositionValid(const Position& pos, int boardSize = Common::BOARD_SIZE);
        int manhattanDistance(const Position& a, const Position& b);
        bool isCornerAdjacent(const Position& pos1, const Position& pos2);
        bool isEdgeAdjacent(const Position& pos1, const Position& pos2);
        QString trim(const QString& str);
        bool isValidUsername(const QString& username);
        bool isValidRoomName(const QString& roomName);
        int getBlockScore(BlockType blockType);
        QString getBlockName(BlockType blockType);
        QString formatTurnTime(int seconds);
        bool isTurnTimeExpired(int remainingSeconds);
    }

} // namespace Blokus