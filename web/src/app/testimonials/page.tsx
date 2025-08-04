'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import Layout from '@/components/layout/Layout';
import Card, { CardContent } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import TestimonialModal from '@/components/testimonials/TestimonialModal';
import { Star, MessageSquarePlus, Loader2, Home } from 'lucide-react';
import { formatDate } from '@/utils/format';
import { api } from '@/utils/api';
import { Testimonial, PaginatedResponse } from '@/types';

export default function TestimonialsPage() {
  const [testimonials, setTestimonials] = useState<Testimonial[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [isModalOpen, setIsModalOpen] = useState(false);

  const fetchTestimonials = async (page: number = 1) => {
    try {
      setLoading(true);
      const response = await api.getFull(`/api/testimonials?page=${page}&limit=20`) as PaginatedResponse<Testimonial>;
      setTestimonials(response.data || []);
      setTotalPages(response.pagination?.totalPages || 1);
      setCurrentPage(page);
    } catch (error: any) {
      setError(error.message || '후기를 불러오는데 실패했습니다.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTestimonials();
  }, []);

  const handlePageChange = (page: number) => {
    fetchTestimonials(page);
  };

  const handleModalSuccess = () => {
    fetchTestimonials(currentPage); // 현재 페이지 새로고침
  };

  if (loading && testimonials.length === 0) {
    return (
      <Layout>
        <div className="py-12">
          <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex items-center justify-center min-h-96">
              <div className="text-center">
                <Loader2 className="w-8 h-8 animate-spin text-primary-400 mx-auto mb-4" />
                <p className="text-gray-400">후기를 불러오는 중...</p>
              </div>
            </div>
          </div>
        </div>
      </Layout>
    );
  }

  if (error) {
    return (
      <Layout>
        <div className="py-12">
          <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="text-center">
              <h1 className="text-4xl font-bold text-white mb-6">플레이어 후기</h1>
              <Card className="bg-red-900/20 border-red-500/30">
                <CardContent className="text-center py-12">
                  <p className="text-red-400 mb-4">{error}</p>
                  <Button onClick={() => fetchTestimonials()} variant="outline">
                    다시 시도
                  </Button>
                </CardContent>
              </Card>
            </div>
          </div>
        </div>
      </Layout>
    );
  }

  return (
    <Layout>
      <div className="py-12">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
          {/* Header */}
          <div className="text-center mb-12">
            <h1 className="text-4xl md:text-5xl font-bold text-white mb-6">
              플레이어 후기
            </h1>
            <p className="text-xl text-gray-400 mb-8">
              블로커스 온라인을 즐기고 있는 플레이어들의 생생한 후기를 모두 확인해보세요.
            </p>
            <div className="flex justify-center space-x-4">
              <Link href="/">
                <Button variant="outline" className="flex items-center space-x-2">
                  <Home className="w-4 h-4" />
                  <span>홈으로</span>
                </Button>
              </Link>
              <Button
                onClick={() => setIsModalOpen(true)}
                className="flex items-center space-x-2"
              >
                <MessageSquarePlus className="w-4 h-4" />
                <span>후기 작성하기</span>
              </Button>
            </div>
          </div>

          {/* 후기 목록 */}
          {testimonials.length === 0 ? (
            <Card>
              <CardContent className="text-center py-12">
                <p className="text-gray-400 text-lg mb-4">등록된 후기가 없습니다.</p>
                <p className="text-gray-500 mb-6">첫 번째 후기를 작성해보세요!</p>

              </CardContent>
            </Card>
          ) : (
            <div className="space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
                {testimonials.map((testimonial) => (
                  <Card key={testimonial.id} hover className="h-full">
                    <CardContent className="flex flex-col h-full">
                      <div className="flex items-center justify-between mb-4">
                        <div className="flex items-center">
                          {[...Array(5)].map((_, i) => (
                            <Star
                              key={i}
                              size={16}
                              className={`${
                                i < testimonial.rating
                                  ? 'text-yellow-400 fill-current'
                                  : 'text-gray-600'
                              }`}
                            />
                          ))}
                        </div>
                        {testimonial.isPinned && (
                          <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-primary-900/30 text-primary-400 border border-primary-500/30">
                            고정
                          </span>
                        )}
                      </div>
                      
                      {testimonial.comment && (
                        <p className="text-gray-300 mb-4 flex-1 line-clamp-4">
                          {testimonial.comment}
                        </p>
                      )}
                      
                      <div className="mt-auto">
                        {testimonial.user ? (
                          <div className="bg-gray-700/30 rounded-lg p-3 mb-2">
                            <div className="flex items-center justify-between mb-2">
                              <p className="text-white font-semibold">
                                {testimonial.user.username}
                              </p>
                              <span className="text-xs bg-primary-600 text-white px-2 py-1 rounded">
                                Lv.{testimonial.user.level}
                              </span>
                            </div>
                            <div className="grid grid-cols-2 gap-2 text-xs text-gray-400">
                              <div>게임 {testimonial.user.totalGames}회</div>
                              <div>승률 {testimonial.user.winRate}%</div>
                              <div>최고점수</div>
                              <div className="text-primary-400 font-medium">
                                {testimonial.user.bestScore.toLocaleString()}
                              </div>
                            </div>
                          </div>
                        ) : (
                          <p className="text-white font-semibold mb-2">
                            - {testimonial.name}
                          </p>
                        )}
                        <p className="text-xs text-gray-500">
                          {formatDate(testimonial.createdAt)}
                        </p>
                      </div>
                    </CardContent>
                  </Card>
                ))}
              </div>
            </div>
          )}

          {/* 페이지네이션 */}
          {totalPages > 1 && (
            <div className="flex justify-center mt-12">
              <div className="flex space-x-2">
                <Button
                  variant="outline"
                  onClick={() => handlePageChange(currentPage - 1)}
                  disabled={currentPage === 1 || loading}
                  className="px-4 py-2"
                >
                  이전
                </Button>
                
                {Array.from({ length: Math.min(totalPages, 10) }, (_, i) => {
                  let page;
                  if (totalPages <= 10) {
                    page = i + 1;
                  } else if (currentPage <= 5) {
                    page = i + 1;
                  } else if (currentPage >= totalPages - 4) {
                    page = totalPages - 9 + i;
                  } else {
                    page = currentPage - 4 + i;
                  }

                  return (
                    <Button
                      key={page}
                      variant={currentPage === page ? "primary" : "outline"}
                      onClick={() => handlePageChange(page)}
                      disabled={loading}
                      className="px-4 py-2"
                    >
                      {page}
                    </Button>
                  );
                })}
                
                <Button
                  variant="outline"
                  onClick={() => handlePageChange(currentPage + 1)}
                  disabled={currentPage === totalPages || loading}
                  className="px-4 py-2"
                >
                  다음
                </Button>
              </div>
            </div>
          )}
        </div>

        {/* 후기 작성 모달 */}
        <TestimonialModal
          isOpen={isModalOpen}
          onClose={() => setIsModalOpen(false)}
          onSuccess={handleModalSuccess}
        />
      </div>
    </Layout>
  );
}