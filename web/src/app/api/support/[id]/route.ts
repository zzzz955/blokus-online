import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from 'next-auth';
import { authOptions } from '@/lib/auth';
import { prisma } from '@/lib/prisma';
import { ApiResponse } from '@/types';

// API 라우트가 빌드 타임에 실행되지 않도록 설정
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

// GET /api/support/[id] - 개별 문의 상세 조회 (본인 문의만)
export async function GET(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    const session = await getServerSession(authOptions);

    // 로그인 체크
    if (!session?.user?.id) {
      const response: ApiResponse = {
        success: false,
        error: '로그인이 필요합니다.',
      };
      return NextResponse.json(response, { status: 401 });
    }

    const ticketId = parseInt(params.id);

    if (isNaN(ticketId)) {
      const response: ApiResponse = {
        success: false,
        error: '잘못된 문의 ID입니다.',
      };
      return NextResponse.json(response, { status: 400 });
    }

    // 문의 조회 (본인 문의만)
    const ticket = await prisma.supportTicket.findFirst({
      where: {
        id: ticketId,
        userId: parseInt(session.user.id), // 본인 문의만 조회 가능
      },
    });

    if (!ticket) {
      const response: ApiResponse = {
        success: false,
        error: '문의를 찾을 수 없거나 접근 권한이 없습니다.',
      };
      return NextResponse.json(response, { status: 404 });
    }

    // 응답 데이터 변환 (camelCase 변환)
    const responseData = {
      id: ticket.id,
      userId: ticket.userId,
      email: ticket.email,
      subject: ticket.subject,
      message: ticket.message,
      status: ticket.status,
      adminReply: ticket.admin_reply,
      createdAt: ticket.created_at.toISOString(),
      repliedAt: ticket.replied_at?.toISOString(),
    };

    const response: ApiResponse = {
      success: true,
      data: responseData,
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Get support ticket error:', error);
    const response: ApiResponse = {
      success: false,
      error: '문의를 불러오는 중 오류가 발생했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}