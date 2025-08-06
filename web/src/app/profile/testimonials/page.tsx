'use client';

import { useState, useEffect } from 'react';
import { useSession } from 'next-auth/react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowLeft, Star, Calendar, Pin, Eye, EyeOff } from 'lucide-react';

interface MyTestimonial {
  id: number;
  rating: number;
  comment?: string;
  is_pinned: boolean;
  is_published: boolean;
  created_at: string;
}

export default function MyTestimonialsPage() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [testimonials, setTestimonials] = useState<MyTestimonial[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (status === 'loading') return;
    
    if (!session) {
      router.push('/auth/signin');
      return;
    }

    fetchMyTestimonials();
  }, [session, status, router]);

  const fetchMyTestimonials = async () => {
    try {
      const response = await fetch('/api/user/my-content?type=testimonials');
      const data = await response.json();
      
      if (data.success) {
        setTestimonials(data.data);
      } else {
        setError(data.error || '후기를 불러올 수 없습니다.');
      }
    } catch (err) {
      setError('네트워크 오류가 발생했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('ko-KR', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  };

  const renderStars = (rating: number) => {
    return Array.from(Array(5), (_, i) => (
      <Star
        key={i}
        size={16}
        className={i < rating ? 'text-yellow-400 fill-current' : 'text-gray-600'}
      />
    ));
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-dark-bg">
        <div className="max-w-4xl mx-auto px-4 py-8">
          <div className="animate-pulse">
            <div className="h-8 bg-gray-700 rounded mb-4"></div>
            <div className="space-y-4">
              {[...Array(3)].map((_, i) => (
                <div key={i} className="h-32 bg-gray-700 rounded-lg"></div>
              ))}
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen bg-dark-bg flex items-center justify-center">
        <div className="text-center">
          <div className="text-red-400 text-xl mb-4">{error}</div>
          <button 
            onClick={fetchMyTestimonials}
            className="btn-primary"
          >
            다시 시도
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-dark-bg">
      <div className="max-w-4xl mx-auto px-4 py-8">
        {/* 헤더 */}
        <div className="mb-8">
          <Link 
            href="/profile"
            className="inline-flex items-center text-gray-400 hover:text-white mb-4"
          >
            <ArrowLeft size={20} className="mr-2" />
            내 정보로 돌아가기
          </Link>
          <h1 className="text-3xl font-bold text-white mb-2">내 후기</h1>
          <p className="text-gray-400">작성한 후기를 확인하세요.</p>
        </div>

        {/* 후기 목록 */}
        {testimonials.length === 0 ? (
          <div className="bg-dark-card border border-dark-border rounded-lg p-12 text-center">
            <Star size={48} className="text-gray-500 mx-auto mb-4" />
            <h3 className="text-xl font-semibold text-white mb-2">작성한 후기가 없습니다</h3>
            <p className="text-gray-400 mb-6">게임 경험을 다른 사용자들과 공유해보세요!</p>
            <Link href="/testimonials" className="btn-primary">
              후기 작성하기
            </Link>
          </div>
        ) : (
          <div className="space-y-4">
            {testimonials.map((testimonial) => (
              <div 
                key={testimonial.id}
                className="bg-dark-card border border-dark-border rounded-lg p-6"
              >
                <div className="flex items-start justify-between mb-4">
                  <div className="flex items-center space-x-3">
                    <div className="flex items-center space-x-1">
                      {renderStars(testimonial.rating)}
                    </div>
                    <span className="text-yellow-400 font-medium">
                      {testimonial.rating}/5
                    </span>
                    {testimonial.is_pinned && (
                      <span className="px-2 py-1 rounded-full text-xs font-medium bg-primary-500/20 text-primary-400 flex items-center">
                        <Pin size={12} className="mr-1" />
                        고정됨
                      </span>
                    )}
                    {!testimonial.is_published && (
                      <span className="px-2 py-1 rounded-full text-xs font-medium bg-red-500/20 text-red-400 flex items-center">
                        <EyeOff size={12} className="mr-1" />
                        비공개
                      </span>
                    )}
                  </div>
                  
                  <div className="flex items-center space-x-1 text-sm text-gray-400">
                    <Calendar size={14} />
                    <span>{formatDate(testimonial.created_at)}</span>
                  </div>
                </div>

                {testimonial.comment && (
                  <div className="mb-4">
                    <p className="text-gray-300 leading-relaxed">
                      {testimonial.comment}
                    </p>
                  </div>
                )}

                <div className="flex items-center justify-between text-sm">
                  <div className="text-gray-400">
                    {testimonial.is_published ? (
                      <span className="flex items-center text-green-400">
                        <Eye size={14} className="mr-1" />
                        공개됨
                      </span>
                    ) : (
                      <span className="flex items-center text-gray-400">
                        <EyeOff size={14} className="mr-1" />
                        관리자 검토 중
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* 하단 액션 버튼 */}
        {testimonials.length > 0 && (
          <div className="mt-8 text-center">
            <Link href="/testimonials" className="btn-primary">
              새 후기 작성하기
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}