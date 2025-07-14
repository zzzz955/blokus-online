#pragma once

#include "Types.h"
#include <string>
#include <cmath>

namespace Blokus {
    namespace Common {
        namespace Utils {

            // PlayerColor ���� �Լ���
            std::string playerColorToString(PlayerColor color);
            PlayerColor getNextPlayer(PlayerColor current);

            // ��ġ ���� �Լ���
            bool isPositionValid(const Position& pos, int boardSize = BOARD_SIZE);
            int manhattanDistance(const Position& a, const Position& b);

            // �ð� ���� �Լ���
            std::string formatTurnTime(int seconds);
            bool isTurnTimeExpired(int remainingSeconds);

            // ���� ���� ���� �Լ���
            bool isCornerAdjacent(const Position& pos1, const Position& pos2);
            bool isEdgeAdjacent(const Position& pos1, const Position& pos2);

            // ���ڿ� ��ƿ��Ƽ
            std::string trim(const std::string& str);
            bool isValidUsername(const std::string& username);
            bool isValidRoomName(const std::string& roomName);

            // ��� Ÿ�� ����
            int getBlockScore(BlockType blockType);
            std::string getBlockName(BlockType blockType);

        } // namespace Utils
    } // namespace Common
} // namespace Blokus