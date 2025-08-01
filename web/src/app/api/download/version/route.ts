import { NextRequest, NextResponse } from 'next/server';
import { promises as fs } from 'fs';
import path from 'path';

interface ClientVersion {
  version: string;
  releaseDate: string;
  downloadUrl: string;
  fileSize: number;
  changelog?: string[];
}

// 버전 정보를 파일에서 읽거나 기본값 반환
async function getVersionInfo(): Promise<ClientVersion> {
  const versionFilePath = path.join(process.cwd(), 'public', 'downloads', 'version.json');
  const clientFilePath = path.join(process.cwd(), 'public', 'downloads', 'BlokusClient-latest.zip');
  
  console.log('Current working directory:', process.cwd());
  console.log('Looking for version file at:', versionFilePath);
  console.log('Looking for client file at:', clientFilePath);

  try {
    // version.json 파일이 있으면 읽기
    const versionData = await fs.readFile(versionFilePath, 'utf-8');
    return JSON.parse(versionData);
  } catch (error) {
    // version.json이 없으면 파일 정보를 바탕으로 기본값 생성
    try {
      const stats = await fs.stat(clientFilePath);
      const defaultVersion: ClientVersion = {
        version: "1.0.0",
        releaseDate: stats.mtime.toISOString(),
        downloadUrl: "/api/download/client",
        fileSize: stats.size,
        changelog: [
          "초기 릴리즈",
          "멀티플레이어 블로쿠스 게임 지원",
          "실시간 채팅 기능",
          "사용자 통계 및 순위 시스템"
        ]
      };
      
      // 기본 version.json 파일 생성
      await fs.writeFile(versionFilePath, JSON.stringify(defaultVersion, null, 2));
      return defaultVersion;
    } catch (fileError) {
      console.error('File access error:', fileError);
      console.log('Attempting to list directory contents...');
      
      try {
        const publicDir = path.join(process.cwd(), 'public');
        const downloadsDir = path.join(process.cwd(), 'public', 'downloads');
        
        console.log('Public directory exists:', await fs.access(publicDir).then(() => true).catch(() => false));
        console.log('Downloads directory exists:', await fs.access(downloadsDir).then(() => true).catch(() => false));
        
        if (await fs.access(downloadsDir).then(() => true).catch(() => false)) {
          const files = await fs.readdir(downloadsDir);
          console.log('Files in downloads directory:', files);
        }
      } catch (listError) {
        console.error('Directory listing error:', listError);
      }
      
      // 파일이 없어도 기본 정보 반환
      return {
        version: "1.0.0",
        releaseDate: new Date().toISOString(),
        downloadUrl: "/api/download/client",
        fileSize: 15670931, // 대략적인 크기
        changelog: [
          "초기 릴리즈",
          "멀티플레이어 블로쿠스 게임 지원",
          "실시간 채팅 기능",
          "사용자 통계 및 순위 시스템"
        ]
      };
    }
  }
}

export async function GET(request: NextRequest) {
  try {
    const versionInfo = await getVersionInfo();
    
    return NextResponse.json({
      success: true,
      data: versionInfo
    });
  } catch (error) {
    console.error('버전 정보 조회 오류:', error);
    return NextResponse.json(
      { 
        success: false,
        error: '버전 정보를 가져올 수 없습니다.' 
      },
      { status: 500 }
    );
  }
}

// 관리자용 버전 정보 업데이트 API
export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { version, changelog, adminKey } = body;

    // 간단한 관리자 키 검증 (실제로는 더 강력한 인증 필요)
    if (adminKey !== process.env.ADMIN_API_KEY) {
      return NextResponse.json(
        { error: '권한이 없습니다.' },
        { status: 401 }
      );
    }

    const versionFilePath = path.join(process.cwd(), 'public', 'downloads', 'version.json');
    const clientFilePath = path.join(process.cwd(), 'public', 'downloads', 'BlokusClient-latest.zip');
    
    // 현재 파일 정보 가져오기
    const stats = await fs.stat(clientFilePath);
    
    const newVersionInfo: ClientVersion = {
      version: version || "1.0.0",
      releaseDate: new Date().toISOString(),
      downloadUrl: "/api/download/client",
      fileSize: stats.size,
      changelog: changelog || ["버전 업데이트"]
    };

    await fs.writeFile(versionFilePath, JSON.stringify(newVersionInfo, null, 2));

    return NextResponse.json({
      success: true,
      message: '버전 정보가 업데이트되었습니다.',
      data: newVersionInfo
    });
  } catch (error) {
    console.error('버전 정보 업데이트 오류:', error);
    return NextResponse.json(
      { 
        success: false,
        error: '버전 정보를 업데이트할 수 없습니다.' 
      },
      { status: 500 }
    );
  }
}