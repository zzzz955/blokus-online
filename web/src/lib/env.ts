/**
 * Environment variables with fallback values
 * 개발/빌드/프로덕션 환경에서 일관된 동작을 보장
 */

// 개발환경용 fallback 값들
const DEV_FALLBACKS = {
  NEXTAUTH_SECRET: 'dev-nextauth-secret-32-chars-min',
  JWT_SECRET: 'dev-jwt-secret-32-chars-minimum',
  JWT_REFRESH_SECRET: 'dev-jwt-refresh-secret-32-chars',
  DATABASE_URL: 'postgresql://admin:admin@localhost:5432/blokus_online',
  GOOGLE_CLIENT_ID: '',
  GOOGLE_CLIENT_SECRET: '',
  NEXTAUTH_URL: 'http://localhost:3000',
  CLIENT_VERSION: 'v1.0.0',
  INTERNAL_API_KEY: 'dev-internal-api-key',
  ADMIN_SETUP_KEY: 'setup-admin-2024',
  APP_ROOT_DIR: '',
  THUMBNAIL_PUBLIC_PATH: '/api/thumbnails',
  THUMBNAIL_STORAGE_DIR: '../data/stage-thumbnails',
  ALLOW_ADMIN_CREATION: 'false'
} as const;

/**
 * 환경변수를 가져오되, 없으면 개발용 fallback 사용
 */
function getEnvWithFallback<T extends keyof typeof DEV_FALLBACKS>(
  key: T, 
  required: boolean = true
): string {
  const value = process.env[key] || DEV_FALLBACKS[key];
  
  if (required && !value) {
    throw new Error(`Environment variable ${key} is required`);
  }
  
  return value;
}

/**
 * 환경변수 설정 객체
 */
export const env = {
  // 인증 관련
  NEXTAUTH_SECRET: getEnvWithFallback('NEXTAUTH_SECRET'),
  JWT_SECRET: getEnvWithFallback('JWT_SECRET'),
  JWT_REFRESH_SECRET: getEnvWithFallback('JWT_REFRESH_SECRET'),
  NEXTAUTH_URL: getEnvWithFallback('NEXTAUTH_URL', false),
  
  // 데이터베이스
  DATABASE_URL: getEnvWithFallback('DATABASE_URL'),
  
  // OAuth 설정 (선택사항)
  GOOGLE_CLIENT_ID: getEnvWithFallback('GOOGLE_CLIENT_ID', false),
  GOOGLE_CLIENT_SECRET: getEnvWithFallback('GOOGLE_CLIENT_SECRET', false),
  
  // 애플리케이션 설정
  CLIENT_VERSION: getEnvWithFallback('CLIENT_VERSION', false),
  INTERNAL_API_KEY: getEnvWithFallback('INTERNAL_API_KEY', false),
  ADMIN_SETUP_KEY: getEnvWithFallback('ADMIN_SETUP_KEY', false),
  ALLOW_ADMIN_CREATION: getEnvWithFallback('ALLOW_ADMIN_CREATION', false),
  
  // 파일 저장 설정
  APP_ROOT_DIR: getEnvWithFallback('APP_ROOT_DIR', false) || process.cwd(),
  THUMBNAIL_PUBLIC_PATH: getEnvWithFallback('THUMBNAIL_PUBLIC_PATH', false),
  THUMBNAIL_STORAGE_DIR: getEnvWithFallback('THUMBNAIL_STORAGE_DIR', false),
  
  // 환경 타입
  NODE_ENV: process.env.NODE_ENV || 'development',
  
  // 빌드 시점인지 확인
  IS_BUILD_TIME: process.env.NODE_ENV === 'production' && !process.env.DATABASE_URL,
  
  // 패키지 버전
  NPM_PACKAGE_VERSION: process.env.npm_package_version || '1.0.0'
} as const;

/**
 * 개발환경 여부 확인
 */
export const isDevelopment = env.NODE_ENV === 'development';

/**
 * 프로덕션 환경 여부 확인
 */
export const isProduction = env.NODE_ENV === 'production';

/**
 * 빌드 시점 여부 확인 (Docker build 중)
 */
export const isBuildTime = env.IS_BUILD_TIME;