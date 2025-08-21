const jwt = require('jsonwebtoken')
const jwksClient = require('jwks-rsa')
const logger = require('../config/logger')

// JWKS 클라이언트 설정
const jwksClientInstance = jwksClient({
  jwksUri: process.env.OIDC_JWKS_URI || 'http://localhost:9000/jwks.json',
  cache: true,
  cacheMaxEntries: 5,
  cacheMaxAge: 10 * 60 * 1000, // 10분 캐시
  rateLimit: true,
  jwksRequestsPerMinute: 10
})

// 공개키 가져오기 함수
const getSigningKey = (kid) => {
  return new Promise((resolve, reject) => {
    jwksClientInstance.getSigningKey(kid, (err, key) => {
      if (err) {
        logger.error('Failed to get signing key from JWKS', { 
          error: err.message, 
          kid 
        })
        reject(err)
        return
      }
      
      const signingKey = key.getPublicKey()
      resolve(signingKey)
    })
  })
}

/**
 * JWT 토큰 검증 미들웨어 (JWKS 기반)
 * Authorization: Bearer <token> 헤더에서 토큰을 추출하고 OIDC 서버에서 검증
 */
const authenticateToken = async (req, res, next) => {
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

    // 토큰 헤더에서 kid 추출
    const decodedHeader = jwt.decode(token, { complete: true })
    if (!decodedHeader || !decodedHeader.header.kid) {
      return res.status(401).json({
        success: false,
        message: 'Invalid token: missing key ID',
        error: 'MISSING_KEY_ID'
      })
    }

    const kid = decodedHeader.header.kid

    // JWKS에서 공개키 가져오기
    const signingKey = await getSigningKey(kid)

    // JWT 토큰 검증 (RS256)
    const decoded = jwt.verify(token, signingKey, {
      algorithms: ['RS256'],
      issuer: process.env.OIDC_ISSUER || 'http://localhost:9000',
      audience: ['unity-mobile-client', 'qt-desktop-client', 'nextjs-web-client']
    })

    // 토큰이 유효한지 확인 (만료시간 등)
    const currentTime = Math.floor(Date.now() / 1000)
    if (decoded.exp && decoded.exp < currentTime) {
      return res.status(401).json({
        success: false,
        message: 'Token has expired',
        error: 'TOKEN_EXPIRED'
      })
    }

    // 필수 클레임 확인 (OIDC 표준)
    if (!decoded.sub) {
      return res.status(401).json({
        success: false,
        message: 'Invalid token payload: missing subject',
        error: 'INVALID_TOKEN_PAYLOAD'
      })
    }

    // 요청 객체에 사용자 정보 추가 (OIDC 표준 클레임 사용)
    req.user = {
      sub: decoded.sub,
      username: decoded.preferred_username || decoded.sub,
      userId: decoded.sub, // sub을 user_id로 사용
      email: decoded.email,
      iat: decoded.iat,
      exp: decoded.exp,
      iss: decoded.iss,
      aud: decoded.aud
    }

    logger.debug('JWT authentication successful (OIDC)', {
      sub: decoded.sub,
      username: decoded.preferred_username,
      ip: req.ip,
      userAgent: req.get('User-Agent'),
      issuer: decoded.iss
    })

    next()
  } catch (error) {
    logger.warn('JWT authentication failed (OIDC)', {
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

    // JWKS 관련 에러
    if (error.message.includes('JWKS') || error.message.includes('signing key')) {
      return res.status(401).json({
        success: false,
        message: 'Token verification failed: unable to get signing key',
        error: 'JWKS_ERROR'
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
 * @deprecated 이 API 서버는 더 이상 토큰을 생성하지 않습니다.
 * 모든 토큰은 OIDC 서버에서 발급받아야 합니다.
 * 
 * JWT 토큰 생성 헬퍼 함수 (게스트 토큰용으로만 유지)
 * @param {Object} payload - 토큰에 포함할 데이터
 * @param {string} payload.username - 사용자명
 * @param {number} payload.userId - 사용자 ID
 * @param {string} expiresIn - 만료시간 (기본: 7d)
 * @returns {string} JWT 토큰
 */
const generateToken = (payload, expiresIn = process.env.JWT_EXPIRE_IN || '7d') => {
  try {
    // 게스트 토큰만 허용
    if (!payload.is_guest) {
      throw new Error('Token generation is only allowed for guest users. Use OIDC server for regular users.')
    }

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

    logger.debug('JWT guest token generated', {
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
const optionalAuth = async (req, res, next) => {
  const authHeader = req.headers.authorization

  if (!authHeader) {
    // 토큰이 없어도 통과
    return next()
  }

  // 토큰이 있으면 검증 시도
  await authenticateToken(req, res, next)
}

module.exports = {
  authenticateToken,
  generateToken,
  decodeToken,
  optionalAuth
}
