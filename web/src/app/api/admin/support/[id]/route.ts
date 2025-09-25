import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { getAdminFromRequest, requireAdmin } from '@/lib/server/admin-auth';
import { ApiResponse } from '@/types';

// API 라우트가 빌드 타임에 실행되지 않도록 설정
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

// GET /api/admin/support/[id] - 관리자용 개별 문의 상세 조회
export async function GET(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    // 관리자 권한 체크
    const admin = getAdminFromRequest(request);
    if (!requireAdmin()(admin)) {
      const response: ApiResponse = {
        success: false,
        error: '관리자 권한이 필요합니다.',
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

    // 문의 조회 (사용자 정보 포함)
    const ticket = await prisma.supportTicket.findUnique({
      where: {
        id: ticketId,
      },
      include: {
        user: {
          select: {
            username: true,
            display_name: true,
            email: true,
          },
        },
      },
    });

    if (!ticket) {
      const response: ApiResponse = {
        success: false,
        error: '존재하지 않는 문의입니다.',
      };
      return NextResponse.json(response, { status: 404 });
    }

    // 응답 데이터 변환
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
      userName: ticket.user?.display_name || ticket.user?.username || null,
    };

    const response: ApiResponse = {
      success: true,
      data: responseData,
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Admin get support ticket error:', error);
    const response: ApiResponse = {
      success: false,
      error: '문의를 불러오는 중 오류가 발생했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}