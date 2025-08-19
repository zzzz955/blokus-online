const express = require('express')
const router = express.Router()
const logger = require('../config/logger')
const dbService = require('../config/database')

/**
 * GET /api/health
 * 서버 상태 확인
 */
router.get('/', async (req, res) => {
  const startTime = Date.now()

  try {
    // 기본 서버 정보
    const serverInfo = {
      service: 'Blokus Single Player API',
      version: '1.0.0',
      environment: process.env.NODE_ENV || 'development',
      timestamp: new Date().toISOString(),
      uptime: process.uptime()
    }

    // 데이터베이스 상태 확인
    let dbStatus = 'unknown'
    let dbResponseTime = null

    try {
      const dbStart = Date.now()
      const dbHealthy = await dbService.healthCheck()
      dbResponseTime = Date.now() - dbStart
      dbStatus = dbHealthy ? 'healthy' : 'unhealthy'
    } catch (error) {
      dbStatus = 'error'
      logger.warn('Database health check failed', { error: error.message })
    }

    // 메모리 사용량
    const memUsage = process.memoryUsage()
    const memoryInfo = {
      rss: Math.round(memUsage.rss / 1024 / 1024), // MB
      heapTotal: Math.round(memUsage.heapTotal / 1024 / 1024), // MB
      heapUsed: Math.round(memUsage.heapUsed / 1024 / 1024), // MB
      external: Math.round(memUsage.external / 1024 / 1024) // MB
    }

    // 전체 응답 시간
    const totalResponseTime = Date.now() - startTime

    const healthData = {
      status: dbStatus === 'healthy' ? 'healthy' : 'degraded',
      server: serverInfo,
      database: {
        status: dbStatus,
        response_time_ms: dbResponseTime
      },
      memory: memoryInfo,
      response_time_ms: totalResponseTime
    }

    // 상태에 따른 HTTP 상태 코드
    const httpStatus = dbStatus === 'healthy' ? 200 : 503

    res.status(httpStatus).json({
      success: true,
      message: 'Health check completed',
      data: healthData
    })

    // 로깅 (정상 상태일 때는 debug, 문제 있을 때는 warn)
    if (dbStatus === 'healthy') {
      logger.debug('Health check passed', {
        dbResponseTime,
        totalResponseTime,
        memoryUsedMB: memoryInfo.heapUsed
      })
    } else {
      logger.warn('Health check failed', {
        dbStatus,
        dbResponseTime,
        totalResponseTime
      })
    }
  } catch (error) {
    const totalResponseTime = Date.now() - startTime

    logger.error('Health check error', {
      error: error.message,
      responseTime: totalResponseTime,
      stack: error.stack
    })

    res.status(503).json({
      success: false,
      message: 'Health check failed',
      error: 'HEALTH_CHECK_ERROR',
      data: {
        status: 'unhealthy',
        response_time_ms: totalResponseTime,
        error_message: error.message
      }
    })
  }
})

/**
 * GET /api/health/ready
 * Kubernetes readiness probe용 간단한 상태 확인
 */
router.get('/ready', async (req, res) => {
  try {
    // 데이터베이스 연결만 확인
    const dbHealthy = await dbService.healthCheck()

    if (dbHealthy) {
      res.status(200).json({
        success: true,
        message: 'Service is ready',
        data: { status: 'ready' }
      })
    } else {
      res.status(503).json({
        success: false,
        message: 'Service is not ready',
        data: { status: 'not_ready', reason: 'database_unavailable' }
      })
    }
  } catch (error) {
    logger.error('Readiness check error', { error: error.message })
    res.status(503).json({
      success: false,
      message: 'Service is not ready',
      data: { status: 'not_ready', reason: 'internal_error' }
    })
  }
})

/**
 * GET /api/health/live
 * Kubernetes liveness probe용 기본 상태 확인
 */
router.get('/live', (req, res) => {
  // 서버가 응답할 수 있으면 살아있는 것으로 간주
  res.status(200).json({
    success: true,
    message: 'Service is alive',
    data: {
      status: 'alive',
      uptime: process.uptime(),
      timestamp: new Date().toISOString()
    }
  })
})

module.exports = router
