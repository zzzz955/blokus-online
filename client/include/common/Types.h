#pragma once

// ========================================
// Common ���̺귯�� import
// ========================================
#include "../../../common/include/common/Types.h"        // ������ �����ϴ� Common Ÿ�Ե�
#include "../../../common/include/common/Utils.h"        // ������ �����ϴ� Common ��ƿ��Ƽ
#include "QtAdapter.h"                         // Qt ��ȯ �����

// ========================================
// Qt �����
// ========================================
#include <QString>
#include <QList>
#include <QColor>
#include <QPoint>
#include <QDateTime>

namespace Blokus {

    // ========================================
    // Common ���̺귯�� Ÿ�Ե��� �⺻ ���ӽ����̽��� ��������
    // ========================================

    // �⺻ Ÿ�Ե� (���� �ڵ�� ȣȯ)
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
    // Qt ����� Ÿ�Ե�� ��ü (���� �ڵ� ȣȯ�� ����)
    // ========================================

    // ���� �ڵ忡�� ����ϴ� Ÿ�Ը��� �״�� �����ϵ�, ���������δ� Qt ����� ���
    using UserInfo = QtAdapter::QtUserInfo;
    using RoomInfo = QtAdapter::QtRoomInfo;
    using PlayerSlot = QtAdapter::QtPlayerSlot;
    using GameRoomInfo = QtAdapter::QtGameRoomInfo;

    // ========================================
    // Qt ���� ����ü�� (���� ����)
    // ========================================

    // ChatMessage�� Qt �����̹Ƿ� �״�� ����
    struct ChatMessage {
        QString username;
        QString message;
        QDateTime timestamp;
        enum Type { Normal, System, Whisper } type;

        ChatMessage()
            : username(QString::fromUtf8("�ý���"))
            , message("")
            , timestamp(QDateTime::currentDateTime())
            , type(System)
        {
        }
    };

    // ========================================
    // ��ƿ��Ƽ �Լ��� (���� �ڵ� ȣȯ�� ����)
    // ========================================

    namespace Utils {

        // ���� �ڵ忡�� ����ϴ� �Լ������ �״�� ����
        inline QString playerColorToString(PlayerColor color) {
            return QtAdapter::Utils::playerColorToString(color);
        }

        // �Լ� �����ε� ���� - QColor ��ȯ �Լ��� �ٸ� �̸� ���
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
    // ���� ����� ���� ��ȯ ���� (���� �߰�)
    // ========================================

    namespace ServerAdapter {

        // Qt Ÿ���� ���� ���ۿ� Common Ÿ������ ��ȯ
        inline Common::UserInfo toServer(const UserInfo& qtUser) {
            return qtUser.toCommon();
        }

        inline Common::RoomInfo toServer(const RoomInfo& qtRoom) {
            return qtRoom.toCommon();
        }

        inline Common::GameRoomInfo toServer(const GameRoomInfo& qtGameRoom) {
            return qtGameRoom.toCommon();
        }

        // �������� ���� Common Ÿ���� Qt Ÿ������ ��ȯ
        inline UserInfo fromServer(const Common::UserInfo& serverUser) {
            return UserInfo(serverUser);
        }

        inline RoomInfo fromServer(const Common::RoomInfo& serverRoom) {
            return RoomInfo(serverRoom);
        }

        inline GameRoomInfo fromServer(const Common::GameRoomInfo& serverGameRoom) {
            return GameRoomInfo(serverGameRoom);
        }

        // ���� ��ȯ ����
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