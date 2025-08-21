const express = require('express')
const router = express.Router()
const keyManager = require('../config/keys')
const logger = require('../config/logger')

/**
 * GET /jwks.json
 * JSON Web Key Set (JWKS) 엔드포인트
 * JWT 검증을 위한 공개 키들을 제공
 * RFC 7517: JSON Web Key (JWK)
 */
router.get('/', async (req, res) => {
  try {
    // 키가 초기화되지 않았다면 초기화
    if (!keyManager.getKid()) {
      await keyManager.initialize()
    }

    const jwks = keyManager.getJWKS()

    logger.debug('JWKS requested', {
      ip: req.ip,
      userAgent: req.get('User-Agent'),
      keyCount: jwks.keys.length
    })

    // Cache control headers for JWKS
    res.set({
      'Cache-Control': 'public, max-age=3600', // 1시간 캐시
      'Content-Type': 'application/json'
    })

    res.json(jwks)
  } catch (error) {
    logger.error('Failed to serve JWKS', {
      error: error.message,
      stack: error.stack,
      ip: req.ip
    })

    res.status(500).json({
      error: 'server_error',
      error_description: 'Failed to retrieve public keys'
    })
  }
})

/**
 * GET /jwks.json/health
 * JWKS 건강성 체크 엔드포인트
 */
router.get('/health', async (req, res) => {
  try {
    const healthResult = await keyManager.healthCheck()

    if (healthResult.healthy) {
      res.json({
        status: 'healthy',
        kid: healthResult.kid,
        timestamp: new Date().toISOString()
      })
    } else {
      res.status(503).json({
        status: 'unhealthy',
        reason: healthResult.reason,
        timestamp: new Date().toISOString()
      })
    }
  } catch (error) {
    logger.error('JWKS health check failed', {
      error: error.message,
      stack: error.stack
    })

    res.status(503).json({
      status: 'unhealthy',
      reason: 'health_check_failed',
      timestamp: new Date().toISOString()
    })
  }
})

module.exports = router