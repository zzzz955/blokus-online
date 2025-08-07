'use client'

import React, { useState, useEffect } from 'react'
import { formatDateTime, formatRelativeTime } from '@/utils/format'
import Button from './Button'

interface UserProfileData {
  user_id: number
  username: string
  display_name?: string
  total_games: number
  wins: number
  losses: number
  draws: number
  win_rate: number
  best_score: number
  total_score: number
  average_score: number
  longest_win_streak: number
  current_win_streak: number
  level: number
  experience_points: number
  last_played?: string
  created_at?: string
  last_login_at?: string
  updated_at: string
  rank?: {
    by_wins: number
    by_score: number
  }
}

interface UserProfileModalProps {
  userId: number
  username: string
  isOpen: boolean
  onClose: () => void
}

export default function UserProfileModal({ userId, username, isOpen, onClose }: UserProfileModalProps) {
  const [userData, setUserData] = useState<UserProfileData | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (isOpen && userId) {
      fetchUserData()
    }
  }, [isOpen, userId])

  const fetchUserData = async () => {
    try {
      setLoading(true)
      setError('')

      const response = await fetch(`/api/stats/user/${userId}`)
      const result = await response.json()

      if (!response.ok || !result.success) {
        throw new Error(result.error || '사용자 정보를 불러오는데 실패했습니다.')
      }

      setUserData(result.data)
    } catch (err) {
      setError(err instanceof Error ? err.message : '알 수 없는 오류가 발생했습니다.')
    } finally {
      setLoading(false)
    }
  }

  const handleBackdropClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) {
      onClose()
    }
  }

  const handleKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'Escape') {
      onClose()
    }
  }

  useEffect(() => {
    if (isOpen) {
      document.addEventListener('keydown', handleKeyDown)
      document.body.style.overflow = 'hidden'
    }

    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = 'unset'
    }
  }, [isOpen])

  if (!isOpen) return null

  return (
    <div 
      className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4"
      onClick={handleBackdropClick}
    >
      <div className="bg-dark-card border border-dark-border rounded-lg shadow-xl max-w-md w-full max-h-[90vh] overflow-y-auto">
        {/* 모달 헤더 */}
        <div className="flex items-center justify-between p-6 border-b border-dark-border">
          <h2 className="text-xl font-semibold text-white">
            사용자 정보
          </h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-300 transition-colors"
          >
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* 모달 내용 */}
        <div className="p-6">
          {loading && (
            <div className="flex items-center justify-center py-8">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-600"></div>
              <span className="ml-3 text-gray-300">로딩중...</span>
            </div>
          )}

          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-4">
              <p className="text-red-700 text-sm">{error}</p>
              <Button
                onClick={fetchUserData}
                size="sm"
                className="mt-2"
              >
                다시 시도
              </Button>
            </div>
          )}

          {userData && !loading && (
            <div className="space-y-6">
              {/* 기본 정보 */}
              <div>
                <h3 className="text-lg font-medium text-white mb-3">기본 정보</h3>
                <div className="bg-dark-bg rounded-lg p-4 space-y-3">
                  <div className="flex justify-between">
                    <span className="text-gray-400">사용자명</span>
                    <span className="font-medium text-white">{userData.username}</span>
                  </div>
                  {userData.display_name && (
                    <div className="flex justify-between">
                      <span className="text-gray-400">표시명</span>
                      <span className="font-medium text-white">{userData.display_name}</span>
                    </div>
                  )}
                  <div className="flex justify-between">
                    <span className="text-gray-400">레벨</span>
                    <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-primary-600 text-white">
                      Lv.{userData.level}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">경험치</span>
                    <span className="font-medium text-white">{userData.experience_points.toLocaleString()} XP</span>
                  </div>
                </div>
              </div>

              {/* 게임 통계 */}
              <div>
                <h3 className="text-lg font-medium text-white mb-3">게임 통계</h3>
                <div className="bg-dark-bg rounded-lg p-4 space-y-3">
                  <div className="flex justify-between">
                    <span className="text-gray-400">총 게임 수</span>
                    <span className="font-medium text-white">{userData.total_games.toLocaleString()}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">승</span>
                    <span className="font-medium text-green-600">{userData.wins.toLocaleString()}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">패</span>
                    <span className="font-medium text-red-600">{userData.losses.toLocaleString()}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">무</span>
                    <span className="font-medium text-gray-400">{userData.draws.toLocaleString()}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">승률</span>
                    <span className="font-medium text-white">{userData.win_rate.toFixed(1)}%</span>
                  </div>
                </div>
              </div>

              {/* 점수 정보 */}
              <div>
                <h3 className="text-lg font-medium text-white mb-3">점수 정보</h3>
                <div className="bg-dark-bg rounded-lg p-4 space-y-3">
                  <div className="flex justify-between">
                    <span className="text-gray-400">최고 점수</span>
                    <span className="font-medium text-yellow-600">{userData.best_score.toLocaleString()}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">총 점수</span>
                    <span className="font-medium text-white">{userData.total_score.toLocaleString()}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">평균 점수</span>
                    <span className="font-medium text-white">{userData.average_score.toFixed(1)}</span>
                  </div>
                </div>
              </div>

              {/* 연승 정보 */}
              <div>
                <h3 className="text-lg font-medium text-white mb-3">연승 정보</h3>
                <div className="bg-dark-bg rounded-lg p-4 space-y-3">
                  <div className="flex justify-between">
                    <span className="text-gray-400">최대 연승</span>
                    <span className="font-medium text-white">{userData.longest_win_streak.toLocaleString()}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">현재 연승</span>
                    <span className="font-medium text-white">{userData.current_win_streak.toLocaleString()}</span>
                  </div>
                </div>
              </div>

              {/* 랭킹 정보 */}
              {userData.rank && (
                <div>
                  <h3 className="text-lg font-medium text-white mb-3">랭킹</h3>
                  <div className="bg-dark-bg rounded-lg p-4 space-y-3">
                    <div className="flex justify-between">
                      <span className="text-gray-400">승수 랭킹</span>
                      <span className="font-medium text-white">{userData.rank.by_wins}위</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-gray-400">점수 랭킹</span>
                      <span className="font-medium text-white">{userData.rank.by_score}위</span>
                    </div>
                  </div>
                </div>
              )}

              {/* 활동 정보 */}
              <div>
                <h3 className="text-lg font-medium text-white mb-3">활동 정보</h3>
                <div className="bg-dark-bg rounded-lg p-4 space-y-3">
                  {userData.created_at && (
                    <div className="flex justify-between">
                      <span className="text-gray-400">가입일</span>
                      <span className="font-medium text-sm text-white">{formatDateTime(userData.created_at)}</span>
                    </div>
                  )}
                  {userData.last_login_at && (
                    <div className="flex justify-between">
                      <span className="text-gray-400">최근 로그인</span>
                      <span className="font-medium text-sm text-white">{formatRelativeTime(userData.last_login_at)}</span>
                    </div>
                  )}
                  {userData.last_played && (
                    <div className="flex justify-between">
                      <span className="text-gray-400">최근 게임</span>
                      <span className="font-medium text-sm text-white">{formatRelativeTime(userData.last_played)}</span>
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}
        </div>

        {/* 모달 푸터 */}
        <div className="flex justify-end p-6 border-t border-dark-border">
          <Button
            variant="secondary"
            onClick={onClose}
          >
            닫기
          </Button>
        </div>
      </div>
    </div>
  )
}