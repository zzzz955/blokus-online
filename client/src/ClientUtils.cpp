#include "ClientTypes.h"

namespace Blokus {
    namespace Utils {

        QString playerColorToString(PlayerColor color) {
            return QString::fromStdString(Common::Utils::playerColorToString(color));
        }

        QColor getPlayerColor(PlayerColor color) {
            switch (color) {
            case PlayerColor::Blue: return QColor(52, 152, 219);
            case PlayerColor::Yellow: return QColor(241, 196, 15);
            case PlayerColor::Red: return QColor(231, 76, 60);
            case PlayerColor::Green: return QColor(46, 204, 113);
            default: return QColor(149, 165, 166);
            }
        }

        QString trim(const QString& str) {
            return str.trimmed();
        }

        bool isValidUsername(const QString& username) {
            return Common::Utils::isValidUsername(username.toUtf8().toStdString());
        }

        bool isValidRoomName(const QString& roomName) {
            return Common::Utils::isValidRoomName(roomName.toUtf8().toStdString());
        }

        QString getBlockName(BlockType blockType) {
            return QString::fromStdString(Common::Utils::getBlockName(blockType));
        }

        QString formatTurnTime(int seconds) {
            return QString::fromStdString(Common::Utils::formatTurnTime(seconds));
        }

        bool isTurnTimeExpired(int remainingSeconds) {
            return Common::Utils::isTurnTimeExpired(remainingSeconds);
        }

    } // namespace Utils
} // namespace Blokus