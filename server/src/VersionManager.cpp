#include "VersionManager.h"
#include <cstdlib>
#include <ctime>
#include <sstream>
#include <algorithm>
#include <spdlog/spdlog.h>

using namespace Blokus::Server;

namespace blokus
{

    // VersionManager constructor
    VersionManager::VersionManager()
    {
        initializeServerVersion();
        loadCompatibilitySettings();

        spdlog::info("VersionManager 초기화 완료 - 서버 버전: {}, 최소 클라이언트 버전: {}",
                     serverVersion_.version, minRequiredClientVersion_);
    }

    // Version constructor
    VersionManager::Version::Version(const std::string &ver, const std::string &date,
                                     const std::string &commit, const std::string &branch, bool prod)
        : version(ver), buildDate(date), gitCommit(commit), gitBranch(branch), isProduction(prod)
    {

        // Add features based on build configuration
        features.push_back("multiplayer");
        features.push_back("chat");
        features.push_back("statistics");
        features.push_back("user_management");

        if (isProduction)
        {
            features.push_back("ssl");
            features.push_back("monitoring");
        }
        else
        {
            features.push_back("debug");
            features.push_back("development_tools");
        }
    }

    // Initialize server version from ConfigManager
    void VersionManager::initializeServerVersion()
    {
        serverVersion_ = Version(
            ConfigManager::serverVersion,
            ConfigManager::buildDate,
            ConfigManager::gitCommit,
            ConfigManager::gitBranch,
            ConfigManager::isProduction);

        spdlog::info("서버 버전 정보 - Version: {} ({}), Build: {}, Commit: {}",
                     serverVersion_.version,
                     serverVersion_.isProduction ? "Production" : "Development",
                     serverVersion_.buildDate,
                     serverVersion_.gitCommit);
    }

    // Load compatibility settings from ConfigManager
    void VersionManager::loadCompatibilitySettings()
    {
        minRequiredClientVersion_ = ConfigManager::minClientVersion;
        downloadUrl_ = ConfigManager::downloadUrl;
        forceUpdateEnabled_ = ConfigManager::forceUpdate;
        gracePeriodHours_ = ConfigManager::updateGracePeriodHours;
    }

    bool VersionManager::Version::isCompatibleWith(const std::string &clientVersion) const
    {
        // Simple version compatibility check
        // For now, require exact major version match
        auto serverParts = VersionManager::parseVersion(version);
        auto clientParts = VersionManager::parseVersion(clientVersion);

        if (serverParts.empty() || clientParts.empty())
        {
            return false;
        }

        // Major version must match
        return serverParts[0] == clientParts[0];
    }

    int VersionManager::Version::compare(const std::string &otherVersion) const
    {
        auto thisParts = VersionManager::parseVersion(version);
        auto otherParts = VersionManager::parseVersion(otherVersion);

        size_t maxSize = std::max(thisParts.size(), otherParts.size());

        for (size_t i = 0; i < maxSize; ++i)
        {
            int thisNum = (i < thisParts.size()) ? thisParts[i] : 0;
            int otherNum = (i < otherParts.size()) ? otherParts[i] : 0;

            if (thisNum < otherNum)
                return -1;
            if (thisNum > otherNum)
                return 1;
        }

        return 0; // Equal
    }

    VersionManager::CompatibilityInfo VersionManager::checkCompatibility(
        const std::string &clientVersion, const std::string &platform) const
    {

        CompatibilityInfo info;
        info.minRequiredVersion = minRequiredClientVersion_;
        info.downloadUrl = downloadUrl_;
        info.gracePeriodHours = gracePeriodHours_;
        info.forceUpdate = forceUpdateEnabled_;

        // Check if client version meets minimum requirement
        bool meetsMinimum = isVersionNewer(clientVersion, minRequiredClientVersion_) ||
                            (clientVersion == minRequiredClientVersion_);

        // Check server compatibility
        bool serverCompatible = serverVersion_.isCompatibleWith(clientVersion);

        info.compatible = meetsMinimum && serverCompatible;
        info.updateRequired = !meetsMinimum;

        // Check if newer version is available (recommend update)
        info.updateRecommended = serverVersion_.compare(clientVersion) > 0;

        // Generate appropriate message
        if (!info.compatible)
        {
            if (!meetsMinimum)
            {
                info.message = "클라이언트 버전이 너무 낮습니다. 최소 " +
                               minRequiredClientVersion_ + " 버전이 필요합니다.";
            }
            else
            {
                info.message = "서버와 호환되지 않는 클라이언트 버전입니다.";
            }
        }
        else if (info.updateRecommended)
        {
            info.message = "새로운 클라이언트 버전이 있습니다. 업데이트를 권장합니다.";
        }
        else
        {
            info.message = "클라이언트가 최신 버전입니다.";
        }

        spdlog::debug("Version check - Client: {}, Compatible: {}, Update Required: {}",
                      clientVersion, info.compatible, info.updateRequired);

        return info;
    }

    bool VersionManager::isVersionNewer(const std::string &version1, const std::string &version2)
    {
        auto parts1 = parseVersion(version1);
        auto parts2 = parseVersion(version2);

        size_t maxSize = std::max(parts1.size(), parts2.size());

        for (size_t i = 0; i < maxSize; ++i)
        {
            int num1 = (i < parts1.size()) ? parts1[i] : 0;
            int num2 = (i < parts2.size()) ? parts2[i] : 0;

            if (num1 > num2)
                return true;
            if (num1 < num2)
                return false;
        }

        return false; // Equal versions
    }

    std::vector<int> VersionManager::parseVersion(const std::string &version)
    {
        std::vector<int> parts;
        std::stringstream ss(version);
        std::string part;

        // Handle 'v' prefix
        std::string cleanVersion = version;
        if (!cleanVersion.empty() && cleanVersion[0] == 'v')
        {
            cleanVersion = cleanVersion.substr(1);
        }

        ss.str(cleanVersion);

        while (std::getline(ss, part, '.'))
        {
            try
            {
                parts.push_back(std::stoi(part));
            }
            catch (const std::exception &)
            {
                // Invalid version part, skip
                break;
            }
        }

        return parts;
    }

} // namespace blokus