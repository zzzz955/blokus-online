#include "ServerTypes.h"
#include <unordered_map>
#include <algorithm>
#include <cctype>
#include <spdlog/spdlog.h>

namespace Blokus
{
    namespace Server
    {

        // 메시지 trim처리
        std::string trimString(const std::string &str)
        {
            auto start = str.find_first_not_of(" \t\r\n");
            if (start == std::string::npos)
                return "";

            auto end = str.find_last_not_of(" \t\r\n");
            return str.substr(start, end - start + 1);
        }

        // MessageType 파싱
        MessageType parseMessageType(const std::string &messageStr)
        {
            // 메시지를 가져옴
            std::string clean = trimString(messageStr);

            // 소문자로 통일
            std::transform(clean.begin(), clean.end(), clean.begin(), ::tolower);

            // 메시지 식별
            static const std::unordered_map<std::string, MessageType> typeMap = {
                // 하트비트 관련
                {"ping", MessageType::Ping},

                // 인증 관련
                {"auth", MessageType::Auth},
                {"auth:jwt", MessageType::Auth},  // JWT 토큰 인증
                {"auth:login", MessageType::Auth},  // 사용자명/비밀번호 인증
                {"auth:register", MessageType::Register},  // 회원가입
                {"auth:guest", MessageType::Guest},  // 게스트 인증
                {"auth:mobile_jwt", MessageType::Auth},  // 모바일 JWT 인증
                {"register", MessageType::Register},
                {"guest", MessageType::Guest},
                {"logout", MessageType::Logout},
                {"validate", MessageType::Validate},

                // 로비 관련
                {"lobby:enter", MessageType::LobbyEnter},
                {"lobby:leave", MessageType::LobbyLeave},
                {"lobby:list", MessageType::LobbyList},

                // 방 관련
                {"room:create", MessageType::RoomCreate},
                {"room:join", MessageType::RoomJoin},
                {"room:leave", MessageType::RoomLeave},
                {"room:list", MessageType::RoomList},
                {"room:ready", MessageType::RoomReady},
                {"room:start", MessageType::RoomStart},
                {"room:end", MessageType::RoomEnd},
                {"room:transfer", MessageType::RoomTransferHost},

                // 게임 관련
                {"game:move", MessageType::GameMove},
                {"game:end", MessageType::GameEnd},
                {"game:result", MessageType::GameResultResponse},

                // 채팅 관련
                {"chat", MessageType::Chat},

                // 유저 관련
                {"user:stats", MessageType::UserStats},
                {"user:settings", MessageType::UserSettings},

                // 버전 관련
                {"version:check", MessageType::VersionCheck}};

            auto it = typeMap.find(clean);
            return (it != typeMap.end()) ? it->second : MessageType::Unknown;
        }

        // 헤더 기반 메시지 타입 식별
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