'use client';

import { useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';

interface PatchNote {
  id: number;
  version: string;
  title: string;
  content: string;
  release_date: string;
  download_url?: string;
  created_at: string;
}

interface PatchNoteForm {
  version: string;
  title: string;
  content: string;
  release_date: string;
  download_url: string;
}

export default function AdminPatchNotesPage() {
  const [patchNotes, setPatchNotes] = useState<PatchNote[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<PatchNoteForm>({
    version: '',
    title: '',
    content: '',
    release_date: '',
    download_url: ''
  });
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const searchParams = useSearchParams();

  useEffect(() => {
    fetchPatchNotes();
    
    // URL 파라미터로 생성 모드 체크
    if (searchParams.get('action') === 'create') {
      setShowForm(true);
    }
  }, [searchParams]);

  const fetchPatchNotes = async () => {
    try {
      const response = await fetch('/api/admin/patch-notes');
      const data = await response.json();
      
      if (data.success) {
        setPatchNotes(data.data.patchNotes || []);
      } else {
        setError(data.error || '패치노트를 불러오는데 실패했습니다.');
      }
    } catch (error) {
      console.error('패치노트 조회 오류:', error);
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
        ? `/api/admin/patch-notes?id=${editingId}`
        : '/api/admin/patch-notes';
      
      const method = editingId ? 'PUT' : 'POST';

      // 빈 download_url 처리
      const submitData = {
        ...form,
        download_url: form.download_url || undefined
      };

      const response = await fetch(url, {
        method,
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(submitData)
      });

      const data = await response.json();

      if (data.success) {
        setSuccess(data.message || '패치노트가 저장되었습니다.');
        setShowForm(false);
        setEditingId(null);
        setForm({
          version: '',
          title: '',
          content: '',
          release_date: '',
          download_url: ''
        });
        fetchPatchNotes();
      } else {
        setError(data.error || '저장에 실패했습니다.');
      }
    } catch (error) {
      console.error('패치노트 저장 오류:', error);
      setError('서버 오류가 발생했습니다.');
    }
  };

  const handleEdit = (patchNote: PatchNote) => {
    setForm({
      version: patchNote.version,
      title: patchNote.title,
      content: patchNote.content,
      release_date: new Date(patchNote.release_date).toISOString().split('T')[0],
      download_url: patchNote.download_url || ''
    });
    setEditingId(patchNote.id);
    setShowForm(true);
  };

  const handleDelete = async (id: number) => {
    if (!confirm('정말로 이 패치노트를 삭제하시겠습니까?')) {
      return;
    }

    try {
      const response = await fetch(`/api/admin/patch-notes?id=${id}`, {
        method: 'DELETE'
      });

      const data = await response.json();

      if (data.success) {
        setSuccess('패치노트가 삭제되었습니다.');
        fetchPatchNotes();
      } else {
        setError(data.error || '삭제에 실패했습니다.');
      }
    } catch (error) {
      console.error('패치노트 삭제 오류:', error);
      setError('서버 오류가 발생했습니다.');
    }
  };

  const resetForm = () => {
    setShowForm(false);
    setEditingId(null);
    setForm({
      version: '',
      title: '',
      content: '',
      release_date: '',
      download_url: ''
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
    <div className="px-4 py-6 sm:px-0">
      <div className="border-4 border-dashed border-gray-200 rounded-lg p-6">
        <div className="flex justify-between items-center mb-6">
          <h1 className="text-2xl font-bold text-gray-900">패치노트 관리</h1>
          <button
            onClick={() => setShowForm(true)}
            className="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-md text-sm font-medium"
          >
            새 패치노트
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

        {/* 패치노트 폼 */}
        {showForm && (
          <div className="mb-6 bg-white p-6 rounded-lg shadow">
            <h2 className="text-lg font-medium mb-4">
              {editingId ? '패치노트 수정' : '새 패치노트 작성'}
            </h2>
            
            <form onSubmit={handleSubmit}>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    버전 *
                  </label>
                  <input
                    type="text"
                    value={form.version}
                    onChange={(e) => setForm(prev => ({ ...prev, version: e.target.value }))}
                    placeholder="예: v1.0.0 또는 1.0.0"
                    className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-green-500 focus:border-green-500"
                    required
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    릴리즈 날짜 *
                  </label>
                  <input
                    type="date"
                    value={form.release_date}
                    onChange={(e) => setForm(prev => ({ ...prev, release_date: e.target.value }))}
                    className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-green-500 focus:border-green-500"
                    required
                  />
                </div>
              </div>

              <div className="mb-4">
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  제목 *
                </label>
                <input
                  type="text"
                  value={form.title}
                  onChange={(e) => setForm(prev => ({ ...prev, title: e.target.value }))}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-green-500 focus:border-green-500"
                  required
                  maxLength={200}
                />
              </div>

              <div className="mb-4">
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  다운로드 URL (선택사항)
                </label>
                <input
                  type="url"
                  value={form.download_url}
                  onChange={(e) => setForm(prev => ({ ...prev, download_url: e.target.value }))}
                  placeholder="https://github.com/user/repo/releases/download/..."
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-green-500 focus:border-green-500"
                />
              </div>

              <div className="mb-4">
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  내용 * (Markdown 지원)
                </label>
                <textarea
                  value={form.content}
                  onChange={(e) => setForm(prev => ({ ...prev, content: e.target.value }))}
                  rows={12}
                  placeholder="## 새로운 기능&#10;- 기능 1&#10;- 기능 2&#10;&#10;## 버그 수정&#10;- 수정 1&#10;- 수정 2"
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-green-500 focus:border-green-500"
                  required
                />
              </div>

              <div className="flex space-x-3">
                <button
                  type="submit"
                  className="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-md text-sm font-medium"
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

        {/* 패치노트 목록 */}
        <div className="bg-white shadow rounded-lg">
          <div className="px-4 py-5 sm:p-6">
            {patchNotes.length === 0 ? (
              <div className="text-center py-12">
                <p className="text-gray-500">등록된 패치노트가 없습니다.</p>
              </div>
            ) : (
              <div className="space-y-4">
                {patchNotes.map((patchNote) => (
                  <div key={patchNote.id} className="border border-gray-200 rounded-lg p-4">
                    <div className="flex justify-between items-start">
                      <div className="flex-1">
                        <div className="flex items-center space-x-2 mb-2">
                          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">
                            {patchNote.version}
                          </span>
                          <h3 className="text-lg font-medium text-gray-900">
                            {patchNote.title}
                          </h3>
                        </div>
                        <p className="text-gray-600 text-sm mb-2 line-clamp-3">
                          {patchNote.content.substring(0, 300)}...
                        </p>
                        <div className="text-xs text-gray-500">
                          릴리즈 날짜: {new Date(patchNote.release_date).toLocaleDateString()} | 
                          작성일: {new Date(patchNote.created_at).toLocaleString()}
                          {patchNote.download_url && (
                            <> | <a href={patchNote.download_url} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:text-blue-700">다운로드</a></>
                          )}
                        </div>
                      </div>
                      <div className="flex space-x-2 ml-4">
                        <button
                          onClick={() => handleEdit(patchNote)}
                          className="text-green-600 hover:text-green-700 text-sm font-medium"
                        >
                          수정
                        </button>
                        <button
                          onClick={() => handleDelete(patchNote.id)}
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