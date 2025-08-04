import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from 'next-auth';
import { authOptions } from '@/lib/auth';
import { prisma } from '@/lib/prisma';
import { ApiResponse, PaginatedResponse, ContactForm } from '@/types';

// API 라우트가 빌드 타임에 실행되지 않도록 설정
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(request: NextRequest) {
  try {
    const session = await getServerSession(authOptions);
    const body: ContactForm = await request.json();
    const { email, subject, message } = body;

    // 기본 유효성 검사
    if (!email || !subject || !message) {
      const response: ApiResponse = {
        success: false,
        error: '모든 필드를 입력해주세요.',
      };
      return NextResponse.json(response, { status: 400 });
    }

    // 이메일 형식 검사
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
      const response: ApiResponse = {
        success: false,
        error: '올바른 이메일 형식이 아닙니다.',
      };
      return NextResponse.json(response, { status: 400 });
    }

    // 내용 길이 검사
    if (subject.length > 200) {
      const response: ApiResponse = {
        success: false,
        error: '제목은 200자 이하로 입력해주세요.',
      };
      return NextResponse.json(response, { status: 400 });
    }

    if (message.length > 2000) {
      const response: ApiResponse = {
        success: false,
        error: '메시지는 2000자 이하로 입력해주세요.',
      };
      return NextResponse.json(response, { status: 400 });
    }

    // DB에 저장 (로그인 사용자인 경우 userId 연결)
    const ticket = await prisma.supportTicket.create({
      data: {
        userId: session?.user?.id ? parseInt(session.user.id) : null,
        email,
        subject,
        message,
        status: 'PENDING',
      },
    });

    const response: ApiResponse = {
      success: true,
      message: '문의가 성공적으로 접수되었습니다. 빠른 시일 내에 답변드리겠습니다.',
      data: { id: ticket.id },
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Create support ticket error:', error);
    const response: ApiResponse = {
      success: false,
      error: '문의 접수에 실패했습니다. 잠시 후 다시 시도해주세요.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}

// GET /api/support - 내 문의 목록 조회 (로그인 필요)
export async function GET(request: NextRequest) {
  try {
    const session = await getServerSession(authOptions);
    if (!session?.user?.id) {
      const response: ApiResponse = {
        success: false,
        error: '로그인이 필요합니다.',
      };
      return NextResponse.json(response, { status: 401 });
    }

    const { searchParams } = new URL(request.url);
    const page = parseInt(searchParams.get('page') || '1');
    const limit = parseInt(searchParams.get('limit') || '10');
    const offset = (page - 1) * limit;

    // 총 개수 조회
    const total = await prisma.supportTicket.count({
      where: { userId: parseInt(session.user.id) }
    });
    const totalPages = Math.ceil(total / limit);

    // 문의 목록 조회
    const tickets = await prisma.supportTicket.findMany({
      where: { userId: parseInt(session.user.id) },
      orderBy: { created_at: 'desc' },
      take: limit,
      skip: offset,
      select: {
        id: true,
        subject: true,
        message: true,
        status: true,
        admin_reply: true,
        created_at: true,
        replied_at: true
      }
    });

    const formattedTickets = tickets.map(ticket => ({
      id: ticket.id,
      subject: ticket.subject,
      message: ticket.message,
      status: ticket.status,
      adminReply: ticket.admin_reply,
      createdAt: ticket.created_at.toISOString(),
      repliedAt: ticket.replied_at?.toISOString() || null
    }));

    const response: PaginatedResponse<any> = {
      success: true,
      data: formattedTickets,
      pagination: {
        page,
        limit,
        total,
        totalPages
      }
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Get support tickets error:', error);
    const response: ApiResponse = {
      success: false,
      error: '문의 목록을 불러오는데 실패했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}