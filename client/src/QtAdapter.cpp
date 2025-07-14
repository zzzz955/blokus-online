#include "ClientTypes.h"
#include "QtAdapter.h"
#include "Utils.h"
#include <QDebug>

namespace Blokus {

    // ========================================
    // QtAdapter 네임스페이스 구현
    // ========================================
    namespace QtAdapter {

        QRect boundingRectToQRect(const Common::Block::BoundingRect& rect) {
            return QRect(rect.left, rect.top, rect.width, rect.height);
        }

    } // namespace QtAdapter

    // ========================================
    // 🔥 Utils 네임스페이스 구현 (중복 오류 해결됨)
    // ========================================
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

        PlayerColor getNextPlayer(PlayerColor current) {
            return Common::Utils::getNextPlayer(current);
        }

        bool isPositionValid(const Position& pos, int boardSize) {
            return Common::Utils::isPositionValid(pos, boardSize);
        }

        int manhattanDistance(const Position& a, const Position& b) {
            return Common::Utils::manhattanDistance(a, b);
        }

        bool isCornerAdjacent(const Position& pos1, const Position& pos2) {
            return Common::Utils::isCornerAdjacent(pos1, pos2);
        }

        bool isEdgeAdjacent(const Position& pos1, const Position& pos2) {
            return Common::Utils::isEdgeAdjacent(pos1, pos2);
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

        int getBlockScore(BlockType blockType) {
            return Common::Utils::getBlockScore(blockType);
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