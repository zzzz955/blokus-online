import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { ApiResponse, PaginatedResponse, Announcement } from '@/types';
import { verifyAdminToken } from '@/lib/middleware';

export async function GET(request: NextRequest) {
  try {
    const admin = await verifyAdminToken(request);
    if (!admin) {
      return NextResponse.json(
        { success: false, error: '인증이 필요합니다.' },
        { status: 401 }
      );
    }

    const { searchParams } = new URL(request.url);
    const page = parseInt(searchParams.get('page') || '1');
    const limit = parseInt(searchParams.get('limit') || '10');
    
    const skip = (page - 1) * limit;

    const [announcements, total] = await Promise.all([
      prisma.announcement.findMany({
        orderBy: [
          { isPinned: 'desc' },
          { createdAt: 'desc' },
        ],
        skip,
        take: limit,
      }),
      prisma.announcement.count(),
    ]);

    const response: PaginatedResponse<Announcement> = {
      success: true,
      data: announcements,
      pagination: {
        page,
        limit,
        total,
        totalPages: Math.ceil(total / limit),
      },
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Admin get announcements error:', error);
    const response: ApiResponse = {
      success: false,
      error: '공지사항을 불러오는데 실패했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}

export async function POST(request: NextRequest) {
  try {
    const admin = await verifyAdminToken(request);
    if (!admin) {
      return NextResponse.json(
        { success: false, error: '인증이 필요합니다.' },
        { status: 401 }
      );
    }

    const body = await request.json();
    const { title, content, isPinned = false, isPublished = true } = body;

    if (!title || !content) {
      const response: ApiResponse = {
        success: false,
        error: '제목과 내용을 입력해주세요.',
      };
      return NextResponse.json(response, { status: 400 });
    }

    const announcement = await prisma.announcement.create({
      data: {
        title,
        content,
        author: admin.username,
        isPinned,
        isPublished,
      },
    });

    const response: ApiResponse<Announcement> = {
      success: true,
      data: announcement,
      message: '공지사항이 생성되었습니다.',
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Admin create announcement error:', error);
    const response: ApiResponse = {
      success: false,
      error: '공지사항 생성에 실패했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}