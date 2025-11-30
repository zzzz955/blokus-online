#include "VersionManager.h"
#include <cstdlib>
#include <ctime>
#include <sstream>
#include <algorithm>
#include <spdlog/spdlog.h>

namespace Blokus::Server
{
    // 버전 매니저 생성자
    VersionManager::VersionManager()
    {
        initializeServerVersion();
        loadCompatibilitySettings();

        spdlog::info("VersionManager 초기화 완료 - 서버 버전: {}, 다운로드 URL: {}",
                     serverVersion_.version, downloadUrl_);
    }

    // 버전 매니저 생성자(오버라이딩)
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

    // 환경변수 기반 서버 버전 초기화
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

    // 다운로드 페이지 URL 초기화
    void VersionManager::loadCompatibilitySettings()
    {
        downloadUrl_ = ConfigManager::downloadUrl;
    }

    bool VersionManager::Version::isCompatibleWith(const std::string &clientVersion) const
    {
        // 서버-클라이언트 버전 체크
        // 규칙: 서버 버전 X.Y.Z 중 클라이언트 버전 X.Y.0~X.Y.9와 일치
        auto serverParts = VersionManager::parseVersion(version);
        auto clientParts = VersionManager::parseVersion(clientVersion);

        if (serverParts.size() < 2 || clientParts.size() < 2)
        {
            return false;
        }

        // 반드시 아래와 같이 일치해야함
        // 예시1) 서버 1.0.0 클라이언트 1.0.0~1.0.9 지원
        // 예시2) 서버 1.1.0 클라이언트 1.1.0~1.1.9 지원
        return (serverParts[0] == clientParts[0]) && (serverParts[1] == clientParts[1]);
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

        return 0; // 일치
    }

    VersionManager::CompatibilityInfo VersionManager::checkCompatibility(
        const std::string &clientVersion, const std::string &platform) const
    {
        CompatibilityInfo info;
        info.downloadUrl = downloadUrl_;

        // 서버-클라이언트 버전 체크
        info.compatible = serverVersion_.isCompatibleWith(clientVersion);

        // 불일치할 경우 새로운 클라이언트 다운로드 필요 안내
        if (!info.compatible)
        {
            info.message = "서버 버전(" + serverVersion_.version + ")과 호환되지 않는 클라이언트 버전(" + 
                          clientVersion + ")입니다. 최신 클라이언트를 다운로드해 주세요.";
        }
        else
        {
            info.message = "클라이언트 버전이 서버와 호환됩니다.";
        }

        spdlog::debug("버전 호환성 확인 - 서버: {}, 클라이언트: {}, 호환: {}",
                      serverVersion_.version, clientVersion, info.compatible);

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

        return false;
    }

    std::vector<int> VersionManager::parseVersion(const std::string &version)
    {
        std::vector<int> parts;
        std::stringstream ss(version);
        std::string part;

        // v문자를 제외한 버전 파싱
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
                // 버전 관련 문자열이 잘못된 경우 예외 처리
                break;
            }
        }

        return parts;
    }

} // namespace blokus