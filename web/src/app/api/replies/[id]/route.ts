import { NextRequest, NextResponse } from 'next/server'
import { getServerSession } from 'next-auth'
import { authOptions } from '@/lib/auth'
import { prisma } from '@/lib/prisma'

interface RouteParams {
  params: {
    id: string
  }
}

// DELETE /api/replies/[id] - 대댓글 삭제 (작성자만)
export async function DELETE(request: NextRequest, { params }: RouteParams) {
  try {
    const session = await getServerSession(authOptions)
    if (!session?.user?.id) {
      return NextResponse.json(
        { error: '로그인이 필요합니다.' },
        { status: 401 }
      )
    }

    const replyId = parseInt(params.id)
    if (isNaN(replyId)) {
      return NextResponse.json(
        { error: '올바르지 않은 대댓글 ID입니다.' },
        { status: 400 }
      )
    }

    // 대댓글 존재 및 작성자 확인
    const reply = await prisma.reply.findUnique({
      where: { id: replyId },
      include: { author: true },
    })

    if (!reply) {
      return NextResponse.json(
        { error: '존재하지 않는 대댓글입니다.' },
        { status: 404 }
      )
    }

    if (reply.is_deleted) {
      return NextResponse.json(
        { error: '이미 삭제된 대댓글입니다.' },
        { status: 400 }
      )
    }

    // 작성자만 삭제 가능
    if (reply.author_id !== parseInt(session.user.id)) {
      return NextResponse.json(
        { error: '본인이 작성한 대댓글만 삭제할 수 있습니다.' },
        { status: 403 }
      )
    }

    // 논리적 삭제 (실제로는 is_deleted 플래그만 업데이트)
    await prisma.reply.update({
      where: { id: replyId },
      data: { is_deleted: true },
    })

    return NextResponse.json({ message: '대댓글이 삭제되었습니다.' })
  } catch (error) {
    console.error('대댓글 삭제 에러:', error)
    return NextResponse.json(
      { error: '대댓글 삭제 중 오류가 발생했습니다.' },
      { status: 500 }
    )
  }
}