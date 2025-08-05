import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from 'next-auth';
import { authOptions } from '@/lib/auth';
import { prisma } from '@/lib/prisma';
import { PostCategory } from '@/types';
import { z } from 'zod';

// 게시글 목록 조회 (GET /api/posts)
export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const category = searchParams.get('category') as PostCategory | null;
    const page = parseInt(searchParams.get('page') || '1');
    const limit = parseInt(searchParams.get('limit') || '10');
    const search = searchParams.get('search') || '';

    // 기본 필터: 삭제되지 않고 숨겨지지 않은 게시글만
    const where: any = {
      is_deleted: false,
      is_hidden: false,
    };

    // 카테고리 필터
    if (category && ['QUESTION', 'GUIDE', 'GENERAL'].includes(category)) {
      where.category = category;
    }

    // 검색 필터
    if (search) {
      where.OR = [
        { title: { contains: search, mode: 'insensitive' } },
        { content: { contains: search, mode: 'insensitive' } },
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
      content: post.content,
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

    return NextResponse.json({
      success: true,
      data: transformedPosts,
      pagination: {
        page,
        limit,
        total,
        totalPages: Math.ceil(total / limit),
      },
    });
  } catch (error) {
    console.error('게시글 목록 조회 에러:', error);
    return NextResponse.json(
      { success: false, error: '게시글 목록을 불러오는데 실패했습니다.' },
      { status: 500 }
    );
  }
}

// 게시글 작성 (POST /api/posts)
export async function POST(request: NextRequest) {
  try {
    // 인증 확인
    const session = await getServerSession(authOptions);
    if (!session?.user?.id) {
      return NextResponse.json(
        { success: false, error: '로그인이 필요합니다.' },
        { status: 401 }
      );
    }

    const body = await request.json();

    // 입력 검증
    const schema = z.object({
      title: z.string().min(1, '제목을 입력해주세요.').max(200, '제목은 200자 이하로 입력해주세요.'),
      content: z.string().min(1, '내용을 입력해주세요.'),
      category: z.enum(['QUESTION', 'GUIDE', 'GENERAL'], {
        errorMap: () => ({ message: '올바른 카테고리를 선택해주세요.' }),
      }),
    });

    const validatedData = schema.parse(body);

    // 게시글 생성
    const post = await prisma.post.create({
      data: {
        title: validatedData.title,
        content: validatedData.content,
        category: validatedData.category,
        author_id: parseInt(session.user.id),
      },
      include: {
        author: {
          select: {
            username: true,
            display_name: true,
          },
        },
      },
    });

    // 응답 데이터 변환
    const transformedPost = {
      id: post.id,
      title: post.title,
      content: post.content,
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
    };

    return NextResponse.json({
      success: true,
      data: transformedPost,
      message: '게시글이 성공적으로 작성되었습니다.',
    });
  } catch (error) {
    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { success: false, error: error.errors[0].message },
        { status: 400 }
      );
    }

    console.error('게시글 작성 에러:', error);
    return NextResponse.json(
      { success: false, error: '게시글 작성에 실패했습니다.' },
      { status: 500 }
    );
  }
}