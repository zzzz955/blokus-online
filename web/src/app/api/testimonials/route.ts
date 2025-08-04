import { NextRequest, NextResponse } from 'next/server';
import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

// Force dynamic rendering for this API route
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

// 후기 작성 데이터 검증
function validateTestimonial(data: any) {
  const errors: string[] = [];
  
  if (!data.name || typeof data.name !== 'string') {
    errors.push('이름을 입력해주세요');
  } else if (data.name.trim().length === 0) {
    errors.push('이름을 입력해주세요');
  } else if (data.name.length > 50) {
    errors.push('이름은 50자 이하로 입력해주세요');
  }
  
  if (typeof data.rating !== 'number') {
    errors.push('별점을 선택해주세요');
  } else if (data.rating < 1 || data.rating > 5) {
    errors.push('별점은 1점부터 5점까지 선택할 수 있습니다');
  }
  
  if (data.comment && typeof data.comment === 'string' && data.comment.length > 500) {
    errors.push('후기는 500자 이하로 입력해주세요');
  }
  
  return {
    isValid: errors.length === 0,
    errors
  };
}

// GET /api/testimonials - 후기 목록 조회
export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const page = parseInt(searchParams.get('page') || '1');
    const limit = parseInt(searchParams.get('limit') || '20');
    const homeMode = searchParams.get('home') === 'true'; // 홈페이지 모드
    
    // 홈페이지 모드에서는 최대 20개로 제한
    const effectiveLimit = homeMode ? Math.min(limit, 20) : limit;
    const offset = (page - 1) * effectiveLimit;

    // 총 개수 조회 (발행된 후기만)
    const total = await prisma.testimonial.count({
      where: { is_published: true }
    });
    const totalPages = Math.ceil(total / effectiveLimit);

    // 후기 목록 조회 (is_pinned DESC, created_at DESC 순으로 정렬)
    const testimonials = await prisma.testimonial.findMany({
      where: { is_published: true },
      orderBy: [
        { is_pinned: 'desc' },
        { created_at: 'desc' }
      ],
      take: effectiveLimit,
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

    if (homeMode) {
      // 홈페이지 모드에서는 간단한 응답
      return NextResponse.json({
        success: true,
        data: testimonials,
        hasMore: total > offset + effectiveLimit,
        total
      });
    } else {
      // 일반 페이지네이션 응답
      return NextResponse.json({
        success: true,
        data: testimonials,
        pagination: {
          page,
          limit: effectiveLimit,
          total,
          totalPages
        }
      });
    }
  } catch (error) {
    console.error('Error fetching testimonials:', error);
    return NextResponse.json(
      { success: false, error: '후기를 불러오는데 실패했습니다.' },
      { status: 500 }
    );
  }
}

// POST /api/testimonials - 새 후기 작성
export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    
    // 입력 데이터 검증
    const validation = validateTestimonial(body);
    if (!validation.isValid) {
      return NextResponse.json(
        { 
          success: false, 
          error: validation.errors.join(', ')
        },
        { status: 400 }
      );
    }
    
    const { name, rating, comment } = body;

    // 데이터베이스에 후기 저장
    const testimonial = await prisma.testimonial.create({
      data: {
        name,
        rating,
        comment: comment || null,
      },
      select: {
        id: true,
        name: true,
        rating: true,
        comment: true,
        created_at: true,
        is_pinned: true,
        is_published: true
      }
    });

    const newTestimonial = {
      id: testimonial.id,
      name: testimonial.name,
      rating: testimonial.rating,
      comment: testimonial.comment,
      createdAt: testimonial.created_at.toISOString(),
      isPinned: testimonial.is_pinned,
      isPublished: testimonial.is_published
    };

    return NextResponse.json({
      success: true,
      data: newTestimonial,
      message: '후기가 성공적으로 등록되었습니다.'
    }, { status: 201 });

  } catch (error) {
    console.error('Error creating testimonial:', error);
    
    return NextResponse.json(
      { success: false, error: '후기 등록에 실패했습니다.' },
      { status: 500 }
    );
  }
}