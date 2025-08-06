import { NextRequest, NextResponse } from 'next/server'
import { getServerSession } from 'next-auth'
import { authOptions } from '@/lib/auth'
import { prisma } from '@/lib/prisma'
import { z } from 'zod'

const createReplySchema = z.object({
  content: z.string().min(1).max(1000),
})

interface RouteParams {
  params: {
    id: string
  }
}

// POST /api/comments/[id]/replies - 대댓글 작성
export async function POST(request: NextRequest, { params }: RouteParams) {
  try {
    const session = await getServerSession(authOptions)
    if (!session?.user?.id) {
      return NextResponse.json(
        { error: '로그인이 필요합니다.' },
        { status: 401 }
      )
    }

    const commentId = parseInt(params.id)
    if (isNaN(commentId)) {
      return NextResponse.json(
        { error: '올바르지 않은 댓글 ID입니다.' },
        { status: 400 }
      )
    }

    const body = await request.json()
    const { content } = createReplySchema.parse(body)

    // 원댓글 존재 확인
    const comment = await prisma.comment.findUnique({
      where: { id: commentId },
    })

    if (!comment) {
      return NextResponse.json(
        { error: '존재하지 않는 댓글입니다.' },
        { status: 404 }
      )
    }

    if (comment.is_deleted) {
      return NextResponse.json(
        { error: '삭제된 댓글에는 대댓글을 달 수 없습니다.' },
        { status: 400 }
      )
    }

    const reply = await prisma.reply.create({
      data: {
        content,
        author_id: parseInt(session.user.id),
        comment_id: commentId,
      },
      include: {
        author: {
          include: {
            user_stats: true,
          },
        },
      },
    })

    const formattedReply = {
      id: reply.id,
      content: reply.content,
      author: {
        id: reply.author.user_id,
        username: reply.author.username,
        level: reply.author.user_stats?.level || 1,
      },
      createdAt: reply.created_at,
    }

    return NextResponse.json(formattedReply, { status: 201 })
  } catch (error) {
    console.error('대댓글 작성 에러:', error)
    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { error: '입력 데이터가 올바르지 않습니다.', details: error.errors },
        { status: 400 }
      )
    }
    return NextResponse.json(
      { error: '대댓글 작성 중 오류가 발생했습니다.' },
      { status: 500 }
    )
  }
}