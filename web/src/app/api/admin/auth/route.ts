import { NextRequest, NextResponse } from 'next/server';
import { authenticateAdmin, createAdminUser, type AdminLoginRequest } from '@/lib/admin-auth';

/**
 * 관리자 로그인 API
 */
export async function POST(request: NextRequest) {
  try {
    const body: AdminLoginRequest = await request.json();
    const { username, password } = body;

    // 입력 검증
    if (!username || !password) {
      return NextResponse.json({
        success: false,
        error: '사용자명과 비밀번호를 입력해주세요.'
      }, { status: 400 });
    }

    // 관리자 인증
    const result = await authenticateAdmin({ username, password });
    
    if (result.success) {
      // HTTP-only 쿠키로 토큰 설정
      const res = NextResponse.json({
        success: true,
        data: {
          token: result.token,
          user: result.admin
        },
        message: '로그인에 성공했습니다.'
      });
      
      res.cookies.set('admin-token', result.token!, {
        httpOnly: true,
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'strict',
        maxAge: 7 * 24 * 60 * 60, // 7 days
      });

      return res;
    } else {
      return NextResponse.json({
        success: false,
        error: result.error
      }, { status: 401 });
    }

  } catch (error) {
    console.error('관리자 로그인 API 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}

/**
 * 관리자 로그아웃 API
 */
export async function DELETE() {
  try {
    const res = NextResponse.json({
      success: true,
      message: '로그아웃되었습니다.'
    });
    
    res.cookies.delete('admin-token');
    return res;
  } catch (error) {
    console.error('관리자 로그아웃 API 오류:', error);
    return NextResponse.json({
      success: false,
      error: '로그아웃에 실패했습니다.'
    }, { status: 500 });
  }
}

/**
 * 관리자 계정 생성 API (개발/초기 설정용)
 */
export async function PUT(request: NextRequest) {
  try {
    // 환경 변수로 초기 설정 모드 체크
    if (process.env.NODE_ENV === 'production' && !process.env.ALLOW_ADMIN_CREATION) {
      return NextResponse.json({
        success: false,
        error: '운영 환경에서는 관리자 계정 생성이 제한됩니다.'
      }, { status: 403 });
    }

    const { username, password, role, setupKey } = await request.json();

    // 설정 키 검증 (보안 강화)
    const expectedSetupKey = process.env.ADMIN_SETUP_KEY || 'setup-admin-2024';
    if (setupKey !== expectedSetupKey) {
      return NextResponse.json({
        success: false,
        error: '유효하지 않은 설정 키입니다.'
      }, { status: 403 });
    }

    // 입력 검증
    if (!username || !password) {
      return NextResponse.json({
        success: false,
        error: '사용자명과 비밀번호를 입력해주세요.'
      }, { status: 400 });
    }

    if (password.length < 8) {
      return NextResponse.json({
        success: false,
        error: '비밀번호는 최소 8자 이상이어야 합니다.'
      }, { status: 400 });
    }

    // 관리자 계정 생성
    const result = await createAdminUser(username, password, role || 'ADMIN');
    
    if (result.success) {
      return NextResponse.json({
        success: true,
        message: '관리자 계정이 생성되었습니다.'
      });
    } else {
      return NextResponse.json({
        success: false,
        error: result.error
      }, { status: 400 });
    }

  } catch (error) {
    console.error('관리자 계정 생성 API 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}