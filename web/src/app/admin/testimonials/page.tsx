'use client';

import { useEffect, useState } from 'react';
import { adminFetch } from '@/lib/admin-auth';
import { Star, Pin, Eye, EyeOff, Trash2, RotateCcw } from 'lucide-react';

interface Testimonial {
  id: number;
  name: string;
  rating: number;
  comment?: string;
  createdAt: string;
  isPinned: boolean;
  isPublished: boolean;
}

export default function AdminTestimonialsPage() {
  const [testimonials, setTestimonials] = useState<Testimonial[]>([]);
  const [loading, setLoading] = useState(true);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  useEffect(() => {
    fetchTestimonials();
  }, [currentPage]);

  const fetchTestimonials = async () => {
    try {
      setLoading(true);
      const response = await adminFetch(`/api/admin/testimonials?page=${currentPage}&limit=20`);
      const data = await response.json();

      if (data.success) {
        setTestimonials(data.data);
        setTotalPages(data.pagination.totalPages);
        setError('');
      } else {
        setError(data.error || '후기를 불러오는데 실패했습니다.');
      }
    } catch (error) {
      console.error('Error fetching testimonials:', error);
      setError('후기를 불러오는데 실패했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const updateTestimonial = async (id: number, updates: { isPinned?: boolean; isPublished?: boolean }) => {
    try {
      const response = await adminFetch(`/api/admin/testimonials/${id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(updates),
      });

      const data = await response.json();

      if (data.success) {
        setTestimonials(prev =>
          prev.map(testimonial =>
            testimonial.id === id ? { ...testimonial, ...updates } : testimonial
          )
        );
        setSuccess('후기가 성공적으로 업데이트되었습니다.');
        setTimeout(() => setSuccess(''), 3000);
      } else {
        setError(data.error || '업데이트에 실패했습니다.');
      }
    } catch (error) {
      console.error('Error updating testimonial:', error);
      setError('업데이트에 실패했습니다.');
    }
  };

  const deleteTestimonial = async (id: number) => {
    if (!confirm('정말로 이 후기를 삭제하시겠습니까?')) {
      return;
    }

    try {
      const response = await adminFetch(`/api/admin/testimonials/${id}`, {
        method: 'DELETE',
      });

      const data = await response.json();

      if (data.success) {
        setTestimonials(prev => prev.filter(testimonial => testimonial.id !== id));
        setSuccess('후기가 성공적으로 삭제되었습니다.');
        setTimeout(() => setSuccess(''), 3000);
      } else {
        setError(data.error || '삭제에 실패했습니다.');
      }
    } catch (error) {
      console.error('Error deleting testimonial:', error);
      setError('삭제에 실패했습니다.');
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString('ko-KR');
  };

  if (loading) {
    return (
      <div className="px-4 py-8">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">로딩 중...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 sm:px-6 lg:px-8">
      <div className="sm:flex sm:items-center">
        <div className="sm:flex-auto">
          <h1 className="text-2xl font-semibold text-gray-900">후기 관리</h1>
          <p className="mt-2 text-sm text-gray-700">
            사용자들이 작성한 후기를 관리할 수 있습니다.
          </p>
        </div>
      </div>

      {/* 알림 메시지 */}
      {error && (
        <div className="mt-4 bg-red-50 border border-red-200 rounded-md p-4">
          <div className="flex">
            <div className="ml-3">
              <p className="text-sm text-red-800">{error}</p>
            </div>
            <button
              onClick={() => setError('')}
              className="ml-auto text-red-500 hover:text-red-700"
            >
              ×
            </button>
          </div>
        </div>
      )}

      {success && (
        <div className="mt-4 bg-green-50 border border-green-200 rounded-md p-4">
          <div className="flex">
            <div className="ml-3">
              <p className="text-sm text-green-800">{success}</p>
            </div>
            <button
              onClick={() => setSuccess('')}
              className="ml-auto text-green-500 hover:text-green-700"
            >
              ×
            </button>
          </div>
        </div>
      )}

      {/* 후기 목록 */}
      <div className="mt-8 flex flex-col">
        <div className="-my-2 -mx-4 overflow-x-auto sm:-mx-6 lg:-mx-8">
          <div className="inline-block min-w-full py-2 align-middle md:px-6 lg:px-8">
            <div className="overflow-hidden shadow ring-1 ring-black ring-opacity-5 md:rounded-lg">
              <table className="min-w-full divide-y divide-gray-300">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      작성자
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      별점
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      후기 내용
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      작성일
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      상태
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      관리
                    </th>
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                  {testimonials.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-6 py-4 text-center text-gray-500">
                        등록된 후기가 없습니다.
                      </td>
                    </tr>
                  ) : (
                    testimonials.map((testimonial) => (
                      <tr key={testimonial.id} className={!testimonial.isPublished ? 'bg-gray-50' : ''}>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="flex items-center">
                            <div>
                              <div className="text-sm font-medium text-gray-900">
                                {testimonial.name}
                              </div>
                              <div className="text-sm text-gray-500">
                                ID: {testimonial.id}
                              </div>
                            </div>
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="flex items-center">
                            {[...Array(5)].map((_, i) => (
                              <Star
                                key={i}
                                size={16}
                                className={`${
                                  i < testimonial.rating
                                    ? 'text-yellow-400 fill-current'
                                    : 'text-gray-300'
                                }`}
                              />
                            ))}
                            <span className="ml-2 text-sm text-gray-600">
                              {testimonial.rating}점
                            </span>
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          <div className="text-sm text-gray-900 max-w-xs truncate">
                            {testimonial.comment || '(내용 없음)'}
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                          {formatDate(testimonial.createdAt)}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="flex space-x-2">
                            {testimonial.isPinned && (
                              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800">
                                고정
                              </span>
                            )}
                            <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                              testimonial.isPublished
                                ? 'bg-green-100 text-green-800'
                                : 'bg-red-100 text-red-800'
                            }`}>
                              {testimonial.isPublished ? '발행됨' : '숨김'}
                            </span>
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm font-medium">
                          <div className="flex space-x-2">
                            {/* 고정 토글 */}
                            <button
                              onClick={() => updateTestimonial(testimonial.id, { isPinned: !testimonial.isPinned })}
                              className={`p-2 rounded-md ${
                                testimonial.isPinned
                                  ? 'text-blue-600 hover:bg-blue-50'
                                  : 'text-gray-400 hover:text-blue-600 hover:bg-blue-50'
                              }`}
                              title={testimonial.isPinned ? '고정 해제' : '고정'}
                            >
                              <Pin size={16} />
                            </button>

                            {/* 발행 토글 */}
                            <button
                              onClick={() => updateTestimonial(testimonial.id, { isPublished: !testimonial.isPublished })}
                              className={`p-2 rounded-md ${
                                testimonial.isPublished
                                  ? 'text-green-600 hover:bg-green-50'
                                  : 'text-red-600 hover:bg-red-50'
                              }`}
                              title={testimonial.isPublished ? '숨김' : '발행'}
                            >
                              {testimonial.isPublished ? <Eye size={16} /> : <EyeOff size={16} />}
                            </button>

                            {/* 삭제 */}
                            <button
                              onClick={() => deleteTestimonial(testimonial.id)}
                              className="p-2 rounded-md text-red-600 hover:bg-red-50"
                              title="삭제"
                            >
                              <Trash2 size={16} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      {/* 페이지네이션 */}
      {totalPages > 1 && (
        <div className="mt-6 flex items-center justify-between">
          <div className="flex-1 flex justify-between sm:hidden">
            <button
              onClick={() => setCurrentPage(prev => Math.max(prev - 1, 1))}
              disabled={currentPage === 1}
              className="relative inline-flex items-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 disabled:bg-gray-100 disabled:text-gray-400"
            >
              이전
            </button>
            <button
              onClick={() => setCurrentPage(prev => Math.min(prev + 1, totalPages))}
              disabled={currentPage === totalPages}
              className="ml-3 relative inline-flex items-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 disabled:bg-gray-100 disabled:text-gray-400"
            >
              다음
            </button>
          </div>
          <div className="hidden sm:flex-1 sm:flex sm:items-center sm:justify-between">
            <div>
              <p className="text-sm text-gray-700">
                <span className="font-medium">{currentPage}</span> / <span className="font-medium">{totalPages}</span> 페이지
              </p>
            </div>
            <div>
              <nav className="relative z-0 inline-flex rounded-md shadow-sm -space-x-px">
                <button
                  onClick={() => setCurrentPage(prev => Math.max(prev - 1, 1))}
                  disabled={currentPage === 1}
                  className="relative inline-flex items-center px-2 py-2 rounded-l-md border border-gray-300 bg-white text-sm font-medium text-gray-500 hover:bg-gray-50 disabled:bg-gray-100 disabled:text-gray-400"
                >
                  이전
                </button>
                <button
                  onClick={() => setCurrentPage(prev => Math.min(prev + 1, totalPages))}
                  disabled={currentPage === totalPages}
                  className="relative inline-flex items-center px-2 py-2 rounded-r-md border border-gray-300 bg-white text-sm font-medium text-gray-500 hover:bg-gray-50 disabled:bg-gray-100 disabled:text-gray-400"
                >
                  다음
                </button>
              </nav>
            </div>
          </div>
        </div>
      )}

      {/* 새로고침 버튼 */}
      <div className="mt-6 flex justify-end">
        <button
          onClick={fetchTestimonials}
          className="inline-flex items-center px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50"
        >
          <RotateCcw size={16} className="mr-2" />
          새로고침
        </button>
      </div>
    </div>
  );
}