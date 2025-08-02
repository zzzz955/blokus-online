import { NextRequest, NextResponse } from 'next/server';
import { getVersionInfo } from '@/lib/github-release';

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