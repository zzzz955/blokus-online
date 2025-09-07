const express = require('express')
const router = express.Router()
const { body, validationResult } = require('express-validator')
const { jwtVerify } = require('jose')

const oidcConfig = require('../config/oidc')
const keyManager = require('../config/keys')
const dbService = require('../config/database')
const logger = require('../config/logger')

/**
 * POST /auth/mobile/tcp-token
 * 모바일 클라이언트 TCP 서버 연결용 토큰 발급/갱신 엔드포인트
 * 
 * 전략:
 * - 네트워크 지연 엣지 케이스 방지를 위해 토큰 교체 방식 사용
 * - 현재 AccessToken을 검증하고, 필요시 갱신하여 새로운 토큰 반환
 * - TCP 서버는 항상 유효한 토큰만 받게 됨
 */
router.post('/tcp-token',
  [
    body('client_id').notEmpty().withMessage('client_id is required'),
    // Authorization 헤더에서 Bearer 토큰 추출하거나 body에서 access_token 받기
    body('access_token').optional().isLength({ min: 10 }).withMessage('Invalid access_token'),
  ],
  async (req, res) => {
    try {
      // 입력 검증
      const errors = validationResult(req)
      if (!errors.isEmpty()) {
        logger.warn('Invalid mobile TCP token request', {
          errors: errors.array(),
          ip: req.ip
        })

        return res.status(400).json({
          valid: false,
          error: 'INVALID_REQUEST',
          message: 'Invalid request parameters'
        })
      }

      const { client_id } = req.body
      
      // Access Token 추출 (Authorization 헤더 우선, body 대안)
      let accessToken = extractBearerToken(req)
      if (!accessToken && req.body.access_token) {
        accessToken = req.body.access_token
      }

      if (!accessToken) {
        return res.status(400).json({
          valid: false,
          error: 'ACCESS_TOKEN_REQUIRED',
          message: 'Access token is required in Authorization header or request body'
        })
      }

      logger.info('Mobile TCP token request received', {
        client_id,
        tokenPrefix: accessToken.substring(0, 20) + '...',
        ip: req.ip
      })

      // 클라이언트 검증
      const clientValidation = oidcConfig.validateClient(client_id)
      if (!clientValidation.valid) {
        logger.warn('Client validation failed for mobile TCP token', {
          client_id,
          error: clientValidation.error,
          ip: req.ip
        })

        return res.status(401).json({
          valid: false,
          error: 'INVALID_CLIENT',
          message: 'Client authentication failed'
        })
      }

      const client = clientValidation.client

      // JWT 토큰 검증 및 처리
      const tokenResult = await processAccessTokenForTcp(accessToken, client)
      
      logger.info('Mobile TCP token processed', {
        client_id,
        result: tokenResult.valid ? 'success' : 'failed',
        refreshed: tokenResult.refreshed || false,
        reason: tokenResult.error || 'none',
        ip: req.ip
      })

      res.json(tokenResult)

    } catch (error) {
      logger.error('Mobile TCP token endpoint error', {
        error: error.message,
        stack: error.stack,
        ip: req.ip
      })

      res.status(500).json({
        valid: false,
        error: 'SERVER_ERROR',
        message: 'Internal server error occurred'
      })
    }
  }
)

/**
 * Access Token 처리 (검증 및 필요시 갱신)
 */
async function processAccessTokenForTcp(accessToken, client) {
  try {
    // JWT 토큰 검증
    const verificationResult = await verifyAccessToken(accessToken)
    
    if (!verificationResult.valid) {
      // 토큰이 유효하지 않은 경우
      if (verificationResult.expired) {
        // 만료된 경우 -> Refresh Token으로 갱신 시도
        logger.info('Access token expired, attempting refresh', {
          client_id: client.client_id,
          sub: verificationResult.payload?.sub
        })

        return await attemptTokenRefresh(verificationResult.payload, client)
      } else {
        // 기타 오류 (서명 불일치, 형식 오류 등)
        return {
          valid: false,
          error: 'INVALID_TOKEN',
          message: 'Token is invalid or malformed'
        }
      }
    }

    // 토큰이 유효한 경우
    const payload = verificationResult.payload
    const now = Math.floor(Date.now() / 1000)
    const expiresIn = payload.exp - now

    // 만료까지 5분 미만 남은 경우 사전 갱신 (선택적)
    const REFRESH_THRESHOLD = 5 * 60 // 5분
    
    if (expiresIn < REFRESH_THRESHOLD) {
      logger.info('Access token expiring soon, proactive refresh', {
        client_id: client.client_id,
        sub: payload.sub,
        expiresIn
      })

      const refreshResult = await attemptTokenRefresh(payload, client)
      if (refreshResult.valid) {
        return refreshResult // 갱신 성공
      }
      
      // 갱신 실패해도 현재 토큰이 아직 유효하므로 사용
      logger.warn('Proactive refresh failed, using current token', {
        client_id: client.client_id,
        sub: payload.sub
      })
    }

    // 현재 토큰 사용
    return {
      valid: true,
      accessToken: accessToken,
      expiresIn: expiresIn,
      refreshed: false
    }

  } catch (error) {
    logger.error('Error processing access token for TCP', {
      error: error.message,
      stack: error.stack
    })

    return {
      valid: false,
      error: 'TOKEN_PROCESSING_ERROR',
      message: 'Failed to process access token'
    }
  }
}

/**
 * JWT Access Token 검증
 */
async function verifyAccessToken(accessToken) {
  try {
    // 키 관리자 초기화 확인
    if (!keyManager.getKid()) {
      await keyManager.initialize()
    }

    const { payload } = await jwtVerify(
      accessToken,
      keyManager.getPublicKey(),
      {
        issuer: oidcConfig.issuer,
        algorithms: ['RS256']
      }
    )

    return {
      valid: true,
      payload: payload,
      expired: false
    }

  } catch (error) {
    // JWT 검증 오류 분석
    if (error.code === 'ERR_JWT_EXPIRED') {
      // 만료된 토큰 - payload 추출 시도 (검증 없이)
      try {
        const payload = extractPayloadWithoutVerification(accessToken)
        return {
          valid: false,
          expired: true,
          payload: payload
        }
      } catch (extractError) {
        logger.debug('Failed to extract payload from expired token', {
          error: extractError.message
        })
      }
    }

    logger.debug('JWT verification failed', {
      error: error.message,
      code: error.code
    })

    return {
      valid: false,
      expired: false,
      error: error.code || 'JWT_VERIFICATION_FAILED'
    }
  }
}

/**
 * Refresh Token을 사용한 토큰 갱신 시도
 */
async function attemptTokenRefresh(payload, client) {
  try {
    if (!payload || !payload.sub) {
      return {
        valid: false,
        error: 'INVALID_PAYLOAD',
        message: 'Cannot extract user information from token'
      }
    }

    // 사용자 정보로 활성 Refresh Token 조회
    const refreshTokens = await dbService.getActiveRefreshTokens(payload.sub, client.client_id)
    
    if (!refreshTokens || refreshTokens.length === 0) {
      logger.warn('No active refresh tokens found for user', {
        sub: payload.sub,
        client_id: client.client_id
      })

      return {
        valid: false,
        error: 'REFRESH_TOKEN_EXPIRED',
        message: 'Please login again - no valid refresh tokens'
      }
    }

    // 가장 최근 Refresh Token 사용 (family_id 기준)
    const latestRefreshToken = refreshTokens[0]

    // Refresh Token으로 새 Access Token 발급
    // 이는 기존 token endpoint의 refresh_token grant를 내부적으로 호출
    const newTokens = await performInternalTokenRefresh(latestRefreshToken, client)

    if (newTokens && newTokens.access_token) {
      return {
        valid: true,
        accessToken: newTokens.access_token,
        expiresIn: newTokens.expires_in,
        refreshed: true
      }
    } else {
      return {
        valid: false,
        error: 'TOKEN_REFRESH_FAILED',
        message: 'Failed to refresh access token'
      }
    }

  } catch (error) {
    logger.error('Token refresh attempt failed', {
      error: error.message,
      stack: error.stack
    })

    return {
      valid: false,
      error: 'REFRESH_ERROR',
      message: 'Token refresh process failed'
    }
  }
}

/**
 * 내부 토큰 갱신 수행 (기존 token 로직 재사용)
 */
async function performInternalTokenRefresh(refreshTokenData, client) {
  try {
    // 실제 Refresh Token JWT 생성 필요 (DB에서는 JTI만 저장됨)
    // 이 부분은 실제 구현에서 DB 스키마에 따라 조정 필요
    
    // 사용자 정보 조회
    const user = await dbService.getUserById(refreshTokenData.user_id)
    if (!user) {
      throw new Error('User not found')
    }

    // 기존 token.js의 generateTokens 함수 재사용
    const tokenRoutes = require('./token')
    
    // 새로운 토큰 생성 (간소화된 버전)
    const tokens = await generateTokensForMobile(user, client, refreshTokenData.family_id)
    
    return tokens

  } catch (error) {
    logger.error('Internal token refresh failed', {
      error: error.message,
      familyId: refreshTokenData.family_id
    })
    return null
  }
}

/**
 * 검증 없이 JWT payload 추출
 */
function extractPayloadWithoutVerification(token) {
  const parts = token.split('.')
  if (parts.length !== 3) {
    throw new Error('Invalid JWT format')
  }

  return JSON.parse(Buffer.from(parts[1], 'base64url').toString())
}

/**
 * Authorization 헤더에서 Bearer 토큰 추출
 */
function extractBearerToken(req) {
  const authHeader = req.headers.authorization
  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    return null
  }
  return authHeader.substring(7) // 'Bearer ' 제거
}

/**
 * 모바일용 간소화된 토큰 생성
 */
async function generateTokensForMobile(user, client, familyId) {
  // token.js의 generateTokens 함수와 유사하지만 간소화
  const { generateTokens } = require('./token')
  
  return await generateTokens(
    user, 
    client, 
    'openid profile email', // 기본 scope
    familyId
  )
}

module.exports = router