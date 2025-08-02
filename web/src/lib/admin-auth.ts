import { NextRequest } from 'next/server';
import jwt from 'jsonwebtoken';
import bcrypt from 'bcryptjs';
import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

export interface AdminSession {
  id: number;
  username: string;
  role: 'ADMIN' | 'SUPER_ADMIN';
}

export interface AdminLoginRequest {
  username: string;
  password: string;
}

export interface AdminLoginResponse {
  success: boolean;
  token?: string;
  admin?: AdminSession;
  error?: string;
}

/**
 * 관리자 로그인 처리
 */
export async function authenticateAdmin(credentials: AdminLoginRequest): Promise<AdminLoginResponse> {
  try {
    const { username, password } = credentials;

    // 관리자 사용자 조회
    const adminUser = await prisma.adminUser.findUnique({
      where: { username }
    });

    if (!adminUser) {
      return {
        success: false,
        error: '존재하지 않는 관리자 계정입니다.'
      };
    }

    // 비밀번호 검증
    const isPasswordValid = await bcrypt.compare(password, adminUser.password_hash);
    if (!isPasswordValid) {
      console.log(adminUser.password_hash);
      return {
        success: false,
        error: '비밀번호가 일치하지 않습니다.'
      };
    }

    // JWT 토큰 생성
    const adminSession: AdminSession = {
      id: adminUser.id,
      username: adminUser.username,
      role: adminUser.role
    };

    const token = jwt.sign(
      adminSession,
      process.env.JWT_SECRET || 'fallback-secret',
      { expiresIn: '24h' }
    );

    return {
      success: true,
      token,
      admin: adminSession
    };

  } catch (error) {
    console.error('관리자 인증 오류:', error);
    return {
      success: false,
      error: '서버 오류가 발생했습니다.'
    };
  }
}

/**
 * JWT 토큰에서 관리자 세션 추출
 */
export function verifyAdminToken(token: string): AdminSession | null {
  try {
    const decoded = jwt.verify(token, process.env.JWT_SECRET || 'fallback-secret') as AdminSession;
    return decoded;
  } catch (error) {
    console.error('토큰 검증 실패:', error);
    return null;
  }
}

/**
 * 요청에서 관리자 인증 정보 추출
 */
export function getAdminFromRequest(request: NextRequest): AdminSession | null {
  try {
    const authHeader = request.headers.get('authorization');

    if (!authHeader || !authHeader.startsWith('Bearer ')) {
      return null;
    }

    const token = authHeader.substring(7); // "Bearer " 제거
    return verifyAdminToken(token);
  } catch (error) {
    console.error('요청에서 관리자 정보 추출 실패:', error);
    return null;
  }
}

/**
 * 관리자 권한 검증 미들웨어
 */
export function requireAdmin(adminRole?: 'ADMIN' | 'SUPER_ADMIN') {
  return (admin: AdminSession | null): boolean => {
    if (!admin) {
      return false;
    }

    if (adminRole && admin.role !== adminRole && admin.role !== 'SUPER_ADMIN') {
      return false;
    }

    return true;
  };
}

/**
 * 비밀번호 해싱
 */
export async function hashPassword(password: string): Promise<string> {
  return await bcrypt.hash(password, 12);
}

/**
 * 관리자 계정 생성 (초기 설정용)
 */
export async function createAdminUser(
  username: string,
  password: string,
  role: 'ADMIN' | 'SUPER_ADMIN' = 'ADMIN'
): Promise<{ success: boolean; error?: string }> {
  try {
    // 중복 체크
    const existingAdmin = await prisma.adminUser.findUnique({
      where: { username }
    });

    if (existingAdmin) {
      return {
        success: false,
        error: '이미 존재하는 관리자 계정입니다.'
      };
    }

    // 비밀번호 해싱
    const password_hash = await hashPassword(password);

    // 관리자 계정 생성
    await prisma.adminUser.create({
      data: {
        username,
        password_hash,
        role
      }
    });

    return { success: true };

  } catch (error) {
    console.error('관리자 계정 생성 오류:', error);
    return {
      success: false,
      error: '서버 오류가 발생했습니다.'
    };
  }
}