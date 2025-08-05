'use client'

import { useState, useEffect } from 'react'
import { useSession, signOut } from 'next-auth/react'
import { useRouter } from 'next/navigation'
import { useForm } from 'react-hook-form'
import Button from '@/components/ui/Button'
import Card from '@/components/ui/Card'
import Layout from '@/components/layout/Layout'

interface ResetPasswordForm {
  username: string
  newPassword: string
  confirmPassword: string
}

export default function ResetPasswordPage() {
  const { data: session, status } = useSession()
  const router = useRouter()
  const [isSubmitting, setIsSubmitting] = useState(false)

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
    setError
  } = useForm<ResetPasswordForm>()

  const watchedNewPassword = watch('newPassword')

  useEffect(() => {
    if (status === 'loading') return

    if (!session?.user) {
      router.push('/auth/signin')
      return
    }

    if (session.user.needs_username) {
      router.push('/auth/complete-registration') // 신규 가입자는 회원가입으로
    }
  }, [session, status, router])

  const onSubmit = async (data: ResetPasswordForm) => {
    if (data.newPassword !== data.confirmPassword) {
      setError('confirmPassword', { message: '비밀번호가 일치하지 않습니다.' })
      return
    }

    setIsSubmitting(true)

    try {
      const response = await fetch('/api/auth/reset-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          username: data.username,
          newPassword: data.newPassword
        })
      })

      const result = await response.json()

      if (response.ok) {
        alert(`비밀번호가 재설정되었습니다!\n게임 ID: ${result.username}\n새 비밀번호로 게임에 로그인할 수 있습니다.`)
        await signOut({ callbackUrl: '/' })
      } else {
        setError('root', { message: result.error })
      }
    } catch (error) {
      setError('root', { message: '비밀번호 재설정 중 오류가 발생했습니다.' })
    } finally {
      setIsSubmitting(false)
    }
  }

  if (status === 'loading') {
    return (
      <Layout>
        <div className="flex items-center justify-center py-20">
          <div className="text-white">로딩 중...</div>
        </div>
      </Layout>
    )
  }

  if (!session?.user || session.user.needs_username) {
    return null
  }

  return (
    <Layout>
      <div className="flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
        <div className="max-w-md w-full space-y-8">
          <div>
            <h2 className="mt-6 text-center text-3xl font-extrabold text-white">
              비밀번호 재설정
            </h2>
            <p className="mt-2 text-center text-sm text-gray-300">
              {session.user.email}로 인증되었습니다<br/>
              게임 ID를 입력하고 새 비밀번호를 설정해주세요
            </p>
          </div>

        <Card className="p-8">
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
            {errors.root && (
              <div className="text-red-400 text-sm text-center">
                {errors.root.message}
              </div>
            )}

            {/* 게임 ID */}
            <div>
              <label className="block text-sm font-medium text-white">
                게임 ID (사용자명) *
              </label>
              <input
                {...register('username', {
                  required: '게임 ID를 입력해주세요',
                  pattern: {
                    value: /^[a-zA-Z0-9_]{4,20}$/,
                    message: '올바른 게임 ID를 입력해주세요'
                  }
                })}
                className="mt-1 block w-full border border-dark-border bg-white text-gray-900 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary-500"
                placeholder="현재 게임에서 사용 중인 ID"
              />
              {errors.username && (
                <p className="text-sm text-red-400 mt-1">{errors.username.message}</p>
              )}
              <p className="text-sm text-gray-400 mt-1">
                이 이메일로 가입한 게임 ID를 정확히 입력해주세요
              </p>
            </div>

            {/* 새 비밀번호 */}
            <div>
              <label className="block text-sm font-medium text-white">
                새 비밀번호 *
              </label>
              <input
                type="password"
                {...register('newPassword', {
                  required: '새 비밀번호를 입력해주세요',
                  minLength: {
                    value: 6,
                    message: '비밀번호는 최소 6자 이상이어야 합니다'
                  }
                })}
                className="mt-1 block w-full border border-dark-border bg-white text-gray-900 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary-500"
                placeholder="새로운 게임 로그인 비밀번호"
              />
              {errors.newPassword && (
                <p className="text-sm text-red-400 mt-1">{errors.newPassword.message}</p>
              )}
            </div>

            {/* 비밀번호 확인 */}
            <div>
              <label className="block text-sm font-medium text-white">
                새 비밀번호 확인 *
              </label>
              <input
                type="password"
                {...register('confirmPassword', {
                  required: '비밀번호를 다시 입력해주세요',
                  validate: (value) => 
                    value === watchedNewPassword || '비밀번호가 일치하지 않습니다'
                })}
                className="mt-1 block w-full border border-dark-border bg-white text-gray-900 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary-500"
                placeholder="새 비밀번호 재입력"
              />
              {errors.confirmPassword && (
                <p className="text-sm text-red-400 mt-1">{errors.confirmPassword.message}</p>
              )}
            </div>

            <Button
              type="submit"
              disabled={isSubmitting}
              className="w-full"
            >
              {isSubmitting ? '비밀번호 재설정 중...' : '비밀번호 재설정'}
            </Button>
          </form>

          <div className="mt-4 text-center">
            <button
              onClick={() => signOut({ callbackUrl: '/auth/signin' })}
              className="text-sm text-gray-400 hover:text-gray-300"
            >
              다른 계정으로 로그인
            </button>
          </div>
        </Card>
      </div>
    </Layout>
  )
}