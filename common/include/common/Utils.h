#pragma once

#include "Types.h"
#include <string>
#include <cmath>

namespace Blokus {
    namespace Common {
        namespace Utils {

            // PlayerColor 관련 함수들
            std::string playerColorToString(PlayerColor color);
            PlayerColor getNextPlayer(PlayerColor current);

            // 위치 관련 함수들
            bool isPositionValid(const Position& pos, int boardSize = BOARD_SIZE);
            int manhattanDistance(const Position& a, const Position& b);

            // 시간 관련 함수들
            std::string formatTurnTime(int seconds);
            bool isTurnTimeExpired(int remainingSeconds);

            // 게임 로직 관련 함수들
            bool isCornerAdjacent(const Position& pos1, const Position& pos2);
            bool isEdgeAdjacent(const Position& pos1, const Position& pos2);

            // 문자열 유틸리티
            std::string trim(const std::string& str);
            bool isValidUsername(const std::string& username);
            bool isValidRoomName(const std::string& roomName);

            // 블록 타입 관련
            int getBlockScore(BlockType blockType);
            std::string getBlockName(BlockType blockType);

        } // namespace Utils
    } // namespace Common
} // namespace Blokus