#include "ClientVersion.h"
#include <sstream>
#include <algorithm>
#include <vector>

namespace Blokus {
namespace Client {

bool ClientVersion::isCompatibleWith(const std::string& serverVersion) {
    // Server-Client version compatibility check
    // Rule: Client X.Y.Z is compatible with Server X.Y.Z
    auto clientParts = parseVersion(VERSION);
    auto serverParts = parseVersion(serverVersion);

    if (clientParts.size() < 2 || serverParts.size() < 2) {
        return false;
    }

    // Major and Minor versions must match exactly
    // Client 1.0.x is compatible with Server 1.0.x
    return (clientParts[0] == serverParts[0]) && (clientParts[1] == serverParts[1]);
}

std::vector<int> ClientVersion::parseVersion(const std::string& version) {
    std::vector<int> parts;
    std::stringstream ss(version);
    std::string part;
    
    // Handle 'v' prefix
    std::string cleanVersion = version;
    if (!cleanVersion.empty() && cleanVersion[0] == 'v') {
        cleanVersion = cleanVersion.substr(1);
    }
    
    ss.str(cleanVersion);
    
    while (std::getline(ss, part, '.')) {
        try {
            parts.push_back(std::stoi(part));
        } catch (const std::exception&) {
            // Invalid version part, skip
            break;
        }
    }
    
    return parts;
}

} // namespace Client
} // namespace Blokus