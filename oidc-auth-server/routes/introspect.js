const express = require('express')
const router = express.Router()
const { body, validationResult } = require('express-validator')
const { jwtVerify } = require('jose')

const oidcConfig = require('../config/oidc')
const keyManager = require('../config/keys')
const dbService = require('../config/database')
const logger = require('../config/logger')

/**
 * POST /introspect
 * RFC 7662: OAuth 2.0 Token Introspection
 * 토큰의 유효성과 메타데이터를 확인
 */
router.post('/',
  [
    body('token').notEmpty().withMessage('token is required'),
    body('token_type_hint').optional().isIn(['access_token', 'refresh_token']).withMessage('Invalid token_type_hint'),
    body('client_id').notEmpty().withMessage('client_id is required'),
    body('client_secret').optional()
  ],
  async (req, res) => {
    try {
      // 입력 검증
      const errors = validationResult(req)
      if (!errors.isEmpty()) {
        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'Invalid request parameters'
        })
      }

      const { token, token_type_hint, client_id, client_secret } = req.body

      logger.info('Token introspection request', {
        client_id,
        token_type_hint,
        ip: req.ip
      })

      // 클라이언트 검증
      const clientValidation = oidcConfig.validateClient(client_id, client_secret)
      if (!clientValidation.valid) {
        logger.warn('Client validation failed for introspection', {
          client_id,
          error: clientValidation.error,
          ip: req.ip
        })

        return res.status(401).json({
          error: clientValidation.error,
          error_description: 'Client authentication failed'
        })
      }

      // 키 관리자 초기화 확인
      if (!keyManager.getKid()) {
        await keyManager.initialize()
      }

      // 토큰 검증 및 정보 추출
      const introspectionResult = await introspectToken(token, token_type_hint)

      logger.debug('Token introspection completed', {
        client_id,
        active: introspectionResult.active,
        token_type: introspectionResult.token_type,
        ip: req.ip
      })

      res.json(introspectionResult)

    } catch (error) {
      logger.error('Token introspection error', {
        error: error.message,
        stack: error.stack,
        ip: req.ip
      })

      // RFC 7662: 에러가 발생해도 active: false로 응답
      res.json({
        active: false
      })
    }
  }
)

/**
 * 토큰 검증 및 메타데이터 추출
 */
async function introspectToken(token, tokenTypeHint) {
  try {
    // JWT 검증
    const { payload, protectedHeader } = await jwtVerify(
      token,
      keyManager.getPublicKey(),
      {
        issuer: oidcConfig.issuer
      }
    )

    const now = Math.floor(Date.now() / 1000)

    // 토큰 만료 확인
    if (payload.exp && payload.exp < now) {
      return { active: false }
    }

    // 토큰 타입 판별
    const tokenType = payload.token_type || (payload.scope ? 'access_token' : 'refresh_token')

    // Refresh Token인 경우 DB에서 상태 확인
    if (tokenType === 'refresh_token' && payload.jti) {
      const refreshTokenData = await dbService.getRefreshToken(payload.jti)
      if (!refreshTokenData || refreshTokenData.status !== 'active') {
        return { active: false }
      }
    }

    // 사용자 정보 조회 (옵션)
    let userInfo = null
    if (payload.sub) {
      userInfo = await dbService.getUserById(parseInt(payload.sub))
    }

    // 성공적인 introspection 결과
    const result = {
      active: true,
      token_type: tokenType,
      scope: payload.scope,
      client_id: payload.aud,
      username: payload.username || userInfo?.username,
      sub: payload.sub,
      iss: payload.iss,
      exp: payload.exp,
      iat: payload.iat,
      nbf: payload.nbf,
      aud: payload.aud
    }

    // Refresh Token 추가 정보
    if (tokenType === 'refresh_token') {
      result.jti = payload.jti
      result.family_id = payload.family_id
    }

    // 사용자 추가 정보 (있는 경우)
    if (userInfo) {
      result.email = userInfo.email
      result.name = userInfo.display_name || userInfo.username
      result.preferred_username = userInfo.username
    }

    return result

  } catch (error) {
    logger.debug('Token verification failed during introspection', {
      error: error.message
    })

    // 검증 실패 시 inactive
    return { active: false }
  }
}

/**
 * GET /introspect/health
 * Introspection 서비스 건강성 체크
 */
router.get('/health', async (req, res) => {
  try {
    // 키 관리자 상태 확인
    const keyHealth = await keyManager.healthCheck()
    
    if (!keyHealth.healthy) {
      return res.status(503).json({
        status: 'unhealthy',
        reason: 'key_manager_unhealthy',
        details: keyHealth
      })
    }

    // 간단한 토큰 검증 테스트
    const testPayload = { 
      sub: 'test',
      aud: 'test-client',
      scope: 'openid',
      iat: Math.floor(Date.now() / 1000),
      exp: Math.floor(Date.now() / 1000) + 60
    }

    const { SignJWT } = require('jose')
    const testToken = await new SignJWT(testPayload)
      .setProtectedHeader({ alg: 'RS256', kid: keyManager.getKid() })
      .setIssuer(oidcConfig.issuer)
      .sign(keyManager.getPrivateKey())

    const introspectionResult = await introspectToken(testToken)

    if (introspectionResult.active) {
      res.json({
        status: 'healthy',
        timestamp: new Date().toISOString(),
        key_id: keyHealth.kid
      })
    } else {
      res.status(503).json({
        status: 'unhealthy',
        reason: 'token_verification_failed'
      })
    }

  } catch (error) {
    logger.error('Introspection health check failed', {
      error: error.message,
      stack: error.stack
    })

    res.status(503).json({
      status: 'unhealthy',
      reason: 'health_check_error',
      error: error.message
    })
  }
})

module.exports = router