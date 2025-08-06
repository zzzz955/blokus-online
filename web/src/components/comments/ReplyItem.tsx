'use client'

import React, { useState } from 'react'
import { formatRelativeTime } from '@/utils/format'
import Button from '@/components/ui/Button'
import UserProfileModal from '@/components/ui/UserProfileModal'

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

interface ReplyItemProps {
  reply: Reply
  currentUserId?: number
  onReplyDeleted: (replyId: number) => void
}

export default function ReplyItem({ reply, currentUserId, onReplyDeleted }: ReplyItemProps) {
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [showUserProfile, setShowUserProfile] = useState(false)

  const isAuthor = currentUserId === reply.author.id
  const timeAgo = formatRelativeTime(reply.createdAt)

  const handleDelete = async () => {
    if (!confirm('정말로 이 대댓글을 삭제하시겠습니까?')) {
      return
    }

    try {
      setLoading(true)
      setError('')

      const response = await fetch(`/api/replies/${reply.id}`, {
        method: 'DELETE',
      })

      if (!response.ok) {
        const errorData = await response.json()
        throw new Error(errorData.error || '대댓글 삭제 중 오류가 발생했습니다.')
      }

      onReplyDeleted(reply.id)
    } catch (err) {
      setError(err instanceof Error ? err.message : '알 수 없는 오류가 발생했습니다.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="bg-dark-card border border-dark-border rounded-lg p-3">
      {/* 대댓글 헤더 */}
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center space-x-3">
          <div className="flex items-center space-x-2">
            <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-primary-600 text-white">
              Lv.{reply.author.level}
            </span>
            <button
              onClick={() => setShowUserProfile(true)}
              className="font-semibold text-white text-sm hover:text-primary-400 transition-colors cursor-pointer"
            >
              {reply.author.username}
            </button>
          </div>
          <span className="text-xs text-gray-400">
            {timeAgo}
          </span>
        </div>
        
        {isAuthor && (
          <Button
            variant="ghost"
            size="sm"
            onClick={handleDelete}
            loading={loading}
            className="text-red-600 hover:text-red-700 hover:bg-red-100 px-2 py-1 text-xs"
          >
            삭제
          </Button>
        )}
      </div>

      {/* 대댓글 내용 */}
      <div className="mb-2">
        <p className="text-gray-300 text-sm whitespace-pre-wrap">{reply.content}</p>
      </div>

      {/* 에러 메시지 */}
      {error && (
        <div className="mt-2 p-2 bg-red-900/20 border border-red-500/30 rounded text-red-400 text-xs">
          {error}
        </div>
      )}

      {/* 사용자 프로필 모달 */}
      <UserProfileModal
        userId={reply.author.id}
        username={reply.author.username}
        isOpen={showUserProfile}
        onClose={() => setShowUserProfile(false)}
      />
    </div>
  )
}