'use client';

import { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
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
  Send,
  AlertCircle,
  Save
} from 'lucide-react';
import { formatDate } from '@/utils/format';
import { SupportTicket } from '@/types';

interface AdminSupportTicket extends SupportTicket {
  userName?: string;
}

export default function AdminSupportTicketDetailPage() {
  const router = useRouter();
  const params = useParams();
  const ticketId = params.id as string;

  const [ticket, setTicket] = useState<AdminSupportTicket | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Reply form
  const [reply, setReply] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitMessage, setSubmitMessage] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  useEffect(() => {
    if (!ticketId || isNaN(Number(ticketId))) {
      setError('잘못된 문의 ID입니다.');
      setLoading(false);
      return;
    }

    fetchTicket();
  }, [ticketId]);

  const fetchTicket = async () => {
    try {
      setLoading(true);

      const response = await fetch(`/api/admin/support/${ticketId}`, {
        credentials: 'include',
      });

      const data = await response.json();

      if (data.success) {
        setTicket(data.data);
        setReply(data.data.adminReply || ''); // 기존 답변이 있으면 로드
      } else {
        if (response.status === 401) {
          setError('관리자 권한이 필요합니다.');
        } else if (response.status === 404) {
          setError('존재하지 않는 문의입니다.');
        } else {
          setError(data.error || '문의를 불러올 수 없습니다.');
        }
      }
    } catch (error: any) {
      setError('문의를 불러오는 중 오류가 발생했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmitReply = async () => {
    if (!reply.trim()) {
      setSubmitMessage({
        type: 'error',
        message: '답변 내용을 입력해주세요.',
      });
      return;
    }

    setSubmitting(true);
    setSubmitMessage(null);

    try {
      const response = await fetch(`/api/admin/support/${ticketId}/reply`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({
          adminReply: reply,
        }),
      });

      const data = await response.json();

      if (data.success) {
        setSubmitMessage({
          type: 'success',
          message: '답변이 성공적으로 저장되고 문의가 종료되었습니다.',
        });
        // 티켓 정보 새로고침
        fetchTicket();
      } else {
        setSubmitMessage({
          type: 'error',
          message: data.error || '답변 저장에 실패했습니다.',
        });
      }
    } catch (error) {
      setSubmitMessage({
        type: 'error',
        message: '답변 저장 중 오류가 발생했습니다.',
      });
    } finally {
      setSubmitting(false);
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
      <div className="animate-pulse">
        <div className="h-8 bg-gray-700 rounded mb-4"></div>
        <div className="h-64 bg-gray-700 rounded-lg"></div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="text-center">
        <AlertCircle className="w-16 h-16 text-red-400 mx-auto mb-4" />
        <h1 className="text-2xl font-bold text-white mb-4">문의를 불러올 수 없습니다</h1>
        <p className="text-gray-400 mb-8">{error}</p>
        <div className="flex justify-center space-x-4">
          <Link href="/admin/support">
            <Button variant="outline">문의 관리로</Button>
          </Link>
          <Button onClick={fetchTicket}>다시 시도</Button>
        </div>
      </div>
    );
  }

  if (!ticket) {
    return null;
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex-1">
          <Link
            href="/admin/support"
            className="inline-flex items-center text-gray-400 hover:text-white mb-4 transition-colors"
          >
            <ArrowLeft className="w-4 h-4 mr-2" />
            문의 관리로 돌아가기
          </Link>

          <div className="flex items-start justify-between">
            <div className="flex-1">
              <h1 className="text-3xl font-bold text-white mb-2">{ticket.subject}</h1>
              <div className="flex items-center space-x-4 text-sm text-gray-400">
                <div className="flex items-center space-x-1">
                  <MessageSquare className="w-4 h-4" />
                  <span>문의 번호: #{ticket.id}</span>
                </div>
                <div className="flex items-center space-x-1">
                  <Calendar className="w-4 h-4" />
                  <span>문의일: {formatDate(ticket.createdAt)}</span>
                </div>
                <div className="flex items-center space-x-1">
                  <Mail className="w-4 h-4" />
                  <span>{ticket.email}</span>
                </div>
                {ticket.userName && (
                  <div className="flex items-center space-x-1">
                    <User className="w-4 h-4" />
                    <span>{ticket.userName}</span>
                  </div>
                )}
              </div>
            </div>

            <div className={`flex items-center space-x-2 px-4 py-2 rounded-full border ${getStatusColor(ticket.status)}`}>
              {getStatusIcon(ticket.status)}
              <span className="font-medium">{getStatusText(ticket.status)}</span>
            </div>
          </div>
        </div>
      </div>

      {/* Customer Message */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center space-x-2">
            <User className="w-5 h-5" />
            <span>고객 문의 내용</span>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="bg-dark-bg rounded-lg p-6">
            <p className="text-gray-300 whitespace-pre-wrap leading-relaxed">
              {ticket.message}
            </p>
          </div>
        </CardContent>
      </Card>

      {/* Admin Reply Section */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center space-x-2">
            <MessageSquare className="w-5 h-5 text-green-400" />
            <span>관리자 답변</span>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            <textarea
              value={reply}
              onChange={(e) => setReply(e.target.value)}
              placeholder="고객에게 보낼 답변을 입력하세요..."
              rows={8}
              className="w-full px-4 py-3 bg-dark-card border border-dark-border rounded-lg text-white placeholder-gray-400 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500 resize-none"
            />

            {submitMessage && (
              <div
                className={`p-4 rounded-lg ${
                  submitMessage.type === 'success'
                    ? 'bg-green-900/20 border border-green-500/30 text-green-400'
                    : 'bg-red-900/20 border border-red-500/30 text-red-400'
                }`}
              >
                {submitMessage.message}
              </div>
            )}

            <div className="flex items-center justify-between">
              <p className="text-sm text-gray-400">
                답변을 저장하면 문의 상태가 자동으로 '종료'로 변경됩니다.
              </p>
              <Button
                onClick={handleSubmitReply}
                loading={submitting}
                disabled={!reply.trim() || submitting}
                className="flex items-center space-x-2"
              >
                <Send className="w-4 h-4" />
                <span>답변 저장 및 종료</span>
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Existing Reply (if any) */}
      {ticket.adminReply && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center space-x-2">
              <CheckCircle className="w-5 h-5 text-green-400" />
              <span>기존 답변</span>
              {ticket.repliedAt && (
                <span className="text-sm text-gray-400">
                  ({formatDate(ticket.repliedAt)})
                </span>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="bg-green-900/20 border border-green-500/30 rounded-lg p-6">
              <p className="text-gray-300 whitespace-pre-wrap leading-relaxed">
                {ticket.adminReply}
              </p>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}