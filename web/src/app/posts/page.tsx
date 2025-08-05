'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { useSession } from 'next-auth/react';
import { Post, PostCategory, PaginatedResponse } from '@/types';
import Button from '@/components/ui/Button';
import Card from '@/components/ui/Card';
import Layout from '@/components/layout/Layout';
import { formatPostDate, isPostModified } from '@/utils/format';

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
        limit: '20',
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

  // formatPostDate를 사용하므로 이 함수는 제거

  return (
    <Layout>
      <div className="max-w-6xl mx-auto px-4 py-8">
        {/* 헤더 */}
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-3xl font-bold text-white">자유 게시판</h1>
            <p className="text-gray-300 mt-2">블로커스 온라인 커뮤니티에서 자유롭게 소통해보세요</p>
          </div>
          {session && (
            <Link href="/posts/write">
              <Button className="bg-primary-600 hover:bg-primary-700">
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
                ? 'bg-primary-600 text-white'
                : 'bg-dark-card text-gray-300 hover:bg-gray-700'
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
                  ? 'bg-primary-600 text-white'
                  : 'bg-dark-card text-gray-300 hover:bg-gray-700'
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
              className="flex-1 px-4 py-2 bg-dark-card border border-dark-border text-white rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent placeholder-gray-400"
            />
            <Button type="submit" variant="outline">
              검색
            </Button>
          </div>
        </form>

        {/* 로딩 상태 */}
        {loading && (
          <div className="text-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-600 mx-auto"></div>
            <p className="text-gray-300 mt-2">게시글을 불러오는 중...</p>
          </div>
        )}

        {/* 에러 상태 */}
        {error && (
          <Card className="p-6 text-center">
            <p className="text-red-400">{error}</p>
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
                <h3 className="text-lg font-medium text-white mb-2">
                  게시글이 없습니다
                </h3>
                <p className="text-gray-300 mb-4">
                  첫 번째 게시글을 작성해보세요!
                </p>
                {session && (
                  <Link href="/posts/write">
                    <Button>글쓰기</Button>
                  </Link>
                )}
              </Card>
            ) : (
              <div className="bg-dark-card border border-dark-border rounded-lg overflow-hidden">
              <div className="divide-y divide-gray-700">
                {posts.map((post, index) => (
                  <div key={post.id} className="px-3 py-2 hover:bg-gray-700 transition-colors">
                    <div className="flex items-center justify-between">
                      {/* 좌측: 번호, 카테고리, 제목 */}
                      <div className="flex items-center space-x-3 flex-1 min-w-0">
                        <span className="text-gray-400 text-xs font-mono w-6 text-center">
                          {(currentPage - 1) * 20 + index + 1}
                        </span>
                        
                        <span className={`px-2 py-0.5 rounded text-xs font-medium flex-shrink-0 ${CATEGORY_COLORS[post.category]}`}>
                          {CATEGORY_LABELS[post.category]}
                        </span>
                        
                        <Link href={`/posts/${post.id}`} className="flex-1 min-w-0">
                          <span className="text-sm text-white hover:text-primary-400 transition-colors truncate block">
                            {post.title}
                          </span>
                        </Link>
                      </div>

                      {/* 우측: 작성자, 작성일, 조회수 */}
                      <div className="flex items-center space-x-3 text-xs text-gray-400 flex-shrink-0">
                        <span className="hidden sm:inline flex items-center space-x-1">
                          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-primary-100 text-primary-800">
                            Lv.{post.author.level}
                          </span>
                          <span>{post.author.displayName || post.author.username}</span>
                        </span>
                        <span className="hidden md:inline">
                          {formatPostDate(post.createdAt)}
                          {isPostModified(post.createdAt, post.updatedAt) && (
                            <span className="text-primary-400 ml-1">(수정)</span>
                          )}
                        </span>
                        <span className="w-8 text-right">
                          {post.viewCount}
                        </span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
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
    </Layout>
  );
}