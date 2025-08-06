import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from 'next-auth';
import { authOptions } from '@/lib/auth';
import { prisma } from '@/lib/prisma';

// 게시글 숨기기/보이기 토글 (PATCH /api/posts/[id]/hide)
export async function PATCH(
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

    // 숨김 상태 토글
    const updatedPost = await prisma.post.update({
      where: { id: postId },
      data: { is_hidden: !existingPost.is_hidden },
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

    const action = updatedPost.is_hidden ? '숨김' : '공개';
    
    return NextResponse.json({
      success: true,
      data: transformedPost,
      message: `게시글이 성공적으로 ${action} 처리되었습니다.`,
    });
  } catch (error) {
    console.error('게시글 숨김 처리 에러:', error);
    return NextResponse.json(
      { success: false, error: '게시글 숨김 처리에 실패했습니다.' },
      { status: 500 }
    );
  }
}