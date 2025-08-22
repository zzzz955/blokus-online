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
 * JWT 토큰 검증 미들웨어 (하이브리드 - OIDC + 로컬 API 토큰 지원)
 * Authorization: Bearer <token> 헤더에서 토큰을 추출하여 검증
 * - OIDC 토큰 (RS256): kid 헤더 있음 → JWKS로 검증
 * - 로컬 API 토큰 (HS256): kid 헤더 없음 → JWT_SECRET으로 검증
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

    // 토큰 헤더 디코딩 (검증 없이)
    const decodedHeader = jwt.decode(token, { complete: true })
    if (!decodedHeader) {
      return res.status(401).json({
        success: false,
        message: 'Invalid token format',
        error: 'INVALID_TOKEN_STRUCTURE'
      })
    }

    let decoded
    let tokenType = 'unknown'

    // 토큰 타입 판별 및 검증
    if (decodedHeader.header.kid) {
      // OIDC 토큰 (RS256) - kid 헤더 존재
      try {
        const kid = decodedHeader.header.kid
        const signingKey = await getSigningKey(kid)
        
        decoded = jwt.verify(token, signingKey, {
          algorithms: ['RS256'],
          issuer: process.env.OIDC_ISSUER || 'http://localhost:9000',
          audience: ['unity-mobile-client', 'qt-desktop-client', 'nextjs-web-client']
        })
        tokenType = 'OIDC'
        
        logger.debug('OIDC token verification successful', {
          kid: kid,
          issuer: decoded.iss,
          subject: decoded.sub
        })
      } catch (error) {
        logger.warn('OIDC token verification failed', { 
          error: error.message, 
          kid: decodedHeader.header.kid 
        })
        throw error
      }
    } else {
      // 로컬 API 토큰 (HS256) - kid 헤더 없음
      try {
        if (!process.env.JWT_SECRET) {
          throw new Error('JWT_SECRET not configured for local token verification')
        }
        
        decoded = jwt.verify(token, process.env.JWT_SECRET, {
          algorithms: ['HS256'],
          issuer: 'blokus-single-api'
        })
        tokenType = 'Local'
        
        logger.debug('Local API token verification successful', {
          issuer: decoded.iss,
          userId: decoded.user_id || decoded.sub,
          username: decoded.username
        })
      } catch (error) {
        logger.warn('Local API token verification failed', { 
          error: error.message,
          issuer: decodedHeader.payload?.iss 
        })
        throw error
      }
    }

    // 토큰 만료 확인
    const currentTime = Math.floor(Date.now() / 1000)
    if (decoded.exp && decoded.exp < currentTime) {
      return res.status(401).json({
        success: false,
        message: 'Token has expired',
        error: 'TOKEN_EXPIRED'
      })
    }

    // 사용자 정보 추가 (토큰 타입에 따라 다른 구조)
    if (tokenType === 'OIDC') {
      // OIDC 표준 클레임 사용
      if (!decoded.sub) {
        return res.status(401).json({
          success: false,
          message: 'Invalid OIDC token: missing subject',
          error: 'INVALID_TOKEN_PAYLOAD'
        })
      }
      
      req.user = {
        sub: decoded.sub,
        username: decoded.preferred_username || decoded.sub,
        userId: decoded.sub,
        email: decoded.email,
        iat: decoded.iat,
        exp: decoded.exp,
        iss: decoded.iss,
        aud: decoded.aud,
        tokenType: 'OIDC'
      }
    } else {
      // 로컬 API 토큰 클레임 사용
      if (!decoded.user_id && !decoded.username) {
        return res.status(401).json({
          success: false,
          message: 'Invalid local token: missing user identification',
          error: 'INVALID_TOKEN_PAYLOAD'
        })
      }
      
      req.user = {
        sub: decoded.user_id?.toString(),
        username: decoded.username,
        userId: decoded.user_id,
        iat: decoded.iat,
        exp: decoded.exp,
        iss: decoded.iss,
        is_guest: decoded.is_guest || false,
        tokenType: 'Local'
      }
    }

    logger.debug(`JWT authentication successful (${tokenType})`, {
      userId: req.user.userId,
      username: req.user.username,
      tokenType: tokenType,
      ip: req.ip,
      userAgent: req.get('User-Agent'),
      issuer: decoded.iss
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
 * JWT 토큰 생성 헬퍼 함수 (로컬 API 전용)
 * @param {Object} payload - 토큰에 포함할 데이터
 * @param {string} payload.username - 사용자명
 * @param {number} payload.userId - 사용자 ID
 * @param {boolean} payload.is_guest - 게스트 여부 (옵션)
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
      isGuest: payload.is_guest || false,
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
