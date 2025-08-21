require('dotenv').config()
const express = require('express')
const cors = require('cors')
const helmet = require('helmet')
const compression = require('compression')
const logger = require('./config/logger')
const dbService = require('./config/database')
const keyManager = require('./config/keys')

// Import routes
const wellKnownRoutes = require('./routes/well-known')
const authorizeRoutes = require('./routes/authorize')
const tokenRoutes = require('./routes/token')
const jwksRoutes = require('./routes/jwks')
const introspectRoutes = require('./routes/introspect')
const revocationRoutes = require('./routes/revocation')
const adminRoutes = require('./routes/admin')

const app = express()
const PORT = process.env.PORT || 9000

// Security middleware
app.use(helmet({
  contentSecurityPolicy: {
    directives: {
      defaultSrc: ["'self'"],
      scriptSrc: ["'self'", "'unsafe-inline'"],
      styleSrc: ["'self'", "'unsafe-inline'"],
      imgSrc: ["'self'", "data:", "https:"],
    },
  },
  crossOriginEmbedderPolicy: false
}))

// CORS configuration
const corsOptions = {
  origin: function (origin, callback) {
    // Allow requests with no origin (like mobile apps or curl requests)
    if (!origin) return callback(null, true)
    
    const allowedOrigins = [
      'http://localhost:3000',  // Next.js dev server
      'http://localhost:8080',  // API server
      'https://blokus-online.mooo.com'  // Production web
    ]
    
    if (allowedOrigins.indexOf(origin) !== -1) {
      callback(null, true)
    } else {
      callback(new Error('Not allowed by CORS'))
    }
  },
  credentials: true,
  methods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'],
  allowedHeaders: ['Content-Type', 'Authorization', 'X-Requested-With']
}

app.use(cors(corsOptions))
app.use(compression())
app.use(express.json({ limit: '10mb' }))
app.use(express.urlencoded({ extended: true, limit: '10mb' }))

// Request logging middleware
app.use((req, res, next) => {
  logger.info('Request received', {
    method: req.method,
    url: req.url,
    ip: req.ip,
    userAgent: req.get('User-Agent')
  })
  next()
})

// Health check endpoint
app.get('/health', async (req, res) => {
  try {
    // Test database connection
    await dbService.query('SELECT 1')
    
    res.json({
      status: 'healthy',
      timestamp: new Date().toISOString(),
      service: 'oidc-auth-server',
      version: process.env.npm_package_version || '1.0.0',
      uptime: process.uptime(),
      database: 'connected'
    })
  } catch (error) {
    logger.error('Health check failed', { error: error.message })
    res.status(503).json({
      status: 'unhealthy',
      timestamp: new Date().toISOString(),
      service: 'oidc-auth-server',
      error: 'Database connection failed'
    })
  }
})

// Mount OIDC routes
app.use('/.well-known', wellKnownRoutes)
app.use('/authorize', authorizeRoutes)
app.use('/token', tokenRoutes)
app.use('/jwks.json', jwksRoutes)
app.use('/introspect', introspectRoutes)
app.use('/revocation', revocationRoutes)
app.use('/admin', adminRoutes)

// 404 handler
app.use('*', (req, res) => {
  logger.warn('Route not found', {
    method: req.method,
    url: req.originalUrl,
    ip: req.ip
  })
  
  res.status(404).json({
    error: 'not_found',
    error_description: 'The requested endpoint was not found',
    timestamp: new Date().toISOString()
  })
})

// Error handling middleware
app.use((error, req, res, next) => {
  logger.error('Unhandled error', {
    error: error.message,
    stack: error.stack,
    url: req.url,
    method: req.method,
    ip: req.ip
  })

  res.status(500).json({
    error: 'server_error',
    error_description: 'Internal server error occurred',
    timestamp: new Date().toISOString()
  })
})

// Graceful shutdown
process.on('SIGTERM', () => {
  logger.info('SIGTERM received, shutting down gracefully')
  process.exit(0)
})

process.on('SIGINT', () => {
  logger.info('SIGINT received, shutting down gracefully')
  process.exit(0)
})

// Initialize and start server
async function startServer() {
  try {
    // Initialize key manager first
    await keyManager.initialize()
    logger.info('Key manager initialized successfully')

    // Start the server
    app.listen(PORT, () => {
      logger.info(`OIDC Auth Server started on port ${PORT}`, {
        environment: process.env.NODE_ENV || 'development',
        port: PORT,
        timestamp: new Date().toISOString()
      })
    })
  } catch (error) {
    logger.error('Failed to start server', {
      error: error.message,
      stack: error.stack
    })
    process.exit(1)
  }
}

startServer()

module.exports = app