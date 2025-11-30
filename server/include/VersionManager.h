#pragma once

#include <string>
#include <memory>
#include <vector>
#include "ServerTypes.h"
#include "ConfigManager.h"

namespace Blokus::Server
{

    class VersionManager
    {
    public:
        // 버전 정보 구조체
        struct Version
        {
            std::string version;
            std::string buildDate;
            std::string gitCommit;
            std::string gitBranch;
            bool isProduction;
            std::vector<std::string> features;

            Version() = default;
            Version(const std::string &ver, const std::string &date, const std::string &commit,
                    const std::string &branch, bool prod);

            bool isCompatibleWith(const std::string &clientVersion) const;
            int compare(const std::string &otherVersion) const;
        };

        struct CompatibilityInfo
        {
            bool compatible;
            std::string message;
            std::string downloadUrl;
        };

        explicit VersionManager();

        // 현재 서버 버전 반환
        const Version &getServerVersion() const { return serverVersion_; }

        // 클라이언트 버전 호환 여부 확인
        CompatibilityInfo checkCompatibility(const std::string &clientVersion,
                                             const std::string &platform = "Windows") const;

        static bool isVersionNewer(const std::string &version1, const std::string &version2);
        static std::vector<int> parseVersion(const std::string &version);
        const std::string getDownloadURL() const { return downloadUrl_; }

    private:
        Version serverVersion_;
        std::string downloadUrl_;

        void initializeServerVersion();
        void loadCompatibilitySettings();
    };

} // namespace blokus