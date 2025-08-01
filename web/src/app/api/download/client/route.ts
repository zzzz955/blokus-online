import { NextRequest, NextResponse } from 'next/server';

// GitHub Releases 최신 다운로드 URL
const GITHUB_RELEASE_URL = 'https://github.com/zzzz955/blokus-online/releases/download/v1.0.0/BlokusClient-v1.0.0.zip';

export async function GET(request: NextRequest) {
  try {
    // 다운로드 로깅 및 통계 업데이트
    const userAgent = request.headers.get('user-agent') || 'Unknown';
    const ip = request.headers.get('x-forwarded-for') || 
               request.headers.get('x-real-ip') || 
               'Unknown';
    
    console.log(`[DOWNLOAD] Client download redirected to GitHub - IP: ${ip}, User-Agent: ${userAgent}`);
    
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
        fileSize: 0, // GitHub에서 직접 다운로드하므로 크기 정보 없음
        source: 'github-redirect'
      })
    }).catch(error => {
      console.error('통계 업데이트 실패:', error);
    });

    // GitHub Releases로 리다이렉트
    return NextResponse.redirect(GITHUB_RELEASE_URL, 302);
  } catch (error) {
    console.error('클라이언트 다운로드 리다이렉트 오류:', error);
    return NextResponse.json(
      { error: '서버 오류가 발생했습니다.' },
      { status: 500 }
    );
  }
}

// 클라이언트 정보 조회 API
export async function POST(request: NextRequest) {
  try {
    // GitHub Releases 정보 반환
    return NextResponse.json({
      available: true,
      filename: 'BlokusClient-v1.0.0.zip',
      version: 'v1.0.0',
      downloadUrl: '/api/download/client', // 리다이렉트 API 경로
      directUrl: GITHUB_RELEASE_URL, // 직접 GitHub 링크
      source: 'github-releases'
    });
  } catch (error) {
    console.error('클라이언트 정보 조회 오류:', error);
    return NextResponse.json(
      { error: '서버 오류가 발생했습니다.' },
      { status: 500 }
    );
  }
}