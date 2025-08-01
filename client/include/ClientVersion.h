#pragma once

#include <string>
#include <vector>

namespace Blokus {
namespace Client {

// 클라이언트 버전 정보
class ClientVersion {
public:
    // 클라이언트 버전 상수 (하드코딩)
    static constexpr const char* VERSION = "1.1.0";
    static constexpr const char* BUILD_DATE = __DATE__ " " __TIME__;
    
    // 버전 정보 반환
    static std::string getVersion() { return VERSION; }
    static std::string getBuildDate() { return BUILD_DATE; }
    
    // 버전 호환성 확인 (Major.Minor 매칭)
    static bool isCompatibleWith(const std::string& serverVersion);
    
    // 버전 문자열 파싱
    static std::vector<int> parseVersion(const std::string& version);
    
private:
    ClientVersion() = delete; // 유틸리티 클래스
};

} // namespace Client
} // namespace Blokus