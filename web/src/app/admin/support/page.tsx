'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import Card, { CardContent, CardHeader, CardTitle } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import {
  Search,
  Filter,
  MessageSquare,
  Clock,
  CheckCircle,
  XCircle,
  User,
  Calendar,
  Mail,
  ChevronDown,
  RefreshCw
} from 'lucide-react';
import { formatDate } from '@/utils/format';
import { SupportTicket } from '@/types';

interface AdminSupportTicket extends SupportTicket {
  userName?: string;
}

export default function AdminSupportPage() {
  const [tickets, setTickets] = useState<AdminSupportTicket[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'PENDING' | 'CLOSED'>('ALL');
  const [searchQuery, setSearchQuery] = useState('');
  const [searchType, setSearchType] = useState<'subject' | 'email' | 'content'>('subject');

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  useEffect(() => {
    fetchTickets();
  }, [statusFilter, currentPage, searchQuery, searchType]);

  const fetchTickets = async () => {
    try {
      setLoading(true);

      const params = new URLSearchParams({
        page: currentPage.toString(),
        limit: '10',
        status: statusFilter,
        search: searchQuery,
        searchType: searchType,
      });

      const response = await fetch(`/api/admin/support?${params}`, {
        credentials: 'include',
      });

      const data = await response.json();

      if (data.success) {
        setTickets(data.data || []);
        setTotalPages(data.pagination?.totalPages || 1);
        setTotalCount(data.pagination?.total || 0);
      } else {
        setError(data.error || '티켓을 불러올 수 없습니다.');
      }
    } catch (error: any) {
      setError('네트워크 오류가 발생했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    setCurrentPage(1);
    fetchTickets();
  };

  const handlePageChange = (page: number) => {
    setCurrentPage(page);
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

  if (loading) {
    return (
      <div className="animate-pulse">
        <div className="h-8 bg-gray-700 rounded mb-4"></div>
        <div className="space-y-4">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="h-32 bg-gray-700 rounded-lg"></div>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
          {/* Header */}
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-2xl font-bold text-white">고객 지원 관리</h1>
              <p className="text-gray-400 mt-2">
                총 {totalCount}개의 문의 | {statusFilter === 'ALL' ? '전체' : getStatusText(statusFilter)}
              </p>
            </div>
            <button
              onClick={fetchTickets}
              className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-md text-sm font-medium flex items-center space-x-2"
            >
              <RefreshCw className="w-4 h-4" />
              <span>새로고침</span>
            </button>
          </div>

          {/* Filters */}
          <div className="bg-dark-card border border-dark-border shadow rounded-lg">
            <div className="px-4 py-5 sm:p-6">
              <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                {/* Status Filter */}
                <div>
                  <label className="block text-sm font-medium text-gray-300 mb-2">상태</label>
                  <select
                    value={statusFilter}
                    onChange={(e) => setStatusFilter(e.target.value as any)}
                    className="w-full px-3 py-2 bg-dark-card border border-dark-border rounded-lg text-white focus:outline-none focus:border-primary-500"
                  >
                    <option value="ALL">전체</option>
                    <option value="PENDING">답변 대기</option>
                    <option value="CLOSED">종료</option>
                  </select>
                </div>

                {/* Search Type */}
                <div>
                  <label className="block text-sm font-medium text-gray-300 mb-2">검색 기준</label>
                  <select
                    value={searchType}
                    onChange={(e) => setSearchType(e.target.value as any)}
                    className="w-full px-3 py-2 bg-dark-card border border-dark-border rounded-lg text-white focus:outline-none focus:border-primary-500"
                  >
                    <option value="subject">제목</option>
                    <option value="email">이메일</option>
                    <option value="content">내용</option>
                  </select>
                </div>

                {/* Search Query */}
                <div className="md:col-span-2">
                  <label className="block text-sm font-medium text-gray-300 mb-2">검색어</label>
                  <div className="flex space-x-2">
                    <input
                      type="text"
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      placeholder="검색할 내용을 입력하세요..."
                      className="flex-1 px-3 py-2 bg-dark-card border border-dark-border rounded-lg text-white placeholder-gray-400 focus:outline-none focus:border-primary-500"
                      onKeyPress={(e) => e.key === 'Enter' && handleSearch()}
                    />
                    <button
                      onClick={handleSearch}
                      className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-md text-sm font-medium flex items-center space-x-2"
                    >
                      <Search className="w-4 h-4" />
                      <span>검색</span>
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Error State */}
          {error && (
            <div className="bg-dark-card border border-dark-border shadow rounded-lg">
              <div className="px-4 py-5 sm:p-6 text-center">
                <p className="text-red-400 mb-4">{error}</p>
                <button
                  onClick={fetchTickets}
                  className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-md text-sm font-medium"
                >
                  다시 시도
                </button>
              </div>
            </div>
          )}

          {/* Tickets List */}
          {!error && (
            <div className="space-y-4">
              {tickets.length === 0 ? (
                <div className="bg-dark-card border border-dark-border shadow rounded-lg">
                  <div className="px-4 py-5 sm:p-6 text-center py-12">
                    <MessageSquare className="w-12 h-12 text-gray-400 mx-auto mb-4" />
                    <p className="text-gray-400 text-lg mb-2">문의가 없습니다</p>
                    <p className="text-gray-500">검색 조건을 변경해보세요.</p>
                  </div>
                </div>
              ) : (
                tickets.map((ticket) => (
                  <div key={ticket.id} className="bg-dark-card border border-dark-border shadow rounded-lg hover:bg-dark-bg transition-colors">
                    <Link href={`/admin/support/${ticket.id}`}>
                      <div className="px-4 py-5 sm:p-6">
                        <div className="flex items-start justify-between">
                          <div className="flex-1">
                            <div className="flex items-center space-x-4 mb-3">
                              <h3 className="text-lg font-semibold text-white hover:text-primary-400 transition-colors">
                                {ticket.subject}
                              </h3>
                              <span className={`flex items-center space-x-1 px-3 py-1 rounded-full text-sm font-medium border ${getStatusColor(ticket.status)}`}>
                                {getStatusIcon(ticket.status)}
                                <span>{getStatusText(ticket.status)}</span>
                              </span>
                            </div>

                            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm text-gray-400 mb-4">
                              <div className="flex items-center space-x-2">
                                <MessageSquare className="w-4 h-4" />
                                <span>#{ticket.id}</span>
                              </div>
                              <div className="flex items-center space-x-2">
                                <Mail className="w-4 h-4" />
                                <span>{ticket.email}</span>
                              </div>
                              <div className="flex items-center space-x-2">
                                <Calendar className="w-4 h-4" />
                                <span>{formatDate(ticket.createdAt)}</span>
                              </div>
                            </div>

                            <p className="text-gray-300 line-clamp-2 mb-3">
                              {ticket.message}
                            </p>

                            {ticket.adminReply && (
                              <div className="bg-green-900/20 border border-green-500/30 rounded-lg p-3">
                                <div className="flex items-center space-x-2 mb-2">
                                  <CheckCircle className="w-4 h-4 text-green-400" />
                                  <span className="text-sm text-green-400 font-medium">답변 완료</span>
                                  {ticket.repliedAt && (
                                    <span className="text-xs text-gray-500">
                                      {formatDate(ticket.repliedAt)}
                                    </span>
                                  )}
                                </div>
                              </div>
                            )}
                          </div>
                        </div>
                      </div>
                    </Link>
                  </div>
                ))
              )}
            </div>
          )}

          {/* Pagination */}
          {!error && totalPages > 1 && (
            <div className="flex justify-center">
              <div className="flex space-x-2">
                <button
                  onClick={() => handlePageChange(currentPage - 1)}
                  disabled={currentPage === 1}
                  className="px-4 py-2 bg-gray-600 hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-md text-sm font-medium"
                >
                  이전
                </button>

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
                    <button
                      key={page}
                      onClick={() => handlePageChange(page)}
                      className={`px-4 py-2 rounded-md text-sm font-medium ${
                        currentPage === page
                          ? 'bg-blue-600 hover:bg-blue-700 text-white'
                          : 'bg-gray-600 hover:bg-gray-700 text-white'
                      }`}
                    >
                      {page}
                    </button>
                  );
                })}

                <button
                  onClick={() => handlePageChange(currentPage + 1)}
                  disabled={currentPage === totalPages}
                  className="px-4 py-2 bg-gray-600 hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-md text-sm font-medium"
                >
                  다음
                </button>
              </div>
            </div>
          )}
    </div>
  );
}