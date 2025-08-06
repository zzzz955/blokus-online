// OAuth 후 ID/PW 설정 완료 API
import { NextRequest, NextResponse } from 'next/server'
import { getServerSession } from 'next-auth'
import { authOptions, completeOAuthRegistration, checkUsernameAvailable } from '@/lib/auth'

export async function POST(request: NextRequest) {
  try {
    const session = await getServerSession(authOptions)
    
    if (!session?.user?.needs_username) {
      return NextResponse.json(
        { error: 'OAuth 인증이 필요합니다.' },
        { status: 401 }
      )
    }
    
    const { username, password, display_name } = await request.json()
    
    // 입력값 검증
    if (!username || !password) {
      return NextResponse.json(
        { error: '사용자명과 비밀번호를 입력해주세요.' },
        { status: 400 }
      )
    }
    
    // 사용자명 형식 검증
    if (!/^[a-zA-Z0-9_]{4,20}$/.test(username)) {
      return NextResponse.json(
        { error: '사용자명은 4-20자의 영문, 숫자, 언더스코어만 사용 가능합니다.' },
        { status: 400 }
      )
    }
    
    // 사용자명 중복 확인
    const isAvailable = await checkUsernameAvailable(username)
    if (!isAvailable) {
      return NextResponse.json(
        { error: '이미 사용 중인 사용자명입니다.' },
        { status: 409 }
      )
    }
    
    // 계정 생성 완료
    const user = await completeOAuthRegistration(
      session.user.email!,
      session.user.oauth_provider!,
      username,
      password,
      display_name
    )
    
    return NextResponse.json({
      success: true,
      message: '회원가입이 완료되었습니다. 게임에 로그인할 수 있습니다.',
      user: {
        user_id: user.user_id,
        username: user.username,
        display_name: user.display_name
      }
    })
    
  } catch (error) {
    console.error('회원가입 완료 오류:', error)
    return NextResponse.json(
      { error: '회원가입 처리 중 오류가 발생했습니다.' },
      { status: 500 }
    )
  }
}