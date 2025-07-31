import { NextRequest, NextResponse } from 'next/server';
import { authenticateAdmin, generateToken } from '@/lib/auth';
import { ApiResponse, AdminLoginForm } from '@/types';

export async function POST(request: NextRequest) {
  try {
    const body: AdminLoginForm = await request.json();
    const { username, password } = body;

    if (!username || !password) {
      const response: ApiResponse = {
        success: false,
        error: '사용자명과 비밀번호를 입력해주세요.',
      };
      return NextResponse.json(response, { status: 400 });
    }

    const admin = await authenticateAdmin(username, password);
    
    if (!admin) {
      const response: ApiResponse = {
        success: false,
        error: '잘못된 사용자명 또는 비밀번호입니다.',
      };
      return NextResponse.json(response, { status: 401 });
    }

    const token = generateToken({
      userId: admin.id,
      username: admin.username,
      role: admin.role,
    });

    const response: ApiResponse = {
      success: true,
      data: {
        token,
        user: {
          id: admin.id,
          username: admin.username,
          role: admin.role,
        },
      },
      message: '로그인에 성공했습니다.',
    };

    // HTTP-only 쿠키로 토큰 설정
    const res = NextResponse.json(response);
    res.cookies.set('admin-token', token, {
      httpOnly: true,
      secure: process.env.NODE_ENV === 'production',
      sameSite: 'strict',
      maxAge: 7 * 24 * 60 * 60, // 7 days
    });

    return res;
  } catch (error) {
    console.error('Admin login error:', error);
    const response: ApiResponse = {
      success: false,
      error: '로그인에 실패했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}

export async function DELETE() {
  try {
    const response: ApiResponse = {
      success: true,
      message: '로그아웃되었습니다.',
    };

    const res = NextResponse.json(response);
    res.cookies.delete('admin-token');

    return res;
  } catch (error) {
    console.error('Admin logout error:', error);
    const response: ApiResponse = {
      success: false,
      error: '로그아웃에 실패했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}