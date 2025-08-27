const express = require('express')
const router = express.Router()
const { body, validationResult } = require('express-validator')
const { jwtVerify } = require('jose')

const oidcConfig = require('../config/oidc')
const keyManager = require('../config/keys')
const dbService = require('../config/database')
const logger = require('../config/logger')
const { env } = require('../config/env')

/**
 * POST /revocation
 * RFC 7009: OAuth 2.0 Token Revocation
 * 토큰 무효화 엔드포인트
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

      logger.info('Token revocation request', {
        client_id,
        token_type_hint,
        ip: req.ip
      })

      // 클라이언트 검증
      const clientValidation = oidcConfig.validateClient(client_id, client_secret)
      if (!clientValidation.valid) {
        logger.warn('Client validation failed for revocation', {
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

      // 토큰 무효화 처리
      const revocationResult = await revokeToken(token, client_id)

      logger.info('Token revocation completed', {
        client_id,
        success: revocationResult.success,
        tokenType: revocationResult.tokenType,
        familyId: revocationResult.familyId,
        ip: req.ip
      })

      // RFC 7009: 성공시 200 OK, 실패해도 200 OK (보안상 이유)
      res.status(200).json({
        revoked: revocationResult.success
      })

    } catch (error) {
      logger.error('Token revocation error', {
        error: error.message,
        stack: error.stack,
        ip: req.ip
      })

      // RFC 7009: 에러가 발생해도 200 OK로 응답 (보안상 이유)
      res.status(200).json({
        revoked: false
      })
    }
  }
)

/**
 * 토큰 무효화 처리
 */
async function revokeToken(token, clientId) {
  try {
    // JWT 검증 (만료된 토큰도 허용)
    const { payload } = await jwtVerify(
      token,
      keyManager.getPublicKey(),
      {
        issuer: oidcConfig.issuer,
        clockTolerance: '1y' // 만료된 토큰도 처리하기 위해 큰 tolerance 설정
      }
    )

    // 클라이언트 일치 확인
    if (payload.aud !== clientId) {
      logger.warn('Token revocation attempted by wrong client', {
        tokenAud: payload.aud,
        requestClientId: clientId
      })
      return { success: false, reason: 'client_mismatch' }
    }

    // 토큰 타입 판별
    const tokenType = payload.token_type || (payload.scope ? 'access_token' : 'refresh_token')

    if (tokenType === 'refresh_token' && payload.jti) {
      // Refresh Token 무효화
      return await revokeRefreshToken(payload.jti, clientId)
    } else if (tokenType === 'access_token') {
      // Access Token 무효화 (현재는 블랙리스트 없이 단순 로깅만)
      // 실제 구현에서는 Redis 등에 블랙리스트 저장 가능
      logger.info('Access token revocation logged', {
        sub: payload.sub,
        aud: payload.aud,
        exp: payload.exp
      })
      return { success: true, tokenType: 'access_token' }
    }

    return { success: false, reason: 'unknown_token_type' }

  } catch (error) {
    logger.debug('Token verification failed during revocation', {
      error: error.message
    })

    // 검증 실패한 토큰도 "성공"으로 처리 (보안상 이유)
    return { success: true, reason: 'invalid_token' }
  }
}

/**
 * Refresh Token 무효화
 */
async function revokeRefreshToken(jti, clientId) {
  try {
    // Refresh Token 조회
    const refreshTokenData = await dbService.getRefreshToken(jti)
    
    if (!refreshTokenData) {
      // 토큰이 존재하지 않아도 성공으로 처리
      return { success: true, reason: 'token_not_found' }
    }

    // 클라이언트 검증
    if (refreshTokenData.client_id !== clientId) {
      return { success: false, reason: 'client_mismatch' }
    }

    // Refresh Token Family 전체 무효화
    const revokedCount = await dbService.revokeRefreshTokenFamily(refreshTokenData.family_id)

    logger.info('Refresh token family revoked', {
      familyId: refreshTokenData.family_id,
      userId: refreshTokenData.user_id,
      clientId: refreshTokenData.client_id,
      revokedTokens: revokedCount
    })

    return {
      success: true,
      tokenType: 'refresh_token',
      familyId: refreshTokenData.family_id,
      revokedTokens: revokedCount
    }

  } catch (error) {
    logger.error('Refresh token revocation failed', {
      jti,
      error: error.message
    })
    throw error
  }
}

/**
 * POST /revocation/user/:userId
 * 특정 사용자의 모든 토큰 무효화 (관리자용)
 */
router.post('/user/:userId',
  [
    body('client_id').notEmpty().withMessage('client_id is required'),
    body('client_secret').optional(),
    body('admin_token').notEmpty().withMessage('admin_token is required')
  ],
  async (req, res) => {
    try {
      const { userId } = req.params
      const { client_id, client_secret, admin_token } = req.body

      // TODO: admin_token 검증 구현 필요
      // 현재는 간단한 체크만 수행
      if (admin_token !== env.ADMIN_TOKEN) {
        return res.status(403).json({
          error: 'access_denied',
          error_description: 'Invalid admin token'
        })
      }

      logger.info('Admin revocation request', {
        userId,
        client_id,
        ip: req.ip
      })

      // 사용자의 모든 Refresh Token Family 무효화
      const revokedFamilies = await dbService.revokeAllUserRefreshTokens(parseInt(userId))

      logger.info('All user tokens revoked by admin', {
        userId,
        revokedFamilies,
        adminIp: req.ip
      })

      res.json({
        success: true,
        user_id: userId,
        revoked_families: revokedFamilies
      })

    } catch (error) {
      logger.error('Admin revocation error', {
        error: error.message,
        stack: error.stack,
        userId: req.params.userId,
        ip: req.ip
      })

      res.status(500).json({
        error: 'server_error',
        error_description: 'Internal server error'
      })
    }
  }
)

/**
 * GET /revocation/health
 * Revocation 서비스 건강성 체크
 */
router.get('/health', async (req, res) => {
  try {
    // 데이터베이스 연결 확인
    const dbHealthy = await dbService.healthCheck()
    
    if (!dbHealthy) {
      return res.status(503).json({
        status: 'unhealthy',
        reason: 'database_unavailable'
      })
    }

    // 키 관리자 상태 확인
    const keyHealth = await keyManager.healthCheck()
    
    if (!keyHealth.healthy) {
      return res.status(503).json({
        status: 'unhealthy',
        reason: 'key_manager_unhealthy',
        details: keyHealth
      })
    }

    res.json({
      status: 'healthy',
      timestamp: new Date().toISOString(),
      database: 'connected',
      key_manager: keyHealth.kid
    })

  } catch (error) {
    logger.error('Revocation health check failed', {
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