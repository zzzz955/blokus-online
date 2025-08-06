import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from 'next-auth';
import { authOptions } from '@/lib/auth';
import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

// Force dynamic rendering for this API route
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

// 후기 작성 데이터 검증
function validateTestimonial(data: any) {
  const errors: string[] = [];
  
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

    console.log('Fetching testimonials, total count:', total);

    // 후기 목록 조회 (사용자 정보 포함)
    const rawTestimonials = await prisma.testimonial.findMany({
      where: { is_published: true },
      orderBy: [
        { is_pinned: 'desc' },
        { created_at: 'desc' }
      ],
      take: effectiveLimit,
      skip: offset,
      include: {
        user: {
          select: {
            username: true,
            user_stats: {
              select: {
                level: true,
                total_games: true,
                wins: true,
                total_score: true,
                best_score: true
              }
            }
          }
        }
      }
    } as any);

    console.log('Raw testimonials from DB:', rawTestimonials.length);
    
    const testimonials = rawTestimonials.map((item: any) => ({
      id: item.id,
      rating: item.rating,
      comment: item.comment,
      createdAt: item.created_at.toISOString(),
      isPinned: item.is_pinned,
      isPublished: item.is_published,
      user: item.user ? {
        username: item.user.username,
        level: item.user.user_stats?.level || 1,
        totalGames: item.user.user_stats?.total_games || 0,
        wins: item.user.user_stats?.wins || 0,
        totalScore: item.user.user_stats?.total_score || 0,
        bestScore: item.user.user_stats?.best_score || 0,
        winRate: item.user.user_stats?.total_games > 0 
          ? Math.round((item.user.user_stats.wins / item.user.user_stats.total_games) * 100)
          : 0
      } : null,
      // 하위 호환성을 위한 name 필드 (사용자명 또는 게스트)
      name: item.user?.username || '게스트 사용자'
    }));

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

// POST /api/testimonials - 새 후기 작성 (로그인 필수)
export async function POST(request: NextRequest) {
  try {
    // 세션 확인
    const session = await getServerSession(authOptions);
    if (!session?.user?.id) {
      return NextResponse.json(
        { success: false, error: '로그인이 필요합니다.' },
        { status: 401 }
      );
    }

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
    
    const { rating, comment } = body;

    // 기존 후기가 있는지 확인 (한 사용자당 하나의 후기만 허용)
    const existingTestimonial = await prisma.testimonial.findFirst({
      where: { userId: parseInt(session.user.id) }
    } as any);

    if (existingTestimonial) {
      return NextResponse.json(
        { success: false, error: '이미 후기를 작성하셨습니다. 수정을 원하시면 기존 후기를 삭제해주세요.' },
        { status: 409 }
      );
    }

    // 데이터베이스에 후기 저장
    const testimonial = await prisma.testimonial.create({
      data: {
        userId: parseInt(session.user.id),
        rating,
        comment: comment || null,
      },
      include: {
        user: {
          select: {
            username: true,
            user_stats: {
              select: {
                level: true,
                total_games: true,
                wins: true,
                total_score: true,
                best_score: true
              }
            }
          }
        }
      }
    });

    const newTestimonial = {
      id: testimonial.id,
      rating: testimonial.rating,
      comment: testimonial.comment,
      createdAt: testimonial.created_at.toISOString(),
      isPinned: testimonial.is_pinned,
      isPublished: testimonial.is_published,
      user: (testimonial as any).user ? {
        username: (testimonial as any).user.username,
        level: (testimonial as any).user.user_stats?.level || 1,
        totalGames: (testimonial as any).user.user_stats?.total_games || 0,
        wins: (testimonial as any).user.user_stats?.wins || 0,
        totalScore: (testimonial as any).user.user_stats?.total_score || 0,
        bestScore: (testimonial as any).user.user_stats?.best_score || 0,
        winRate: (testimonial as any).user.user_stats?.total_games > 0 
          ? Math.round(((testimonial as any).user.user_stats.wins / (testimonial as any).user.user_stats.total_games) * 100)
          : 0
      } : null,
      name: (testimonial as any).user?.username || '게스트 사용자'
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

// DELETE /api/testimonials - 후기 삭제 (본인만)
export async function DELETE(request: NextRequest) {
  try {
    // 세션 확인
    const session = await getServerSession(authOptions);
    if (!session?.user?.id) {
      return NextResponse.json(
        { success: false, error: '로그인이 필요합니다.' },
        { status: 401 }
      );
    }

    const { searchParams } = new URL(request.url);
    const testimonialId = searchParams.get('id');

    if (!testimonialId) {
      return NextResponse.json(
        { success: false, error: '후기 ID가 필요합니다.' },
        { status: 400 }
      );
    }

    // 후기 존재 여부 및 소유권 확인
    const testimonial = await prisma.testimonial.findUnique({
      where: { id: parseInt(testimonialId) }
    });

    if (!testimonial) {
      return NextResponse.json(
        { success: false, error: '후기를 찾을 수 없습니다.' },
        { status: 404 }
      );
    }

    if (testimonial.userId !== parseInt(session.user.id)) {
      return NextResponse.json(
        { success: false, error: '본인의 후기만 삭제할 수 있습니다.' },
        { status: 403 }
      );
    }

    // 후기 삭제
    await prisma.testimonial.delete({
      where: { id: parseInt(testimonialId) }
    });

    return NextResponse.json({
      success: true,
      message: '후기가 성공적으로 삭제되었습니다.'
    });

  } catch (error) {
    console.error('Error deleting testimonial:', error);
    
    return NextResponse.json(
      { success: false, error: '후기 삭제에 실패했습니다.' },
      { status: 500 }
    );
  }
}