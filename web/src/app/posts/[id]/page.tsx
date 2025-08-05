'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useSession } from 'next-auth/react';
import Link from 'next/link';
import ReactMarkdown from 'react-markdown';
import { Post, PostCategory } from '@/types';
import Button from '@/components/ui/Button';
import Card from '@/components/ui/Card';
import CommentSection from '@/components/comments/CommentSection';

const CATEGORY_LABELS: Record<PostCategory, string> = {
  QUESTION: '질문',
  GUIDE: '공략',
  GENERAL: '기타',
};

const CATEGORY_COLORS: Record<PostCategory, string> = {
  QUESTION: 'bg-blue-100 text-blue-800',
  GUIDE: 'bg-green-100 text-green-800',
  GENERAL: 'bg-gray-100 text-gray-800',
};

interface PostDetailPageProps {
  params: { id: string };
}

export default function PostDetailPage({ params }: PostDetailPageProps) {
  const { data: session } = useSession();
  const router = useRouter();
  const [post, setPost] = useState<Post | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [actionLoading, setActionLoading] = useState(false);

  const fetchPost = async () => {
    try {
      setLoading(true);
      const response = await fetch(`/api/posts/${params.id}`);
      const data = await response.json();

      if (data.success) {
        setPost(data.data);
      } else {
        setError(data.error || '게시글을 불러오는데 실패했습니다.');
      }
    } catch (err) {
      setError('게시글을 불러오는데 실패했습니다.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPost();
  }, [params.id]);

  const handleToggleHidden = async () => {
    if (!post) return;

    setActionLoading(true);
    try {
      const response = await fetch(`/api/posts/${post.id}/hide`, {
        method: 'PATCH',
      });
      const data = await response.json();

      if (data.success) {
        setPost(data.data);
      } else {
        alert(data.error || '처리에 실패했습니다.');
      }
    } catch (err) {
      alert('처리에 실패했습니다.');
    } finally {
      setActionLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!post) return;

    if (!confirm('정말로 이 게시글을 삭제하시겠습니까?')) {
      return;
    }

    setActionLoading(true);
    try {
      const response = await fetch(`/api/posts/${post.id}`, {
        method: 'DELETE',
      });
      const data = await response.json();

      if (data.success) {
        alert('게시글이 삭제되었습니다.');
        router.push('/posts');
      } else {
        alert(data.error || '삭제에 실패했습니다.');
      }
    } catch (err) {
      alert('삭제에 실패했습니다.');
    } finally {
      setActionLoading(false);
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString('ko-KR', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto"></div>
          <p className="text-gray-600 mt-2">게시글을 불러오는 중...</p>
        </div>
      </div>
    );
  }

  if (error || !post) {
    return (
      <div className="min-h-screen bg-gray-50">
        <div className="max-w-4xl mx-auto px-4 py-8">
          <Card className="p-8 text-center">
            <h2 className="text-xl font-semibold text-gray-900 mb-4">
              게시글을 찾을 수 없습니다
            </h2>
            <p className="text-gray-600 mb-6">{error}</p>
            <div className="flex justify-center gap-4">
              <Button onClick={() => router.back()}>
                이전으로
              </Button>
              <Link href="/posts">
                <Button variant="outline">
                  게시판으로
                </Button>
              </Link>
            </div>
          </Card>
        </div>
      </div>
    );
  }

  const isAuthor = session?.user?.id && parseInt(session.user.id) === post.authorId;
  // 관리자 권한은 서버 사이드에서 확인되므로 여기서는 UI만 처리
  // 실제 권한은 API 호출 시 서버에서 확인됩니다
  const isAdmin = false; // UI에서는 관리자 버튼을 숨김 (관리자 페이지에서 관리)

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-4xl mx-auto px-4 py-8">
        {/* 상단 네비게이션 */}
        <div className="mb-6">
          <Link href="/posts" className="text-blue-600 hover:text-blue-800 text-sm">
            ← 게시판으로 돌아가기
          </Link>
        </div>

        {/* 게시글 헤더 */}
        <Card className="p-6 mb-6">
          <div className="flex items-start justify-between mb-4">
            <div className="flex-1">
              <div className="flex items-center gap-3 mb-3">
                <span className={`px-3 py-1 rounded-full text-sm font-medium ${CATEGORY_COLORS[post.category]}`}>
                  {CATEGORY_LABELS[post.category]}
                </span>
                {post.isHidden && (
                  <span className="px-3 py-1 rounded-full text-sm font-medium bg-red-100 text-red-800">
                    숨김
                  </span>
                )}
                <span className="text-gray-500 text-sm">
                  조회 {post.viewCount}
                </span>
              </div>
              
              <h1 className="text-2xl font-bold text-gray-900 mb-4">
                {post.title}
              </h1>
              
              <div className="flex items-center gap-4 text-sm text-gray-600">
                <span className="font-medium">
                  {post.author.displayName || post.author.username}
                </span>
                <span>{formatDate(post.createdAt)}</span>
                {/* 수정된 시간이 생성 시간보다 1분 이상 차이날 때만 표시 */}
                {new Date(post.updatedAt).getTime() - new Date(post.createdAt).getTime() > 60000 && (
                  <span className="text-blue-600">
                    (수정됨: {formatDate(post.updatedAt)})
                  </span>
                )}
              </div>
            </div>

            {/* 작성자/관리자 액션 버튼 */}
            {(isAuthor || isAdmin) && (
              <div className="flex gap-2 ml-4">
                {isAuthor && (
                  <>
                    <Link href={`/posts/${post.id}/edit`}>
                      <Button variant="outline" size="sm">
                        수정
                      </Button>
                    </Link>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={handleToggleHidden}
                      disabled={actionLoading}
                    >
                      {post.isHidden ? '공개' : '숨김'}
                    </Button>
                  </>
                )}
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleDelete}
                  disabled={actionLoading}
                  className="text-red-600 hover:text-red-700 hover:bg-red-50"
                >
                  {actionLoading ? '처리 중...' : '삭제'}
                </Button>
              </div>
            )}
          </div>
        </Card>

        {/* 게시글 내용 */}
        <Card className="p-6">
          <div className="prose prose-slate max-w-none">
            <ReactMarkdown
              components={{
                // 마크다운 스타일링 커스터마이징
                h1: ({ children }) => <h1 className="text-2xl font-bold text-gray-900 mb-4">{children}</h1>,
                h2: ({ children }) => <h2 className="text-xl font-bold text-gray-900 mb-3">{children}</h2>,
                h3: ({ children }) => <h3 className="text-lg font-bold text-gray-900 mb-2">{children}</h3>,
                p: ({ children }) => <p className="text-gray-700 mb-4 leading-relaxed">{children}</p>,
                ul: ({ children }) => <ul className="list-disc list-inside text-gray-700 mb-4 space-y-1">{children}</ul>,
                ol: ({ children }) => <ol className="list-decimal list-inside text-gray-700 mb-4 space-y-1">{children}</ol>,
                blockquote: ({ children }) => (
                  <blockquote className="border-l-4 border-blue-300 pl-4 py-2 bg-blue-50 text-gray-700 mb-4 italic">
                    {children}
                  </blockquote>
                ),
                code: ({ children }) => (
                  <code className="bg-gray-100 text-gray-800 px-2 py-1 rounded text-sm font-mono">
                    {children}
                  </code>
                ),
                pre: ({ children }) => (
                  <pre className="bg-gray-100 text-gray-800 p-4 rounded-lg overflow-x-auto mb-4 text-sm font-mono">
                    {children}
                  </pre>
                ),
                a: ({ href, children }) => (
                  <a
                    href={href}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-blue-600 hover:text-blue-800 underline"
                  >
                    {children}
                  </a>
                ),
              }}
            >
              {post.content}
            </ReactMarkdown>
          </div>
        </Card>

        {/* 댓글 섹션 */}
        <CommentSection postId={post.id} />

        {/* 하단 네비게이션 */}
        <div className="mt-8 flex justify-between">
          <Button variant="outline" onClick={() => router.back()}>
            이전으로
          </Button>
          <Link href="/posts">
            <Button variant="outline">
              게시판 목록
            </Button>
          </Link>
        </div>
      </div>
    </div>
  );
}