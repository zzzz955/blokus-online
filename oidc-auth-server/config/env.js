/**
 * Environment variables with fallback values for OIDC Auth Server
 * 개발/빌드/프로덕션 환경에서 일관된 동작을 보장
 */

// 개발환경용 fallback 값들
const DEV_FALLBACKS = {
  // OIDC 서버 설정
  NODE_ENV: 'development',
  PORT: '9000',
  LOG_LEVEL: 'info',
  
  // OIDC Issuer 설정
  OIDC_ISSUER: 'http://localhost:9000',
  OIDC_BASE_URL: 'http://localhost:9000',
  
  // Token 수명 설정
  ACCESS_TOKEN_LIFETIME: '10m',
  REFRESH_TOKEN_LIFETIME: '30d',
  REFRESH_TOKEN_MAX_LIFETIME: '90d',
  AUTH_CODE_LIFETIME: '10m',
  
  // 데이터베이스
  DB_HOST: 'localhost',
  DB_PORT: '5432',
  DB_NAME: 'blokus_online',
  DB_USER: 'admin',
  DB_PASSWORD: 'admin',
  DB_POOL_MIN: '2',
  DB_POOL_MAX: '10',
  
  // Google OAuth
  GOOGLE_CLIENT_ID: '',
  GOOGLE_CLIENT_SECRET: '',
  
  // Session & Security
  SESSION_SECRET: 'oidc-session-secret-change-in-production',
  WEB_CLIENT_SECRET: 'web-client-secret-change-in-production',
  ADMIN_TOKEN: 'admin-token-change-in-production',
  
  // URLs
};

/**
 * 환경변수를 가져오되, 없으면 개발용 fallback 사용
 */
function getEnvWithFallback(key, required = true) {
  const value = process.env[key] || DEV_FALLBACKS[key];
  
  if (required && !value) {
    throw new Error(`Environment variable ${key} is required`);
  }
  
  return value;
}

/**
 * 환경변수 설정 객체
 */
const env = {
  // 기본 서버 설정
  NODE_ENV: getEnvWithFallback('NODE_ENV'),
  PORT: getEnvWithFallback('PORT'),
  LOG_LEVEL: getEnvWithFallback('LOG_LEVEL'),
  
  // OIDC Issuer 설정
  OIDC_ISSUER: getEnvWithFallback('OIDC_ISSUER'),
  OIDC_BASE_URL: getEnvWithFallback('OIDC_BASE_URL'),
  
  // Token 수명 설정
  ACCESS_TOKEN_LIFETIME: getEnvWithFallback('ACCESS_TOKEN_LIFETIME'),
  REFRESH_TOKEN_LIFETIME: getEnvWithFallback('REFRESH_TOKEN_LIFETIME'),
  REFRESH_TOKEN_MAX_LIFETIME: getEnvWithFallback('REFRESH_TOKEN_MAX_LIFETIME'),
  AUTH_CODE_LIFETIME: getEnvWithFallback('AUTH_CODE_LIFETIME'),
  
  // 데이터베이스
  DB_HOST: getEnvWithFallback('DB_HOST'),
  DB_PORT: parseInt(getEnvWithFallback('DB_PORT')),
  DB_NAME: getEnvWithFallback('DB_NAME'),
  DB_USER: getEnvWithFallback('DB_USER'),
  DB_PASSWORD: getEnvWithFallback('DB_PASSWORD'),
  DB_POOL_MIN: parseInt(getEnvWithFallback('DB_POOL_MIN')),
  DB_POOL_MAX: parseInt(getEnvWithFallback('DB_POOL_MAX')),
  
  // Google OAuth
  GOOGLE_CLIENT_ID: getEnvWithFallback('GOOGLE_CLIENT_ID', false),
  GOOGLE_CLIENT_SECRET: getEnvWithFallback('GOOGLE_CLIENT_SECRET', false),
  
  // Session & Security
  SESSION_SECRET: getEnvWithFallback('SESSION_SECRET'),
  WEB_CLIENT_SECRET: getEnvWithFallback('WEB_CLIENT_SECRET'),
  ADMIN_TOKEN: getEnvWithFallback('ADMIN_TOKEN'),
  
  // URLs
  
  // 빌드 시점인지 확인
  IS_BUILD_TIME: process.env.NODE_ENV === 'production' && !process.env.DB_HOST
};

/**
 * 개발환경 여부 확인
 */
const isDevelopment = env.NODE_ENV === 'development';

/**
 * 프로덕션 환경 여부 확인
 */
const isProduction = env.NODE_ENV === 'production';

/**
 * 빌드 시점 여부 확인 (Docker build 중)
 */
const isBuildTime = env.IS_BUILD_TIME;

module.exports = {
  env,
  isDevelopment,
  isProduction,
  isBuildTime
};