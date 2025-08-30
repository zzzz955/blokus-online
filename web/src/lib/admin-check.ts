import { getServerSession } from 'next-auth';
import { authOptions } from '@/lib/auth';
import { prisma } from '@/lib/prisma';

/**
 * 현재 세션 사용자가 관리자인지 확인하는 함수
 * 사용자의 username으로 AdminUser 테이블을 조회합니다.
 */
export async function checkIsAdmin(sessionUserId?: string): Promise<boolean> {
  try {
    if (!sessionUserId) {
      const session = await getServerSession(authOptions);
      if (!session?.user?.id) return false;
      sessionUserId = session.user.id;
    }

    // 사용자 정보 조회
    const user = await prisma.user.findUnique({
      where: { user_id: parseInt(sessionUserId) },
      select: { username: true },
    });

    if (!user) return false;

    // 관리자 테이블에 해당 사용자명이 있는지 확인
    const adminUser = await prisma.adminUser.findUnique({
      where: { username: user.username },
    });

    return !!adminUser;
  } catch (error) {
    console.error('관리자 권한 확인 오류:', error);
    return false;
  }
}

/**
 * 관리자 권한이 필요한 API에서 사용할 미들웨어
 */
export async function requireAdminPermission(): Promise<{ isAdmin: boolean; userId?: string; error?: string }> {
  try {
    const session = await getServerSession(authOptions);
    
    if (!session?.user?.id) {
      return { isAdmin: false, error: '로그인이 필요합니다.' };
    }

    const isAdmin = await checkIsAdmin(session.user.id);
    
    if (!isAdmin) {
      return { isAdmin: false, userId: session.user.id, error: '관리자 권한이 필요합니다.' };
    }

    return { isAdmin: true, userId: session.user.id };
  } catch (error) {
    console.error('관리자 권한 확인 오류:', error);
    return { isAdmin: false, error: '권한 확인 중 오류가 발생했습니다.' };
  }
}
