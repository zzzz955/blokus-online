#pragma once

#include "ServerTypes.h"
#include <string>

namespace Blokus {
    namespace Server {

        class AuthenticationService {
        public:
            AuthenticationService();
            ~AuthenticationService();

            // ���� ó��
            bool authenticate(const std::string& username, const std::string& password);
            std::string generateSessionToken(const std::string& userId);
            bool validateSessionToken(const std::string& token);

            // ȸ������
            bool registerUser(const std::string& username, const std::string& password, const std::string& email);

        private:
            std::string hashPassword(const std::string& password);
            bool verifyPassword(const std::string& password, const std::string& hash);
        };

    } // namespace Server
} // namespace Blokus