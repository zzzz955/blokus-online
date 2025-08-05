'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useSession } from 'next-auth/react';
import { PostCategory, PostForm } from '@/types';
import Button from '@/components/ui/Button';
import Card from '@/components/ui/Card';
import Layout from '@/components/layout/Layout';

const CATEGORY_OPTIONS: { value: PostCategory; label: string }[] = [
  { value: 'QUESTION', label: '질문' },
  { value: 'GUIDE', label: '공략' },
  { value: 'GENERAL', label: '기타' },
];

export default function WritePostPage() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [form, setForm] = useState<PostForm>({
    title: '',
    content: '',
    category: 'GENERAL',
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // 로그인 확인
  if (status === 'loading') {
    return (
      <Layout>
        <div className="flex items-center justify-center py-20">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-600"></div>
        </div>
      </Layout>
    );
  }

  if (!session) {
    router.push('/auth/signin?callbackUrl=/posts/write');
    return null;
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!form.title.trim()) {
      setError('제목을 입력해주세요.');
      return;
    }
    
    if (!form.content.trim()) {
      setError('내용을 입력해주세요.');
      return;
    }

    setLoading(true);
    setError('');

    try {
      const response = await fetch('/api/posts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(form),
      });

      const data = await response.json();

      if (data.success) {
        router.push(`/posts/${data.data.id}`);
      } else {
        setError(data.error || '게시글 작성에 실패했습니다.');
      }
    } catch (err) {
      setError('게시글 작성에 실패했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>
  ) => {
    const { name, value } = e.target;
    setForm(prev => ({ ...prev, [name]: value }));
    if (error) setError(''); // 에러 메시지 초기화
  };

  return (
    <Layout>
      <div className="max-w-4xl mx-auto px-4 py-8">
        {/* 헤더 */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-white">게시글 작성</h1>
          <p className="text-gray-300 mt-2">커뮤니티에 새로운 게시글을 작성해보세요</p>
        </div>

        <Card className="p-6">
          <form onSubmit={handleSubmit} className="space-y-6">
            {/* 카테고리 선택 */}
            <div>
              <label htmlFor="category" className="block text-sm font-medium text-white mb-2">
                카테고리
              </label>
              <select
                id="category"
                name="category"
                value={form.category}
                onChange={handleChange}
                className="w-full px-3 py-2 border border-dark-border bg-white text-gray-900 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
              >
                {CATEGORY_OPTIONS.map(option => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>

            {/* 제목 */}
            <div>
              <label htmlFor="title" className="block text-sm font-medium text-white mb-2">
                제목 *
              </label>
              <input
                type="text"
                id="title"
                name="title"
                value={form.title}
                onChange={handleChange}
                maxLength={200}
                placeholder="게시글 제목을 입력해주세요"
                className="w-full px-3 py-2 border border-dark-border bg-white text-gray-900 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
              />
              <p className="text-sm text-gray-400 mt-1">
                {form.title.length}/200자
              </p>
            </div>

            {/* 내용 */}
            <div>
              <label htmlFor="content" className="block text-sm font-medium text-white mb-2">
                내용 *
              </label>
              <textarea
                id="content"
                name="content"
                value={form.content}
                onChange={handleChange}
                rows={15}
                placeholder="게시글 내용을 입력해주세요. 마크다운 문법을 사용할 수 있습니다."
                className="w-full px-3 py-2 border border-dark-border bg-white text-gray-900 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent resize-vertical"
              />
              <p className="text-sm text-gray-400 mt-1">
                마크다운 문법을 지원합니다. (굵게: **텍스트**, 기울임: *텍스트*, 링크: [텍스트](URL))
              </p>
            </div>

            {/* 에러 메시지 */}
            {error && (
              <div className="p-4 bg-red-50 border border-red-200 rounded-lg">
                <p className="text-red-600 text-sm">{error}</p>
              </div>
            )}

            {/* 버튼들 */}
            <div className="flex gap-4 pt-4">
              <Button
                type="submit"
                disabled={loading}
                className="bg-primary-600 hover:bg-primary-700"
              >
                {loading ? (
                  <>
                    <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2"></div>
                    작성 중...
                  </>
                ) : (
                  '게시글 작성'
                )}
              </Button>
              
              <Button
                type="button"
                variant="outline"
                onClick={() => router.back()}
                disabled={loading}
              >
                취소
              </Button>
            </div>
          </form>
        </Card>

        {/* 도움말 */}
        <Card className="mt-6 p-4">
          <h3 className="font-medium text-white mb-2">작성 가이드</h3>
          <ul className="text-sm text-gray-300 space-y-1">
            <li>• <strong>질문:</strong> 게임 플레이, 규칙, 기술적 문제에 대한 질문</li>
            <li>• <strong>공략:</strong> 게임 전략, 팁, 가이드 공유</li>
            <li>• <strong>기타:</strong> 자유로운 주제의 게시글</li>
            <li>• 건전하고 예의 바른 내용으로 작성해주세요.</li>
            <li>• 스팸이나 광고성 게시글은 삭제될 수 있습니다.</li>
          </ul>
        </Card>
      </div>
    </Layout>
  );
}