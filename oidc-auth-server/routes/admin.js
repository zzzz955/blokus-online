const express = require('express')
const router = express.Router()
const { body, param, query, validationResult } = require('express-validator')

const oidcConfig = require('../config/oidc')
const keyManager = require('../config/keys')
const dbService = require('../config/database')
const logger = require('../config/logger')

/**
 * 간단한 Admin Token 검증 미들웨어
 */
function requireAdminAuth(req, res, next) {
  const adminToken = req.headers.authorization?.replace('Bearer ', '') || req.body.admin_token

  if (!adminToken || adminToken !== process.env.ADMIN_TOKEN) {
    logger.warn('Unauthorized admin access attempt', {
      ip: req.ip,
      userAgent: req.get('User-Agent'),
      path: req.path
    })

    return res.status(403).json({
      error: 'access_denied',
      error_description: 'Invalid or missing admin token'
    })
  }

  next()
}

/**
 * GET /admin/status
 * 전체 OIDC 서버 상태 조회
 */
router.get('/status', requireAdminAuth, async (req, res) => {
  try {
    // 데이터베이스 상태
    const dbHealthy = await dbService.healthCheck()
    
    // 키 관리자 상태
    const keyHealth = await keyManager.healthCheck()

    // 토큰 통계 조회
    const tokenStats = await getTokenStatistics()

    // 클라이언트 설정 정보
    const clientsInfo = Object.keys(oidcConfig.clients).map(clientId => {
      const client = oidcConfig.clients[clientId]
      return {
        client_id: clientId,
        client_type: client.client_type,
        grant_types: client.grant_types,
        response_types: client.response_types,
        redirect_uri_count: client.redirect_uris.length,
        require_pkce: client.require_pkce
      }
    })

    const status = {
      service: 'blokus-oidc-auth-server',
      version: process.env.npm_package_version || '1.0.0',
      timestamp: new Date().toISOString(),
      uptime: process.uptime(),
      environment: process.env.NODE_ENV || 'development',
      
      database: {
        status: dbHealthy ? 'healthy' : 'unhealthy',
        connection_pool: {
          total: dbService.pool?.totalCount || 0,
          idle: dbService.pool?.idleCount || 0,
          waiting: dbService.pool?.waitingCount || 0
        }
      },
      
      key_manager: {
        status: keyHealth.healthy ? 'healthy' : 'unhealthy',
        current_kid: keyHealth.kid,
        algorithm: 'RS256'
      },
      
      oidc_config: {
        issuer: oidcConfig.issuer,
        clients_count: Object.keys(oidcConfig.clients).length,
        token_lifetimes: oidcConfig.tokenLifetimes
      },
      
      clients: clientsInfo,
      token_statistics: tokenStats
    }

    logger.info('Admin status requested', {
      ip: req.ip,
      status: status.database.status && status.key_manager.status ? 'healthy' : 'degraded'
    })

    res.json(status)

  } catch (error) {
    logger.error('Admin status check failed', {
      error: error.message,
      stack: error.stack
    })

    res.status(500).json({
      error: 'server_error',
      error_description: 'Failed to retrieve status information'
    })
  }
})

/**
 * GET /admin/tokens/stats
 * 토큰 통계 조회
 */
router.get('/tokens/stats', requireAdminAuth, async (req, res) => {
  try {
    const stats = await getTokenStatistics()

    logger.info('Token statistics requested', { ip: req.ip })

    res.json(stats)

  } catch (error) {
    logger.error('Failed to retrieve token statistics', {
      error: error.message,
      stack: error.stack
    })

    res.status(500).json({
      error: 'server_error',
      error_description: 'Failed to retrieve token statistics'
    })
  }
})

/**
 * POST /admin/tokens/cleanup
 * 만료된 토큰 정리
 */
router.post('/tokens/cleanup', requireAdminAuth, async (req, res) => {
  try {
    const cleanupResult = await dbService.cleanupExpiredTokens()

    logger.info('Token cleanup performed', {
      ...cleanupResult,
      adminIp: req.ip
    })

    res.json({
      success: true,
      cleanup_result: cleanupResult,
      timestamp: new Date().toISOString()
    })

  } catch (error) {
    logger.error('Token cleanup failed', {
      error: error.message,
      stack: error.stack
    })

    res.status(500).json({
      error: 'server_error',
      error_description: 'Token cleanup failed'
    })
  }
})

/**
 * POST /admin/keys/rotate
 * 키 회전 (RSA 키 페어 갱신)
 */
router.post('/keys/rotate', requireAdminAuth, async (req, res) => {
  try {
    const oldKid = keyManager.getKid()
    
    await keyManager.rotateKeys()
    
    const newKid = keyManager.getKid()

    logger.warn('Key rotation performed', {
      oldKid,
      newKid,
      adminIp: req.ip,
      timestamp: new Date().toISOString()
    })

    res.json({
      success: true,
      old_kid: oldKid,
      new_kid: newKid,
      timestamp: new Date().toISOString(),
      note: 'All existing tokens will be invalid after key rotation'
    })

  } catch (error) {
    logger.error('Key rotation failed', {
      error: error.message,
      stack: error.stack
    })

    res.status(500).json({
      error: 'server_error',
      error_description: 'Key rotation failed'
    })
  }
})

/**
 * GET /admin/users/:userId/tokens
 * 특정 사용자의 토큰 정보 조회
 */
router.get('/users/:userId/tokens',
  [
    param('userId').isInt({ min: 1 }).withMessage('Valid user ID required')
  ],
  requireAdminAuth,
  async (req, res) => {
    try {
      const errors = validationResult(req)
      if (!errors.isEmpty()) {
        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'Invalid user ID'
        })
      }

      const userId = parseInt(req.params.userId)

      // 사용자 정보 조회
      const user = await dbService.getUserById(userId)
      if (!user) {
        return res.status(404).json({
          error: 'user_not_found',
          error_description: 'User not found'
        })
      }

      // 사용자의 토큰 패밀리 조회
      const familiesQuery = `
        SELECT 
          rtf.family_id,
          rtf.client_id,
          rtf.device_fingerprint,
          rtf.status as family_status,
          rtf.created_at as family_created_at,
          rtf.last_used_at as family_last_used_at,
          COUNT(rt.token_id) as token_count,
          COUNT(CASE WHEN rt.status = 'active' THEN 1 END) as active_tokens
        FROM refresh_token_families rtf
        LEFT JOIN refresh_tokens rt ON rtf.family_id = rt.family_id
        WHERE rtf.user_id = $1
        GROUP BY rtf.family_id, rtf.client_id, rtf.device_fingerprint, rtf.status, rtf.created_at, rtf.last_used_at
        ORDER BY rtf.last_used_at DESC
      `

      const familiesResult = await dbService.query(familiesQuery, [userId])

      const tokenInfo = {
        user: {
          user_id: user.user_id,
          username: user.username,
          display_name: user.display_name,
          email: user.email
        },
        token_families: familiesResult.rows.map(family => ({
          family_id: family.family_id,
          client_id: family.client_id,
          device_fingerprint: family.device_fingerprint,
          family_status: family.family_status,
          created_at: family.family_created_at,
          last_used_at: family.family_last_used_at,
          total_tokens: parseInt(family.token_count),
          active_tokens: parseInt(family.active_tokens)
        })),
        summary: {
          total_families: familiesResult.rows.length,
          active_families: familiesResult.rows.filter(f => f.family_status === 'active').length,
          total_tokens: familiesResult.rows.reduce((sum, f) => sum + parseInt(f.token_count), 0),
          active_tokens: familiesResult.rows.reduce((sum, f) => sum + parseInt(f.active_tokens), 0)
        }
      }

      logger.info('User token information requested', {
        userId,
        adminIp: req.ip,
        tokenFamilies: tokenInfo.summary.total_families
      })

      res.json(tokenInfo)

    } catch (error) {
      logger.error('Failed to retrieve user token information', {
        error: error.message,
        stack: error.stack,
        userId: req.params.userId
      })

      res.status(500).json({
        error: 'server_error',
        error_description: 'Failed to retrieve user token information'
      })
    }
  }
)

/**
 * DELETE /admin/users/:userId/tokens
 * 특정 사용자의 모든 토큰 무효화
 */
router.delete('/users/:userId/tokens',
  [
    param('userId').isInt({ min: 1 }).withMessage('Valid user ID required')
  ],
  requireAdminAuth,
  async (req, res) => {
    try {
      const errors = validationResult(req)
      if (!errors.isEmpty()) {
        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'Invalid user ID'
        })
      }

      const userId = parseInt(req.params.userId)

      // 사용자 존재 확인
      const user = await dbService.getUserById(userId)
      if (!user) {
        return res.status(404).json({
          error: 'user_not_found',
          error_description: 'User not found'
        })
      }

      // 모든 토큰 무효화
      const revokedFamilies = await dbService.revokeAllUserRefreshTokens(userId)

      logger.warn('All user tokens revoked by admin', {
        userId,
        username: user.username,
        revokedFamilies,
        adminIp: req.ip
      })

      res.json({
        success: true,
        user_id: userId,
        username: user.username,
        revoked_families: revokedFamilies,
        timestamp: new Date().toISOString()
      })

    } catch (error) {
      logger.error('Failed to revoke user tokens', {
        error: error.message,
        stack: error.stack,
        userId: req.params.userId
      })

      res.status(500).json({
        error: 'server_error',
        error_description: 'Failed to revoke user tokens'
      })
    }
  }
)

/**
 * 토큰 통계 조회 헬퍼 함수
 */
async function getTokenStatistics() {
  try {
    const statsQuery = `
      SELECT 
        COUNT(DISTINCT rtf.family_id) as total_families,
        COUNT(DISTINCT CASE WHEN rtf.status = 'active' THEN rtf.family_id END) as active_families,
        COUNT(rt.token_id) as total_tokens,
        COUNT(CASE WHEN rt.status = 'active' THEN rt.token_id END) as active_tokens,
        COUNT(CASE WHEN rt.status = 'used' THEN rt.token_id END) as used_tokens,
        COUNT(CASE WHEN rt.status = 'revoked' THEN rt.token_id END) as revoked_tokens,
        COUNT(CASE WHEN rt.status = 'expired' THEN rt.token_id END) as expired_tokens,
        COUNT(DISTINCT rtf.user_id) as unique_users,
        COUNT(DISTINCT rtf.client_id) as unique_clients
      FROM refresh_token_families rtf
      LEFT JOIN refresh_tokens rt ON rtf.family_id = rt.family_id
    `

    const result = await dbService.query(statsQuery)
    const stats = result.rows[0]

    // Authorization Codes 통계
    const authCodesQuery = `
      SELECT 
        COUNT(*) as total_auth_codes,
        COUNT(CASE WHEN expires_at > NOW() THEN 1 END) as active_auth_codes,
        COUNT(CASE WHEN expires_at <= NOW() THEN 1 END) as expired_auth_codes
      FROM authorization_codes
    `

    const authCodesResult = await dbService.query(authCodesQuery)
    const authCodesStats = authCodesResult.rows[0]

    return {
      refresh_tokens: {
        total_families: parseInt(stats.total_families) || 0,
        active_families: parseInt(stats.active_families) || 0,
        total_tokens: parseInt(stats.total_tokens) || 0,
        active_tokens: parseInt(stats.active_tokens) || 0,
        used_tokens: parseInt(stats.used_tokens) || 0,
        revoked_tokens: parseInt(stats.revoked_tokens) || 0,
        expired_tokens: parseInt(stats.expired_tokens) || 0
      },
      authorization_codes: {
        total: parseInt(authCodesStats.total_auth_codes) || 0,
        active: parseInt(authCodesStats.active_auth_codes) || 0,
        expired: parseInt(authCodesStats.expired_auth_codes) || 0
      },
      users_and_clients: {
        unique_users: parseInt(stats.unique_users) || 0,
        unique_clients: parseInt(stats.unique_clients) || 0
      },
      last_updated: new Date().toISOString()
    }

  } catch (error) {
    logger.error('Failed to retrieve token statistics', {
      error: error.message
    })
    throw error
  }
}

module.exports = router