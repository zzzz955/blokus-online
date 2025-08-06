import * as argon2 from 'argon2';
import jwt from 'jsonwebtoken';
import { NextAuthOptions } from 'next-auth';
import GoogleProvider from 'next-auth/providers/google';
import { prisma } from './prisma';

const JWT_SECRET = process.env.JWT_SECRET || 'fallback-secret';

export interface JWTPayload {
  userId: number;
  username: string;
  role: string;
}

// NextAuth 세션 타입 확장
declare module "next-auth" {
  interface Session {
    user: {
      id: string
      email?: string | null
      name?: string | null
      image?: string | null
      oauth_provider?: string | null
      needs_username?: boolean // OAuth 후 ID/PW 설정 필요 여부
      needs_reactivation?: boolean // 계정 복구 필요 여부
      deactivated_account?: {
        username: string
        display_name?: string
        member_since: Date
      }
    }
  }
  
  interface User {
    id: string
    email?: string | null
    name?: string | null
    image?: string | null
    oauth_provider?: string | null
    needs_username?: boolean
    needs_reactivation?: boolean
    deactivated_account?: {
      username: string
      display_name?: string
      member_since: Date
    }
  }
}

export async function hashPassword(password: string): Promise<string> {
  // Argon2id 해싱 (게임 서버와 동일한 파라미터)
  return argon2.hash(password, {
    type: argon2.argon2id,
    memoryCost: 2 ** 16, // 64MB
    timeCost: 2,         // 2 iterations
    parallelism: 1,      // 1 thread
  });
}

export async function verifyPassword(password: string, hashedPassword: string): Promise<boolean> {
  try {
    return await argon2.verify(hashedPassword, password);
  } catch (error) {
    console.error('비밀번호 검증 오류:', error);
    return false;
  }
}

export function generateToken(payload: JWTPayload): string {
  return jwt.sign(payload, JWT_SECRET, { expiresIn: '7d' });
}

export function verifyToken(token: string): JWTPayload | null {
  try {
    return jwt.verify(token, JWT_SECRET) as JWTPayload;
  } catch {
    return null;
  }
}

export async function authenticateAdmin(username: string, password: string) {
  try {
    const admin = await prisma.adminUser.findUnique({
      where: { username },
    });

    if (!admin) {
      return null;
    }

    const isValid = await verifyPassword(password, admin.password_hash);
    if (!isValid) {
      return null;
    }

    return {
      id: admin.id,
      username: admin.username,
      role: admin.role,
    };
  } catch (error) {
    console.error('Authentication error:', error);
    return null;
  }
}

export async function createAdminUser(username: string, password: string, role: 'ADMIN' | 'SUPER_ADMIN' = 'ADMIN') {
  const hashedPassword = await hashPassword(password);
  
  return prisma.adminUser.create({
    data: {
      username,
      password_hash: hashedPassword,
      role,
    },
  });
}

// ========================================
// NextAuth OAuth 설정
// ========================================

export const authOptions: NextAuthOptions = {
  providers: [
    GoogleProvider({
      clientId: process.env.GOOGLE_CLIENT_ID!,
      clientSecret: process.env.GOOGLE_CLIENT_SECRET!,
    }),
    // 추후 Kakao, Naver 등 추가 가능
  ],
  
  callbacks: {
    async signIn({ user, account, profile }) {
      if (!account || !user.email) return false
      
      try {
        // OAuth 사용자 확인 (활성 계정)
        const existingActiveUser = await prisma.user.findFirst({
          where: {
            AND: [
              { oauth_provider: account.provider },
              { email: user.email },
              { is_active: true }
            ]
          }
        })
        
        if (existingActiveUser) {
          // 기존 활성 사용자 - 로그인 시간 업데이트
          await prisma.user.update({
            where: { user_id: existingActiveUser.user_id },
            data: { last_login_at: new Date() }
          })
          
          user.id = existingActiveUser.user_id.toString()
          user.oauth_provider = existingActiveUser.oauth_provider
          user.needs_username = false
          user.needs_reactivation = false
        } else {
          // 비활성 계정 확인
          const existingDeactivatedUser = await prisma.user.findFirst({
            where: {
              AND: [
                { oauth_provider: account.provider },
                { email: user.email },
                { is_active: false }
              ]
            },
            select: {
              user_id: true,
              username: true,
              display_name: true,
              created_at: true,
              oauth_provider: true
            }
          })
          
          if (existingDeactivatedUser) {
            // 비활성 계정 발견 - 복구 확인 필요
            user.id = 'deactivated-' + existingDeactivatedUser.user_id.toString()
            user.oauth_provider = existingDeactivatedUser.oauth_provider
            user.needs_username = false
            user.needs_reactivation = true
            user.deactivated_account = {
              username: existingDeactivatedUser.username,
              display_name: existingDeactivatedUser.display_name || undefined,
              member_since: existingDeactivatedUser.created_at
            }
          } else {
            // 새 OAuth 사용자 - 임시 세션 생성 (ID/PW 입력 대기)
            user.oauth_provider = account.provider
            user.needs_username = true
            user.needs_reactivation = false
          }
        }
        
        return true
      } catch (error) {
        console.error('OAuth 로그인 오류:', error)
        return false
      }
    },
    
    async jwt({ token, user, account }) {
      if (user) {
        token.oauth_provider = user.oauth_provider
        token.needs_username = user.needs_username
        token.needs_reactivation = user.needs_reactivation
        token.deactivated_account = user.deactivated_account
      }
      return token
    },
    
    async session({ session, token }) {
      if (session.user) {
        session.user.id = token.sub!
        session.user.oauth_provider = token.oauth_provider as string
        session.user.needs_username = token.needs_username as boolean
        session.user.needs_reactivation = token.needs_reactivation as boolean
        session.user.deactivated_account = token.deactivated_account as {
          username: string
          display_name?: string
          member_since: Date
        }
      }
      return session
    }
  },
  
  pages: {
    signIn: '/auth/signin',
    error: '/auth/error',
  },
  
  session: {
    strategy: 'jwt',
    maxAge: 24 * 60 * 60, // 24시간
  },
  
  secret: process.env.NEXTAUTH_SECRET,
}

// ========================================
// 사용자 관리 함수들
// ========================================

// OAuth 후 ID/PW 설정하여 계정 완성
export async function completeOAuthRegistration(
  email: string,
  oauth_provider: string,
  username: string,
  password: string,
  display_name?: string
) {
  try {
    const hashedPassword = await hashPassword(password)
    
    // 새 사용자 생성
    const user = await prisma.user.create({
      data: {
        username,
        password_hash: hashedPassword,
        email,
        oauth_provider,
        oauth_id: email, // OAuth ID로 email 사용
        display_name: display_name || username,
        is_active: true,
        created_at: new Date(),
        last_login_at: new Date(),
      }
    })
    
    // 초기 게임 통계 생성
    await prisma.userStats.create({
      data: {
        user_id: user.user_id
      }
    })
    
    return user
  } catch (error) {
    console.error('OAuth 회원가입 완료 오류:', error)
    throw error
  }
}

// 사용자명 중복 체크
export async function checkUsernameAvailable(username: string): Promise<boolean> {
  const existingUser = await prisma.user.findUnique({
    where: { username }
  })
  return !existingUser
}

// OAuth 기반 비밀번호 재설정을 위한 계정 찾기
export async function findUserByOAuth(email: string, oauth_provider: string) {
  return prisma.user.findFirst({
    where: {
      AND: [
        { email },
        { oauth_provider }
      ]
    }
  })
}

// 비밀번호 재설정
export async function resetUserPassword(user_id: number, newPassword: string) {
  const hashedPassword = await hashPassword(newPassword)
  
  return prisma.user.update({
    where: { user_id },
    data: { 
      password_hash: hashedPassword,
      updated_at: new Date()
    }
  })
}