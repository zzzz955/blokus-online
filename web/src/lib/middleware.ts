import { NextRequest } from 'next/server';
import { verifyToken } from './auth';

export async function verifyAdminToken(request: NextRequest) {
  try {
    const token = request.cookies.get('admin-token')?.value;
    
    if (!token) {
      return null;
    }

    const payload = verifyToken(token);
    return payload;
  } catch (error) {
    console.error('Token verification error:', error);
    return null;
  }
}

export function createAuthMiddleware() {
  return async function authMiddleware(request: NextRequest) {
    const admin = await verifyAdminToken(request);
    
    if (!admin) {
      return Response.json(
        { success: false, error: '인증이 필요합니다.' },
        { status: 401 }
      );
    }

    // request에 admin 정보 추가
    (request as any).admin = admin;
    return null; // 인증 성공
  };
}