'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useSession } from 'next-auth/react';
import { Post, PostCategory, PostForm } from '@/types';
import Button from '@/components/ui/Button';
import Card from '@/components/ui/Card';

const CATEGORY_OPTIONS: { value: PostCategory; label: string }[] = [
  { value: 'QUESTION', label: '질문' },
  { value: 'GUIDE', label: '공략' },
  { value: 'GENERAL', label: '기타' },
];

interface EditPostPageProps {
  params: { id: string };
}

export default function EditPostPage({ params }: EditPostPageProps) {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [post, setPost] = useState<Post | null>(null);
  const [form, setForm] = useState<PostForm>({
    title: '',
    content: '',
    category: 'GENERAL',
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  // 게시글 데이터 로드
  const fetchPost = async () => {
    try {
      setLoading(true);
      const response = await fetch(`/api/posts/${params.id}`);
      const data = await response.json();

      if (data.success) {
        const postData = data.data;
        setPost(postData);
        setForm({
          title: postData.title,
          content: postData.content,
          category: postData.category,
        });
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

  // 로그인 및 권한 확인
  useEffect(() => {
    if (status === 'loading') return;
    
    if (!session) {
      router.push(`/auth/signin?callbackUrl=/posts/${params.id}/edit`);
      return;
    }

    if (post && session.user?.id && parseInt(session.user.id) !== post.authorId) {
      router.push(`/posts/${params.id}`);
      return;
    }
  }, [session, status, post, params.id, router]);

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

    setSaving(true);
    setError('');

    try {
      const response = await fetch(`/api/posts/${params.id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(form),
      });

      const data = await response.json();

      if (data.success) {
        router.push(`/posts/${params.id}`);
      } else {
        setError(data.error || '게시글 수정에 실패했습니다.');
      }
    } catch (err) {
      setError('게시글 수정에 실패했습니다.');
    } finally {
      setSaving(false);
    }
  };

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>
  ) => {
    const { name, value } = e.target;
    setForm(prev => ({ ...prev, [name]: value }));
    if (error) setError(''); // 에러 메시지 초기화
  };

  if (status === 'loading' || loading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto"></div>
          <p className="text-gray-600 mt-2">게시글을 불러오는 중...</p>
        </div>
      </div>
    );
  }

  if (error && !post) {
    return (
      <div className="min-h-screen bg-gray-50">
        <div className="max-w-4xl mx-auto px-4 py-8">
          <Card className="p-8 text-center">
            <h2 className="text-xl font-semibold text-gray-900 mb-4">
              게시글을 찾을 수 없습니다
            </h2>
            <p className="text-gray-600 mb-6">{error}</p>
            <Button onClick={() => router.back()}>
              이전으로
            </Button>
          </Card>
        </div>
      </div>
    );
  }

  if (!session) {
    return null; // 리다이렉트 중
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <div className="max-w-4xl mx-auto px-4 py-8">
        {/* 헤더 */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900">게시글 수정</h1>
          <p className="text-gray-600 mt-2">게시글을 수정해보세요</p>
        </div>

        <Card className="p-6">
          <form onSubmit={handleSubmit} className="space-y-6">
            {/* 카테고리 선택 */}
            <div>
              <label htmlFor="category" className="block text-sm font-medium text-gray-700 mb-2">
                카테고리
              </label>
              <select
                id="category"
                name="category"
                value={form.category}
                onChange={handleChange}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
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
              <label htmlFor="title" className="block text-sm font-medium text-gray-700 mb-2">
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
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
              <p className="text-sm text-gray-500 mt-1">
                {form.title.length}/200자
              </p>
            </div>

            {/* 내용 */}
            <div>
              <label htmlFor="content" className="block text-sm font-medium text-gray-700 mb-2">
                내용 *
              </label>
              <textarea
                id="content"
                name="content"
                value={form.content}
                onChange={handleChange}
                rows={15}
                placeholder="게시글 내용을 입력해주세요. 마크다운 문법을 사용할 수 있습니다."
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent resize-vertical"
              />
              <p className="text-sm text-gray-500 mt-1">
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
                disabled={saving}
                className="bg-blue-600 hover:bg-blue-700"
              >
                {saving ? (
                  <>
                    <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2"></div>
                    수정 중...
                  </>
                ) : (
                  '게시글 수정'
                )}
              </Button>
              
              <Button
                type="button"
                variant="outline"
                onClick={() => router.push(`/posts/${params.id}`)}
                disabled={saving}
              >
                취소
              </Button>
            </div>
          </form>
        </Card>

        {/* 미리보기 안내 */}
        <Card className="mt-6 p-4">
          <h3 className="font-medium text-gray-900 mb-2">수정 안내</h3>
          <ul className="text-sm text-gray-600 space-y-1">
            <li>• 수정된 게시글에는 "(수정됨)" 표시가 추가됩니다.</li>
            <li>• 카테고리 변경도 가능합니다.</li>
            <li>• 마크다운 문법을 사용하여 텍스트를 꾸밀 수 있습니다.</li>
          </ul>
        </Card>
      </div>
    </div>
  );
}