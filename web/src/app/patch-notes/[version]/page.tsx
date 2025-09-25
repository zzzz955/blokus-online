'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import Layout from '@/components/layout/Layout';
import Card, { CardContent } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { ArrowLeft, Calendar, Tag, Download, Loader2 } from 'lucide-react';
import { formatDate } from '@/utils/format';
import { api } from '@/utils/api';
import { PatchNote, PaginatedResponse } from '@/types';
import ReactMarkdown from 'react-markdown';
import HtmlRenderer from '@/components/ui/HtmlRenderer';
import CommentSection from '@/components/comments/CommentSection';

interface PatchNoteDetailPageProps {
  params: { version: string };
}

export default function PatchNoteDetailPage({ params }: PatchNoteDetailPageProps) {
  const router = useRouter();
  const [patchNote, setPatchNote] = useState<PatchNote | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchPatchNote = async () => {
      try {
        setLoading(true);
        const patchNoteData = await api.get<PatchNote>(`/api/patch-notes/${encodeURIComponent(params.version)}`);
        setPatchNote(patchNoteData);
      } catch (error: any) {
        setError(error.message || '패치 노트를 불러오는데 실패했습니다.');
      } finally {
        setLoading(false);
      }
    };

    if (params.version) {
      fetchPatchNote();
    }
  }, [params.version]);

  const getVersionBadgeColor = (version: string) => {
    const majorVersion = version.split('.')[0];
    const colors = {
      '1': 'bg-blue-900/30 text-blue-400 border-blue-500/30',
      '2': 'bg-green-900/30 text-green-400 border-green-500/30',
      '3': 'bg-purple-900/30 text-purple-400 border-purple-500/30',
    };
    return colors[majorVersion as keyof typeof colors] || 'bg-gray-900/30 text-gray-400 border-gray-500/30';
  };

  if (loading) {
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

  if (error || !patchNote) {
    return (
      <Layout>
        <div className="py-12">
          <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
            <Card className="bg-red-900/20 border-red-500/30">
              <CardContent className="text-center py-12">
                <p className="text-red-400 mb-4">
                  {error || '패치 노트를 찾을 수 없습니다.'}
                </p>
                <Button onClick={() => router.back()} variant="outline">
                  돌아가기
                </Button>
              </CardContent>
            </Card>
          </div>
        </div>
      </Layout>
    );
  }

  return (
    <Layout>
      <div className="py-12">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
          {/* 뒤로가기 버튼 */}
          <div className="mb-8">
            <Button
              variant="ghost"
              onClick={() => router.back()}
              className="flex items-center space-x-2 text-gray-400 hover:text-white"
            >
              <ArrowLeft className="w-4 h-4" />
              <span>패치 노트 목록으로</span>
            </Button>
          </div>

          {/* 패치노트 상세 */}
          <Card>
            <CardContent>
              {/* 헤더 */}
              <div className="border-b border-dark-border pb-6 mb-8">
                <div className="flex items-center space-x-3 mb-4">
                  <span className={`inline-flex items-center px-3 py-1 rounded-full text-sm font-medium border ${getVersionBadgeColor(patchNote.version)}`}>
                    <Tag className="w-4 h-4 mr-1" />
                    버전 {patchNote.version}
                  </span>
                </div>
                
                <h1 className="text-3xl md:text-4xl font-bold text-white mb-6">
                  {patchNote.title}
                </h1>
                
                <div className="flex items-center justify-between">
                  <div className="flex items-center space-x-6 text-gray-400 text-sm">
                    <span className="flex items-center space-x-2">
                      <Calendar className="w-4 h-4" />
                      <span>출시일: {formatDate(patchNote.releaseDate)}</span>
                    </span>
                  </div>
                  
                  {patchNote.downloadUrl && (
                    <Link href={patchNote.downloadUrl}>
                      <Button className="flex items-center space-x-2">
                        <Download className="w-4 h-4" />
                        <span>다운로드</span>
                      </Button>
                    </Link>
                  )}
                </div>
              </div>

              {/* 내용 */}
              <div className="prose-custom">
                <HtmlRenderer 
                  content={patchNote.content}
                  className="patch-note-content"
                />
              </div>
              
              {/* 다운로드 섹션 */}
              {patchNote.downloadUrl && (
                <div className="mt-8 p-6 bg-primary-900/20 border border-primary-500/30 rounded-lg">
                  <h3 className="text-lg font-semibold text-white mb-2">
                    이 버전 다운로드
                  </h3>
                  <p className="text-white mb-4">
                    블로블로 v{patchNote.version}을 다운로드하여 최신 기능을 경험해보세요.
                  </p>
                  <Link href={patchNote.downloadUrl}>
                    <Button size="lg" className="flex items-center space-x-2">
                      <Download className="w-5 h-5" />
                      <span>버전 {patchNote.version} 다운로드</span>
                    </Button>
                  </Link>
                </div>
              )}
            </CardContent>
          </Card>

          {/* 댓글 섹션 */}
          <CommentSection patchNoteId={patchNote.id} />

          {/* 하단 네비게이션 */}
          <div className="mt-8 flex justify-between">
            <Button
              variant="outline"
              onClick={() => router.push('/patch-notes')}
              className="flex items-center space-x-2 text-gray-400 hover:text-black"
            >
              <ArrowLeft className="w-4 h-4" />
              <span>목록으로</span>
            </Button>
          </div>
        </div>
      </div>
    </Layout>
  );
}