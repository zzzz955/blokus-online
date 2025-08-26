require('dotenv').config()

const app = require('./app')
const logger = require('./config/logger')
const dbService = require('./config/database')

// 환경 변수 검증
const requiredEnvVars = ['JWT_SECRET', 'DB_HOST', 'DB_USER', 'DB_PASSWORD', 'DB_NAME']
const missingEnvVars = requiredEnvVars.filter(varName => !process.env[varName])

if (missingEnvVars.length > 0) {
  logger.error('Missing required environment variables', {
    missing: missingEnvVars
  })
  process.exit(1)
}

// 서버 설정
const PORT = process.env.PORT || 8080
const HOST = process.env.API_SERVER_HOST || '0.0.0.0'

let server

// 서버 시작 함수
async function startServer () {
  try {
    logger.info('Starting Blokus Single Player API Server...', {
      environment: process.env.NODE_ENV || 'development',
      port: PORT,
      host: HOST
    })

    // 데이터베이스 초기화
    logger.info('Initializing database connection...')
    await dbService.initialize()
    logger.info('Database connection established successfully')

    // HTTP 서버 시작
    server = app.listen(PORT, HOST, () => {
      logger.info('Server started successfully', {
        host: HOST,
        port: PORT,
        environment: process.env.NODE_ENV || 'development',
        pid: process.pid,
        timestamp: new Date().toISOString()
      })

      // 개발 환경에서는 추가 정보 출력
      if (process.env.NODE_ENV !== 'production') {
        console.log('🚀 Server is running!')
        console.log(`📍 URL: http://${HOST === '0.0.0.0' ? 'localhost' : HOST}:${PORT}`)
        console.log(`📚 API Docs: http://${HOST === '0.0.0.0' ? 'localhost' : HOST}:${PORT}/api`)
        console.log(`❤️  Health Check: http://${HOST === '0.0.0.0' ? 'localhost' : HOST}:${PORT}/api/health`)
      }
    })

    // 서버 에러 처리
    server.on('error', (error) => {
      if (error.code === 'EADDRINUSE') {
        logger.error(`Port ${PORT} is already in use`)
      } else {
        logger.error('Server error:', error)
      }
      process.exit(1)
    })

    // Keep-alive 설정
    server.keepAliveTimeout = 65000
    server.headersTimeout = 66000
  } catch (error) {
    logger.error('Failed to start server:', {
      error: error.message,
      stack: error.stack
    })
    process.exit(1)
  }
}

// 서버 종료 함수
async function stopServer () {
  logger.info('Shutting down server...')

  try {
    // HTTP 서버 종료
    if (server) {
      await new Promise((resolve, reject) => {
        server.close((error) => {
          if (error) reject(error)
          else resolve()
        })
      })
      logger.info('HTTP server closed')
    }

    // 데이터베이스 연결 종료
    await dbService.close()
    logger.info('Database connections closed')

    logger.info('Server shutdown completed gracefully')
    process.exit(0)
  } catch (error) {
    logger.error('Error during server shutdown:', {
      error: error.message,
      stack: error.stack
    })
    process.exit(1)
  }
}

// 시그널 처리
process.on('SIGTERM', () => {
  logger.info('SIGTERM received, starting graceful shutdown')
  stopServer()
})

process.on('SIGINT', () => {
  logger.info('SIGINT received, starting graceful shutdown')
  stopServer()
})

// 처리되지 않은 Promise rejection 처리
process.on('unhandledRejection', (reason, promise) => {
  logger.error('Unhandled Promise Rejection:', {
    reason: reason?.message || reason,
    stack: reason?.stack,
    promise
  })

  // 프로덕션에서는 프로세스 종료
  if (process.env.NODE_ENV === 'production') {
    stopServer()
  }
})

// 처리되지 않은 예외 처리
process.on('uncaughtException', (error) => {
  logger.error('Uncaught Exception:', {
    error: error.message,
    stack: error.stack
  })

  // 즉시 종료
  process.exit(1)
})

// 메모리 사용량 모니터링 (개발 환경)
if (process.env.NODE_ENV === 'development') {
  setInterval(() => {
    const memUsage = process.memoryUsage()
    const memInfo = {
      rss: Math.round(memUsage.rss / 1024 / 1024) + ' MB',
      heapTotal: Math.round(memUsage.heapTotal / 1024 / 1024) + ' MB',
      heapUsed: Math.round(memUsage.heapUsed / 1024 / 1024) + ' MB',
      external: Math.round(memUsage.external / 1024 / 1024) + ' MB'
    }

    logger.debug('Memory usage', memInfo)
  }, 60000) // 1분마다
}

// 서버 시작
startServer().catch((error) => {
  logger.error('Failed to start server:', error)
  process.exit(1)
})
