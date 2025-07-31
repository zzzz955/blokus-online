'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import Layout from '@/components/layout/Layout';
import Card, { CardContent } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { Pin, Calendar, User, ChevronRight, Loader2 } from 'lucide-react';
import { formatDate } from '@/utils/format';
import { api } from '@/utils/api';
import { Announcement, PaginatedResponse } from '@/types';

export default function AnnouncementsPage() {
  const [announcements, setAnnouncements] = useState<Announcement[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const fetchAnnouncements = async (page: number = 1) => {
    try {
      setLoading(true);
      const response = await api.get<PaginatedResponse<Announcement>>(`/api/announcements?page=${page}&limit=10`);
      setAnnouncements(response.data || []);
      setTotalPages(response.pagination.totalPages);
      setCurrentPage(page);
    } catch (error: any) {
      setError(error.message || '공지사항을 불러오는데 실패했습니다.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAnnouncements();
  }, []);

  const handlePageChange = (page: number) => {
    fetchAnnouncements(page);
  };

  if (loading && announcements.length === 0) {
    return (
      <Layout>
        <div className="py-12">
          <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex items-center justify-center min-h-96">
              <div className="text-center">
                <Loader2 className="w-8 h-8 animate-spin text-primary-400 mx-auto mb-4" />
                <p className="text-gray-400">공지사항을 불러오는 중...</p>
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
          <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="text-center">
              <h1 className="text-4xl font-bold text-white mb-6">공지사항</h1>
              <Card className="bg-red-900/20 border-red-500/30">
                <CardContent className="text-center py-12">
                  <p className="text-red-400 mb-4">{error}</p>
                  <Button onClick={() => fetchAnnouncements()} variant="outline">
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
              공지사항
            </h1>
            <p className="text-xl text-gray-400">
              블로커스 온라인의 최신 소식과 중요한 공지사항을 확인하세요.
            </p>
          </div>

          {/* 공지사항 목록 */}
          {announcements.length === 0 ? (
            <Card>
              <CardContent className="text-center py-12">
                <p className="text-gray-400 text-lg">등록된 공지사항이 없습니다.</p>
              </CardContent>
            </Card>
          ) : (
            <div className="space-y-6">
              {announcements.map((announcement) => (
                <Card key={announcement.id} hover>
                  <CardContent>
                    <div className="flex items-start justify-between">
                      <div className="flex-1">
                        <div className="flex items-center space-x-3 mb-3">
                          {announcement.isPinned && (
                            <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-primary-900/30 text-primary-400 border border-primary-500/30">
                              <Pin className="w-3 h-3 mr-1" />
                              고정
                            </span>
                          )}
                          <div className="flex items-center text-gray-400 text-sm space-x-4">
                            <span className="flex items-center space-x-1">
                              <User className="w-4 h-4" />
                              <span>{announcement.author}</span>
                            </span>
                            <span className="flex items-center space-x-1">
                              <Calendar className="w-4 h-4" />
                              <span>{formatDate(announcement.createdAt)}</span>
                            </span>
                          </div>
                        </div>
                        
                        <Link href={`/announcements/${announcement.id}`}>
                          <h2 className="text-xl font-semibold text-white hover:text-primary-400 transition-colors mb-2">
                            {announcement.title}
                          </h2>
                        </Link>
                        
                        <p className="text-gray-300 line-clamp-2">
                          {announcement.content.replace(/[#*`]/g, '').substring(0, 150)}
                          {announcement.content.length > 150 && '...'}
                        </p>
                      </div>
                      
                      <Link href={`/announcements/${announcement.id}`}>
                        <button className="ml-4 p-2 text-gray-400 hover:text-primary-400 transition-colors">
                          <ChevronRight className="w-5 h-5" />
                        </button>
                      </Link>
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
                
                {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                  <Button
                    key={page}
                    variant={currentPage === page ? "primary" : "outline"}
                    onClick={() => handlePageChange(page)}
                    disabled={loading}
                    className="px-4 py-2"
                  >
                    {page}
                  </Button>
                ))}
                
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