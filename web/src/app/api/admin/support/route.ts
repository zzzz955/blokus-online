import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { getAdminFromRequest, requireAdmin } from '@/lib/server/admin-auth';
import { ApiResponse, PaginatedResponse } from '@/types';

// API 라우트가 빌드 타임에 실행되지 않도록 설정
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

// GET /api/admin/support - 관리자 문의 목록 조회
export async function GET(request: NextRequest) {
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

    const { searchParams } = new URL(request.url);
    const page = parseInt(searchParams.get('page') || '1');
    const limit = parseInt(searchParams.get('limit') || '10');
    const status = searchParams.get('status') || 'ALL';
    const search = searchParams.get('search') || '';
    const searchType = searchParams.get('searchType') || 'subject';

    const offset = (page - 1) * limit;

    // 필터 조건 구성
    const where: any = {};

    // 상태 필터
    if (status !== 'ALL') {
      where.status = status;
    }

    // 검색 조건
    if (search) {
      switch (searchType) {
        case 'subject':
          where.subject = {
            contains: search,
            mode: 'insensitive',
          };
          break;
        case 'email':
          where.email = {
            contains: search,
            mode: 'insensitive',
          };
          break;
        case 'content':
          where.message = {
            contains: search,
            mode: 'insensitive',
          };
          break;
      }
    }

    // 총 개수 조회
    const total = await prisma.supportTicket.count({ where });

    // 문의 목록 조회 (사용자 정보 포함)
    const tickets = await prisma.supportTicket.findMany({
      where,
      include: {
        user: {
          select: {
            username: true,
            display_name: true,
            email: true,
          },
        },
      },
      orderBy: {
        id: 'desc', // 최신순
      },
      skip: offset,
      take: limit,
    });

    // 응답 데이터 변환
    const responseData = tickets.map((ticket) => ({
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
    }));

    const pagination = {
      page,
      limit,
      total,
      totalPages: Math.ceil(total / limit),
    };

    const response: PaginatedResponse<typeof responseData[0]> = {
      success: true,
      data: responseData,
      pagination,
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Admin get support tickets error:', error);
    const response: ApiResponse = {
      success: false,
      error: '문의 목록을 불러오는 중 오류가 발생했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}