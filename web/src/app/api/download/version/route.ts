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
    const response = await fetch('https://api.github.com/repos/zzzz955/blokus-online/releases/latest', {
      headers: {
        'Accept': 'application/vnd.github+json',
        'User-Agent': 'blokus-web-service'
      }
    });

    if (!response.ok) {
      console.warn('GitHub Releases API 오류:', response.status, response.statusText);
      return getDefaultVersion();
    }

    const data = await response.json();

    const version = data.tag_name || '1.0.0';
    const releaseDate = data.published_at || new Date().toISOString();
    const downloadAsset = data.assets?.[0]; // 첫 번째 첨부 파일 기준
    const downloadUrl = downloadAsset?.browser_download_url || '';
    const fileSize = downloadAsset?.size || 0;
    const changelog = (data.body || '')
      .split('\n')
      .map(line => line.trim())
      .filter(line => line.startsWith('- '))
      .map(line => line.slice(2)); // "- " 제거

    return {
      version,
      releaseDate,
      downloadUrl,
      fileSize,
      changelog
    };
  } catch (error) {
    console.error('GitHub 릴리즈 정보 조회 실패:', error);
    return getDefaultVersion();
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