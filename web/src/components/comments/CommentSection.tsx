'use client'

import React, { useState, useEffect } from 'react'
import { useSession } from 'next-auth/react'
import CommentForm from './CommentForm'
import CommentList from './CommentList'

interface Comment {
  id: number
  content: string
  author: {
    id: number
    username: string
    level: number
  }
  createdAt: string
  replies: Reply[]
}

interface Reply {
  id: number
  content: string
  author: {
    id: number
    username: string
    level: number
  }
  createdAt: string
}

interface CommentSectionProps {
  postId?: number
  announcementId?: number
  patchNoteId?: number
}

export default function CommentSection({ postId, announcementId, patchNoteId }: CommentSectionProps) {
  const { data: session } = useSession()
  const [comments, setComments] = useState<Comment[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>('')

  // 댓글 목록 불러오기
  const fetchComments = async () => {
    try {
      setLoading(true)
      const params = new URLSearchParams()
      if (postId !== undefined) params.set('postId', postId.toString())
      if (announcementId !== undefined) params.set('announcementId', announcementId.toString())
      if (patchNoteId !== undefined) params.set('patchNoteId', patchNoteId.toString())

      const response = await fetch(`/api/comments?${params}`)
      if (!response.ok) {
        throw new Error('댓글을 불러오는 중 오류가 발생했습니다.')
      }

      const data = await response.json()
      setComments(data)
      setError('')
    } catch (err) {
      setError(err instanceof Error ? err.message : '알 수 없는 오류가 발생했습니다.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchComments()
  }, [postId, announcementId, patchNoteId])

  // 댓글 작성 완료 후 목록 갱신
  const handleCommentAdded = (newComment: Comment) => {
    setComments(prev => [newComment, ...prev])
  }

  // 댓글 삭제 후 목록 갱신
  const handleCommentDeleted = (commentId: number) => {
    setComments(prev => prev.filter(comment => comment.id !== commentId))
  }

  // 대댓글 추가 후 목록 갱신
  const handleReplyAdded = (commentId: number, newReply: Reply) => {
    setComments(prev => prev.map(comment => 
      comment.id === commentId 
        ? { ...comment, replies: [...comment.replies, newReply] }
        : comment
    ))
  }

  // 대댓글 삭제 후 목록 갱신
  const handleReplyDeleted = (commentId: number, replyId: number) => {
    setComments(prev => prev.map(comment => 
      comment.id === commentId 
        ? { ...comment, replies: comment.replies.filter(reply => reply.id !== replyId) }
        : comment
    ))
  }

  if (loading) {
    return (
      <div className="mt-12">
        <h3 className="text-xl font-bold mb-6">댓글</h3>
        <div className="flex justify-center py-8">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-600"></div>
        </div>
      </div>
    )
  }

  return (
    <div className="mt-12">
      <h3 className="text-xl font-bold mb-6">댓글 ({comments.length})</h3>
      
      {error && (
        <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-lg text-red-700">
          {error}
        </div>
      )}

      {session ? (
        <CommentForm
          postId={postId}
          announcementId={announcementId}
          patchNoteId={patchNoteId}
          onCommentAdded={handleCommentAdded}
        />
      ) : (
        <div className="mb-6 p-4 bg-gray-50 border border-gray-200 rounded-lg text-gray-600">
          댓글을 작성하려면 <a href="/auth/signin" className="text-primary-600 hover:underline">로그인</a>이 필요합니다.
        </div>
      )}

      <CommentList
        comments={comments}
        currentUserId={session?.user?.id ? parseInt(session.user.id) : undefined}
        onCommentDeleted={handleCommentDeleted}
        onReplyAdded={handleReplyAdded}
        onReplyDeleted={handleReplyDeleted}
      />
    </div>
  )
}