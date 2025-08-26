const express = require('express')
const router = express.Router()
const passport = require('../config/passport')
const crypto = require('crypto')
const { v4: uuidv4 } = require('uuid')
const logger = require('../config/logger')
const dbService = require('../config/database')
const oidcConfig = require('../config/oidc')

/**
 * Google OAuth Routes
 * OIDC Authorization Code Flow와 Google OAuth 통합
 * 
 * Flow:
 * 1. Client → GET /authorize (OIDC 파라미터 포함)
 * 2. Server → Redirect to Google OAuth with state
 * 3. Google → Callback with user info
 * 4. Server → Generate authorization code
 * 5. Server → Redirect back to client with code
 */

/**
 * GET /auth/google
 * Google OAuth 시작점 - OIDC 파라미터를 state로 전달
 */
router.get('/auth/google', (req, res, next) => {
  try {
    const {
      client_id,
      redirect_uri,
      scope,
      state,
      nonce,
      code_challenge,
      code_challenge_method
    } = req.query

    logger.info('Google OAuth initiation', {
      client_id,
      redirect_uri,
      has_pkce: !!code_challenge,
      ip: req.ip
    })

    // OIDC 파라미터 검증
    if (!client_id || !redirect_uri || !scope) {
      return res.status(400).json({
        error: 'invalid_request',
        error_description: 'Missing required OIDC parameters'
      })
    }

    // 클라이언트 검증
    const clientValidation = oidcConfig.validateClient(client_id)
    if (!clientValidation.valid) {
      return res.status(400).json({
        error: clientValidation.error,
        error_description: 'Invalid client'
      })
    }

    // Redirect URI 검증
    if (!oidcConfig.validateRedirectUri(client_id, redirect_uri)) {
      return res.status(400).json({
        error: 'invalid_request',
        error_description: 'Invalid redirect_uri'
      })
    }

    // OIDC 파라미터를 state에 인코딩하여 Google OAuth에 전달
    const oidcState = {
      client_id,
      redirect_uri,
      scope,
      state,
      nonce,
      code_challenge,
      code_challenge_method,
      csrf_token: crypto.randomBytes(16).toString('hex')
    }

    // Base64로 인코딩하여 URL에 안전하게 전달
    const encodedState = Buffer.from(JSON.stringify(oidcState)).toString('base64url')

    // 세션에 CSRF 토큰 저장
    req.session.csrf_token = oidcState.csrf_token

    // Google OAuth로 리다이렉트 (state 파라미터 포함)
    passport.authenticate('google', {
      scope: ['profile', 'email'],
      state: encodedState
    })(req, res, next)

  } catch (error) {
    logger.error('Google OAuth initiation error', {
      error: error.message,
      stack: error.stack,
      ip: req.ip
    })

    res.status(500).json({
      error: 'server_error',
      error_description: 'Internal server error during OAuth initiation'
    })
  }
})

/**
 * GET /auth/google/callback
 * Google OAuth 콜백 - 사용자 인증 완료 후 OIDC Authorization Code 발급
 */
router.get('/auth/google/callback', (req, res, next) => {
  const { state: encodedState, error } = req.query

  // OAuth 에러 처리
  if (error) {
    logger.warn('Google OAuth error', {
      error,
      error_description: req.query.error_description,
      ip: req.ip
    })

    return res.status(400).json({
      error: 'access_denied',
      error_description: 'User denied authorization or OAuth error occurred'
    })
  }

  try {
    // State 파라미터에서 OIDC 정보 복원
    if (!encodedState) {
      throw new Error('Missing state parameter')
    }

    const oidcState = JSON.parse(Buffer.from(encodedState, 'base64url').toString())

    // CSRF 토큰 검증
    if (!req.session.csrf_token || req.session.csrf_token !== oidcState.csrf_token) {
      logger.warn('CSRF token mismatch in OAuth callback', {
        sessionToken: req.session.csrf_token,
        stateToken: oidcState.csrf_token,
        ip: req.ip
      })

      return res.status(400).json({
        error: 'invalid_request',
        error_description: 'CSRF protection: Invalid state parameter'
      })
    }

    // Google 사용자 인증
    passport.authenticate('google', { session: true }, async (error, user, info) => {
      try {
        if (error) {
          logger.error('Google OAuth authentication error', {
            error: error.message,
            ip: req.ip
          })

          return res.status(500).json({
            error: 'server_error',
            error_description: 'Authentication service error'
          })
        }

        if (!user) {
          logger.warn('Google OAuth authentication failed', {
            info,
            ip: req.ip
          })

          return res.status(401).json({
            error: 'access_denied',
            error_description: 'Authentication failed'
          })
        }

        // 사용자 로그인 처리
        req.logIn(user, async (loginError) => {
          if (loginError) {
            logger.error('User login error after OAuth', {
              error: loginError.message,
              userId: user.user_id,
              ip: req.ip
            })

            return res.status(500).json({
              error: 'server_error',
              error_description: 'Login processing error'
            })
          }

          try {
            // Authorization Code 생성
            const authCode = crypto.randomBytes(32).toString('base64url')
            const expiresAt = new Date(Date.now() + oidcConfig.getTokenLifetimeInSeconds('authorizationCode') * 1000)

            // Authorization Code 저장
            await dbService.storeAuthorizationCode(
              authCode,
              oidcState.client_id,
              user.user_id,
              oidcState.redirect_uri,
              oidcState.scope,
              oidcState.code_challenge,
              oidcState.code_challenge_method,
              expiresAt
            )

            logger.info('OAuth authorization code issued', {
              userId: user.user_id,
              username: user.username,
              client_id: oidcState.client_id,
              ip: req.ip
            })

            // CSRF 토큰 정리
            delete req.session.csrf_token

            // OIDC 클라이언트로 리다이렉트 (Authorization Code와 함께)
            const redirectUrl = new URL(oidcState.redirect_uri)
            redirectUrl.searchParams.append('code', authCode)
            if (oidcState.state) {
              redirectUrl.searchParams.append('state', oidcState.state)
            }

            res.redirect(redirectUrl.toString())

          } catch (codeError) {
            logger.error('Authorization code generation error', {
              error: codeError.message,
              stack: codeError.stack,
              userId: user.user_id,
              ip: req.ip
            })

            res.status(500).json({
              error: 'server_error',
              error_description: 'Failed to generate authorization code'
            })
          }
        })

      } catch (callbackError) {
        logger.error('OAuth callback processing error', {
          error: callbackError.message,
          stack: callbackError.stack,
          ip: req.ip
        })

        res.status(500).json({
          error: 'server_error',
          error_description: 'Callback processing failed'
        })
      }
    })(req, res, next)

  } catch (stateError) {
    logger.error('State parameter parsing error', {
      error: stateError.message,
      encodedState,
      ip: req.ip
    })

    res.status(400).json({
      error: 'invalid_request',
      error_description: 'Invalid state parameter format'
    })
  }
})

/**
 * POST /auth/logout
 * OAuth 사용자 로그아웃
 */
router.post('/auth/logout', (req, res) => {
  const userId = req.user?.user_id

  req.logout((error) => {
    if (error) {
      logger.error('Logout error', {
        error: error.message,
        userId,
        ip: req.ip
      })

      return res.status(500).json({
        error: 'server_error',
        error_description: 'Logout processing error'
      })
    }

    req.session.destroy((sessionError) => {
      if (sessionError) {
        logger.warn('Session destruction error', {
          error: sessionError.message,
          userId,
          ip: req.ip
        })
      }

      logger.info('User logged out successfully', {
        userId,
        ip: req.ip
      })

      res.json({
        message: 'Logged out successfully'
      })
    })
  })
})

module.exports = router