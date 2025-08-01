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

// 버전 정보를 releases 디렉토리에서 읽거나 기본값 반환
async function getVersionInfo(): Promise<ClientVersion> {
  try {
    const releasesDir = path.join(process.cwd(), '..', 'releases');
    
    // 전체 릴리즈 목록 읽기
    const releasesIndexPath = path.join(releasesDir, 'releases.json');
    if (await fs.access(releasesIndexPath).then(() => true).catch(() => false)) {
      const releasesData = await fs.readFile(releasesIndexPath, 'utf-8');
      const releases = JSON.parse(releasesData);
      
      if (releases && releases.length > 0) {
        const latestRelease = releases[0]; // 첫 번째가 최신 버전
        return {
          version: latestRelease.version,
          releaseDate: latestRelease.releaseDate,
          downloadUrl: "/api/download/client",
          fileSize: latestRelease.fileSize,
          changelog: latestRelease.changelog || ["최신 버전 릴리즈"]
        };
      }
    }
    
    // 최신 버전 디렉토리에서 정보 읽기
    const latestDir = path.join(releasesDir, 'latest');
    const releaseInfoPath = path.join(latestDir, 'release-info.json');
    
    if (await fs.access(releaseInfoPath).then(() => true).catch(() => false)) {
      const releaseData = await fs.readFile(releaseInfoPath, 'utf-8');
      const releaseInfo = JSON.parse(releaseData);
      
      return {
        version: releaseInfo.version,
        releaseDate: releaseInfo.releaseDate,
        downloadUrl: "/api/download/client",
        fileSize: releaseInfo.fileSize,
        changelog: releaseInfo.changelog || ["최신 버전 릴리즈"]
      };
    }
    
    // fallback to default
    return {
      version: "1.0.0",
      releaseDate: new Date().toISOString(),
      downloadUrl: "/api/download/client",
      fileSize: 15670931,
      changelog: [
        "초기 릴리즈",
        "멀티플레이어 블로쿠스 게임 지원",
        "실시간 채팅 기능",
        "사용자 통계 및 순위 시스템"
      ]
    };
  } catch (error) {
    console.error('릴리즈 정보 읽기 실패:', error);
    
    // fallback to default
    return {
      version: "1.0.0",
      releaseDate: new Date().toISOString(),
      downloadUrl: "/api/download/client",
      fileSize: 15670931,
      changelog: [
        "초기 릴리즈",
        "멀티플레이어 블로쿠스 게임 지원",
        "실시간 채팅 기능",
        "사용자 통계 및 순위 시스템"
      ]
    };
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

// 관리자용 버전 정보 업데이트 API - 이제는 읽기 전용 (releases 디렉토리 기반)
export async function POST(request: NextRequest) {
  try {
    return NextResponse.json({
      success: false,
      message: '버전 정보는 이제 releases 디렉토리를 통해 자동으로 관리됩니다.',
      instructions: [
        '1. generate-certificate.ps1을 실행하여 코드 서명 인증서 생성',
        '2. build-prod-signed.bat을 실행하여 서명된 빌드 생성',
        '3. package-release-signed.bat을 실행하여 releases/ 디렉토리에 버전 관리',
        '4. 웹 API는 자동으로 최신 릴리즈를 감지합니다'
      ]
    }, { status: 200 });
  } catch (error) {
    console.error('API 호출 오류:', error);
    return NextResponse.json(
      { 
        success: false,
        error: 'API 호출 중 오류가 발생했습니다.' 
      },
      { status: 500 }
    );
  }
}