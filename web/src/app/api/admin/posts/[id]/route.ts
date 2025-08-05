import { NextRequest, NextResponse } from 'next/server';
import { getAdminFromRequest } from '@/lib/admin-auth';
import { prisma } from '@/lib/prisma';

// 관리자용 게시글 삭제 (DELETE /api/admin/posts/[id])
export async function DELETE(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    // 관리자 권한 확인
    const admin = getAdminFromRequest(request);
    if (!admin) {
      return NextResponse.json(
        { success: false, error: '관리자 권한이 필요합니다.' },
        { status: 401 }
      );
    }

    const postId = parseInt(params.id);

    if (isNaN(postId)) {
      return NextResponse.json(
        { success: false, error: '올바른 게시글 ID가 아닙니다.' },
        { status: 400 }
      );
    }

    // 게시글 존재 확인
    const existingPost = await prisma.post.findUnique({
      where: { id: postId },
      include: {
        author: {
          select: {
            username: true,
          },
        },
      },
    });

    if (!existingPost) {
      return NextResponse.json(
        { success: false, error: '게시글을 찾을 수 없습니다.' },
        { status: 404 }
      );
    }

    // 게시글 소프트 삭제 (관리자 권한으로)
    await prisma.post.update({
      where: { id: postId },
      data: { is_deleted: true },
    });

    // 관리 로그 남기기 (필요시 구현)
    console.log(`관리자 ${admin.username}이 게시글 ${postId}를 삭제했습니다. (작성자: ${existingPost.author.username})`);

    return NextResponse.json({
      success: true,
      message: '게시글이 성공적으로 삭제되었습니다.',
      data: {
        postId,
        title: existingPost.title,
        author: existingPost.author.username,
        deletedBy: admin.username,
        deletedAt: new Date().toISOString(),
      },
    });
  } catch (error) {
    console.error('관리자 게시글 삭제 에러:', error);
    return NextResponse.json(
      { success: false, error: '게시글 삭제에 실패했습니다.' },
      { status: 500 }
    );
  }
}

// 관리자용 게시글 복구 (PATCH /api/admin/posts/[id])
export async function PATCH(
  request: NextRequest,
  { params }: { params: { id: string } }
) {
  try {
    // 관리자 권한 확인
    const admin = getAdminFromRequest(request);
    if (!admin) {
      return NextResponse.json(
        { success: false, error: '관리자 권한이 필요합니다.' },
        { status: 401 }
      );
    }

    const postId = parseInt(params.id);
    const body = await request.json();
    const action = body.action; // 'restore', 'hide', 'show'

    if (isNaN(postId)) {
      return NextResponse.json(
        { success: false, error: '올바른 게시글 ID가 아닙니다.' },
        { status: 400 }
      );
    }

    // 게시글 존재 확인
    const existingPost = await prisma.post.findUnique({
      where: { id: postId },
      include: {
        author: {
          select: {
            username: true,
          },
        },
      },
    });

    if (!existingPost) {
      return NextResponse.json(
        { success: false, error: '게시글을 찾을 수 없습니다.' },
        { status: 404 }
      );
    }

    let updateData: any = {};
    let actionMessage = '';

    switch (action) {
      case 'restore':
        updateData = { is_deleted: false };
        actionMessage = '복구';
        break;
      case 'hide':
        updateData = { is_hidden: true };
        actionMessage = '숨김';
        break;
      case 'show':
        updateData = { is_hidden: false };
        actionMessage = '공개';
        break;
      default:
        return NextResponse.json(
          { success: false, error: '올바른 액션이 아닙니다.' },
          { status: 400 }
        );
    }

    // 게시글 상태 업데이트
    const updatedPost = await prisma.post.update({
      where: { id: postId },
      data: updateData,
      include: {
        author: {
          select: {
            username: true,
            display_name: true,
          },
        },
      },
    });

    // 관리 로그 남기기
    console.log(`관리자 ${admin.username}이 게시글 ${postId}를 ${actionMessage} 처리했습니다. (작성자: ${existingPost.author.username})`);

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
      message: `게시글이 성공적으로 ${actionMessage} 처리되었습니다.`,
      admin: {
        actionBy: admin.username,
        action: actionMessage,
        actionAt: new Date().toISOString(),
      },
    });
  } catch (error) {
    console.error('관리자 게시글 관리 에러:', error);
    return NextResponse.json(
      { success: false, error: '게시글 관리에 실패했습니다.' },
      { status: 500 }
    );
  }
}