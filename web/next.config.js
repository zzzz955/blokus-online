/** @type {import('next').NextConfig} */
const nextConfig = {
  // Docker 배포를 위한 standalone 모드
  output: 'standalone',
  
  // Git 커밋 해시 기반 빌드 ID (코드 변경시에만 캐시 무효화)
  generateBuildId: async () => {
    if (process.env.GITHUB_SHA) {
      return process.env.GITHUB_SHA.substring(0, 7);
    }
    // 로컬 개발환경에서는 git 커밋 해시 사용
    try {
      const { execSync } = require('child_process');
      const gitHash = execSync('git rev-parse --short HEAD').toString().trim();
      return gitHash;
    } catch {
      return 'dev-build';
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