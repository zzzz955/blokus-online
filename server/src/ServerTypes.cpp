// ServerTypes.cpp 또는 별도 파일에 구현

#include "ServerTypes.h"
#include <unordered_map>
#include <algorithm>
#include <cctype>

namespace Blokus {
    namespace Server {

        // 문자열 정리 유틸리티
        std::string trimString(const std::string& str) {
            auto start = str.find_first_not_of(" \t\r\n");
            if (start == std::string::npos) return "";

            auto end = str.find_last_not_of(" \t\r\n");
            return str.substr(start, end - start + 1);
        }

        // MessageType 파서
        MessageType parseMessageType(const std::string& messageStr) {
            // 메시지 정리
            std::string clean = trimString(messageStr);

            // 소문자로 변환 (대소문자 무관)
            std::transform(clean.begin(), clean.end(), clean.begin(), ::tolower);

            // 매핑 테이블
            static const std::unordered_map<std::string, MessageType> typeMap = {
                // 기본
                {"ping", MessageType::Ping},

                // 인증
                {"auth", MessageType::Auth},
                {"register", MessageType::Register},
                {"guest", MessageType::Guest},
                {"logout", MessageType::Logout},
                {"validate", MessageType::Validate},

                // 방 관련
                {"room:create", MessageType::RoomCreate},
                {"room:join", MessageType::RoomJoin},
                {"room:leave", MessageType::RoomLeave},
                {"room:list", MessageType::RoomList},
                {"room:ready", MessageType::RoomReady},
                {"room:start", MessageType::RoomStart},
                {"room:end", MessageType::RoomEnd},
                {"room:transfer", MessageType::RoomTransferHost},

                // 게임
                {"game:move", MessageType::GameMove},
                {"game:end", MessageType::GameEnd},

                // 채팅
                {"chat", MessageType::Chat}
            };

            auto it = typeMap.find(clean);
            return (it != typeMap.end()) ? it->second : MessageType::Unknown;
        }

        // MessageType을 문자열로 변환
        std::string messageTypeToString(MessageType type) {
            switch (type) {
            case MessageType::Ping: return "ping";
            case MessageType::Auth: return "auth";
            case MessageType::Register: return "register";
            case MessageType::Guest: return "guest";
            case MessageType::Logout: return "logout";
            case MessageType::Validate: return "validate";
            case MessageType::RoomCreate: return "room:create";
            case MessageType::RoomJoin: return "room:join";
            case MessageType::RoomLeave: return "room:leave";
            case MessageType::RoomList: return "room:list";
            case MessageType::RoomReady: return "room:ready";
            case MessageType::RoomStart: return "room:start";
            case MessageType::RoomEnd: return "room:end";
            case MessageType::RoomTransferHost: return "room:transfer";
            case MessageType::GameMove: return "game:move";
            case MessageType::GameEnd: return "game:end";
            case MessageType::Chat: return "chat";
            default: return "unknown";
            }
        }

    } // namespace Server
} // namespace Blokus