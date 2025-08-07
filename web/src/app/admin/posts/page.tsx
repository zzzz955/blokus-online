'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { PostCategory, Post } from '@/types';
import Button from '@/components/ui/Button';
import Card from '@/components/ui/Card';
import { adminFetch } from '@/lib/admin-auth';

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

interface AdminPostStats {
  categories: Record<PostCategory, number>;
  status: {
    total: number;
    active: number;
    hidden: number;
    deleted: number;
  };
}

export default function AdminPostsPage() {
  const [posts, setPosts] = useState<Post[]>([]);
  const [stats, setStats] = useState<AdminPostStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<PostCategory | 'ALL'>('ALL');
  const [selectedStatus, setSelectedStatus] = useState<'all' | 'active' | 'hidden' | 'deleted'>('all');
  const [searchTerm, setSearchTerm] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [actionLoading, setActionLoading] = useState<number | null>(null);

  const fetchPosts = async () => {
    try {
      setLoading(true);
      const params = new URLSearchParams({
        page: currentPage.toString(),
        limit: '20',
        status: selectedStatus,
      });

      if (selectedCategory !== 'ALL') {
        params.append('category', selectedCategory);
      }

      if (searchTerm) {
        params.append('search', searchTerm);
      }

      const response = await adminFetch(`/api/admin/posts?${params}`);
      const data = await response.json();

      if (data.success) {
        setPosts(data.data);
        setTotalPages(data.pagination.totalPages);
        setStats(data.stats);
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
  }, [selectedCategory, selectedStatus, currentPage]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setCurrentPage(1);
    fetchPosts();
  };

  const handleAction = async (postId: number, action: 'delete' | 'restore' | 'hide' | 'show') => {
    if (actionLoading) return;

    const confirmMessages = {
      delete: '정말로 이 게시글을 삭제하시겠습니까?',
      restore: '이 게시글을 복구하시겠습니까?',
      hide: '이 게시글을 숨기시겠습니까?',
      show: '이 게시글을 공개하시겠습니까?',
    };

    if (!confirm(confirmMessages[action])) {
      return;
    }

    setActionLoading(postId);
    try {
      let response;

      if (action === 'delete') {
        response = await adminFetch(`/api/admin/posts/${postId}`, {
          method: 'DELETE',
        });
      } else {
        response = await adminFetch(`/api/admin/posts/${postId}`, {
          method: 'PATCH',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ action }),
        });
      }

      const data = await response.json();

      if (data.success) {
        alert(data.message);
        fetchPosts(); // 목록 새로고침
      } else {
        alert(data.error || '처리에 실패했습니다.');
      }
    } catch (err) {
      alert('처리에 실패했습니다.');
    } finally {
      setActionLoading(null);
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString('ko-KR', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const getStatusBadge = (post: Post) => {
    if (post.isDeleted) {
      return <span className="px-2 py-1 rounded-full text-xs font-medium bg-red-100 text-red-800">삭제됨</span>;
    }
    if (post.isHidden) {
      return <span className="px-2 py-1 rounded-full text-xs font-medium bg-yellow-100 text-yellow-800">숨김</span>;
    }
    return <span className="px-2 py-1 rounded-full text-xs font-medium bg-green-100 text-green-800">공개</span>;
  };

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 sm:px-6 lg:px-8 space-y-6">
      {/* 헤더 */}
      <div>
        <h1 className="text-2xl font-bold text-white">게시글 관리</h1>
        <p className="text-gray-400 mt-2">커뮤니티 게시글을 관리합니다</p>
      </div>

      {/* 통계 */}
      {stats && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <Card className="p-4">
            <div className="text-2xl font-bold text-blue-600">{stats.status.total}</div>
            <div className="text-sm text-gray-400">전체 게시글</div>
          </Card>
          <Card className="p-4">
            <div className="text-2xl font-bold text-green-600">{stats.status.active}</div>
            <div className="text-sm text-gray-400">공개</div>
          </Card>
          <Card className="p-4">
            <div className="text-2xl font-bold text-yellow-600">{stats.status.hidden}</div>
            <div className="text-sm text-gray-400">숨김</div>
          </Card>
          <Card className="p-4">
            <div className="text-2xl font-bold text-red-600">{stats.status.deleted}</div>
            <div className="text-sm text-gray-400">삭제</div>
          </Card>
        </div>
      )}

      {/* 필터 */}
      <Card className="p-4">
        <div className="space-y-4">
          {/* 카테고리 필터 */}
          <div>
            <label className="block text-sm font-medium text-white mb-2">카테고리</label>
            <div className="flex flex-wrap gap-2">
              <button
                onClick={() => setSelectedCategory('ALL')}
                className={`px-3 py-1 rounded-lg text-sm font-medium transition-colors ${selectedCategory === 'ALL'
                    ? 'bg-blue-600 text-white'
                    : 'bg-gray-700 text-gray-300 hover:bg-gray-600'
                  }`}
              >
                전체
              </button>
              {Object.entries(CATEGORY_LABELS).map(([category, label]) => (
                <button
                  key={category}
                  onClick={() => setSelectedCategory(category as PostCategory)}
                  className={`px-3 py-1 rounded-lg text-sm font-medium transition-colors ${selectedCategory === category
                      ? 'bg-blue-600 text-white'
                      : 'bg-gray-700 text-gray-300 hover:bg-gray-600'
                    }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>

          {/* 상태 필터 */}
          <div>
            <label className="block text-sm font-medium text-white mb-2">상태</label>
            <div className="flex flex-wrap gap-2">
              {[
                { value: 'all', label: '전체' },
                { value: 'active', label: '공개' },
                { value: 'hidden', label: '숨김' },
                { value: 'deleted', label: '삭제' },
              ].map(({ value, label }) => (
                <button
                  key={value}
                  onClick={() => setSelectedStatus(value as any)}
                  className={`px-3 py-1 rounded-lg text-sm font-medium transition-colors ${selectedStatus === value
                      ? 'bg-blue-600 text-white'
                      : 'bg-gray-700 text-gray-300 hover:bg-gray-600'
                    }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>

          {/* 검색 */}
          <form onSubmit={handleSearch} className="flex gap-2">
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="제목, 내용, 작성자로 검색..."
              className="flex-1 px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent text-sm"
            />
            <Button type="submit" size="sm">
              검색
            </Button>
          </form>
        </div>
      </Card>

      {/* 게시글 목록 */}
      <Card className="overflow-hidden">
        {loading ? (
          <div className="p-8 text-center">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto"></div>
            <p className="text-gray-400 mt-2">게시글을 불러오는 중...</p>
          </div>
        ) : error ? (
          <div className="p-8 text-center">
            <p className="text-red-600">{error}</p>
            <Button onClick={fetchPosts} className="mt-4">
              다시 시도
            </Button>
          </div>
        ) : posts.length === 0 ? (
          <div className="p-8 text-center">
            <p className="text-gray-400">조건에 맞는 게시글이 없습니다.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-dark-bg">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">
                    게시글
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">
                    작성자
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">
                    상태
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">
                    조회수
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">
                    작성일
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-400 uppercase tracking-wider">
                    관리
                  </th>
                </tr>
              </thead>
              <tbody className="bg-dark-card border border-dark-border divide-y divide-gray-200">
                {posts.map((post) => (
                  <tr key={post.id} className="hover:bg-gray-700/30">
                    <td className="px-6 py-4">
                      <div className="flex items-start space-x-3">
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-2 mb-1">
                            <span className={`px-2 py-1 rounded-full text-xs font-medium ${CATEGORY_COLORS[post.category]}`}>
                              {CATEGORY_LABELS[post.category]}
                            </span>
                            <Link 
                              href={`/posts/${post.id}`}
                              className="text-sm font-medium text-white hover:text-blue-400 transition-colors truncate w-72 block"
                              target="_blank"
                              rel="noopener noreferrer"
                            >
                              {post.title}
                            </Link>
                          </div>
                          {/* <p className="text-sm text-gray-400 line-clamp-2">
                            {post.content}
                          </p> */}
                        </div>
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="text-sm text-white">
                        {post.author.displayName || post.author.username}
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      {getStatusBadge(post)}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-white">
                      {post.viewCount}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-400">
                      {formatDate(post.createdAt)}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium">
                      <div className="flex justify-end gap-2">
                        {post.isDeleted ? (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => handleAction(post.id, 'restore')}
                            disabled={actionLoading === post.id}
                            className="text-green-600 hover:text-green-700"
                          >
                            복구
                          </Button>
                        ) : (
                          <>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => handleAction(post.id, post.isHidden ? 'show' : 'hide')}
                              disabled={actionLoading === post.id}
                              className="text-yellow-600 hover:text-yellow-700"
                            >
                              {post.isHidden ? '공개' : '숨김'}
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => handleAction(post.id, 'delete')}
                              disabled={actionLoading === post.id}
                              className="text-red-600 hover:text-red-700"
                            >
                              {actionLoading === post.id ? '처리 중...' : '삭제'}
                            </Button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* 페이지네이션 */}
      {totalPages > 1 && (
        <div className="flex justify-center">
          <div className="flex gap-2">
            <Button
              variant="outline"
              disabled={currentPage === 1}
              onClick={() => setCurrentPage(currentPage - 1)}
            >
              이전
            </Button>

            {Array.from({ length: Math.min(totalPages, 5) }, (_, i) => {
              const page = currentPage <= 3 ? i + 1 : currentPage - 2 + i;
              if (page > totalPages) return null;

              return (
                <Button
                  key={page}
                  variant={currentPage === page ? 'primary' : 'outline'}
                  onClick={() => setCurrentPage(page)}
                >
                  {page}
                </Button>
              );
            })}

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
    </div>
  );
}