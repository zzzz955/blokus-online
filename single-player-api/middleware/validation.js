const { body, param, query, validationResult } = require('express-validator')
const logger = require('../config/logger')

/**
 * 검증 결과를 확인하고 에러 응답을 보내는 미들웨어
 */
const handleValidationErrors = (req, res, next) => {
  const errors = validationResult(req)

  if (!errors.isEmpty()) {
    const errorDetails = errors.array().map(error => ({
      field: error.path || error.param,
      message: error.msg,
      value: error.value
    }))

    logger.warn('Request validation failed', {
      ip: req.ip,
      method: req.method,
      url: req.originalUrl,
      errors: errorDetails
    })

    return res.status(400).json({
      success: false,
      message: 'Validation failed',
      error: 'VALIDATION_ERROR',
      details: errorDetails
    })
  }

  next()
}

/**
 * 스테이지 번호 검증 규칙
 */
const validateStageNumber = [
  param('stageNumber')
    .isInt({ min: 1, max: 1000 })
    .withMessage('Stage number must be an integer between 1 and 1000'),
  handleValidationErrors
]

/**
 * 스테이지 완료 요청 검증 규칙
 */
const validateStageCompletion = [
  body('stage_number')
    .isInt({ min: 1, max: 1000 })
    .withMessage('Stage number must be an integer between 1 and 1000'),

  body('score')
    .isInt({ min: 0, max: 1000 })
    .withMessage('Score must be an integer between 0 and 1000'),

  body('completion_time')
    .optional()
    .isInt({ min: 1 })
    .withMessage('Completion time must be a positive integer (seconds)'),

  body('completed')
    .isBoolean()
    .withMessage('Completed must be a boolean value'),

  handleValidationErrors
]

/**
 * 페이지네이션 검증 규칙
 */
const validatePagination = [
  query('page')
    .optional()
    .isInt({ min: 1 })
    .withMessage('Page must be a positive integer'),

  query('limit')
    .optional()
    .isInt({ min: 1, max: 100 })
    .withMessage('Limit must be an integer between 1 and 100'),

  handleValidationErrors
]

/**
 * 사용자명 검증 규칙 (파라미터용)
 */
const validateUsername = [
  param('username')
    .isLength({ min: 3, max: 50 })
    .withMessage('Username must be between 3 and 50 characters')
    .matches(/^[a-zA-Z0-9_-]+$/)
    .withMessage('Username can only contain letters, numbers, underscore, and dash'),

  handleValidationErrors
]

/**
 * 사용자 인증 요청 검증 (만약 필요한 경우)
 */
const validateAuthRequest = [
  body('username')
    .isLength({ min: 3, max: 50 })
    .withMessage('Username must be between 3 and 50 characters')
    .matches(/^[a-zA-Z0-9_-]+$/)
    .withMessage('Username can only contain letters, numbers, underscore, and dash'),

  body('password')
    .isLength({ min: 6, max: 100 })
    .withMessage('Password must be between 6 and 100 characters'),

  handleValidationErrors
]

/**
 * Content-Type 검증 미들웨어
 */
const requireJson = (req, res, next) => {
  if (req.method === 'POST' || req.method === 'PUT' || req.method === 'PATCH') {
    if (!req.is('application/json')) {
      return res.status(400).json({
        success: false,
        message: 'Content-Type must be application/json',
        error: 'INVALID_CONTENT_TYPE'
      })
    }
  }
  next()
}

/**
 * 요청 크기 검증 미들웨어
 */
const validateRequestSize = (maxSizeInMB = 1) => {
  return (req, res, next) => {
    const contentLength = parseInt(req.get('Content-Length') || '0')
    const maxBytes = maxSizeInMB * 1024 * 1024

    if (contentLength > maxBytes) {
      logger.warn('Request too large', {
        contentLength,
        maxBytes,
        ip: req.ip,
        url: req.originalUrl
      })

      return res.status(413).json({
        success: false,
        message: `Request too large. Maximum size is ${maxSizeInMB}MB`,
        error: 'REQUEST_TOO_LARGE'
      })
    }

    next()
  }
}

/**
 * 사용자 권한 검증 미들웨어
 * 요청한 사용자가 자신의 데이터만 접근할 수 있도록 함
 */
const validateUserAccess = (req, res, next) => {
  const requestedUsername = req.params.username
  const authenticatedUsername = req.user?.username

  if (!authenticatedUsername) {
    return res.status(401).json({
      success: false,
      message: 'Authentication required',
      error: 'AUTHENTICATION_REQUIRED'
    })
  }

  // 대소문자 구분 없이 비교
  if (requestedUsername.toLowerCase() !== authenticatedUsername.toLowerCase()) {
    logger.warn('Unauthorized access attempt', {
      authenticatedUser: authenticatedUsername,
      requestedUser: requestedUsername,
      ip: req.ip,
      url: req.originalUrl
    })

    return res.status(403).json({
      success: false,
      message: 'Access denied. You can only access your own data',
      error: 'ACCESS_DENIED'
    })
  }

  next()
}

module.exports = {
  handleValidationErrors,
  validateStageNumber,
  validateStageCompletion,
  validatePagination,
  validateUsername,
  validateAuthRequest,
  requireJson,
  validateRequestSize,
  validateUserAccess
}
