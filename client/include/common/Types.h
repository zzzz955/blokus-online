#pragma once

#include <cstdint>
#include <string>
#include <vector>
#include <array>
#include <memory>

namespace Blokus {

    // 기본 타입들
    using PlayerId = uint8_t;
    using BlockId = uint8_t;
    using Position = std::pair<int, int>;
    using BoardSize = int;

    // 게임 상수들
    constexpr BoardSize BOARD_SIZE = 20;
    constexpr int MAX_PLAYERS = 4;
    constexpr int BLOCKS_PER_PLAYER = 21;

    // 플레이어 색상
    enum class PlayerColor : uint8_t {
        Blue = 0,
        Yellow = 1,
        Red = 2,
        Green = 3,
        None = 255
    };

    // 게임 상태
    enum class GameState : uint8_t {
        Waiting,
        Playing,
        Finished,
        Paused
    };

    // 블록 회전 상태
    enum class Rotation : uint8_t {
        Rotation0 = 0,
        Rotation90 = 1,
        Rotation180 = 2,
        Rotation270 = 3
    };

    // 블록 뒤집기 상태
    enum class Flip : uint8_t {
        Normal = 0,
        Flipped = 1
    };

    // 블록 모양 정의 (상대 좌표)
    struct BlockShape {
        std::vector<Position> positions;
        int width;
        int height;

        BlockShape() : width(0), height(0) {}
        BlockShape(const std::vector<Position>& pos) : positions(pos) {
            calculateDimensions();
        }

    private:
        void calculateDimensions();
    };

    // 블록 정보
    struct BlockInfo {
        BlockId id;
        BlockShape shape;
        int size; // 블록을 구성하는 셀의 개수
        bool isUsed;

        BlockInfo() : id(0), size(0), isUsed(false) {}
        BlockInfo(BlockId blockId, const BlockShape& blockShape)
            : id(blockId), shape(blockShape), size(blockShape.positions.size()), isUsed(false) {
        }
    };

    // 플레이어 정보
    struct PlayerInfo {
        PlayerId id;
        std::string name;
        PlayerColor color;
        int score;
        bool isConnected;
        std::array<BlockInfo, BLOCKS_PER_PLAYER> blocks;

        PlayerInfo() : id(0), color(PlayerColor::None), score(0), isConnected(false) {}
    };

    // 게임 방 정보
    struct RoomInfo {
        uint32_t roomId;
        std::string roomName;
        std::string hostName;
        int currentPlayers;
        int maxPlayers;
        GameState state;
        bool hasPassword;

        RoomInfo() : roomId(0), currentPlayers(0), maxPlayers(4),
            state(GameState::Waiting), hasPassword(false) {
        }
    };

    // 블록 배치 정보
    struct BlockPlacement {
        BlockId blockId;
        Position position;
        Rotation rotation;
        Flip flip;

        BlockPlacement() : blockId(0), position({ 0, 0 }),
            rotation(Rotation::Rotation0), flip(Flip::Normal) {
        }
        BlockPlacement(BlockId id, const Position& pos, Rotation rot = Rotation::Rotation0, Flip f = Flip::Normal)
            : blockId(id), position(pos), rotation(rot), flip(f) {
        }
    };

    // 네트워크 메시지 타입
    enum class MessageType : uint16_t {
        // 연결 관련
        Connect = 1000,
        Disconnect = 1001,

        // 인증 관련
        Login = 2000,
        Logout = 2001,
        Register = 2002,

        // 로비 관련
        JoinLobby = 3000,
        LeaveLobby = 3001,
        CreateRoom = 3002,
        JoinRoom = 3003,
        LeaveRoom = 3004,
        RoomList = 3005,

        // 게임 관련
        StartGame = 4000,
        PlaceBlock = 4001,
        GameState = 4002,
        TurnChange = 4003,
        GameEnd = 4004,

        // 채팅
        ChatMessage = 5000,

        // 오류
        Error = 9999
    };

    // 오류 코드
    enum class ErrorCode : uint16_t {
        Success = 0,
        InvalidCredentials = 1001,
        UserAlreadyExists = 1002,
        RoomNotFound = 2001,
        RoomFull = 2002,
        InvalidMove = 3001,
        NotYourTurn = 3002,
        GameNotStarted = 3003,
        NetworkError = 9001,
        UnknownError = 9999
    };

} // namespace Blokus