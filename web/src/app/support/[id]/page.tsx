'use client';

import { useState, useEffect } from 'react';
import { useSession } from 'next-auth/react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import Layout from '@/components/layout/Layout';
import Card, { CardContent, CardHeader, CardTitle } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import {
  ArrowLeft,
  Clock,
  CheckCircle,
  XCircle,
  MessageSquare,
  Calendar,
  Mail,
  User,
  AlertCircle
} from 'lucide-react';
import { formatDate } from '@/utils/format';
import { api } from '@/utils/api';
import { SupportTicket } from '@/types';

export default function SupportTicketDetailPage() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const params = useParams();
  const ticketId = params.id as string;

  const [ticket, setTicket] = useState<SupportTicket | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (status === 'loading') return;

    if (!session?.user) {
      router.push('/auth/signin?callbackUrl=/support');
      return;
    }

    if (!ticketId || isNaN(Number(ticketId))) {
      setError('잘못된 문의 ID입니다.');
      setLoading(false);
      return;
    }

    fetchTicket();
  }, [session, status, router, ticketId]);

  const fetchTicket = async () => {
    try {
      setLoading(true);
      const response = await api.getFull(`/api/support/${ticketId}`);
      if (response.success) {
        setTicket(response.data);
      } else {
        setError(response.error || '문의를 불러올 수 없습니다.');
      }
    } catch (error: any) {
      if (error.status === 403) {
        setError('접근 권한이 없습니다.');
      } else if (error.status === 404) {
        setError('존재하지 않는 문의입니다.');
      } else {
        setError('문의를 불러오는 중 오류가 발생했습니다.');
      }
    } finally {
      setLoading(false);
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'PENDING':
        return <Clock className="w-5 h-5 text-yellow-400" />;
      case 'ANSWERED':
        return <CheckCircle className="w-5 h-5 text-green-400" />;
      case 'CLOSED':
        return <XCircle className="w-5 h-5 text-gray-400" />;
      default:
        return <MessageSquare className="w-5 h-5 text-gray-400" />;
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

  if (loading) {
    return (
      <Layout>
        <div className="py-12">
          <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="animate-pulse">
              <div className="h-8 bg-gray-700 rounded mb-4"></div>
              <div className="h-64 bg-gray-700 rounded-lg"></div>
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
          <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="text-center">
              <AlertCircle className="w-16 h-16 text-red-400 mx-auto mb-4" />
              <h1 className="text-2xl font-bold text-white mb-4">문의를 불러올 수 없습니다</h1>
              <p className="text-gray-400 mb-8">{error}</p>
              <div className="flex justify-center space-x-4">
                <Link href="/support">
                  <Button variant="outline">문의 목록으로</Button>
                </Link>
                <Button onClick={fetchTicket}>다시 시도</Button>
              </div>
            </div>
          </div>
        </div>
      </Layout>
    );
  }

  if (!ticket) {
    return null;
  }

  return (
    <Layout>
      <div className="py-12">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
          {/* Header */}
          <div className="mb-8">
            <Link
              href="/support"
              className="inline-flex items-center text-gray-400 hover:text-white mb-4 transition-colors"
            >
              <ArrowLeft className="w-4 h-4 mr-2" />
              문의 목록으로 돌아가기
            </Link>

            <div className="flex items-start justify-between">
              <div>
                <h1 className="text-3xl font-bold text-white mb-2">{ticket.subject}</h1>
                <div className="flex items-center space-x-4 text-sm text-gray-400">
                  <div className="flex items-center space-x-1">
                    <Calendar className="w-4 h-4" />
                    <span>문의일: {formatDate(ticket.createdAt)}</span>
                  </div>
                  <div className="flex items-center space-x-1">
                    <MessageSquare className="w-4 h-4" />
                    <span>문의 번호: #{ticket.id}</span>
                  </div>
                </div>
              </div>

              <div className={`flex items-center space-x-2 px-4 py-2 rounded-full border ${getStatusColor(ticket.status)}`}>
                {getStatusIcon(ticket.status)}
                <span className="font-medium">{getStatusText(ticket.status)}</span>
              </div>
            </div>
          </div>

          {/* Ticket Content */}
          <div className="space-y-6">
            {/* Original Message */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center space-x-2">
                  <User className="w-5 h-5" />
                  <span>문의 내용</span>
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="flex items-start space-x-4 mb-4">
                  <div className="flex-shrink-0">
                    <div className="w-10 h-10 bg-primary-500 rounded-full flex items-center justify-center">
                      <User className="w-5 h-5 text-white" />
                    </div>
                  </div>
                  <div className="flex-1">
                    <div className="flex items-center space-x-2 mb-2">
                      <span className="font-medium text-white">
                        {session?.user?.name || session?.user?.email}
                      </span>
                      <span className="text-sm text-gray-400">
                        {formatDate(ticket.createdAt)}
                      </span>
                    </div>
                    <div className="bg-dark-bg rounded-lg p-4">
                      <p className="text-gray-300 whitespace-pre-wrap leading-relaxed">
                        {ticket.message}
                      </p>
                    </div>
                  </div>
                </div>

                <div className="flex items-center space-x-2 text-sm text-gray-400 border-t border-dark-border pt-4">
                  <Mail className="w-4 h-4" />
                  <span>답변 받을 이메일: {ticket.email}</span>
                </div>
              </CardContent>
            </Card>

            {/* Admin Reply */}
            {ticket.adminReply && (
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center space-x-2">
                    <MessageSquare className="w-5 h-5 text-green-400" />
                    <span>관리자 답변</span>
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="flex items-start space-x-4">
                    <div className="flex-shrink-0">
                      <div className="w-10 h-10 bg-green-500 rounded-full flex items-center justify-center">
                        <MessageSquare className="w-5 h-5 text-white" />
                      </div>
                    </div>
                    <div className="flex-1">
                      <div className="flex items-center space-x-2 mb-2">
                        <span className="font-medium text-green-400">관리자</span>
                        {ticket.repliedAt && (
                          <span className="text-sm text-gray-400">
                            {formatDate(ticket.repliedAt)}
                          </span>
                        )}
                      </div>
                      <div className="bg-green-900/20 border border-green-500/30 rounded-lg p-4">
                        <p className="text-gray-300 whitespace-pre-wrap leading-relaxed">
                          {ticket.adminReply}
                        </p>
                      </div>
                    </div>
                  </div>
                </CardContent>
              </Card>
            )}

            {/* Status Information */}
            <Card>
              <CardContent className="pt-6">
                <div className="text-center">
                  {ticket.status === 'PENDING' && (
                    <p className="text-gray-400">
                      문의가 접수되었습니다. 빠른 시일 내에 답변드리겠습니다.
                    </p>
                  )}
                  {ticket.status === 'ANSWERED' && (
                    <p className="text-green-400">
                      답변이 완료되었습니다. 추가 문의사항이 있으시면 새로운 문의를 작성해주세요.
                    </p>
                  )}
                  {ticket.status === 'CLOSED' && (
                    <p className="text-gray-400">
                      이 문의는 종료되었습니다. 추가 문의사항이 있으시면 새로운 문의를 작성해주세요.
                    </p>
                  )}
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Actions */}
          <div className="mt-8 text-center">
            <Link href="/support">
              <Button variant="outline" className="mr-4">
                문의 목록으로
              </Button>
            </Link>
            <Link href="/support">
              <Button onClick={() => {
                // URL에 create 탭 파라미터를 추가하는 대신 클릭 후 탭 전환
                const url = new URL('/support', window.location.origin);
                window.location.href = url.toString();
              }}>
                새 문의 작성
              </Button>
            </Link>
          </div>
        </div>
      </div>
    </Layout>
  );
}