'use client';

import { useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { adminFetch } from '@/lib/admin-auth';
import RichTextEditor from '@/components/ui/RichTextEditor';

interface Announcement {
  id: number;
  title: string;
  content: string;
  author: string;
  created_at: string;
  updated_at: string;
  is_pinned: boolean;
  is_published: boolean;
}

interface AnnouncementForm {
  title: string;
  content: string;
  is_pinned: boolean;
  is_published: boolean;
}

export default function AdminAnnouncementsPage() {
  const [announcements, setAnnouncements] = useState<Announcement[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<AnnouncementForm>({
    title: '',
    content: '',
    is_pinned: false,
    is_published: true
  });
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const searchParams = useSearchParams();

  useEffect(() => {
    fetchAnnouncements();
  }, []);

  useEffect(() => {
    // URL 파라미터로 생성 모드 체크
    if (searchParams.get('action') === 'create') {
      setShowForm(true);
    }
  }, [searchParams]);

  const fetchAnnouncements = async () => {
    try {
      const response = await adminFetch('/api/admin/announcements');
      const data = await response.json();
      
      if (data.success) {
        setAnnouncements(data.data.announcements || []);
      } else {
        setError(data.error || '공지사항을 불러오는데 실패했습니다.');
      }
    } catch (error) {
      console.error('공지사항 조회 오류:', error);
      setError('서버 오류가 발생했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    try {
      const url = editingId 
        ? `/api/admin/announcements?id=${editingId}`
        : '/api/admin/announcements';
      
      const method = editingId ? 'PUT' : 'POST';

      const response = await adminFetch(url, {
        method,
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(form)
      });

      const data = await response.json();

      if (data.success) {
        setSuccess(data.message || '공지사항이 저장되었습니다.');
        setShowForm(false);
        setEditingId(null);
        setForm({
          title: '',
          content: '',
          is_pinned: false,
          is_published: true
        });
        fetchAnnouncements();
      } else {
        setError(data.error || '저장에 실패했습니다.');
      }
    } catch (error) {
      console.error('공지사항 저장 오류:', error);
      setError('서버 오류가 발생했습니다.');
    }
  };

  const handleEdit = (announcement: Announcement) => {
    setForm({
      title: announcement.title,
      content: announcement.content,
      is_pinned: announcement.is_pinned,
      is_published: announcement.is_published
    });
    setEditingId(announcement.id);
    setShowForm(true);
  };

  const handleDelete = async (id: number) => {
    if (!confirm('정말로 이 공지사항을 삭제하시겠습니까?')) {
      return;
    }

    try {
      const response = await adminFetch(`/api/admin/announcements?id=${id}`, {
        method: 'DELETE'
      });

      const data = await response.json();

      if (data.success) {
        setSuccess('공지사항이 삭제되었습니다.');
        fetchAnnouncements();
      } else {
        setError(data.error || '삭제에 실패했습니다.');
      }
    } catch (error) {
      console.error('공지사항 삭제 오류:', error);
      setError('서버 오류가 발생했습니다.');
    }
  };

  const resetForm = () => {
    setShowForm(false);
    setEditingId(null);
    setForm({
      title: '',
      content: '',
      is_pinned: false,
      is_published: true
    });
    setError('');
    setSuccess('');
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 sm:px-6 lg:px-8">
      <div className="border-4 border-dashed border-dark-border rounded-lg p-6 bg-dark-card">
        <div className="flex justify-between items-center mb-6">
          <h1 className="text-2xl font-bold text-white">공지사항 관리</h1>
          <button
            onClick={() => setShowForm(true)}
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-md text-sm font-medium"
          >
            새 공지사항
          </button>
        </div>

        {/* 알림 메시지 */}
        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded">
            {error}
          </div>
        )}
        
        {success && (
          <div className="mb-4 bg-green-50 border border-green-200 text-green-700 px-4 py-3 rounded">
            {success}
          </div>
        )}

        {/* 공지사항 폼 */}
        {showForm && (
          <div className="mb-6 bg-dark-card border border-dark-border p-6 rounded-lg shadow">
            <h2 className="text-lg font-medium mb-4">
              {editingId ? '공지사항 수정' : '새 공지사항 작성'}
            </h2>
            
            <form onSubmit={handleSubmit}>
              <div className="mb-4">
                <label className="block text-sm font-medium text-white mb-2">
                  제목
                </label>
                <input
                  type="text"
                  value={form.title}
                  onChange={(e) => setForm(prev => ({ ...prev, title: e.target.value }))}
                  className="w-full px-3 py-2 border text-black border-gray-300 rounded-md focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                  required
                  maxLength={200}
                />
              </div>

              <div className="mb-4">
                <label className="block text-sm font-medium text-white mb-2">
                  내용
                </label>
                <RichTextEditor
                  value={form.content}
                  onChange={(value) => setForm(prev => ({ ...prev, content: value }))}
                  placeholder="공지사항 내용을 입력해주세요. 다양한 서식을 사용할 수 있습니다."
                  height={250}
                  className="mb-2"
                />
              </div>

              <div className="mb-4 flex items-center space-x-6">
                <label className="flex items-center">
                  <input
                    type="checkbox"
                    checked={form.is_pinned}
                    onChange={(e) => setForm(prev => ({ ...prev, is_pinned: e.target.checked }))}
                    className="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
                  />
                  <span className="ml-2 text-sm text-white">상단 고정</span>
                </label>

                <label className="flex items-center">
                  <input
                    type="checkbox"
                    checked={form.is_published}
                    onChange={(e) => setForm(prev => ({ ...prev, is_published: e.target.checked }))}
                    className="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
                  />
                  <span className="ml-2 text-sm text-white">게시</span>
                </label>
              </div>

              <div className="flex space-x-3">
                <button
                  type="submit"
                  className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-md text-sm font-medium"
                >
                  {editingId ? '수정' : '작성'}
                </button>
                <button
                  type="button"
                  onClick={resetForm}
                  className="bg-gray-600 hover:bg-gray-700 text-white px-4 py-2 rounded-md text-sm font-medium"
                >
                  취소
                </button>
              </div>
            </form>
          </div>
        )}

        {/* 공지사항 목록 */}
        <div className="bg-dark-card border border-dark-border shadow rounded-lg">
          <div className="px-4 py-5 sm:p-6">
            {announcements.length === 0 ? (
              <div className="text-center py-12">
                <p className="text-gray-400">등록된 공지사항이 없습니다.</p>
              </div>
            ) : (
              <div className="space-y-4">
                {announcements.map((announcement) => (
                  <div key={announcement.id} className="border border-gray-600 rounded-lg p-4 bg-dark-bg">
                    <div className="flex justify-between items-start">
                      <div className="flex-1">
                        <div className="flex items-center space-x-2 mb-2">
                          <h3 className="text-lg font-medium text-white">
                            {announcement.title}
                          </h3>
                          {announcement.is_pinned && (
                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-yellow-100 text-yellow-800">
                              고정
                            </span>
                          )}
                          {!announcement.is_published && (
                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                              비공개
                            </span>
                          )}
                        </div>
                        {/* <p className="text-gray-600 text-sm mb-2 line-clamp-2">
                          {announcement.content.substring(0, 200)}...
                        </p> */}
                        <div className="text-xs text-gray-400">
                          작성자: {announcement.author} | 
                          작성일: {new Date(announcement.created_at).toLocaleString()}
                          {announcement.updated_at !== announcement.created_at && (
                            <> | 수정일: {new Date(announcement.updated_at).toLocaleString()}</>
                          )}
                        </div>
                      </div>
                      <div className="flex space-x-2 ml-4">
                        <button
                          onClick={() => handleEdit(announcement)}
                          className="text-blue-600 hover:text-blue-700 text-sm font-medium"
                        >
                          수정
                        </button>
                        <button
                          onClick={() => handleDelete(announcement.id)}
                          className="text-red-600 hover:text-red-700 text-sm font-medium"
                        >
                          삭제
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}