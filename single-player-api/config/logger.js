const winston = require('winston');

// 로그 레벨 설정
const logLevel = process.env.LOG_LEVEL || 'info';
const isProduction = process.env.NODE_ENV === 'production';

// 로그 포맷 정의
const logFormat = winston.format.combine(
  winston.format.timestamp({
    format: 'YYYY-MM-DD HH:mm:ss'
  }),
  winston.format.errors({ stack: true }),
  winston.format.json(),
  winston.format.prettyPrint()
);

// Console 포맷 (개발용)
const consoleFormat = winston.format.combine(
  winston.format.colorize(),
  winston.format.timestamp({
    format: 'HH:mm:ss'
  }),
  winston.format.printf(({ timestamp, level, message, ...meta }) => {
    let log = `${timestamp} [${level}] ${message}`;
    
    // 메타데이터가 있으면 추가
    if (Object.keys(meta).length > 0) {
      log += ` ${JSON.stringify(meta, null, 2)}`;
    }
    
    return log;
  })
);

// Transport 설정
const transports = [
  // Console 출력
  new winston.transports.Console({
    level: logLevel,
    format: isProduction ? logFormat : consoleFormat,
    handleExceptions: true,
    handleRejections: true
  })
];

// 프로덕션 환경에서는 파일 로그도 추가
if (isProduction) {
  transports.push(
    // Error 로그 파일
    new winston.transports.File({
      filename: 'logs/error.log',
      level: 'error',
      format: logFormat,
      maxsize: 5242880, // 5MB
      maxFiles: 5,
      handleExceptions: true,
      handleRejections: true
    }),
    
    // 일반 로그 파일
    new winston.transports.File({
      filename: 'logs/combined.log',
      format: logFormat,
      maxsize: 5242880, // 5MB
      maxFiles: 5
    })
  );
}

// Winston 로거 생성
const logger = winston.createLogger({
  level: logLevel,
  format: logFormat,
  transports,
  exitOnError: false
});

// HTTP 요청 로깅을 위한 Morgan과 연동
logger.stream = {
  write: (message) => {
    // Morgan의 메시지에서 개행 문자 제거
    logger.info(message.trim());
  }
};

// 개발 환경에서만 디버그 정보 출력
if (!isProduction) {
  logger.debug('Logger initialized', {
    level: logLevel,
    environment: process.env.NODE_ENV,
    transports: transports.length
  });
}

module.exports = logger;