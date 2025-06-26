#pragma once

#include <cstdint>
#include <string>
#include <vector>
#include <array>
#include <memory>

namespace Blokus {

    // �⺻ Ÿ�Ե�
    using PlayerId = uint8_t;
    using BlockId = uint8_t;
    using Position = std::pair<int, int>;
    using BoardSize = int;

    // ���� �����
    constexpr BoardSize BOARD_SIZE = 20;
    constexpr int MAX_PLAYERS = 4;
    constexpr int BLOCKS_PER_PLAYER = 21;

    // �÷��̾� ����
    enum class PlayerColor : uint8_t {
        Blue = 0,
        Yellow = 1,
        Red = 2,
        Green = 3,
        None = 255
    };

    // ���� ����
    enum class GameState : uint8_t {
        Waiting,
        Playing,
        Finished,
        Paused
    };

    // ��� ȸ�� ����
    enum class Rotation : uint8_t {
        Rotation0 = 0,
        Rotation90 = 1,
        Rotation180 = 2,
        Rotation270 = 3
    };

    // ��� ������ ����
    enum class Flip : uint8_t {
        Normal = 0,
        Flipped = 1
    };

    // ��� ��� ���� (��� ��ǥ)
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

    // ��� ����
    struct BlockInfo {
        BlockId id;
        BlockShape shape;
        int size; // ����� �����ϴ� ���� ����
        bool isUsed;

        BlockInfo() : id(0), size(0), isUsed(false) {}
        BlockInfo(BlockId blockId, const BlockShape& blockShape)
            : id(blockId), shape(blockShape), size(blockShape.positions.size()), isUsed(false) {
        }
    };

    // �÷��̾� ����
    struct PlayerInfo {
        PlayerId id;
        std::string name;
        PlayerColor color;
        int score;
        bool isConnected;
        std::array<BlockInfo, BLOCKS_PER_PLAYER> blocks;

        PlayerInfo() : id(0), color(PlayerColor::None), score(0), isConnected(false) {}
    };

    // ���� �� ����
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

    // ��� ��ġ ����
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

    // ��Ʈ��ũ �޽��� Ÿ��
    enum class MessageType : uint16_t {
        // ���� ����
        Connect = 1000,
        Disconnect = 1001,

        // ���� ����
        Login = 2000,
        Logout = 2001,
        Register = 2002,

        // �κ� ����
        JoinLobby = 3000,
        LeaveLobby = 3001,
        CreateRoom = 3002,
        JoinRoom = 3003,
        LeaveRoom = 3004,
        RoomList = 3005,

        // ���� ����
        StartGame = 4000,
        PlaceBlock = 4001,
        GameState = 4002,
        TurnChange = 4003,
        GameEnd = 4004,

        // ä��
        ChatMessage = 5000,

        // ����
        Error = 9999
    };

    // ���� �ڵ�
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