import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { ApiResponse, Announcement } from '@/types';

export async function GET(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    const id = parseInt(params.id);
    
    if (isNaN(id)) {
      const response: ApiResponse = {
        success: false,
        error: '잘못된 공지사항 ID입니다.',
      };
      return NextResponse.json(response, { status: 400 });
    }

    const announcement = await prisma.announcement.findUnique({
      where: {
        id,
        isPublished: true,
      },
    });

    if (!announcement) {
      const response: ApiResponse = {
        success: false,
        error: '공지사항을 찾을 수 없습니다.',
      };
      return NextResponse.json(response, { status: 404 });
    }

    const response: ApiResponse<Announcement> = {
      success: true,
      data: announcement,
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Get announcement error:', error);
    const response: ApiResponse = {
      success: false,
      error: '공지사항을 불러오는데 실패했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}