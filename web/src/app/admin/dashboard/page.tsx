'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';

interface DashboardStats {
  announcements: {
    total: number;
    published: number;
    pinned: number;
  };
  patchNotes: {
    total: number;
    latest: string;
  };
  supportTickets?: {
    pending: number;
    answered: number;
    total: number;
  };
  testimonials: {
    total: number;
    today: number;
  };
  posts: {
    total: number;
    today: number;
    hidden: number;
    deleted: number;
  };
  comments: {
    today: number;
  };
}

export default function AdminDashboard() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchDashboardStats();
  }, []);

  const fetchDashboardStats = async () => {
    try {
      const response = await fetch('/api/admin/dashboard');
      if (response.ok) {
        const result = await response.json();
        if (result.success) {
          setStats(result.data);
        } else {
          console.error('대시보드 통계 조회 실패:', result.error);
        }
      } else {
        console.error('대시보드 통계 API 호출 실패:', response.status);
      }
    } catch (error) {
      console.error('대시보드 통계 조회 오류:', error);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500"></div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 sm:px-6 lg:px-8">
      <div className="border-4 border-dashed border-dark-border rounded-lg p-6 bg-dark-card">
        <h1 className="text-2xl font-bold text-white mb-6">관리자 대시보드</h1>
        
        {/* 통계 카드들 */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          {/* 공지사항 통계 */}
          <div className="bg-dark-card border border-dark-border overflow-hidden shadow rounded-lg">
            <div className="p-5">
              <div className="flex items-center">
                <div className="flex-shrink-0">
                  <div className="w-8 h-8 bg-blue-500 rounded-md flex items-center justify-center">
                    <svg className="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5.882V19.24a1.76 1.76 0 01-3.417.592l-2.147-6.15M18 13a3 3 0 100-6M5.436 13.683A4.001 4.001 0 017 6h1.832c4.1 0 7.625-1.234 9.168-3v14c-1.543-1.766-5.067-3-9.168-3H7a3.988 3.988 0 01-1.564-.317z" />
                    </svg>
                  </div>
                </div>
                <div className="ml-5 w-0 flex-1">
                  <dl>
                    <dt className="text-sm font-medium text-gray-400 truncate">
                      공지사항
                    </dt>
                    <dd className="text-lg font-medium text-white">
                      총 {stats?.announcements.total || 0}개
                    </dd>
                    <dd className="text-sm text-gray-400">
                      게시: {stats?.announcements.published || 0}개 | 고정: {stats?.announcements.pinned || 0}개
                    </dd>
                  </dl>
                </div>
              </div>
            </div>
            <div className="bg-dark-bg px-5 py-3">
              <div className="text-sm">
                <Link href="/admin/announcements" className="font-medium text-blue-400 hover:text-blue-300">
                  관리하기 →
                </Link>
              </div>
            </div>
          </div>

          {/* 패치노트 통계 */}
          <div className="bg-dark-card border border-dark-border overflow-hidden shadow rounded-lg">
            <div className="p-5">
              <div className="flex items-center">
                <div className="flex-shrink-0">
                  <div className="w-8 h-8 bg-green-500 rounded-md flex items-center justify-center">
                    <svg className="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                    </svg>
                  </div>
                </div>
                <div className="ml-5 w-0 flex-1">
                  <dl>
                    <dt className="text-sm font-medium text-gray-400 truncate">
                      패치노트
                    </dt>
                    <dd className="text-lg font-medium text-white">
                      총 {stats?.patchNotes.total || 0}개
                    </dd>
                    <dd className="text-sm text-gray-400">
                      최신: {stats?.patchNotes.latest || '없음'}
                    </dd>
                  </dl>
                </div>
              </div>
            </div>
            <div className="bg-dark-bg px-5 py-3">
              <div className="text-sm">
                <Link href="/admin/patch-notes" className="font-medium text-green-400 hover:text-green-300">
                  관리하기 →
                </Link>
              </div>
            </div>
          </div>

          {/* 후기 통계 */}
          <div className="bg-dark-card border border-dark-border overflow-hidden shadow rounded-lg">
            <div className="p-5">
              <div className="flex items-center">
                <div className="flex-shrink-0">
                  <div className="w-8 h-8 bg-purple-500 rounded-md flex items-center justify-center">
                    <svg className="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11.049 2.927c.3-.921 1.603-.921 1.902 0l1.519 4.674a1 1 0 00.95.69h4.915c.969 0 1.371 1.24.588 1.81l-3.976 2.888a1 1 0 00-.363 1.118l1.518 4.674c.3.922-.755 1.688-1.538 1.118l-3.976-2.888a1 1 0 00-1.176 0l-3.976 2.888c-.783.57-1.838-.197-1.538-1.118l1.518-4.674a1 1 0 00-.363-1.118l-3.976-2.888c-.784-.57-.38-1.81.588-1.81h4.914a1 1 0 00.951-.69l1.519-4.674z" />
                    </svg>
                  </div>
                </div>
                <div className="ml-5 w-0 flex-1">
                  <dl>
                    <dt className="text-sm font-medium text-gray-400 truncate">
                      후기
                    </dt>
                    <dd className="text-lg font-medium text-white">
                      총 {stats?.testimonials?.total || 0}개
                    </dd>
                    <dd className="text-sm text-gray-400">
                      오늘: {stats?.testimonials?.today || 0}개
                    </dd>
                  </dl>
                </div>
              </div>
            </div>
            <div className="bg-dark-bg px-5 py-3">
              <div className="text-sm">
                <Link href="/admin/testimonials" className="font-medium text-purple-400 hover:text-purple-300">
                  관리하기 →
                </Link>
              </div>
            </div>
          </div>

          {/* 게시판 통계 */}
          <div className="bg-dark-card border border-dark-border overflow-hidden shadow rounded-lg">
            <div className="p-5">
              <div className="flex items-center">
                <div className="flex-shrink-0">
                  <div className="w-8 h-8 bg-orange-500 rounded-md flex items-center justify-center">
                    <svg className="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 20H5a2 2 0 01-2-2V6a2 2 0 012-2h10a2 2 0 012 2v1m2 13a2 2 0 01-2-2V7m2 13a2 2 0 002-2V9a2 2 0 00-2-2h-2m-4-3H9M7 16h6M7 8h6v4H7V8z" />
                    </svg>
                  </div>
                </div>
                <div className="ml-5 w-0 flex-1">
                  <dl>
                    <dt className="text-sm font-medium text-gray-400 truncate">
                      게시판
                    </dt>
                    <dd className="text-lg font-medium text-white">
                      총 {stats?.posts?.total || 0}개
                    </dd>
                    <dd className="text-sm text-gray-400">
                      오늘: {stats?.posts?.today || 0}개 | 댓글: {stats?.comments?.today || 0}개
                    </dd>
                  </dl>
                </div>
              </div>
            </div>
            <div className="bg-dark-bg px-5 py-3">
              <div className="text-sm">
                <Link href="/admin/posts" className="font-medium text-orange-400 hover:text-orange-300">
                  관리하기 →
                </Link>
              </div>
            </div>
          </div>
        </div>

        {/* 빠른 작업 */}
        <div className="bg-dark-card border border-dark-border shadow rounded-lg">
          <div className="px-4 py-5 sm:p-6">
            <h3 className="text-lg leading-6 font-medium text-white mb-4">
              빠른 작업
            </h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              <Link 
                href="/admin/announcements?action=create"
                className="bg-blue-500/10 hover:bg-blue-500/20 p-4 rounded-lg border border-blue-500/30 transition-colors"
              >
                <div className="text-blue-400 font-medium">새 공지사항</div>
                <div className="text-blue-300 text-sm mt-1">공지사항 작성하기</div>
              </Link>
              
              <Link 
                href="/admin/patch-notes?action=create"
                className="bg-green-500/10 hover:bg-green-500/20 p-4 rounded-lg border border-green-500/30 transition-colors"
              >
                <div className="text-green-400 font-medium">새 패치노트</div>
                <div className="text-green-300 text-sm mt-1">패치노트 작성하기</div>
              </Link>
              
              <Link 
                href="/admin/testimonials"
                className="bg-purple-500/10 hover:bg-purple-500/20 p-4 rounded-lg border border-purple-500/30 transition-colors"
              >
                <div className="text-purple-400 font-medium">후기 관리</div>
                <div className="text-purple-300 text-sm mt-1">승인/거절/삭제</div>
              </Link>
              
              <Link 
                href="/admin/posts"
                className="bg-orange-500/10 hover:bg-orange-500/20 p-4 rounded-lg border border-orange-500/30 transition-colors"
              >
                <div className="text-orange-400 font-medium">게시글 관리</div>
                <div className="text-orange-300 text-sm mt-1">숨김/삭제 관리</div>
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}