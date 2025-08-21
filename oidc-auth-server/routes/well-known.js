const express = require('express')
const router = express.Router()
const oidcConfig = require('../config/oidc')
const logger = require('../config/logger')

/**
 * GET /.well-known/openid-configuration
 * OIDC Discovery 메타데이터 엔드포인트
 * RFC 8414: OAuth 2.0 Authorization Server Metadata
 */
router.get('/openid-configuration', (req, res) => {
  try {
    const discoveryDocument = oidcConfig.getDiscoveryDocument()

    logger.info('OIDC discovery document requested', {
      ip: req.ip,
      userAgent: req.get('User-Agent')
    })

    // Cache control headers for discovery document
    res.set({
      'Cache-Control': 'public, max-age=3600', // 1시간 캐시
      'Content-Type': 'application/json'
    })

    res.json(discoveryDocument)
  } catch (error) {
    logger.error('Failed to serve discovery document', {
      error: error.message,
      stack: error.stack,
      ip: req.ip
    })

    res.status(500).json({
      error: 'server_error',
      error_description: 'Failed to generate discovery document'
    })
  }
})

/**
 * GET /.well-known/oauth-authorization-server
 * OAuth 2.0 Authorization Server Metadata (별칭)
 * 일부 클라이언트가 이 경로를 사용할 수 있음
 */
router.get('/oauth-authorization-server', (req, res) => {
  // OIDC discovery와 동일한 응답
  req.url = '/openid-configuration'
  router.handle(req, res)
})

module.exports = router