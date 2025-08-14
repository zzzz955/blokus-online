const express = require('express');
const cors = require('cors');
const helmet = require('helmet');
const compression = require('compression');
const rateLimit = require('express-rate-limit');
const path = require('path');
const logger = require('./config/logger');

// Route imports
const stagesRouter = require('./routes/stages');
const userRouter = require('./routes/user');
const authRouter = require('./routes/auth');
const healthRouter = require('./routes/health');

const app = express();

// Trust proxy (for rate limiting and IP detection)
if (process.env.TRUST_PROXY === 'true') {
  app.set('trust proxy', 1);
}

// Security middleware
app.use(helmet({
  contentSecurityPolicy: {
    directives: {
      defaultSrc: ["'self'"],
      styleSrc: ["'self'", "'unsafe-inline'"],
      scriptSrc: ["'self'"],
      imgSrc: ["'self'", "data:", "https:"],
    },
  },
  crossOriginEmbedderPolicy: false // Unity WebGL 호환성을 위해
}));

// CORS configuration
const corsOptions = {
  origin: function (origin, callback) {
    const allowedOrigins = (process.env.ALLOWED_ORIGINS || 'http://localhost:3000').split(',');
    
    // 개발 환경에서는 origin이 없는 요청도 허용 (Postman, 모바일 앱 등)
    if (!origin && process.env.NODE_ENV === 'development') {
      return callback(null, true);
    }
    
    if (allowedOrigins.includes(origin) || allowedOrigins.includes('*')) {
      callback(null, true);
    } else {
      logger.warn('CORS blocked request', { origin, allowedOrigins });
      callback(new Error('Not allowed by CORS'));
    }
  },
  credentials: true,
  optionsSuccessStatus: 200,
  methods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'],
  allowedHeaders: ['Content-Type', 'Authorization', 'X-Requested-With']
};

app.use(cors(corsOptions));

// Compression middleware
app.use(compression({
  level: parseInt(process.env.COMPRESSION_LEVEL) || 6,
  threshold: 1024 // 1KB 이상만 압축
}));

// Rate limiting
const rateLimitOptions = {
  windowMs: parseInt(process.env.RATE_LIMIT_WINDOW_MS) || 15 * 60 * 1000, // 15분
  max: parseInt(process.env.RATE_LIMIT_MAX_REQUESTS) || 100, // 최대 100 요청
  message: {
    success: false,
    message: 'Too many requests from this IP, please try again later',
    error: 'RATE_LIMIT_EXCEEDED'
  },
  standardHeaders: true,
  legacyHeaders: false,
  handler: (req, res) => {
    logger.warn('Rate limit exceeded', {
      ip: req.ip,
      userAgent: req.get('User-Agent'),
      method: req.method,
      url: req.originalUrl
    });
    
    res.status(429).json({
      success: false,
      message: 'Too many requests from this IP, please try again later',
      error: 'RATE_LIMIT_EXCEEDED',
      retry_after: Math.ceil(rateLimitOptions.windowMs / 1000)
    });
  }
};

// Apply rate limiting to all API routes
app.use('/api/', rateLimit(rateLimitOptions));

// Body parsing middleware
app.use(express.json({ 
  limit: '1mb',
  strict: true
}));
app.use(express.urlencoded({ 
  extended: true, 
  limit: '1mb' 
}));

// Request logging middleware
app.use((req, res, next) => {
  const startTime = Date.now();
  
  // Response logging
  res.on('finish', () => {
    const duration = Date.now() - startTime;
    const logLevel = res.statusCode >= 400 ? 'warn' : 'info';
    
    logger[logLevel]('HTTP Request', {
      method: req.method,
      url: req.originalUrl,
      status: res.statusCode,
      duration: `${duration}ms`,
      ip: req.ip,
      userAgent: req.get('User-Agent'),
      contentLength: res.get('Content-Length') || 0
    });
  });
  
  next();
});

// API Routes
const apiPrefix = process.env.API_PREFIX || '/api';

app.use(`${apiPrefix}/stages`, stagesRouter);
app.use(`${apiPrefix}/user`, userRouter);
app.use(`${apiPrefix}/auth`, authRouter);
app.use(`${apiPrefix}/health`, healthRouter);

// Root endpoint
app.get('/', (req, res) => {
  res.json({
    success: true,
    message: 'Blokus Single Player API Server',
    version: '1.0.0',
    environment: process.env.NODE_ENV || 'development',
    timestamp: new Date().toISOString(),
    endpoints: {
      health: `${apiPrefix}/health`,
      stages: `${apiPrefix}/stages/:stageNumber`,
      progress: `${apiPrefix}/stages/:stageNumber/progress`,
      complete: `${apiPrefix}/stages/complete`,
      profile: `${apiPrefix}/user/profile`,
      stats: `${apiPrefix}/user/stats`,
      auth: `${apiPrefix}/auth/validate`
    }
  });
});

// API documentation endpoint
app.get(`${apiPrefix}`, (req, res) => {
  res.json({
    success: true,
    message: 'Blokus Single Player API Documentation',
    version: '1.0.0',
    endpoints: [
      {
        path: `${apiPrefix}/health`,
        methods: ['GET'],
        description: 'Health check endpoint',
        auth: false
      },
      {
        path: `${apiPrefix}/stages/:stageNumber`,
        methods: ['GET'],
        description: 'Get stage data',
        auth: true
      },
      {
        path: `${apiPrefix}/stages/:stageNumber/progress`,
        methods: ['GET'],
        description: 'Get stage progress',
        auth: true
      },
      {
        path: `${apiPrefix}/stages/complete`,
        methods: ['POST'],
        description: 'Report stage completion',
        auth: true
      },
      {
        path: `${apiPrefix}/user/profile`,
        methods: ['GET'],
        description: 'Get user profile',
        auth: true
      },
      {
        path: `${apiPrefix}/user/stats`,
        methods: ['GET'],
        description: 'Get user detailed statistics',
        auth: true
      },
      {
        path: `${apiPrefix}/user/progress`,
        methods: ['GET'],
        description: 'Get user progress list (paginated)',
        auth: true
      },
      {
        path: `${apiPrefix}/auth/validate`,
        methods: ['POST'],
        description: 'Validate JWT token',
        auth: true
      },
      {
        path: `${apiPrefix}/auth/info`,
        methods: ['GET'],
        description: 'Get token information',
        auth: true
      }
    ]
  });
});

// 404 handler
app.use('*', (req, res) => {
  logger.warn('404 Not Found', {
    method: req.method,
    url: req.originalUrl,
    ip: req.ip
  });

  res.status(404).json({
    success: false,
    message: `Route ${req.method} ${req.originalUrl} not found`,
    error: 'ROUTE_NOT_FOUND',
    available_routes: [
      'GET /',
      `GET ${apiPrefix}`,
      `GET ${apiPrefix}/health`,
      `GET ${apiPrefix}/stages/:stageNumber`,
      `POST ${apiPrefix}/stages/complete`,
      `GET ${apiPrefix}/user/profile`
    ]
  });
});

// Global error handler
app.use((error, req, res, next) => {
  logger.error('Unhandled error', {
    error: error.message,
    stack: error.stack,
    method: req.method,
    url: req.originalUrl,
    ip: req.ip,
    userAgent: req.get('User-Agent')
  });

  // CORS 에러 처리
  if (error.message === 'Not allowed by CORS') {
    return res.status(403).json({
      success: false,
      message: 'CORS policy violation',
      error: 'CORS_ERROR'
    });
  }

  // JSON parsing 에러 처리
  if (error instanceof SyntaxError && error.status === 400 && 'body' in error) {
    return res.status(400).json({
      success: false,
      message: 'Invalid JSON format',
      error: 'INVALID_JSON'
    });
  }

  // 기본 에러 응답
  res.status(500).json({
    success: false,
    message: 'Internal server error',
    error: 'INTERNAL_SERVER_ERROR',
    ...(process.env.NODE_ENV === 'development' && { 
      details: error.message 
    })
  });
});

module.exports = app;