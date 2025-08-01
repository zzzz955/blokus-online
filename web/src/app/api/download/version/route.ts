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

// 서버 컨테이너에서 버전 정보를 조회하거나 환경변수/releases 디렉토리에서 읽기
async function getVersionInfo(): Promise<ClientVersion> {
  try {
    // 1. 먼저 서버 컨테이너에서 버전 정보 조회 시도
    const serverVersion = await getServerVersionInfo();
    if (serverVersion) {
      return serverVersion;
    }

    // 2. 환경변수에서 버전 정보 읽기
    const envVersion = await getVersionFromEnvironment();
    if (envVersion) {
      return envVersion;
    }

    // 3. releases 디렉토리에서 읽기 (기존 로직)
    const releasesVersion = await getVersionFromReleases();
    if (releasesVersion) {
      return releasesVersion;
    }

    // 4. 최종 fallback
    return getDefaultVersion();
  } catch (error) {
    console.error('버전 정보 조회 실패:', error);
    return getDefaultVersion();
  }
}

// 서버 컨테이너에서 버전 정보 조회
async function getServerVersionInfo(): Promise<ClientVersion | null> {
  try {
    const serverHost = process.env.BLOKUS_SERVER_HOST || 'blokus-server';
    const serverPort = process.env.BLOKUS_SERVER_ADMIN_PORT || '9998';
    const serverUrl = `http://${serverHost}:${serverPort}/version`;
    
    console.log(`서버 버전 정보 조회 시도: ${serverUrl}`);
    
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 5000); // 5초 타임아웃
    
    const response = await fetch(serverUrl, {
      method: 'GET',
      signal: controller.signal,
      headers: {
        'Accept': 'application/json',
        'User-Agent': 'blokus-web-service'
      }
    });
    
    clearTimeout(timeoutId);
    
    if (response.ok) {
      const serverData = await response.json();
      console.log('서버에서 버전 정보 조회 성공:', serverData);
      
      return {
        version: serverData.version || "1.0.0",
        releaseDate: serverData.buildDate || new Date().toISOString(),
        downloadUrl: "/api/download/client",
        fileSize: parseInt(process.env.BLOKUS_CLIENT_FILE_SIZE || "15670931"),
        changelog: [
          `서버 버전 ${serverData.version}과 호환`,
          "최신 멀티플레이어 기능 지원",
          ...(serverData.features || [])
        ]
      };
    }
    
    console.warn(`서버 응답 오류: ${response.status} ${response.statusText}`);
    return null;
  } catch (error) {
    if (error.name === 'AbortError') {
      console.warn('서버 버전 조회 타임아웃');
    } else {
      console.warn('서버 버전 조회 실패:', error.message);
    }
    return null;
  }
}

// 환경변수에서 버전 정보 읽기
async function getVersionFromEnvironment(): Promise<ClientVersion | null> {
  try {
    const version = process.env.BLOKUS_CLIENT_VERSION;
    const buildDate = process.env.BLOKUS_BUILD_DATE;
    const fileSize = process.env.BLOKUS_CLIENT_FILE_SIZE;
    
    if (version) {
      console.log('환경변수에서 버전 정보 조회:', version);
      return {
        version,
        releaseDate: buildDate || new Date().toISOString(),
        downloadUrl: "/api/download/client",
        fileSize: parseInt(fileSize || "15670931"),
        changelog: [
          `버전 ${version} 릴리즈`,
          "환경변수 기반 버전 관리",
          "컨테이너 환경 최적화"
        ]
      };
    }
    
    return null;
  } catch (error) {
    console.warn('환경변수 버전 정보 읽기 실패:', error);
    return null;
  }
}

// releases 디렉토리에서 버전 정보 읽기 (기존 로직)
async function getVersionFromReleases(): Promise<ClientVersion | null> {
  try {
    const releasesDir = path.join(process.cwd(), '..', 'releases');
    
    // 전체 릴리즈 목록 읽기
    const releasesIndexPath = path.join(releasesDir, 'releases.json');
    if (await fs.access(releasesIndexPath).then(() => true).catch(() => false)) {
      const releasesData = await fs.readFile(releasesIndexPath, 'utf-8');
      const releases = JSON.parse(releasesData);
      
      if (releases && releases.length > 0) {
        const latestRelease = releases[0];
        console.log('releases.json에서 버전 정보 조회:', latestRelease.version);
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
      
      console.log('release-info.json에서 버전 정보 조회:', releaseInfo.version);
      return {
        version: releaseInfo.version,
        releaseDate: releaseInfo.releaseDate,
        downloadUrl: "/api/download/client",
        fileSize: releaseInfo.fileSize,
        changelog: releaseInfo.changelog || ["최신 버전 릴리즈"]
      };
    }
    
    return null;
  } catch (error) {
    console.warn('releases 디렉토리 버전 정보 읽기 실패:', error);
    return null;
  }
}

// 기본 버전 정보 반환
function getDefaultVersion(): ClientVersion {
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