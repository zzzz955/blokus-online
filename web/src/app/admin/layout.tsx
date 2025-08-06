'use client';

import { useEffect, useState } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import Layout from '@/components/layout/Layout';

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

  // 로딩 중
  if (loading) {
    return (
      <div className="min-h-screen bg-dark-bg text-white flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500 mx-auto"></div>
          <p className="mt-4 text-gray-300">로딩 중...</p>
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

  // 메인 Layout 사용하되 admin 정보를 context로 전달
  return (
    <Layout>
      <div className="admin-layout" data-admin={JSON.stringify(admin)}>
        {children}
      </div>
    </Layout>
  );
}