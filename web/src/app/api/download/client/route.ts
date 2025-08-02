import { NextRequest, NextResponse } from 'next/server';
import fs from 'fs';
import path from 'path';
import { fetchGitHubRelease, getVersionInfo, type GitHubRelease } from '@/lib/github-release';

interface ReleaseInfo {
  version: string;
  filename: string;
  archivePath?: string;
  releaseInfo?: any;
  githubUrl: string;
}

// 메모리 캐시
let releaseCache: { data: ReleaseInfo | null; timestamp: number } = { data: null, timestamp: 0 };
const CACHE_DURATION = 5 * 60 * 1000; // 5분

// 공통 함수 사용 (fetchGitHubRelease는 이제 lib에서 import)

// 환경변수에서 클라이언트 버전 가져오기
function getClientVersion(): string {
  return process.env.CLIENT_VERSION || 'v1.0.0';
}

// 캐시된 릴리즈 정보 확인
function getCachedRelease(): ReleaseInfo | null {
  const now = Date.now();
  if (releaseCache.data && (now - releaseCache.timestamp) < CACHE_DURATION) {
    return releaseCache.data;
  }
  return null;
}

// 통합된 릴리즈 정보 가져오기 (공통 함수 활용)
async function getLatestRelease(): Promise<ReleaseInfo> {
  try {
    // 1. 캐시 확인
    const cached = getCachedRelease();
    if (cached) {
      console.log(`캐시된 릴리즈 정보 사용: v${cached.version}`);
      return cached;
    }

    // 2. 통합된 버전 정보 가져오기 (버전 API와 동일한 로직)
    const versionInfo = await getVersionInfo();
    console.log(`통합 API에서 버전 정보 확인됨: v${versionInfo.version}`);

    // 3. 로컬 파일 확인
    const releasesDir = path.join(process.cwd(), '..', 'releases');
    const latestDir = path.join(releasesDir, 'latest');
    let archivePath: string | undefined;

    if (fs.existsSync(latestDir)) {
      const localArchivePath = path.join(latestDir, `BlokusClient-v${versionInfo.version}.zip`);
      if (fs.existsSync(localArchivePath)) {
        archivePath = localArchivePath;
        console.log(`로컬 파일 발견: ${archivePath}`);
      }
    }

    // 4. 최종 릴리즈 정보 생성
    const result: ReleaseInfo = {
      version: versionInfo.version,
      filename: `BlokusClient-v${versionInfo.version}.zip`,
      archivePath,
      releaseInfo: {
        version: versionInfo.version,
        releaseDate: versionInfo.releaseDate,
        signed: false,
        fileSize: versionInfo.fileSize,
        changelog: versionInfo.changelog
      },
      githubUrl: versionInfo.downloadUrl || `https://github.com/zzzz955/blokus-online/releases/download/v${versionInfo.version}/BlokusClient-v${versionInfo.version}.zip`
    };

    // 5. 캐시 업데이트
    releaseCache = {
      data: result,
      timestamp: Date.now()
    };

    console.log(`릴리즈 정보 생성 완료: v${result.version} (로컬파일: ${!!archivePath})`);
    return result;

  } catch (error) {
    console.error('릴리즈 정보 가져오기 실패:', error);
    
    // fallback - 환경변수 사용
    const fallbackVersion = getClientVersion().replace('v', '');
    return {
      version: fallbackVersion,
      filename: `BlokusClient-v${fallbackVersion}.zip`,
      githubUrl: `https://github.com/zzzz955/blokus-online/releases/download/v${fallbackVersion}/BlokusClient-v${fallbackVersion}.zip`
    };
  }
}

export async function GET(request: NextRequest) {
  try {
    const release = await getLatestRelease();
    const userAgent = request.headers.get('user-agent') || 'Unknown';
    const ip = request.headers.get('x-forwarded-for') || 
               request.headers.get('x-real-ip') || 
               'Unknown';
    
    console.log(`[DOWNLOAD] Client download - Version: v${release.version}, IP: ${ip}, User-Agent: ${userAgent}`);
    
    // 비동기적으로 통계 업데이트 (응답 속도에 영향 없음)
    fetch(`${process.env.NEXTAUTH_URL || 'http://localhost:3000'}/api/download/stats`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'x-internal-api-key': 'internal-stats-update'
      },
      body: JSON.stringify({
        ip,
        userAgent,
        version: release.version,
        source: release.archivePath ? 'local-releases' : 'github-fallback'
      })
    }).catch(error => {
      console.error('통계 업데이트 실패:', error);
    });

    // 로컬 파일이 있으면 로컬에서 서빙, 없으면 GitHub으로 리다이렉트
    if (release.archivePath && fs.existsSync(release.archivePath)) {
      // 로컬 파일 스트리밍
      const fileBuffer = fs.readFileSync(release.archivePath);
      
      return new NextResponse(fileBuffer, {
        status: 200,
        headers: {
          'Content-Type': 'application/zip',
          'Content-Disposition': `attachment; filename="${release.filename}"`,
          'Content-Length': fileBuffer.length.toString(),
          'Cache-Control': 'public, max-age=3600' // 1시간 캐시
        }
      });
    } else {
      // GitHub으로 리다이렉트
      return NextResponse.redirect(release.githubUrl, 302);
    }
  } catch (error) {
    console.error('클라이언트 다운로드 오류:', error);
    return NextResponse.json(
      { error: '서버 오류가 발생했습니다.' },
      { status: 500 }
    );
  }
}

// 클라이언트 정보 조회 API
export async function POST(request: NextRequest) {
  try {
    const release = await getLatestRelease();
    
    return NextResponse.json({
      available: true,
      filename: release.filename,
      version: `v${release.version}`,
      downloadUrl: '/api/download/client',
      source: release.archivePath ? 'local-releases' : 'github-fallback',
      signed: release.releaseInfo?.signed || false,
      fileSize: release.releaseInfo?.fileSize || null,
      releaseDate: release.releaseInfo?.releaseDate || null,
      changelog: release.releaseInfo?.changelog || ['최신 버전 릴리즈']
    });
  } catch (error) {
    console.error('클라이언트 정보 조회 오류:', error);
    return NextResponse.json(
      { error: '서버 오류가 발생했습니다.' },
      { status: 500 }
    );
  }
}

// 캐시 무효화 API (내부 사용)
export async function DELETE(request: NextRequest) {
  try {
    const apiKey = request.headers.get('x-internal-api-key');
    if (apiKey !== 'internal-cache-invalidate') {
      return NextResponse.json(
        { error: '권한이 없습니다.' },
        { status: 401 }
      );
    }

    // 캐시 무효화
    releaseCache = { data: null, timestamp: 0 };
    
    console.log('릴리즈 캐시가 무효화되었습니다.');
    
    return NextResponse.json({
      success: true,
      message: '캐시가 무효화되었습니다.',
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