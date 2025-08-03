'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import Layout from '@/components/layout/Layout';
import Card, { CardContent } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import { ArrowLeft, Calendar, User, Pin, Loader2 } from 'lucide-react';
import { formatDateTime } from '@/utils/format';
import { api } from '@/utils/api';
import { Announcement } from '@/types';
import ReactMarkdown from 'react-markdown';

interface AnnouncementDetailPageProps {
  params: { id: string };
}

export default function AnnouncementDetailPage({ params }: AnnouncementDetailPageProps) {
  const router = useRouter();
  const [announcement, setAnnouncement] = useState<Announcement | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchAnnouncement = async () => {
      try {
        setLoading(true);
        const response = await api.get<Announcement>(`/api/announcements/${params.id}`);
        setAnnouncement(response);
      } catch (error: any) {
        setError(error.message || '공지사항을 불러오는데 실패했습니다.');
      } finally {
        setLoading(false);
      }
    };

    if (params.id) {
      fetchAnnouncement();
    }
  }, [params.id]);

  if (loading) {
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

  if (error || !announcement) {
    return (
      <Layout>
        <div className="py-12">
          <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
            <Card className="bg-red-900/20 border-red-500/30">
              <CardContent className="text-center py-12">
                <p className="text-red-400 mb-4">
                  {error || '공지사항을 찾을 수 없습니다.'}
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
              className="flex items-center space-x-2 text-gray-400 hover:text-black"
            >
              <ArrowLeft className="w-4 h-4" />
              <span>공지사항 목록으로</span>
            </Button>
          </div>

          {/* 공지사항 상세 */}
          <Card>
            <CardContent>
              {/* 헤더 */}
              <div className="border-b border-dark-border pb-6 mb-8">
                <div className="flex items-center space-x-3 mb-4">
                  {announcement.isPinned && (
                    <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-primary-900/30 text-primary-400 border border-primary-500/30">
                      <Pin className="w-4 h-4 mr-1" />
                      고정 공지
                    </span>
                  )}
                </div>
                
                <h1 className="text-3xl md:text-4xl font-bold text-white mb-6">
                  {announcement.title}
                </h1>
                
                <div className="flex items-center justify-between text-gray-400 text-sm">
                  <div className="flex items-center space-x-6">
                    <span className="flex items-center space-x-2">
                      <User className="w-4 h-4" />
                      <span>{announcement.author}</span>
                    </span>
                    <span className="flex items-center space-x-2">
                      <Calendar className="w-4 h-4" />
                      <span>{formatDateTime(announcement.createdAt)}</span>
                    </span>
                  </div>
                  {announcement.updatedAt !== announcement.createdAt && (
                    <span className="text-xs">
                      수정: {formatDateTime(announcement.updatedAt)}
                    </span>
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
                      <p className="text-white mb-4 leading-relaxed">{children}</p>
                    ),
                    ul: ({ children }) => (
                      <ul className="list-disc list-inside text-white mb-4 space-y-2">{children}</ul>
                    ),
                    ol: ({ children }) => (
                      <ol className="list-decimal list-inside text-white mb-4 space-y-2">{children}</ol>
                    ),
                    li: ({ children }) => (
                      <li className="text-white">{children}</li>
                    ),
                    strong: ({ children }) => (
                      <strong className="text-white font-semibold">{children}</strong>
                    ),
                    em: ({ children }) => (
                      <em className="text-white italic">{children}</em>
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
                      <blockquote className="border-l-4 border-primary-500 pl-4 italic text-white mb-4">
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
                  {announcement.content}
                </ReactMarkdown>
              </div>
            </CardContent>
          </Card>

          {/* 하단 네비게이션 */}
          <div className="mt-8 flex justify-between">
            <Button
              variant="outline"
              onClick={() => router.push('/announcements')}
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