'use client';

import { useState, useEffect } from 'react';
import { useSession } from 'next-auth/react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowLeft, FileText, Eye, Calendar, Edit, EyeOff } from 'lucide-react';

interface MyPost {
  id: number;
  title: string;
  category: string;
  view_count: number;
  is_hidden: boolean;
  created_at: string;
  updated_at: string;
}

export default function MyPostsPage() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [posts, setPosts] = useState<MyPost[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (status === 'loading') return;
    
    if (!session) {
      router.push('/auth/signin');
      return;
    }

    fetchMyPosts();
  }, [session, status, router]);

  const fetchMyPosts = async () => {
    try {
      const response = await fetch('/api/user/my-content?type=posts');
      const data = await response.json();
      
      if (data.success) {
        setPosts(data.data);
      } else {
        setError(data.error || '게시글을 불러올 수 없습니다.');
      }
    } catch (err) {
      setError('네트워크 오류가 발생했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const getCategoryLabel = (category: string) => {
    switch (category) {
      case 'question': return '질문';
      case 'guide': return '공략';
      case 'general': return '기타';
      default: return category;
    }
  };

  const getCategoryColor = (category: string) => {
    switch (category) {
      case 'question': return 'bg-blue-500/20 text-blue-400';
      case 'guide': return 'bg-green-500/20 text-green-400';
      case 'general': return 'bg-gray-500/20 text-gray-400';
      default: return 'bg-gray-500/20 text-gray-400';
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('ko-KR', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-dark-bg">
        <div className="max-w-4xl mx-auto px-4 py-8">
          <div className="animate-pulse">
            <div className="h-8 bg-gray-700 rounded mb-4"></div>
            <div className="space-y-4">
              {[...Array(5)].map((_, i) => (
                <div key={i} className="h-20 bg-gray-700 rounded-lg"></div>
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
            onClick={fetchMyPosts}
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
          <h1 className="text-3xl font-bold text-white mb-2">내 게시글</h1>
          <p className="text-gray-400">작성한 게시글을 확인하고 관리하세요.</p>
        </div>

        {/* 게시글 목록 */}
        {posts.length === 0 ? (
          <div className="bg-dark-card border border-dark-border rounded-lg p-12 text-center">
            <FileText size={48} className="text-gray-500 mx-auto mb-4" />
            <h3 className="text-xl font-semibold text-white mb-2">작성한 게시글이 없습니다</h3>
            <p className="text-gray-400 mb-6">첫 번째 게시글을 작성해보세요!</p>
            <Link href="/posts/write" className="btn-primary">
              게시글 작성하기
            </Link>
          </div>
        ) : (
          <div className="space-y-4">
            {posts.map((post) => (
              <div 
                key={post.id}
                className="bg-dark-card border border-dark-border rounded-lg p-6 hover:border-primary-500/50 transition-colors"
              >
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <div className="flex items-center space-x-3 mb-3">
                      <span className={`px-2 py-1 rounded-full text-xs font-medium ${getCategoryColor(post.category)}`}>
                        {getCategoryLabel(post.category)}
                      </span>
                      {post.is_hidden && (
                        <span className="px-2 py-1 rounded-full text-xs font-medium bg-yellow-500/20 text-yellow-400 flex items-center">
                          <EyeOff size={12} className="mr-1" />
                          숨김
                        </span>
                      )}
                    </div>
                    
                    <Link 
                      href={`/posts/${post.id}`}
                      className="block"
                    >
                      <h3 className="text-lg font-semibold text-white hover:text-primary-400 transition-colors mb-2">
                        {post.title}
                      </h3>
                    </Link>

                    <div className="flex items-center space-x-4 text-sm text-gray-400">
                      <div className="flex items-center space-x-1">
                        <Eye size={14} />
                        <span>{post.view_count}회</span>
                      </div>
                      <div className="flex items-center space-x-1">
                        <Calendar size={14} />
                        <span>{formatDate(post.created_at)}</span>
                      </div>
                      {post.created_at !== post.updated_at && (
                        <span className="text-xs text-gray-500">
                          (수정: {formatDate(post.updated_at)})
                        </span>
                      )}
                    </div>
                  </div>

                  <Link 
                    href={`/posts/${post.id}/edit`}
                    className="text-gray-400 hover:text-white p-2 rounded transition-colors"
                    title="수정"
                  >
                    <Edit size={18} />
                  </Link>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* 하단 액션 버튼 */}
        {posts.length > 0 && (
          <div className="mt-8 text-center">
            <Link href="/posts/write" className="btn-primary">
              새 게시글 작성하기
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}