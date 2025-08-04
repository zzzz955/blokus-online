import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { ApiResponse, PaginatedResponse, Announcement } from '@/types';

// Force dynamic rendering for this API route
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const page = parseInt(searchParams.get('page') || '1');
    const limit = parseInt(searchParams.get('limit') || '10');
    const pinned = searchParams.get('pinned') === 'true';
    
    const skip = (page - 1) * limit;
    
    const where = {
      is_published: true,
      ...(pinned && { is_pinned: true }),
    };

    const [announcements, total] = await Promise.all([
      prisma.announcement.findMany({
        where,
        orderBy: [
          { is_pinned: 'desc' },
          { created_at: 'desc' },
        ],
        skip,
        take: limit,
      }),
      prisma.announcement.count({ where }),
    ]);

    const response: PaginatedResponse<Announcement> = {
      success: true,
      data: announcements.map(announcement => ({
        ...announcement,
        createdAt: announcement.created_at.toISOString(),
        updatedAt: announcement.updated_at.toISOString(),
        isPinned: announcement.is_pinned,
        isPublished: announcement.is_published,
      })),
      pagination: {
        page,
        limit,
        total,
        totalPages: Math.ceil(total / limit),
      },
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Get announcements error:', error);
    const response: ApiResponse = {
      success: false,
      error: '공지사항을 불러오는데 실패했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}