const express = require('express')
const router = express.Router()
const { body, validationResult } = require('express-validator')
const crypto = require('crypto')
const argon2 = require('argon2')
const { v4: uuidv4 } = require('uuid')
const { SignJWT } = require('jose')

const oidcConfig = require('../config/oidc')
const keyManager = require('../config/keys')
const dbService = require('../config/database')
const logger = require('../config/logger')
const { env } = require('../config/env')

/**
 * Direct Authentication API Routes
 * 웹 페이지 리디렉션 없이 직접 API 호출로 인증 처리
 * 모바일/데스크톱 앱에서 사용
 */

/**
 * POST /api/auth/login
 * 직접 ID/PW 로그인 - 토큰 즉시 반환
 */
router.post('/login',
  [
    body('username').trim().isLength({ min: 3, max: 50 }).withMessage('Username must be 3-50 characters'),
    body('password').isLength({ min: 4, max: 100 }).withMessage('Password must be 4-100 characters'),
    body('client_id').notEmpty().withMessage('client_id is required'),
    body('client_secret').optional(),
    body('scope').optional().default('openid profile email')
  ],
  async (req, res) => {
    try {
      // 입력 검증
      const errors = validationResult(req)
      if (!errors.isEmpty()) {
        logger.warn('Invalid direct login request', {
          errors: errors.array(),
          ip: req.ip
        })

        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'Invalid request parameters',
          details: errors.array()
        })
      }

      const { username, password, client_id, client_secret, scope } = req.body

      logger.info('Direct login attempt', {
        username,
        client_id,
        ip: req.ip
      })

      // 클라이언트 검증
      const clientValidation = oidcConfig.validateClient(client_id, client_secret)
      if (!clientValidation.valid) {
        logger.warn('Client validation failed in direct login', {
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

      // Scope 검증
      const scopeValidation = oidcConfig.validateScope(scope)
      if (!scopeValidation.valid) {
        return res.status(400).json({
          error: scopeValidation.error,
          error_description: scopeValidation.description
        })
      }

      // 데이터베이스 기반 사용자 인증
      let user = null

      try {
        const dbUser = await dbService.getUserByUsername(username)
        if (!dbUser) {
          logger.warn('Direct login failed - user not found', { username, ip: req.ip })
          return res.status(401).json({
            error: 'invalid_credentials',
            error_description: 'Invalid username or password'
          })
        }

        if (!dbUser.password_hash) {
          logger.warn('Direct login failed - no password hash', { username, ip: req.ip })
          return res.status(401).json({
            error: 'invalid_credentials',
            error_description: 'Invalid username or password'
          })
        }

        const isValidPassword = await argon2.verify(dbUser.password_hash, password)
        if (!isValidPassword) {
          logger.warn('Direct login failed - invalid password', { username, ip: req.ip })
          return res.status(401).json({
            error: 'invalid_credentials',
            error_description: 'Invalid username or password'
          })
        }
        
        user = dbUser
        logger.info('Database authentication successful', { username, ip: req.ip })
        
      } catch (dbError) {
        logger.error('Database authentication error in direct login', { 
          error: dbError.message, 
          username, 
          ip: req.ip 
        })
        
        return res.status(500).json({
          error: 'server_error',
          error_description: 'Authentication service temporarily unavailable'
        })
      }

      if (!user) {
        logger.warn('Direct login failed - authentication failed', { username, ip: req.ip })
        return res.status(401).json({
          error: 'invalid_credentials',
          error_description: 'Invalid username or password'
        })
      }

      // Device fingerprint 생성
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
      const tokens = await generateTokens(user, client, scope, tokenFamily.family_id)

      logger.info('Direct login successful - tokens issued', {
        username: user.username,
        client_id: client.client_id,
        scope: scope,
        familyId: tokenFamily.family_id,
        ip: req.ip
      })

      res.json({
        ...tokens,
        user: {
          user_id: user.user_id,
          username: user.username,
          email: user.email,
          display_name: user.display_name || user.username,
          level: user.level || 1,
          experience_points: user.experience_points || 0
        }
      })

    } catch (error) {
      logger.error('Direct login processing error', {
        error: error.message,
        stack: error.stack,
        username: req.body?.username,
        ip: req.ip
      })

      res.status(500).json({
        error: 'server_error',
        error_description: 'Internal server error during authentication'
      })
    }
  }
)

/**
 * POST /api/auth/refresh
 * Refresh Token으로 새 토큰 발급 (기존 /token 엔드포인트와 동일 기능)
 */
router.post('/refresh',
  [
    body('refresh_token').notEmpty().withMessage('refresh_token is required'),
    body('client_id').notEmpty().withMessage('client_id is required'),
    body('client_secret').optional()
  ],
  async (req, res) => {
    try {
      // 입력 검증
      const errors = validationResult(req)
      if (!errors.isEmpty()) {
        logger.warn('Invalid refresh token request', {
          errors: errors.array(),
          ip: req.ip
        })

        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'Invalid request parameters'
        })
      }

      const { refresh_token, client_id, client_secret } = req.body

      logger.info('Direct refresh token request', {
        client_id,
        ip: req.ip
      })

      // 클라이언트 검증
      const clientValidation = oidcConfig.validateClient(client_id, client_secret)
      if (!clientValidation.valid) {
        logger.warn('Client validation failed in refresh', {
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
        logger.warn('Refresh token reuse detected in direct refresh', {
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
      await dbService.rotateRefreshToken(
        jti,
        newJti,
        new Date(Date.now() + oidcConfig.getTokenLifetimeInSeconds('refreshToken') * 1000)
      )

      // 새로운 토큰 생성
      const tokens = await generateTokens(
        user, 
        client, 
        'openid profile email',
        refreshTokenData.family_id,
        newJti
      )

      logger.info('Direct refresh successful - tokens issued', {
        username: user.username,
        client_id: client.client_id,
        familyId: refreshTokenData.family_id,
        oldJti: jti,
        newJti,
        ip: req.ip
      })

      res.json({
        ...tokens,
        user: {
          user_id: user.user_id,
          username: user.username,
          email: user.email,
          display_name: user.display_name || user.username,
          level: user.level || 1,
          experience_points: user.experience_points || 0
        }
      })

    } catch (error) {
      logger.error('Direct refresh token error', {
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
 * POST /api/auth/logout
 * 로그아웃 - Refresh Token Family 무효화
 */
router.post('/logout',
  [
    body('refresh_token').notEmpty().withMessage('refresh_token is required'),
    body('client_id').notEmpty().withMessage('client_id is required'),
    body('client_secret').optional()
  ],
  async (req, res) => {
    try {
      const { refresh_token, client_id, client_secret } = req.body

      // 클라이언트 검증
      const clientValidation = oidcConfig.validateClient(client_id, client_secret)
      if (!clientValidation.valid) {
        return res.status(401).json({
          error: clientValidation.error,
          error_description: 'Client authentication failed'
        })
      }

      // JTI 추출
      const jti = extractJtiFromRefreshToken(refresh_token)
      if (!jti) {
        return res.status(400).json({
          error: 'invalid_request',
          error_description: 'Invalid refresh token format'
        })
      }

      // Refresh Token 조회
      const refreshTokenData = await dbService.getRefreshToken(jti)
      if (refreshTokenData && refreshTokenData.client_id === client_id) {
        // Family 무효화
        await dbService.revokeRefreshTokenFamily(refreshTokenData.family_id)
        
        logger.info('Direct logout successful', {
          familyId: refreshTokenData.family_id,
          client_id: client_id,
          ip: req.ip
        })
      }

      res.json({
        message: 'Logout successful'
      })

    } catch (error) {
      logger.error('Direct logout error', {
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
 * Access Token과 Refresh Token 생성
 */
async function generateTokens(user, client, scope, familyId, refreshTokenJti = null) {
  const now = Math.floor(Date.now() / 1000)
  const accessTokenLifetime = oidcConfig.getTokenLifetimeInSeconds('accessToken')
  const refreshTokenLifetime = oidcConfig.getTokenLifetimeInSeconds('refreshToken')

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
    .setExpirationTime(now + accessTokenLifetime)
    .sign(keyManager.getPrivateKey())

  // Refresh Token 생성
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
      null,
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
 * Refresh Token에서 JTI 추출
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