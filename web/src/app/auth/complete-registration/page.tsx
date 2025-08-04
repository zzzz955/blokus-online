'use client'

import { useState, useEffect } from 'react'
import { useSession, signOut } from 'next-auth/react'
import { useRouter } from 'next/navigation'
import { useForm } from 'react-hook-form'
import Button from '@/components/ui/Button'
import Card from '@/components/ui/Card'

interface RegistrationForm {
  username: string
  password: string
  confirmPassword: string
  display_name?: string
}

export default function CompleteRegistrationPage() {
  const { data: session, status } = useSession()
  const router = useRouter()
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [usernameCheck, setUsernameCheck] = useState<{
    checking: boolean
    available: boolean | null
    message: string
  }>({ checking: false, available: null, message: '' })

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
    setError,
    clearErrors
  } = useForm<RegistrationForm>()

  const watchedUsername = watch('username')
  const watchedPassword = watch('password')

  useEffect(() => {
    if (status === 'loading') return

    if (!session?.user) {
      router.push('/auth/signin')
      return
    }

    if (!session.user.needs_username) {
      router.push('/') // 이미 완료된 계정
    }
  }, [session, status, router])

  // 사용자명 중복 확인
  useEffect(() => {
    if (!watchedUsername || watchedUsername.length < 4) {
      setUsernameCheck({ checking: false, available: null, message: '' })
      return
    }

    const timeoutId = setTimeout(async () => {
      setUsernameCheck(prev => ({ ...prev, checking: true }))
      
      try {
        const response = await fetch('/api/auth/check-username', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ username: watchedUsername })
        })
        
        const data = await response.json()
        
        setUsernameCheck({
          checking: false,
          available: data.available,
          message: data.message || data.error
        })
        
        if (!data.available) {
          setError('username', { message: data.error || '사용할 수 없는 사용자명입니다.' })
        } else {
          clearErrors('username')
        }
      } catch (error) {
        setUsernameCheck({
          checking: false,
          available: false,
          message: '사용자명 확인 중 오류가 발생했습니다.'
        })
      }
    }, 500)

    return () => clearTimeout(timeoutId)
  }, [watchedUsername, setError, clearErrors])

  const onSubmit = async (data: RegistrationForm) => {
    if (data.password !== data.confirmPassword) {
      setError('confirmPassword', { message: '비밀번호가 일치하지 않습니다.' })
      return
    }

    if (!usernameCheck.available) {
      setError('username', { message: '사용 가능한 사용자명을 입력해주세요.' })
      return
    }

    setIsSubmitting(true)

    try {
      const response = await fetch('/api/auth/complete-registration', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          username: data.username,
          password: data.password,
          display_name: data.display_name || data.username
        })
      })

      const result = await response.json()

      if (response.ok) {
        alert('회원가입이 완료되었습니다! 게임에 로그인할 수 있습니다.')
        await signOut({ callbackUrl: '/' })
      } else {
        setError('root', { message: result.error })
      }
    } catch (error) {
      setError('root', { message: '회원가입 중 오류가 발생했습니다.' })
    } finally {
      setIsSubmitting(false)
    }
  }

  if (status === 'loading') {
    return <div className="min-h-screen flex items-center justify-center">로딩 중...</div>
  }

  if (!session?.user?.needs_username) {
    return null
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        <div>
          <h2 className="mt-6 text-center text-3xl font-extrabold text-gray-900">
            게임 계정 설정
          </h2>
          <p className="mt-2 text-center text-sm text-gray-600">
            {session.user.email}로 로그인했습니다<br/>
            게임에 사용할 ID와 비밀번호를 설정해주세요
          </p>
        </div>

        <Card className="p-8">
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
            {errors.root && (
              <div className="text-red-600 text-sm text-center">
                {errors.root.message}
              </div>
            )}

            {/* 사용자명 */}
            <div>
              <label className="block text-sm font-medium text-gray-700">
                게임 ID (사용자명) *
              </label>
              <input
                {...register('username', {
                  required: '사용자명을 입력해주세요',
                  pattern: {
                    value: /^[a-zA-Z0-9_]{4,20}$/,
                    message: '4-20자의 영문, 숫자, 언더스코어만 사용 가능합니다'
                  }
                })}
                className="mt-1 block w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="게임에서 사용할 ID"
              />
              {usernameCheck.checking && (
                <p className="text-sm text-gray-500 mt-1">확인 중...</p>
              )}
              {usernameCheck.available === true && (
                <p className="text-sm text-green-600 mt-1">✓ {usernameCheck.message}</p>
              )}
              {(errors.username || usernameCheck.available === false) && (
                <p className="text-sm text-red-600 mt-1">
                  {errors.username?.message || usernameCheck.message}
                </p>
              )}
            </div>

            {/* 비밀번호 */}
            <div>
              <label className="block text-sm font-medium text-gray-700">
                비밀번호 *
              </label>
              <input
                type="password"
                {...register('password', {
                  required: '비밀번호를 입력해주세요',
                  minLength: {
                    value: 6,
                    message: '비밀번호는 최소 6자 이상이어야 합니다'
                  }
                })}
                className="mt-1 block w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="게임 로그인 비밀번호"
              />
              {errors.password && (
                <p className="text-sm text-red-600 mt-1">{errors.password.message}</p>
              )}
            </div>

            {/* 비밀번호 확인 */}
            <div>
              <label className="block text-sm font-medium text-gray-700">
                비밀번호 확인 *
              </label>
              <input
                type="password"
                {...register('confirmPassword', {
                  required: '비밀번호를 다시 입력해주세요',
                  validate: (value) => 
                    value === watchedPassword || '비밀번호가 일치하지 않습니다'
                })}
                className="mt-1 block w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="비밀번호 재입력"
              />
              {errors.confirmPassword && (
                <p className="text-sm text-red-600 mt-1">{errors.confirmPassword.message}</p>
              )}
            </div>

            {/* 표시명 (선택사항) */}
            <div>
              <label className="block text-sm font-medium text-gray-700">
                표시명 (선택사항)
              </label>
              <input
                {...register('display_name')}
                className="mt-1 block w-full border border-gray-300 rounded-md px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="웹사이트에 표시될 이름 (미입력시 ID 사용)"
              />
            </div>

            <Button
              type="submit"
              disabled={isSubmitting || !usernameCheck.available}
              className="w-full"
            >
              {isSubmitting ? '계정 생성 중...' : '계정 생성 완료'}
            </Button>
          </form>

          <div className="mt-4 text-center">
            <button
              onClick={() => signOut({ callbackUrl: '/auth/signin' })}
              className="text-sm text-gray-500 hover:text-gray-700"
            >
              다른 계정으로 로그인
            </button>
          </div>
        </Card>
      </div>
    </div>
  )
}