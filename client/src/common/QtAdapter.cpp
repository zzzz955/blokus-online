#include "common/QtAdapter.h"

namespace Blokus {
    namespace QtAdapter {

        // ========================================
        // ���ڿ� ��ȯ
        // ========================================

        QString toQString(const std::string& str) {
            return QString::fromUtf8(str.c_str());
        }

        std::string fromQString(const QString& qstr) {
            return qstr.toUtf8().toStdString();
        }

        // ========================================
        // QtUserInfo ����
        // ========================================

        QtUserInfo::QtUserInfo()
            : username(QString::fromUtf8("�͸�"))
            , level(1)
            , totalGames(0)
            , wins(0)
            , losses(0)
            , averageScore(0)
            , isOnline(true)
            , status(QString::fromUtf8("�κ�"))
        {
        }

        QtUserInfo::QtUserInfo(const Common::UserInfo& commonInfo)
            : username(toQString(commonInfo.username))
            , level(commonInfo.level)
            , totalGames(commonInfo.totalGames)
            , wins(commonInfo.wins)
            , losses(commonInfo.losses)
            , averageScore(commonInfo.averageScore)
            , isOnline(commonInfo.isOnline)
            , status(toQString(commonInfo.status))
        {
        }

        Common::UserInfo QtUserInfo::toCommon() const {
            Common::UserInfo result;
            result.username = fromQString(username);
            result.level = level;
            result.totalGames = totalGames;
            result.wins = wins;
            result.losses = losses;
            result.averageScore = averageScore;
            result.isOnline = isOnline;
            result.status = fromQString(status);
            return result;
        }

        double QtUserInfo::getWinRate() const {
            return totalGames > 0 ? static_cast<double>(wins) / totalGames * 100.0 : 0.0;
        }

        int QtUserInfo::calculateLevel() const {
            return (totalGames / 10) + 1;
        }

        QString QtUserInfo::getFormattedStats() const {
            return QString::fromUtf8("���� %1 | %2�� %3�� | �·� %4%")
                .arg(level)
                .arg(wins)
                .arg(losses)
                .arg(QString::number(getWinRate(), 'f', 1));
        }

        // ========================================
        // QtRoomInfo ����
        // ========================================

        QtRoomInfo::QtRoomInfo()
            : roomId(0)
            , roomName(QString::fromUtf8("�� ��"))
            , hostName(QString::fromUtf8("ȣ��Ʈ"))
            , currentPlayers(1)
            , maxPlayers(4)
            , isPrivate(false)
            , isPlaying(false)
            , gameMode(QString::fromUtf8("Ŭ����"))
        {
        }

        QtRoomInfo::QtRoomInfo(const Common::RoomInfo& commonInfo)
            : roomId(commonInfo.roomId)
            , roomName(toQString(commonInfo.roomName))
            , hostName(toQString(commonInfo.hostName))
            , currentPlayers(commonInfo.currentPlayers)
            , maxPlayers(commonInfo.maxPlayers)
            , isPrivate(commonInfo.isPrivate)
            , isPlaying(commonInfo.isPlaying)
            , gameMode(toQString(commonInfo.gameMode))
        {
        }

        Common::RoomInfo QtRoomInfo::toCommon() const {
            Common::RoomInfo result;
            result.roomId = roomId;
            result.roomName = fromQString(roomName);
            result.hostName = fromQString(hostName);
            result.currentPlayers = currentPlayers;
            result.maxPlayers = maxPlayers;
            result.isPrivate = isPrivate;
            result.isPlaying = isPlaying;
            result.gameMode = fromQString(gameMode);
            return result;
        }

        QString QtRoomInfo::getStatusText() const {
            return isPlaying ? QString::fromUtf8("������") : QString::fromUtf8("�����");
        }

        QString QtRoomInfo::getPlayerCountText() const {
            return QString::fromUtf8("%1/%2��").arg(currentPlayers).arg(maxPlayers);
        }

        // ========================================
        // QtPlayerSlot ����
        // ========================================

        QtPlayerSlot::QtPlayerSlot()
            : color(Common::PlayerColor::None)
            , username("")
            , isAI(false)
            , aiDifficulty(2)
            , isHost(false)
            , isReady(false)
            , score(0)
            , remainingBlocks(Common::BLOCKS_PER_PLAYER)
        {
        }

        QtPlayerSlot::QtPlayerSlot(const Common::PlayerSlot& commonSlot)
            : color(commonSlot.color)
            , username(toQString(commonSlot.username))
            , isAI(commonSlot.isAI)
            , aiDifficulty(commonSlot.aiDifficulty)
            , isHost(commonSlot.isHost)
            , isReady(commonSlot.isReady)
            , score(commonSlot.score)
            , remainingBlocks(commonSlot.remainingBlocks)
        {
        }

        Common::PlayerSlot QtPlayerSlot::toCommon() const {
            Common::PlayerSlot result;
            result.color = color;
            result.username = fromQString(username);
            result.isAI = isAI;
            result.aiDifficulty = aiDifficulty;
            result.isHost = isHost;
            result.isReady = isReady;
            result.score = score;
            result.remainingBlocks = remainingBlocks;
            return result;
        }

        bool QtPlayerSlot::isEmpty() const {
            return username.isEmpty() && !isAI;
        }

        QString QtPlayerSlot::getDisplayName() const {
            if (isEmpty()) {
                return QString::fromUtf8("�� ����");
            }
            else if (isAI) {
                return QString::fromUtf8("AI (���� %1)").arg(aiDifficulty);
            }
            else {
                return username;
            }
        }

        QString QtPlayerSlot::getStatusText() const {
            if (isEmpty()) {
                return QString::fromUtf8("�� ����");
            }
            else if (isAI) {
                QString difficultyText = (aiDifficulty == 1) ? QString::fromUtf8("����") :
                    (aiDifficulty == 2) ? QString::fromUtf8("����") : QString::fromUtf8("�����");
                return QString::fromUtf8("AI %1").arg(difficultyText);
            }
            else {
                return isReady ? QString::fromUtf8("�غ��") : QString::fromUtf8("��� ��");
            }
        }

        QColor QtPlayerSlot::getPlayerColor() const {
            return Utils::playerColorToQColor(color);
        }

        // ========================================
        // QtGameRoomInfo ����
        // ========================================

        QtGameRoomInfo::QtGameRoomInfo()
            : roomId(0)
            , roomName(QString::fromUtf8("�� ��"))
            , hostUsername("")
            , hostColor(Common::PlayerColor::Blue)
            , maxPlayers(4)
            , gameMode(QString::fromUtf8("Ŭ����"))
            , isPlaying(false)
        {
            // 4�� ���� ���� �ʱ�ȭ
            for (int i = 0; i < 4; ++i) {
                QtPlayerSlot slot;
                slot.color = static_cast<Common::PlayerColor>(i + 1);
                playerSlots.append(slot);
            }
        }

        QtGameRoomInfo::QtGameRoomInfo(const Common::GameRoomInfo& commonInfo)
            : roomId(commonInfo.roomId)
            , roomName(toQString(commonInfo.roomName))
            , hostUsername(toQString(commonInfo.hostUsername))
            , hostColor(commonInfo.hostColor)
            , maxPlayers(commonInfo.maxPlayers)
            , gameMode(toQString(commonInfo.gameMode))
            , isPlaying(commonInfo.isPlaying)
        {
            // std::array�� QList�� ��ȯ
            for (const auto& slot : commonInfo.playerSlots) {
                playerSlots.append(QtPlayerSlot(slot));
            }
        }

        Common::GameRoomInfo QtGameRoomInfo::toCommon() const {
            Common::GameRoomInfo result;
            result.roomId = roomId;
            result.roomName = fromQString(roomName);
            result.hostUsername = fromQString(hostUsername);
            result.hostColor = hostColor;
            result.maxPlayers = maxPlayers;
            result.gameMode = fromQString(gameMode);
            result.isPlaying = isPlaying;

            // QList�� std::array�� ��ȯ
            for (int i = 0; i < std::min(playerSlots.size(), 4); ++i) {
                result.playerSlots[i] = playerSlots[i].toCommon();
            }

            return result;
        }

        int QtGameRoomInfo::getCurrentPlayerCount() const {
            int count = 0;
            for (const auto& slot : playerSlots) {
                if (!slot.isEmpty()) count++;
            }
            return count;
        }

        Common::PlayerColor QtGameRoomInfo::getMyColor(const QString& username) const {
            for (const auto& slot : playerSlots) {
                if (slot.username == username) {
                    return slot.color;
                }
            }
            return Common::PlayerColor::None;
        }

        bool QtGameRoomInfo::isMyTurn(const QString& username, Common::PlayerColor currentTurn) const {
            return getMyColor(username) == currentTurn;
        }

        // ========================================
        // ��ƿ��Ƽ �Լ� ���� ����
        // ========================================

        namespace Utils {

            QString playerColorToString(Common::PlayerColor color) {
                return toQString(Common::Utils::playerColorToString(color));
            }

            QColor playerColorToQColor(Common::PlayerColor color) {
                switch (color) {
                case Common::PlayerColor::Blue: return QColor(52, 152, 219);
                case Common::PlayerColor::Yellow: return QColor(241, 196, 15);
                case Common::PlayerColor::Red: return QColor(231, 76, 60);
                case Common::PlayerColor::Green: return QColor(46, 204, 113);
                default: return QColor(149, 165, 166);
                }
            }

            QPoint positionToQPoint(const Common::Position& pos) {
                return QPoint(pos.second, pos.first); // x=col, y=row
            }

            Common::Position qPointToPosition(const QPoint& point) {
                return { point.y(), point.x() }; // row=y, col=x
            }

            QString formatTurnTime(int seconds) {
                return toQString(Common::Utils::formatTurnTime(seconds));
            }

            QString getBlockName(Common::BlockType blockType) {
                return toQString(Common::Utils::getBlockName(blockType));
            }

            QString getBlockDescription(Common::BlockType blockType) {
                QString name = getBlockName(blockType);
                int score = Common::Utils::getBlockScore(blockType);
                return QString::fromUtf8("%1 (%2ĭ)").arg(name).arg(score);
            }

        } // namespace Utils

    } // namespace QtAdapter
} // namespace Blokus