// ServerTypes.cpp �Ǵ� ���� ���Ͽ� ����

#include "ServerTypes.h"
#include <unordered_map>
#include <algorithm>
#include <cctype>
#include <spdlog/spdlog.h>

namespace Blokus
{
    namespace Server
    {

        // ���ڿ� ���� ��ƿ��Ƽ
        std::string trimString(const std::string &str)
        {
            auto start = str.find_first_not_of(" \t\r\n");
            if (start == std::string::npos)
                return "";

            auto end = str.find_last_not_of(" \t\r\n");
            return str.substr(start, end - start + 1);
        }

        // MessageType �ļ�
        MessageType parseMessageType(const std::string &messageStr)
        {
            // �޽��� ����
            std::string clean = trimString(messageStr);

            // �ҹ��ڷ� ��ȯ (��ҹ��� ����)
            std::transform(clean.begin(), clean.end(), clean.begin(), ::tolower);

            // ���� ���̺�
            static const std::unordered_map<std::string, MessageType> typeMap = {
                // �⺻
                {"ping", MessageType::Ping},

                // ����
                {"auth", MessageType::Auth},
                {"auth_request", MessageType::Auth},  // 모바일 클라이언트 호환
                {"register", MessageType::Register},
                {"guest", MessageType::Guest},
                {"logout", MessageType::Logout},
                {"validate", MessageType::Validate},

                // �κ�
                {"lobby:enter", MessageType::LobbyEnter},
                {"lobby:leave", MessageType::LobbyLeave},
                {"lobby:list", MessageType::LobbyList},

                // �� ����
                {"room:create", MessageType::RoomCreate},
                {"room:join", MessageType::RoomJoin},
                {"room:leave", MessageType::RoomLeave},
                {"room:list", MessageType::RoomList},
                {"room:ready", MessageType::RoomReady},
                {"room:start", MessageType::RoomStart},
                {"room:end", MessageType::RoomEnd},
                {"room:transfer", MessageType::RoomTransferHost},

                // ����
                {"game:move", MessageType::GameMove},
                {"game:end", MessageType::GameEnd},
                {"game:result", MessageType::GameResultResponse},

                // ä��
                {"chat", MessageType::Chat},

                // 유저 관련
                {"user:stats", MessageType::UserStats},
                {"user:settings", MessageType::UserSettings},

                // 버전 관련
                {"version:check", MessageType::VersionCheck}};

            auto it = typeMap.find(clean);
            spdlog::warn("parseMessageType DEBUG: '{}' -> {}", clean, (it != typeMap.end()) ? "FOUND" : "NOT_FOUND");
            return (it != typeMap.end()) ? it->second : MessageType::Unknown;
        }

        // MessageType�� ���ڿ��� ��ȯ
        std::string messageTypeToString(MessageType type)
        {
            switch (type)
            {
            case MessageType::Ping:
                return "ping";
            case MessageType::Auth:
                return "auth";
            case MessageType::Register:
                return "register";
            case MessageType::Guest:
                return "guest";
            case MessageType::Logout:
                return "logout";
            case MessageType::Validate:
                return "validate";
            case MessageType::LobbyEnter:
                return "lobby:enter";
            case MessageType::LobbyLeave:
                return "lobby:leave";
            case MessageType::LobbyList:
                return "lobby:list";
            case MessageType::RoomCreate:
                return "room:create";
            case MessageType::RoomJoin:
                return "room:join";
            case MessageType::RoomLeave:
                return "room:leave";
            case MessageType::RoomList:
                return "room:list";
            case MessageType::RoomReady:
                return "room:ready";
            case MessageType::RoomStart:
                return "room:start";
            case MessageType::RoomEnd:
                return "room:end";
            case MessageType::RoomTransferHost:
                return "room:transfer";
            case MessageType::GameMove:
                return "game:move";
            case MessageType::GameEnd:
                return "game:end";
            case MessageType::GameResultResponse:
                return "game:result";
            case MessageType::Chat:
                return "chat";
            case MessageType::UserStats:
                return "user:stats";
            case MessageType::UserSettings:
                return "user:settings";
            case MessageType::VersionCheck:
                return "version:check";
            default:
                return "unknown";
            }
        }

    } // namespace Server
} // namespace Blokus