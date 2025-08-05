// ========================================
// 계정 복구 API 엔드포인트  
// ========================================
// POST /api/user/reactivate - OAuth를 통한 비활성 계정 복구
// ========================================

import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from 'next-auth/next';
import { prisma } from '@/lib/prisma';
import { ApiResponse } from '@/types';
import { authOptions } from '@/lib/auth';

interface ReactivateRequest {
  email: string;
  oauth_provider: string;
  confirm_reactivation: boolean;
}

// POST /api/user/reactivate - 비활성 계정 복구
export async function POST(request: NextRequest) {
  try {
    const body: ReactivateRequest = await request.json();
    
    // 입력 유효성 검사
    if (!body.email || !body.oauth_provider) {
      const response: ApiResponse<null> = {
        success: false,
        error: '이메일과 OAuth 제공자 정보가 필요합니다.'
      };
      return NextResponse.json(response, { status: 400 });
    }

    if (!body.confirm_reactivation) {
      const response: ApiResponse<null> = {
        success: false,
        error: '계정 복구 확인이 필요합니다.'
      };
      return NextResponse.json(response, { status: 400 });
    }

    // 비활성 계정 찾기
    const deactivatedUser = await prisma.user.findFirst({
      where: {
        email: body.email,
        oauth_provider: body.oauth_provider,
        is_active: false
      },
      select: {
        user_id: true,
        username: true,
        email: true,
        oauth_provider: true,
        display_name: true,
        created_at: true
      }
    });

    if (!deactivatedUser) {
      const response: ApiResponse<null> = {
        success: false,
        error: '복구할 수 있는 비활성 계정을 찾을 수 없습니다.'
      };
      return NextResponse.json(response, { status: 404 });
    }

    // 트랜잭션으로 계정 복구 처리
    const reactivatedUser = await prisma.$transaction(async (tx) => {
      // 1. 사용자 계정 재활성화
      const user = await tx.user.update({
        where: { user_id: deactivatedUser.user_id },
        data: {
          is_active: true,
          last_login_at: new Date(),
          updated_at: new Date()
        }
      });

      // 2. 사용자가 작성한 게시글 복구 (이전에 숨겨진 것들)
      await tx.post.updateMany({
        where: { 
          author_id: deactivatedUser.user_id,
          is_hidden: true,
          is_deleted: false
        },
        data: { 
          is_hidden: false 
        }
      });

      // 3. 사용자 후기 복구 (이전에 비공개 처리된 것들)
      await tx.testimonial.updateMany({
        where: { 
          userId: deactivatedUser.user_id,
          is_published: false
        },
        data: { 
          is_published: true 
        }
      });

      return user;
    });

    const response: ApiResponse<{
      user_id: number;
      username: string;
      email: string;
      display_name?: string;
      reactivated_at: Date;
      member_since: Date;
    }> = {
      success: true,
      data: {
        user_id: reactivatedUser.user_id,
        username: reactivatedUser.username,
        email: reactivatedUser.email!,
        display_name: reactivatedUser.display_name || undefined,
        reactivated_at: new Date(),
        member_since: deactivatedUser.created_at
      },
      message: '계정이 성공적으로 복구되었습니다. 다시 돌아오신 것을 환영합니다!'
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('계정 복구 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '계정 복구 중 오류가 발생했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}

// GET /api/user/reactivate - 복구 가능한 계정 확인
export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const email = searchParams.get('email');
    const oauth_provider = searchParams.get('oauth_provider');

    if (!email || !oauth_provider) {
      const response: ApiResponse<null> = {
        success: false,
        error: '이메일과 OAuth 제공자 정보가 필요합니다.'
      };
      return NextResponse.json(response, { status: 400 });
    }

    // 비활성 계정 확인
    const deactivatedUser = await prisma.user.findFirst({
      where: {
        email,
        oauth_provider,
        is_active: false
      },
      select: {
        username: true,
        email: true,
        oauth_provider: true,
        display_name: true,
        created_at: true,
        updated_at: true
      }
    });

    if (!deactivatedUser) {
      const response: ApiResponse<null> = {
        success: false,
        error: '복구할 수 있는 계정이 없습니다.'
      };
      return NextResponse.json(response, { status: 404 });
    }

    const response: ApiResponse<{
      username: string;
      display_name?: string;
      member_since: Date;
      deactivated_at: Date;
      can_reactivate: boolean;
    }> = {
      success: true,
      data: {
        username: deactivatedUser.username,
        display_name: deactivatedUser.display_name || undefined,
        member_since: deactivatedUser.created_at,
        deactivated_at: deactivatedUser.updated_at,
        can_reactivate: true
      }
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('계정 복구 확인 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '계정 복구 확인 중 오류가 발생했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}