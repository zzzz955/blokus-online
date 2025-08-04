import bcrypt from 'bcryptjs';
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
    }
  }
  
  interface User {
    id: string
    email?: string | null
    name?: string | null
    image?: string | null
    oauth_provider?: string | null
    needs_username?: boolean
  }
}

export async function hashPassword(password: string): Promise<string> {
  return bcrypt.hash(password, 12);
}

export async function verifyPassword(password: string, hashedPassword: string): Promise<boolean> {
  return bcrypt.compare(password, hashedPassword);
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
        // OAuth 사용자 확인
        const existingUser = await prisma.user.findFirst({
          where: {
            AND: [
              { oauth_provider: account.provider },
              { email: user.email }
            ]
          }
        })
        
        if (existingUser) {
          // 기존 사용자 - 로그인 시간 업데이트
          await prisma.user.update({
            where: { user_id: existingUser.user_id },
            data: { last_login_at: new Date() }
          })
          
          user.id = existingUser.user_id.toString()
          user.oauth_provider = existingUser.oauth_provider
          user.needs_username = false // 이미 계정 완성됨
        } else {
          // 새 OAuth 사용자 - 임시 세션 생성 (ID/PW 입력 대기)
          user.oauth_provider = account.provider
          user.needs_username = true // ID/PW 설정 필요
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
      }
      return token
    },
    
    async session({ session, token }) {
      if (session.user) {
        session.user.id = token.sub!
        session.user.oauth_provider = token.oauth_provider as string
        session.user.needs_username = token.needs_username as boolean
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