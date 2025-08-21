#include "JwtVerifier.h"
#include <spdlog/spdlog.h>
#include <jwt-cpp/jwt.h>
#include <cpr/cpr.h>
#include <nlohmann/json.hpp>
#include <thread>
#include <regex>

namespace Blokus {
    namespace Server {

        JwtVerifier::JwtVerifier(const std::string& jwksUrl, 
                               const std::string& issuer, 
                               const std::vector<std::string>& audiences)
            : m_jwksUrl(jwksUrl)
            , m_issuer(issuer)
            , m_audiences(audiences)
        {
            spdlog::info("JwtVerifier 생성 - JWKS URL: {}, Issuer: {}", jwksUrl, issuer);
        }

        JwtVerifier::~JwtVerifier() {
            shutdown();
            spdlog::info("JwtVerifier 소멸");
        }

        bool JwtVerifier::initialize() {
            if (m_isInitialized.load()) {
                return true;
            }

            try {
                spdlog::info("JwtVerifier 초기화 시작");

                // 초기 JWKS 캐시
                if (!refreshJwksCache()) {
                    spdlog::error("초기 JWKS 캐시 실패");
                    return false;
                }

                // 백그라운드 새로고침 시작
                startBackgroundRefresh();

                m_isInitialized = true;
                spdlog::info("JwtVerifier 초기화 완료 - 캐시된 키 개수: {}", getCachedKeyCount());
                return true;

            } catch (const std::exception& e) {
                spdlog::error("JwtVerifier 초기화 실패: {}", e.what());
                return false;
            }
        }

        void JwtVerifier::shutdown() {
            if (!m_isInitialized.load()) {
                return;
            }

            m_shouldStop = true;
            stopBackgroundRefresh();

            std::lock_guard<std::mutex> lock(m_keysMutex);
            m_cachedKeys.clear();

            m_isInitialized = false;
            spdlog::info("JwtVerifier 종료 완료");
        }

        JwtVerificationResult JwtVerifier::verifyToken(const std::string& token) {
            if (!m_isInitialized.load()) {
                return JwtVerificationResult(false, "JWT verifier not initialized");
            }

            try {
                // 토큰에서 kid 추출
                auto kidOpt = extractKidFromToken(token);
                if (!kidOpt) {
                    return JwtVerificationResult(false, "Failed to extract kid from token header");
                }

                const std::string kid = *kidOpt;
                spdlog::debug("JWT 토큰 검증 시작 - kid: {}", kid);

                // 해당 키 조회
                auto keyOpt = getKey(kid);
                if (!keyOpt) {
                    // 캐시 새로고침 후 재시도
                    spdlog::info("키를 찾을 수 없음, JWKS 캐시 새로고침 중... kid: {}", kid);
                    if (refreshJwksCache()) {
                        keyOpt = getKey(kid);
                    }
                    
                    if (!keyOpt) {
                        return JwtVerificationResult(false, "Key not found for kid: " + kid);
                    }
                }

                // JWT 토큰 검증
                return verifyTokenWithKey(token, *keyOpt);

            } catch (const std::exception& e) {
                spdlog::error("JWT 토큰 검증 중 오류: {}", e.what());
                return JwtVerificationResult(false, "Token verification error: " + std::string(e.what()));
            }
        }

        bool JwtVerifier::refreshJwksCache() {
            try {
                spdlog::debug("JWKS 캐시 새로고침 시작");

                auto jwksJsonOpt = fetchJwks();
                if (!jwksJsonOpt) {
                    spdlog::error("JWKS 페치 실패");
                    return false;
                }

                if (!parseAndCacheJwks(*jwksJsonOpt)) {
                    spdlog::error("JWKS 파싱 및 캐시 실패");
                    return false;
                }

                spdlog::info("JWKS 캐시 새로고침 완료 - 키 개수: {}", getCachedKeyCount());
                return true;

            } catch (const std::exception& e) {
                spdlog::error("JWKS 캐시 새로고침 실패: {}", e.what());
                return false;
            }
        }

        size_t JwtVerifier::getCachedKeyCount() const {
            std::lock_guard<std::mutex> lock(m_keysMutex);
            return m_cachedKeys.size();
        }

        std::optional<std::string> JwtVerifier::fetchJwks() {
            try {
                spdlog::debug("JWKS 페치 중: {}", m_jwksUrl);

                auto response = cpr::Get(
                    cpr::Url{ m_jwksUrl },
                    cpr::Timeout{ 5000 }, // 5초 타임아웃
                    cpr::Header{
                        {"User-Agent", "BlokusServer/1.0"},
                        {"Accept", "application/json"}
                    }
                );

                if (response.status_code != 200) {
                    spdlog::error("JWKS 페치 실패 - HTTP {}: {}", response.status_code, response.error.message);
                    return std::nullopt;
                }

                if (response.text.empty()) {
                    spdlog::error("JWKS 응답이 비어있음");
                    return std::nullopt;
                }

                spdlog::debug("JWKS 페치 성공 - 크기: {} bytes", response.text.size());
                return response.text;

            } catch (const std::exception& e) {
                spdlog::error("JWKS 페치 중 예외: {}", e.what());
                return std::nullopt;
            }
        }

        bool JwtVerifier::parseAndCacheJwks(const std::string& jwksJson) {
            try {
                auto jwks = nlohmann::json::parse(jwksJson);
                
                if (!jwks.contains("keys") || !jwks["keys"].is_array()) {
                    spdlog::error("JWKS 형식 오류: keys 배열이 없음");
                    return false;
                }

                std::lock_guard<std::mutex> lock(m_keysMutex);
                m_cachedKeys.clear();

                auto now = std::chrono::system_clock::now();

                for (const auto& keyJson : jwks["keys"]) {
                    try {
                        JwksKey key;
                        key.kid = keyJson.value("kid", "");
                        key.kty = keyJson.value("kty", "");
                        key.use = keyJson.value("use", "");
                        key.alg = keyJson.value("alg", "");
                        key.n = keyJson.value("n", "");
                        key.e = keyJson.value("e", "");
                        key.cached_at = now;

                        // 필수 필드 확인
                        if (key.kid.empty() || key.kty != "RSA" || key.n.empty() || key.e.empty()) {
                            spdlog::warn("키 스킵됨 - 필수 필드 누락: kid={}, kty={}", key.kid, key.kty);
                            continue;
                        }

                        m_cachedKeys[key.kid] = std::move(key);
                        spdlog::debug("키 캐시됨: kid={}, alg={}", key.kid, key.alg);

                    } catch (const std::exception& e) {
                        spdlog::warn("키 파싱 실패: {}", e.what());
                        continue;
                    }
                }

                m_lastCacheUpdate = now;
                spdlog::info("JWKS 파싱 완료 - 총 {} 개 키 캐시됨", m_cachedKeys.size());
                return !m_cachedKeys.empty();

            } catch (const std::exception& e) {
                spdlog::error("JWKS 파싱 실패: {}", e.what());
                return false;
            }
        }

        std::optional<JwksKey> JwtVerifier::getKey(const std::string& kid) {
            std::lock_guard<std::mutex> lock(m_keysMutex);
            
            auto it = m_cachedKeys.find(kid);
            if (it != m_cachedKeys.end()) {
                return it->second;
            }
            
            return std::nullopt;
        }

        bool JwtVerifier::isKeyCacheValid() const {
            std::lock_guard<std::mutex> lock(m_keysMutex);
            
            auto now = std::chrono::system_clock::now();
            auto elapsed = std::chrono::duration_cast<std::chrono::minutes>(now - m_lastCacheUpdate);
            
            return elapsed < m_cacheExpiration;
        }

        std::optional<std::string> JwtVerifier::extractKidFromToken(const std::string& token) {
            try {
                // JWT 헤더 파트 추출 (첫 번째 '.' 이전)
                size_t firstDot = token.find('.');
                if (firstDot == std::string::npos) {
                    return std::nullopt;
                }

                std::string headerB64 = token.substr(0, firstDot);
                std::string headerJson = base64UrlDecode(headerB64);
                
                auto header = nlohmann::json::parse(headerJson);
                
                if (header.contains("kid") && header["kid"].is_string()) {
                    return header["kid"].get<std::string>();
                }

                return std::nullopt;

            } catch (const std::exception& e) {
                spdlog::error("토큰에서 kid 추출 실패: {}", e.what());
                return std::nullopt;
            }
        }

        JwtVerificationResult JwtVerifier::verifyTokenWithKey(const std::string& token, const JwksKey& key) {
            try {
                // JWT 검증자 생성 (키 컴포넌트를 직접 사용)
                auto verifier = jwt::verify()
                    .allow_algorithm(jwt::algorithm::rs256(key.n, key.e))
                    .with_issuer(m_issuer);

                // Audience 검증 추가 (복수 audience 지원)
                for (const auto& aud : m_audiences) {
                    verifier.with_audience(aud);
                }

                // 토큰 검증
                auto decoded = jwt::decode(token);
                verifier.verify(decoded);

                // 클레임 추출
                JwtClaims claims;
                claims.sub = decoded.get_payload_claim("sub").as_string();
                claims.iss = decoded.get_issuer();
                claims.aud = decoded.get_audience().empty() ? "" : *decoded.get_audience().begin();
                
                if (decoded.has_payload_claim("preferred_username")) {
                    claims.preferred_username = decoded.get_payload_claim("preferred_username").as_string();
                }
                if (decoded.has_payload_claim("email")) {
                    claims.email = decoded.get_payload_claim("email").as_string();
                }

                claims.iat = decoded.get_issued_at();
                claims.exp = decoded.get_expires_at();
                
                if (decoded.has_not_before()) {
                    claims.nbf = decoded.get_not_before();
                }
                
                claims.kid = key.kid;

                // 추가 시간 검증 (유예 기간 적용)
                auto now = std::chrono::system_clock::now();
                if (claims.exp + m_gracePeriod < now) {
                    return JwtVerificationResult(false, "Token expired beyond grace period");
                }

                if (claims.nbf > now + m_gracePeriod) {
                    return JwtVerificationResult(false, "Token not yet valid");
                }

                JwtVerificationResult result(true);
                result.claims = std::move(claims);
                
                spdlog::debug("JWT 검증 성공 - sub: {}, username: {}", 
                            result.claims->sub, result.claims->preferred_username);
                
                return result;

            } catch (const std::exception& e) {
                std::string error_msg = e.what();
                if (error_msg.find("expired") != std::string::npos) {
                    return JwtVerificationResult(false, "Token expired: " + error_msg);
                } else if (error_msg.find("signature") != std::string::npos) {
                    return JwtVerificationResult(false, "Signature verification failed: " + error_msg);
                } else {
                    return JwtVerificationResult(false, "Token verification failed: " + error_msg);
                }
            }
        }

        std::string JwtVerifier::base64UrlDecode(const std::string& input) {
            try {
                return jwt::base::decode<jwt::alphabet::base64url>(input);
            } catch (const std::exception& e) {
                spdlog::error("Base64 URL 디코딩 실패: {}", e.what());
                throw;
            }
        }

        void JwtVerifier::startBackgroundRefresh() {
            m_shouldStop = false;
            m_refreshThread = std::make_unique<std::thread>(&JwtVerifier::backgroundRefreshWorker, this);
        }

        void JwtVerifier::stopBackgroundRefresh() {
            m_shouldStop = true;
            if (m_refreshThread && m_refreshThread->joinable()) {
                m_refreshThread->join();
            }
            m_refreshThread.reset();
        }

        void JwtVerifier::backgroundRefreshWorker() {
            spdlog::info("JWKS 백그라운드 새로고침 워커 시작");

            while (!m_shouldStop) {
                try {
                    // 5분마다 체크
                    std::this_thread::sleep_for(std::chrono::minutes(5));
                    
                    if (m_shouldStop) break;

                    // 캐시가 만료되었으면 새로고침
                    if (!isKeyCacheValid()) {
                        spdlog::info("JWKS 캐시 만료, 백그라운드 새로고침 실행");
                        refreshJwksCache();
                    }

                } catch (const std::exception& e) {
                    spdlog::error("백그라운드 새로고침 오류: {}", e.what());
                    // 오류가 발생해도 계속 실행
                }
            }

            spdlog::info("JWKS 백그라운드 새로고침 워커 종료");
        }

    } // namespace Server
} // namespace Blokus