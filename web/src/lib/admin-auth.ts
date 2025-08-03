import { NextRequest } from 'next/server';
import jwt from 'jsonwebtoken';
import bcrypt from 'bcryptjs';
import { PrismaClient } from '@prisma/client';

const prisma = new PrismaClient();

export interface AdminSession {
  id: number;
  username: string;
  role: 'ADMIN' | 'SUPER_ADMIN';
  iat?: number;
  exp?: number;
}

export interface AdminLoginRequest {
  username: string;
  password: string;
}

export interface AdminLoginResponse {
  success: boolean;
  token?: string;
  refreshToken?: string;
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

    const accessToken = jwt.sign(
      adminSession,
      process.env.JWT_SECRET || 'fallback-secret',
      { expiresIn: '15m' } // Access token은 15분으로 단축
    );

    const refreshToken = jwt.sign(
      { id: adminSession.id, username: adminSession.username },
      process.env.JWT_REFRESH_SECRET || 'fallback-refresh-secret',
      { expiresIn: '7d' } // Refresh token은 7일
    );

    return {
      success: true,
      token: accessToken,
      refreshToken,
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
 * Refresh Token 검증
 */
export function verifyRefreshToken(refreshToken: string): { id: number; username: string } | null {
  try {
    const decoded = jwt.verify(refreshToken, process.env.JWT_REFRESH_SECRET || 'fallback-refresh-secret') as { id: number; username: string };
    return decoded;
  } catch (error) {
    console.error('Refresh 토큰 검증 실패:', error);
    return null;
  }
}

/**
 * 새로운 Access Token 생성
 */
export async function generateNewAccessToken(refreshTokenPayload: { id: number; username: string }): Promise<AdminLoginResponse> {
  try {
    // 관리자 사용자 조회 (refresh token이 유효한지 재확인)
    const adminUser = await prisma.adminUser.findUnique({
      where: { id: refreshTokenPayload.id }
    });

    if (!adminUser) {
      return {
        success: false,
        error: '존재하지 않는 관리자 계정입니다.'
      };
    }

    const adminSession: AdminSession = {
      id: adminUser.id,
      username: adminUser.username,
      role: adminUser.role
    };

    const newAccessToken = jwt.sign(
      adminSession,
      process.env.JWT_SECRET || 'fallback-secret',
      { expiresIn: '15m' }
    );

    return {
      success: true,
      token: newAccessToken,
      admin: adminSession
    };

  } catch (error) {
    console.error('새 토큰 생성 오류:', error);
    return {
      success: false,
      error: '토큰 갱신에 실패했습니다.'
    };
  }
}

/**
 * 요청에서 관리자 인증 정보 추출
 */
export function getAdminFromRequest(request: NextRequest): AdminSession | null {
  try {
    let token: string | null = null;

    // 1. Authorization 헤더에서 토큰 확인
    const authHeader = request.headers.get('authorization');
    if (authHeader && authHeader.startsWith('Bearer ')) {
      token = authHeader.substring(7); // "Bearer " 제거
    }

    // 2. Authorization 헤더가 없으면 쿠키에서 토큰 확인
    if (!token) {
      token = request.cookies.get('admin-token')?.value || null;
    }

    if (!token) {
      return null;
    }

    return verifyAdminToken(token);
  } catch (error) {
    console.error('요청에서 관리자 정보 추출 실패:', error);
    return null;
  }
}

/**
 * 토큰 만료 체크 및 자동 갱신 (클라이언트 사이드용)
 */
export async function refreshTokenIfNeeded(): Promise<boolean> {
  try {
    const response = await fetch('/api/admin/auth/refresh', {
      method: 'POST',
      credentials: 'include'
    });

    const data = await response.json();
    
    if (data.success) {
      // 관리자 정보를 로컬스토리지에 업데이트
      if (typeof window !== 'undefined') {
        localStorage.setItem('admin', JSON.stringify(data.data.user));
      }
      return true;
    } else {
      // Refresh 실패 시 로그아웃 처리
      if (typeof window !== 'undefined') {
        localStorage.removeItem('admin');
        window.location.href = '/admin/login';
      }
      return false;
    }
  } catch (error) {
    console.error('토큰 갱신 실패:', error);
    return false;
  }
}

/**
 * API 요청 시 자동 토큰 갱신을 포함한 fetch wrapper
 */
export async function adminFetch(url: string, options: RequestInit = {}): Promise<Response> {
  // 첫 번째 시도
  let response = await fetch(url, {
    ...options,
    credentials: 'include'
  });

  // 401 에러 (Unauthorized)인 경우 토큰 갱신 시도
  if (response.status === 401) {
    const refreshed = await refreshTokenIfNeeded();
    
    if (refreshed) {
      // 토큰이 갱신되었으면 다시 시도
      response = await fetch(url, {
        ...options,
        credentials: 'include'
      });
    }
  }

  return response;
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