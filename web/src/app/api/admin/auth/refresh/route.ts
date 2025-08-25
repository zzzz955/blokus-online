import { NextRequest, NextResponse } from 'next/server';
import { verifyRefreshToken, generateNewAccessToken } from '@/lib/server/admin-auth';

/**
 * Token Refresh API
 */
export async function POST(request: NextRequest) {
  try {
    // 1. 쿠키에서 refresh token 추출
    const refreshToken = request.cookies.get('admin-refresh-token')?.value;
    
    if (!refreshToken) {
      return NextResponse.json({
        success: false,
        error: 'Refresh token이 없습니다.'
      }, { status: 401 });
    }

    // 2. Refresh token 검증
    const refreshPayload = verifyRefreshToken(refreshToken);
    if (!refreshPayload) {
      return NextResponse.json({
        success: false,
        error: '유효하지 않은 refresh token입니다.'
      }, { status: 401 });
    }

    // 3. 새로운 access token 생성
    const result = await generateNewAccessToken(refreshPayload);
    
    if (result.success) {
      const res = NextResponse.json({
        success: true,
        data: {
          token: result.token,
          user: result.admin
        },
        message: '토큰이 갱신되었습니다.'
      });

      // 4. 새로운 access token을 쿠키에 저장
      res.cookies.set('admin-token', result.token!, {
        httpOnly: true,
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'strict',
        maxAge: 15 * 60, // 15 minutes
      });

      return res;
    } else {
      return NextResponse.json({
        success: false,
        error: result.error
      }, { status: 401 });
    }

  } catch (error) {
    console.error('토큰 갱신 API 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}