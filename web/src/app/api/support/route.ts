import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { ApiResponse, ContactForm } from '@/types';

// API 라우트가 빌드 타임에 실행되지 않도록 설정
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(request: NextRequest) {
  try {
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

    // DB에 저장
    const ticket = await prisma.supportTicket.create({
      data: {
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