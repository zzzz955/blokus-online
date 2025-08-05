'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { useSession } from 'next-auth/react';
import { Post, PostCategory, PaginatedResponse } from '@/types';
import Button from '@/components/ui/Button';
import Card from '@/components/ui/Card';

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

export default function PostsPage() {
  const { data: session } = useSession();
  const [posts, setPosts] = useState<Post[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<PostCategory | 'ALL'>('ALL');
  const [searchTerm, setSearchTerm] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const fetchPosts = async () => {
    try {
      setLoading(true);
      const params = new URLSearchParams({
        page: currentPage.toString(),
        limit: '10',
      });

      if (selectedCategory !== 'ALL') {
        params.append('category', selectedCategory);
      }

      if (searchTerm) {
        params.append('search', searchTerm);
      }

      const response = await fetch(`/api/posts?${params}`);
      const data: PaginatedResponse<Post> = await response.json();

      if (data.success && data.data) {
        setPosts(data.data);
        setTotalPages(data.pagination.totalPages);
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
    fetchPosts();
  }, [selectedCategory, currentPage]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setCurrentPage(1);
    fetchPosts();
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const now = new Date();
    const diffInHours = Math.abs(now.getTime() - date.getTime()) / (1000 * 60 * 60);

    if (diffInHours < 24) {
      return date.toLocaleTimeString('ko-KR', { 
        hour: '2-digit', 
        minute: '2-digit' 
      });
    } else {
      return date.toLocaleDateString('ko-KR', { 
        month: 'short', 
        day: 'numeric' 
      });
    }
  };

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-6xl mx-auto px-4 py-8">
        {/* 헤더 */}
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-3xl font-bold text-gray-900">자유 게시판</h1>
            <p className="text-gray-600 mt-2">블로쿠스 온라인 커뮤니티에서 자유롭게 소통해보세요</p>
          </div>
          {session && (
            <Link href="/posts/write">
              <Button className="bg-blue-600 hover:bg-blue-700">
                글쓰기
              </Button>
            </Link>
          )}
        </div>

        {/* 카테고리 필터 */}
        <div className="flex flex-wrap gap-2 mb-6">
          <button
            onClick={() => setSelectedCategory('ALL')}
            className={`px-4 py-2 rounded-lg font-medium transition-colors ${
              selectedCategory === 'ALL'
                ? 'bg-blue-600 text-white'
                : 'bg-white text-gray-700 hover:bg-gray-100'
            }`}
          >
            전체
          </button>
          {Object.entries(CATEGORY_LABELS).map(([category, label]) => (
            <button
              key={category}
              onClick={() => setSelectedCategory(category as PostCategory)}
              className={`px-4 py-2 rounded-lg font-medium transition-colors ${
                selectedCategory === category
                  ? 'bg-blue-600 text-white'
                  : 'bg-white text-gray-700 hover:bg-gray-100'
              }`}
            >
              {label}
            </button>
          ))}
        </div>

        {/* 검색 */}
        <form onSubmit={handleSearch} className="mb-6">
          <div className="flex gap-2">
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="제목이나 내용으로 검색..."
              className="flex-1 px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
            <Button type="submit" variant="outline">
              검색
            </Button>
          </div>
        </form>

        {/* 로딩 상태 */}
        {loading && (
          <div className="text-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto"></div>
            <p className="text-gray-600 mt-2">게시글을 불러오는 중...</p>
          </div>
        )}

        {/* 에러 상태 */}
        {error && (
          <Card className="p-6 text-center">
            <p className="text-red-600">{error}</p>
            <Button onClick={fetchPosts} className="mt-4">
              다시 시도
            </Button>
          </Card>
        )}

        {/* 게시글 목록 */}
        {!loading && !error && (
          <>
            {posts.length === 0 ? (
              <Card className="p-8 text-center">
                <h3 className="text-lg font-medium text-gray-900 mb-2">
                  게시글이 없습니다
                </h3>
                <p className="text-gray-600 mb-4">
                  첫 번째 게시글을 작성해보세요!
                </p>
                {session && (
                  <Link href="/posts/write">
                    <Button>글쓰기</Button>
                  </Link>
                )}
              </Card>
            ) : (
              <div className="space-y-4">
                {posts.map((post) => (
                  <Card key={post.id} className="p-6 hover:shadow-md transition-shadow">
                    <div className="flex items-start justify-between">
                      <div className="flex-1">
                        <div className="flex items-center gap-3 mb-2">
                          <span className={`px-2 py-1 rounded-full text-sm font-medium ${CATEGORY_COLORS[post.category]}`}>
                            {CATEGORY_LABELS[post.category]}
                          </span>
                          <span className="text-gray-500 text-sm">
                            조회 {post.viewCount}
                          </span>
                        </div>
                        
                        <Link href={`/posts/${post.id}`}>
                          <h3 className="text-lg font-semibold text-gray-900 hover:text-blue-600 transition-colors">
                            {post.title}
                          </h3>
                        </Link>
                        
                        <div className="flex items-center gap-4 mt-3 text-sm text-gray-600">
                          <span>
                            {post.author.displayName || post.author.username}
                          </span>
                          <span>{formatDate(post.createdAt)}</span>
                          {post.createdAt !== post.updatedAt && (
                            <span className="text-blue-600">(수정됨)</span>
                          )}
                        </div>
                      </div>
                    </div>
                  </Card>
                ))}
              </div>
            )}

            {/* 페이지네이션 */}
            {totalPages > 1 && (
              <div className="flex justify-center mt-8">
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    disabled={currentPage === 1}
                    onClick={() => setCurrentPage(currentPage - 1)}
                  >
                    이전
                  </Button>
                  
                  {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                    <Button
                      key={page}
                      variant={currentPage === page ? 'default' : 'outline'}
                      onClick={() => setCurrentPage(page)}
                    >
                      {page}
                    </Button>
                  ))}
                  
                  <Button
                    variant="outline"
                    disabled={currentPage === totalPages}
                    onClick={() => setCurrentPage(currentPage + 1)}
                  >
                    다음
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}