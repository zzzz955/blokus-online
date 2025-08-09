const express = require('express');
const router = express.Router();
const logger = require('../config/logger');
const { authenticateToken, decodeToken } = require('../middleware/auth');

/**
 * POST /api/auth/validate
 * JWT 토큰 유효성 검증
 */
router.post('/validate',
  authenticateToken,
  async (req, res) => {
    try {
      // authenticateToken 미들웨어를 통과했다면 토큰이 유효함
      const { username, userId, iat, exp } = req.user;

      logger.info('Token validation successful', {
        username,
        userId,
        ip: req.ip,
        userAgent: req.get('User-Agent')
      });

      // 토큰 정보 응답
      const responseData = {
        valid: true,
        username,
        user_id: userId,
        issued_at: new Date(iat * 1000).toISOString(),
        expires_at: new Date(exp * 1000).toISOString(),
        remaining_time: Math.max(0, exp - Math.floor(Date.now() / 1000))
      };

      res.json({
        success: true,
        message: 'Token is valid',
        data: responseData
      });

    } catch (error) {
      logger.error('Token validation error', {
        error: error.message,
        ip: req.ip,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Token validation failed',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * GET /api/auth/info
 * 토큰에서 사용자 정보 추출 (검증 포함)
 */
router.get('/info',
  authenticateToken,
  async (req, res) => {
    try {
      const { username, userId, iat, exp } = req.user;

      logger.debug('Auth info requested', {
        username,
        userId,
        ip: req.ip
      });

      // 토큰 만료까지 남은 시간 계산
      const currentTime = Math.floor(Date.now() / 1000);
      const remainingTime = Math.max(0, exp - currentTime);
      const remainingHours = Math.floor(remainingTime / 3600);
      const remainingMinutes = Math.floor((remainingTime % 3600) / 60);

      const responseData = {
        username,
        user_id: userId,
        token_info: {
          issued_at: new Date(iat * 1000).toISOString(),
          expires_at: new Date(exp * 1000).toISOString(),
          remaining_seconds: remainingTime,
          remaining_human: `${remainingHours}h ${remainingMinutes}m`
        }
      };

      res.json({
        success: true,
        message: 'Authentication info retrieved successfully',
        data: responseData
      });

    } catch (error) {
      logger.error('Failed to retrieve auth info', {
        error: error.message,
        username: req.user?.username,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Failed to retrieve authentication info',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

/**
 * POST /api/auth/refresh
 * 토큰 갱신 (현재는 단순히 토큰 유효성만 확인)
 * 실제 토큰 갱신은 메인 서버에서 처리
 */
router.post('/refresh',
  async (req, res) => {
    try {
      const authHeader = req.headers['authorization'];
      
      if (!authHeader) {
        return res.status(401).json({
          success: false,
          message: 'Authorization token required for refresh',
          error: 'MISSING_TOKEN'
        });
      }

      // Bearer 토큰 추출
      const token = authHeader.startsWith('Bearer ') 
        ? authHeader.slice(7) 
        : authHeader;

      // 토큰 디코딩 (검증 없이)
      const decoded = decodeToken(token);
      
      if (!decoded) {
        return res.status(400).json({
          success: false,
          message: 'Invalid token format',
          error: 'INVALID_TOKEN'
        });
      }

      logger.info('Token refresh requested', {
        username: decoded.username,
        userId: decoded.user_id,
        ip: req.ip
      });

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
      });

    } catch (error) {
      logger.error('Token refresh error', {
        error: error.message,
        ip: req.ip,
        stack: error.stack
      });

      res.status(500).json({
        success: false,
        message: 'Token refresh failed',
        error: 'INTERNAL_SERVER_ERROR'
      });
    }
  }
);

module.exports = router;