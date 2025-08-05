'use client'

import React from 'react'
import CommentItem from './CommentItem'

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

interface CommentListProps {
  comments: Comment[]
  currentUserId?: number
  onCommentDeleted: (commentId: number) => void
  onReplyAdded: (commentId: number, reply: Reply) => void
  onReplyDeleted: (commentId: number, replyId: number) => void
}

export default function CommentList({ 
  comments, 
  currentUserId, 
  onCommentDeleted, 
  onReplyAdded, 
  onReplyDeleted 
}: CommentListProps) {
  if (comments.length === 0) {
    return (
      <div className="text-center py-8 text-gray-500">
        아직 댓글이 없습니다. 첫 번째 댓글을 작성해보세요!
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {comments.map((comment) => (
        <CommentItem
          key={comment.id}
          comment={comment}
          currentUserId={currentUserId}
          onCommentDeleted={onCommentDeleted}
          onReplyAdded={onReplyAdded}
          onReplyDeleted={onReplyDeleted}
        />
      ))}
    </div>
  )
}