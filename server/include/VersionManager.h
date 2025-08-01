#pragma once

#include <string>
#include <memory>
#include <vector>
#include "ServerTypes.h"
#include "ConfigManager.h"

namespace blokus {

class VersionManager {
public:
    // Version information structure
    struct Version {
        std::string version;
        std::string buildDate;
        std::string gitCommit;
        std::string gitBranch;
        bool isProduction;
        std::vector<std::string> features;
        
        Version() = default;
        Version(const std::string& ver, const std::string& date, const std::string& commit,
                const std::string& branch, bool prod);
        
        bool isCompatibleWith(const std::string& clientVersion) const;
        int compare(const std::string& otherVersion) const;
    };
    
    struct CompatibilityInfo {
        bool compatible;
        std::string message;
        std::string downloadUrl;
    };
    
    // Constructor - takes configuration from ConfigManager
    explicit VersionManager();
    
    // Get current server version
    const Version& getServerVersion() const { return serverVersion_; }
    
    // Check client compatibility
    CompatibilityInfo checkCompatibility(const std::string& clientVersion, 
                                        const std::string& platform = "Windows") const;
    
    // Version comparison utilities (static for utility use)
    static bool isVersionNewer(const std::string& version1, const std::string& version2);
    static std::vector<int> parseVersion(const std::string& version);
    
private:
    Version serverVersion_;
    std::string downloadUrl_;
    
    // Helper methods
    void initializeServerVersion();
    void loadCompatibilitySettings();
};

} // namespace blokus