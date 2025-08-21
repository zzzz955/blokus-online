#pragma once

#include <string>
#include <memory>
#include <optional>
#include <chrono>
#include <unordered_map>
#include <mutex>
#include <future>

namespace Blokus {
    namespace Server {

        // JWT 클레임 정보
        struct JwtClaims {
            std::string sub;              // Subject (user ID)
            std::string iss;              // Issuer
            std::string aud;              // Audience
            std::string preferred_username; // Username
            std::string email;            // Email
            std::chrono::system_clock::time_point iat; // Issued at
            std::chrono::system_clock::time_point exp; // Expires at
            std::chrono::system_clock::time_point nbf; // Not before
            std::string kid;              // Key ID

            JwtClaims() = default;
        };

        // JWT 검증 결과
        struct JwtVerificationResult {
            bool success;
            std::string error;
            std::optional<JwtClaims> claims;

            JwtVerificationResult(bool s = false, const std::string& err = "")
                : success(s), error(err) {}
        };

        // JWKS 공개키 정보
        struct JwksKey {
            std::string kid;              // Key ID
            std::string kty;              // Key Type (RSA)
            std::string use;              // Usage (sig)
            std::string alg;              // Algorithm (RS256)
            std::string n;                // RSA modulus
            std::string e;                // RSA exponent
            std::chrono::system_clock::time_point cached_at; // 캐시 시간

            JwksKey() = default;
        };

        // JWT 검증기 클래스
        class JwtVerifier {
        public:
            explicit JwtVerifier(const std::string& jwksUrl, 
                               const std::string& issuer, 
                               const std::vector<std::string>& audiences);
            ~JwtVerifier();

            // 초기화
            bool initialize();
            void shutdown();

            // JWT 토큰 검증
            JwtVerificationResult verifyToken(const std::string& token);

            // JWKS 캐시 새로고침 (수동)
            bool refreshJwksCache();

            // 설정
            void setCacheExpiration(std::chrono::minutes duration) { m_cacheExpiration = duration; }
            void setGracePeriod(std::chrono::seconds grace) { m_gracePeriod = grace; }

            // 상태 확인
            bool isInitialized() const { return m_isInitialized; }
            size_t getCachedKeyCount() const;

        private:
            // JWKS 페칭
            std::optional<std::string> fetchJwks();
            bool parseAndCacheJwks(const std::string& jwksJson);

            // 키 관리
            std::optional<JwksKey> getKey(const std::string& kid);
            bool isKeyCacheValid() const;

            // JWT 파싱 및 검증
            std::optional<std::string> extractKidFromToken(const std::string& token);
            JwtVerificationResult verifyTokenWithKey(const std::string& token, const JwksKey& key);

            // Base64 URL 디코딩
            std::string base64UrlDecode(const std::string& input);

            // 백그라운드 캐시 새로고침
            void startBackgroundRefresh();
            void stopBackgroundRefresh();
            void backgroundRefreshWorker();

        private:
            // 설정
            std::string m_jwksUrl;
            std::string m_issuer;
            std::vector<std::string> m_audiences;
            std::chrono::minutes m_cacheExpiration{ 10 }; // 10분 캐시
            std::chrono::seconds m_gracePeriod{ 30 };     // 30초 유예

            // 키 캐시
            mutable std::mutex m_keysMutex;
            std::unordered_map<std::string, JwksKey> m_cachedKeys;
            std::chrono::system_clock::time_point m_lastCacheUpdate;

            // 상태
            std::atomic<bool> m_isInitialized{ false };
            std::atomic<bool> m_shouldStop{ false };

            // 백그라운드 새로고침
            std::unique_ptr<std::thread> m_refreshThread;
        };

    } // namespace Server
} // namespace Blokus