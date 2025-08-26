import { NextRequest, NextResponse } from 'next/server';
import fs from 'fs';
import path from 'path';
import { 
  fetchGitHubRelease, 
  getVersionInfo, 
  getMultiPlatformReleaseInfo,
  type GitHubRelease,
  type MultiPlatformRelease,
  type PlatformType
} from '@/lib/github-release';

interface PlatformReleaseInfo {
  version: string;
  filename: string;
  archivePath?: string;
  githubUrl: string;
  available: boolean;
  fileSize: number;
  platform: PlatformType;
}

interface MultiPlatformReleaseInfo {
  version: string;
  releaseDate: string;
  changelog: string[];
  platforms: {
    desktop: PlatformReleaseInfo | null;
    mobile: PlatformReleaseInfo | null;
  };
}

// 메모리 캐시 - 다중 플랫폼 지원
let multiPlatformCache: { data: MultiPlatformReleaseInfo | null; timestamp: number } = { data: null, timestamp: 0 };
const CACHE_DURATION = 5 * 60 * 1000; // 5분

// 환경변수에서 클라이언트 버전 가져오기
function getClientVersion(): string {
  return process.env.CLIENT_VERSION || 'v1.0.0';
}

// 캐시된 다중 플랫폼 릴리즈 정보 확인
function getCachedMultiPlatformRelease(): MultiPlatformReleaseInfo | null {
  const now = Date.now();
  if (multiPlatformCache.data && (now - multiPlatformCache.timestamp) < CACHE_DURATION) {
    return multiPlatformCache.data;
  }
  return null;
}

// 플랫폼별 로컬 파일 경로 생성
function getLocalFilePath(platform: PlatformType, version: string): string {
  const releasesDir = path.join(process.cwd(), '..', 'releases');
  const latestDir = path.join(releasesDir, 'latest');
  
  switch (platform) {
    case 'desktop':
      return path.join(latestDir, `BlokusClient-Desktop-v${version}.zip`);
    case 'mobile':
      return path.join(latestDir, `BlokusClient-Mobile-v${version}.apk`);
    default:
      return '';
  }
}

// GitHub URL 생성
function getGitHubUrl(platform: PlatformType, version: string): string {
  const baseUrl = `https://github.com/zzzz955/blokus-online/releases/download/v${version}`;
  switch (platform) {
    case 'desktop':
      return `${baseUrl}/BlokusClient-Desktop-v${version}.zip`;
    case 'mobile':
      return `${baseUrl}/BlokusClient-Mobile-v${version}.apk`;
    default:
      return '';
  }
}

// 다중 플랫폼 릴리즈 정보 생성 함수
function createPlatformReleaseInfo(
  multiPlatformRelease: MultiPlatformRelease,
  platform: PlatformType
): PlatformReleaseInfo | null {
  const platformAsset = multiPlatformRelease.platforms[platform];
  if (!platformAsset || !platformAsset.available) {
    return null;
  }

  const localFilePath = getLocalFilePath(platform, multiPlatformRelease.version);
  const archivePath = fs.existsSync(localFilePath) ? localFilePath : undefined;
  
  if (archivePath) {
    console.log(`로컬 ${platform} 파일 발견: ${archivePath}`);
  }

  return {
    version: multiPlatformRelease.version,
    filename: platformAsset.filename,
    archivePath,
    githubUrl: platformAsset.downloadUrl,
    available: true,
    fileSize: platformAsset.size,
    platform
  };
}

// 통합된 다중 플랫폼 릴리즈 정보 가져오기
async function getLatestMultiPlatformRelease(): Promise<MultiPlatformReleaseInfo> {
  try {
    // 1. 캐시 확인
    const cached = getCachedMultiPlatformRelease();
    if (cached) {
      console.log(`캐시된 다중 플랫폼 릴리즈 정보 사용: v${cached.version}`);
      return cached;
    }

    // 2. 다중 플랫폼 릴리즈 정보 가져오기
    const multiPlatformRelease = await getMultiPlatformReleaseInfo();
    console.log(`다중 플랫폼 릴리즈 정보 확인됨: v${multiPlatformRelease.version}`);

    // 3. 플랫폼별 릴리즈 정보 생성
    const desktopInfo = createPlatformReleaseInfo(multiPlatformRelease, 'desktop');
    const mobileInfo = createPlatformReleaseInfo(multiPlatformRelease, 'mobile');

    // 4. 최종 다중 플랫폼 릴리즈 정보 생성
    const result: MultiPlatformReleaseInfo = {
      version: multiPlatformRelease.version,
      releaseDate: multiPlatformRelease.releaseDate,
      changelog: multiPlatformRelease.changelog,
      platforms: {
        desktop: desktopInfo,
        mobile: mobileInfo
      }
    };

    // 5. 캐시 업데이트
    multiPlatformCache = {
      data: result,
      timestamp: Date.now()
    };

    console.log(`다중 플랫폼 릴리즈 정보 생성 완료: v${result.version} (데스크톱: ${!!desktopInfo?.archivePath}, 모바일: ${!!mobileInfo?.archivePath})`);
    return result;

  } catch (error) {
    console.error('다중 플랫폼 릴리즈 정보 가져오기 실패:', error);
    
    // fallback - 환경변수 사용
    const fallbackVersion = getClientVersion().replace('v', '');
    return {
      version: fallbackVersion,
      releaseDate: new Date().toISOString(),
      changelog: ['기본 버전 정보'],
      platforms: {
        desktop: {
          version: fallbackVersion,
          filename: `BlokusClient-Desktop-v${fallbackVersion}.zip`,
          githubUrl: getGitHubUrl('desktop', fallbackVersion),
          available: true,
          fileSize: 15670931,
          platform: 'desktop'
        },
        mobile: {
          version: fallbackVersion,
          filename: `BlokusClient-Mobile-v${fallbackVersion}.apk`,
          githubUrl: getGitHubUrl('mobile', fallbackVersion),
          available: true,
          fileSize: 45000000,
          platform: 'mobile'
        }
      }
    };
  }
}

export async function GET(request: NextRequest) {
  try {
    const url = new URL(request.url);
    const platform = url.searchParams.get('platform') as PlatformType || 'desktop';
    
    // 플랫폼 유효성 검사
    if (!['desktop', 'mobile'].includes(platform)) {
      return NextResponse.json(
        { error: '지원되지 않는 플랫폼입니다.' },
        { status: 400 }
      );
    }

    const multiRelease = await getLatestMultiPlatformRelease();
    const platformRelease = multiRelease.platforms[platform];

    if (!platformRelease) {
      return NextResponse.json(
        { error: `${platform} 플랫폼용 클라이언트를 찾을 수 없습니다.` },
        { status: 404 }
      );
    }

    const userAgent = request.headers.get('user-agent') || 'Unknown';
    const ip = request.headers.get('x-forwarded-for') || 
               request.headers.get('x-real-ip') || 
               'Unknown';
    
    console.log(`[DOWNLOAD] ${platform} client download - Version: v${platformRelease.version}, IP: ${ip}, User-Agent: ${userAgent}`);
    
    // 비동기적으로 통계 업데이트
    fetch(`${process.env.NEXTAUTH_URL || 'http://localhost:3000'}/api/download/stats`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'x-internal-api-key': 'internal-stats-update'
      },
      body: JSON.stringify({
        ip,
        userAgent,
        version: platformRelease.version,
        platform,
        source: platformRelease.archivePath ? 'local-releases' : 'github-fallback'
      })
    }).catch(error => {
      console.error('통계 업데이트 실패:', error);
    });

    // 로컬 파일이 있으면 로컬에서 서빙, 없으면 GitHub으로 리다이렉트
    if (platformRelease.archivePath && fs.existsSync(platformRelease.archivePath)) {
      // 로컬 파일 스트리밍
      const fileBuffer = fs.readFileSync(platformRelease.archivePath);
      
      const contentType = platform === 'mobile' ? 'application/vnd.android.package-archive' : 'application/zip';
      
      return new NextResponse(fileBuffer, {
        status: 200,
        headers: {
          'Content-Type': contentType,
          'Content-Disposition': `attachment; filename="${platformRelease.filename}"`,
          'Content-Length': fileBuffer.length.toString(),
          'Cache-Control': 'public, max-age=3600' // 1시간 캐시
        }
      });
    } else {
      // GitHub으로 리다이렉트
      return NextResponse.redirect(platformRelease.githubUrl, 302);
    }
  } catch (error) {
    console.error('클라이언트 다운로드 오류:', error);
    return NextResponse.json(
      { error: '서버 오류가 발생했습니다.' },
      { status: 500 }
    );
  }
}

// 다중 플랫폼 클라이언트 정보 조회 API
export async function POST(request: NextRequest) {
  try {
    const multiRelease = await getLatestMultiPlatformRelease();
    
    return NextResponse.json({
      available: true,
      version: `v${multiRelease.version}`,
      releaseDate: multiRelease.releaseDate,
      changelog: multiRelease.changelog,
      platforms: {
        desktop: multiRelease.platforms.desktop ? {
          available: true,
          filename: multiRelease.platforms.desktop.filename,
          downloadUrl: `/api/download/client?platform=desktop`,
          fileSize: multiRelease.platforms.desktop.fileSize,
          source: multiRelease.platforms.desktop.archivePath ? 'local-releases' : 'github-fallback',
          platform: 'desktop'
        } : null,
        mobile: multiRelease.platforms.mobile ? {
          available: true,
          filename: multiRelease.platforms.mobile.filename,
          downloadUrl: `/api/download/client?platform=mobile`,
          fileSize: multiRelease.platforms.mobile.fileSize,
          source: multiRelease.platforms.mobile.archivePath ? 'local-releases' : 'github-fallback',
          platform: 'mobile'
        } : null
      }
    });
  } catch (error) {
    console.error('다중 플랫폼 클라이언트 정보 조회 오류:', error);
    return NextResponse.json(
      { error: '서버 오류가 발생했습니다.' },
      { status: 500 }
    );
  }
}

// 다중 플랫폼 캐시 무효화 API (내부 사용)
export async function DELETE(request: NextRequest) {
  try {
    const apiKey = request.headers.get('x-internal-api-key');
    if (apiKey !== 'internal-cache-invalidate') {
      return NextResponse.json(
        { error: '권한이 없습니다.' },
        { status: 401 }
      );
    }

    // 다중 플랫폼 캐시 무효화
    multiPlatformCache = { data: null, timestamp: 0 };
    
    console.log('다중 플랫폼 릴리즈 캐시가 무효화되었습니다.');
    
    return NextResponse.json({
      success: true,
      message: '다중 플랫폼 캐시가 무효화되었습니다.',
      timestamp: new Date().toISOString()
    });
  } catch (error) {
    console.error('캐시 무효화 오류:', error);
    return NextResponse.json(
      { error: '서버 오류가 발생했습니다.' },
      { status: 500 }
    );
  }
}