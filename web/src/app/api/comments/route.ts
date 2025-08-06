import { NextRequest, NextResponse } from 'next/server'
import { getServerSession } from 'next-auth'
import { authOptions } from '@/lib/auth'
import { prisma } from '@/lib/prisma'
import { z } from 'zod'

const createCommentSchema = z.object({
  content: z.string().min(1).max(1000),
  postId: z.number().optional(),
  announcementId: z.number().optional(),
  patchNoteId: z.number().optional(),
})

const getCommentsSchema = z.object({
  postId: z.string().nullable().optional(),
  announcementId: z.string().nullable().optional(), 
  patchNoteId: z.string().nullable().optional(),
})

// GET /api/comments - 댓글 목록 조회
export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url)
    const query = getCommentsSchema.parse({
      postId: searchParams.get('postId'),
      announcementId: searchParams.get('announcementId'),
      patchNoteId: searchParams.get('patchNoteId'),
    })

    // 정확히 하나의 대상이 지정되어야 함
    const validTargets = [query.postId, query.announcementId, query.patchNoteId].filter(id => id && id !== 'null')
    if (validTargets.length !== 1) {
      return NextResponse.json(
        { error: '정확히 하나의 대상(postId, announcementId, patchNoteId)을 지정해야 합니다.' },
        { status: 400 }
      )
    }

    const whereClause: any = { is_deleted: false }
    
    if (query.postId && query.postId !== 'null') {
      whereClause.post_id = parseInt(query.postId)
    } else if (query.announcementId && query.announcementId !== 'null') {
      whereClause.announcement_id = parseInt(query.announcementId)
    } else if (query.patchNoteId && query.patchNoteId !== 'null') {
      whereClause.patch_note_id = parseInt(query.patchNoteId)
    }

    const comments = await prisma.comment.findMany({
      where: whereClause,
      include: {
        author: {
          include: {
            user_stats: true,
          },
        },
        replies: {
          where: { is_deleted: false },
          include: {
            author: {
              include: {
                user_stats: true,
              },
            },
          },
          orderBy: { created_at: 'asc' },
        },
      },
      orderBy: { created_at: 'desc' },
    })

    // 응답 데이터 형식 변환
    const formattedComments = comments.map(comment => ({
      id: comment.id,
      content: comment.content,
      author: {
        id: comment.author.user_id,
        username: comment.author.username,
        level: comment.author.user_stats?.level || 1,
      },
      createdAt: comment.created_at,
      replies: comment.replies.map(reply => ({
        id: reply.id,
        content: reply.content,
        author: {
          id: reply.author.user_id,
          username: reply.author.username,
          level: reply.author.user_stats?.level || 1,
        },
        createdAt: reply.created_at,
      })),
    }))

    return NextResponse.json(formattedComments)
  } catch (error) {
    console.error('댓글 조회 에러:', error)
    return NextResponse.json(
      { error: '댓글을 불러오는 중 오류가 발생했습니다.' },
      { status: 500 }
    )
  }
}

// POST /api/comments - 댓글 작성
export async function POST(request: NextRequest) {
  try {
    const session = await getServerSession(authOptions)
    if (!session?.user?.id) {
      return NextResponse.json(
        { error: '로그인이 필요합니다.' },
        { status: 401 }
      )
    }

    const body = await request.json()
    const { content, postId, announcementId, patchNoteId } = createCommentSchema.parse(body)

    // 정확히 하나의 대상에만 댓글을 달 수 있음
    const targetCount = [postId, announcementId, patchNoteId].filter(Boolean).length
    if (targetCount !== 1) {
      return NextResponse.json(
        { error: '정확히 하나의 대상에만 댓글을 달 수 있습니다.' },
        { status: 400 }
      )
    }

    // 대상이 존재하는지 확인
    if (postId) {
      const post = await prisma.post.findFirst({
        where: { id: postId, is_deleted: false, is_hidden: false }
      })
      if (!post) {
        return NextResponse.json(
          { error: '존재하지 않는 게시글입니다.' },
          { status: 404 }
        )
      }
    } else if (announcementId) {
      const announcement = await prisma.announcement.findFirst({
        where: { id: announcementId, is_published: true }
      })
      if (!announcement) {
        return NextResponse.json(
          { error: '존재하지 않는 공지사항입니다.' },
          { status: 404 }
        )
      }
    } else if (patchNoteId) {
      const patchNote = await prisma.patchNote.findUnique({
        where: { id: patchNoteId }
      })
      if (!patchNote) {
        return NextResponse.json(
          { error: '존재하지 않는 패치노트입니다.' },
          { status: 404 }
        )
      }
    }

    const comment = await prisma.comment.create({
      data: {
        content,
        author_id: parseInt(session.user.id),
        post_id: postId,
        announcement_id: announcementId,
        patch_note_id: patchNoteId,
      },
      include: {
        author: {
          include: {
            user_stats: true,
          },
        },
      },
    })

    const formattedComment = {
      id: comment.id,
      content: comment.content,
      author: {
        id: comment.author.user_id,
        username: comment.author.username,
        level: comment.author.user_stats?.level || 1,
      },
      createdAt: comment.created_at,
      replies: [],
    }

    return NextResponse.json(formattedComment, { status: 201 })
  } catch (error) {
    console.error('댓글 작성 에러:', error)
    if (error instanceof z.ZodError) {
      return NextResponse.json(
        { error: '입력 데이터가 올바르지 않습니다.', details: error.errors },
        { status: 400 }
      )
    }
    return NextResponse.json(
      { error: '댓글 작성 중 오류가 발생했습니다.' },
      { status: 500 }
    )
  }
}