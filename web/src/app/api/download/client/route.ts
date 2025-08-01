import { NextRequest, NextResponse } from 'next/server';
import { promises as fs } from 'fs';
import path from 'path';

export async function GET(request: NextRequest) {
  try {
    // 클라이언트 파일 경로
    const clientFilePath = path.join(process.cwd(), 'public', 'downloads', 'BlokusClient-latest.zip');
    
    // 파일 존재 확인
    try {
      await fs.access(clientFilePath);
    } catch (error) {
      return NextResponse.json(
        { error: '클라이언트 파일을 찾을 수 없습니다.' },
        { status: 404 }
      );
    }

    // 파일 정보 가져오기
    const stats = await fs.stat(clientFilePath);
    const fileBuffer = await fs.readFile(clientFilePath);

    // 다운로드 로깅 및 통계 업데이트
    const userAgent = request.headers.get('user-agent') || 'Unknown';
    const ip = request.headers.get('x-forwarded-for') || 
               request.headers.get('x-real-ip') || 
               'Unknown';
    
    console.log(`[DOWNLOAD] Client downloaded - IP: ${ip}, User-Agent: ${userAgent}, Size: ${stats.size} bytes`);
    
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
        fileSize: stats.size
      })
    }).catch(error => {
      console.error('통계 업데이트 실패:', error);
    });

    // 파일 다운로드 응답 생성
    const response = new NextResponse(fileBuffer);
    
    response.headers.set('Content-Type', 'application/zip');
    response.headers.set('Content-Disposition', 'attachment; filename="BlokusClient-latest.zip"');
    response.headers.set('Content-Length', stats.size.toString());
    response.headers.set('Cache-Control', 'no-cache, no-store, must-revalidate');
    response.headers.set('Pragma', 'no-cache');
    response.headers.set('Expires', '0');

    return response;
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
    const clientFilePath = path.join(process.cwd(), 'public', 'downloads', 'BlokusClient-latest.zip');
    
    try {
      const stats = await fs.stat(clientFilePath);
      
      return NextResponse.json({
        available: true,
        filename: 'BlokusClient-latest.zip',
        size: stats.size,
        lastModified: stats.mtime.toISOString(),
        downloadUrl: '/api/download/client'
      });
    } catch (error) {
      return NextResponse.json({
        available: false,
        error: '클라이언트 파일을 찾을 수 없습니다.'
      });
    }
  } catch (error) {
    console.error('클라이언트 정보 조회 오류:', error);
    return NextResponse.json(
      { error: '서버 오류가 발생했습니다.' },
      { status: 500 }
    );
  }
}