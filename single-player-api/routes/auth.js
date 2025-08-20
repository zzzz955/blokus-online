const express = require('express')
const router = express.Router()
const logger = require('../config/logger')
const { authenticateToken, decodeToken, generateToken } = require('../middleware/auth')
const { body, validationResult } = require('express-validator')
const argon2 = require('argon2')
const dbService = require('../config/database')

/**
 * POST /api/auth/login
 * 사용자 로그인 및 JWT 토큰 발급
 */
router.post('/login',
  [
    body('username').trim().isLength({ min: 3, max: 50 }).withMessage('Username must be 3-50 characters'),
    body('password').isLength({ min: 4, max: 100 }).withMessage('Password must be 4-100 characters')
  ],
  async (req, res) => {
    try {
      // 입력 검증
      const errors = validationResult(req)
      if (!errors.isEmpty()) {
        return res.status(400).json({
          success: false,
          message: 'Invalid input data',
          error: 'VALIDATION_ERROR',
          details: errors.array()
        })
      }

      const { username, password } = req.body

      logger.info('Login attempt', {
        username,
        ip: req.ip,
        userAgent: req.get('User-Agent')
      })

      // 사용자 조회
      const userQuery = `
        SELECT u.user_id, u.username, u.display_name, u.password_hash, u.is_active,
               COALESCE(s.total_games, 0) as total_games,
               COALESCE(s.wins, 0) as wins,
               COALESCE(s.losses, 0) as losses,
               COALESCE(s.level, 1) as level,
               COALESCE(s.experience_points, 0) as experience_points,
               COALESCE(s.total_score, 0) as total_score,
               COALESCE(s.best_score, 0) as best_score,
               COALESCE(s.single_player_level, 1) as single_player_level,
               COALESCE(s.max_stage_completed, 0) as max_stage_completed
        FROM users u 
        LEFT JOIN user_stats s ON u.user_id = s.user_id 
        WHERE LOWER(u.username) = LOWER($1) AND u.is_active = true
      `

      const userResult = await dbService.query(userQuery, [username])

      if (userResult.rows.length === 0) {
        logger.warn('Login failed - user not found', { username, ip: req.ip })
        return res.status(401).json({
          success: false,
          message: 'Invalid username or password',
          error: 'INVALID_CREDENTIALS'
        })
      }

      const user = userResult.rows[0]

      // 비밀번호 검증
      const isValidPassword = await argon2.verify(user.password_hash, password)

      if (!isValidPassword) {
        logger.warn('Login failed - invalid password', { username, ip: req.ip })
        return res.status(401).json({
          success: false,
          message: 'Invalid username or password',
          error: 'INVALID_CREDENTIALS'
        })
      }

      // JWT 토큰 생성
      const tokenPayload = {
        user_id: user.user_id,
        username: user.username
      }

      const token = generateToken(tokenPayload)

      // 로그인 성공 응답
      const responseData = {
        user: {
          user_id: user.user_id,
          username: user.username,
          display_name: user.display_name,
          level: user.level,
          experience_points: user.experience_points,
          single_player_level: user.single_player_level,
          max_stage_completed: user.max_stage_completed,
          stats: {
            total_games: user.total_games,
            wins: user.wins,
            losses: user.losses,
            total_score: user.total_score,
            best_score: user.best_score,
            win_rate: user.total_games > 0 ? Math.round((user.wins / user.total_games) * 100) : 0
          }
        },
        token,
        expires_in: process.env.JWT_EXPIRE_IN || '7d'
      }

      logger.info('Login successful', {
        username,
        userId: user.user_id,
        ip: req.ip
      })

      res.json({
        success: true,
        message: 'Login successful',
        data: responseData
      })
    } catch (error) {
      logger.error('Login error', {
        error: error.message,
        username: req.body?.username,
        ip: req.ip,
        stack: error.stack
      })

      res.status(500).json({
        success: false,
        message: 'Internal server error during login',
        error: 'INTERNAL_SERVER_ERROR'
      })
    }
  }
)

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
 * JWT 토큰 유효성 검증
 */
router.post('/validate',
  authenticateToken,
  async (req, res) => {
    try {
      // authenticateToken 미들웨어를 통과했다면 토큰이 유효함
      const { username, userId, iat, exp } = req.user

      logger.info('Token validation successful', {
        username,
        userId,
        ip: req.ip,
        userAgent: req.get('User-Agent')
      })

      // 토큰 정보 응답
      const responseData = {
        valid: true,
        username,
        user_id: userId,
        issued_at: new Date(iat * 1000).toISOString(),
        expires_at: new Date(exp * 1000).toISOString(),
        remaining_time: Math.max(0, exp - Math.floor(Date.now() / 1000))
      }

      res.json({
        success: true,
        message: 'Token is valid',
        data: responseData
      })
    } catch (error) {
      logger.error('Token validation error', {
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
 * 토큰에서 사용자 정보 추출 (검증 포함)
 */
router.get('/info',
  authenticateToken,
  async (req, res) => {
    try {
      const { username, userId, iat, exp } = req.user

      logger.debug('Auth info requested', {
        username,
        userId,
        ip: req.ip
      })

      // 토큰 만료까지 남은 시간 계산
      const currentTime = Math.floor(Date.now() / 1000)
      const remainingTime = Math.max(0, exp - currentTime)
      const remainingHours = Math.floor(remainingTime / 3600)
      const remainingMinutes = Math.floor((remainingTime % 3600) / 60)

      const responseData = {
        username,
        user_id: userId,
        token_info: {
          issued_at: new Date(iat * 1000).toISOString(),
          expires_at: new Date(exp * 1000).toISOString(),
          remaining_seconds: remainingTime,
          remaining_human: `${remainingHours}h ${remainingMinutes}m`
        }
      }

      res.json({
        success: true,
        message: 'Authentication info retrieved successfully',
        data: responseData
      })
    } catch (error) {
      logger.error('Failed to retrieve auth info', {
        error: error.message,
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
 * 토큰 갱신 (현재는 단순히 토큰 유효성만 확인)
 * 실제 토큰 갱신은 메인 서버에서 처리
 */
router.post('/refresh',
  async (req, res) => {
    try {
      const authHeader = req.headers.authorization

      if (!authHeader) {
        return res.status(401).json({
          success: false,
          message: 'Authorization token required for refresh',
          error: 'MISSING_TOKEN'
        })
      }

      // Bearer 토큰 추출
      const token = authHeader.startsWith('Bearer ')
        ? authHeader.slice(7)
        : authHeader

      // 토큰 디코딩 (검증 없이)
      const decoded = decodeToken(token)

      if (!decoded) {
        return res.status(400).json({
          success: false,
          message: 'Invalid token format',
          error: 'INVALID_TOKEN'
        })
      }

      logger.info('Token refresh requested', {
        username: decoded.username,
        userId: decoded.user_id,
        ip: req.ip
      })

      // 이 API 서버에서는 토큰을 새로 발급하지 않음
      // 메인 서버(TCP 또는 Web)에서 새 토큰을 받아야 함을 안내
      res.json({
        success: false,
        message: 'Token refresh must be done through the main authentication server',
        error: 'REFRESH_NOT_SUPPORTED',
        data: {
          suggestion: 'Please re-authenticate through the main server to get a new token',
          current_token_info: {
            username: decoded.username,
            expires_at: new Date(decoded.exp * 1000).toISOString(),
            is_expired: decoded.exp < Math.floor(Date.now() / 1000)
          }
        }
      })
    } catch (error) {
      logger.error('Token refresh error', {
        error: error.message,
        ip: req.ip,
        stack: error.stack
      })

      res.status(500).json({
        success: false,
        message: 'Token refresh failed',
        error: 'INTERNAL_SERVER_ERROR'
      })
    }
  }
)

module.exports = router
