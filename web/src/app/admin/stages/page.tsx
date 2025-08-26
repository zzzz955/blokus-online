'use client';

import { useEffect, useState } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import StageEditor from '@/components/admin/StageEditor';
import StageList from '@/components/admin/StageList';
import { BoardState, createEmptyBoardState } from '@/lib/board-state-codec';

interface Stage {
  stage_id: number;
  stage_number: number;
  difficulty: number;
  initial_board_state: BoardState; // Changed to int[] format
  available_blocks: number[];
  optimal_score: number;
  time_limit: number | null;
  max_undo_count: number;
  stage_description: string | null;
  stage_hints: string | null;
  thumbnail_url: string | null;
  is_active: boolean;
  is_featured: boolean;
  created_at: string;
  updated_at: string;
}

export default function StagesAdminPage() {
  const [stages, setStages] = useState<Stage[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editingStage, setEditingStage] = useState<Stage | null>(null);
  const [showEditor, setShowEditor] = useState(false);
  
  const searchParams = useSearchParams();
  const router = useRouter();

  useEffect(() => {
    const action = searchParams?.get('action');
    if (action === 'create') {
      handleCreateStage();
    } else {
      fetchStages();
    }
  }, [searchParams]);

  const fetchStages = async () => {
    try {
      setLoading(true);
      const response = await fetch('/api/admin/stages');
      if (response.ok) {
        const result = await response.json();
        if (result.success) {
          setStages(result.data.stages || []);
        } else {
          setError(result.message || '스테이지 조회 실패');
        }
      } else {
        setError(`API 호출 실패: ${response.status}`);
      }
    } catch (err) {
      setError('스테이지 조회 중 오류가 발생했습니다.');
      console.error('Stage fetch error:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateStage = () => {
    const newStage: Partial<Stage> = {
      stage_number: (stages.length > 0 ? Math.max(...stages.map(s => s.stage_number)) + 1 : 1),
      difficulty: 1,
      initial_board_state: createEmptyBoardState(), // Use new int[] format
      available_blocks: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21],
      optimal_score: 0,
      time_limit: null,
      max_undo_count: 3,
      stage_description: '',
      stage_hints: '',
      is_active: true,
      is_featured: false
    };
    
    setEditingStage(newStage as Stage);
    setShowEditor(true);
  };

  const handleEditStage = (stage: Stage) => {
    setEditingStage(stage);
    setShowEditor(true);
  };

  const handleCloneStage = (sourceStage: Stage) => {
    // 현재 최대 스테이지 번호 + 1 계산
    const maxStageNumber = stages.length > 0 ? Math.max(...stages.map(s => s.stage_number)) : 0;
    
    // 소스 스테이지 데이터를 복사하되 특정 필드는 수정
    const clonedStage: Partial<Stage> = {
      ...sourceStage,
      stage_id: undefined, // 새 스테이지이므로 ID 제거
      stage_number: maxStageNumber + 1, // 최대 번호 + 1
      thumbnail_url: null, // 썸네일 초기화
      created_at: undefined, // 새로 생성될 때의 시간으로 설정
      updated_at: undefined // 새로 생성될 때의 시간으로 설정
    };
    
    setEditingStage(clonedStage as Stage);
    setShowEditor(true);
  };

  const handleSaveStage = async (stageData: Partial<Stage>) => {
    try {
      const isNew = !stageData.stage_id;
      const url = isNew ? '/api/admin/stages' : `/api/admin/stages/${stageData.stage_id}`;
      const method = isNew ? 'POST' : 'PUT';

      const response = await fetch(url, {
        method,
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(stageData),
      });

      if (response.ok) {
        const result = await response.json();
        if (result.success) {
          setShowEditor(false);
          setEditingStage(null);
          fetchStages(); // Refresh the list
          
          // Show success message
          const successMessage = isNew 
            ? '스테이지가 성공적으로 생성되었습니다!' 
            : '스테이지가 성공적으로 수정되었습니다!';
          alert(successMessage);
        } else {
          setError(result.message || '스테이지 저장 실패');
        }
      } else {
        setError(`API 호출 실패: ${response.status}`);
      }
    } catch (err) {
      setError('스테이지 저장 중 오류가 발생했습니다.');
      console.error('Stage save error:', err);
    }
  };

  const handleDeleteStage = async (stageId: number) => {
    if (!confirm('정말로 이 스테이지를 삭제하시겠습니까?')) {
      return;
    }

    try {
      const response = await fetch(`/api/admin/stages/${stageId}`, {
        method: 'DELETE',
      });

      if (response.ok) {
        const result = await response.json();
        if (result.success) {
          fetchStages(); // Refresh the list
          alert('스테이지가 성공적으로 삭제되었습니다!');
        } else {
          setError(result.message || '스테이지 삭제 실패');
        }
      } else {
        setError(`API 호출 실패: ${response.status}`);
      }
    } catch (err) {
      setError('스테이지 삭제 중 오류가 발생했습니다.');
      console.error('Stage delete error:', err);
    }
  };

  const handleCloseEditor = () => {
    setShowEditor(false);
    setEditingStage(null);
    // Clear the action parameter from URL
    router.push('/admin/stages');
  };

  if (loading) {
    return (
      <div className="max-w-7xl mx-auto px-4 py-6 sm:px-6 lg:px-8">
        <div className="flex items-center justify-center h-64">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500"></div>
          <span className="ml-3 text-gray-400">스테이지 로딩 중...</span>
        </div>
      </div>
    );
  }

  if (showEditor && editingStage) {
    return (
      <StageEditor
        stage={editingStage}
        onSave={handleSaveStage}
        onCancel={handleCloseEditor}
      />
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 sm:px-6 lg:px-8">
      <div className="border-4 border-dashed border-dark-border rounded-lg p-6 bg-dark-card">
        {/* Header */}
        <div className="flex justify-between items-center mb-6">
          <div>
            <h1 className="text-2xl font-bold text-white">스테이지 관리</h1>
            <p className="text-gray-400 mt-1">싱글플레이어 스테이지를 생성하고 관리합니다</p>
          </div>
          <button
            onClick={handleCreateStage}
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg transition-colors flex items-center gap-2"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
            </svg>
            새 스테이지 생성
          </button>
        </div>

        {/* Error Display */}
        {error && (
          <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-4 mb-6">
            <div className="flex items-center">
              <svg className="w-5 h-5 text-red-400 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <span className="text-red-400">{error}</span>
            </div>
          </div>
        )}

        {/* Stage Statistics */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
          <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
            <div className="text-2xl font-bold text-white">{stages.length}</div>
            <div className="text-gray-400 text-sm">총 스테이지</div>
          </div>
          <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
            <div className="text-2xl font-bold text-green-400">{stages.filter(s => s.is_active).length}</div>
            <div className="text-gray-400 text-sm">활성 스테이지</div>
          </div>
          <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
            <div className="text-2xl font-bold text-blue-400">{stages.filter(s => s.is_featured).length}</div>
            <div className="text-gray-400 text-sm">추천 스테이지</div>
          </div>
          <div className="bg-dark-bg border border-dark-border rounded-lg p-4">
            <div className="text-2xl font-bold text-yellow-400">
              {stages.length > 0 ? Math.round(stages.reduce((sum, s) => sum + s.difficulty, 0) / stages.length * 10) / 10 : 0}
            </div>
            <div className="text-gray-400 text-sm">평균 난이도</div>
          </div>
        </div>

        {/* Stage List */}
        <StageList
          stages={stages}
          onEditStage={handleEditStage}
          onDeleteStage={handleDeleteStage}
          onCloneStage={handleCloneStage}
        />
      </div>
    </div>
  );
}