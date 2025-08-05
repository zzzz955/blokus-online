import { NextRequest, NextResponse } from 'next/server';
import { getAdminFromRequest } from '@/lib/admin-auth';
import { prisma } from '@/lib/prisma';
import { PostCategory } from '@/types';

// 관리자용 게시글 목록 조회 (모든 게시글 포함 - 숨김/삭제 포함)
export async function GET(request: NextRequest) {
  try {
    // 관리자 권한 확인
    const admin = getAdminFromRequest(request);
    if (!admin) {
      return NextResponse.json(
        { success: false, error: '관리자 권한이 필요합니다.' },
        { status: 401 }
      );
    }

    const { searchParams } = new URL(request.url);
    const category = searchParams.get('category') as PostCategory | null;
    const page = parseInt(searchParams.get('page') || '1');
    const limit = parseInt(searchParams.get('limit') || '20');
    const search = searchParams.get('search') || '';
    const status = searchParams.get('status') || 'all'; // all, active, hidden, deleted

    // 필터 조건
    const where: any = {};

    // 카테고리 필터
    if (category && ['QUESTION', 'GUIDE', 'GENERAL'].includes(category)) {
      where.category = category;
    }

    // 상태 필터
    switch (status) {
      case 'active':
        where.is_deleted = false;
        where.is_hidden = false;
        break;
      case 'hidden':
        where.is_deleted = false;
        where.is_hidden = true;
        break;
      case 'deleted':
        where.is_deleted = true;
        break;
      // 'all'의 경우 모든 게시글 조회
    }

    // 검색 필터
    if (search) {
      where.OR = [
        { title: { contains: search, mode: 'insensitive' } },
        { content: { contains: search, mode: 'insensitive' } },
        { author: { username: { contains: search, mode: 'insensitive' } } },
      ];
    }

    // 전체 개수 조회
    const total = await prisma.post.count({ where });

    // 게시글 목록 조회
    const posts = await prisma.post.findMany({
      where,
      include: {
        author: {
          select: {
            username: true,
            display_name: true,
          },
        },
      },
      orderBy: {
        created_at: 'desc',
      },
      skip: (page - 1) * limit,
      take: limit,
    });

    // 응답 데이터 변환
    const transformedPosts = posts.map(post => ({
      id: post.id,
      title: post.title,
      content: post.content.substring(0, 200) + (post.content.length > 200 ? '...' : ''), // 미리보기
      category: post.category,
      authorId: post.author_id,
      author: {
        username: post.author.username,
        displayName: post.author.display_name,
      },
      isHidden: post.is_hidden,
      isDeleted: post.is_deleted,
      viewCount: post.view_count,
      createdAt: post.created_at.toISOString(),
      updatedAt: post.updated_at.toISOString(),
    }));

    // 통계 정보도 함께 반환
    const stats = await prisma.post.groupBy({
      by: ['category'],
      where: { is_deleted: false },
      _count: { id: true },
    });

    const categoryStats = {
      QUESTION: 0,
      GUIDE: 0,
      GENERAL: 0,
    };

    stats.forEach(stat => {
      categoryStats[stat.category as PostCategory] = stat._count.id;
    });

    const statusStats = {
      total: await prisma.post.count(),
      active: await prisma.post.count({ where: { is_deleted: false, is_hidden: false } }),
      hidden: await prisma.post.count({ where: { is_deleted: false, is_hidden: true } }),
      deleted: await prisma.post.count({ where: { is_deleted: true } }),
    };

    return NextResponse.json({
      success: true,
      data: transformedPosts,
      pagination: {
        page,
        limit,
        total,
        totalPages: Math.ceil(total / limit),
      },
      stats: {
        categories: categoryStats,
        status: statusStats,
      },
    });
  } catch (error) {
    console.error('관리자 게시글 목록 조회 에러:', error);
    return NextResponse.json(
      { success: false, error: '게시글 목록을 불러오는데 실패했습니다.' },
      { status: 500 }
    );
  }
}