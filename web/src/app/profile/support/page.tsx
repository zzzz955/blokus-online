'use client';

import { useState, useEffect } from 'react';
import { useSession } from 'next-auth/react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowLeft, HelpCircle, Calendar, Clock, CheckCircle, MessageSquare } from 'lucide-react';

interface MySupportTicket {
  id: number;
  subject: string;
  status: string;
  admin_reply?: string;
  created_at: string;
  replied_at?: string;
}

export default function MySupportPage() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [tickets, setTickets] = useState<MySupportTicket[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (status === 'loading') return;
    
    if (!session) {
      router.push('/auth/signin');
      return;
    }

    fetchMyTickets();
  }, [session, status, router]);

  const fetchMyTickets = async () => {
    try {
      const response = await fetch('/api/user/my-content?type=tickets');
      const data = await response.json();
      
      if (data.success) {
        setTickets(data.data);
      } else {
        setError(data.error || '문의 내역을 불러올 수 없습니다.');
      }
    } catch (err) {
      setError('네트워크 오류가 발생했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const getStatusLabel = (status: string) => {
    switch (status) {
      case 'PENDING': return '대기 중';
      case 'ANSWERED': return '답변 완료';
      case 'CLOSED': return '해결됨';
      default: return status;
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'PENDING': return 'bg-yellow-500/20 text-yellow-400';
      case 'ANSWERED': return 'bg-blue-500/20 text-blue-400';
      case 'CLOSED': return 'bg-green-500/20 text-green-400';
      default: return 'bg-gray-500/20 text-gray-400';
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'PENDING': return <Clock size={16} />;
      case 'ANSWERED': return <MessageSquare size={16} />;
      case 'CLOSED': return <CheckCircle size={16} />;
      default: return <HelpCircle size={16} />;
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('ko-KR', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
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
            onClick={fetchMyTickets}
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
          <h1 className="text-3xl font-bold text-white mb-2">내 문의</h1>
          <p className="text-gray-400">제출한 문의 내역과 답변을 확인하세요.</p>
        </div>

        {/* 문의 목록 */}
        {tickets.length === 0 ? (
          <div className="bg-dark-card border border-dark-border rounded-lg p-12 text-center">
            <HelpCircle size={48} className="text-gray-500 mx-auto mb-4" />
            <h3 className="text-xl font-semibold text-white mb-2">문의 내역이 없습니다</h3>
            <p className="text-gray-400 mb-6">궁금한 점이 있으시면 언제든 문의해주세요!</p>
            <Link href="/support" className="btn-primary">
              문의하기
            </Link>
          </div>
        ) : (
          <div className="space-y-4">
            {tickets.map((ticket) => (
              <div 
                key={ticket.id}
                className="bg-dark-card border border-dark-border rounded-lg p-6"
              >
                <div className="flex items-start justify-between mb-4">
                  <div className="flex-1">
                    <div className="flex items-center space-x-3 mb-2">
                      <span className={`px-3 py-1 rounded-full text-sm font-medium flex items-center space-x-1 ${getStatusColor(ticket.status)}`}>
                        {getStatusIcon(ticket.status)}
                        <span>{getStatusLabel(ticket.status)}</span>
                      </span>
                      <span className="text-gray-400 text-sm">#{ticket.id}</span>
                    </div>
                    
                    <h3 className="text-lg font-semibold text-white mb-3">
                      {ticket.subject}
                    </h3>

                    <div className="flex items-center space-x-4 text-sm text-gray-400 mb-4">
                      <div className="flex items-center space-x-1">
                        <Calendar size={14} />
                        <span>문의일: {formatDate(ticket.created_at)}</span>
                      </div>
                      {ticket.replied_at && (
                        <div className="flex items-center space-x-1">
                          <MessageSquare size={14} />
                          <span>답변일: {formatDate(ticket.replied_at)}</span>
                        </div>
                      )}
                    </div>

                    {ticket.admin_reply && (
                      <div className="bg-dark-bg border border-primary-500/30 rounded-lg p-4">
                        <div className="flex items-center space-x-2 mb-2">
                          <MessageSquare size={16} className="text-primary-400" />
                          <span className="text-primary-400 font-medium">관리자 답변</span>
                        </div>
                        <p className="text-gray-300 leading-relaxed">
                          {ticket.admin_reply}
                        </p>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* 하단 액션 버튼 */}
        <div className="mt-8 text-center">
          <Link href="/support" className="btn-primary">
            새 문의하기
          </Link>
        </div>
      </div>
    </div>
  );
}