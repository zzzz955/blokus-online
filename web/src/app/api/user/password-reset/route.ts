// ========================================
// 사용자 비밀번호 재설정 API 엔드포인트
// ========================================
// PUT /api/user/password-reset - 현재 비밀번호 검증 없이 새 비밀번호로 재설정
// ========================================

import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from 'next-auth/next';
import { prisma } from '@/lib/prisma';
import { hashPassword } from '@/lib/auth';
import { ApiResponse } from '@/types';
import { authOptions } from '@/lib/auth';

interface PasswordResetRequest {
  new_password: string;
  confirm_password: string;
}

// PUT /api/user/password-reset - 비밀번호 재설정
export async function PUT(request: NextRequest) {
  try {
    const session = await getServerSession(authOptions);
    
    if (!session?.user?.email) {
      const response: ApiResponse<null> = {
        success: false,
        error: '로그인이 필요합니다.'
      };
      return NextResponse.json(response, { status: 401 });
    }

    const body: PasswordResetRequest = await request.json();
    
    // 입력 유효성 검사
    if (!body.new_password || !body.confirm_password) {
      const response: ApiResponse<null> = {
        success: false,
        error: '새 비밀번호와 비밀번호 확인을 모두 입력해주세요.'
      };
      return NextResponse.json(response, { status: 400 });
    }

    if (body.new_password !== body.confirm_password) {
      const response: ApiResponse<null> = {
        success: false,
        error: '새 비밀번호와 비밀번호 확인이 일치하지 않습니다.'
      };
      return NextResponse.json(response, { status: 400 });
    }

    // 비밀번호 강도 검사
    if (body.new_password.length < 8) {
      const response: ApiResponse<null> = {
        success: false,
        error: '비밀번호는 최소 8자 이상이어야 합니다.'
      };
      return NextResponse.json(response, { status: 400 });
    }

    // 영문, 숫자, 특수문자 조합 검사
    const passwordRegex = /^(?=.*[a-zA-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]/;
    if (!passwordRegex.test(body.new_password)) {
      const response: ApiResponse<null> = {
        success: false,
        error: '비밀번호는 영문, 숫자, 특수문자를 모두 포함해야 합니다.'
      };
      return NextResponse.json(response, { status: 400 });
    }

    // 사용자 확인
    const user = await prisma.user.findUnique({
      where: { 
        email: session.user.email,
        is_active: true 
      },
      select: { 
        user_id: true, 
        username: true,
        email: true 
      }
    });

    if (!user) {
      const response: ApiResponse<null> = {
        success: false,
        error: '사용자 정보를 찾을 수 없습니다.'
      };
      return NextResponse.json(response, { status: 404 });
    }

    // 새 비밀번호 해싱
    const hashedNewPassword = await hashPassword(body.new_password);

    // 비밀번호 업데이트
    await prisma.user.update({
      where: { user_id: user.user_id },
      data: {
        password_hash: hashedNewPassword,
        updated_at: new Date()
      }
    });

    // 보안을 위해 세션 무효화 권장 (사용자가 다시 로그인하도록)
    const response: ApiResponse<{ 
      username: string; 
      updated_at: Date;
      requires_relogin: boolean;
    }> = {
      success: true,
      data: {
        username: user.username,
        updated_at: new Date(),
        requires_relogin: true
      },
      message: '비밀번호가 성공적으로 변경되었습니다. 보안을 위해 다시 로그인해주세요.'
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('비밀번호 재설정 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '비밀번호 변경 중 오류가 발생했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}