#include "common/Utils.h"
#include <algorithm>
#include <cctype>
#include <sstream>
#include <iomanip>

namespace Blokus {
    namespace Common {
        namespace Utils {

            std::string playerColorToString(PlayerColor color) {
                switch (color) {
                case PlayerColor::Blue: return "파랑";
                case PlayerColor::Yellow: return "노랑";
                case PlayerColor::Red: return "빨강";
                case PlayerColor::Green: return "초록";
                default: return "없음";
                }
            }

            PlayerColor getNextPlayer(PlayerColor current) {
                switch (current) {
                case PlayerColor::Blue: return PlayerColor::Yellow;
                case PlayerColor::Yellow: return PlayerColor::Red;
                case PlayerColor::Red: return PlayerColor::Green;
                case PlayerColor::Green: return PlayerColor::Blue;
                default: return PlayerColor::Blue;
                }
            }

            bool isPositionValid(const Position& pos, int boardSize) {
                return pos.first >= 0 && pos.first < boardSize &&
                    pos.second >= 0 && pos.second < boardSize;
            }

            int manhattanDistance(const Position& a, const Position& b) {
                return std::abs(a.first - b.first) + std::abs(a.second - b.second);
            }

            std::string formatTurnTime(int seconds) {
                int minutes = seconds / 60;
                int remainingSeconds = seconds % 60;

                std::ostringstream oss;
                oss << minutes << ":" << std::setfill('0') << std::setw(2) << remainingSeconds;
                return oss.str();
            }

            bool isTurnTimeExpired(int remainingSeconds) {
                return remainingSeconds <= 0;
            }

            bool isCornerAdjacent(const Position& pos1, const Position& pos2) {
                int rowDiff = std::abs(pos1.first - pos2.first);
                int colDiff = std::abs(pos1.second - pos2.second);

                // 대각선으로 인접 (모서리 접촉)
                return (rowDiff == 1 && colDiff == 1);
            }

            bool isEdgeAdjacent(const Position& pos1, const Position& pos2) {
                int rowDiff = std::abs(pos1.first - pos2.first);
                int colDiff = std::abs(pos1.second - pos2.second);

                // 상하좌우로 인접 (변 접촉)
                return (rowDiff == 1 && colDiff == 0) || (rowDiff == 0 && colDiff == 1);
            }

            std::string trim(const std::string& str) {
                std::string result = str;

                // 앞의 공백 제거
                result.erase(result.begin(), std::find_if(result.begin(), result.end(), [](unsigned char ch) {
                    return !std::isspace(ch);
                    }));

                // 뒤의 공백 제거
                result.erase(std::find_if(result.rbegin(), result.rend(), [](unsigned char ch) {
                    return !std::isspace(ch);
                    }).base(), result.end());

                return result;
            }

            bool isValidUsername(const std::string& username) {
                if (username.length() < 3 || username.length() > 20) {
                    return false;
                }

                // 영문, 숫자, 언더스코어만 허용
                return std::all_of(username.begin(), username.end(), [](char c) {
                    return std::isalnum(c) || c == '_';
                    });
            }

            bool isValidRoomName(const std::string& roomName) {
                std::string trimmed = trim(roomName);
                return !trimmed.empty() && trimmed.length() <= 30;
            }

            int getBlockScore(BlockType blockType) {
                // 블록의 점수는 차지하는 칸 수와 동일
                switch (blockType) {
                case BlockType::Single: return 1;
                case BlockType::Domino: return 2;
                case BlockType::TrioLine:
                case BlockType::TrioAngle: return 3;
                case BlockType::Tetro_I:
                case BlockType::Tetro_O:
                case BlockType::Tetro_T:
                case BlockType::Tetro_L:
                case BlockType::Tetro_S: return 4;
                case BlockType::Pento_F:
                case BlockType::Pento_I:
                case BlockType::Pento_L:
                case BlockType::Pento_N:
                case BlockType::Pento_P:
                case BlockType::Pento_T:
                case BlockType::Pento_U:
                case BlockType::Pento_V:
                case BlockType::Pento_W:
                case BlockType::Pento_X:
                case BlockType::Pento_Y:
                case BlockType::Pento_Z: return 5;
                default: return 0;
                }
            }

            std::string getBlockName(BlockType blockType) {
                switch (blockType) {
                case BlockType::Single: return "단일";
                case BlockType::Domino: return "도미노";
                case BlockType::TrioLine: return "3일자";
                case BlockType::TrioAngle: return "3꺾임";
                case BlockType::Tetro_I: return "테트로 I";
                case BlockType::Tetro_O: return "테트로 O";
                case BlockType::Tetro_T: return "테트로 T";
                case BlockType::Tetro_L: return "테트로 L";
                case BlockType::Tetro_S: return "테트로 S";
                case BlockType::Pento_F: return "펜토 F";
                case BlockType::Pento_I: return "펜토 I";
                case BlockType::Pento_L: return "펜토 L";
                case BlockType::Pento_N: return "펜토 N";
                case BlockType::Pento_P: return "펜토 P";
                case BlockType::Pento_T: return "펜토 T";
                case BlockType::Pento_U: return "펜토 U";
                case BlockType::Pento_V: return "펜토 V";
                case BlockType::Pento_W: return "펜토 W";
                case BlockType::Pento_X: return "펜토 X";
                case BlockType::Pento_Y: return "펜토 Y";
                case BlockType::Pento_Z: return "펜토 Z";
                default: return "알 수 없음";
                }
            }

        } // namespace Utils
    } // namespace Common
} // namespace Blokus