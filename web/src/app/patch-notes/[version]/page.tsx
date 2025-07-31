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
import { PatchNote } from '@/types';
import ReactMarkdown from 'react-markdown';

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
        // 버전으로 패치노트 찾기 (API 엔드포인트는 ID를 사용하므로 실제로는 버전 검색 API가 필요)
        const response = await api.get(`/api/patch-notes`);
        const foundPatchNote = response.data.find((p: PatchNote) => p.version === decodeURIComponent(params.version));
        
        if (foundPatchNote) {
          setPatchNote(foundPatchNote);
        } else {
          setError('패치 노트를 찾을 수 없습니다.');
        }
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
                <ReactMarkdown
                  components={{
                    h1: ({ children }) => (
                      <h1 className="text-2xl font-bold text-white mb-4 mt-8">{children}</h1>
                    ),
                    h2: ({ children }) => (
                      <h2 className="text-xl font-semibold text-white mb-3 mt-6">{children}</h2>
                    ),
                    h3: ({ children }) => (
                      <h3 className="text-lg font-semibold text-white mb-2 mt-4">{children}</h3>
                    ),
                    p: ({ children }) => (
                      <p className="text-gray-300 mb-4 leading-relaxed">{children}</p>
                    ),
                    ul: ({ children }) => (
                      <ul className="list-disc list-inside text-gray-300 mb-4 space-y-2">{children}</ul>
                    ),
                    ol: ({ children }) => (
                      <ol className="list-decimal list-inside text-gray-300 mb-4 space-y-2">{children}</ol>
                    ),
                    li: ({ children }) => (
                      <li className="text-gray-300">{children}</li>
                    ),
                    strong: ({ children }) => (
                      <strong className="text-white font-semibold">{children}</strong>
                    ),
                    em: ({ children }) => (
                      <em className="text-gray-200 italic">{children}</em>
                    ),
                    code: ({ children }) => (
                      <code className="bg-gray-800 text-primary-400 px-2 py-1 rounded text-sm">
                        {children}
                      </code>
                    ),
                    pre: ({ children }) => (
                      <pre className="bg-gray-800 p-4 rounded-lg overflow-x-auto mb-4">
                        {children}
                      </pre>
                    ),
                    blockquote: ({ children }) => (
                      <blockquote className="border-l-4 border-primary-500 pl-4 italic text-gray-300 mb-4">
                        {children}
                      </blockquote>
                    ),
                    a: ({ href, children }) => (
                      <a
                        href={href}
                        className="text-primary-400 hover:text-primary-300 underline"
                        target="_blank"
                        rel="noopener noreferrer"
                      >
                        {children}
                      </a>
                    ),
                  }}
                >
                  {patchNote.content}
                </ReactMarkdown>
              </div>
              
              {/* 다운로드 섹션 */}
              {patchNote.downloadUrl && (
                <div className="mt-8 p-6 bg-primary-900/20 border border-primary-500/30 rounded-lg">
                  <h3 className="text-lg font-semibold text-white mb-2">
                    이 버전 다운로드
                  </h3>
                  <p className="text-gray-300 mb-4">
                    블로커스 온라인 v{patchNote.version}을 다운로드하여 최신 기능을 경험해보세요.
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

          {/* 하단 네비게이션 */}
          <div className="mt-8 flex justify-between">
            <Button
              variant="outline"
              onClick={() => router.push('/patch-notes')}
              className="flex items-center space-x-2"
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