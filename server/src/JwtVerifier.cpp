#include "JwtVerifier.h"
#include <spdlog/spdlog.h>
#include <jwt-cpp/jwt.h>
#include <cpr/cpr.h>
#include <nlohmann/json.hpp>
#include <thread>
#include <regex>
#include <vector>
#include <cstdint>

namespace Blokus
{
    namespace Server
    {

        JwtVerifier::JwtVerifier(const std::string &jwksUrl,
                                 const std::string &issuer,
                                 const std::vector<std::string> &audiences)
            : m_jwksUrl(jwksUrl), m_issuer(issuer), m_audiences(audiences)
        {
            spdlog::info("JwtVerifier 생성 - JWKS URL: {}, Issuer: {}", jwksUrl, issuer);
        }

        JwtVerifier::~JwtVerifier()
        {
            shutdown();
            spdlog::info("JwtVerifier 소멸");
        }

        bool JwtVerifier::initialize()
        {
            if (m_isInitialized.load())
            {
                return true;
            }

            try
            {
                spdlog::info("JwtVerifier 초기화 시작");

                // 초기 JWKS 캐시
                if (!refreshJwksCache())
                {
                    spdlog::error("초기 JWKS 캐시 실패");
                    return false;
                }

                // 백그라운드 새로고침 시작
                startBackgroundRefresh();

                m_isInitialized = true;
                spdlog::debug("JwtVerifier 초기화 완료 - 캐시된 키 개수: {}", getCachedKeyCount());
                return true;
            }
            catch (const std::exception &e)
            {
                spdlog::error("JwtVerifier 초기화 실패: {}", e.what());
                return false;
            }
        }

        void JwtVerifier::shutdown()
        {
            if (!m_isInitialized.load())
            {
                return;
            }

            m_shouldStop = true;
            stopBackgroundRefresh();

            std::lock_guard<std::mutex> lock(m_keysMutex);
            m_cachedKeys.clear();

            m_isInitialized = false;
            spdlog::info("JwtVerifier 종료 완료");
        }

        JwtVerificationResult JwtVerifier::verifyToken(const std::string &token)
        {
            if (!m_isInitialized.load())
            {
                return JwtVerificationResult(false, "JWT verifier not initialized");
            }

            try
            {
                // 토큰에서 kid 추출
                auto kidOpt = extractKidFromToken(token);
                if (!kidOpt)
                {
                    return JwtVerificationResult(false, "Failed to extract kid from token header");
                }

                const std::string kid = *kidOpt;
                spdlog::debug("JWT 토큰 검증 시작 - kid: {}", kid);

                // 해당 키 조회
                auto keyOpt = getKey(kid);
                if (!keyOpt)
                {
                    // 캐시 새로고침 후 재시도
                    spdlog::info("키를 찾을 수 없음, JWKS 캐시 새로고침 중... kid: {}", kid);
                    if (refreshJwksCache())
                    {
                        keyOpt = getKey(kid);
                    }

                    if (!keyOpt)
                    {
                        return JwtVerificationResult(false, "Key not found for kid: " + kid);
                    }
                }

                // JWT 토큰 검증
                return verifyTokenWithKey(token, *keyOpt);
            }
            catch (const std::exception &e)
            {
                spdlog::error("JWT 토큰 검증 중 오류: {}", e.what());
                return JwtVerificationResult(false, "Token verification error: " + std::string(e.what()));
            }
        }

        bool JwtVerifier::refreshJwksCache()
        {
            try
            {
                spdlog::debug("JWKS 캐시 새로고침 시작");

                auto jwksJsonOpt = fetchJwks();
                if (!jwksJsonOpt)
                {
                    spdlog::error("JWKS 페치 실패");
                    return false;
                }

                if (!parseAndCacheJwks(*jwksJsonOpt))
                {
                    spdlog::error("JWKS 파싱 및 캐시 실패");
                    return false;
                }

                spdlog::debug("JWKS 캐시 새로고침 완료 - 키 개수: {}", getCachedKeyCount());
                return true;
            }
            catch (const std::exception &e)
            {
                spdlog::error("JWKS 캐시 새로고침 실패: {}", e.what());
                return false;
            }
        }

        size_t JwtVerifier::getCachedKeyCount() const
        {
            std::lock_guard<std::mutex> lock(m_keysMutex);
            return m_cachedKeys.size();
        }

        std::optional<std::string> JwtVerifier::fetchJwks()
        {
            try
            {
                spdlog::debug("JWKS 페치 중: {}", m_jwksUrl);

                auto response = cpr::Get(
                    cpr::Url{m_jwksUrl},
                    cpr::Timeout{5000}, // 5초 타임아웃
                    cpr::Header{
                        {"User-Agent", "BlokusServer/1.0"},
                        {"Accept", "application/json"}});

                if (response.status_code != 200)
                {
                    spdlog::error("JWKS 페치 실패 - HTTP {}: {}", response.status_code, response.error.message);
                    return std::nullopt;
                }

                if (response.text.empty())
                {
                    spdlog::error("JWKS 응답이 비어있음");
                    return std::nullopt;
                }

                spdlog::debug("JWKS 페치 성공 - 크기: {} bytes", response.text.size());
                return response.text;
            }
            catch (const std::exception &e)
            {
                spdlog::error("JWKS 페치 중 예외: {}", e.what());
                return std::nullopt;
            }
        }

        bool JwtVerifier::parseAndCacheJwks(const std::string &jwksJson)
        {
            try
            {
                auto jwks = nlohmann::json::parse(jwksJson);

                if (!jwks.contains("keys") || !jwks["keys"].is_array())
                {
                    spdlog::error("JWKS 형식 오류: keys 배열이 없음");
                    return false;
                }

                std::lock_guard<std::mutex> lock(m_keysMutex);
                m_cachedKeys.clear();

                auto now = std::chrono::system_clock::now();

                for (const auto &keyJson : jwks["keys"])
                {
                    try
                    {
                        JwksKey key;
                        key.kid = keyJson.value("kid", "");
                        key.kty = keyJson.value("kty", "");
                        key.use = keyJson.value("use", "");
                        key.alg = keyJson.value("alg", "");
                        key.n = keyJson.value("n", "");
                        key.e = keyJson.value("e", "");
                        key.cached_at = now;

                        // 필수 필드 확인
                        if (key.kid.empty() || key.kty != "RSA" || key.n.empty() || key.e.empty())
                        {
                            spdlog::warn("키 스킵됨 - 필수 필드 누락: kid={}, kty={}", key.kid, key.kty);
                            continue;
                        }

                        m_cachedKeys[key.kid] = std::move(key);
                        spdlog::debug("키 캐시됨: kid={}, alg={}", key.kid, key.alg);
                    }
                    catch (const std::exception &e)
                    {
                        spdlog::warn("키 파싱 실패: {}", e.what());
                        continue;
                    }
                }

                m_lastCacheUpdate = now;
                spdlog::debug("JWKS 파싱 완료 - 총 {} 개 키 캐시됨", m_cachedKeys.size());
                return !m_cachedKeys.empty();
            }
            catch (const std::exception &e)
            {
                spdlog::error("JWKS 파싱 실패: {}", e.what());
                return false;
            }
        }

        std::optional<JwksKey> JwtVerifier::getKey(const std::string &kid)
        {
            std::lock_guard<std::mutex> lock(m_keysMutex);

            auto it = m_cachedKeys.find(kid);
            if (it != m_cachedKeys.end())
            {
                return it->second;
            }

            return std::nullopt;
        }

        bool JwtVerifier::isKeyCacheValid() const
        {
            std::lock_guard<std::mutex> lock(m_keysMutex);

            auto now = std::chrono::system_clock::now();
            auto elapsed = std::chrono::duration_cast<std::chrono::minutes>(now - m_lastCacheUpdate);

            return elapsed < m_cacheExpiration;
        }

        std::optional<std::string> JwtVerifier::extractKidFromToken(const std::string &token)
        {
            try
            {
                // JWT 헤더 파트 추출 (첫 번째 '.' 이전)
                size_t firstDot = token.find('.');
                if (firstDot == std::string::npos)
                {
                    return std::nullopt;
                }

                std::string headerB64 = token.substr(0, firstDot);
                std::string headerJson = base64UrlDecode(headerB64);

                auto header = nlohmann::json::parse(headerJson);

                if (header.contains("kid") && header["kid"].is_string())
                {
                    return header["kid"].get<std::string>();
                }

                return std::nullopt;
            }
            catch (const std::exception &e)
            {
                spdlog::error("토큰에서 kid 추출 실패: {}", e.what());
                return std::nullopt;
            }
        }

        JwtVerificationResult JwtVerifier::verifyTokenWithKey(const std::string &token, const JwksKey &key)
        {
            try
            {
                // JWK → PEM 공개키 변환
                std::string publicKeyPem = jwkToPem(key.n, key.e);
                spdlog::debug("JWK를 PEM으로 변환 완료 - kid: {}", key.kid);

                // 1) 먼저 서명/발급자(issuer)만 프레임워크 검증
                auto decoded = jwt::decode(token);

                auto verifier = jwt::verify()
                                    .allow_algorithm(jwt::algorithm::rs256(publicKeyPem, "", "", ""))
                                    .with_issuer(m_issuer);

                verifier.verify(decoded); // 서명·iss·기본 타임클레임 검증

                const auto &tokenAudSet = decoded.get_audience();

                if (!m_audiences.empty())
                {
                    bool anyMatched = false;

                    // 토큰에 aud가 아예 없으면 바로 실패
                    if (tokenAudSet.empty())
                    {
                        return JwtVerificationResult(false, "Token doesn't contain any audience");
                    }

                    for (const auto &allowed : m_audiences)
                    {
                        if (tokenAudSet.count(allowed) > 0)
                        {
                            anyMatched = true;
                            break;
                        }
                    }

                    if (!anyMatched)
                    {
                        // 디버깅 편의를 위한 상세 메시지
                        std::string tokenAudJoined;
                        for (auto it = tokenAudSet.begin(); it != tokenAudSet.end(); ++it)
                        {
                            if (it != tokenAudSet.begin())
                                tokenAudJoined += ", ";
                            tokenAudJoined += *it;
                        }

                        std::string allowedAudJoined;
                        for (size_t i = 0; i < m_audiences.size(); ++i)
                        {
                            if (i > 0)
                                allowedAudJoined += ", ";
                            allowedAudJoined += m_audiences[i];
                        }

                        return JwtVerificationResult(
                            false,
                            "Token doesn't contain the required audience (token aud: [" +
                                tokenAudJoined + "], allowed: [" + allowedAudJoined + "])");
                    }
                }
                // m_audiences가 비어있다면 aud 검증은 스킵(호환 유지)

                // 3) 클레임 추출
                JwtClaims claims;
                claims.sub = decoded.get_payload_claim("sub").as_string();
                claims.iss = decoded.get_issuer();

                // aud는 첫 원소를 대표로 저장(필요시 배열 전체를 추가 필드로 확장 가능)
                claims.aud = tokenAudSet.empty() ? "" : *tokenAudSet.begin();

                if (decoded.has_payload_claim("preferred_username"))
                {
                    claims.preferred_username = decoded.get_payload_claim("preferred_username").as_string();
                }
                if (decoded.has_payload_claim("email"))
                {
                    claims.email = decoded.get_payload_claim("email").as_string();
                }

                claims.iat = decoded.get_issued_at();
                claims.exp = decoded.get_expires_at();
                if (decoded.has_not_before())
                {
                    claims.nbf = decoded.get_not_before();
                }
                claims.kid = key.kid;

                // 4) 유예 기간(grace period) 고려한 추가 만료/nbf 체크
                auto now = std::chrono::system_clock::now();
                if (claims.exp + m_gracePeriod < now)
                {
                    return JwtVerificationResult(false, "Token expired beyond grace period");
                }
                if (claims.nbf > now + m_gracePeriod)
                {
                    return JwtVerificationResult(false, "Token not yet valid");
                }

                JwtVerificationResult result(true);
                result.claims = std::move(claims);

                spdlog::debug("JWT 검증 성공 - sub: {}, username: {}",
                              result.claims->sub,
                              result.claims->preferred_username);

                return result;
            }
            catch (const std::exception &e)
            {
                std::string error_msg = e.what();
                if (error_msg.find("expired") != std::string::npos)
                {
                    return JwtVerificationResult(false, "Token expired: " + error_msg);
                }
                else if (error_msg.find("signature") != std::string::npos)
                {
                    return JwtVerificationResult(false, "Signature verification failed: " + error_msg);
                }
                else
                {
                    return JwtVerificationResult(false, "Token verification failed: " + error_msg);
                }
            }
        }

        std::string JwtVerifier::base64UrlDecode(const std::string &input)
        {
            try
            {
                // Base64 URL 패딩 정규화
                std::string paddedInput = input;

                // Base64 URL 문자를 Base64 문자로 변환
                std::replace(paddedInput.begin(), paddedInput.end(), '-', '+');
                std::replace(paddedInput.begin(), paddedInput.end(), '_', '/');

                // 필요한 경우 패딩 추가
                size_t remainder = paddedInput.length() % 4;
                if (remainder > 0)
                {
                    paddedInput.append(4 - remainder, '=');
                }

                return jwt::base::decode<jwt::alphabet::base64>(paddedInput);
            }
            catch (const std::exception &e)
            {
                spdlog::error("Base64 URL 디코딩 실패: {} (입력: {})", e.what(), input);
                throw;
            }
        }

        std::string JwtVerifier::jwkToPem(const std::string &n, const std::string &e)
        {
            try
            {
                // Base64 URL 디코딩
                std::string n_binary = base64UrlDecode(n);
                std::string e_binary = base64UrlDecode(e);

                spdlog::debug("RSA 컴포넌트 디코딩 완료 - n: {} bytes, e: {} bytes", n_binary.size(), e_binary.size());

                // ASN.1 길이 인코딩 헬퍼 함수
                auto encodeLength = [](std::vector<uint8_t> &der, size_t len)
                {
                    if (len < 128)
                    {
                        der.push_back(static_cast<uint8_t>(len));
                    }
                    else if (len <= 0xFF)
                    {
                        der.push_back(0x81); // 1바이트로 길이 표현
                        der.push_back(static_cast<uint8_t>(len));
                    }
                    else if (len <= 0xFFFF)
                    {
                        der.push_back(0x82); // 2바이트로 길이 표현
                        der.push_back(static_cast<uint8_t>((len >> 8) & 0xFF));
                        der.push_back(static_cast<uint8_t>(len & 0xFF));
                    }
                    else if (len <= 0xFFFFFF)
                    {
                        der.push_back(0x83); // 3바이트로 길이 표현
                        der.push_back(static_cast<uint8_t>((len >> 16) & 0xFF));
                        der.push_back(static_cast<uint8_t>((len >> 8) & 0xFF));
                        der.push_back(static_cast<uint8_t>(len & 0xFF));
                    }
                    else
                    {
                        throw std::runtime_error("길이가 너무 큽니다");
                    }
                };

                // INTEGER 인코딩 헬퍼 함수
                auto encodeInteger = [&](std::vector<uint8_t> &der, const std::string &value)
                {
                    der.push_back(0x02); // INTEGER 태그

                    // 음수로 해석되지 않도록 필요시 0x00 패딩 추가
                    bool need_padding = !value.empty() && (static_cast<uint8_t>(value[0]) & 0x80) != 0;
                    size_t content_len = value.size() + (need_padding ? 1 : 0);

                    encodeLength(der, content_len);

                    if (need_padding)
                    {
                        der.push_back(0x00);
                    }
                    der.insert(der.end(), value.begin(), value.end());
                };

                // 먼저 내용물을 임시 벡터에 생성 (전체 길이 계산을 위해)
                std::vector<uint8_t> content;
                encodeInteger(content, n_binary); // modulus
                encodeInteger(content, e_binary); // exponent

                // 최종 DER 구조 생성
                std::vector<uint8_t> der;
                der.push_back(0x30); // SEQUENCE 태그
                encodeLength(der, content.size());
                der.insert(der.end(), content.begin(), content.end());

                // DER을 Base64로 인코딩
                std::string der_base64 = jwt::base::encode<jwt::alphabet::base64>(
                    std::string(der.begin(), der.end()));

                // PEM 형식으로 포맷
                std::string pem = "-----BEGIN RSA PUBLIC KEY-----\n";

                // 64자씩 줄바꿈
                for (size_t i = 0; i < der_base64.length(); i += 64)
                {
                    pem += der_base64.substr(i, 64) + "\n";
                }

                pem += "-----END RSA PUBLIC KEY-----\n";

                spdlog::debug("PEM 형식 변환 완료 - DER 길이: {} bytes, PEM 길이: {} bytes", der.size(), pem.size());
                return pem;
            }
            catch (const std::exception &e)
            {
                spdlog::error("JWK를 PEM으로 변환 실패: {}", e.what());
                throw;
            }
        }

        void JwtVerifier::startBackgroundRefresh()
        {
            m_shouldStop = false;
            m_refreshThread = std::make_unique<std::thread>(&JwtVerifier::backgroundRefreshWorker, this);
        }

        void JwtVerifier::stopBackgroundRefresh()
        {
            m_shouldStop = true;
            if (m_refreshThread && m_refreshThread->joinable())
            {
                m_refreshThread->join();
            }
            m_refreshThread.reset();
        }

        void JwtVerifier::backgroundRefreshWorker()
        {
            spdlog::info("JWKS 백그라운드 새로고침 워커 시작");

            while (!m_shouldStop)
            {
                try
                {
                    // 5분마다 체크
                    std::this_thread::sleep_for(std::chrono::minutes(5));

                    if (m_shouldStop)
                        break;

                    // 캐시가 만료되었으면 새로고침
                    if (!isKeyCacheValid())
                    {
                        spdlog::debug("JWKS 캐시 만료, 백그라운드 새로고침 실행");
                        refreshJwksCache();
                    }
                }
                catch (const std::exception &e)
                {
                    spdlog::error("백그라운드 새로고침 오류: {}", e.what());
                    // 오류가 발생해도 계속 실행
                }
            }

            spdlog::info("JWKS 백그라운드 새로고침 워커 종료");
        }

    } // namespace Server
} // namespace Blokus