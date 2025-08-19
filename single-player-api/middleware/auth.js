const jwt = require('jsonwebtoken')
const logger = require('../config/logger')

/**
 * JWT 토큰 검증 미들웨어
 * Authorization: Bearer <token> 헤더에서 토큰을 추출하고 검증
 */
const authenticateToken = (req, res, next) => {
  try {
    const authHeader = req.headers.authorization

    // Authorization 헤더 확인
    if (!authHeader) {
      return res.status(401).json({
        success: false,
        message: 'Authorization token required',
        error: 'MISSING_TOKEN'
      })
    }

    // Bearer 토큰 추출
    const token = authHeader.startsWith('Bearer ')
      ? authHeader.slice(7)
      : authHeader

    if (!token) {
      return res.status(401).json({
        success: false,
        message: 'Invalid token format',
        error: 'INVALID_TOKEN_FORMAT'
      })
    }

    // JWT 토큰 검증
    const decoded = jwt.verify(token, process.env.JWT_SECRET)

    // 토큰이 유효한지 확인 (만료시간 등)
    const currentTime = Math.floor(Date.now() / 1000)
    if (decoded.exp && decoded.exp < currentTime) {
      return res.status(401).json({
        success: false,
        message: 'Token has expired',
        error: 'TOKEN_EXPIRED'
      })
    }

    // 필수 클레임 확인
    if (!decoded.username || !decoded.user_id) {
      return res.status(401).json({
        success: false,
        message: 'Invalid token payload',
        error: 'INVALID_TOKEN_PAYLOAD'
      })
    }

    // 요청 객체에 사용자 정보 추가
    req.user = {
      username: decoded.username,
      userId: decoded.user_id,
      iat: decoded.iat,
      exp: decoded.exp
    }

    logger.debug('JWT authentication successful', {
      username: decoded.username,
      userId: decoded.user_id,
      ip: req.ip,
      userAgent: req.get('User-Agent')
    })

    next()
  } catch (error) {
    logger.warn('JWT authentication failed', {
      error: error.message,
      ip: req.ip,
      userAgent: req.get('User-Agent'),
      authHeader: req.headers.authorization ? 'present' : 'missing'
    })

    // JWT 에러 타입별 처리
    if (error.name === 'JsonWebTokenError') {
      return res.status(401).json({
        success: false,
        message: 'Invalid token',
        error: 'INVALID_TOKEN'
      })
    }

    if (error.name === 'TokenExpiredError') {
      return res.status(401).json({
        success: false,
        message: 'Token has expired',
        error: 'TOKEN_EXPIRED'
      })
    }

    if (error.name === 'NotBeforeError') {
      return res.status(401).json({
        success: false,
        message: 'Token not active yet',
        error: 'TOKEN_NOT_ACTIVE'
      })
    }

    // 일반적인 에러
    return res.status(500).json({
      success: false,
      message: 'Token verification failed',
      error: 'TOKEN_VERIFICATION_ERROR'
    })
  }
}

/**
 * JWT 토큰 생성 헬퍼 함수
 * @param {Object} payload - 토큰에 포함할 데이터
 * @param {string} payload.username - 사용자명
 * @param {number} payload.userId - 사용자 ID
 * @param {string} expiresIn - 만료시간 (기본: 7d)
 * @returns {string} JWT 토큰
 */
const generateToken = (payload, expiresIn = process.env.JWT_EXPIRE_IN || '7d') => {
  try {
    const tokenPayload = {
      username: payload.username,
      user_id: payload.user_id || payload.userId, // 둘 다 지원
      iss: 'blokus-single-api',
      iat: Math.floor(Date.now() / 1000),
      is_guest: payload.is_guest || false
    }

    const token = jwt.sign(tokenPayload, process.env.JWT_SECRET, {
      expiresIn,
      algorithm: 'HS256'
    })

    logger.debug('JWT token generated', {
      username: payload.username,
      userId: payload.user_id || payload.userId,
      expiresIn
    })

    return token
  } catch (error) {
    logger.error('JWT token generation failed', {
      error: error.message,
      payload: { username: payload.username, userId: payload.user_id || payload.userId }
    })
    throw new Error('Token generation failed')
  }
}

/**
 * JWT 토큰 디코딩 (검증 없이)
 * @param {string} token - JWT 토큰
 * @returns {Object|null} 디코딩된 페이로드
 */
const decodeToken = (token) => {
  try {
    return jwt.decode(token)
  } catch (error) {
    logger.warn('JWT token decode failed', { error: error.message })
    return null
  }
}

/**
 * 선택적 인증 미들웨어
 * 토큰이 있으면 검증하고, 없어도 통과시킴
 */
const optionalAuth = (req, res, next) => {
  const authHeader = req.headers.authorization

  if (!authHeader) {
    // 토큰이 없어도 통과
    return next()
  }

  // 토큰이 있으면 검증 시도
  authenticateToken(req, res, next)
}

module.exports = {
  authenticateToken,
  generateToken,
  decodeToken,
  optionalAuth
}
