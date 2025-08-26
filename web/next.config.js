/** @type {import('next').NextConfig} */
const nextConfig = {
  // Docker 배포를 위한 standalone 모드
  output: 'standalone',
  
  // Git 커밋 해시 기반 빌드 ID (코드 변경시에만 캐시 무효화)
  generateBuildId: async () => {
    // GitHub Actions 환경에서는 GITHUB_SHA 사용
    if (process.env.GITHUB_SHA) {
      return process.env.GITHUB_SHA.substring(0, 7);
    }
    
    // 프로덕션 환경에서는 패키지 버전 기반 빌드 ID 사용
    if (process.env.NODE_ENV === 'production') {
      const packageJson = require('./package.json');
      return `v${packageJson.version}`;
    }
    
    // 로컬 개발환경에서만 git 커밋 해시 시도
    try {
      const { execSync } = require('child_process');
      const gitHash = execSync('git rev-parse --short HEAD', { 
        stdio: ['ignore', 'pipe', 'ignore'],
        timeout: 5000 
      }).toString().trim();
      return gitHash;
    } catch {
      // Git이 없거나 git 명령이 실패하면 타임스탬프 기반 ID 사용
      return `build-${Date.now().toString(36)}`;
    }
  },
  
  images: {
    domains: ['localhost', 'blokus-online.mooo.com'],
  },
  
  async rewrites() {
    return [
      {
        source: '/api/:path*',
        destination: '/api/:path*',
      },
    ];
  },
  
  // 환경 변수 설정
  env: {
    CUSTOM_KEY: process.env.CUSTOM_KEY,
    CLIENT_DOWNLOAD_URL: process.env.CLIENT_DOWNLOAD_URL,
  },
  
  // 정적 파일 경로 설정
  assetPrefix: process.env.NODE_ENV === 'production' ? undefined : undefined,
  
  // 압축 최적화
  compress: true,
  
  // 보안 헤더
  async headers() {
    return [
      {
        source: '/(.*)',
        headers: [
          {
            key: 'X-Frame-Options',
            value: 'DENY',
          },
          {
            key: 'X-Content-Type-Options',
            value: 'nosniff',
          },
          {
            key: 'Referrer-Policy',
            value: 'origin-when-cross-origin',
          },
        ],
      },
    ];
  },
};

module.exports = nextConfig;