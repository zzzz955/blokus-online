#pragma once

#include "../../../common/include/common/Types.h"        // 서버와 공유하는 Common 타입들
#include "../../../common/include/common/Utils.h"        // 서버와 공유하는 Common 유틸리티
#include <QString>
#include <QColor>
#include <QList>
#include <QPoint>

/**
 * @brief Common 라이브러리와 Qt 간의 변환을 담당하는 어댑터 클래스들
 */

namespace Blokus {
    namespace QtAdapter {

        // ========================================
        // 문자열 변환
        // ========================================

        QString toQString(const std::string& str);
        std::string fromQString(const QString& qstr);

        // ========================================
        // UserInfo 변환
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

            // 기본 생성자 추가
            QtUserInfo();

            // Common::UserInfo로부터 변환
            QtUserInfo(const Common::UserInfo& commonInfo);

            // Common::UserInfo로 변환
            Common::UserInfo toCommon() const;

            // Qt 전용 메서드들
            double getWinRate() const;
            int calculateLevel() const;
            QString getFormattedStats() const;
        };

        // ========================================
        // RoomInfo 변환
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

            // 기본 생성자 추가
            QtRoomInfo();

            QtRoomInfo(const Common::RoomInfo& commonInfo);
            Common::RoomInfo toCommon() const;

            QString getStatusText() const;
            QString getPlayerCountText() const;
        };

        // ========================================
        // PlayerSlot 변환
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

            // 기본 생성자 추가
            QtPlayerSlot();

            QtPlayerSlot(const Common::PlayerSlot& commonSlot);
            Common::PlayerSlot toCommon() const;

            bool isEmpty() const;
            QString getDisplayName() const;
            QString getStatusText() const;
            QColor getPlayerColor() const;
        };

        // ========================================
        // GameRoomInfo 변환
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

            // 기본 생성자 추가
            QtGameRoomInfo();

            QtGameRoomInfo(const Common::GameRoomInfo& commonInfo);
            Common::GameRoomInfo toCommon() const;

            int getCurrentPlayerCount() const;
            Common::PlayerColor getMyColor(const QString& username) const;
            bool isMyTurn(const QString& username, Common::PlayerColor currentTurn) const;
        };

        // ========================================
        // 유틸리티 함수 래퍼
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
        // 컨테이너 변환 헬퍼
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