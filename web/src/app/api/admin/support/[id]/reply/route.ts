import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { getAdminFromRequest, requireAdmin } from '@/lib/server/admin-auth';
import { ApiResponse } from '@/types';

// API 라우트가 빌드 타임에 실행되지 않도록 설정
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

// POST /api/admin/support/[id]/reply - 관리자 답변 저장 및 자동 CLOSED 처리
export async function POST(
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

    const body = await request.json();
    const { adminReply } = body;

    // 답변 내용 검증
    if (!adminReply || !adminReply.trim()) {
      const response: ApiResponse = {
        success: false,
        error: '답변 내용을 입력해주세요.',
      };
      return NextResponse.json(response, { status: 400 });
    }

    if (adminReply.length > 5000) {
      const response: ApiResponse = {
        success: false,
        error: '답변은 5000자 이하로 입력해주세요.',
      };
      return NextResponse.json(response, { status: 400 });
    }

    // 문의 존재 여부 확인
    const existingTicket = await prisma.supportTicket.findUnique({
      where: { id: ticketId },
    });

    if (!existingTicket) {
      const response: ApiResponse = {
        success: false,
        error: '존재하지 않는 문의입니다.',
      };
      return NextResponse.json(response, { status: 404 });
    }

    // 관리자 답변 저장 및 자동 CLOSED 처리
    const updatedTicket = await prisma.supportTicket.update({
      where: {
        id: ticketId,
      },
      data: {
        admin_reply: adminReply.trim(),
        status: 'CLOSED', // 자동 CLOSED 처리
        replied_at: new Date(),
      },
    });

    const response: ApiResponse = {
      success: true,
      message: '답변이 저장되었고 문의가 종료 처리되었습니다.',
      data: {
        id: updatedTicket.id,
        status: updatedTicket.status,
        repliedAt: updatedTicket.replied_at?.toISOString(),
      },
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Admin reply support ticket error:', error);
    const response: ApiResponse = {
      success: false,
      error: '답변 저장 중 오류가 발생했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}