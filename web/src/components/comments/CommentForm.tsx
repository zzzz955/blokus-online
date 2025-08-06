'use client'

import React, { useState } from 'react'
import Button from '@/components/ui/Button'

interface Comment {
  id: number
  content: string
  author: {
    id: number
    username: string
    level: number
  }
  createdAt: string
  replies: any[]
}

interface CommentFormProps {
  postId?: number
  announcementId?: number
  patchNoteId?: number
  onCommentAdded: (comment: Comment) => void
}

export default function CommentForm({ postId, announcementId, patchNoteId, onCommentAdded }: CommentFormProps) {
  const [content, setContent] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    
    if (!content.trim()) {
      setError('댓글 내용을 입력해주세요.')
      return
    }

    if (content.length > 1000) {
      setError('댓글은 1000자 이내로 작성해주세요.')
      return
    }

    try {
      setLoading(true)
      setError('')

      const response = await fetch('/api/comments', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          content: content.trim(),
          postId,
          announcementId,
          patchNoteId,
        }),
      })

      if (!response.ok) {
        const errorData = await response.json()
        throw new Error(errorData.error || '댓글 작성 중 오류가 발생했습니다.')
      }

      const newComment = await response.json()
      onCommentAdded(newComment)
      setContent('')
    } catch (err) {
      setError(err instanceof Error ? err.message : '알 수 없는 오류가 발생했습니다.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="mb-6">
      <div className="mb-3">
        <textarea
          value={content}
          onChange={(e) => setContent(e.target.value)}
          placeholder="댓글을 작성해주세요..."
          className="w-full px-4 py-3 border border-dark-border bg-white text-gray-900 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500 resize-none placeholder-gray-500"
          rows={3}
          maxLength={1000}
          disabled={loading}
        />
        <div className="flex justify-between items-center mt-2">
          <span className="text-sm text-gray-400">
            {content.length}/1000
          </span>
        </div>
      </div>

      {error && (
        <div className="mb-3 p-3 bg-red-900/20 border border-red-500/30 rounded-lg text-red-400 text-sm">
          {error}
        </div>
      )}

      <div className="flex justify-end">
        <Button 
          type="submit" 
          loading={loading}
          disabled={!content.trim() || loading}
          size="sm"
        >
          댓글 작성
        </Button>
      </div>
    </form>
  )
}