import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from 'next-auth';
import { authOptions } from '@/lib/auth';
import { prisma } from '@/lib/prisma';
import { z } from 'zod';

// 게시글 상세 조회 (GET /api/posts/[id])
export async function GET(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    const postId = parseInt(params.id);

    if (isNaN(postId)) {
      return NextResponse.json(
        { success: false, error: '올바른 게시글 ID가 아닙니다.' },
        { status: 400 }
      );
    }

    // 게시글 조회
    const post = await prisma.post.findUnique({
      where: {
        id: postId,
        is_deleted: false, // 삭제된 게시글은 조회 불가
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

    if (!post) {
      return NextResponse.json(
        { success: false, error: '게시글을 찾을 수 없습니다.' },
        { status: 404 }
      );
    }

    // 숨겨진 게시글 접근 권한 확인
    if (post.is_hidden) {
      const session = await getServerSession(authOptions);
      const userId = session?.user?.id ? parseInt(session.user.id) : null;
      
      // 작성자만 숨겨진 게시글 조회 가능
      if (userId !== post.author_id) {
        return NextResponse.json(
          { success: false, error: '게시글을 찾을 수 없습니다.' },
          { status: 404 }
        );
      }
    }

    // 조회수 증가
    await prisma.post.update({
      where: { id: postId },
      data: { view_count: { increment: 1 } },
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
      viewCount: post.view_count + 1, // 증가된 조회수 반영
      createdAt: post.created_at.toISOString(),
      updatedAt: post.updated_at.toISOString(),
    };

    return NextResponse.json({
      success: true,
      data: transformedPost,
    });
  } catch (error) {
    console.error('게시글 조회 에러:', error);
    return NextResponse.json(
      { success: false, error: '게시글을 불러오는데 실패했습니다.' },
      { status: 500 }
    );
  }
}

// 게시글 수정 (PUT /api/posts/[id])
export async function PUT(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    // 인증 확인
    const session = await getServerSession(authOptions);
    if (!session?.user?.id) {
      return NextResponse.json(
        { success: false, error: '로그인이 필요합니다.' },
        { status: 401 }
      );
    }

    const postId = parseInt(params.id);
    const userId = parseInt(session.user.id);

    if (isNaN(postId)) {
      return NextResponse.json(
        { success: false, error: '올바른 게시글 ID가 아닙니다.' },
        { status: 400 }
      );
    }

    // 게시글 존재 및 권한 확인
    const existingPost = await prisma.post.findUnique({
      where: { id: postId, is_deleted: false },
    });

    if (!existingPost) {
      return NextResponse.json(
        { success: false, error: '게시글을 찾을 수 없습니다.' },
        { status: 404 }
      );
    }

    if (existingPost.author_id !== userId) {
      return NextResponse.json(
        { success: false, error: '게시글을 수정할 권한이 없습니다.' },
        { status: 403 }
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

    // 게시글 수정
    const updatedPost = await prisma.post.update({
      where: { id: postId },
      data: {
        title: validatedData.title,
        content: validatedData.content,
        category: validatedData.category,
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
      id: updatedPost.id,
      title: updatedPost.title,
      content: updatedPost.content,
      category: updatedPost.category,
      authorId: updatedPost.author_id,
      author: {
        username: updatedPost.author.username,
        displayName: updatedPost.author.display_name,
      },
      isHidden: updatedPost.is_hidden,
      isDeleted: updatedPost.is_deleted,
      viewCount: updatedPost.view_count,
      createdAt: updatedPost.created_at.toISOString(),
      updatedAt: updatedPost.updated_at.toISOString(),
    };

    return NextResponse.json({
      success: true,
      data: transformedPost,
      message: '게시글이 성공적으로 수정되었습니다.',
    });
  } catch (error) {
    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { success: false, error: error.issues[0].message },
        { status: 400 }
      );
    }

    console.error('게시글 수정 에러:', error);
    return NextResponse.json(
      { success: false, error: '게시글 수정에 실패했습니다.' },
      { status: 500 }
    );
  }
}

// 게시글 삭제 (DELETE /api/posts/[id])
export async function DELETE(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    // 인증 확인
    const session = await getServerSession(authOptions);
    if (!session?.user?.id) {
      return NextResponse.json(
        { success: false, error: '로그인이 필요합니다.' },
        { status: 401 }
      );
    }

    const postId = parseInt(params.id);
    const userId = parseInt(session.user.id);

    if (isNaN(postId)) {
      return NextResponse.json(
        { success: false, error: '올바른 게시글 ID가 아닙니다.' },
        { status: 400 }
      );
    }

    // 게시글 존재 확인
    const existingPost = await prisma.post.findUnique({
      where: { id: postId, is_deleted: false },
    });

    if (!existingPost) {
      return NextResponse.json(
        { success: false, error: '게시글을 찾을 수 없습니다.' },
        { status: 404 }
      );
    }

    // 권한 확인 (작성자 또는 관리자)
    const isAuthor = existingPost.author_id === userId;
    
    // 관리자 권한 확인
    let isAdmin = false;
    if (!isAuthor) {
      const { checkIsAdmin } = await import('@/lib/admin-check');
      isAdmin = await checkIsAdmin(session.user.id);
    }

    if (!isAuthor && !isAdmin) {
      return NextResponse.json(
        { success: false, error: '게시글을 삭제할 권한이 없습니다.' },
        { status: 403 }
      );
    }

    // 게시글 소프트 삭제
    await prisma.post.update({
      where: { id: postId },
      data: { is_deleted: true },
    });

    return NextResponse.json({
      success: true,
      message: '게시글이 성공적으로 삭제되었습니다.',
    });
  } catch (error) {
    console.error('게시글 삭제 에러:', error);
    return NextResponse.json(
      { success: false, error: '게시글 삭제에 실패했습니다.' },
      { status: 500 }
    );
  }
}