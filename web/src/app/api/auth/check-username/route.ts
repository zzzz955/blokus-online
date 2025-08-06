// 사용자명 중복 확인 API
import { NextRequest, NextResponse } from 'next/server'
import { checkUsernameAvailable } from '@/lib/auth'

export async function POST(request: NextRequest) {
  try {
    const { username } = await request.json()
    
    if (!username) {
      return NextResponse.json(
        { error: '사용자명을 입력해주세요.' },
        { status: 400 }
      )
    }
    
    // 사용자명 형식 검증
    if (!/^[a-zA-Z0-9_]{4,20}$/.test(username)) {
      return NextResponse.json({
        available: false,
        error: '사용자명은 4-20자의 영문, 숫자, 언더스코어만 사용 가능합니다.'
      })
    }
    
    const isAvailable = await checkUsernameAvailable(username)
    
    return NextResponse.json({
      available: isAvailable,
      message: isAvailable ? '사용 가능한 사용자명입니다.' : '이미 사용 중인 사용자명입니다.'
    })
    
  } catch (error) {
    console.error('사용자명 확인 오류:', error)
    return NextResponse.json(
      { error: '사용자명 확인 중 오류가 발생했습니다.' },
      { status: 500 }
    )
  }
}