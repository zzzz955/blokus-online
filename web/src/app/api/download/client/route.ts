import { NextRequest, NextResponse } from 'next/server';
import fs from 'fs';
import path from 'path';

// 로컬 releases 디렉토리에서 최신 버전 정보 읽기
function getLatestRelease() {
  try {
    const releasesDir = path.join(process.cwd(), '..', 'releases');
    const latestDir = path.join(releasesDir, 'latest');
    
    if (fs.existsSync(latestDir)) {
      const releaseInfoPath = path.join(latestDir, 'release-info.json');
      if (fs.existsSync(releaseInfoPath)) {
        const releaseInfo = JSON.parse(fs.readFileSync(releaseInfoPath, 'utf8'));
        const archivePath = path.join(latestDir, `BlokusClient-v${releaseInfo.version}.zip`);
        
        if (fs.existsSync(archivePath)) {
          return {
            version: releaseInfo.version,
            filename: `BlokusClient-v${releaseInfo.version}.zip`,
            archivePath,
            releaseInfo
          };
        }
      }
    }
    
    // fallback to GitHub releases if local files not found
    return {
      version: '1.0.0',
      filename: 'BlokusClient-v1.0.0.zip',
      githubUrl: 'https://github.com/zzzz955/blokus-online/releases/download/v1.0.0/BlokusClient-v1.0.0.zip'
    };
  } catch (error) {
    console.error('최신 릴리즈 정보 읽기 실패:', error);
    return {
      version: '1.0.0',
      filename: 'BlokusClient-v1.0.0.zip',
      githubUrl: 'https://github.com/zzzz955/blokus-online/releases/download/v1.0.0/BlokusClient-v1.0.0.zip'
    };
  }
}

export async function GET(request: NextRequest) {
  try {
    const release = getLatestRelease();
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
    const release = getLatestRelease();
    
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