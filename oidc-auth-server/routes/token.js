const express = require('express')
const router = express.Router()
const { body, validationResult } = require('express-validator')
const crypto = require('crypto')
const { v4: uuidv4 } = require('uuid')
const { SignJWT } = require('jose')

const oidcConfig = require('../config/oidc')
const keyManager = require('../config/keys')
const dbService = require('../config/database')
const logger = require('../config/logger')

/**
 * POST /token
 * OAuth 2.1 Token 엔드포인트
 * Authorization Code를 Access Token + Refresh Token으로 교환
 * 또는 Refresh Token으로 새로운 토큰 발급
 */
router.post('/',
  [
    body('grant_type').isIn(['authorization_code', 'refresh_token']).withMessage('Invalid grant_type'),
    body('client_id').notEmpty().withMessage('client_id is required'),
    
    // Authorization Code Grant 필수 파라미터
    body('code').optional().isLength({ min: 10 }).withMessage('Invalid authorization code'),
    // redirect_uri는 런타임에서 검증 (express-validator 우회)
    body('code_verifier').optional().isLength({ min: 43, max: 128 }).withMessage('Invalid code_verifier'),
    
    // Refresh Token Grant 필수 파라미터
    body('refresh_token').optional().isLength({ min: 10 }).withMessage('Invalid refresh_token'),
    
    // Client Secret (confidential clients)
    body('client_secret').optional()
  ],
  async (req, res) => {
    try {
      // 입력 검증
      const errors = validationResult(req)
      if (!errors.isEmpty()) {
        logger.warn('Invalid token request', {
          errors: errors.array(),
          ip: req.ip
        })

        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'Invalid request parameters'
        })
      }

      const { grant_type, client_id, client_secret } = req.body

      logger.info('Token request received', {
        grant_type,
        client_id,
        ip: req.ip
      })

      // 클라이언트 검증
      const clientValidation = oidcConfig.validateClient(client_id, client_secret)
      if (!clientValidation.valid) {
        logger.warn('Client validation failed', {
          client_id,
          error: clientValidation.error,
          ip: req.ip
        })

        return res.status(401).json({
          error: clientValidation.error,
          error_description: 'Client authentication failed'
        })
      }

      const client = clientValidation.client

      // Grant Type별 처리
      if (grant_type === 'authorization_code') {
        return await handleAuthorizationCodeGrant(req, res, client)
      } else if (grant_type === 'refresh_token') {
        return await handleRefreshTokenGrant(req, res, client)
      }

    } catch (error) {
      logger.error('Token endpoint error', {
        error: error.message,
        stack: error.stack,
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
 * Authorization Code Grant 처리
 */
async function handleAuthorizationCodeGrant(req, res, client) {
  const { code, redirect_uri, code_verifier } = req.body

  // 필수 파라미터 체크
  if (!code || !redirect_uri) {
    return res.status(400).json({
      error: 'invalid_request',
      error_description: 'code and redirect_uri are required for authorization_code grant'
    })
  }

  // redirect_uri 형식 검증 (express-validator 우회)
  if (!redirect_uri.startsWith('http://') && !redirect_uri.startsWith('https://') && !redirect_uri.startsWith('blokus://')) {
    return res.status(400).json({
      error: 'invalid_request',
      error_description: 'Invalid redirect_uri format'
    })
  }

  try {
    // Authorization Code 조회 및 소비
    const authCodeData = await dbService.consumeAuthorizationCode(code)
    if (!authCodeData) {
      logger.warn('Invalid or expired authorization code', {
        code: code.substring(0, 10) + '...',
        ip: req.ip
      })

      return res.status(400).json({
        error: 'invalid_grant',
        error_description: 'Invalid or expired authorization code'
      })
    }

    // Redirect URI 검증
    if (authCodeData.redirect_uri !== redirect_uri) {
      return res.status(400).json({
        error: 'invalid_grant',
        error_description: 'redirect_uri mismatch'
      })
    }

    // PKCE 검증 (코드가 PKCE로 발급된 경우)
    if (authCodeData.code_challenge) {
      if (!code_verifier) {
        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'code_verifier is required'
        })
      }

      const hash = crypto.createHash('sha256').update(code_verifier).digest('base64url')
      if (hash !== authCodeData.code_challenge) {
        logger.warn('PKCE verification failed', {
          client_id: client.client_id,
          ip: req.ip
        })

        return res.status(400).json({
          error: 'invalid_grant',
          error_description: 'PKCE verification failed'
        })
      }
    }

    // 사용자 정보 조회
    const user = await dbService.getUserById(authCodeData.user_id)
    if (!user) {
      return res.status(400).json({
        error: 'invalid_grant',
        error_description: 'User not found'
      })
    }

    // Device fingerprint 생성 (간단한 버전)
    const deviceFingerprint = crypto.createHash('sha256')
      .update(`${req.ip}-${req.get('User-Agent')}-${client.client_id}`)
      .digest('hex')

    // Refresh Token Family 생성
    const tokenFamily = await dbService.createRefreshTokenFamily(
      user.user_id,
      client.client_id,
      deviceFingerprint
    )

    // 토큰 생성
    const tokens = await generateTokens(user, client, authCodeData.scope, tokenFamily.family_id)

    logger.info('Tokens issued via authorization code', {
      username: user.username,
      client_id: client.client_id,
      scope: authCodeData.scope,
      familyId: tokenFamily.family_id,
      ip: req.ip
    })

    res.json(tokens)

  } catch (error) {
    logger.error('Authorization code grant error', {
      error: error.message,
      stack: error.stack
    })
    throw error
  }
}

/**
 * Refresh Token Grant 처리
 */
async function handleRefreshTokenGrant(req, res, client) {
  const { refresh_token } = req.body

  if (!refresh_token) {
    return res.status(400).json({
      error: 'invalid_request',
      error_description: 'refresh_token is required'
    })
  }

  try {
    // Refresh Token JTI 추출
    const jti = extractJtiFromRefreshToken(refresh_token)
    if (!jti) {
      return res.status(400).json({
        error: 'invalid_grant',
        error_description: 'Invalid refresh token format'
      })
    }

    // 재사용 감지
    const reuseCheck = await dbService.detectTokenReuseAndRevoke(jti)
    if (reuseCheck.detected) {
      logger.warn('Refresh token reuse detected', {
        jti,
        familyId: reuseCheck.familyId,
        reason: reuseCheck.reason,
        client_id: client.client_id,
        ip: req.ip
      })

      return res.status(400).json({
        error: 'invalid_grant',
        error_description: 'Token reuse detected - all tokens revoked'
      })
    }

    // Refresh Token 조회
    const refreshTokenData = await dbService.getRefreshToken(jti)
    if (!refreshTokenData) {
      return res.status(400).json({
        error: 'invalid_grant',
        error_description: 'Invalid or expired refresh token'
      })
    }

    // 클라이언트 검증
    if (refreshTokenData.client_id !== client.client_id) {
      return res.status(400).json({
        error: 'invalid_grant',
        error_description: 'Client mismatch'
      })
    }

    // 사용자 정보 조회
    const user = await dbService.getUserById(refreshTokenData.user_id)
    if (!user) {
      return res.status(400).json({
        error: 'invalid_grant',
        error_description: 'User not found'
      })
    }

    // 새로운 JTI 생성
    const newJti = uuidv4()

    // Refresh Token 회전
    const newRefreshTokenData = await dbService.rotateRefreshToken(
      jti,
      newJti,
      new Date(Date.now() + oidcConfig.getTokenLifetimeInSeconds('refreshToken') * 1000)
    )

    // 새로운 토큰 생성
    const tokens = await generateTokens(
      user, 
      client, 
      'openid profile email', // TODO: 원래 scope 저장/복원
      refreshTokenData.family_id,
      newJti
    )

    logger.info('Tokens refreshed', {
      username: user.username,
      client_id: client.client_id,
      familyId: refreshTokenData.family_id,
      oldJti: jti,
      newJti,
      ip: req.ip
    })

    res.json(tokens)

  } catch (error) {
    logger.error('Refresh token grant error', {
      error: error.message,
      stack: error.stack
    })
    throw error
  }
}

/**
 * Access Token과 Refresh Token 생성 (슬라이딩 윈도우 적용)
 */
async function generateTokens(user, client, scope, familyId, refreshTokenJti = null) {
  const now = Math.floor(Date.now() / 1000)
  const accessTokenLifetime = oidcConfig.getTokenLifetimeInSeconds('accessToken')
  const refreshTokenLifetime = oidcConfig.getTokenLifetimeInSeconds('refreshToken') // 30일

  // 키 관리자가 초기화되지 않았다면 초기화
  if (!keyManager.getKid()) {
    await keyManager.initialize()
  }

  // Access Token 생성 (JWT)
  const accessTokenPayload = {
    sub: user.user_id.toString(),
    aud: client.client_id,
    scope: scope,
    username: user.username,
    email: user.email,
    name: user.display_name || user.username,
    preferred_username: user.username
  }

  const accessToken = await new SignJWT(accessTokenPayload)
    .setProtectedHeader({ alg: 'RS256', kid: keyManager.getKid() })
    .setIssuer(oidcConfig.issuer)
    .setIssuedAt(now)
    .setExpirationTime(now + accessTokenLifetime)
    .sign(keyManager.getPrivateKey())

  // ID Token 생성 (OIDC 표준 준수)
  const idTokenPayload = {
    sub: user.user_id.toString(),
    aud: client.client_id,
    iss: oidcConfig.issuer,
    email: user.email,
    email_verified: true,
    name: user.display_name || user.username,
    preferred_username: user.username,
    auth_time: now
  }

  const idToken = await new SignJWT(idTokenPayload)
    .setProtectedHeader({ alg: 'RS256', kid: keyManager.getKid() })
    .setIssuer(oidcConfig.issuer)
    .setIssuedAt(now)
    .setExpirationTime(now + accessTokenLifetime) // ID Token은 Access Token과 같은 수명
    .sign(keyManager.getPrivateKey())

  // Refresh Token 생성 (새로운 JTI 또는 제공된 JTI 사용)
  const jti = refreshTokenJti || uuidv4()
  const refreshTokenPayload = {
    jti,
    sub: user.user_id.toString(),
    aud: client.client_id,
    family_id: familyId,
    token_type: 'refresh_token'
  }

  const refreshToken = await new SignJWT(refreshTokenPayload)
    .setProtectedHeader({ alg: 'RS256', kid: keyManager.getKid() })
    .setIssuer(oidcConfig.issuer)
    .setIssuedAt(now)
    .setExpirationTime(now + refreshTokenLifetime)
    .sign(keyManager.getPrivateKey())

  // Refresh Token 저장 (신규 발급인 경우에만)
  if (!refreshTokenJti) {
    await dbService.storeRefreshToken(
      familyId,
      jti,
      null, // prev_jti는 null (첫 발급)
      new Date((now + refreshTokenLifetime) * 1000)
    )
  }

  return {
    access_token: accessToken,
    id_token: idToken,
    token_type: 'Bearer',
    expires_in: accessTokenLifetime,
    refresh_token: refreshToken,
    scope: scope
  }
}

/**
 * Refresh Token에서 JTI 추출 (JWT 파싱)
 */
function extractJtiFromRefreshToken(refreshToken) {
  try {
    const parts = refreshToken.split('.')
    if (parts.length !== 3) return null

    const payload = JSON.parse(Buffer.from(parts[1], 'base64url').toString())
    return payload.jti || null
  } catch (error) {
    logger.debug('Failed to extract JTI from refresh token', { error: error.message })
    return null
  }
}

module.exports = router
module.exports.generateTokens = generateTokens