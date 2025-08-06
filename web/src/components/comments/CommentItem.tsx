'use client'

import React, { useState } from 'react'
import { formatRelativeTime } from '@/utils/format'
import Button from '@/components/ui/Button'
import UserProfileModal from '@/components/ui/UserProfileModal'
import ReplyForm from './ReplyForm'
import ReplyItem from './ReplyItem'

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

interface CommentItemProps {
  comment: Comment
  currentUserId?: number
  onCommentDeleted: (commentId: number) => void
  onReplyAdded: (commentId: number, reply: Reply) => void
  onReplyDeleted: (commentId: number, replyId: number) => void
}

export default function CommentItem({ 
  comment, 
  currentUserId, 
  onCommentDeleted, 
  onReplyAdded, 
  onReplyDeleted 
}: CommentItemProps) {
  const [showReplyForm, setShowReplyForm] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [showUserProfile, setShowUserProfile] = useState(false)

  const isAuthor = currentUserId === comment.author.id
  const timeAgo = formatRelativeTime(comment.createdAt)

  const handleDelete = async () => {
    if (!confirm('정말로 이 댓글을 삭제하시겠습니까?')) {
      return
    }

    try {
      setLoading(true)
      setError('')

      const response = await fetch(`/api/comments/${comment.id}`, {
        method: 'DELETE',
      })

      if (!response.ok) {
        const errorData = await response.json()
        throw new Error(errorData.error || '댓글 삭제 중 오류가 발생했습니다.')
      }

      onCommentDeleted(comment.id)
    } catch (err) {
      setError(err instanceof Error ? err.message : '알 수 없는 오류가 발생했습니다.')
    } finally {
      setLoading(false)
    }
  }

  const handleReplyAdded = (newReply: Reply) => {
    onReplyAdded(comment.id, newReply)
    setShowReplyForm(false)
  }

  const handleReplyDeleted = (replyId: number) => {
    onReplyDeleted(comment.id, replyId)
  }

  return (
    <div className="border border-dark-border bg-dark-card rounded-lg p-4">
      {/* 댓글 헤더 */}
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center space-x-3">
          <div className="flex items-center space-x-2">
            <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-primary-600 text-white">
              Lv.{comment.author.level}
            </span>
            <button
              onClick={() => setShowUserProfile(true)}
              className="font-semibold text-white hover:text-primary-400 transition-colors cursor-pointer"
            >
              {comment.author.username}
            </button>
          </div>
          <span className="text-sm text-gray-400">
            {timeAgo}
          </span>
        </div>
        
        {isAuthor && (
          <Button
            variant="ghost"
            size="sm"
            onClick={handleDelete}
            loading={loading}
            className="text-red-600 hover:text-red-700 hover:bg-red-50"
          >
            삭제
          </Button>
        )}
      </div>

      {/* 댓글 내용 */}
      <div className="mb-3">
        <p className="text-gray-300 whitespace-pre-wrap">{comment.content}</p>
      </div>

      {/* 에러 메시지 */}
      {error && (
        <div className="mb-3 p-2 bg-red-900/20 border border-red-500/30 rounded text-red-400 text-sm">
          {error}
        </div>
      )}

      {/* 댓글 액션 */}
      {currentUserId && (
        <div className="flex items-center space-x-4 mb-4">
          <button
            onClick={() => setShowReplyForm(!showReplyForm)}
            className="text-sm text-primary-400 hover:text-primary-300 font-medium"
          >
            {showReplyForm ? '취소' : '대댓글 달기'}
          </button>
        </div>
      )}

      {/* 대댓글 작성 폼 */}
      {showReplyForm && (
        <div className="mb-4">
          <ReplyForm
            commentId={comment.id}
            onReplyAdded={handleReplyAdded}
            onCancel={() => setShowReplyForm(false)}
          />
        </div>
      )}

      {/* 대댓글 목록 */}
      {comment.replies.length > 0 && (
        <div className="pl-6 border-l-2 border-gray-600 space-y-4">
          {comment.replies.map((reply) => (
            <ReplyItem
              key={reply.id}
              reply={reply}
              currentUserId={currentUserId}
              onReplyDeleted={handleReplyDeleted}
            />
          ))}
        </div>
      )}

      {/* 사용자 프로필 모달 */}
      <UserProfileModal
        userId={comment.author.id}
        username={comment.author.username}
        isOpen={showUserProfile}
        onClose={() => setShowUserProfile(false)}
      />
    </div>
  )
}