const express = require('express')
const router = express.Router()
const logger = require('../config/logger')
const { authenticateToken } = require('../middleware/auth')

// Authentication endpoints removed - all authentication is handled by oidc-auth-server
// This API now only validates JWT tokens



/**
 * GET /api/auth/oidc-discovery
 * OIDC 서버 Discovery 정보 제공
 */
router.get('/oidc-discovery', async (req, res) => {
  try {
    // 환경별 OIDC 서버 URL 결정
    const isProduction = process.env.NODE_ENV === 'production'
    const oidcBaseUrl = isProduction
      ? (process.env.OIDC_BASE_URL_PROD || 'https://blokus-online.mooo.com')
      : (process.env.OIDC_BASE_URL || 'http://localhost:9000')

    const discoveryData = {
      oidc_server: {
        issuer: oidcBaseUrl,
        discovery_url: `${oidcBaseUrl}/.well-known/openid-configuration`,
        endpoints: {
          authorization: `${oidcBaseUrl}/authorize`,
          token: `${oidcBaseUrl}/token`,
          jwks: `${oidcBaseUrl}/jwks.json`,
          introspection: `${oidcBaseUrl}/introspect`,
          revocation: `${oidcBaseUrl}/revocation`
        }
      },
      supported_clients: [
        {
          client_id: 'unity-mobile-client',
          client_type: 'public',
          pkce_required: true,
          redirect_uris: [
            isProduction ? 'blokus://auth/callback' : 'http://localhost:7777/auth/callback'
          ]
        },
        {
          client_id: 'qt-desktop-client', 
          client_type: 'public',
          pkce_required: true,
          redirect_uris: ['http://localhost:8080/auth/callback']
        },
        {
          client_id: 'nextjs-web-client',
          client_type: 'confidential',
          pkce_required: false,
          redirect_uris: [
            isProduction 
              ? 'https://blokus-online.mooo.com/api/auth/callback/blokus-oidc'
              : 'http://localhost:3000/api/auth/callback/blokus-oidc'
          ]
        }
      ],
      api_info: {
        message: 'This API server only validates tokens via JWKS. All authentication must be done through OIDC server.',
        token_validation_method: 'RS256 + JWKS',
        supported_flows: ['authorization_code', 'refresh_token'],
        environment: isProduction ? 'production' : 'development'
      }
    }

    logger.debug('OIDC discovery info requested', {
      ip: req.ip,
      userAgent: req.get('User-Agent'),
      environment: isProduction ? 'production' : 'development'
    })

    res.json({
      success: true,
      message: 'OIDC discovery information',
      data: discoveryData
    })
  } catch (error) {
    logger.error('OIDC discovery error', {
      error: error.message,
      ip: req.ip,
      stack: error.stack
    })

    res.status(500).json({
      success: false,
      message: 'Failed to retrieve OIDC discovery information',
      error: 'INTERNAL_SERVER_ERROR'
    })
  }
})



/**
 * POST /api/auth/validate
 * OIDC JWT 토큰 유효성 검증 (JWKS 기반)
 */
router.post('/validate',
  authenticateToken,
  async (req, res) => {
    try {
      // authenticateToken 미들웨어를 통과했다면 토큰이 유효함 (OIDC 표준)
      const { sub, username, userId, email, iat, exp, iss, aud } = req.user

      logger.info('Token validation successful (OIDC)', {
        sub,
        username,
        email,
        ip: req.ip,
        userAgent: req.get('User-Agent'),
        issuer: iss
      })

      // 토큰 정보 응답 (OIDC 표준 클레임)
      const responseData = {
        valid: true,
        sub, // OIDC 표준 subject
        username,
        user_id: userId, // 호환성을 위해 유지
        email,
        issued_at: new Date(iat * 1000).toISOString(),
        expires_at: new Date(exp * 1000).toISOString(),
        remaining_time: Math.max(0, exp - Math.floor(Date.now() / 1000)),
        issuer: iss,
        audience: aud,
        token_type: 'Bearer',
        scope: 'openid profile email'
      }

      res.json({
        success: true,
        message: 'OIDC token is valid',
        data: responseData
      })
    } catch (error) {
      logger.error('Token validation error (OIDC)', {
        error: error.message,
        ip: req.ip,
        stack: error.stack
      })

      res.status(500).json({
        success: false,
        message: 'Token validation failed',
        error: 'INTERNAL_SERVER_ERROR'
      })
    }
  }
)

/**
 * GET /api/auth/info
 * OIDC 토큰에서 사용자 정보 추출 (검증 포함)
 */
router.get('/info',
  authenticateToken,
  async (req, res) => {
    try {
      const { sub, username, userId, email, iat, exp, iss, aud } = req.user

      logger.debug('Auth info requested (OIDC)', {
        sub,
        username,
        email,
        ip: req.ip,
        issuer: iss
      })

      // 토큰 만료까지 남은 시간 계산
      const currentTime = Math.floor(Date.now() / 1000)
      const remainingTime = Math.max(0, exp - currentTime)
      const remainingHours = Math.floor(remainingTime / 3600)
      const remainingMinutes = Math.floor((remainingTime % 3600) / 60)

      const responseData = {
        sub, // OIDC 표준 subject
        username,
        user_id: userId, // 호환성을 위해 유지
        email,
        token_info: {
          type: 'Bearer',
          issuer: iss,
          audience: Array.isArray(aud) ? aud : [aud],
          issued_at: new Date(iat * 1000).toISOString(),
          expires_at: new Date(exp * 1000).toISOString(),
          remaining_seconds: remainingTime,
          remaining_human: `${remainingHours}h ${remainingMinutes}m`,
          scope: 'openid profile email'
        },
        oidc_claims: {
          sub,
          email,
          preferred_username: username,
          iss,
          aud
        }
      }

      res.json({
        success: true,
        message: 'OIDC authentication info retrieved successfully',
        data: responseData
      })
    } catch (error) {
      logger.error('Failed to retrieve OIDC auth info', {
        error: error.message,
        sub: req.user?.sub,
        username: req.user?.username,
        stack: error.stack
      })

      res.status(500).json({
        success: false,
        message: 'Failed to retrieve authentication info',
        error: 'INTERNAL_SERVER_ERROR'
      })
    }
  }
)


module.exports = router
