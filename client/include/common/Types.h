#pragma once

// ========================================
// Common 라이브러리 import
// ========================================
#include "../../../common/include/common/Types.h"        // 서버와 공유하는 Common 타입들
#include "../../../common/include/common/Utils.h"        // 서버와 공유하는 Common 유틸리티
#include "QtAdapter.h"                         // Qt 변환 어댑터

// ========================================
// Qt 헤더들
// ========================================
#include <QString>
#include <QList>
#include <QColor>
#include <QPoint>
#include <QDateTime>

namespace Blokus {

    // ========================================
    // Common 라이브러리 타입들을 기본 네임스페이스로 가져오기
    // ========================================

    // 기본 타입들 (기존 코드와 호환)
    using Common::BOARD_SIZE;
    using Common::MAX_PLAYERS;
    using Common::BLOCKS_PER_PLAYER;
    using Common::DEFAULT_TURN_TIME;

    using Common::Position;
    using Common::PositionList;
    using Common::PlayerColor;
    using Common::Rotation;
    using Common::FlipState;
    using Common::GameState;
    using Common::TurnState;
    using Common::BlockType;
    using Common::BlockPlacement;
    using Common::GameSettings;

    // ========================================
    // Qt 어댑터 타입들로 교체 (기존 코드 호환성 유지)
    // ========================================

    // 기존 코드에서 사용하던 타입명을 그대로 유지하되, 내부적으로는 Qt 어댑터 사용
    using UserInfo = QtAdapter::QtUserInfo;
    using RoomInfo = QtAdapter::QtRoomInfo;
    using PlayerSlot = QtAdapter::QtPlayerSlot;
    using GameRoomInfo = QtAdapter::QtGameRoomInfo;

    // ========================================
    // Qt 전용 구조체들 (기존 유지)
    // ========================================

    // ChatMessage는 Qt 전용이므로 그대로 유지
    struct ChatMessage {
        QString username;
        QString message;
        QDateTime timestamp;
        enum Type { Normal, System, Whisper } type;

        ChatMessage()
            : username(QString::fromUtf8("시스템"))
            , message("")
            , timestamp(QDateTime::currentDateTime())
            , type(System)
        {
        }
    };

    // ========================================
    // 유틸리티 함수들 (기존 코드 호환성 유지)
    // ========================================

    namespace Utils {

        // 기존 코드에서 사용하던 함수명들을 그대로 유지
        inline QString playerColorToString(PlayerColor color) {
            return QtAdapter::Utils::playerColorToString(color);
        }

        // 함수 오버로드 제거 - QColor 반환 함수는 다른 이름 사용
        inline QColor getPlayerColor(PlayerColor color) {
            return QtAdapter::Utils::playerColorToQColor(color);
        }

        inline PlayerColor getNextPlayer(PlayerColor current) {
            return Common::Utils::getNextPlayer(current);
        }

        inline bool isPositionValid(const Position& pos) {
            return Common::Utils::isPositionValid(pos);
        }

        inline int manhattanDistance(const Position& a, const Position& b) {
            return Common::Utils::manhattanDistance(a, b);
        }

        inline QString formatTurnTime(int seconds) {
            return QtAdapter::Utils::formatTurnTime(seconds);
        }

        inline bool isTurnTimeExpired(int remainingSeconds) {
            return Common::Utils::isTurnTimeExpired(remainingSeconds);
        }

        inline QString getBlockName(BlockType blockType) {
            return QtAdapter::Utils::getBlockName(blockType);
        }

        inline QString getBlockDescription(BlockType blockType) {
            return QtAdapter::Utils::getBlockDescription(blockType);
        }

        inline QPoint positionToQPoint(const Position& pos) {
            return QtAdapter::Utils::positionToQPoint(pos);
        }

        inline Position qPointToPosition(const QPoint& point) {
            return QtAdapter::Utils::qPointToPosition(point);
        }

    } // namespace Utils

    // ========================================
    // 서버 통신을 위한 변환 헬퍼 (새로 추가)
    // ========================================

    namespace ServerAdapter {

        // Qt 타입을 서버 전송용 Common 타입으로 변환
        inline Common::UserInfo toServer(const UserInfo& qtUser) {
            return qtUser.toCommon();
        }

        inline Common::RoomInfo toServer(const RoomInfo& qtRoom) {
            return qtRoom.toCommon();
        }

        inline Common::GameRoomInfo toServer(const GameRoomInfo& qtGameRoom) {
            return qtGameRoom.toCommon();
        }

        // 서버에서 받은 Common 타입을 Qt 타입으로 변환
        inline UserInfo fromServer(const Common::UserInfo& serverUser) {
            return UserInfo(serverUser);
        }

        inline RoomInfo fromServer(const Common::RoomInfo& serverRoom) {
            return RoomInfo(serverRoom);
        }

        inline GameRoomInfo fromServer(const Common::GameRoomInfo& serverGameRoom) {
            return GameRoomInfo(serverGameRoom);
        }

        // 벡터 변환 헬퍼
        inline QList<UserInfo> fromServerUserList(const std::vector<Common::UserInfo>& serverUsers) {
            return QtAdapter::toQList<UserInfo, Common::UserInfo>(serverUsers);
        }

        inline QList<RoomInfo> fromServerRoomList(const std::vector<Common::RoomInfo>& serverRooms) {
            return QtAdapter::toQList<RoomInfo, Common::RoomInfo>(serverRooms);
        }

        inline std::vector<Common::UserInfo> toServerUserList(const QList<UserInfo>& qtUsers) {
            return QtAdapter::fromQList<Common::UserInfo, UserInfo>(qtUsers);
        }

    } // namespace ServerAdapter

} // namespace Blokus