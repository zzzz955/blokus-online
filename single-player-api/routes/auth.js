const express = require('express')
const router = express.Router()
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
    logger.info('Login redirect request (OIDC)', {
      ip: req.ip,
      userAgent: req.get('User-Agent'),
      body: req.body
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
 * 클라이언트 타입별 기본 리디렉트 URI 반환
 */
function getDefaultRedirectUri(clientType, isProduction) {
  const redirectUris = {
    'unity-mobile-client': isProduction 
      ? 'blokus://auth/callback'
      : 'http://localhost:7777/auth/callback',
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

/**
 * POST /api/auth/refresh
 * OIDC 토큰 갱신 - OIDC 서버로 리디렉트
 * 실제 토큰 갱신은 OIDC 서버에서 처리
 */
router.post('/refresh', async (req, res) => {
  try {
    const authHeader = req.headers.authorization
    const { refresh_token, client_id } = req.body

    if (!authHeader && !refresh_token) {
      return res.status(401).json({
        success: false,
        message: 'Authorization token or refresh token required',
        error: 'MISSING_TOKEN'
      })
    }

    // Bearer 토큰 추출 (있는 경우)
    let currentToken = null
    if (authHeader) {
      currentToken = authHeader.startsWith('Bearer ')
        ? authHeader.slice(7)
        : authHeader
    }

    // 토큰 디코딩 (검증 없이 - 정보 확인용)
    const decoded = currentToken ? decodeToken(currentToken) : null

    logger.info('Token refresh redirect request (OIDC)', {
      sub: decoded?.sub,
      hasRefreshToken: !!refresh_token,
      clientId: client_id,
      ip: req.ip
    })

    // 환경별 OIDC 서버 URL 결정
    const isProduction = process.env.NODE_ENV === 'production'
    const oidcBaseUrl = isProduction
      ? (process.env.OIDC_BASE_URL_PROD || 'https://blokus-online.mooo.com')
      : (process.env.OIDC_BASE_URL || 'http://localhost:9000')

    // 이 API 서버에서는 토큰을 새로 발급하지 않음
    // OIDC 서버에서 새 토큰을 받아야 함을 안내
    res.json({
      success: false,
      message: 'Token refresh must be done through OIDC server',
      error: 'OIDC_REFRESH_REQUIRED',
      data: {
        suggestion: 'Use refresh token directly with OIDC token endpoint',
        oidc_endpoints: {
          token: `${oidcBaseUrl}/token`,
          revocation: `${oidcBaseUrl}/revocation`,
          discovery: `${oidcBaseUrl}/.well-known/openid-configuration`
        },
        refresh_flow: {
          method: 'POST',
          url: `${oidcBaseUrl}/token`,
          content_type: 'application/x-www-form-urlencoded',
          body_params: {
            grant_type: 'refresh_token',
            refresh_token: '[YOUR_REFRESH_TOKEN]',
            client_id: client_id || 'unity-mobile-client'
          }
        },
        current_token_info: decoded ? {
          sub: decoded.sub,
          expires_at: new Date(decoded.exp * 1000).toISOString(),
          is_expired: decoded.exp < Math.floor(Date.now() / 1000),
          issuer: decoded.iss
        } : null,
        instructions: {
          ko: '리프레시 토큰으로 OIDC 서버에서 새로운 액세스 토큰을 받아주세요.',
          en: 'Please obtain new access token from OIDC server using refresh token.'
        }
      }
    })
  } catch (error) {
    logger.error('Token refresh redirect error (OIDC)', {
      error: error.message,
      ip: req.ip,
      stack: error.stack
    })

    res.status(500).json({
      success: false,
      message: 'Token refresh redirect failed',
      error: 'INTERNAL_SERVER_ERROR'
    })
  }
})

module.exports = router
