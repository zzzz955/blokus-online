import { NextRequest, NextResponse } from 'next/server';
import { PrismaClient } from '@prisma/client';
import { getAdminFromRequest, requireAdmin } from '@/lib/admin-auth';

const prisma = new PrismaClient();

// Force dynamic rendering for this API route
export const dynamic = 'force-dynamic';

// GET /api/admin/testimonials - 관리자용 후기 목록 조회 (모든 후기 포함)
export async function GET(request: NextRequest) {
  try {
    const admin = getAdminFromRequest(request);
    if (!requireAdmin()(admin)) {
      return NextResponse.json(
        { success: false, error: '관리자 권한이 필요합니다.' },
        { status: 401 }
      );
    }

    const { searchParams } = new URL(request.url);
    const page = parseInt(searchParams.get('page') || '1');
    const limit = parseInt(searchParams.get('limit') || '20');
    const offset = (page - 1) * limit;

    // 총 개수 조회 (모든 후기)
    const total = await prisma.testimonial.count();
    const totalPages = Math.ceil(total / limit);

    // 후기 목록 조회 (모든 후기, is_pinned DESC, created_at DESC 순으로 정렬)
    const testimonials = await prisma.testimonial.findMany({
      orderBy: [
        { is_pinned: 'desc' },
        { created_at: 'desc' }
      ],
      take: limit,
      skip: offset,
      select: {
        id: true,
        name: true,
        rating: true,
        comment: true,
        created_at: true,
        is_pinned: true,
        is_published: true
      }
    }).then((items: any[]) => items.map((item: any) => ({
      id: item.id,
      name: item.name,
      rating: item.rating,
      comment: item.comment,
      createdAt: item.created_at.toISOString(),
      isPinned: item.is_pinned,
      isPublished: item.is_published
    })));

    return NextResponse.json({
      success: true,
      data: testimonials,
      pagination: {
        page,
        limit,
        total,
        totalPages
      }
    });
  } catch (error) {
    console.error('Error fetching admin testimonials:', error);
    return NextResponse.json(
      { success: false, error: '후기를 불러오는데 실패했습니다.' },
      { status: 500 }
    );
  }
}