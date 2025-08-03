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
}

export default function AdminDashboard() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchDashboardStats();
  }, []);

  const fetchDashboardStats = async () => {
    try {
      // 실제로는 대시보드 통계 API를 만들어야 하지만, 
      // 지금은 간단한 더미 데이터로 구성
      setStats({
        announcements: {
          total: 0,
          published: 0,
          pinned: 0
        },
        patchNotes: {
          total: 0,
          latest: '없음'
        },
        supportTickets: {
          pending: 0,
          answered: 0,
          total: 0
        }
      });
    } catch (error) {
      console.error('대시보드 통계 조회 오류:', error);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  return (
    <div className="px-4 py-6 sm:px-0">
      <div className="border-4 border-dashed border-gray-200 rounded-lg p-6">
        <h1 className="text-2xl font-bold text-gray-900 mb-6">관리자 대시보드</h1>
        
        {/* 통계 카드들 */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-8">
          {/* 공지사항 통계 */}
          <div className="bg-white overflow-hidden shadow rounded-lg">
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
                    <dt className="text-sm font-medium text-gray-500 truncate">
                      공지사항
                    </dt>
                    <dd className="text-lg font-medium text-gray-900">
                      총 {stats?.announcements.total || 0}개
                    </dd>
                    <dd className="text-sm text-gray-500">
                      게시: {stats?.announcements.published || 0}개 | 고정: {stats?.announcements.pinned || 0}개
                    </dd>
                  </dl>
                </div>
              </div>
            </div>
            <div className="bg-gray-50 px-5 py-3">
              <div className="text-sm">
                <Link href="/admin/announcements" className="font-medium text-blue-700 hover:text-blue-900">
                  관리하기 →
                </Link>
              </div>
            </div>
          </div>

          {/* 패치노트 통계 */}
          <div className="bg-white overflow-hidden shadow rounded-lg">
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
                    <dt className="text-sm font-medium text-gray-500 truncate">
                      패치노트
                    </dt>
                    <dd className="text-lg font-medium text-gray-900">
                      총 {stats?.patchNotes.total || 0}개
                    </dd>
                    <dd className="text-sm text-gray-500">
                      최신: {stats?.patchNotes.latest || '없음'}
                    </dd>
                  </dl>
                </div>
              </div>
            </div>
            <div className="bg-gray-50 px-5 py-3">
              <div className="text-sm">
                <Link href="/admin/patch-notes" className="font-medium text-green-700 hover:text-green-900">
                  관리하기 →
                </Link>
              </div>
            </div>
          </div>

          {/* 지원 티켓 통계 */}
          <div className="bg-white overflow-hidden shadow rounded-lg">
            <div className="p-5">
              <div className="flex items-center">
                <div className="flex-shrink-0">
                  <div className="w-8 h-8 bg-yellow-500 rounded-md flex items-center justify-center">
                    <svg className="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M18.364 5.636l-3.536 3.536m0 5.656l3.536 3.536M9.172 9.172L5.636 5.636m3.536 9.192L5.636 18.364M12 12h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                  </div>
                </div>
                <div className="ml-5 w-0 flex-1">
                  <dl>
                    <dt className="text-sm font-medium text-gray-500 truncate">
                      지원 티켓
                    </dt>
                    <dd className="text-lg font-medium text-gray-900">
                      총 {stats?.supportTickets?.total || 0}개
                    </dd>
                    <dd className="text-sm text-gray-500">
                      대기: {stats?.supportTickets?.pending || 0}개 | 답변완료: {stats?.supportTickets?.answered || 0}개
                    </dd>
                  </dl>
                </div>
              </div>
            </div>
            <div className="bg-gray-50 px-5 py-3">
              <div className="text-sm">
                <span className="font-medium text-yellow-700">
                  곧 지원 예정
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* 빠른 작업 */}
        <div className="bg-white shadow rounded-lg">
          <div className="px-4 py-5 sm:p-6">
            <h3 className="text-lg leading-6 font-medium text-gray-900 mb-4">
              빠른 작업
            </h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              <Link 
                href="/admin/announcements?action=create"
                className="bg-blue-50 hover:bg-blue-100 p-4 rounded-lg border border-blue-200 transition-colors"
              >
                <div className="text-blue-600 font-medium">새 공지사항</div>
                <div className="text-blue-500 text-sm mt-1">공지사항 작성하기</div>
              </Link>
              
              <Link 
                href="/admin/patch-notes?action=create"
                className="bg-green-50 hover:bg-green-100 p-4 rounded-lg border border-green-200 transition-colors"
              >
                <div className="text-green-600 font-medium">새 패치노트</div>
                <div className="text-green-500 text-sm mt-1">패치노트 작성하기</div>
              </Link>
              
              <Link 
                href="/admin/announcements"
                className="bg-gray-50 hover:bg-gray-100 p-4 rounded-lg border border-gray-200 transition-colors"
              >
                <div className="text-gray-600 font-medium">공지사항 관리</div>
                <div className="text-gray-500 text-sm mt-1">수정/삭제</div>
              </Link>
              
              <Link 
                href="/admin/patch-notes"
                className="bg-gray-50 hover:bg-gray-100 p-4 rounded-lg border border-gray-200 transition-colors"
              >
                <div className="text-gray-600 font-medium">패치노트 관리</div>
                <div className="text-gray-500 text-sm mt-1">수정/삭제</div>
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}