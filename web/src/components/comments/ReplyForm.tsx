'use client'

import React, { useState } from 'react'
import Button from '@/components/ui/Button'

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

interface ReplyFormProps {
  commentId: number
  onReplyAdded: (reply: Reply) => void
  onCancel: () => void
}

export default function ReplyForm({ commentId, onReplyAdded, onCancel }: ReplyFormProps) {
  const [content, setContent] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    
    if (!content.trim()) {
      setError('대댓글 내용을 입력해주세요.')
      return
    }

    if (content.length > 1000) {
      setError('대댓글은 1000자 이내로 작성해주세요.')
      return
    }

    try {
      setLoading(true)
      setError('')

      const response = await fetch(`/api/comments/${commentId}/replies`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          content: content.trim(),
        }),
      })

      if (!response.ok) {
        const errorData = await response.json()
        throw new Error(errorData.error || '대댓글 작성 중 오류가 발생했습니다.')
      }

      const newReply = await response.json()
      onReplyAdded(newReply)
      setContent('')
    } catch (err) {
      setError(err instanceof Error ? err.message : '알 수 없는 오류가 발생했습니다.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="bg-gray-50 p-4 rounded-lg">
      <div className="mb-3">
        <textarea
          value={content}
          onChange={(e) => setContent(e.target.value)}
          placeholder="대댓글을 작성해주세요..."
          className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500 resize-none text-sm"
          rows={2}
          maxLength={1000}
          disabled={loading}
        />
        <div className="flex justify-between items-center mt-1">
          <span className="text-xs text-gray-500">
            {content.length}/1000
          </span>
        </div>
      </div>

      {error && (
        <div className="mb-3 p-2 bg-red-50 border border-red-200 rounded text-red-700 text-sm">
          {error}
        </div>
      )}

      <div className="flex justify-end space-x-2">
        <Button 
          type="button" 
          variant="ghost" 
          size="sm"
          onClick={onCancel}
          disabled={loading}
        >
          취소
        </Button>
        <Button 
          type="submit" 
          loading={loading}
          disabled={!content.trim() || loading}
          size="sm"
        >
          대댓글 작성
        </Button>
      </div>
    </form>
  )
}