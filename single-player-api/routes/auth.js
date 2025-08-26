const express = require('express')
const router = express.Router()
const jwt = require('jsonwebtoken')
const logger = require('../config/logger')
const { authenticateToken, decodeToken, generateToken } = require('../middleware/auth')
const { body, validationResult } = require('express-validator')
const argon2 = require('argon2')
const dbService = require('../config/database')

/**
 * POST /api/auth/login
 * OIDC 기반 로그인 - 인증 서버로 리디렉트
 * 모바일 앱에서 로그인 시도 시 OIDC 플로우로 안내
 */
router.post('/login', async (req, res) => {
  try {
    const { username, password } = req.body

    // ID/PW 로그인 시도 (개발/프로덕션 모두 지원)
    if (username && password) {
      logger.info('Development direct login attempt', {
        username,
        ip: req.ip,
        userAgent: req.get('User-Agent')
      })

      // 데이터베이스 기반 ID/PW 인증
      try {
        const user = await dbService.authenticateUser(username, password)
        if (user) {
          // 사용자 통계 정보 가져오기
          const userStats = await dbService.getUserStats(user.user_id)
          
          const accessPayload = {
            user_id: user.user_id,
            username: user.username,
            email: user.email,
            iss: 'blokus-single-api',
            aud: 'unity-mobile-client',
            iat: Math.floor(Date.now() / 1000),
            exp: Math.floor(Date.now() / 1000) + (24 * 60 * 60) // 1일
          }

          const refreshPayload = {
            user_id: user.user_id,
            username: user.username,
            type: 'refresh',
            iss: 'blokus-single-api',
            aud: 'unity-mobile-client',
            iat: Math.floor(Date.now() / 1000),
            exp: Math.floor(Date.now() / 1000) + (30 * 24 * 60 * 60) // 30일
          }

          const accessToken = jwt.sign(accessPayload, process.env.JWT_SECRET)
          const refreshToken = jwt.sign(refreshPayload, process.env.JWT_SECRET)

          logger.info('Database login successful', {
            username,
            userId: user.user_id,
            ip: req.ip
          })

          return res.json({
            success: true,
            message: 'Login successful',
            data: {
              access_token: accessToken,
              refresh_token: refreshToken,
              token_type: 'Bearer',
              expires_in: 24 * 60 * 60, // access token 만료 시간
              user: {
                user_id: user.user_id,
                username: user.username,
                email: user.email,
                display_name: user.username, // username을 display_name으로 사용
                single_player_level: userStats?.single_player_level || 1,
                max_stage_completed: userStats?.max_stage_completed || 0,
                single_player_score: userStats?.single_player_score || 0
              }
            }
          })
        } else {
          // DB에 사용자가 없거나 비밀번호 불일치
          logger.warn('Database authentication failed - invalid credentials', {
            username,
            ip: req.ip
          })
          
          return res.status(401).json({
            success: false,
            message: '아이디 또는 비밀번호가 올바르지 않습니다.',
            error: 'INVALID_CREDENTIALS'
          })
        }
      } catch (dbError) {
        logger.error('Database authentication error', {
          error: dbError.message,
          username,
          ip: req.ip
        })
        
        return res.status(500).json({
          success: false,
          message: '서버 오류가 발생했습니다. 잠시 후 다시 시도해주세요.',
          error: 'DATABASE_ERROR'
        })
      }
    }

    // DB 인증 실패 시에만 OIDC 플로우로 리다이렉트
    logger.info('Login redirect request (OIDC)', {
      ip: req.ip,
      userAgent: req.get('User-Agent'),
      body: { username: req.body.username } // 비밀번호는 로그에 기록하지 않음
    })

    // 클라이언트 타입 감지 (요청 본문이나 헤더에서)
    const { client_type, redirect_uri, state } = req.body
    const userAgent = req.get('User-Agent') || ''
    
    // Unity 모바일 클라이언트 감지
    let detectedClientType = 'unity-mobile-client'
    if (client_type) {
      detectedClientType = client_type
    } else if (userAgent.includes('Qt') || userAgent.includes('Desktop')) {
      detectedClientType = 'qt-desktop-client'
    }

    // 환경별 OIDC 서버 URL 결정
    const isProduction = process.env.NODE_ENV === 'production'
    const oidcBaseUrl = isProduction
      ? (process.env.OIDC_BASE_URL_PROD || 'https://blokus-online.mooo.com')
      : (process.env.OIDC_BASE_URL || 'http://localhost:9000')

    // OIDC Authorization URL 구성
    const authParams = new URLSearchParams({
      response_type: 'code',
      client_id: detectedClientType,
      scope: 'openid profile email',
      redirect_uri: redirect_uri || getDefaultRedirectUri(detectedClientType, isProduction),
      state: state || generateRandomState()
    })

    // PKCE는 클라이언트에서 생성해야 함을 안내
    const authUrl = `${oidcBaseUrl}/authorize?${authParams.toString()}`

    // 모바일/데스크톱 클라이언트에서는 이 응답을 받아서 브라우저를 열어야 함
    res.json({
      success: false, // 직접 로그인이 아니므로 false
      message: 'Authentication must be completed through OIDC flow',
      error: 'OIDC_REDIRECT_REQUIRED',
      data: {
        auth_url: authUrl,
        client_id: detectedClientType,
        auth_type: 'oidc_pkce',
        instructions: {
          ko: 'OIDC 인증을 위해 시스템 브라우저에서 로그인을 완료해주세요.',
          en: 'Please complete authentication in system browser for OIDC flow.'
        },
        flow_steps: [
          '1. 클라이언트에서 PKCE challenge 생성',
          '2. 시스템 브라우저에서 OIDC 인증',
          '3. Authorization code 받기',
          '4. Token endpoint로 Access Token 교환'
        ],
        pkce_required: detectedClientType !== 'nextjs-web-client',
        endpoints: {
          authorization: `${oidcBaseUrl}/authorize`,
          token: `${oidcBaseUrl}/token`,
          jwks: `${oidcBaseUrl}/jwks.json`,
          discovery: `${oidcBaseUrl}/.well-known/openid-configuration`
        }
      }
    })

    logger.info('Login redirect provided (OIDC)', {
      authUrl,
      clientType: detectedClientType,
      environment: isProduction ? 'production' : 'development',
      ip: req.ip
    })
  } catch (error) {
    logger.error('Login redirect error (OIDC)', {
      error: error.message,
      ip: req.ip,
      stack: error.stack
    })

    res.status(500).json({
      success: false,
      message: 'Failed to provide OIDC authentication redirect',
      error: 'INTERNAL_SERVER_ERROR'
    })
  }
})

/**
 * POST /api/auth/refresh
 * Refresh Token을 사용한 Access Token 갱신
 */
router.post('/refresh', async (req, res) => {
  try {
    const { refresh_token } = req.body

    if (!refresh_token) {
      return res.status(400).json({
        success: false,
        message: 'Refresh token이 필요합니다.',
        error: 'MISSING_REFRESH_TOKEN'
      })
    }

    // Refresh Token 검증
    let decoded
    try {
      decoded = jwt.verify(refresh_token, process.env.JWT_SECRET)
    } catch (jwtError) {
      logger.warn('Invalid refresh token', {
        error: jwtError.message,
        ip: req.ip
      })

      return res.status(401).json({
        success: false,
        message: 'Refresh token이 유효하지 않습니다.',
        error: 'INVALID_REFRESH_TOKEN'
      })
    }

    // Refresh Token인지 확인
    if (decoded.type !== 'refresh') {
      return res.status(401).json({
        success: false,
        message: '올바른 refresh token이 아닙니다.',
        error: 'INVALID_TOKEN_TYPE'
      })
    }

    // 사용자 정보 조회 (DB에서 최신 정보)
    const user = await dbService.getUserById(decoded.user_id)
    if (!user || !user.is_active) {
      return res.status(401).json({
        success: false,
        message: '사용자 계정이 비활성화되었습니다.',
        error: 'USER_DEACTIVATED'
      })
    }

    // 새로운 Access Token 및 Refresh Token 발급
    const newAccessPayload = {
      user_id: user.user_id,
      username: user.username,
      email: user.email,
      iss: 'blokus-single-api',
      aud: 'unity-mobile-client',
      iat: Math.floor(Date.now() / 1000),
      exp: Math.floor(Date.now() / 1000) + (24 * 60 * 60) // 1일
    }

    const newRefreshPayload = {
      user_id: user.user_id,
      username: user.username,
      type: 'refresh',
      iss: 'blokus-single-api',
      aud: 'unity-mobile-client',
      iat: Math.floor(Date.now() / 1000),
      exp: Math.floor(Date.now() / 1000) + (30 * 24 * 60 * 60) // 30일
    }

    const newAccessToken = jwt.sign(newAccessPayload, process.env.JWT_SECRET)
    const newRefreshToken = jwt.sign(newRefreshPayload, process.env.JWT_SECRET)

    logger.info('Token refresh successful', {
      userId: user.user_id,
      username: user.username,
      ip: req.ip
    })

    res.json({
      success: true,
      message: 'Token refreshed successfully',
      data: {
        access_token: newAccessToken,
        refresh_token: newRefreshToken,
        token_type: 'Bearer',
        expires_in: 24 * 60 * 60,
        user: {
          user_id: user.user_id,
          username: user.username,
          email: user.email
        }
      }
    })

  } catch (error) {
    logger.error('Token refresh error', {
      error: error.message,
      stack: error.stack,
      ip: req.ip
    })

    res.status(500).json({
      success: false,
      message: '토큰 갱신 중 오류가 발생했습니다.',
      error: 'TOKEN_REFRESH_ERROR'
    })
  }
})

/**
 * 클라이언트 타입별 기본 리디렉트 URI 반환
 */
function getDefaultRedirectUri(clientType, isProduction) {
  const redirectUris = {
    'unity-mobile-client': 'blokus://auth/callback', // 개발/프로덕션 모두 동일한 Deep Link 사용
    'qt-desktop-client': isProduction
      ? 'http://localhost:8080/auth/callback'  // 프로덕션에서도 로컬
      : 'http://localhost:8080/auth/callback',
    'nextjs-web-client': isProduction
      ? 'https://blokus-online.mooo.com/api/auth/callback/blokus-oidc'
      : 'http://localhost:3000/api/auth/callback/blokus-oidc'
  }
  
  return redirectUris[clientType] || redirectUris['unity-mobile-client']
}

/**
 * 랜덤 state 파라미터 생성
 */
function generateRandomState() {
  return require('crypto').randomBytes(16).toString('hex')
}

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
 * POST /api/auth/register
 * OAuth 기반 회원가입 - 웹 페이지로 리다이렉트
 * 모바일 앱에서 회원가입 시도 시 웹 OAuth 플로우로 안내
 */
router.post('/register', async (req, res) => {
  try {
    logger.info('Registration redirect request', {
      ip: req.ip,
      userAgent: req.get('User-Agent')
    })

    // 환경별 웹 등록 URL 결정
    const isProduction = process.env.NODE_ENV === 'production'
    const webRegisterUrl = isProduction
      ? (process.env.WEB_REGISTER_URL_PROD || 'https://blokus-online.mooo.com/register')
      : (process.env.WEB_REGISTER_URL_DEV || 'http://localhost:3000/register')

    // 선택적 파라미터 처리 (앱에서 전달한 정보가 있다면)
    const { app_callback, user_agent, device_id } = req.body

    // URL 파라미터 구성
    const urlParams = new URLSearchParams()
    if (app_callback) urlParams.append('callback', app_callback)
    if (user_agent) urlParams.append('source', 'mobile_app')
    if (device_id) urlParams.append('device_id', device_id)

    const finalUrl = urlParams.toString()
      ? `${webRegisterUrl}?${urlParams.toString()}`
      : webRegisterUrl

    // 모바일 앱에서는 이 응답을 받아서 브라우저나 WebView로 URL을 열어야 함
    res.json({
      success: false, // 직접 회원가입이 아니므로 false
      message: 'Registration must be completed through web OAuth flow',
      error: 'OAUTH_REDIRECT_REQUIRED',
      data: {
        redirect_url: finalUrl,
        registration_type: 'oauth_web',
        instructions: {
          ko: 'OAuth 인증을 위해 웹 브라우저에서 회원가입을 완료해주세요.',
          en: 'Please complete registration in web browser for OAuth authentication.'
        },
        flow_steps: [
          '1. 웹 브라우저에서 OAuth 인증 (Google/Discord 등)',
          '2. ID, 비밀번호, 닉네임 설정',
          '3. 회원가입 완료 후 앱에서 로그인'
        ]
      }
    })

    logger.info('Registration redirect provided', {
      redirectUrl: finalUrl,
      environment: isProduction ? 'production' : 'development',
      ip: req.ip
    })
  } catch (error) {
    logger.error('Registration redirect error', {
      error: error.message,
      ip: req.ip,
      stack: error.stack
    })

    res.status(500).json({
      success: false,
      message: 'Failed to provide registration redirect',
      error: 'INTERNAL_SERVER_ERROR'
    })
  }
})

/**
 * POST /api/auth/guest
 * 게스트 로그인 (임시 사용자)
 */
router.post('/guest',
  async (req, res) => {
    try {
      // 게스트 사용자명 생성
      const guestId = Date.now()
      const guestUsername = `guest_${guestId}`

      logger.info('Guest login attempt', {
        guestUsername,
        ip: req.ip
      })

      // JWT 토큰 생성 (게스트용)
      const tokenPayload = {
        user_id: 0, // 게스트는 user_id가 0
        username: guestUsername,
        is_guest: true
      }

      const token = generateToken(tokenPayload)

      // 게스트 로그인 응답
      const responseData = {
        user: {
          user_id: 0,
          username: guestUsername,
          level: 1,
          experience_points: 0,
          single_player_level: 1,
          max_stage_completed: 0,
          is_guest: true,
          stats: {
            total_games: 0,
            wins: 0,
            losses: 0,
            total_score: 0,
            best_score: 0,
            win_rate: 0
          }
        },
        token,
        expires_in: process.env.JWT_EXPIRE_IN || '7d'
      }

      logger.info('Guest login successful', {
        guestUsername,
        ip: req.ip
      })

      res.json({
        success: true,
        message: 'Guest login successful',
        data: responseData
      })
    } catch (error) {
      logger.error('Guest login error', {
        error: error.message,
        ip: req.ip,
        stack: error.stack
      })

      res.status(500).json({
        success: false,
        message: 'Internal server error during guest login',
        error: 'INTERNAL_SERVER_ERROR'
      })
    }
  }
)

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
