'use client';

import { useEffect, useState } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import Link from 'next/link';

interface AdminUser {
  id: number;
  username: string;
  role: 'ADMIN' | 'SUPER_ADMIN';
}

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const [admin, setAdmin] = useState<AdminUser | null>(null);
  const [loading, setLoading] = useState(true);
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    // 로그인 페이지는 인증 체크 건너뛰기
    if (pathname === '/admin/login') {
      setLoading(false);
      return;
    }

    // 로컬 스토리지에서 관리자 정보 확인
    const adminData = localStorage.getItem('admin');
    if (adminData) {
      try {
        const parsedAdmin = JSON.parse(adminData);
        setAdmin(parsedAdmin);
      } catch (error) {
        console.error('관리자 정보 파싱 오류:', error);
        router.push('/admin/login');
      }
    } else {
      router.push('/admin/login');
    }
    
    setLoading(false);
  }, [pathname, router]);

  const handleLogout = async () => {
    try {
      await fetch('/api/admin/auth', {
        method: 'DELETE'
      });
      
      localStorage.removeItem('admin');
      router.push('/admin/login');
    } catch (error) {
      console.error('로그아웃 오류:', error);
      // 오류가 발생해도 로그아웃 처리
      localStorage.removeItem('admin');
      router.push('/admin/login');
    }
  };

  // 로딩 중
  if (loading) {
    return (
      <div className="min-h-screen bg-gray-100 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">로딩 중...</p>
        </div>
      </div>
    );
  }

  // 로그인 페이지는 레이아웃 없이 렌더링
  if (pathname === '/admin/login') {
    return <>{children}</>;
  }

  // 인증되지 않은 경우 (이미 리다이렉트 되었지만 안전장치)
  if (!admin) {
    return null;
  }

  return (
    <div className="min-h-screen bg-gray-100">
      {/* 상단 네비게이션 */}
      <nav className="bg-white shadow-sm border-b border-gray-200">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between h-16">
            <div className="flex">
              <div className="flex-shrink-0 flex items-center">
                <h1 className="text-xl font-bold text-gray-900">
                  Blokus 관리자
                </h1>
              </div>
              
              <div className="hidden sm:ml-6 sm:flex sm:space-x-8">
                <Link
                  href="/admin/dashboard"
                  className={`inline-flex items-center px-1 pt-1 text-sm font-medium border-b-2 ${
                    pathname === '/admin/dashboard'
                      ? 'border-blue-500 text-gray-900'
                      : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                  }`}
                >
                  대시보드
                </Link>
                
                <Link
                  href="/admin/announcements"
                  className={`inline-flex items-center px-1 pt-1 text-sm font-medium border-b-2 ${
                    pathname.startsWith('/admin/announcements')
                      ? 'border-blue-500 text-gray-900'
                      : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                  }`}
                >
                  공지사항
                </Link>
                
                <Link
                  href="/admin/patch-notes"
                  className={`inline-flex items-center px-1 pt-1 text-sm font-medium border-b-2 ${
                    pathname.startsWith('/admin/patch-notes')
                      ? 'border-blue-500 text-gray-900'
                      : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                  }`}
                >
                  패치노트
                </Link>
                
                <Link
                  href="/admin/testimonials"
                  className={`inline-flex items-center px-1 pt-1 text-sm font-medium border-b-2 ${
                    pathname.startsWith('/admin/testimonials')
                      ? 'border-blue-500 text-gray-900'
                      : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                  }`}
                >
                  후기 관리
                </Link>
              </div>
            </div>
            
            <div className="flex items-center">
              <div className="flex-shrink-0">
                <span className="text-sm text-gray-700">
                  {admin.username} ({admin.role})
                </span>
              </div>
              <div className="ml-4 flex-shrink-0">
                <button
                  onClick={handleLogout}
                  className="bg-white p-2 rounded-md text-gray-400 hover:text-gray-500 hover:bg-gray-100 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
                >
                  로그아웃
                </button>
              </div>
            </div>
          </div>
        </div>
      </nav>

      {/* 메인 컨텐츠 */}
      <main className="max-w-7xl mx-auto py-6 sm:px-6 lg:px-8">
        {children}
      </main>
    </div>
  );
}