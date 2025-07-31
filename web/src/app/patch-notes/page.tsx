'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import Layout from '@/components/layout/Layout';
import Card, { CardContent } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { Download, Calendar, Tag, ChevronRight, Loader2 } from 'lucide-react';
import { formatDate } from '@/utils/format';
import { api } from '@/utils/api';
import { PatchNote } from '@/types';

export default function PatchNotesPage() {
  const [patchNotes, setPatchNotes] = useState<PatchNote[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const fetchPatchNotes = async (page: number = 1) => {
    try {
      setLoading(true);
      const response = await api.get(`/api/patch-notes?page=${page}&limit=10`);
      setPatchNotes(response.data);
      setTotalPages(response.pagination.totalPages);
      setCurrentPage(page);
    } catch (error: any) {
      setError(error.message || '패치 노트를 불러오는데 실패했습니다.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPatchNotes();
  }, []);

  const handlePageChange = (page: number) => {
    fetchPatchNotes(page);
  };

  const getVersionBadgeColor = (version: string) => {
    const majorVersion = version.split('.')[0];
    const colors = {
      '1': 'bg-blue-900/30 text-blue-400 border-blue-500/30',
      '2': 'bg-green-900/30 text-green-400 border-green-500/30',
      '3': 'bg-purple-900/30 text-purple-400 border-purple-500/30',
    };
    return colors[majorVersion as keyof typeof colors] || 'bg-gray-900/30 text-gray-400 border-gray-500/30';
  };

  if (loading && patchNotes.length === 0) {
    return (
      <Layout>
        <div className="py-12">
          <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex items-center justify-center min-h-96">
              <div className="text-center">
                <Loader2 className="w-8 h-8 animate-spin text-primary-400 mx-auto mb-4" />
                <p className="text-gray-400">패치 노트를 불러오는 중...</p>
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
              <h1 className="text-4xl font-bold text-white mb-6">패치 노트</h1>
              <Card className="bg-red-900/20 border-red-500/30">
                <CardContent className="text-center py-12">
                  <p className="text-red-400 mb-4">{error}</p>
                  <Button onClick={() => fetchPatchNotes()} variant="outline">
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
              패치 노트
            </h1>
            <p className="text-xl text-gray-400">
              블로커스 온라인의 업데이트 내역과 새로운 기능들을 확인하세요.
            </p>
          </div>

          {/* 패치 노트 목록 */}
          {patchNotes.length === 0 ? (
            <Card>
              <CardContent className="text-center py-12">
                <p className="text-gray-400 text-lg">등록된 패치 노트가 없습니다.</p>
              </CardContent>
            </Card>
          ) : (
            <div className="space-y-8">
              {patchNotes.map((patchNote, index) => (
                <Card key={patchNote.id} hover>
                  <CardContent>
                    <div className="flex items-start justify-between">
                      <div className="flex-1">
                        <div className="flex items-center space-x-3 mb-4">
                          <span className={`inline-flex items-center px-3 py-1 rounded-full text-sm font-medium border ${getVersionBadgeColor(patchNote.version)}`}>
                            <Tag className="w-3 h-3 mr-1" />
                            v{patchNote.version}
                          </span>
                          {index === 0 && (
                            <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-primary-900/30 text-primary-400 border border-primary-500/30">
                              최신
                            </span>
                          )}
                          <span className="flex items-center text-gray-400 text-sm space-x-1">
                            <Calendar className="w-4 h-4" />
                            <span>{formatDate(patchNote.releaseDate)}</span>
                          </span>
                        </div>
                        
                        <Link href={`/patch-notes/${patchNote.version}`}>
                          <h2 className="text-xl font-semibold text-white hover:text-primary-400 transition-colors mb-3">
                            {patchNote.title}
                          </h2>
                        </Link>
                        
                        <p className="text-gray-300 line-clamp-3 mb-4">
                          {patchNote.content.replace(/[#*`]/g, '').substring(0, 200)}
                          {patchNote.content.length > 200 && '...'}
                        </p>

                        <div className="flex items-center space-x-4">
                          <Link href={`/patch-notes/${patchNote.version}`}>
                            <Button variant="outline" size="sm">
                              자세히 보기
                            </Button>
                          </Link>
                          {patchNote.downloadUrl && (
                            <Link href={patchNote.downloadUrl}>
                              <Button size="sm" className="flex items-center space-x-2">
                                <Download className="w-4 h-4" />
                                <span>다운로드</span>
                              </Button>
                            </Link>
                          )}
                        </div>
                      </div>
                      
                      <Link href={`/patch-notes/${patchNote.version}`}>
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