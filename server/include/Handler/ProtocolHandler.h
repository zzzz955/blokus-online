#pragma once

#include "ServerTypes.h"
#include <string>

namespace Blokus {
    namespace Server {

        class ProtocolHandler {
        public:
            ProtocolHandler();
            ~ProtocolHandler();

            // Protobuf �޽��� ó��
            std::string serializeMessage(const google::protobuf::Message& message);
            bool deserializeMessage(const std::string& data, google::protobuf::Message& message);

            // �޽��� ����/����
            std::string wrapMessage(const std::string& messageType, const std::string& payload);
            bool unwrapMessage(const std::string& data, std::string& messageType, std::string& payload);

        private:
            // �޽��� ����/���� (������)
            std::string compressMessage(const std::string& data);
            std::string decompressMessage(const std::string& data);
        };

    } // namespace Server
} // namespace Blokus