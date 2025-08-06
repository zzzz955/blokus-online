// ========================================
// 회원 탈퇴 API 엔드포인트
// ========================================
// DELETE /api/user/deactivate - 계정 비활성화 (is_active = false)
// ========================================

import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from 'next-auth/next';
import { prisma } from '@/lib/prisma';
import { ApiResponse } from '@/types';
import { authOptions } from '@/lib/auth';

interface DeactivateRequest {
  confirmation_text: string; // 사용자가 "계정 삭제"라고 입력해야 함
  reason?: string; // 선택적 탈퇴 사유
}

// DELETE /api/user/deactivate - 계정 비활성화
export async function DELETE(request: NextRequest) {
  try {
    const session = await getServerSession(authOptions);
    
    if (!session?.user?.email) {
      const response: ApiResponse<null> = {
        success: false,
        error: '로그인이 필요합니다.'
      };
      return NextResponse.json(response, { status: 401 });
    }

    const body: DeactivateRequest = await request.json();
    
    // 확인 텍스트 검증
    if (body.confirmation_text !== '계정 삭제') {
      const response: ApiResponse<null> = {
        success: false,
        error: '확인 텍스트가 올바르지 않습니다. "계정 삭제"라고 정확히 입력해주세요.'
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
        email: true,
        oauth_provider: true
      }
    });

    if (!user) {
      const response: ApiResponse<null> = {
        success: false,
        error: '활성화된 사용자 계정을 찾을 수 없습니다.'
      };
      return NextResponse.json(response, { status: 404 });
    }

    // 트랜잭션으로 계정 비활성화 처리
    await prisma.$transaction(async (tx) => {
      // 1. 사용자 계정 비활성화
      await tx.user.update({
        where: { user_id: user.user_id },
        data: {
          is_active: false,
          updated_at: new Date()
        }
      });

      // 2. 사용자가 작성한 게시글 숨김 처리 (선택사항)
      await tx.post.updateMany({
        where: { 
          author_id: user.user_id,
          is_deleted: false 
        },
        data: { 
          is_hidden: true 
        }
      });

      // 3. 사용자 후기 비공개 처리 (선택사항)
      await tx.testimonial.updateMany({
        where: { 
          userId: user.user_id,
          is_published: true 
        },
        data: { 
          is_published: false 
        }
      });

      // 4. 미처리 문의는 그대로 유지 (관리자가 답변할 수 있도록)
      // 문의 내역은 건드리지 않음

      // 5. 탈퇴 로그 기록 (선택사항 - 별도 테이블이 있다면)
      // 현재는 스키마에 없으므로 생략
    });

    const response: ApiResponse<{ 
      deactivated_user: string;
      deactivated_at: Date;
      recovery_info: string;
    }> = {
      success: true,
      data: {
        deactivated_user: user.username,
        deactivated_at: new Date(),
        recovery_info: user.oauth_provider 
          ? `${user.oauth_provider} 계정으로 다시 로그인하면 계정 복구가 가능합니다.`
          : '계정이 비활성화되었습니다.'
      },
      message: '계정이 성공적으로 비활성화되었습니다. 언제든 다시 로그인하여 계정을 복구할 수 있습니다.'
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('계정 비활성화 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '계정 비활성화 중 오류가 발생했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}