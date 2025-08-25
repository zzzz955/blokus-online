'use client';

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