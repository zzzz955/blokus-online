// OAuth 기반 비밀번호 재설정 API
import { NextRequest, NextResponse } from 'next/server'
import { getServerSession } from 'next-auth'
import { authOptions, findUserByOAuth, resetUserPassword } from '@/lib/auth'

export async function POST(request: NextRequest) {
  try {
    const session = await getServerSession(authOptions)
    
    if (!session?.user?.email || !session?.user?.oauth_provider) {
      return NextResponse.json(
        { error: 'OAuth 인증이 필요합니다.' },
        { status: 401 }
      )
    }
    
    const { username, newPassword } = await request.json()
    
    if (!username || !newPassword) {
      return NextResponse.json(
        { error: '사용자명과 새 비밀번호를 입력해주세요.' },
        { status: 400 }
      )
    }
    
    // OAuth 정보로 기존 계정 확인
    const existingUser = await findUserByOAuth(
      session.user.email,
      session.user.oauth_provider
    )
    
    if (!existingUser) {
      return NextResponse.json(
        { error: '해당 이메일로 가입된 계정을 찾을 수 없습니다.' },
        { status: 404 }
      )
    }
    
    // 입력한 사용자명과 일치하는지 확인
    if (existingUser.username !== username) {
      return NextResponse.json(
        { error: '사용자명이 일치하지 않습니다.' },
        { status: 400 }
      )
    }
    
    // 비밀번호 재설정
    await resetUserPassword(existingUser.user_id, newPassword)
    
    return NextResponse.json({
      success: true,
      message: '비밀번호가 성공적으로 재설정되었습니다. 게임에 로그인할 수 있습니다.',
      username: existingUser.username
    })
    
  } catch (error) {
    console.error('비밀번호 재설정 오류:', error)
    return NextResponse.json(
      { error: '비밀번호 재설정 중 오류가 발생했습니다.' },
      { status: 500 }
    )
  }
}