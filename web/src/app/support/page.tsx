'use client';

import { useState, useEffect } from 'react';
import { useSession } from 'next-auth/react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import Layout from '@/components/layout/Layout';
import Card, { CardContent, CardHeader, CardTitle } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { 
  MessageSquare, 
  Clock, 
  CheckCircle, 
  XCircle, 
  Plus, 
  Home,
  Loader2,
  Mail,
  Calendar
} from 'lucide-react';
import { formatDate } from '@/utils/format';
import { api } from '@/utils/api';
import { SupportTicket, PaginatedResponse } from '@/types';

export default function SupportPage() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [tickets, setTickets] = useState<SupportTicket[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const fetchTickets = async (page: number = 1) => {
    try {
      setLoading(true);
      const response = await api.getFull(`/api/support?page=${page}&limit=10`) as PaginatedResponse<SupportTicket>;
      setTickets(response.data || []);
      setTotalPages(response.pagination?.totalPages || 1);
      setCurrentPage(page);
    } catch (error: any) {
      setError(error.message || '문의 목록을 불러오는데 실패했습니다.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (status === 'loading') return;

    if (!session?.user) {
      router.push('/auth/signin?callbackUrl=/support');
      return;
    }

    fetchTickets();
  }, [session, status, router]);

  const handlePageChange = (page: number) => {
    fetchTickets(page);
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'PENDING':
        return <Clock className="w-4 h-4 text-yellow-400" />;
      case 'ANSWERED':
        return <CheckCircle className="w-4 h-4 text-green-400" />;
      case 'CLOSED':
        return <XCircle className="w-4 h-4 text-gray-400" />;
      default:
        return <MessageSquare className="w-4 h-4 text-gray-400" />;
    }
  };

  const getStatusText = (status: string) => {
    switch (status) {
      case 'PENDING':
        return '답변 대기';
      case 'ANSWERED':
        return '답변 완료';
      case 'CLOSED':
        return '종료';
      default:
        return '알 수 없음';
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'PENDING':
        return 'bg-yellow-900/30 text-yellow-400 border-yellow-500/30';
      case 'ANSWERED':
        return 'bg-green-900/30 text-green-400 border-green-500/30';
      case 'CLOSED':
        return 'bg-gray-900/30 text-gray-400 border-gray-500/30';
      default:
        return 'bg-gray-900/30 text-gray-400 border-gray-500/30';
    }
  };

  if (status === 'loading' || loading) {
    return (
      <Layout>
        <div className="py-12">
          <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex items-center justify-center min-h-96">
              <div className="text-center">
                <Loader2 className="w-8 h-8 animate-spin text-primary-400 mx-auto mb-4" />
                <p className="text-gray-400">문의 목록을 불러오는 중...</p>
              </div>
            </div>
          </div>
        </div>
      </Layout>
    );
  }

  if (!session?.user) {
    return null; // 리다이렉트 처리됨
  }

  if (error) {
    return (
      <Layout>
        <div className="py-12">
          <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="text-center">
              <h1 className="text-4xl font-bold text-white mb-6">내 문의</h1>
              <Card className="bg-red-900/20 border-red-500/30">
                <CardContent className="text-center py-12">
                  <p className="text-red-400 mb-4">{error}</p>
                  <Button onClick={() => fetchTickets()} variant="outline">
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
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
          {/* Header */}
          <div className="text-center mb-12">
            <h1 className="text-4xl md:text-5xl font-bold text-white mb-6">
              내 문의
            </h1>
            <p className="text-xl text-gray-400 mb-8">
              제출한 문의 사항과 답변을 확인하실 수 있습니다.
            </p>
            <div className="flex justify-center space-x-4">
              <Link href="/">
                <Button variant="outline" className="flex items-center space-x-2">
                  <Home className="w-4 h-4" />
                  <span>홈으로</span>
                </Button>
              </Link>
              <Link href="/contact">
                <Button className="flex items-center space-x-2">
                  <Plus className="w-4 h-4" />
                  <span>새 문의 작성</span>
                </Button>
              </Link>
            </div>
          </div>

          {/* 사용자 정보 */}
          <Card className="mb-8">
            <CardHeader>
              <CardTitle className="flex items-center space-x-2">
                <Mail className="w-5 h-5" />
                <span>문의자 정보</span>
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex items-center space-x-4">
                <div>
                  <p className="text-white font-medium">{session.user.name || session.user.email}</p>
                  <p className="text-gray-400 text-sm">{session.user.email}</p>
                </div>
                <div className="ml-auto">
                  <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-primary-900/30 text-primary-400 border border-primary-500/30">
                    등록된 사용자
                  </span>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* 문의 목록 */}
          {tickets.length === 0 ? (
            <Card>
              <CardContent className="text-center py-12">
                <MessageSquare className="w-12 h-12 text-gray-400 mx-auto mb-4" />
                <p className="text-gray-400 text-lg mb-4">등록된 문의가 없습니다.</p>
                <p className="text-gray-500 mb-6">궁금한 점이 있으시면 언제든 문의해주세요!</p>
                <Link href="/contact">
                  <Button className="flex items-center space-x-2">
                    <Plus className="w-4 h-4" />
                    <span>첫 문의 작성하기</span>
                  </Button>
                </Link>
              </CardContent>
            </Card>
          ) : (
            <div className="space-y-6">
              {tickets.map((ticket) => (
                <Card key={ticket.id} hover>
                  <CardHeader>
                    <div className="flex items-start justify-between">
                      <div className="flex-1">
                        <CardTitle className="text-lg mb-2">{ticket.subject}</CardTitle>
                        <div className="flex items-center space-x-4 text-sm text-gray-400">
                          <div className="flex items-center space-x-1">
                            <Calendar className="w-4 h-4" />
                            <span>{formatDate(ticket.createdAt)}</span>
                          </div>
                          <div className="flex items-center space-x-1">
                            {getStatusIcon(ticket.status)}
                            <span 
                              className={`px-2 py-1 rounded-full text-xs font-medium border ${getStatusColor(ticket.status)}`}
                            >
                              {getStatusText(ticket.status)}
                            </span>
                          </div>
                        </div>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent>
                    <div className="space-y-4">
                      {/* 원본 문의 */}
                      <div>
                        <h4 className="text-sm font-medium text-gray-300 mb-2">문의 내용</h4>
                        <div className="bg-gray-700/30 rounded-lg p-4">
                          <p className="text-gray-300 whitespace-pre-wrap">{ticket.message}</p>
                        </div>
                      </div>

                      {/* 관리자 답변 */}
                      {ticket.adminReply && (
                        <div>
                          <div className="flex items-center justify-between mb-2">
                            <h4 className="text-sm font-medium text-green-400">관리자 답변</h4>
                            {ticket.repliedAt && (
                              <span className="text-xs text-gray-500">
                                {formatDate(ticket.repliedAt)}
                              </span>
                            )}
                          </div>
                          <div className="bg-green-900/20 border border-green-500/30 rounded-lg p-4">
                            <p className="text-gray-300 whitespace-pre-wrap">{ticket.adminReply}</p>
                          </div>
                        </div>
                      )}
                    </div>
                  </CardContent>
                </Card>
              ))}
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
      </div>
    </Layout>
  );
}