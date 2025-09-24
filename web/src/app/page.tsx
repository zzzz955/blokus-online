'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { Download, Users, Trophy, Shield, Gamepad2, Star, ChevronLeft, ChevronRight, MessageSquarePlus, ArrowRight, UserPlus } from 'lucide-react';
import { useSession } from 'next-auth/react';
import Layout from '@/components/layout/Layout';
import Card, { CardHeader, CardTitle, CardContent } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import TestimonialModal from '@/components/testimonials/TestimonialModal';
import { api } from '@/utils/api';
import { Testimonial } from '@/types';

export default function HomePage() {
  const { data: session } = useSession();
  const [testimonials, setTestimonials] = useState<Testimonial[]>([]);
  const [currentPage, setCurrentPage] = useState(0);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const [total, setTotal] = useState(0);

  const features = [
    {
      icon: <Users className="w-8 h-8 text-primary-400" />,
      title: '멀티플레이어',
      description: '최대 4명까지 함께 즐기는 온라인 대전',
    },
    {
      icon: <Trophy className="w-8 h-8 text-secondary-400" />,
      title: '랭킹 시스템',
      description: '실력을 겨루고 순위를 올려보세요',
    },
    {
      icon: <Shield className="w-8 h-8 text-green-400" />,
      title: '안정적인 서버',
      description: '끊김없는 게임 환경을 제공합니다',
    },
    {
      icon: <Gamepad2 className="w-8 h-8 text-purple-400" />,
      title: '직관적인 UI',
      description: '누구나 쉽게 배우고 즐길 수 있습니다',
    },
  ];

  const ITEMS_PER_PAGE = 5;

  // 후기 목록 불러오기
  const fetchTestimonials = async () => {
    try {
      setLoading(true);
      const response = await api.getFull('/api/testimonials?home=true&limit=20') as any;
      setTestimonials(response.data || []);
      setHasMore(response.hasMore || false);
      setTotal(response.total || 0);
    } catch (error) {
      console.error('Error fetching testimonials:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTestimonials();
  }, []);

  // 현재 페이지의 후기들
  const getCurrentTestimonials = () => {
    const startIndex = currentPage * ITEMS_PER_PAGE;
    return testimonials.slice(startIndex, startIndex + ITEMS_PER_PAGE);
  };

  // 총 페이지 수 계산
  const totalPages = Math.ceil(testimonials.length / ITEMS_PER_PAGE);

  const handlePrevPage = () => {
    setCurrentPage(prev => Math.max(0, prev - 1));
  };

  const handleNextPage = () => {
    setCurrentPage(prev => Math.min(totalPages - 1, prev + 1));
  };

  const handleModalSuccess = () => {
    fetchTestimonials(); // 후기 목록 새로고침
  };

  return (
    <Layout>
      {/* Hero Section */}
      <section className="hero-gradient relative overflow-hidden">
        <div className="absolute inset-0 bg-black/20"></div>
        <div className="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-24 lg:py-32">
          <div className="text-center">
            <h1 className="text-4xl md:text-6xl font-bold text-white mb-6">
              <span className="block">전략적 사고와</span>
              <span className="block gaming-text">창의성이 만나는</span>
              <span className="block">블로블로</span>
            </h1>
            <p className="text-xl text-gray-200 mb-8 max-w-3xl mx-auto">
              친구들과 함께 즐기는 온라인 블로블로 게임입니다. 
              전략적 사고력을 기르고 창의적인 플레이를 통해 승리를 쟁취하세요!
            </p>
            <div className="flex flex-col sm:flex-row gap-4 justify-center">
              <Link href="/download">
                <Button size="lg" className="flex items-center space-x-2">
                  <Download size={20} />
                  <span>무료 다운로드</span>
                </Button>
              </Link>
              
              {!session?.user && (
                <Link href="/auth/signin">
                  <Button size="lg" variant="outline" className="flex items-center space-x-2 text-white border-white/30 hover:bg-white/10">
                    <UserPlus size={20} />
                    <span>회원가입하고 시작하기</span>
                  </Button>
                </Link>
              )}
              
              <Link href="/guide">
                <Button size="lg" variant="secondary" className="text-primary-700">
                  게임 가이드 보기
                </Button>
              </Link>
            </div>
            
            {!session?.user && (
              <div className="mt-6 text-center text-gray-300 text-sm">
                <p>
                  이미 계정이 있으신가요?{' '}
                  <Link href="/auth/signin" className="text-primary-400 hover:text-primary-300 underline">
                    로그인
                  </Link>
                  {' '} | {' '}
                  <Link href="/auth/reset-password" className="text-gray-400 hover:text-gray-300 underline">
                    비밀번호 재설정
                  </Link>
                </p>
              </div>
            )}
          </div>
        </div>
      </section>

      {/* Features Section */}
      <section className="py-20 bg-gray-900">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-16">
            <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">
              게임 특징
            </h2>
            <p className="text-xl text-gray-400 max-w-3xl mx-auto">
              블로블로만의 특별한 기능들을 만나보세요
            </p>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8">
            {features.map((feature, index) => (
              <Card key={index} hover className="text-center">
                <CardContent>
                  <div className="flex justify-center mb-4">
                    {feature.icon}
                  </div>
                  <h3 className="text-xl font-semibold text-white mb-2">
                    {feature.title}
                  </h3>
                  <p className="text-gray-400">
                    {feature.description}
                  </p>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      </section>

      {/* Testimonials Section */}
      <section className="py-20">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-16">
            <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">
              플레이어 후기
            </h2>
            <p className="text-xl text-gray-400 mb-6">
              블로블로를 즐기고 있는 플레이어들의 생생한 후기
            </p>
            <div className="flex justify-center space-x-4">
              <Button
                onClick={() => setIsModalOpen(true)}
                className="flex items-center space-x-2"
              >
                <MessageSquarePlus className="w-4 h-4" />
                <span>후기 작성하기</span>
              </Button>
              {total > testimonials.length && (
                <Link href="/testimonials">
                  <Button variant="outline" className="flex items-center space-x-2">
                    <ArrowRight className="w-4 h-4" />
                    <span>후기 전체 보기</span>
                  </Button>
                </Link>
              )}
            </div>
          </div>

          {loading ? (
            <div className="text-center text-gray-400">
              후기를 불러오는 중...
            </div>
          ) : testimonials.length === 0 ? (
            <div className="text-center">
              <Card>
                <CardContent className="py-12">
                  <p className="text-gray-400 text-lg mb-4">
                    아직 후기가 없습니다.
                  </p>
                  <p className="text-gray-500 mb-6">
                    첫 번째 후기를 작성해보세요!
                  </p>
                  <Button
                    onClick={() => setIsModalOpen(true)}
                    className="flex items-center space-x-2"
                  >
                    <MessageSquarePlus className="w-4 h-4" />
                    <span>후기 작성하기</span>
                  </Button>
                </CardContent>
              </Card>
            </div>
          ) : (
            <>
              {/* 후기 목록 */}
              <div className="relative">
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-6 mb-8">
                  {getCurrentTestimonials().map((testimonial) => (
                    <Card key={testimonial.id} className="h-full">
                      <CardContent className="flex flex-col h-full">
                        <div className="flex items-center mb-4">
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
                          {testimonial.isPinned && (
                            <span className="ml-2 inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-primary-900/30 text-primary-400">
                              고정
                            </span>
                          )}
                        </div>
                        {testimonial.comment && (
                          <p className="text-gray-300 mb-4 flex-1">
                            {testimonial.comment}
                          </p>
                        )}
                        {testimonial.user ? (
                          <div className="bg-gray-700/30 rounded-lg p-3">
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
                            </div>
                          </div>
                        ) : (
                          <p className="text-white font-semibold">
                            - {testimonial.name}
                          </p>
                        )}
                      </CardContent>
                    </Card>
                  ))}
                </div>

                {/* 네비게이션 버튼 */}
                {totalPages > 1 && (
                  <div className="flex justify-center items-center space-x-4">
                    <button
                      onClick={handlePrevPage}
                      disabled={currentPage === 0}
                      className="p-2 rounded-full bg-gray-800 border border-gray-700 text-gray-400 hover:text-white hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                    >
                      <ChevronLeft className="w-5 h-5" />
                    </button>
                    
                    <span className="text-gray-400">
                      {currentPage + 1} / {totalPages}
                    </span>
                    
                    <button
                      onClick={handleNextPage}
                      disabled={currentPage === totalPages - 1}
                      className="p-2 rounded-full bg-gray-800 border border-gray-700 text-gray-400 hover:text-white hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                    >
                      <ChevronRight className="w-5 h-5" />
                    </button>
                  </div>
                )}
              </div>

              {/* 더 많은 후기 보기 버튼 */}
              {hasMore && (
                <div className="text-center mt-8">
                  <Link href="/testimonials">
                    <Button variant="outline" size="lg" className="flex items-center space-x-2">
                      <ArrowRight className="w-4 h-4" />
                      <span>후기 전체 보기 ({total}개)</span>
                    </Button>
                  </Link>
                </div>
              )}
            </>
          )}
        </div>

        {/* 후기 작성 모달 */}
        <TestimonialModal
          isOpen={isModalOpen}
          onClose={() => setIsModalOpen(false)}
          onSuccess={handleModalSuccess}
        />
      </section>

      {/* CTA Section */}
      <section className="py-20 bg-gradient-to-r from-primary-600 to-secondary-600">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
          <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">
            지금 바로 시작하세요!
          </h2>
          <p className="text-xl text-gray-200 mb-8 max-w-2xl mx-auto">
            {session?.user 
              ? "게임을 다운로드하고 친구들과 함께 블로블로의 재미를 경험해보세요."
              : "회원가입하고 무료로 다운로드하여 친구들과 함께 블로블로의 재미를 경험해보세요."
            }
          </p>
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            {!session?.user && (
              <Link href="/auth/signin">
                <Button size="lg" variant="secondary" className="flex items-center space-x-2 text-primary-700">
                  <UserPlus size={20} />
                  <span>회원가입</span>
                </Button>
              </Link>
            )}
            <Link href="/download">
              <Button size="lg" variant="secondary" className="text-primary-700">
                <Download size={20} className="mr-2" />
                게임 다운로드
              </Button>
            </Link>
          </div>
        </div>
      </section>
    </Layout>
  );
}