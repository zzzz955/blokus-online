#pragma once

#include "../../../common/include/common/Types.h"        // ������ �����ϴ� Common Ÿ�Ե�
#include "../../../common/include/common/Utils.h"        // ������ �����ϴ� Common ��ƿ��Ƽ
#include <QString>
#include <QColor>
#include <QList>
#include <QPoint>

/**
 * @brief Common ���̺귯���� Qt ���� ��ȯ�� ����ϴ� ����� Ŭ������
 */

namespace Blokus {
    namespace QtAdapter {

        // ========================================
        // ���ڿ� ��ȯ
        // ========================================

        QString toQString(const std::string& str);
        std::string fromQString(const QString& qstr);

        // ========================================
        // UserInfo ��ȯ
        // ========================================

        struct QtUserInfo {
            QString username;
            int level;
            int totalGames;
            int wins;
            int losses;
            int averageScore;
            bool isOnline;
            QString status;

            // �⺻ ������ �߰�
            QtUserInfo();

            // Common::UserInfo�κ��� ��ȯ
            QtUserInfo(const Common::UserInfo& commonInfo);

            // Common::UserInfo�� ��ȯ
            Common::UserInfo toCommon() const;

            // Qt ���� �޼����
            double getWinRate() const;
            int calculateLevel() const;
            QString getFormattedStats() const;
        };

        // ========================================
        // RoomInfo ��ȯ
        // ========================================

        struct QtRoomInfo {
            int roomId;
            QString roomName;
            QString hostName;
            int currentPlayers;
            int maxPlayers;
            bool isPrivate;
            bool isPlaying;
            QString gameMode;

            // �⺻ ������ �߰�
            QtRoomInfo();

            QtRoomInfo(const Common::RoomInfo& commonInfo);
            Common::RoomInfo toCommon() const;

            QString getStatusText() const;
            QString getPlayerCountText() const;
        };

        // ========================================
        // PlayerSlot ��ȯ
        // ========================================

        struct QtPlayerSlot {
            Common::PlayerColor color;
            QString username;
            bool isAI;
            int aiDifficulty;
            bool isHost;
            bool isReady;
            int score;
            int remainingBlocks;

            // �⺻ ������ �߰�
            QtPlayerSlot();

            QtPlayerSlot(const Common::PlayerSlot& commonSlot);
            Common::PlayerSlot toCommon() const;

            bool isEmpty() const;
            QString getDisplayName() const;
            QString getStatusText() const;
            QColor getPlayerColor() const;
        };

        // ========================================
        // GameRoomInfo ��ȯ
        // ========================================

        struct QtGameRoomInfo {
            int roomId;
            QString roomName;
            QString hostUsername;
            Common::PlayerColor hostColor;
            int maxPlayers;
            QString gameMode;
            bool isPlaying;
            QList<QtPlayerSlot> playerSlots;

            // �⺻ ������ �߰�
            QtGameRoomInfo();

            QtGameRoomInfo(const Common::GameRoomInfo& commonInfo);
            Common::GameRoomInfo toCommon() const;

            int getCurrentPlayerCount() const;
            Common::PlayerColor getMyColor(const QString& username) const;
            bool isMyTurn(const QString& username, Common::PlayerColor currentTurn) const;
        };

        // ========================================
        // ��ƿ��Ƽ �Լ� ����
        // ========================================

        namespace Utils {
            QString playerColorToString(Common::PlayerColor color);
            QColor playerColorToQColor(Common::PlayerColor color);
            QPoint positionToQPoint(const Common::Position& pos);
            Common::Position qPointToPosition(const QPoint& point);
            QString formatTurnTime(int seconds);
            QString getBlockName(Common::BlockType blockType);
            QString getBlockDescription(Common::BlockType blockType);
        }

        // ========================================
        // �����̳� ��ȯ ����
        // ========================================

        template<typename QtType, typename CommonType>
        QList<QtType> toQList(const std::vector<CommonType>& vec) {
            QList<QtType> result;
            result.reserve(vec.size());
            for (const auto& item : vec) {
                result.append(QtType(item));
            }
            return result;
        }

        template<typename CommonType, typename QtType>
        std::vector<CommonType> fromQList(const QList<QtType>& list) {
            std::vector<CommonType> result;
            result.reserve(list.size());
            for (const auto& item : list) {
                result.push_back(item.toCommon());
            }
            return result;
        }

    } // namespace QtAdapter
} // namespace Blokus